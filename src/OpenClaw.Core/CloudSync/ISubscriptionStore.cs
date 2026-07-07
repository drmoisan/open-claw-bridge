namespace OpenClaw.Core.CloudSync;

/// <summary>The <c>graph_subscriptions.status</c> values.</summary>
internal static class SubscriptionStatus
{
    /// <summary>The subscription is live and renewable.</summary>
    internal const string Active = "active";

    /// <summary>A <c>reauthorizationRequired</c> renewal failed with an auth error.</summary>
    internal const string ReauthorizeFailed = "reauthorize_failed";
}

/// <summary>
/// A durable Graph subscription record (table <c>graph_subscriptions</c>): id,
/// resource, mailbox, per-subscription <c>clientState</c> secret, expiration, and
/// status. Survives restart so renewal and webhook validation work across host
/// lifetimes (master §6.1, §13 Step 4).
/// </summary>
/// <param name="SubscriptionId">The Graph subscription id (primary key).</param>
/// <param name="Resource">The subscribed Graph resource path.</param>
/// <param name="Mailbox">The mailbox UPN the subscription watches.</param>
/// <param name="ClientState">The per-subscription secret compared constant-time (D-1).</param>
/// <param name="ExpirationUtc">The subscription's expiration instant.</param>
/// <param name="Status">One of the <see cref="SubscriptionStatus"/> values.</param>
internal sealed record GraphSubscriptionRecord(
    string SubscriptionId,
    string Resource,
    string Mailbox,
    string ClientState,
    DateTimeOffset ExpirationUtc,
    string Status
);

/// <summary>
/// Persistence seam for Graph subscription state, implemented by the
/// <c>CoreCacheRepository.Subscriptions</c> partial. All timestamps are
/// caller-supplied (clock-free contract); the store performs no clock reads.
/// </summary>
internal interface ISubscriptionStore
{
    /// <summary>Returns the subscription with <paramref name="subscriptionId"/>, or null when absent.</summary>
    /// <param name="subscriptionId">The Graph subscription id to look up.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task<GraphSubscriptionRecord?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken ct
    );

    /// <summary>Returns every stored subscription (single-principal deployments store at most a handful).</summary>
    /// <param name="ct">Cancels the operation.</param>
    Task<IReadOnlyList<GraphSubscriptionRecord>> ListSubscriptionsAsync(CancellationToken ct);

    /// <summary>
    /// Inserts or replaces the record keyed on its subscription id. The insert path
    /// stamps <paramref name="nowUtc"/> as both created and updated timestamps; the
    /// update path preserves the original created timestamp.
    /// </summary>
    /// <param name="record">The subscription record to persist.</param>
    /// <param name="nowUtc">The caller-supplied write instant.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task UpsertSubscriptionAsync(
        GraphSubscriptionRecord record,
        DateTimeOffset nowUtc,
        CancellationToken ct
    );

    /// <summary>Updates only the status (and updated timestamp) of an existing record.</summary>
    /// <param name="subscriptionId">The Graph subscription id to update.</param>
    /// <param name="status">One of the <see cref="SubscriptionStatus"/> values.</param>
    /// <param name="updatedAtUtc">The caller-supplied write instant.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task UpdateSubscriptionStatusAsync(
        string subscriptionId,
        string status,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct
    );

    /// <summary>Deletes the record with <paramref name="subscriptionId"/>; deleting an absent row is a no-op.</summary>
    /// <param name="subscriptionId">The Graph subscription id to delete.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken ct);
}
