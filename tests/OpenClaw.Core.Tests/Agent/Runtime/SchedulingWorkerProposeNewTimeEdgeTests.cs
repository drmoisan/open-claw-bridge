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
using Harness = OpenClaw.Core.Tests.Agent.Runtime.SchedulingWorkerRescheduleTests;
using Local = OpenClaw.Core.Tests.Agent.Runtime.SchedulingWorkerProposeNewTimeTests;
using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Overflow worker unit tests for the attendee propose-new-time path (issue #130), split
/// from <see cref="SchedulingWorkerProposeNewTimeTests"/> to hold each file under the
/// 500-line cap: dedupe skip (e), eligibility fail-closed matrix (f), mutual exclusivity
/// with the F18 organizer path in both directions (g), and send/F18 ActingFlags isolation
/// (h). Reuses the shared harness helpers and the F19 <c>Options</c>/<c>ProposeRecord</c>
/// helpers.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerProposeNewTimeEdgeTests
{
    // ---- (e) dedupe hit ----

    [TestMethod]
    public async Task DedupeHit_AuditsDedupeSkipped_NoWrite()
    {
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: false));
        var worker = Harness.Worker(
            service,
            Harness.Store(isRecorded: true),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            Local.Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        Local.ProposeRecord(captured).ResultCode.Should().Be(ActionAuditResultCode.DedupeSkipped);
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
    }

    // ---- (f) eligibility fail-closed matrix ----

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
            "organizer-owned" => (Harness.OneOnOneEvent(isOrganizer: true), null),
            "proposals-disallowed" => (
                Harness.OneOnOneEvent(isOrganizer: false) with
                {
                    AllowNewTimeProposals = false,
                },
                null
            ),
            "missing-times" => (
                Harness.OneOnOneEvent(isOrganizer: false) with
                {
                    Start = null,
                    End = null,
                },
                null
            ),
            "empty-id" => (Harness.OneOnOneEvent(isOrganizer: false) with { Id = "" }, null),
            "zero-slots" => (Harness.OneOnOneEvent(isOrganizer: false), FullyBusy()),
            _ => throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown case."),
        };

    [DataTestMethod]
    [DataRow("null-event")]
    [DataRow("organizer-owned")]
    [DataRow("proposals-disallowed")]
    [DataRow("missing-times")]
    [DataRow("empty-id")]
    [DataRow("zero-slots")]
    public async Task NoIntent_ProducesNoProposeAuditAndNoServiceCall(string label)
    {
        var (meetingEvent, freeBusy) = NoIntentFixture(label);
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(meetingEvent, freeBusy);
        var worker = Harness.Worker(
            service,
            Harness.Store(),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            Local.Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        captured
            .Should()
            .NotContain(
                r => r.ActionType == SentActionKey.AttendeeProposeNewTime,
                "a missing precondition yields no propose-new-time audit row"
            );
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
    }

    // ---- (g) mutual exclusivity with the F18 organizer path, both directions ----

    [TestMethod]
    public async Task OrganizerOwnedMessage_TriggersOnlyReschedule_ZeroProposeCalls()
    {
        // An organizer-owned message: the F18 reschedule evaluation runs (dry-run here) and
        // the F19 propose evaluation is silent because the intent requires IsOrganizer==false.
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: true));
        var worker = Harness.Worker(
            service,
            Harness.Store(),
            Harness.Audit(captured),
            Harness.MoveHistory(),
            // Reschedule dry-run (organizer flag off); propose flag ON to prove an organizer
            // message never triggers propose even when the propose gate is open.
            Local.Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        captured
            .Should()
            .Contain(
                r => r.ActionType == SentActionKey.OrganizerReschedule,
                "the organizer-owned message is handled by the F18 reschedule path"
            );
        captured
            .Should()
            .NotContain(
                r => r.ActionType == SentActionKey.AttendeeProposeNewTime,
                "an organizer-owned message never triggers the propose path"
            );
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
    }

    [TestMethod]
    public async Task AttendeeMessage_TriggersOnlyPropose_ZeroRescheduleCalls()
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
            Local.Options(calendarWriteEnabled: true, enableAttendeeProposeNewTime: true)
        );

        await worker.RunSchedulingCycleAsync(CancellationToken.None);

        captured
            .Should()
            .Contain(
                r => r.ActionType == SentActionKey.AttendeeProposeNewTime,
                "the attendee message is handled by the F19 propose path"
            );
        captured
            .Should()
            .NotContain(
                r => r.ActionType == SentActionKey.OrganizerReschedule,
                "an attendee message never triggers the reschedule path"
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

    // ---- (h) send-path and F18 reschedule-path ActingFlags isolation ----

    [TestMethod]
    public async Task SendAndReschedulePaths_PersistUnmodifiedActingFlags_AfterF19()
    {
        // Organizer-owned event so the F18 reschedule path also runs (dry-run); send enabled
        // so the send path runs too. F19 added a third path but must not widen either
        // existing ActingFlags builder.
        var captured = new List<ActionAuditRecord>();
        var service = Harness.Service(Harness.OneOnOneEvent(isOrganizer: true));
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
                "the send path's ActingFlags string is byte-identical to pre-F19"
            );
        var rescheduleRecord = captured
            .Should()
            .ContainSingle(r => r.ActionType == SentActionKey.OrganizerReschedule)
            .Subject;
        rescheduleRecord
            .ActingFlags.Should()
            .Be(
                "CalendarWriteEnabled=True;EnableOrganizerReschedule=False",
                "the F18 reschedule path's ActingFlags string is byte-identical to pre-F19"
            );
    }
}
