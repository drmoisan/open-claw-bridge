using System;
using System.Collections.Generic;
using CsCheck;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the deterministic slot proposer (D4, AC-5, AC-12) using a
/// seeded <see cref="FakeTimeProvider"/>.
/// </summary>
[TestClass]
public sealed class SlotProposerPropertyTests
{
    // Monday 2026-06-08 06:00 UTC as the base "now".
    private static readonly DateTimeOffset BaseNow = new(2026, 6, 8, 6, 0, 0, TimeSpan.Zero);

    private static readonly TimeOnly WorkingStart = new(9, 0);
    private static readonly TimeOnly WorkingEnd = new(17, 0);

    private static readonly DayOfWeek[] WeekDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    ];

    [TestMethod]
    public void EveryReturnedSlot_RespectsWorkingHoursBlocksAndMinNotice()
    {
        var gen = Gen.Select(
            Gen.Int[0, 240], // min notice minutes
            Gen.Int[1, 5], // horizon days
            Gen.Int[30, 60] // duration minutes (multiple of 30 enforced below)
        );

        gen.Sample(
            t =>
            {
                var (minNotice, horizonDays, durationMinutes) = t;
                var duration = TimeSpan.FromMinutes((durationMinutes / 30) * 30 == 0 ? 30 : 30);
                var timeProvider = new FakeTimeProvider(BaseNow);
                var settings = new MailboxSettingsDto("UTC", WeekDays, WorkingStart, WorkingEnd);
                var policy = WorkingHoursPolicy.FromOptions(
                    new AgentPolicyOptions
                    {
                        NoMeetingBlocks = new[] { "12:00-13:00" },
                        MinNoticeMinutes = minNotice,
                        PreferredDays = Array.Empty<string>(),
                    }
                );
                var request = new SchedulingRequest(
                    duration,
                    OwnerPriority.P2,
                    TimeSpan.FromDays(horizonDays),
                    "requester@contoso.com"
                );
                var earliest = BaseNow.AddMinutes(minNotice);

                var slots = SlotProposer.ProposeTimes(
                    request,
                    settings,
                    new FreeBusyScheduleDto("owner@contoso.com", Array.Empty<BusyIntervalDto>()),
                    policy,
                    timeProvider
                );

                foreach (var slot in slots)
                {
                    // Inside working hours.
                    TimeOnly.FromTimeSpan(slot.Start.TimeOfDay).Should().BeOnOrAfter(WorkingStart);
                    TimeOnly.FromTimeSpan(slot.End.TimeOfDay).Should().BeOnOrBefore(WorkingEnd);

                    // Does not overlap the 12:00-13:00 no-meeting block.
                    var startsBeforeBlockEnd =
                        TimeOnly.FromTimeSpan(slot.Start.TimeOfDay) < new TimeOnly(13, 0);
                    var endsAfterBlockStart =
                        TimeOnly.FromTimeSpan(slot.End.TimeOfDay) > new TimeOnly(12, 0);
                    (startsBeforeBlockEnd && endsAfterBlockStart).Should().BeFalse();

                    // Respects minimum notice.
                    slot.Start.ToUniversalTime().Should().BeOnOrAfter(earliest);
                }
            },
            iter: 1000
        );
    }
}
