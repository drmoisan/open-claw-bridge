using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for the rewired <see cref="HostAdapterSchedulingService.GetEventForMessageAsync"/>
/// (issue #146). The method must invoke the dedicated linkage client method
/// <see cref="IHostAdapterClient.GetEventForMessageAsync"/> — not the plain
/// <c>GetEventAsync</c> stand-in — apply the <c>{ Ok:true, Data:not null }</c> guard to return the
/// mapped event on a linked hit, and return <see langword="null"/> on an <c>ok:true</c> /
/// <c>data:null</c> envelope so the pipeline degrades gracefully.
/// </summary>
[TestClass]
public sealed class HostAdapterSchedulingServiceLinkageTests
{
    private static readonly ApiMeta Meta = new("req-link", "test", null);

    private static EventDto SampleEvent() =>
        new(
            BridgeId: "evt-linked",
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
    public async Task GetEventForMessageAsync_LinkedHit_ReturnsMappedEvent()
    {
        // Arrange
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        client
            .Setup(c =>
                c.GetEventForMessageAsync(
                    "mtg:abc",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ApiEnvelope<EventDto>(true, SampleEvent(), Meta, null));

        // Act
        var result = await Service(client)
            .GetEventForMessageAsync("mtg:abc", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("evt-linked");
    }

    [TestMethod]
    public async Task GetEventForMessageAsync_OkNull_ReturnsNull()
    {
        // Arrange: a genuinely unlinked message resolves to an ok:true / data:null envelope.
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        client
            .Setup(c =>
                c.GetEventForMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ApiEnvelope<EventDto>(true, null, Meta, null));

        // Act
        var result = await Service(client)
            .GetEventForMessageAsync("mtg:unlinked", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GetEventForMessageAsync_InvokesLinkageMethod_NotGetEvent()
    {
        // Arrange
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);
        client
            .Setup(c =>
                c.GetEventForMessageAsync(
                    "mtg:abc",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ApiEnvelope<EventDto>(true, SampleEvent(), Meta, null));

        // Act
        await Service(client).GetEventForMessageAsync("mtg:abc", CancellationToken.None);

        // Assert: the linkage method is the one called; the plain event lookup is never used. With
        // MockBehavior.Strict, any GetEventAsync call would already have thrown; this makes the
        // contract explicit and fails if the code regresses to the messageId-as-eventId stand-in.
        client.Verify(
            c =>
                c.GetEventForMessageAsync(
                    "mtg:abc",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        client.Verify(
            c =>
                c.GetEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
