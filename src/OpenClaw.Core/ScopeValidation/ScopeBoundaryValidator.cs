using Microsoft.Extensions.Options;

namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Orchestrates the two-probe scope-boundary check (spec D5). Probes the configured
/// in-scope mailbox first, then the out-of-scope mailbox — both always executed, no
/// short-circuit, so the caller's log always carries both sides — and returns
/// <see cref="ScopeBoundaryEvaluator.Evaluate"/> of the pair. Composition only: no logging
/// and no verdict logic (both live in the pure evaluator and the startup validator
/// respectively).
/// </summary>
internal sealed class ScopeBoundaryValidator
{
    private readonly IMailboxScopeProbe probe;
    private readonly ScopeValidationOptions options;

    /// <summary>
    /// Creates the validator over an injected probe and the bound options.
    /// </summary>
    /// <param name="probe">The mailbox scope probe.</param>
    /// <param name="optionsAccessor">The bound <see cref="ScopeValidationOptions"/>.</param>
    public ScopeBoundaryValidator(
        IMailboxScopeProbe probe,
        IOptions<ScopeValidationOptions> optionsAccessor
    )
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(optionsAccessor);

        this.probe = probe;
        options = optionsAccessor.Value;
    }

    /// <summary>
    /// Probes the in-scope mailbox, then the out-of-scope mailbox (deterministic order;
    /// both always run), and returns the evaluated boundary result.
    /// </summary>
    /// <param name="cancellationToken">Flows to both probe calls.</param>
    public async Task<ScopeBoundaryValidationResult> ValidateAsync(
        CancellationToken cancellationToken
    )
    {
        var inScopeOutcome = await probe.ProbeMailboxReadAsync(
            options.InScopeTestMailboxUpn,
            cancellationToken: cancellationToken
        );
        var outOfScopeOutcome = await probe.ProbeMailboxReadAsync(
            options.OutOfScopeTestMailboxUpn,
            cancellationToken: cancellationToken
        );

        return ScopeBoundaryEvaluator.Evaluate(
            options.InScopeTestMailboxUpn,
            options.OutOfScopeTestMailboxUpn,
            inScopeOutcome,
            outOfScopeOutcome
        );
    }
}
