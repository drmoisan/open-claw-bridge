using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the pure one-on-one move guard (issue #105, AC-2/AC-3):
/// monotonicity of the 1:1 decision in the move history, delegation equivalence to
/// <see cref="MovePolicy.CanMove"/> for non-1:1 kinds, invariance of the window count
/// under moves older than the six-anchor window, and an independent previous-week
/// membership oracle. CsCheck prints the failing seed on a <c>Sample</c> failure,
/// satisfying the determinism print-seed requirement.
/// </summary>
[TestClass]
public sealed class OneOnOneMoveGuardPropertyTests
{
    private const string Owner = "owner@contoso.com";
    private const string Organizer = "organizer@contoso.com";

    private static readonly OwnerSchedulingPolicy Policy = OwnerSchedulingPolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    /// <summary>Monday 2026-07-06 15:00 UTC; anchor 2026-07-06.</summary>
    private static readonly DateTimeOffset Candidate = new(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);

    /// <summary>A recurring meeting whose only non-organizer attendee is the owner (1:1).</summary>
    private static NormalizedMeetingContext OneOnOne() =>
        TestContextBuilder.Context(isRecurring: true, required: new[] { Owner });

    /// <summary>A timestamp within 60 days before (or on) the candidate, any hour.</summary>
    private static readonly Gen<DateTimeOffset> GenNearbyInstant = Gen.Select(
            Gen.Int[-60, 0],
            Gen.Int[0, 23]
        )
        .Select(t => Candidate.AddDays(t.Item1).AddHours(t.Item2 - 15));

    // ----- (a) monotonicity: one more moved entry never flips blocked -> allowed -----

    [TestMethod]
    public void CanMove_OneOnOne_IsMonotonicallyBlockingInMoveHistory()
    {
        Gen.Select(GenNearbyInstant.List[0, 8], GenNearbyInstant.List[0, 8], GenNearbyInstant)
            .Sample(
                t =>
                {
                    var (moved, occurrences, extraMove) = t;

                    var before = OneOnOneMoveGuard.CanMove(
                        OneOnOne(),
                        Owner,
                        "requester@contoso.com",
                        OwnerPriority.P2,
                        Policy,
                        OneOnOneMoveGuard.ComputeAnswers(moved, occurrences, Candidate)
                    );
                    var after = OneOnOneMoveGuard.CanMove(
                        OneOnOne(),
                        Owner,
                        "requester@contoso.com",
                        OwnerPriority.P2,
                        Policy,
                        OneOnOneMoveGuard.ComputeAnswers(
                            moved.Append(extraMove).ToList(),
                            occurrences,
                            Candidate
                        )
                    );

                    (before || !after)
                        .Should()
                        .BeTrue(
                            "appending a moved occurrence must never convert a blocked decision into an allowed one"
                        );
                },
                iter: 1000
            );
    }

    // ----- (b) delegation equivalence for non-ONE_ON_ONE kinds -----

    private static readonly Gen<string> GenOtherAttendee = Gen.OneOfConst(
        "alice@contoso.com",
        "bob@contoso.com",
        "carol@contoso.com",
        "dave@contoso.com",
        "erin@contoso.com",
        "frank@contoso.com",
        "ceo@contoso.com"
    );

    private static readonly Gen<string> GenRequester = Gen.OneOfConst(
        Owner,
        Organizer,
        "requester@contoso.com"
    );

    private static readonly Gen<OwnerPriority> GenPriority = Gen.OneOfConst(
        OwnerPriority.P0,
        OwnerPriority.P1,
        OwnerPriority.P2,
        OwnerPriority.P3
    );

    private static readonly Gen<SeriesMoveHistoryAnswers> GenHistory = Gen.Select(
            Gen.Int[0, 5],
            Gen.Bool
        )
        .Select(t => new SeriesMoveHistoryAnswers(t.Item1, t.Item2));

    [TestMethod]
    public void CanMove_NonOneOnOneKinds_DelegateToMovePolicyUnchanged()
    {
        Gen.Select(
                Gen.Bool, // IsRecurring
                Gen.Int[0, 7], // number of extra attendees
                Gen.Bool, // include the owner among the attendees
                GenOtherAttendee.List[0, 7],
                GenRequester,
                GenPriority,
                GenHistory
            )
            .Sample(
                t =>
                {
                    var (
                        isRecurring,
                        extraCount,
                        includeOwner,
                        pool,
                        requester,
                        priority,
                        history
                    ) = t;

                    var attendees = new List<string> { Organizer };
                    if (includeOwner)
                    {
                        attendees.Add(Owner);
                    }
                    foreach (var email in pool.Distinct().Take(extraCount))
                    {
                        attendees.Add(email);
                    }

                    var meeting = TestContextBuilder.Context(
                        isRecurring: isRecurring,
                        organizer: Organizer,
                        required: attendees
                    );

                    var kind = RecurringMeetingClassifier.Classify(meeting, Owner);
                    if (kind == RecurringMeetingKind.ONE_ON_ONE)
                    {
                        return; // property targets the delegation partition only
                    }

                    var guardResult = OneOnOneMoveGuard.CanMove(
                        meeting,
                        Owner,
                        requester,
                        priority,
                        Policy,
                        history
                    );
                    var policyResult = MovePolicy.CanMove(
                        meeting,
                        Owner,
                        requester,
                        priority,
                        Policy
                    );

                    guardResult
                        .Should()
                        .Be(
                            policyResult,
                            "non-1:1 kinds must delegate to MovePolicy.CanMove regardless of history"
                        );
                },
                iter: 1000
            );
    }

    // ----- (c) moves anchored strictly older than the six-anchor window are inert -----

    [TestMethod]
    public void ComputeAnswers_MovesOlderThanWindow_DoNotChangeWindowCount()
    {
        // Weekly occurrence grid: with at least six weekly occurrences at or before the
        // candidate, the window is weeks 0..5; anchors at week >= 6 are strictly older.
        var genInWindowMoves = Gen.Int[0, 5]
            .List[0, 6]
            .Select(weeks => weeks.Select(w => Candidate.AddDays(-7 * w)).ToList());
        var genOlderMoves = Gen.Int[6, 20]
            .List[1, 6]
            .Select(weeks => weeks.Select(w => Candidate.AddDays(-7 * w)).ToList());

        Gen.Select(Gen.Int[6, 12], genInWindowMoves, genOlderMoves)
            .Sample(
                t =>
                {
                    var (occurrenceCount, inWindowMoves, olderMoves) = t;
                    var occurrences = Enumerable
                        .Range(0, occurrenceCount)
                        .Select(w => Candidate.AddDays(-7 * w))
                        .ToList();

                    var baseline = OneOnOneMoveGuard.ComputeAnswers(
                        inWindowMoves,
                        occurrences,
                        Candidate
                    );
                    var withOlder = OneOnOneMoveGuard.ComputeAnswers(
                        inWindowMoves.Concat(olderMoves).ToList(),
                        occurrences,
                        Candidate
                    );

                    withOlder
                        .MovesInLastSixOccurrences.Should()
                        .Be(
                            baseline.MovesInLastSixOccurrences,
                            "moves anchored strictly older than the six-anchor window are outside the count"
                        );
                },
                iter: 1000
            );
    }

    // ----- (d) previous-week membership matches an independent oracle -----

    [TestMethod]
    public void ComputeAnswers_MovedPreviousWeek_MatchesHalfOpenIntervalOracle()
    {
        Gen.Select(GenNearbyInstant.List[0, 8], GenNearbyInstant.List[0, 8])
            .Sample(
                t =>
                {
                    var (moved, occurrences) = t;

                    var answers = OneOnOneMoveGuard.ComputeAnswers(moved, occurrences, Candidate);

                    // Independent oracle over the generated anchors: true iff any moved
                    // anchor lies in [candidateAnchor - 7 days, candidateAnchor).
                    var candidateAnchor = Candidate.UtcDateTime.Date;
                    var oracle = moved.Any(m =>
                    {
                        var anchor = m.UtcDateTime.Date;
                        return candidateAnchor.AddDays(-7) <= anchor && anchor < candidateAnchor;
                    });

                    answers
                        .MovedPreviousWeek.Should()
                        .Be(oracle, "MovedPreviousWeek is membership in [anchor - 7 days, anchor)");
                },
                iter: 1000
            );
    }
}
