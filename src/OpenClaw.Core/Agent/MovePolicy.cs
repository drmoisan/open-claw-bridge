namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic recurring-meeting move policy (D3). <see cref="CanMove"/> is a pure
/// function that evaluates whether a meeting may be moved per master Section 10.3
/// <c>can_move</c>, over the predicates available from the normalized context.
/// </summary>
public static class MovePolicy
{
    /// <summary>
    /// Determines whether the meeting may be moved per master Section 10.3:
    /// a <see cref="RecurringMeetingKind.RECURRING_FORUM"/> is movable only when the
    /// requester is the owner or the meeting owner (organizer); a P0 request may bump a
    /// meeting only when it has fewer than six attendees and no VIP attendee; otherwise
    /// the meeting is movable.
    /// </summary>
    /// <param name="meeting">The normalized meeting context. Must not be null.</param>
    /// <param name="ownerEmail">The mailbox owner email. Must not be null.</param>
    /// <param name="requesterEmail">The requester email. Must not be null.</param>
    /// <param name="requestPriority">The request priority.</param>
    /// <param name="policy">The owner scheduling policy. Must not be null.</param>
    /// <returns><see langword="true"/> when the meeting may be moved.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required reference argument is null.
    /// </exception>
    /// <remarks>
    /// The master's 1:1 rolling-occurrence rule ("at most twice per rolling six
    /// occurrences, never two weeks in a row") depends on per-series move history that
    /// is not part of the pure normalized context. That stateful rule is deferred to the
    /// orchestration layer; this pure surface evaluates the kind- and priority-based
    /// predicates available from the context, so a <see cref="RecurringMeetingKind.ONE_ON_ONE"/>
    /// is treated as movable here and the history guard is applied by the caller.
    /// </remarks>
    public static bool CanMove(
        NormalizedMeetingContext meeting,
        string ownerEmail,
        string requesterEmail,
        OwnerPriority requestPriority,
        OwnerSchedulingPolicy policy
    )
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(ownerEmail);
        ArgumentNullException.ThrowIfNull(requesterEmail);
        ArgumentNullException.ThrowIfNull(policy);

        var owner = MeetingContextNormalizer.NormalizeEmail(ownerEmail);
        var requester = MeetingContextNormalizer.NormalizeEmail(requesterEmail);
        var kind = RecurringMeetingClassifier.Classify(meeting, owner);

        if (kind == RecurringMeetingKind.RECURRING_FORUM)
        {
            // Immovable except by explicit owner or meeting-owner (organizer) request.
            return string.Equals(requester, owner, StringComparison.Ordinal)
                || string.Equals(requester, meeting.Organizer, StringComparison.Ordinal);
        }

        if (requestPriority == OwnerPriority.P0)
        {
            // A P0 request may bump only small, non-VIP meetings.
            var hasVipAttendee = meeting.AllAttendees.Any(email =>
                policy.VipEmails.Contains(email)
            );
            return meeting.AllAttendees.Count < 6 && !hasVipAttendee;
        }

        return true;
    }
}
