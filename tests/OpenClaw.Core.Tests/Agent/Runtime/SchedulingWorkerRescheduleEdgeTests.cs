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
using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Overflow worker unit tests for the organizer-reschedule path (issue #128), split from
/// <see cref="SchedulingWorkerRescheduleTests"/> to hold each file under the 500-line cap:
/// success ordering (d), failure fail-closed (e), dedupe skip (f), no-intent silence (g),
/// and send-path ActingFlags isolation (h). Reuses the shared harness helpers.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerRescheduleEdgeTests
{
    // ---- (d) success ordering ----

    [TestMethod]
    public async Task Success_AuditsThenRecordsMoveThenDedupe_InOrder()
    {
        var order = new List<string>();
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent());
        service
            .Setup(s =>
                s.RescheduleEventAsync(
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
                    if (r.ActionType == SentActionKey.OrganizerReschedule)
                    {
                        order.Add("audit:" + r.ResultCode);
                    }
                }
            )
            .Returns(Task.CompletedTask);

        var recordedMoves = new List<(string Key, DateTimeOffset Start)>();
        var history = new Mock<ISeriesMoveHistory>();
        history
            .Setup(h =>
                h.GetMovedOccurrenceStartsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<DateTimeOffset>());
        history
            .Setup(h =>
                h.RecordMoveAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (string key, DateTimeOffset start, DateTimeOffset _, CancellationToken _) =>
                {
                    recordedMoves.Add((key, start));
                    order.Add("move");
                }
            )
            .Returns(Task.CompletedTask);

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
                Harness.Options(calendarWriteEnabled: true, enableOrganizerReschedule: true)
            ),
            new FakeTimeProvider(Harness.Now),
            NullLogger<SchedulingWorker>.Instance
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var record = Harness.RescheduleRecord(captured);
        record.ResultCode.Should().Be(ActionAuditResultCode.Rescheduled);
        record.EventId.Should().Be("evt-1");
        record.CorrelationId.Should().NotBeNullOrWhiteSpace();
        record.OriginalStartUtc.Should().Be(Harness.EventStart);
        record.NewStartUtc.Should().NotBeNull();
        order.Should().Equal("audit:rescheduled", "move", "dedupe");
        recordedMoves
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(("master-1", Harness.EventStart), "the pre-move occurrence start is recorded");
    }

    // ---- (e) failure fail-closed ----

    [TestMethod]
    public async Task Failure_AuditsRescheduleFailed_NoMoveOrDedupe()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent());
        service
            .Setup(s =>
                s.RescheduleEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new InvalidOperationException("Organizer reschedule failed: UNAUTHORIZED: x")
            );
        var history = Harness.MoveHistory();
        var store = Harness.Store();
        var worker = Harness.Worker(
            service,
            store,
            Harness.Audit(captured),
            history,
            Harness.Options(calendarWriteEnabled: true, enableOrganizerReschedule: true)
        );

        // Per-message isolation swallows the rethrown exception; the cycle completes.
        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var record = Harness.RescheduleRecord(captured);
        record.ResultCode.Should().Be(ActionAuditResultCode.RescheduleFailed);
        record.ErrorDetail.Should().Contain("UNAUTHORIZED");
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

    // ---- (f) dedupe hit ----

    [TestMethod]
    public async Task DedupeHit_AuditsDedupeSkipped_NoWrite()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent());
        var worker = Harness.Worker(
            service,
            Harness.Store(isRecorded: true),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            Harness.Options(calendarWriteEnabled: true, enableOrganizerReschedule: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        Harness
            .RescheduleRecord(captured)
            .ResultCode.Should()
            .Be(ActionAuditResultCode.DedupeSkipped);
        service.Verify(
            s =>
                s.RescheduleEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ---- (g) no-intent silence ----

    private static FreeBusyScheduleDto FullyBusy() =>
        new(
            "owner@contoso.com",
            new[]
            {
                new BusyIntervalDto(
                    SchedulingWorkerRescheduleTests.Now.AddDays(-1),
                    SchedulingWorkerRescheduleTests.Now.AddDays(7)
                ),
            }
        );

    /// <summary>
    /// Resolves a no-intent case label to its (event, free/busy) fixture. Labels avoid
    /// putting null literals into the <c>[DataRow]</c> object arrays.
    /// </summary>
    private static (SchedulingEventDto? Event, FreeBusyScheduleDto? FreeBusy) NoIntentFixture(
        string label
    ) =>
        label switch
        {
            "null-event" => (null, null),
            "non-organizer" => (Harness.OneOnOneEvent(isOrganizer: false), null),
            "missing-times" => (Harness.OneOnOneEvent() with { Start = null, End = null }, null),
            "empty-id" => (Harness.OneOnOneEvent() with { Id = "" }, null),
            "zero-slots" => (Harness.OneOnOneEvent(), FullyBusy()),
            _ => throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown case."),
        };

    [DataTestMethod]
    [DataRow("null-event")]
    [DataRow("non-organizer")]
    [DataRow("missing-times")]
    [DataRow("empty-id")]
    [DataRow("zero-slots")]
    public async Task NoIntent_ProducesNoRescheduleAuditAndNoServiceCall(string label)
    {
        var (meetingEvent, freeBusy) = NoIntentFixture(label);
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(meetingEvent, freeBusy);
        var worker = Harness.Worker(
            service,
            Harness.Store(),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            Harness.Options(calendarWriteEnabled: true, enableOrganizerReschedule: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        captured
            .Should()
            .NotContain(
                r => r.ActionType == SentActionKey.OrganizerReschedule,
                "a missing precondition yields no reschedule audit row"
            );
        service.Verify(
            s =>
                s.RescheduleEventAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ---- (h) send-path ActingFlags isolation ----

    [TestMethod]
    public async Task SendPath_PersistsUnmodifiedActingFlags_AlongsideRescheduleDryRun()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent());
        service
            .Setup(s =>
                s.SendMailAsync(
                    It.IsAny<SendMailRequest>(),
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
            // Send enabled; reschedule remains a dry-run so both paths run in one evaluation.
            Harness.Options(
                sendEnabled: true,
                calendarWriteEnabled: true,
                enableOrganizerReschedule: false
            )
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        var sendRecord = captured
            .Should()
            .ContainSingle(r => r.ActionType == SentActionKey.ProposalReply)
            .Subject;
        sendRecord
            .ActingFlags.Should()
            .Be(
                "SendEnabled=True;CalendarWriteEnabled=True",
                "the send path's ActingFlags string is byte-identical to pre-F18"
            );
    }
}
