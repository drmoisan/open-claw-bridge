using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the calendar-write path flags (issue #109): default-off flag values on
/// <see cref="AgentPolicyOptions"/>, the exhaustive eight-row composition truth table for
/// <see cref="CalendarWritePolicy"/>, and fail-fast null-argument behavior of both predicates.
/// </summary>
[TestClass]
public sealed class CalendarWritePolicyTests
{
    // --- Defaults (spec AC-1, AC-U1) ---

    /// <summary>A freshly constructed options bag has both new path flags off.</summary>
    [TestMethod]
    public void Defaults_FreshOptions_BothPathFlagsAreFalse()
    {
        // Arrange / Act
        var options = new AgentPolicyOptions();

        // Assert
        options
            .EnableOrganizerReschedule.Should()
            .BeFalse("the organizer-reschedule path flag must default to off");
        options
            .EnableAttendeeProposeNewTime.Should()
            .BeFalse("the attendee propose-new-time path flag must default to off");
    }

    // --- Composition truth table (spec AC-2, Behavior table; all 8 combinations) ---

    /// <summary>
    /// Exhaustive truth table from the spec Behavior section: a path is allowed only when
    /// <c>CalendarWriteEnabled</c> AND its specific flag are both true, and the two paths
    /// are gated independently of each other.
    /// </summary>
    [DataTestMethod]
    [DataRow(false, false, false, false, false)]
    [DataRow(false, false, true, false, false)]
    [DataRow(false, true, false, false, false)]
    [DataRow(false, true, true, false, false)]
    [DataRow(true, false, false, false, false)]
    [DataRow(true, false, true, false, true)]
    [DataRow(true, true, false, true, false)]
    [DataRow(true, true, true, true, true)]
    public void TruthTable_AllEightCombinations_MatchSpecBehaviorTable(
        bool calendarWriteEnabled,
        bool enableOrganizerReschedule,
        bool enableAttendeeProposeNewTime,
        bool expectedOrganizerAllowed,
        bool expectedAttendeeAllowed
    )
    {
        // Arrange
        var options = new AgentPolicyOptions
        {
            CalendarWriteEnabled = calendarWriteEnabled,
            EnableOrganizerReschedule = enableOrganizerReschedule,
            EnableAttendeeProposeNewTime = enableAttendeeProposeNewTime,
        };

        // Act
        var organizerAllowed = CalendarWritePolicy.OrganizerRescheduleAllowed(options);
        var attendeeAllowed = CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options);

        // Assert
        organizerAllowed
            .Should()
            .Be(
                expectedOrganizerAllowed,
                "organizer reschedule requires CalendarWriteEnabled={0} AND EnableOrganizerReschedule={1}",
                calendarWriteEnabled,
                enableOrganizerReschedule
            );
        attendeeAllowed
            .Should()
            .Be(
                expectedAttendeeAllowed,
                "attendee propose-new-time requires CalendarWriteEnabled={0} AND EnableAttendeeProposeNewTime={1}",
                calendarWriteEnabled,
                enableAttendeeProposeNewTime
            );
    }

    // --- Configuration binding (spec AC-1, AC-U1; in-memory only, no temp files) ---

    /// <summary>Binding an empty <c>OpenClaw:AgentPolicy</c> section leaves both flags off.</summary>
    [TestMethod]
    public void Binding_EmptyAgentPolicySection_LeavesBothPathFlagsFalse()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var options = new AgentPolicyOptions();

        // Act
        configuration.GetSection("OpenClaw:AgentPolicy").Bind(options);

        // Assert
        options
            .EnableOrganizerReschedule.Should()
            .BeFalse("an empty configuration section must not turn the organizer flag on");
        options
            .EnableAttendeeProposeNewTime.Should()
            .BeFalse("an empty configuration section must not turn the attendee flag on");
    }

    /// <summary>The organizer key binds only the organizer property.</summary>
    [TestMethod]
    public void Binding_EnableOrganizerRescheduleTrue_BindsOnlyThatProperty()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenClaw:AgentPolicy:EnableOrganizerReschedule"] = "true",
                }
            )
            .Build();
        var options = new AgentPolicyOptions();

        // Act
        configuration.GetSection("OpenClaw:AgentPolicy").Bind(options);

        // Assert
        options
            .EnableOrganizerReschedule.Should()
            .BeTrue("the organizer key was set to true in configuration");
        options
            .EnableAttendeeProposeNewTime.Should()
            .BeFalse("the attendee key was not set and must remain independent");
    }

    /// <summary>The attendee key binds only the attendee property.</summary>
    [TestMethod]
    public void Binding_EnableAttendeeProposeNewTimeTrue_BindsOnlyThatProperty()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenClaw:AgentPolicy:EnableAttendeeProposeNewTime"] = "true",
                }
            )
            .Build();
        var options = new AgentPolicyOptions();

        // Act
        configuration.GetSection("OpenClaw:AgentPolicy").Bind(options);

        // Assert
        options
            .EnableAttendeeProposeNewTime.Should()
            .BeTrue("the attendee key was set to true in configuration");
        options
            .EnableOrganizerReschedule.Should()
            .BeFalse("the organizer key was not set and must remain independent");
    }

    // --- Fail-fast null guards ---

    /// <summary>The organizer predicate fails fast on null options.</summary>
    [TestMethod]
    public void OrganizerRescheduleAllowed_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var act = () => CalendarWritePolicy.OrganizerRescheduleAllowed(null!);

        // Act / Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    /// <summary>The attendee predicate fails fast on null options.</summary>
    [TestMethod]
    public void AttendeeProposeNewTimeAllowed_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var act = () => CalendarWritePolicy.AttendeeProposeNewTimeAllowed(null!);

        // Act / Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }
}
