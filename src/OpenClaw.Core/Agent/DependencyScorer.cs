namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic dependency scorer (D2). <see cref="Score"/> is a pure function that
/// applies the master Section 9.2 weights to a normalized context. The score is always
/// non-negative.
/// </summary>
public static class DependencyScorer
{
    /// <summary>
    /// Computes the dependency score per master Section 9.2: recurring +2; large meeting
    /// (all attendees at or above the threshold) +2; any resource attendee +1; online
    /// meeting +1; protected category +3; protected subject pattern over
    /// <c>subject + " " + bodyText</c> +3; VIP organizer +3; any external attendee +2.
    /// </summary>
    /// <param name="ctx">The normalized meeting context. Must not be null.</param>
    /// <param name="policy">The triage policy. Must not be null.</param>
    /// <returns>The non-negative dependency score.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ctx"/> or <paramref name="policy"/> is null.
    /// </exception>
    public static int Score(NormalizedMeetingContext ctx, TriagePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(policy);

        var score = 0;
        var searchableText = $"{ctx.Subject} {ctx.BodyText}";

        if (ctx.IsRecurring)
        {
            score += 2;
        }

        if (ctx.AllAttendees.Count >= policy.LargeMeetingThreshold)
        {
            score += 2;
        }

        if (ctx.ResourceAttendees.Count > 0)
        {
            score += 1;
        }

        if (ctx.IsOnlineMeeting)
        {
            score += 1;
        }

        if (ctx.Categories.Any(c => policy.ProtectedCategories.Contains(c)))
        {
            score += 3;
        }

        if (policy.ProtectedSubjectPatterns.Any(rx => rx.IsMatch(searchableText)))
        {
            score += 3;
        }

        if (policy.VipOrganizers.Contains(ctx.Organizer))
        {
            score += 3;
        }

        if (ctx.AllAttendees.Any(email => !TriageEmail.IsInternal(email, policy)))
        {
            score += 2;
        }

        return score;
    }
}
