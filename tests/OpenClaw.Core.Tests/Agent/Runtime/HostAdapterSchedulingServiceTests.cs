using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

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
    public async Task GetMailboxSettingsAsync_Throws_DeferredNotSupported()
    {
        var client = new Mock<IHostAdapterClient>();

        var act = async () => await Service(client).GetMailboxSettingsAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<NotSupportedException>())
            .Which.Message.Should()
            .Contain("#74/#75");
    }

    [TestMethod]
    public async Task GetFreeBusyAsync_Throws_DeferredNotSupported()
    {
        var client = new Mock<IHostAdapterClient>();

        var act = async () =>
            await Service(client)
                .GetFreeBusyAsync(
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch.AddDays(1),
                    CancellationToken.None
                );

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [TestMethod]
    public async Task SendMailAsync_Throws_DeferredNotSupported()
    {
        var client = new Mock<IHostAdapterClient>();
        var request = new SendMailRequest(
            "Subject",
            "Body",
            "text",
            Array.Empty<AttendeeDto>(),
            Array.Empty<AttendeeDto>(),
            null
        );

        var act = async () => await Service(client).SendMailAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
