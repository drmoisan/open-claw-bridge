using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// One-shot <see cref="IHostedService"/> that runs the scope-boundary validation at
/// startup (spec D5). Registered only when <c>OpenClaw:ScopeValidation:Enabled</c> is
/// <c>true</c>. Because user-registered hosted services run <see cref="StartAsync"/>
/// before the server accepts requests and an exception thrown from <c>StartAsync</c>
/// aborts host startup, this provides the master's fail-fast "at startup ... assert"
/// semantics. On success it logs a single <c>Information</c> entry; on any failure it logs
/// a single <c>Critical</c> entry (including <c>FailureReason</c>) and throws
/// <see cref="InvalidOperationException"/> to hard-abort startup. There is no
/// soft/warn-and-continue mode; transient failures also abort so an unproven boundary is
/// never reported as success. Outcome summaries are limited to
/// <c>Ok</c>/<c>ErrorCode</c>/<c>BridgeErrorCode</c> — never tokens, never response bodies.
/// </summary>
internal sealed class ScopeBoundaryStartupValidator : IHostedService
{
    private readonly ScopeBoundaryValidator validator;
    private readonly ILogger<ScopeBoundaryStartupValidator> logger;

    /// <summary>
    /// Creates the startup validator over the orchestrating validator and a logger.
    /// </summary>
    /// <param name="validator">The two-probe orchestrating validator.</param>
    /// <param name="logger">The structured logger for the single result entry.</param>
    public ScopeBoundaryStartupValidator(
        ScopeBoundaryValidator validator,
        ILogger<ScopeBoundaryStartupValidator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        this.validator = validator;
        this.logger = logger;
    }

    /// <summary>
    /// Runs the validation, logs exactly one structured entry carrying every result field,
    /// and throws to abort host startup on any non-succeeding verdict.
    /// </summary>
    /// <param name="cancellationToken">Cancels the probes and any backoff delay.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(cancellationToken);
        var inScopeSummary = Summarize(result.InScopeOutcome);
        var outOfScopeSummary = Summarize(result.OutOfScopeOutcome);

        if (result.Succeeded)
        {
            logger.LogInformation(
                "Scope-boundary startup validation succeeded. InScopeMailbox={InScopeMailbox} "
                    + "OutOfScopeMailbox={OutOfScopeMailbox} InScopeAllowed={InScopeAllowed} "
                    + "OutOfScopeDenied={OutOfScopeDenied} Succeeded={Succeeded} "
                    + "FailureReason={FailureReason} InScopeOutcome={InScopeOutcome} "
                    + "OutOfScopeOutcome={OutOfScopeOutcome}",
                result.InScopeMailbox,
                result.OutOfScopeMailbox,
                result.InScopeAllowed,
                result.OutOfScopeDenied,
                result.Succeeded,
                result.FailureReason,
                inScopeSummary,
                outOfScopeSummary
            );
            return;
        }

        logger.LogCritical(
            "Scope-boundary startup validation failed. InScopeMailbox={InScopeMailbox} "
                + "OutOfScopeMailbox={OutOfScopeMailbox} InScopeAllowed={InScopeAllowed} "
                + "OutOfScopeDenied={OutOfScopeDenied} Succeeded={Succeeded} "
                + "FailureReason={FailureReason} InScopeOutcome={InScopeOutcome} "
                + "OutOfScopeOutcome={OutOfScopeOutcome}",
            result.InScopeMailbox,
            result.OutOfScopeMailbox,
            result.InScopeAllowed,
            result.OutOfScopeDenied,
            result.Succeeded,
            result.FailureReason,
            inScopeSummary,
            outOfScopeSummary
        );

        throw new InvalidOperationException(
            $"Scope-boundary startup validation failed: {result.FailureReason}"
        );
    }

    /// <summary>No-op stop; the validation is a one-shot startup action.</summary>
    /// <param name="cancellationToken">Unused.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Renders a probe outcome as a token-free, body-free summary limited to the three
    /// classification-relevant fields.
    /// </summary>
    private static string Summarize(MailboxProbeOutcome outcome) =>
        $"Ok={outcome.Ok}, ErrorCode={outcome.ErrorCode ?? "-"}, "
        + $"BridgeErrorCode={outcome.BridgeErrorCode ?? "-"}";
}
