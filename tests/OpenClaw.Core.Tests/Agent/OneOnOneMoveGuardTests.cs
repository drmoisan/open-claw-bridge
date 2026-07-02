using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the pure one-on-one move guard (issue #105, AC-2/AC-3):
/// <see cref="OneOnOneMoveGuard.CanMove"/> truth table for 1:1 meetings,
/// <see cref="OneOnOneMoveGuard.ComputeAnswers"/> window and previous-week boundaries,
/// <see cref="OneOnOneMoveGuard.ResolveSeriesKey"/> fallback chain, and null-argument
/// guards.
/// </summary>
[TestClass]
public sealed class OneOnOneMoveGuardTests
{
    private const string Owner = "owner@contoso.com";
    private const string Requester = "requester@contoso.com";

    private static readonly OwnerSchedulingPolicy Policy = OwnerSchedulingPolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    /// <summary>Monday 2026-07-06 15:00 UTC; anchor 2026-07-06.</summary>
    private static readonly DateTimeOffset Candidate = new(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);

    /// <summary>A recurring meeting whose only non-organizer attendee is the owner (1:1).</summary>
    private static NormalizedMeetingContext OneOnOne() =>
        TestContextBuilder.Context(isRecurring: true, required: new[] { Owner });

    private static DateTimeOffset WeeklyStart(int weeksBeforeCandidate) =>
        Candidate.AddDays(-7 * weeksBeforeCandidate);

    // ----- (a) CanMove truth table for a ONE_ON_ONE context -----

    [TestMethod]
    [DataRow(0, false, true, DisplayName = "0 moves, not previous week -> allowed")]
    [DataRow(1, false, true, DisplayName = "1 move, not previous week -> allowed")]
    [DataRow(2, false, false, DisplayName = "2 moves, not previous week -> blocked")]
    [DataRow(0, true, false, DisplayName = "0 moves, previous week -> blocked")]
    [DataRow(1, true, false, DisplayName = "1 move, previous week -> blocked")]
    [DataRow(2, true, false, DisplayName = "2 moves, previous week -> blocked")]
    public void CanMove_OneOnOne_TruthTable(int moves, bool movedPreviousWeek, bool expected)
    {
        // Arrange
        var history = new SeriesMoveHistoryAnswers(moves, movedPreviousWeek);

        // Act
        var result = OneOnOneMoveGuard.CanMove(
            OneOnOne(),
            Owner,
            Requester,
            OwnerPriority.P2,
            Policy,
            history
        );

        // Assert
        result
            .Should()
            .Be(
                expected,
                "a 1:1 moves at most twice per six occurrences and never two weeks in a row"
            );
    }

    // ----- (b) window boundary at exactly six occurrences -----

    [TestMethod]
    public void ComputeAnswers_SeventhMostRecentOccurrence_IsOutsideWindow()
    {
        // Arrange: seven weekly occurrences; the window keeps the six greatest anchors,
        // so the seventh-most-recent (six weeks before the candidate) is excluded.
        var occurrences = new[]
        {
            WeeklyStart(6),
            WeeklyStart(5),
            WeeklyStart(4),
            WeeklyStart(3),
            WeeklyStart(2),
            WeeklyStart(1),
            WeeklyStart(0),
        };
        var moved = new[] { WeeklyStart(6), WeeklyStart(5) };

        // Act
        var answers = OneOnOneMoveGuard.ComputeAnswers(moved, occurrences, Candidate);

        // Assert
        answers
            .MovesInLastSixOccurrences.Should()
            .Be(
                1,
                "the move on the seventh-most-recent anchor is excluded and the move inside the window is counted"
            );
    }

    // ----- (c) fewer-than-six known occurrences -----

    [TestMethod]
    public void ComputeAnswers_FewerThanSixOccurrences_AllAnchorsFormWindow()
    {
        // Arrange: only two known occurrences plus the candidate; all three anchors form
        // the window, so all three moves are counted.
        var occurrences = new[] { WeeklyStart(2), WeeklyStart(1) };
        var moved = new[] { WeeklyStart(2), WeeklyStart(1), WeeklyStart(0) };

        // Act
        var answers = OneOnOneMoveGuard.ComputeAnswers(moved, occurrences, Candidate);

        // Assert
        answers
            .MovesInLastSixOccurrences.Should()
            .Be(3, "with fewer than six known anchors, every supplied anchor is in the window");
    }

    // ----- (d) previous-week boundaries -----

    [TestMethod]
    [DataRow(-7, true, DisplayName = "exactly candidate - 7 days -> true (closed lower bound)")]
    [DataRow(-8, false, DisplayName = "candidate - 8 days -> false (outside window)")]
    [DataRow(-1, true, DisplayName = "candidate - 1 day -> true")]
    [DataRow(0, false, DisplayName = "candidate's own anchor -> false (open upper bound)")]
    public void ComputeAnswers_PreviousWeekBoundaries(int dayOffset, bool expected)
    {
        // Arrange: MovedPreviousWeek uses the half-open interval
        // [candidateAnchor - 7 days, candidateAnchor).
        var moved = new[] { Candidate.AddDays(dayOffset) };

        // Act
        var answers = OneOnOneMoveGuard.ComputeAnswers(
            moved,
            Array.Empty<DateTimeOffset>(),
            Candidate
        );

        // Assert
        answers
            .MovedPreviousWeek.Should()
            .Be(
                expected,
                "the previous-week interval is [candidate anchor - 7 days, candidate anchor)"
            );
    }

    // ----- (e) same-UTC-day double move counts per move row -----

    [TestMethod]
    public void ComputeAnswers_TwoMovesOnSameAnchorDate_CountAsTwo()
    {
        // Arrange: two moved entries with distinct starts on the same UTC anchor date.
        // Window membership is per anchor, but moves are counted per move row.
        var occurrences = new[] { WeeklyStart(0) };
        var moved = new[]
        {
            new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero),
        };

        // Act
        var answers = OneOnOneMoveGuard.ComputeAnswers(moved, occurrences, Candidate);

        // Assert
        answers
            .MovesInLastSixOccurrences.Should()
            .Be(2, "each move row on an in-window anchor counts individually");
    }

    // ----- (f) occurrence anchors after the candidate are excluded from the window -----

    [TestMethod]
    public void ComputeAnswers_OccurrenceAnchorsAfterCandidate_AreExcludedFromWindow()
    {
        // Arrange: a future occurrence (one week after the candidate) must not enter the
        // window, so a move anchored on it is not counted.
        var occurrences = new[] { WeeklyStart(-1), WeeklyStart(0), WeeklyStart(1) };
        var moved = new[] { WeeklyStart(-1) };

        // Act
        var answers = OneOnOneMoveGuard.ComputeAnswers(moved, occurrences, Candidate);

        // Assert
        answers
            .MovesInLastSixOccurrences.Should()
            .Be(0, "anchors strictly greater than the candidate anchor are outside the window");
    }

    // ----- (g) ResolveSeriesKey fallback chain -----

    [TestMethod]
    public void ResolveSeriesKey_ReturnsSeriesMasterId_WhenPresent()
    {
        var meeting = TestContextBuilder.Context(isRecurring: true);

        OneOnOneMoveGuard
            .ResolveSeriesKey(meeting)
            .Should()
            .Be("series-1", "SeriesMasterId wins when present");
    }

    [TestMethod]
    public void ResolveSeriesKey_FallsBackToEventId_WhenSeriesMasterIdIsNull()
    {
        var meeting = TestContextBuilder.Context(isRecurring: true) with { SeriesMasterId = null };

        OneOnOneMoveGuard
            .ResolveSeriesKey(meeting)
            .Should()
            .Be("evt-1", "EventId is the fallback when SeriesMasterId is null");
    }

    [TestMethod]
    public void ResolveSeriesKey_FallsBackToEventId_WhenSeriesMasterIdIsEmpty()
    {
        var meeting = TestContextBuilder.Context(isRecurring: true) with { SeriesMasterId = "" };

        OneOnOneMoveGuard
            .ResolveSeriesKey(meeting)
            .Should()
            .Be("evt-1", "EventId is the fallback when SeriesMasterId is empty");
    }

    [TestMethod]
    [DataRow(null, null, DisplayName = "both null")]
    [DataRow("", "", DisplayName = "both empty")]
    public void ResolveSeriesKey_Throws_WhenBothIdsAreNullOrEmpty(
        string? seriesMasterId,
        string? eventId
    )
    {
        var meeting = TestContextBuilder.Context(isRecurring: true) with
        {
            SeriesMasterId = seriesMasterId,
            EventId = eventId,
        };

        var act = () => OneOnOneMoveGuard.ResolveSeriesKey(meeting);

        act.Should()
            .Throw<ArgumentException>("no stable series key exists")
            .WithParameterName("meeting");
    }

    // ----- (h) null-argument guards -----

    [TestMethod]
    public void ComputeAnswers_NullMovedList_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.ComputeAnswers(null!, Array.Empty<DateTimeOffset>(), Candidate);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ComputeAnswers_NullOccurrenceList_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.ComputeAnswers(Array.Empty<DateTimeOffset>(), null!, Candidate);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CanMove_NullMeeting_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.CanMove(
                null!,
                Owner,
                Requester,
                OwnerPriority.P2,
                Policy,
                new SeriesMoveHistoryAnswers(0, false)
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CanMove_NullOwnerEmail_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.CanMove(
                OneOnOne(),
                null!,
                Requester,
                OwnerPriority.P2,
                Policy,
                new SeriesMoveHistoryAnswers(0, false)
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CanMove_NullRequesterEmail_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.CanMove(
                OneOnOne(),
                Owner,
                null!,
                OwnerPriority.P2,
                Policy,
                new SeriesMoveHistoryAnswers(0, false)
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CanMove_NullPolicy_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.CanMove(
                OneOnOne(),
                Owner,
                Requester,
                OwnerPriority.P2,
                null!,
                new SeriesMoveHistoryAnswers(0, false)
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CanMove_NullHistory_Throws()
    {
        var act = () =>
            OneOnOneMoveGuard.CanMove(
                OneOnOne(),
                Owner,
                Requester,
                OwnerPriority.P2,
                Policy,
                null!
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ResolveSeriesKey_NullMeeting_Throws()
    {
        var act = () => OneOnOneMoveGuard.ResolveSeriesKey(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
