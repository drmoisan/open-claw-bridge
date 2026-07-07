using System;
using System.Collections.Generic;
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
/// Unit tests for <see cref="HostAdapterSchedulingService.RescheduleEventAsync"/> (issue
/// #128). Kept in a separate file from <see cref="HostAdapterSchedulingServiceTests"/> to
/// hold each test file under the 500-line cap. Verifies delegation, correlation-id
/// forwarding, the failure-envelope throw contract, and the id guard clause.
/// </summary>
[TestClass]
public sealed class HostAdapterSchedulingServiceRescheduleTests
{
    private static readonly ApiMeta Meta = new("req-1", "test", null);
    private static readonly DateTimeOffset NewStart = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NewEnd = new(2026, 7, 9, 14, 30, 0, TimeSpan.Zero);

    private static HostAdapterSchedulingService Service(Mock<IHostAdapterClient> client) =>
        new(client.Object, new SchedulingDtoMapper());

    private static EventDto SampleEvent() =>
        new(
            BridgeId: "evt-1",
            GlobalAppointmentId: null,
            Subject: "Weekly 1:1",
            StartUtc: NewStart,
            EndUtc: NewEnd,
            Location: null,
            BusyStatus: 2,
            MeetingStatus: 1,
            IsRecurring: true,
            Sensitivity: 0,
            Organizer: "paula@contoso.com",
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: null,
            ProtectedFieldsAvailable: true,
            IsRedacted: false
        );

    [TestMethod]
    public async Task RescheduleEventAsync_DelegatesEventIdAndTimesUnchanged()
    {
        var client = new Mock<IHostAdapterClient>();
        var capturedIds = new List<string>();
        var capturedStarts = new List<DateTimeOffset>();
        var capturedEnds = new List<DateTimeOffset>();
        client
            .Setup(c =>
                c.UpdateEventTimesAsync(
                    Capture.In(capturedIds),
                    Capture.In(capturedStarts),
                    Capture.In(capturedEnds),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ApiEnvelope<EventDto>(true, SampleEvent(), Meta, null));

        await Service(client)
            .RescheduleEventAsync("evt-1", NewStart, NewEnd, null, CancellationToken.None);

        capturedIds.Should().ContainSingle().Which.Should().Be("evt-1");
        capturedStarts.Should().ContainSingle().Which.Should().Be(NewStart);
        capturedEnds.Should().ContainSingle().Which.Should().Be(NewEnd);
    }

    [TestMethod]
    public async Task RescheduleEventAsync_ForwardsCorrelationIdAsRequestId()
    {
        var client = new Mock<IHostAdapterClient>();
        var capturedRequestIds = new List<string?>();
        client
            .Setup(c =>
                c.UpdateEventTimesAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    string _,
                    DateTimeOffset _,
                    DateTimeOffset _,
                    string? requestId,
                    CancellationToken _
                ) => capturedRequestIds.Add(requestId)
            )
            .ReturnsAsync(new ApiEnvelope<EventDto>(true, SampleEvent(), Meta, null));
        const string correlationId = "44444444-4444-4444-4444-444444444444";

        await Service(client)
            .RescheduleEventAsync("evt-1", NewStart, NewEnd, correlationId, CancellationToken.None);

        capturedRequestIds
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(correlationId, "the correlation id forwards verbatim as the adapter request id");
    }

    [TestMethod]
    public async Task RescheduleEventAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.UpdateEventTimesAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<EventDto>(
                    false,
                    null,
                    Meta,
                    new ApiError("UNAUTHORIZED", "access denied", "ErrorAccessDenied", false)
                )
            );

        var act = async () =>
            await Service(client)
                .RescheduleEventAsync("evt-1", NewStart, NewEnd, null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should()
            .Contain("UNAUTHORIZED")
            .And.Contain("access denied");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public async Task RescheduleEventAsync_NullOrEmptyEventId_FailsFastWithoutCallingClient(
        string? eventId
    )
    {
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);

        var act = async () =>
            await Service(client)
                .RescheduleEventAsync(eventId!, NewStart, NewEnd, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        client.Verify(
            c =>
                c.UpdateEventTimesAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
