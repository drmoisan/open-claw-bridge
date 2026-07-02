using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the pure composition predicates in
/// <see cref="CalendarWritePolicy"/> (issue #109, T1 module): the global kill switch
/// dominates both paths, and each path predicate is invariant under the other path's
/// flag. CsCheck prints the failing seed on a <c>Sample</c> failure, satisfying the
/// determinism print-seed requirement.
/// </summary>
[TestClass]
public sealed class CalendarWritePolicyPropertyTests
{
    private static readonly Gen<(
        bool CalendarWriteEnabled,
        bool EnableOrganizerReschedule,
        bool EnableAttendeeProposeNewTime
    )> GenFlagTriple = Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool);

    private static AgentPolicyOptions BuildOptions(
        bool calendarWriteEnabled,
        bool enableOrganizerReschedule,
        bool enableAttendeeProposeNewTime
    ) =>
        new()
        {
            CalendarWriteEnabled = calendarWriteEnabled,
            EnableOrganizerReschedule = enableOrganizerReschedule,
            EnableAttendeeProposeNewTime = enableAttendeeProposeNewTime,
        };

    /// <summary>
    /// Kill-switch dominance: for arbitrary path-flag combinations,
    /// <c>CalendarWriteEnabled == false</c> forces both predicates to false.
    /// </summary>
    [TestMethod]
    public void KillSwitchOff_ArbitraryPathFlags_BothPredicatesAreFalse()
    {
        Gen.Select(Gen.Bool, Gen.Bool)
            .Sample(
                pathFlags =>
                {
                    var options = BuildOptions(
                        calendarWriteEnabled: false,
                        enableOrganizerReschedule: pathFlags.Item1,
                        enableAttendeeProposeNewTime: pathFlags.Item2
                    );

                    CalendarWritePolicy
                        .OrganizerRescheduleAllowed(options)
                        .Should()
                        .BeFalse("the global kill switch is off");
                    CalendarWritePolicy
                        .AttendeeProposeNewTimeAllowed(options)
                        .Should()
                        .BeFalse("the global kill switch is off");
                },
                iter: 1000
            );
    }

    /// <summary>
    /// Path independence: <c>OrganizerRescheduleAllowed</c> is invariant under
    /// <c>EnableAttendeeProposeNewTime</c>.
    /// </summary>
    [TestMethod]
    public void OrganizerRescheduleAllowed_IsInvariantUnderAttendeeFlag()
    {
        GenFlagTriple.Sample(
            flags =>
            {
                var withFlag = BuildOptions(
                    flags.CalendarWriteEnabled,
                    flags.EnableOrganizerReschedule,
                    flags.EnableAttendeeProposeNewTime
                );
                var withFlagToggled = BuildOptions(
                    flags.CalendarWriteEnabled,
                    flags.EnableOrganizerReschedule,
                    !flags.EnableAttendeeProposeNewTime
                );

                CalendarWritePolicy
                    .OrganizerRescheduleAllowed(withFlag)
                    .Should()
                    .Be(
                        CalendarWritePolicy.OrganizerRescheduleAllowed(withFlagToggled),
                        "toggling the attendee flag must not change the organizer predicate"
                    );
            },
            iter: 1000
        );
    }

    /// <summary>
    /// Path independence: <c>AttendeeProposeNewTimeAllowed</c> is invariant under
    /// <c>EnableOrganizerReschedule</c>.
    /// </summary>
    [TestMethod]
    public void AttendeeProposeNewTimeAllowed_IsInvariantUnderOrganizerFlag()
    {
        GenFlagTriple.Sample(
            flags =>
            {
                var withFlag = BuildOptions(
                    flags.CalendarWriteEnabled,
                    flags.EnableOrganizerReschedule,
                    flags.EnableAttendeeProposeNewTime
                );
                var withFlagToggled = BuildOptions(
                    flags.CalendarWriteEnabled,
                    !flags.EnableOrganizerReschedule,
                    flags.EnableAttendeeProposeNewTime
                );

                CalendarWritePolicy
                    .AttendeeProposeNewTimeAllowed(withFlag)
                    .Should()
                    .Be(
                        CalendarWritePolicy.AttendeeProposeNewTimeAllowed(withFlagToggled),
                        "toggling the organizer flag must not change the attendee predicate"
                    );
            },
            iter: 1000
        );
    }
}
