using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Unit tests for the pure <see cref="FreeBusyProjection"/> (decision D1). The projection is
/// deterministic for a fixed event list and has no wall-clock dependency.
/// </summary>
[TestClass]
public sealed class FreeBusyProjectionTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-15T09:00:00Z");
    private static readonly DateTimeOffset End = DateTimeOffset.Parse("2026-06-15T10:00:00Z");

    [TestMethod]
    public void Busy_event_status_2_is_projected_as_busy_interval()
    {
        // Arrange
        var events = new[] { Event(Start, End, busyStatus: 2) };

        // Act
        var schedule = FreeBusyProjection.Project("me", events);

        // Assert
        schedule.MailboxUpn.Should().Be("me");
        schedule.BusyIntervals.Should().Equal(new BusyIntervalDto(Start, End));
    }

    [TestMethod]
    public void Tentative_status_1_and_out_of_office_status_3_are_projected_as_busy()
    {
        // Arrange
        var tentativeStart = DateTimeOffset.Parse("2026-06-15T11:00:00Z");
        var tentativeEnd = DateTimeOffset.Parse("2026-06-15T11:30:00Z");
        var oofStart = DateTimeOffset.Parse("2026-06-15T12:00:00Z");
        var oofEnd = DateTimeOffset.Parse("2026-06-15T13:00:00Z");
        var events = new[]
        {
            Event(tentativeStart, tentativeEnd, busyStatus: 1),
            Event(oofStart, oofEnd, busyStatus: 3),
        };

        // Act
        var schedule = FreeBusyProjection.Project("me", events);

        // Assert
        schedule
            .BusyIntervals.Should()
            .Equal(
                new BusyIntervalDto(tentativeStart, tentativeEnd),
                new BusyIntervalDto(oofStart, oofEnd)
            );
    }

    [TestMethod]
    public void Free_event_status_0_is_excluded()
    {
        // Arrange
        var events = new[] { Event(Start, End, busyStatus: 0) };

        // Act
        var schedule = FreeBusyProjection.Project("me", events);

        // Assert
        schedule.BusyIntervals.Should().BeEmpty();
    }

    [TestMethod]
    public void Null_busy_status_is_treated_as_busy()
    {
        // Arrange
        var events = new[] { Event(Start, End, busyStatus: null) };

        // Act
        var schedule = FreeBusyProjection.Project("me", events);

        // Assert
        schedule.BusyIntervals.Should().Equal(new BusyIntervalDto(Start, End));
    }

    [TestMethod]
    public void Empty_input_yields_empty_intervals()
    {
        // Act
        var schedule = FreeBusyProjection.Project("owner@contoso.com", Array.Empty<EventDto>());

        // Assert
        schedule.MailboxUpn.Should().Be("owner@contoso.com");
        schedule.BusyIntervals.Should().BeEmpty();
    }

    private static EventDto Event(DateTimeOffset start, DateTimeOffset end, int? busyStatus) =>
        new(
            BridgeId: "evt",
            GlobalAppointmentId: null,
            Subject: "Event",
            StartUtc: start,
            EndUtc: end,
            Location: null,
            BusyStatus: busyStatus,
            MeetingStatus: null,
            IsRecurring: false,
            Sensitivity: null,
            Organizer: null,
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: null,
            ProtectedFieldsAvailable: false,
            IsRedacted: true
        );
}
