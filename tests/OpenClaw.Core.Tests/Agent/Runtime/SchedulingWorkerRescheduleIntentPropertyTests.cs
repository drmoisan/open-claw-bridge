using System;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using Harness = OpenClaw.Core.Tests.Agent.Runtime.SchedulingWorkerRescheduleTests;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Property-based tests (CsCheck) for the two new pure functions in the reschedule path
/// (issue #128, T1 property-density obligation): the intent-computation helper
/// <see cref="SchedulingWorker.ComputeRescheduleIntent"/> and the flags-snapshot helper
/// <see cref="SchedulingWorker.BuildRescheduleActingFlags"/>. CsCheck prints the failing
/// seed on a <c>Sample</c> failure, satisfying the determinism print-seed requirement.
/// </summary>
[TestClass]
public sealed class SchedulingWorkerRescheduleIntentPropertyTests
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Gen<(int OrigMin, int DurMin, int NewMin)> GenTimes = Gen.Select(
        Gen.Int[0, 2_000_000],
        Gen.Int[1, 600],
        Gen.Int[0, 2_000_000]
    );

    private static NormalizedMeetingContext Context(bool isOrganizer, string? eventId) =>
        new(
            MailboxUpn: "owner@contoso.com",
            MessageId: "msg-1",
            ConversationId: "conv-1",
            EventId: eventId,
            Subject: "Weekly 1:1",
            BodyText: "body",
            MessageSender: "colleague@contoso.com",
            MessageFrom: "colleague@contoso.com",
            Organizer: "colleague@contoso.com",
            RequiredAttendees: Array.Empty<string>(),
            OptionalAttendees: Array.Empty<string>(),
            ResourceAttendees: Array.Empty<string>(),
            AllAttendees: Array.Empty<string>(),
            Categories: Array.Empty<string>(),
            IsMeetingMessage: true,
            IsOrganizer: isOrganizer,
            IsRecurring: true,
            IsOnlineMeeting: false,
            AllowNewTimeProposals: true,
            Sensitivity: "normal",
            ICalUId: null,
            SeriesMasterId: "master-1",
            ReceivedDateTime: null,
            LastModifiedDateTime: null
        );

    /// <summary>
    /// Duration preservation: for any valid original interval and any proposed slot, the
    /// computed target interval preserves the original duration and starts at the slot.
    /// </summary>
    [TestMethod]
    public void ComputeIntent_PreservesDuration()
    {
        GenTimes.Sample(
            t =>
            {
                var start = Base.AddMinutes(t.OrigMin);
                var end = start.AddMinutes(t.DurMin);
                var newStart = Base.AddMinutes(t.NewMin);
                var meetingEvent = Harness.OneOnOneEvent(start: start, end: end);
                var slots = new[] { new CandidateSlot(newStart, newStart.AddMinutes(30), "UTC") };

                var intent = SchedulingWorker.ComputeRescheduleIntent(
                    Context(isOrganizer: true, eventId: "evt-1"),
                    meetingEvent,
                    slots
                );

                intent.HasValue.Should().BeTrue();
                intent!.Value.NewStartUtc.Should().Be(newStart);
                (intent.Value.NewEndUtc - intent.Value.NewStartUtc).Should().Be(end - start);
            },
            iter: 1000
        );
    }

    /// <summary>
    /// Eligibility monotonicity: from any eligible input, removing the event, the organizer
    /// bit, the times, the event id, or all slots never yields an intent.
    /// </summary>
    [TestMethod]
    public void ComputeIntent_MissingPrecondition_NeverYieldsIntent()
    {
        GenTimes.Sample(
            t =>
            {
                var start = Base.AddMinutes(t.OrigMin);
                var end = start.AddMinutes(t.DurMin);
                var newStart = Base.AddMinutes(t.NewMin);
                var meetingEvent = Harness.OneOnOneEvent(start: start, end: end);
                var context = Context(isOrganizer: true, eventId: "evt-1");
                var slots = new[] { new CandidateSlot(newStart, newStart.AddMinutes(30), "UTC") };

                // Baseline eligible input yields an intent.
                SchedulingWorker
                    .ComputeRescheduleIntent(context, meetingEvent, slots)
                    .HasValue.Should()
                    .BeTrue();

                // Each removed precondition yields no intent.
                SchedulingWorker
                    .ComputeRescheduleIntent(context, null, slots)
                    .HasValue.Should()
                    .BeFalse();
                SchedulingWorker
                    .ComputeRescheduleIntent(
                        Context(isOrganizer: false, eventId: "evt-1"),
                        meetingEvent,
                        slots
                    )
                    .HasValue.Should()
                    .BeFalse();
                SchedulingWorker
                    .ComputeRescheduleIntent(
                        context,
                        meetingEvent with
                        {
                            Start = null,
                            End = null,
                        },
                        slots
                    )
                    .HasValue.Should()
                    .BeFalse();
                SchedulingWorker
                    .ComputeRescheduleIntent(
                        Context(isOrganizer: true, eventId: ""),
                        meetingEvent,
                        slots
                    )
                    .HasValue.Should()
                    .BeFalse();
                SchedulingWorker
                    .ComputeRescheduleIntent(context, meetingEvent, Array.Empty<CandidateSlot>())
                    .HasValue.Should()
                    .BeFalse();
            },
            iter: 1000
        );
    }

    /// <summary>
    /// Flags-snapshot round-trip: for any boolean pair, the snapshot string parses back to
    /// the exact input pair.
    /// </summary>
    [TestMethod]
    public void RescheduleActingFlags_RoundTripsBothBooleans()
    {
        Gen.Select(Gen.Bool, Gen.Bool)
            .Sample(
                flags =>
                {
                    var options = new AgentPolicyOptions
                    {
                        CalendarWriteEnabled = flags.Item1,
                        EnableOrganizerReschedule = flags.Item2,
                    };

                    var snapshot = SchedulingWorker.BuildRescheduleActingFlags(options);

                    var parts = snapshot.Split(';');
                    parts.Should().HaveCount(2);
                    var calendarWrite = bool.Parse(parts[0].Split('=')[1]);
                    var organizerReschedule = bool.Parse(parts[1].Split('=')[1]);
                    parts[0].Split('=')[0].Should().Be("CalendarWriteEnabled");
                    parts[1].Split('=')[0].Should().Be("EnableOrganizerReschedule");
                    calendarWrite.Should().Be(flags.Item1);
                    organizerReschedule.Should().Be(flags.Item2);
                },
                iter: 1000
            );
    }
}
