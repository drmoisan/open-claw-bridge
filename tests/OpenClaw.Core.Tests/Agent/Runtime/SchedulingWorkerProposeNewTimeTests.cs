using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.HostAdapter.Contracts;
using Harness = OpenClaw.Core.Tests.Agent.Runtime.SchedulingWorkerRescheduleTests;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Worker unit tests for the attendee propose-new-time path (issue #130): the four-row gate
/// truth table (a), dry-run detail (b), success ordering (c), and failure fail-closed (d).
/// All time comes from <see cref="FakeTimeProvider"/>. The mocked Graph write is exercised
/// through the <see cref="ISchedulingService"/> seam. The shared harness helpers
/// (Message/OneOnOneEvent/Service/CandidateSource/MoveHistory/Store/Audit/Worker) are reused
/// from <see cref="SchedulingWorkerRescheduleTests"/>; the eligibility matrix, dedupe,
/// mutual-exclusivity, and path-isolation rows live in
/// <see cref="SchedulingWorkerProposeNewTimeEdgeTests"/> to hold each file under the
/// 500-line cap.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerProposeNewTimeTests
{
    /// <summary>
    /// F19 policy options: the propose-new-time flag composition. Uses the attendee flag
    /// (<c>EnableAttendeeProposeNewTime</c>) rather than the F18 organizer flag.
    /// </summary>
    internal static AgentPolicyOptions Options(
        bool sendEnabled = false,
        bool calendarWriteEnabled = false,
        bool enableAttendeeProposeNewTime = false
    ) =>
        new()
        {
            InternalDomains = new[] { "contoso.com" },
            InternalDomain = "contoso.com",
            SendEnabled = sendEnabled,
            CalendarWriteEnabled = calendarWriteEnabled,
            EnableAttendeeProposeNewTime = enableAttendeeProposeNewTime,
            CalendarViewFallbackDays = 0,
            NoMeetingBlocks = Array.Empty<string>(),
            MinNoticeMinutes = 0,
            PreferredDays = Array.Empty<string>(),
        };

    internal static ActionAuditRecord ProposeRecord(List<ActionAuditRecord> captured) =>
        captured
            .Should()
            .ContainSingle(r => r.ActionType == SentActionKey.AttendeeProposeNewTime)
            .Subject;

    // ---- (a) gate truth table ----

    [DataTestMethod]
    [DataRow(false, false, DisplayName = "both off")]
    [DataRow(false, true, DisplayName = "kill switch off")]
    [DataRow(true, false, DisplayName = "path flag off")]
    public async Task GateTruthTable_DisabledRow_DryRunDisabledWithNoWrite(
        bool calendarWriteEnabled,
        bool enableAttendeeProposeNewTime
    )
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: false));
        var history = Harness.MoveHistory();
        var store = Harness.Store();
        var worker = Harness.Worker(
            service,
            store,
            Harness.Audit(captured),
            history,
            Options(
                calendarWriteEnabled: calendarWriteEnabled,
                enableAttendeeProposeNewTime: enableAttendeeProposeNewTime
            )
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        ProposeRecord(captured)
            .ResultCode.Should()
            .Be(ActionAuditResultCode.ProposeNewTimeDisabled);
        service.Verify(
            s =>
                s.ProposeNewMeetingTimeAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        history.Verify(
            h =>
                h.RecordMoveAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        store.Verify(
            s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [TestMethod]
    public async Task GateTruthTable_BothFlagsOn_WritesExactlyOnce()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: false));
        service
            .Setup(s =>
                s.ProposeNewMeetingTimeAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        var worker = Harness.Worker(
            service,
            Harness.Store(),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        service.Verify(
            s =>
                s.ProposeNewMeetingTimeAsync(
                    "evt-1",
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ---- (b) dry-run detail ----

    [TestMethod]
    public async Task DryRun_Disabled_CarriesFourTimeColumnsAndProposeActingFlags()
    {
        var captured = new List<ActionAuditRecord>();
        var worker = Harness.Worker(
            Harness.Service(Harness.OneOnOneEvent(isOrganizer: false)),
            Harness.Store(),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: false)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var record = ProposeRecord(captured);
        record.ResultCode.Should().Be(ActionAuditResultCode.ProposeNewTimeDisabled);
        record.EventId.Should().Be("evt-1");
        record.OriginalStartUtc.Should().Be(Harness.EventStart);
        record.OriginalEndUtc.Should().Be(Harness.EventEnd);
        record.NewStartUtc.Should().NotBeNull();
        record.NewEndUtc.Should().NotBeNull();
        // Duration preserved from the original event.
        (record.NewEndUtc - record.NewStartUtc)
            .Should()
            .Be(Harness.EventEnd - Harness.EventStart);
        record
            .ActingFlags.Should()
            .Be(
                "CalendarWriteEnabled=True;EnableAttendeeProposeNewTime=False",
                "the propose path uses its own ActingFlags snapshot"
            );
    }

    // ---- (c) success ordering: audit proposed_new_time, then dedupe record ----

    [TestMethod]
    public async Task Success_AuditsProposedThenRecordsDedupe_InOrder_NoMove()
    {
        var order = new List<string>();
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: false));
        service
            .Setup(s =>
                s.ProposeNewMeetingTimeAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var audit = new Mock<IActionAuditLog>();
        audit
            .Setup(a => a.RecordAsync(Capture.In(captured), It.IsAny<CancellationToken>()))
            .Callback(
                (ActionAuditRecord r, CancellationToken _) =>
                {
                    if (r.ActionType == SentActionKey.AttendeeProposeNewTime)
                    {
                        order.Add("audit:" + r.ResultCode);
                    }
                }
            )
            .Returns(Task.CompletedTask);

        var history = Harness.MoveHistory();

        var store = new Mock<ISentActionStore>();
        store
            .Setup(s => s.IsRecordedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store
            .Setup(s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => order.Add("dedupe"))
            .Returns(Task.CompletedTask);

        var worker = new SchedulingWorker(
            service.Object,
            store.Object,
            audit.Object,
            Harness.CandidateSource("msg-1").Object,
            history.Object,
            Microsoft.Extensions.Options.Options.Create(
                Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
            ),
            new FakeTimeProvider(Harness.Now),
            NullLogger<SchedulingWorker>.Instance
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var record = ProposeRecord(captured);
        record.ResultCode.Should().Be(ActionAuditResultCode.ProposedNewTime);
        record.EventId.Should().Be("evt-1");
        record.CorrelationId.Should().NotBeNullOrWhiteSpace();
        record.OriginalStartUtc.Should().Be(Harness.EventStart);
        record.NewStartUtc.Should().NotBeNull();
        order.Should().Equal("audit:proposed_new_time", "dedupe");
        history.Verify(
            h =>
                h.RecordMoveAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "a proposal moves nothing; series_moves is never touched"
        );
    }

    // ---- (d) failure fail-closed ----

    [TestMethod]
    public async Task Failure_AuditsProposeNewTimeFailed_NoDedupe_NoMove()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: false));
        service
            .Setup(s =>
                s.ProposeNewMeetingTimeAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new InvalidOperationException("Attendee propose-new-time failed: UNAUTHORIZED: x")
            );
        var history = Harness.MoveHistory();
        var store = Harness.Store();
        var worker = Harness.Worker(
            service,
            store,
            Harness.Audit(captured),
            history,
            Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
        );

        // Per-message isolation swallows the rethrown exception; the cycle completes.
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var record = ProposeRecord(captured);
        record.ResultCode.Should().Be(ActionAuditResultCode.ProposeNewTimeFailed);
        record.ErrorDetail.Should().Contain("UNAUTHORIZED");
        store.Verify(
            s =>
                s.RecordAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        history.Verify(
            h =>
                h.RecordMoveAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
