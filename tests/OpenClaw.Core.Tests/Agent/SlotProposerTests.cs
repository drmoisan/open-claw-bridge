using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic slot proposer (D4, AC-5) using
/// <see cref="FakeTimeProvider"/>. No wall-clock waits, sleeps, or temp files are used.
/// </summary>
[TestClass]
public sealed class SlotProposerTests
{
    // Monday 2026-06-08 08:00 UTC.
    private static readonly DateTimeOffset MondayMorning = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);

    private static readonly DayOfWeek[] WeekDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    ];

    private static MailboxSettingsDto Settings(string timeZoneId = "UTC") =>
        new(
            TimeZoneId: timeZoneId,
            WorkingDays: WeekDays,
            WorkingHoursStart: new TimeOnly(9, 0),
            WorkingHoursEnd: new TimeOnly(17, 0)
        );

    private static WorkingHoursPolicy Policy(
        IReadOnlyList<string>? noMeetingBlocks = null,
        int minNoticeMinutes = 0,
        IReadOnlyList<string>? preferredDays = null
    ) =>
        WorkingHoursPolicy.FromOptions(
            new AgentPolicyOptions
            {
                NoMeetingBlocks = noMeetingBlocks ?? Array.Empty<string>(),
                MinNoticeMinutes = minNoticeMinutes,
                PreferredDays = preferredDays ?? Array.Empty<string>(),
            }
        );

    private static SchedulingRequest Request(TimeSpan? horizon = null) =>
        new(
            Duration: TimeSpan.FromMinutes(30),
            RequestedPriority: OwnerPriority.P2,
            Horizon: horizon ?? TimeSpan.FromDays(1),
            RequesterEmail: "requester@contoso.com"
        );

    private static FreeBusyScheduleDto FreeBusy(params BusyIntervalDto[] busy) =>
        new("owner@contoso.com", busy);

    [TestMethod]
    public void ProposeTimes_GeneratesSlotsWithinWorkingHours()
    {
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var slots = SlotProposer.ProposeTimes(
            Request(),
            Settings(),
            FreeBusy(),
            Policy(),
            timeProvider
        );

        slots.Should().NotBeEmpty();
        slots.First().Start.Hour.Should().Be(9);
        slots
            .Should()
            .OnlyContain(s =>
                s.Start.TimeOfDay >= TimeSpan.FromHours(9)
                && s.End.TimeOfDay <= TimeSpan.FromHours(17)
            );
    }

    [TestMethod]
    public void ProposeTimes_ReturnsNoSlots_WhenEntireWindowIsBusy()
    {
        var timeProvider = new FakeTimeProvider(MondayMorning);
        var busy = FreeBusy(new BusyIntervalDto(MondayMorning, MondayMorning.AddDays(1)));

        var slots = SlotProposer.ProposeTimes(Request(), Settings(), busy, Policy(), timeProvider);

        slots.Should().BeEmpty();
    }

    [TestMethod]
    public void ProposeTimes_ExcludesSlotInsideNoMeetingBlock()
    {
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var slots = SlotProposer.ProposeTimes(
            Request(),
            Settings(),
            FreeBusy(),
            Policy(noMeetingBlocks: new[] { "09:00-12:00" }),
            timeProvider
        );

        slots.Should().NotBeEmpty();
        slots.Should().OnlyContain(s => s.Start.TimeOfDay >= TimeSpan.FromHours(12));
    }

    [TestMethod]
    public void ProposeTimes_ExcludesSlotsInsideMinNoticeWindow()
    {
        // now = 08:00; min notice 120 minutes => earliest start 10:00.
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var slots = SlotProposer.ProposeTimes(
            Request(),
            Settings(),
            FreeBusy(),
            Policy(minNoticeMinutes: 120),
            timeProvider
        );

        slots.Should().NotBeEmpty();
        slots.First().Start.Hour.Should().BeGreaterThanOrEqualTo(10);
    }

    [TestMethod]
    public void ProposeTimes_OrdersByDayPreference()
    {
        // Window spans Monday-Thursday; prefer Tue-Thu before Mon/Fri.
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var slots = SlotProposer.ProposeTimes(
            Request(horizon: TimeSpan.FromDays(4)),
            Settings(),
            FreeBusy(),
            Policy(preferredDays: new[] { "Tuesday", "Wednesday", "Thursday", "Monday", "Friday" }),
            timeProvider
        );

        slots.Should().NotBeEmpty();
        slots.First().Start.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
    }

    [TestMethod]
    public void ProposeTimes_NormalizesUtcInputToOwnerTimeZone()
    {
        // Eastern Standard Time is UTC-5 (no DST shift assumption needed for the offset
        // sign check). The proposed slot's offset must be negative, confirming the slot
        // is expressed in the owner zone rather than UTC.
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var slots = SlotProposer.ProposeTimes(
            Request(horizon: TimeSpan.FromDays(2)),
            Settings("Eastern Standard Time"),
            FreeBusy(),
            Policy(),
            timeProvider
        );

        slots.Should().NotBeEmpty();
        slots.First().TimeZoneId.Should().Be("Eastern Standard Time");
        slots.First().Start.Offset.Should().BeLessThan(TimeSpan.Zero);
    }

    [TestMethod]
    public void ProposeTimes_NullRequest_Throws()
    {
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var act = () =>
            SlotProposer.ProposeTimes(null!, Settings(), FreeBusy(), Policy(), timeProvider);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ProposeTimes_NonPositiveMaxResults_Throws()
    {
        var timeProvider = new FakeTimeProvider(MondayMorning);

        var act = () =>
            SlotProposer.ProposeTimes(
                Request(),
                Settings(),
                FreeBusy(),
                Policy(),
                timeProvider,
                maxResults: 0
            );

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
