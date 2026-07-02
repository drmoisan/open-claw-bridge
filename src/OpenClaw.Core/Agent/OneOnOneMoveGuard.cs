namespace OpenClaw.Core.Agent;

/// <summary>
/// The per-series move-history answers consumed by
/// <see cref="OneOnOneMoveGuard.CanMove"/> (issue #105).
/// </summary>
/// <param name="MovesInLastSixOccurrences">
/// The number of recorded moves whose occurrence anchor falls within the rolling
/// six-occurrence window ending at the candidate occurrence (counted per move row).
/// </param>
/// <param name="MovedPreviousWeek">
/// Whether any recorded move's occurrence anchor lies within the seven days strictly
/// before the candidate occurrence anchor.
/// </param>
public sealed record SeriesMoveHistoryAnswers(
    int MovesInLastSixOccurrences,
    bool MovedPreviousWeek
);

/// <summary>
/// Pure one-on-one move guard (issue #105, master Section 10.3 1:1 rolling-occurrence
/// rule): a <see cref="RecurringMeetingKind.ONE_ON_ONE"/> may be moved at most twice per
/// rolling six occurrences and never two weeks in a row. The guard is a pure static
/// surface with no I/O, no clock, and no store reference: history answers are computed
/// from caller-supplied timestamps and passed in as <see cref="SeriesMoveHistoryAnswers"/>.
/// </summary>
public static class OneOnOneMoveGuard
{
    /// <summary>
    /// Computes the <see cref="SeriesMoveHistoryAnswers"/> for a candidate occurrence
    /// from caller-supplied move history and occurrence lists (D1/D2). Each timestamp is
    /// anchored to its UTC calendar date (<c>t.UtcDateTime.Date</c>). The rolling window
    /// is the six greatest distinct anchors, at or before the candidate anchor, drawn
    /// from the supplied occurrence starts plus the candidate itself (all of them when
    /// fewer than six exist). <c>MovesInLastSixOccurrences</c> counts every supplied
    /// moved entry (per move row, not per distinct anchor) whose anchor is a member of
    /// the window. <c>MovedPreviousWeek</c> is true iff any moved anchor lies in the
    /// half-open interval [candidate anchor − 7 days, candidate anchor).
    /// </summary>
    /// <param name="movedOccurrenceStartsUtc">
    /// The recorded moved-occurrence starts for the series (caller-supplied, typically
    /// from <see cref="ISeriesMoveHistory.GetMovedOccurrenceStartsAsync"/>).
    /// </param>
    /// <param name="occurrenceStartsUtc">
    /// The known occurrence starts of the series. The caller supplies this list; per D2,
    /// an incomplete list is conservative — fewer known occurrence anchors shrink the
    /// window toward the candidate, so more moved entries fall inside it and the guard
    /// blocks more, never less.
    /// </param>
    /// <param name="candidateOccurrenceStartUtc">The start of the occurrence the caller wants to move.</param>
    /// <returns>The computed history answers.</returns>
    /// <exception cref="ArgumentNullException">A list argument is null.</exception>
    public static SeriesMoveHistoryAnswers ComputeAnswers(
        IReadOnlyList<DateTimeOffset> movedOccurrenceStartsUtc,
        IReadOnlyList<DateTimeOffset> occurrenceStartsUtc,
        DateTimeOffset candidateOccurrenceStartUtc
    )
    {
        ArgumentNullException.ThrowIfNull(movedOccurrenceStartsUtc);
        ArgumentNullException.ThrowIfNull(occurrenceStartsUtc);

        var candidateAnchor = Anchor(candidateOccurrenceStartUtc);
        var window = occurrenceStartsUtc
            .Select(Anchor)
            .Append(candidateAnchor)
            .Where(anchor => anchor <= candidateAnchor)
            .Distinct()
            .OrderByDescending(anchor => anchor)
            .Take(6)
            .ToHashSet();

        var movesInWindow = movedOccurrenceStartsUtc.Count(moved => window.Contains(Anchor(moved)));

        var previousWeekFloor = candidateAnchor.AddDays(-7);
        var movedPreviousWeek = movedOccurrenceStartsUtc.Any(moved =>
        {
            var anchor = Anchor(moved);
            return previousWeekFloor <= anchor && anchor < candidateAnchor;
        });

        return new SeriesMoveHistoryAnswers(movesInWindow, movedPreviousWeek);
    }

    /// <summary>
    /// Determines whether the meeting may be moved. A meeting classifying as
    /// <see cref="RecurringMeetingKind.ONE_ON_ONE"/> is movable only when it has been
    /// moved fewer than two times in the rolling six-occurrence window and was not moved
    /// in the previous week; every other kind delegates to
    /// <see cref="MovePolicy.CanMove"/> unchanged.
    /// </summary>
    /// <param name="meeting">The normalized meeting context. Must not be null.</param>
    /// <param name="ownerEmail">The mailbox owner email. Must not be null.</param>
    /// <param name="requesterEmail">The requester email. Must not be null.</param>
    /// <param name="requestPriority">The request priority.</param>
    /// <param name="policy">The owner scheduling policy. Must not be null.</param>
    /// <param name="history">The per-series move-history answers. Must not be null.</param>
    /// <returns><see langword="true"/> when the meeting may be moved.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required reference argument is null.
    /// </exception>
    public static bool CanMove(
        NormalizedMeetingContext meeting,
        string ownerEmail,
        string requesterEmail,
        OwnerPriority requestPriority,
        OwnerSchedulingPolicy policy,
        SeriesMoveHistoryAnswers history
    )
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(ownerEmail);
        ArgumentNullException.ThrowIfNull(requesterEmail);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(history);

        var owner = MeetingContextNormalizer.NormalizeEmail(ownerEmail);
        var kind = RecurringMeetingClassifier.Classify(meeting, owner);

        if (kind == RecurringMeetingKind.ONE_ON_ONE)
        {
            return history.MovesInLastSixOccurrences < 2 && !history.MovedPreviousWeek;
        }

        return MovePolicy.CanMove(meeting, ownerEmail, requesterEmail, requestPriority, policy);
    }

    /// <summary>
    /// Resolves the stable series key for a meeting (D3): the
    /// <see cref="NormalizedMeetingContext.SeriesMasterId"/> when present, otherwise the
    /// <see cref="NormalizedMeetingContext.EventId"/>.
    /// </summary>
    /// <param name="meeting">The normalized meeting context. Must not be null.</param>
    /// <returns>The stable series key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="meeting"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Both <see cref="NormalizedMeetingContext.SeriesMasterId"/> and
    /// <see cref="NormalizedMeetingContext.EventId"/> are null or empty.
    /// </exception>
    public static string ResolveSeriesKey(NormalizedMeetingContext meeting)
    {
        ArgumentNullException.ThrowIfNull(meeting);

        if (!string.IsNullOrEmpty(meeting.SeriesMasterId))
        {
            return meeting.SeriesMasterId;
        }

        if (!string.IsNullOrEmpty(meeting.EventId))
        {
            return meeting.EventId;
        }

        throw new ArgumentException(
            "Meeting has neither a SeriesMasterId nor an EventId; no stable series key exists.",
            nameof(meeting)
        );
    }

    /// <summary>Anchors a timestamp to its UTC calendar date.</summary>
    private static DateTime Anchor(DateTimeOffset value) => value.UtcDateTime.Date;
}
