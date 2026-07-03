namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Pure validation for <see cref="GraphAdapterOptions"/> (D8). Returns the full list
/// of violation messages (not first-failure). Messages name the offending
/// configuration keys and never echo configured values. All rules apply only when
/// <see cref="GraphAdapterOptions.Enabled"/> is <c>true</c>; a disabled adapter is
/// always valid.
/// </summary>
public static class GraphAdapterOptionsValidator
{
    /// <summary>
    /// Validates <paramref name="options"/> and returns every violation found. An
    /// empty list means the options are valid. This method is pure: no I/O, no clock,
    /// no shared state.
    /// </summary>
    /// <param name="options">The bound options to validate.</param>
    public static IReadOnlyList<string> Validate(GraphAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var violations = new List<string>();

        if (!options.Enabled)
        {
            return violations;
        }

        if (string.IsNullOrWhiteSpace(options.PrincipalMailboxUpn))
        {
            violations.Add("PrincipalMailboxUpn is required and must be non-whitespace.");
        }

        if (string.IsNullOrWhiteSpace(options.AssistantMailboxUpn))
        {
            violations.Add("AssistantMailboxUpn is required and must be non-whitespace.");
        }

        if (
            !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl)
            || !string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            violations.Add("BaseUrl must be an absolute https URI.");
        }

        if (options.PageSize is < 1 or > 1000)
        {
            violations.Add("PageSize must be between 1 and 1000 inclusive.");
        }

        if (options.MaxPages < 1)
        {
            violations.Add("MaxPages must be at least 1.");
        }

        if (options.MaxAttempts is < 1 or > 10)
        {
            violations.Add("MaxAttempts must be between 1 and 10 inclusive.");
        }

        if (options.BaseDelaySeconds <= 0)
        {
            violations.Add("BaseDelaySeconds must be greater than zero.");
        }

        if (options.MaxDelaySeconds < options.BaseDelaySeconds)
        {
            violations.Add("MaxDelaySeconds must be greater than or equal to BaseDelaySeconds.");
        }

        if (options.AvailabilityViewIntervalMinutes is < 5 or > 1440)
        {
            violations.Add("AvailabilityViewIntervalMinutes must be between 5 and 1440 inclusive.");
        }

        return violations;
    }
}
