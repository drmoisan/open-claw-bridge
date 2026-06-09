namespace OpenClaw.Core.Agent;

/// <summary>
/// The five-way deterministic triage decision (D2), per master Section 9.1-9.2. Member
/// names match the master <c>Decision</c> union.
/// </summary>
public enum TriageDecision
{
    /// <summary>No usable scheduling content; the item is ignored.</summary>
    IGNORE,

    /// <summary>Private meeting; treated as busy-only without semantic ingestion.</summary>
    PRIVATE_BUSY_ONLY,

    /// <summary>Protected meeting; never auto-coordinated regardless of requester priority.</summary>
    PROTECTED_MEETING,

    /// <summary>Requires human approval before the agent acts.</summary>
    HUMAN_APPROVAL,

    /// <summary>The agent may auto-coordinate scheduling.</summary>
    AUTO_COORDINATE,
}

/// <summary>
/// The triage outcome: the decision and the human-readable reasons that produced it.
/// </summary>
/// <param name="Decision">The five-way decision.</param>
/// <param name="Reasons">The ordered reasons supporting the decision.</param>
public sealed record TriageResult(TriageDecision Decision, IReadOnlyList<string> Reasons);
