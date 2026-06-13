namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic five-way triage engine (D2). <see cref="Triage"/> is a pure function
/// that returns exactly one of the five <see cref="TriageDecision"/> members for any
/// valid input, applying the master Section 9.2 thresholds in order.
/// </summary>
public static class TriageEngine
{
    /// <summary>
    /// Classifies a normalized context into a five-way decision per master Section 9.2:
    /// empty subject and body produce <see cref="TriageDecision.IGNORE"/>; private
    /// sensitivity produces <see cref="TriageDecision.PRIVATE_BUSY_ONLY"/>; a VIP
    /// organizer or a dependency score at or above 7 produces
    /// <see cref="TriageDecision.PROTECTED_MEETING"/>; an external sender or a score at
    /// or above 4 produces <see cref="TriageDecision.HUMAN_APPROVAL"/>; otherwise the
    /// result is <see cref="TriageDecision.AUTO_COORDINATE"/>.
    /// </summary>
    /// <param name="ctx">The normalized meeting context. Must not be null.</param>
    /// <param name="policy">The triage policy. Must not be null.</param>
    /// <returns>The triage result.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ctx"/> or <paramref name="policy"/> is null.
    /// </exception>
    public static TriageResult Triage(NormalizedMeetingContext ctx, TriagePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrEmpty(ctx.Subject) && string.IsNullOrEmpty(ctx.BodyText))
        {
            return new TriageResult(
                TriageDecision.IGNORE,
                new[] { "No usable scheduling content" }
            );
        }

        if (string.Equals(ctx.Sensitivity, "private", StringComparison.Ordinal))
        {
            return new TriageResult(
                TriageDecision.PRIVATE_BUSY_ONLY,
                new[]
                {
                    "Event is marked private; treat as unavailable but do not ingest semantics",
                }
            );
        }

        var depScore = DependencyScorer.Score(ctx, policy);
        var organizerIsVip = policy.VipOrganizers.Contains(ctx.Organizer);
        var senderAddress = string.IsNullOrEmpty(ctx.MessageSender)
            ? ctx.MessageFrom
            : ctx.MessageSender;
        var senderIsInternal = TriageEmail.IsInternal(senderAddress, policy);

        if (organizerIsVip || depScore >= 7)
        {
            return new TriageResult(
                TriageDecision.PROTECTED_MEETING,
                new[]
                {
                    organizerIsVip ? "Protected organizer" : "High dependency score",
                    $"dependencyScore={depScore}",
                }
            );
        }

        if (!senderIsInternal || depScore >= 4)
        {
            return new TriageResult(
                TriageDecision.HUMAN_APPROVAL,
                new[]
                {
                    !senderIsInternal
                        ? "External sender or participant present"
                        : "Moderate dependency score",
                    $"dependencyScore={depScore}",
                }
            );
        }

        return new TriageResult(
            TriageDecision.AUTO_COORDINATE,
            new[] { "Internal, non-private, non-protected meeting with low dependency score" }
        );
    }
}
