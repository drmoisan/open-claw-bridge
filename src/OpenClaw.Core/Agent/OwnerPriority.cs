namespace OpenClaw.Core.Agent;

/// <summary>
/// The owner-specific scheduling priority (D3), per master Section 10.1-10.2. The
/// numeric levels are a scheduling/policy vocabulary applied after the five-way triage
/// decision, not the decision taxonomy.
/// </summary>
public enum OwnerPriority
{
    /// <summary>Immediate: VIP sender, or urgent request from a direct report.</summary>
    P0,

    /// <summary>Owner-initiated, non-VIP emblem-domain sender, or explicit Priority 1.</summary>
    P1,

    /// <summary>Direct reports or explicit Priority 2 senders.</summary>
    P2,

    /// <summary>Internal requestors or explicit Priority 3 senders.</summary>
    P3,

    /// <summary>Unknown external senders (reserved level).</summary>
    P4,

    /// <summary>Unknown recruiter; escalate to the owner.</summary>
    ESCALATE_TO_OWNER,

    /// <summary>Likely spam; add to the digest of ignored requests.</summary>
    DIGEST_IGNORED,
}
