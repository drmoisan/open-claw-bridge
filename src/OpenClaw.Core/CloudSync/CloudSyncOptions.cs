namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Graph change-notification / delta-reconciliation configuration (issue #117). Bound
/// from the <c>OpenClaw:CloudSync</c> configuration section (env form
/// <c>OpenClaw__CloudSync__*</c>). This type is a plain options bag; all validation
/// lives in <see cref="CloudSyncOptionsValidator"/> (D-7).
/// </summary>
public sealed class CloudSyncOptions
{
    /// <summary>The configuration section this options type binds from.</summary>
    public const string SectionName = "OpenClaw:CloudSync";

    /// <summary>
    /// Opt-in gate for the webhook endpoint and the CloudSync workers (D-6). Default
    /// <c>false</c>: the composition root registers and maps exactly what it does
    /// today. Env binding: <c>OpenClaw__CloudSync__Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The public absolute <c>https</c> URL Graph posts notifications to; used as both
    /// <c>notificationUrl</c> and <c>lifecycleNotificationUrl</c> on subscription
    /// create. Required when <see cref="Enabled"/>. Env binding:
    /// <c>OpenClaw__CloudSync__NotificationUrl</c>.
    /// </summary>
    public string? NotificationUrl { get; set; }

    /// <summary>
    /// Requested subscription lifetime in minutes; the validator caps at 10,080 (the
    /// master §6.1 maximum for Outlook message subscriptions without resource data).
    /// Env binding: <c>OpenClaw__CloudSync__SubscriptionLifetimeMinutes</c>.
    /// </summary>
    public int SubscriptionLifetimeMinutes { get; set; } = 10080;

    /// <summary>
    /// Renewal lead in minutes: a subscription is due for renewal when
    /// <c>now &gt;= expiration - lead</c>. Must be at least 1 and strictly less than
    /// <see cref="SubscriptionLifetimeMinutes"/>. Env binding:
    /// <c>OpenClaw__CloudSync__RenewalLeadMinutes</c>.
    /// </summary>
    public int RenewalLeadMinutes { get; set; } = 30;

    /// <summary>
    /// Periodic delta-reconciliation cadence in minutes (webhooks are wake signals,
    /// delta is truth). Env binding: <c>OpenClaw__CloudSync__ReconcileIntervalMinutes</c>.
    /// </summary>
    public int ReconcileIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Bounded notification-queue capacity (D-4); writes beyond capacity are dropped
    /// with a Warning and recovered by delta reconciliation. Env binding:
    /// <c>OpenClaw__CloudSync__QueueCapacity</c>.
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;
}
