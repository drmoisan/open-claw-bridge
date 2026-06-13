namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic gating predicate that encodes the layering rule from master
/// Section 10 (and the editorial alignment note): the owner-priority and move-policy
/// layer runs only after triage resolves to
/// <see cref="TriageDecision.AUTO_COORDINATE"/> or
/// <see cref="TriageDecision.HUMAN_APPROVAL"/>. A
/// <see cref="TriageDecision.PROTECTED_MEETING"/> remains protected and a
/// <see cref="TriageDecision.PRIVATE_BUSY_ONLY"/> item remains opaque regardless of
/// requester priority, so neither is passed into the priority layer; an
/// <see cref="TriageDecision.IGNORE"/> item carries no scheduling intent.
/// </summary>
public static class SchedulingGate
{
    /// <summary>
    /// Returns whether the priority and move-policy layer should run for the supplied
    /// triage decision.
    /// </summary>
    /// <param name="decision">The triage decision.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="TriageDecision.AUTO_COORDINATE"/> and
    /// <see cref="TriageDecision.HUMAN_APPROVAL"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool RequiresPriorityLayer(TriageDecision decision) =>
        decision is TriageDecision.AUTO_COORDINATE or TriageDecision.HUMAN_APPROVAL;
}
