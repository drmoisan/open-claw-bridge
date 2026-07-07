namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// The three-value outcome of the send-on-behalf authorization decision (F15, issue
/// #119). Every input to <see cref="SendOnBehalfAuthorizer.Authorize"/> maps to exactly
/// one member (decision totality).
/// </summary>
internal enum SendAuthorizationDecision
{
    /// <summary>
    /// The principal and assistant mailboxes are the same (case-insensitive, trimmed):
    /// the mailbox sends as itself, no representation occurs, and the allowlist is
    /// irrelevant. No <c>from</c> is injected.
    /// </summary>
    AllowedSelf,

    /// <summary>
    /// The principal differs from the assistant and is a member of the allowlist
    /// (trimmed, <see cref="System.StringComparison.OrdinalIgnoreCase"/>): the assistant
    /// mailbox may represent the principal and <c>from = principal</c> is injected.
    /// </summary>
    AllowedOnBehalf,

    /// <summary>
    /// The principal differs from the assistant and is not on the allowlist (including
    /// the empty or absent allowlist): the send is denied fail-closed before any token
    /// acquisition or HTTP request.
    /// </summary>
    DeniedNotAllowlisted,
}

/// <summary>
/// Pure authorization for send-on-behalf principal representation (F15, issue #119, D2).
/// Decides whether the assistant mailbox may send as the configured principal. This
/// class is the single source of the authorization decision and the <c>from</c>-injection
/// predicate, so the two cannot diverge (spec "Single decision source").
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Authorize"/> is a pure function: no I/O, no clock, no logging, no shared
/// state — the same purity contract <see cref="GraphAdapterOptionsValidator"/> documents.
/// </para>
/// <para>
/// Fail-closed-empty semantics: an empty or absent allowlist yields
/// <see cref="SendAuthorizationDecision.DeniedNotAllowlisted"/> for every
/// <c>principal != assistant</c>. Self-send (<c>principal == assistant</c>) is unaffected
/// by allowlist contents and always yields <see cref="SendAuthorizationDecision.AllowedSelf"/>.
/// </para>
/// </remarks>
internal static class SendOnBehalfAuthorizer
{
    /// <summary>
    /// Decides whether the assistant mailbox may send on behalf of the principal.
    /// All comparisons use <see cref="System.StringComparison.OrdinalIgnoreCase"/> on
    /// <see cref="string.Trim()"/>ed values, matching the existing D7 comparison.
    /// </summary>
    /// <param name="principalMailboxUpn">The configured principal UPN (<c>{p}</c>).</param>
    /// <param name="assistantMailboxUpn">The configured assistant UPN (<c>{a}</c>).</param>
    /// <param name="allowedPrincipalMailboxUpns">
    /// The configured allowlist of principal UPNs the assistant may represent. An empty
    /// sequence denies all on-behalf sends (fail-closed).
    /// </param>
    /// <returns>
    /// <see cref="SendAuthorizationDecision.AllowedSelf"/> when the principal equals the
    /// assistant (dominates every allowlist);
    /// <see cref="SendAuthorizationDecision.AllowedOnBehalf"/> when the principal differs
    /// and is a trimmed, case-insensitive member of the allowlist; otherwise
    /// <see cref="SendAuthorizationDecision.DeniedNotAllowlisted"/>.
    /// </returns>
    public static SendAuthorizationDecision Authorize(
        string principalMailboxUpn,
        string assistantMailboxUpn,
        IEnumerable<string> allowedPrincipalMailboxUpns
    )
    {
        ArgumentNullException.ThrowIfNull(principalMailboxUpn);
        ArgumentNullException.ThrowIfNull(assistantMailboxUpn);
        ArgumentNullException.ThrowIfNull(allowedPrincipalMailboxUpns);

        var principal = principalMailboxUpn.Trim();
        var assistant = assistantMailboxUpn.Trim();

        if (string.Equals(principal, assistant, StringComparison.OrdinalIgnoreCase))
        {
            return SendAuthorizationDecision.AllowedSelf;
        }

        foreach (var entry in allowedPrincipalMailboxUpns)
        {
            if (
                entry is not null
                && string.Equals(entry.Trim(), principal, StringComparison.OrdinalIgnoreCase)
            )
            {
                return SendAuthorizationDecision.AllowedOnBehalf;
            }
        }

        return SendAuthorizationDecision.DeniedNotAllowlisted;
    }
}
