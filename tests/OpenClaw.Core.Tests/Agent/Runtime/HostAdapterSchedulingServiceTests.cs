using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;
using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;
using WireSendMailRequest = OpenClaw.HostAdapter.Contracts.SendMailRequest;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for the HostAdapter-backed scheduling service (OR-4, AC-9, AC-13).
/// </summary>
[TestClass]
public sealed class HostAdapterSchedulingServiceTests
{
    private static readonly ApiMeta Meta = new("req-1", "test", null);

    private static MessageDto SampleMessage() =>
        new(
            BridgeId: "msg-1",
            ItemKind: "meeting",
            Subject: "Sync",
            ReceivedUtc: DateTimeOffset.UnixEpoch,
            SentUtc: null,
            Importance: 1,
            Sensitivity: 0,
            Unread: true,
            HasAttachments: false,
            MessageClass: "IPM.Schedule.Meeting.Request",
            SenderName: "Sender",
            SenderEmail: "sender@contoso.com",
            ToJson: null,
            CcJson: null,
            BodyPreview: "Body",
            ProtectedFieldsAvailable: true,
            IsRedacted: false
        );

    private static EventDto SampleEvent() =>
        new(
            BridgeId: "evt-1",
            GlobalAppointmentId: "global-1",
            Subject: "Board",
            StartUtc: DateTimeOffset.UnixEpoch,
            EndUtc: DateTimeOffset.UnixEpoch.AddHours(1),
            Location: null,
            BusyStatus: 2,
            MeetingStatus: 1,
            IsRecurring: false,
            Sensitivity: 0,
            Organizer: "organizer@contoso.com",
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: "Agenda",
            ProtectedFieldsAvailable: true,
            IsRedacted: false
        );

    private static HostAdapterSchedulingService Service(Mock<IHostAdapterClient> client) =>
        new(client.Object, new SchedulingDtoMapper());

    [TestMethod]
    public async Task GetSchedulingMessageAsync_MapsMessage()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.GetMessageAsync("msg-1", It.IsAny<string?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ApiEnvelope<MessageDto>(true, SampleMessage(), Meta, null));

        var result = await Service(client)
            .GetSchedulingMessageAsync("msg-1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be("msg-1");
    }

