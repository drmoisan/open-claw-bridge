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

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for <see cref="HostAdapterSchedulingService.ProposeNewMeetingTimeAsync"/>
/// (issue #130). Kept in a separate file from <see cref="HostAdapterSchedulingServiceTests"/>
/// to hold each test file under the 500-line cap. Verifies delegation, correlation-id
/// forwarding, the failure-envelope throw contract, and the id guard clause.
/// </summary>
[TestClass]
public sealed class HostAdapterSchedulingServiceProposeNewTimeTests
{
    private static readonly ApiMeta Meta = new("req-1", "test", null);
    private static readonly DateTimeOffset ProposedStart = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ProposedEnd = new(2026, 7, 9, 14, 30, 0, TimeSpan.Zero);

    private static HostAdapterSchedulingService Service(Mock<IHostAdapterClient> client) =>
        new(client.Object, new SchedulingDtoMapper());

    private static ApiEnvelope<object?> OkEnvelope() => new(true, null, Meta, null);

    [TestMethod]
    public async Task ProposeNewMeetingTimeAsync_DelegatesEventIdAndTimesUnchanged()
    {
        var client = new Mock<IHostAdapterClient>();
        var capturedIds = new List<string>();
        var capturedStarts = new List<DateTimeOffset>();
        var capturedEnds = new List<DateTimeOffset>();
        client
            .Setup(c =>
                c.ProposeNewMeetingTimeAsync(
                    Capture.In(capturedIds),
                    Capture.In(capturedStarts),
                    Capture.In(capturedEnds),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OkEnvelope());

        await Service(client)
            .ProposeNewMeetingTimeAsync(
                "evt-1",
                ProposedStart,
                ProposedEnd,
                null,
                CancellationToken.None
            );

        capturedIds.Should().ContainSingle().Which.Should().Be("evt-1");
        capturedStarts.Should().ContainSingle().Which.Should().Be(ProposedStart);
        capturedEnds.Should().ContainSingle().Which.Should().Be(ProposedEnd);
    }

    [TestMethod]
    public async Task ProposeNewMeetingTimeAsync_ForwardsCorrelationIdAsRequestId()
    {
        var client = new Mock<IHostAdapterClient>();
        var capturedRequestIds = new List<string?>();
        client
            .Setup(c =>
                c.ProposeNewMeetingTimeAsync(
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
            .ReturnsAsync(OkEnvelope());
        const string correlationId = "55555555-5555-5555-5555-555555555555";

        await Service(client)
            .ProposeNewMeetingTimeAsync(
                "evt-1",
                ProposedStart,
                ProposedEnd,
                correlationId,
                CancellationToken.None
            );

        capturedRequestIds
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(correlationId, "the correlation id forwards verbatim as the adapter request id");
    }

    [TestMethod]
    public async Task ProposeNewMeetingTimeAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage()
    {
        var client = new Mock<IHostAdapterClient>();
        client
            .Setup(c =>
                c.ProposeNewMeetingTimeAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApiEnvelope<object?>(
                    false,
                    null,
                    Meta,
                    new ApiError("UNAUTHORIZED", "access denied", "ErrorAccessDenied", false)
                )
            );

        var act = async () =>
            await Service(client)
                .ProposeNewMeetingTimeAsync(
                    "evt-1",
                    ProposedStart,
                    ProposedEnd,
                    null,
                    CancellationToken.None
                );

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should()
            .Contain("Attendee propose-new-time failed")
            .And.Contain("UNAUTHORIZED")
            .And.Contain("access denied");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public async Task ProposeNewMeetingTimeAsync_NullOrEmptyEventId_FailsFastWithoutCallingClient(
        string? eventId
    )
    {
        var client = new Mock<IHostAdapterClient>(MockBehavior.Strict);

        var act = async () =>
            await Service(client)
                .ProposeNewMeetingTimeAsync(
                    eventId!,
                    ProposedStart,
                    ProposedEnd,
                    null,
                    CancellationToken.None
                );

        await act.Should().ThrowAsync<ArgumentException>();
        client.Verify(
            c =>
                c.ProposeNewMeetingTimeAsync(
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
