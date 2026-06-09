using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the working-hours policy projection (D4, AC-5, AC-6).
/// </summary>
[TestClass]
public sealed class WorkingHoursPolicyTests
{
    [TestMethod]
    public void FromOptions_ParsesBlocksDaysAndNotice()
    {
        var policy = WorkingHoursPolicy.FromOptions(
            new AgentPolicyOptions
            {
                NoMeetingBlocks = new[] { "12:00-13:00", "15:30-16:00" },
                MinNoticeMinutes = 90,
                PreferredDays = new[] { "Tuesday", "wednesday" },
            }
        );

        policy.NoMeetingBlocks.Should().HaveCount(2);
        policy.NoMeetingBlocks[0].Start.Should().Be(new TimeOnly(12, 0));
        policy.NoMeetingBlocks[1].End.Should().Be(new TimeOnly(16, 0));
        policy.MinNoticeMinutes.Should().Be(90);
        policy.PreferredDays.Should().Equal(DayOfWeek.Tuesday, DayOfWeek.Wednesday);
    }

    [TestMethod]
    public void FromOptions_MalformedBlock_Throws()
    {
        var act = () =>
            WorkingHoursPolicy.FromOptions(
                new AgentPolicyOptions { NoMeetingBlocks = new[] { "12:00" } }
            );

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void FromOptions_MalformedTime_Throws()
    {
        var act = () =>
            WorkingHoursPolicy.FromOptions(
                new AgentPolicyOptions { NoMeetingBlocks = new[] { "noon-1pm" } }
            );

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void FromOptions_InvalidDay_Throws()
    {
        var act = () =>
            WorkingHoursPolicy.FromOptions(
                new AgentPolicyOptions { PreferredDays = new[] { "Funday" } }
            );

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void FromOptions_Null_Throws()
    {
        var act = () => WorkingHoursPolicy.FromOptions(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