    [TestMethod]
    public async Task GetSchedulingMessageAsync_NotOk_ReturnsNull()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.GetMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<MessageDto>(false, null, Meta, new ApiError("NOT_FOUND", "x"))
            );

        var result = await Service(client)
            .GetSchedulingMessageAsync("missing", CancellationToken.None);

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEventAsync_MapsEvent()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.GetEventAsync("evt-1", It.IsAny<string?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ApiEnvelope<EventDto>(true, SampleEvent(), Meta, null));

        var result = await Service(client).GetEventAsync("evt-1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be("evt-1");
    }

    [TestMethod]
    public async Task GetCalendarViewAsync_MapsEvents()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.ListCalendarWindowAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<ItemsResponse<EventDto>>(
                    true,
                    new ItemsResponse<EventDto>(new[] { SampleEvent() }),
                    Meta,
                    null
                )
            );

        var result = await Service(client)
            .GetCalendarViewAsync(
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddDays(1),
                CancellationToken.None
            );

        result.Should().ContainSingle();
    }

    [TestMethod]
    public async Task GetMailboxSettingsAsync_DelegatesToClient_ReturnsDto()
    {
        var client = new Mock<IHostAdapterClient>();
        var expected = new MailboxSettingsDto(
            "Pacific Standard Time",
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            new TimeOnly(8, 0),
            new TimeOnly(16, 0)
        );
        client
            .Setup(c =>
                c.GetMailboxSettingsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ApiEnvelope<MailboxSettingsDto>(true, expected, Meta, null));

        var result = await Service(client).GetMailboxSettingsAsync(CancellationToken.None);

        result.Should().Be(expected);
    }

    [TestMethod]
    public async Task GetMailboxSettingsAsync_WhenEnvelopeNotOk_ReturnsDocumentedDefaults()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.GetMailboxSettingsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new ApiEnvelope<MailboxSettingsDto>(
                    false,
                    null,
                    Meta,
                    new ApiError("DOWNSTREAM_FAILURE", "x")
                )
            );

        var result = await Service(client).GetMailboxSettingsAsync(CancellationToken.None);

        result.TimeZoneId.Should().Be("UTC");
        result
            .WorkingDays.Should()
            .Equal(
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            );
        result.WorkingHoursStart.Should().Be(new TimeOnly(9, 0));
        result.WorkingHoursEnd.Should().Be(new TimeOnly(17, 0));
    }

    [TestMethod]
    public async Task GetFreeBusyAsync_DelegatesToClient_ReturnsSchedule()
    {
        // Window values are derived from FakeTimeProvider, never wall-clock.
        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)
        );
        var start = timeProvider.GetUtcNow();
        var end = start.AddDays(5);
        var expected = new FreeBusyScheduleDto(
            "me",
            [new BusyIntervalDto(start, start.AddHours(1))]
        );
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.GetFreeBusyAsync(start, end, It.IsAny<string?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ApiEnvelope<FreeBusyScheduleDto>(true, expected, Meta, null));

        var result = await Service(client).GetFreeBusyAsync(start, end, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public async Task GetFreeBusyAsync_WhenEnvelopeNotOk_ReturnsEmptyIntervals()
    {
        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)
        );
        var start = timeProvider.GetUtcNow();
        var end = start.AddDays(5);
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.GetFreeBusyAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<FreeBusyScheduleDto>(
                    false,
                    null,
                    Meta,
                    new ApiError("DOWNSTREAM_FAILURE", "x")
                )
            );

        var result = await Service(client).GetFreeBusyAsync(start, end, CancellationToken.None);

        result.BusyIntervals.Should().BeEmpty();
    }

    private static SendMailRequest SampleSendRequest() =>
        new(
            "Re: Sync",
            "Proposed slots",
            "text",
            new[] { new AttendeeDto("Alice", "alice@contoso.com") },
            Array.Empty<AttendeeDto>(),
            null
        );

    private static void SetupSendMail(
        Mock<IHostAdapterClient> client,
        ApiEnvelope<object?> envelope
    ) =>
        client
            .Setup(c =>
                c.SendMailAsync(
                    It.IsAny<WireSendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(envelope);

    [TestMethod]
    public async Task SendMailAsync_Success_DelegatesToClientOnceWithCallerToken()
    {
        var client = new Mock<IHostAdapterClient>();
        SetupSendMail(client, new ApiEnvelope<object?>(true, null, Meta, null));
        using var cts = new CancellationTokenSource();

        await Service(client).SendMailAsync(SampleSendRequest(), null, cts.Token);

        client.Verify(
            c => c.SendMailAsync(It.IsAny<WireSendMailRequest>(), It.IsAny<string?>(), cts.Token),
            Times.Once
        );
    }

    [TestMethod]
    public async Task SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage()
    {
        var client = new Mock<IHostAdapterClient>();
        SetupSendMail(
            client,
            new ApiEnvelope<object?>(
                false,
                null,
                Meta,
                new ApiError("BRIDGE_UNAVAILABLE", "bridge offline")
            )
        );

        var act = async () =>
            await Service(client).SendMailAsync(SampleSendRequest(), null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should()
            .Contain("BRIDGE_UNAVAILABLE")
            .And.Contain("bridge offline");
    }

    [TestMethod]
    public async Task SendMailAsync_ClientThrows_PropagatesExceptionUnwrapped()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.SendMailAsync(
                    It.IsAny<WireSendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("socket closed"));

        var act = async () =>
            await Service(client).SendMailAsync(SampleSendRequest(), null, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task SendMailAsync_CanceledToken_PropagatesOperationCanceled()
    {
        var client = new Mock<IHostAdapterClient>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        client
            .Setup(c =>
                c.SendMailAsync(It.IsAny<WireSendMailRequest>(), It.IsAny<string?>(), cts.Token)
            )
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = async () =>
            await Service(client).SendMailAsync(SampleSendRequest(), null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task SendMailAsync_MapsAgentRequestToWireRequest()
    {
        var client = new Mock<IHostAdapterClient>();
        var captured = new List<WireSendMailRequest>();
        client
            .Setup(c =>
                c.SendMailAsync(
                    Capture.In(captured),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ApiEnvelope<object?>(true, null, Meta, null));
        var withCc = new SendMailRequest(
            "Re: Sync",
            "Proposed slots",
            "text",
            new[] { new AttendeeDto("Alice", "alice@contoso.com") },
            new[] { new AttendeeDto("Bob", "bob@contoso.com") },
            null
        );
        var withEmptyCc = SampleSendRequest();
        var service = Service(client);

        await service.SendMailAsync(withCc, null, CancellationToken.None);
        await service.SendMailAsync(withEmptyCc, null, CancellationToken.None);

        captured.Should().HaveCount(2);
        var first = captured[0];
        first.Message.Subject.Should().Be("Re: Sync");
        first.Message.Body.Should().Be(new SendMailBodyDto("text", "Proposed slots"));
        first
            .Message.ToRecipients.Should()
            .ContainSingle()
            .Which.EmailAddress.Should()
            .Be(new SendMailEmailAddressDto("alice@contoso.com", "Alice"));
        first
            .Message.CcRecipients.Should()
            .ContainSingle()
            .Which.EmailAddress.Should()
            .Be(new SendMailEmailAddressDto("bob@contoso.com", "Bob"));
        first.SaveToSentItems.Should().BeTrue();
        captured[1].Message.CcRecipients.Should().BeNull();
    }

    [TestMethod]
    public async Task SendMailAsync_SuppliedCorrelationId_ForwardsVerbatimAsRequestId()
    {
        // Arrange: capture the requestId argument the client receives (issue #107, D5).
        var client = new Mock<IHostAdapterClient>();
        var capturedRequestIds = new List<string?>();
        client
            .Setup(c =>
                c.SendMailAsync(
                    It.IsAny<WireSendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (WireSendMailRequest _, string? requestId, CancellationToken _) =>
                    capturedRequestIds.Add(requestId)
            )
            .ReturnsAsync(new ApiEnvelope<object?>(true, null, Meta, null));
        const string correlationId = "33333333-3333-3333-3333-333333333333";

        // Act
        await Service(client)
            .SendMailAsync(SampleSendRequest(), correlationId, CancellationToken.None);

        // Assert
        capturedRequestIds
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(correlationId, "the correlation id must be forwarded verbatim as the request id");
    }

    [TestMethod]
    public async Task SendMailAsync_NullCorrelationId_ForwardsNullRequestId()
    {
        // Arrange: a null correlation id preserves the client's self-generated request id.
        var client = new Mock<IHostAdapterClient>();
        var capturedRequestIds = new List<string?>();
        client
            .Setup(c =>
                c.SendMailAsync(
                    It.IsAny<WireSendMailRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (WireSendMailRequest _, string? requestId, CancellationToken _) =>
                    capturedRequestIds.Add(requestId)
            )
            .ReturnsAsync(new ApiEnvelope<object?>(true, null, Meta, null));

        // Act
        await Service(client).SendMailAsync(SampleSendRequest(), null, CancellationToken.None);

        // Assert
        capturedRequestIds
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeNull("a null correlation id lets the client self-generate the request id");
    }
}
