namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Pure validation for <see cref="ScopeValidationOptions"/> (spec D6), in the
/// <c>GraphAdapterOptionsValidator</c> style: returns the full list of violation
/// messages (not first-failure), names the offending configuration keys, and never
/// echoes configured values. All rules apply only when
/// <see cref="ScopeValidationOptions.Enabled"/> is <c>true</c>; a disabled section is
/// always valid.
/// </summary>
public static class ScopeValidationOptionsValidator
{
    /// <summary>
    /// Validates <paramref name="options"/> and returns every violation found. An empty
    /// list means the options are valid. This method is pure: no I/O, no clock, no shared
    /// state. When enabled, both UPNs must be present and non-whitespace and must differ
    /// (OrdinalIgnoreCase).
    /// </summary>
    /// <param name="options">The bound options to validate.</param>
    public static IReadOnlyList<string> Validate(ScopeValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var violations = new List<string>();

        if (!options.Enabled)
        {
            return violations;
        }

        var inScopeMissing = string.IsNullOrWhiteSpace(options.InScopeTestMailboxUpn);
        var outOfScopeMissing = string.IsNullOrWhiteSpace(options.OutOfScopeTestMailboxUpn);

        if (inScopeMissing)
        {
            violations.Add("InScopeTestMailboxUpn is required and must be non-whitespace.");
        }

        if (outOfScopeMissing)
        {
            violations.Add("OutOfScopeTestMailboxUpn is required and must be non-whitespace.");
        }

        if (
            !inScopeMissing
            && !outOfScopeMissing
            && string.Equals(
                options.InScopeTestMailboxUpn,
                options.OutOfScopeTestMailboxUpn,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            violations.Add(
                "InScopeTestMailboxUpn and OutOfScopeTestMailboxUpn must differ (OrdinalIgnoreCase)."
            );
        }

        return violations;
    }
}
