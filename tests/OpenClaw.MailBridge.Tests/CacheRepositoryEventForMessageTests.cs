using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Resolution tests for <see cref="CacheRepository.GetEventForMessageAsync"/> (issue #146). Verifies
/// the message-to-event linkage join: a linked meeting message resolves the matching event; ordinary
/// mail, a message row with no matching event, and an absent message row each resolve to a clean
/// <see langword="null"/>; and a recurring series (multiple events sharing one
/// <c>global_appointment_id</c>) resolves to the newest instance by <c>start_utc DESC</c>. Uses
/// in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CacheRepositoryEventForMessageTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 9, 9, 0, 0, TimeSpan.Zero);

    private static CacheRepository NewRepo() =>
        new($"Data Source=evt-for-msg-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");

    private static MessageDto BuildMeetingMessage(string entryId, string? linkedKey)
    {
        var bridgeId = BridgeIdCodec.MessageId(entryId, isMeeting: true);
        return new MessageDto(
            BridgeId: bridgeId,
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
    }

    private static EventDto BuildEvent(string bridgeSuffix, DateTimeOffset startUtc)
    {
        return new EventDto(
            BridgeId: $"evt:{bridgeSuffix}",
            GlobalAppointmentId: null,
            Subject: "Appointment",
            StartUtc: startUtc,
            EndUtc: startUtc.AddHours(1),
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
    }

    [TestMethod]
    public async Task GetEventForMessage_should_resolve_linked_event_for_a_meeting_message()
    {
        // Arrange
        using var repo = NewRepo();
        await repo.InitializeAsync();
        const string gaid = "clean-global-object-id-1";
        var message = BuildMeetingMessage("entry-linked", gaid);
        await repo.UpsertMessageAsync("entry-linked", "store-1", message);
        await repo.UpsertEventAsync("evt-entry-1", "store-1", gaid, BuildEvent("a", BaseTime));

        // Act
        var resolved = await repo.GetEventForMessageAsync(message.BridgeId);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.BridgeId.Should().Be("evt:a");
    }

    [TestMethod]
    public async Task GetEventForMessage_should_return_null_for_ordinary_mail_with_no_linkage_key()
    {
        // Arrange: a message row present but with a null linkage key (ordinary mail).
        using var repo = NewRepo();
        await repo.InitializeAsync();
        var message = BuildMeetingMessage("entry-plain", linkedKey: null) with
        {
            ItemKind = "mail",
        };
        await repo.UpsertMessageAsync("entry-plain", "store-1", message);

        // Act
        var resolved = await repo.GetEventForMessageAsync(message.BridgeId);

        // Assert
        resolved.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEventForMessage_should_return_null_when_linked_key_matches_no_event()
    {
        // Arrange: a linked key that no stored event carries.
        using var repo = NewRepo();
        await repo.InitializeAsync();
        var message = BuildMeetingMessage("entry-orphan", "no-such-gaid");
        await repo.UpsertMessageAsync("entry-orphan", "store-1", message);
        await repo.UpsertEventAsync(
            "evt-entry-2",
            "store-1",
            "different-gaid",
            BuildEvent("b", BaseTime)
        );

        // Act
        var resolved = await repo.GetEventForMessageAsync(message.BridgeId);

        // Assert
        resolved.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEventForMessage_should_return_null_when_message_row_is_absent()
    {
        // Arrange: no message row is inserted for the requested bridge id.
        using var repo = NewRepo();
        await repo.InitializeAsync();
        var absentBridgeId = BridgeIdCodec.MessageId("entry-absent", isMeeting: true);

        // Act
        var resolved = await repo.GetEventForMessageAsync(absentBridgeId);

        // Assert
        resolved.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEventForMessage_should_return_null_for_a_malformed_message_bridge_id()
    {
        // Arrange
        using var repo = NewRepo();
        await repo.InitializeAsync();

        // Act: a bridge id that is not a valid msg:/mtg: token is defensively treated as unlinked.
        var resolved = await repo.GetEventForMessageAsync("not-a-valid-id");

        // Assert
        resolved.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEventForMessage_should_select_the_newest_instance_for_a_recurring_series()
    {
        // Arrange: three events share one global_appointment_id (a recurring series).
        using var repo = NewRepo();
        await repo.InitializeAsync();
        const string gaid = "recurring-series-gaid";
        var message = BuildMeetingMessage("entry-recurring", gaid);
        await repo.UpsertMessageAsync("entry-recurring", "store-1", message);

        var oldest = BaseTime;
        var middle = BaseTime.AddDays(7);
        var newest = BaseTime.AddDays(14);
        await repo.UpsertEventAsync("evt-old", "store-1", gaid, BuildEvent("old", oldest));
        await repo.UpsertEventAsync("evt-new", "store-1", gaid, BuildEvent("new", newest));
        await repo.UpsertEventAsync("evt-mid", "store-1", gaid, BuildEvent("mid", middle));

        // Act
        var resolved = await repo.GetEventForMessageAsync(message.BridgeId);

        // Assert: the newest instance (latest start_utc) is chosen.
        resolved.Should().NotBeNull();
        resolved!.BridgeId.Should().Be("evt:new");
        resolved.StartUtc.Should().Be(newest);
    }
}
