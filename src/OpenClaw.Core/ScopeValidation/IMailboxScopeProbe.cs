namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Narrow probe port (spec D2): performs one harmless read against an arbitrary
/// mailbox and projects the result into a <see cref="MailboxProbeOutcome"/>. The
/// implementation never throws on a Graph failure — every failure is mapped into the
/// returned outcome's error fields (via the F13 D5 error mapping) so the pure evaluator
/// can classify it. This port exists because the F13 <c>IHostAdapterClient</c> reads a
/// single fixed mailbox and cannot express a per-call target; it is not modified by this
/// feature.
/// </summary>
internal interface IMailboxScopeProbe
{
    /// <summary>
    /// Reads at most one message from <paramref name="mailboxUpn"/> to assert whether the
    /// caller is authorized to read that mailbox. Returns a
    /// <see cref="MailboxProbeOutcome"/>; it does not throw on a Graph/authorization
    /// failure.
    /// </summary>
    /// <param name="mailboxUpn">The target mailbox UPN.</param>
    /// <param name="requestId">Optional correlation id; blank generates one.</param>
    /// <param name="cancellationToken">Cancels the request and any backoff delay.</param>
    Task<MailboxProbeOutcome> ProbeMailboxReadAsync(
        string mailboxUpn,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );
}
