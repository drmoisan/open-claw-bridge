namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Pure, host-neutral classifier and pair-evaluator for the negative-scope smoke test
/// (spec D3, D4). No I/O, clock, logging, DI, or <c>OpenClaw.Core.CloudGraph</c>
/// dependency. <see cref="IsAuthorizationDenial"/> recognizes exactly one shape — a real
/// Exchange Application RBAC denial — using Ordinal comparisons and all three conjuncts;
/// the rule is deliberately fail-closed so any ambiguous or transient failure fails the
/// boundary with a reason rather than producing a false pass.
/// </summary>
internal static class ScopeBoundaryEvaluator
{
    /// <summary>The D5 mapping of HTTP 403 (and 401) to the adapter error code.</summary>
    internal const string ExpectedDenialErrorCode = "UNAUTHORIZED";

    /// <summary>The Graph <c>error.code</c> passthrough that discriminates a 403 RBAC denial from a 401 auth fault.</summary>
    internal const string ExpectedDenialGraphCode = "ErrorAccessDenied";

    /// <summary>
    /// True only for the exact RBAC out-of-scope denial shape: the read failed
    /// (<c>!Ok</c>), <c>ErrorCode == "UNAUTHORIZED"</c>, and
    /// <c>BridgeErrorCode == "ErrorAccessDenied"</c>, all compared Ordinal. Because the
    /// F13 executor folds HTTP 401 and 403 into a single <c>UNAUTHORIZED</c> code, the
    /// <c>BridgeErrorCode</c> conjunct is the only discriminator between "authorization
    /// boundary held" and "authentication broke".
    /// </summary>
    /// <param name="outcome">The probe outcome to classify.</param>
    internal static bool IsAuthorizationDenial(MailboxProbeOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return !outcome.Ok
            && string.Equals(outcome.ErrorCode, ExpectedDenialErrorCode, StringComparison.Ordinal)
            && string.Equals(
                outcome.BridgeErrorCode,
                ExpectedDenialGraphCode,
                StringComparison.Ordinal
            );
    }

    /// <summary>
    /// Evaluates the in-scope/out-of-scope probe pair into a
    /// <see cref="ScopeBoundaryValidationResult"/> (spec D4). <c>InScopeAllowed</c> is the
    /// in-scope read's success flag; <c>OutOfScopeDenied</c> is
    /// <see cref="IsAuthorizationDenial"/> of the out-of-scope outcome;
    /// <c>Succeeded = InScopeAllowed &amp;&amp; OutOfScopeDenied</c> (the only pass
    /// condition). <c>FailureReason</c> is null iff <c>Succeeded</c>; otherwise it names
    /// the failing side(s), joined with <c>"; "</c> when both sides fail.
    /// </summary>
    /// <param name="inScopeMailbox">The configured in-scope test mailbox UPN.</param>
    /// <param name="outOfScopeMailbox">The configured out-of-scope test mailbox UPN.</param>
    /// <param name="inScopeOutcome">The in-scope probe outcome.</param>
    /// <param name="outOfScopeOutcome">The out-of-scope probe outcome.</param>
    internal static ScopeBoundaryValidationResult Evaluate(
        string inScopeMailbox,
        string outOfScopeMailbox,
        MailboxProbeOutcome inScopeOutcome,
        MailboxProbeOutcome outOfScopeOutcome
    )
    {
        ArgumentNullException.ThrowIfNull(inScopeOutcome);
        ArgumentNullException.ThrowIfNull(outOfScopeOutcome);

        var inScopeAllowed = inScopeOutcome.Ok;
        var outOfScopeDenied = IsAuthorizationDenial(outOfScopeOutcome);
        var succeeded = inScopeAllowed && outOfScopeDenied;
        var failureReason = succeeded
            ? null
            : BuildFailureReason(
                inScopeOutcome,
                outOfScopeOutcome,
                inScopeAllowed,
                outOfScopeDenied
            );

        return new ScopeBoundaryValidationResult(
            inScopeMailbox,
            outOfScopeMailbox,
            inScopeAllowed,
            outOfScopeDenied,
            succeeded,
            failureReason,
            inScopeOutcome,
            outOfScopeOutcome
        );
    }

    /// <summary>
    /// Builds the precise failure reason(s) for a non-succeeding pair. The in-scope
    /// failure clause and the out-of-scope failure clause (scope leak when the read
    /// succeeded, or wrong-error when it failed for a non-denial reason) are joined with
    /// <c>"; "</c> when both sides fail (mirroring F11 D5).
    /// </summary>
    private static string BuildFailureReason(
        MailboxProbeOutcome inScopeOutcome,
        MailboxProbeOutcome outOfScopeOutcome,
        bool inScopeAllowed,
        bool outOfScopeDenied
    )
    {
        var reasons = new List<string>();

        if (!inScopeAllowed)
        {
            reasons.Add(
                $"in-scope mailbox read failed: {inScopeOutcome.ErrorCode}/"
                    + $"{inScopeOutcome.BridgeErrorCode ?? "-"}: {inScopeOutcome.ErrorMessage}"
            );
        }

        if (!outOfScopeDenied)
        {
            reasons.Add(
                outOfScopeOutcome.Ok
                    ? "out-of-scope mailbox read unexpectedly succeeded; the RBAC scope does not hold"
                    : "out-of-scope mailbox read failed but not with the expected authorization "
                        + "denial (expected UNAUTHORIZED/ErrorAccessDenied; observed "
                        + $"{outOfScopeOutcome.ErrorCode}/{outOfScopeOutcome.BridgeErrorCode ?? "-"})"
            );
        }

        return string.Join("; ", reasons);
    }
}
