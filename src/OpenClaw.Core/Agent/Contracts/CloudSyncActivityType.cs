namespace OpenClaw.Core.Agent;

/// <summary>
/// The closed set of CloudSync/Graph activity event types recorded in
/// <see cref="ActionAuditRecord.ActionType"/> (issue #124). Modeled as
/// <see langword="const"/> strings rather than an enum, matching the existing
/// <see cref="SentActionKey"/>/<see cref="ActionAuditResultCode"/> extensibility pattern, so
/// the persisted <c>TEXT</c> value round-trips to SQLite without a mapping layer and future
/// CloudSync event types can be appended without a contract or schema change.
/// </summary>
public static class CloudSyncActivityType
{
    /// <summary>A Graph change-notification subscription was created.</summary>
    public const string SubscriptionCreated = "subscription-created";

    /// <summary>A Graph change-notification subscription was renewed.</summary>
    public const string SubscriptionRenewed = "subscription-renewed";

    /// <summary>A Graph change-notification subscription reauthorization failed and expired.</summary>
    public const string SubscriptionExpired = "subscription-expired";

    /// <summary>A Graph change-notification subscription was removed (and is being recreated).</summary>
    public const string SubscriptionRemoved = "subscription-removed";

    /// <summary>A Graph webhook notification was received and enqueued for processing.</summary>
    public const string WebhookReceived = "webhook-received";

    /// <summary>A Graph webhook notification was rejected before enqueueing.</summary>
    public const string WebhookRejected = "webhook-rejected";

    /// <summary>A delta reconciliation run completed (success or failure).</summary>
    public const string DeltaReconciliationRun = "delta-reconciliation-run";
}
