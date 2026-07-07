namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Structured verdict of a scope-boundary validation (spec D4; the F11 D5 analog).
/// Carries both mailbox identities, the two boolean sub-verdicts, the overall
/// <see cref="Succeeded"/> flag (invariant: <c>Succeeded == InScopeAllowed &amp;&amp;
/// OutOfScopeDenied</c>), the precise <see cref="FailureReason"/> (null iff
/// <see cref="Succeeded"/>), and both probe outcomes composed verbatim so the startup
/// log always carries both sides.
/// </summary>
/// <param name="InScopeMailbox">The configured in-scope test mailbox UPN.</param>
/// <param name="OutOfScopeMailbox">The configured out-of-scope test mailbox UPN.</param>
/// <param name="InScopeAllowed">Whether the in-scope read succeeded (<c>InScopeOutcome.Ok</c>).</param>
/// <param name="OutOfScopeDenied">Whether the out-of-scope read was the exact RBAC denial (D3).</param>
/// <param name="Succeeded">The boundary verdict: <c>InScopeAllowed &amp;&amp; OutOfScopeDenied</c>.</param>
/// <param name="FailureReason">Null iff <see cref="Succeeded"/>; otherwise the precise failing-side reason(s).</param>
/// <param name="InScopeOutcome">The in-scope probe outcome (F11 D5 "InScopeDetails" analog).</param>
/// <param name="OutOfScopeOutcome">The out-of-scope probe outcome (F11 D5 "OutOfScopeDetails" analog).</param>
internal sealed record ScopeBoundaryValidationResult(
    string InScopeMailbox,
    string OutOfScopeMailbox,
    bool InScopeAllowed,
    bool OutOfScopeDenied,
    bool Succeeded,
    string? FailureReason,
    MailboxProbeOutcome InScopeOutcome,
    MailboxProbeOutcome OutOfScopeOutcome
);
