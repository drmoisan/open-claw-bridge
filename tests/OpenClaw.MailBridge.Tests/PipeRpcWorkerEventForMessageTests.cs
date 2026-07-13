using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Dispatch tests for the <see cref="BridgeMethods.GetEventForMessage"/> RPC handler (issue #146),
/// exercised through <see cref="PipeRpcWorker.BuildResponseAsync"/>. Confirms the graceful-degradation
/// contract: a linked message yields <c>Success(id, event)</c>; an unlinked message yields
/// <c>Success(id, null)</c> (never <c>Failure(NOT_FOUND)</c>); a malformed message bridge id yields
/// <c>Failure(id, INVALID_REQUEST)</c>. Uses in-memory shared-cache SQLite so no temp files are used.
/// </summary>
[TestClass]
public sealed class PipeRpcWorkerEventForMessageTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 9, 9, 0, 0, TimeSpan.Zero);

    private static PipeRpcWorker NewWorker(IBridgeRepository repo)
    {
        var settings = BridgeSettings.Default;
        return new PipeRpcWorker(
            settings,
            new BridgeStateStore(settings),
            repo,
            NullLogger<PipeRpcWorker>.Instance
        );
    }

    private static string BuildPayload(string method, string messageBridgeId)
    {
        var request = new RpcRequest(
            "req-1",
            method,
            new Dictionary<string, string> { ["id"] = messageBridgeId }
        );
        return JsonSerializer.Serialize(
            request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
    }

    private static MessageDto BuildMeetingMessage(string entryId, string? linkedKey) =>
        new(
            BridgeId: BridgeIdCodec.MessageId(entryId, isMeeting: true),
            ItemKind: "meeting",
            Subject: "Linked meeting",
            ReceivedUtc: BaseTime,
            SentUtc: BaseTime.AddMinutes(-5),
            Importance: 1,
            Sensitivity: 0,
            Unread: true,
            HasAttachments: false,
            MessageClass: "IPM.Schedule.Meeting.Request",
            SenderName: "Sender",
            SenderEmail: "sender@contoso.com",
            ToJson: null,
            CcJson: null,
            BodyPreview: "preview",
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            MeetingMessageType: 0,
            LinkedGlobalAppointmentId: linkedKey
        );

    private static EventDto BuildEvent(string bridgeSuffix) =>
        new(
            BridgeId: $"evt:{bridgeSuffix}",
            GlobalAppointmentId: null,
            Subject: "Appointment",
            StartUtc: BaseTime,
            EndUtc: BaseTime.AddHours(1),
            Location: "Room",
            BusyStatus: 2,
            MeetingStatus: 1,
            IsRecurring: false,
            Sensitivity: 0,
            Organizer: "org@contoso.com",
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: "body",
            ProtectedFieldsAvailable: true,
            IsRedacted: false
        );

    [TestMethod]
    public async Task Handler_should_return_success_with_event_for_a_linked_message()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=handler-hit-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        const string gaid = "handler-gaid";
        var message = BuildMeetingMessage("entry-hit", gaid);
        await repo.UpsertMessageAsync("entry-hit", "store-1", message);
        await repo.UpsertEventAsync("evt-entry-1", "store-1", gaid, BuildEvent("hit"));
        var worker = NewWorker(repo);

        // Act
        var response = await worker.BuildResponseAsync(
            BuildPayload(BridgeMethods.GetEventForMessage, message.BridgeId)
        );

        // Assert
        response.Ok.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Result.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Handler_should_return_success_null_for_an_unlinked_message()
    {
        // Arrange: a message row with no linkage key (ordinary mail).
        using var repo = new CacheRepository(
            $"Data Source=handler-null-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var message = BuildMeetingMessage("entry-null", linkedKey: null) with { ItemKind = "mail" };
        await repo.UpsertMessageAsync("entry-null", "store-1", message);
        var worker = NewWorker(repo);

        // Act
        var response = await worker.BuildResponseAsync(
            BuildPayload(BridgeMethods.GetEventForMessage, message.BridgeId)
        );

        // Assert: clean not-linked success, never a NOT_FOUND failure.
        response.Ok.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Result.Should().BeNull();
    }

    [TestMethod]
    public async Task Handler_should_return_success_null_when_message_row_is_absent()
    {
        // Arrange: nothing is seeded for the requested (well-formed) message bridge id.
        using var repo = new CacheRepository(
            $"Data Source=handler-absent-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var worker = NewWorker(repo);
        var absentBridgeId = BridgeIdCodec.MessageId("entry-absent", isMeeting: true);

        // Act
        var response = await worker.BuildResponseAsync(
            BuildPayload(BridgeMethods.GetEventForMessage, absentBridgeId)
        );

        // Assert
        response.Ok.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Result.Should().BeNull();
    }

    [TestMethod]
    public async Task Handler_should_return_invalid_request_for_a_malformed_message_bridge_id()
    {
        // Arrange
        using var repo = new CacheRepository(
            $"Data Source=handler-bad-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var worker = NewWorker(repo);

        // Act: a bridge id that fails TryDecodeMessageId.
        var response = await worker.BuildResponseAsync(
            BuildPayload(BridgeMethods.GetEventForMessage, "not-a-valid-id")
        );

        // Assert
        response.Ok.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
    }
}
