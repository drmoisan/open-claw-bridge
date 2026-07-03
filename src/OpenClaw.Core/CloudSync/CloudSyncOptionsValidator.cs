namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Pure validation for <see cref="CloudSyncOptions"/> (D-7 fail-closed rules),
/// mirroring <see cref="OpenClaw.Core.CloudGraph.GraphAdapterOptionsValidator"/> style:
/// returns the full list of violation messages (not first-failure), names the
/// offending configuration keys, and never echoes configured values. All rules apply
/// only when <see cref="CloudSyncOptions.Enabled"/> is <c>true</c>; a disabled
/// CloudSync block is always valid.
/// </summary>
public static class CloudSyncOptionsValidator
{
    /// <summary>The master §6.1 maximum lifetime (minutes) for Outlook message subscriptions.</summary>
    public const int MaxSubscriptionLifetimeMinutes = 10080;

    /// <summary>
    /// Validates <paramref name="options"/> and returns every violation found. An
    /// empty list means the options are valid. This method is pure: no I/O, no clock,
    /// no shared state.
    /// </summary>
    /// <param name="options">The bound options to validate.</param>
    /// <param name="graphAdapterEnabled">
    /// The composition root's <c>OpenClaw:GraphAdapter:Enabled</c> value; CloudSync
    /// requires the Graph backend (D-7) because dispatch fetches by Graph message id
    /// and delta/subscription calls need <c>GraphAdapterOptions</c> plus CloudAuth.
    /// </param>
    public static IReadOnlyList<string> Validate(CloudSyncOptions options, bool graphAdapterEnabled)
    {
        ArgumentNullException.ThrowIfNull(options);

        var violations = new List<string>();

        if (!options.Enabled)
        {
            return violations;
        }

        if (!graphAdapterEnabled)
        {
            violations.Add(
                "CloudSync requires the Graph backend: OpenClaw:GraphAdapter:Enabled must be "
                    + "true when OpenClaw:CloudSync:Enabled is true."
            );
        }

        if (
            !Uri.TryCreate(options.NotificationUrl, UriKind.Absolute, out var notificationUrl)
            || !string.Equals(notificationUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            violations.Add("NotificationUrl must be an absolute https URI.");
        }

        if (options.SubscriptionLifetimeMinutes is < 1 or > MaxSubscriptionLifetimeMinutes)
        {
            violations.Add(
                $"SubscriptionLifetimeMinutes must be between 1 and {MaxSubscriptionLifetimeMinutes} inclusive."
            );
        }

        if (options.RenewalLeadMinutes < 1)
        {
            violations.Add("RenewalLeadMinutes must be at least 1.");
        }
        else if (options.RenewalLeadMinutes >= options.SubscriptionLifetimeMinutes)
        {
            violations.Add(
                "RenewalLeadMinutes must be strictly less than SubscriptionLifetimeMinutes."
            );
        }

        if (options.ReconcileIntervalMinutes < 1)
        {
            violations.Add("ReconcileIntervalMinutes must be at least 1.");
        }

        if (options.QueueCapacity < 1)
        {
            violations.Add("QueueCapacity must be at least 1.");
        }

        return violations;
    }
}
