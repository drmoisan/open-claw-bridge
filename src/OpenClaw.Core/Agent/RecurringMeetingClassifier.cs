namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic recurring-meeting classifier (D3). <see cref="Classify"/> is a pure
/// function that maps a normalized context to a <see cref="RecurringMeetingKind"/> per
/// master Section 10.3 <c>classify_recurring</c>.
/// </summary>
public static class RecurringMeetingClassifier
{
    /// <summary>
    /// Classifies a recurring meeting per master Section 10.3: a non-recurring meeting
    /// is <see cref="RecurringMeetingKind.NON_RECURRING"/>; a recurring meeting whose
    /// only attendee besides the organizer is the owner is a
    /// <see cref="RecurringMeetingKind.ONE_ON_ONE"/>; a recurring meeting with more than
    /// five attendees is a <see cref="RecurringMeetingKind.RECURRING_FORUM"/>; any other
    /// recurring meeting is <see cref="RecurringMeetingKind.RECURRING_OTHER"/>.
    /// </summary>
    /// <param name="ctx">The normalized meeting context. Must not be null.</param>
    /// <param name="ownerEmail">The mailbox owner email.</param>
    /// <returns>The recurring-meeting kind.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ctx"/> or <paramref name="ownerEmail"/> is null.
    /// </exception>
    public static RecurringMeetingKind Classify(NormalizedMeetingContext ctx, string ownerEmail)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(ownerEmail);

        if (!ctx.IsRecurring)
        {
            return RecurringMeetingKind.NON_RECURRING;
        }

        var owner = MeetingContextNormalizer.NormalizeEmail(ownerEmail);
        var organizer = ctx.Organizer;
        var others = ctx
            .AllAttendees.Where(email => !string.Equals(email, organizer, StringComparison.Ordinal))
            .ToList();

        if (others.Count == 1 && string.Equals(others[0], owner, StringComparison.Ordinal))
        {
            return RecurringMeetingKind.ONE_ON_ONE;
        }

        if (ctx.AllAttendees.Count > 5)
        {
            return RecurringMeetingKind.RECURRING_FORUM;
        }

        return RecurringMeetingKind.RECURRING_OTHER;
    }
}
