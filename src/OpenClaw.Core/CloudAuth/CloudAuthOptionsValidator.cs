namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// Pure validation for <see cref="CloudAuthOptions"/> (D5). Returns the full list of
/// violation messages (not first-failure). Messages name the offending configuration
/// keys and never echo configured values, so no secret or identifier material can leak
/// through validation output.
/// </summary>
public static class CloudAuthOptionsValidator
{
    private const string ScopeSuffix = "/.default";

    /// <summary>
    /// Validates <paramref name="options"/> and returns every violation found. An empty
    /// list means the options are valid. This method is pure: no I/O, no clock, no
    /// shared state.
    /// </summary>
    /// <param name="options">The bound options to validate.</param>
    public static IReadOnlyList<string> Validate(CloudAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            violations.Add("TenantId is required and must be non-whitespace.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            violations.Add("ClientId is required and must be non-whitespace.");
        }

        var certificateConfigured = !string.IsNullOrWhiteSpace(options.CertificatePath);
        var secretConfigured = !string.IsNullOrWhiteSpace(options.ClientSecret);
        if (!certificateConfigured && !secretConfigured)
        {
            violations.Add(
                "Exactly one of CertificatePath or ClientSecret must be configured; neither is set."
            );
        }
        else if (certificateConfigured && secretConfigured)
        {
            violations.Add(
                "Exactly one of CertificatePath or ClientSecret must be configured; both are set (ambiguous credential source is rejected)."
            );
        }

        if (
            !Uri.TryCreate(options.Scope, UriKind.Absolute, out _)
            || !options.Scope.EndsWith(ScopeSuffix, StringComparison.Ordinal)
        )
        {
            violations.Add("Scope must be an absolute URI ending with '/.default'.");
        }

        if (
            !Uri.TryCreate(options.AuthorityHost, UriKind.Absolute, out var authority)
            || !string.Equals(authority.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            violations.Add("AuthorityHost must be an absolute https URI.");
        }

        if (options.RefreshSkewMinutes is < 0 or > 60)
        {
            violations.Add("RefreshSkewMinutes must be between 0 and 60 inclusive.");
        }

        return violations;
    }
}
