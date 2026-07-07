namespace OpenClaw.Core;

/// <summary>
/// Boundary-preserving audit-emission port for CloudSync activity events (issue #124,
/// architecture-boundary revision — see
/// <c>docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/other/architecture-boundary-conflict.md</c>).
/// Declared in the bare <c>OpenClaw.Core</c> namespace — the one non-CloudSync namespace
/// <c>CloudSyncArchitectureBoundaryTests</c> explicitly allows
/// <c>OpenClaw.Core.CloudSync</c> to depend on — so <c>GraphSubscriptionManager</c>,
/// <c>NotificationRequestProcessor</c>, and <c>GraphDeltaReconciler</c> can emit audit
/// events without referencing <c>OpenClaw.Core.Agent</c>. The
/// <c>OpenClaw.Core.Agent.CloudSyncActivityAuditor</c> adapter implements this port and
/// owns the mapping to <c>ActionAuditRecord</c>/<c>IActionAuditLog</c>.
/// </summary>
internal interface ICloudSyncActivityAuditor
{
    /// <summary>Records a <c>CloudSyncActivityType.SubscriptionCreated</c> event.</summary>
    /// <param name="mailbox">The principal mailbox UPN.</param>
    /// <param name="subscriptionId">The created subscription id, or <see langword="null"/> on a failed create.</param>
    /// <param name="correlationId">The Graph request correlation id.</param>
    /// <param name="success">Whether the create succeeded.</param>
    /// <param name="errorDetail">The failure detail, or <see langword="null"/> on success.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordSubscriptionCreatedAsync(
        string mailbox,
        string? subscriptionId,
        string? correlationId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    );

    /// <summary>Records a <c>CloudSyncActivityType.SubscriptionRenewed</c> event.</summary>
    /// <param name="mailbox">The principal mailbox UPN.</param>
    /// <param name="subscriptionId">The renewed subscription id.</param>
    /// <param name="correlationId">The Graph request correlation id.</param>
    /// <param name="success">Whether the renewal succeeded.</param>
    /// <param name="errorDetail">The failure detail, or <see langword="null"/> on success.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordSubscriptionRenewedAsync(
        string mailbox,
        string subscriptionId,
        string? correlationId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    );

    /// <summary>Records a <c>CloudSyncActivityType.SubscriptionExpired</c> event.</summary>
    /// <param name="mailbox">The principal mailbox UPN.</param>
    /// <param name="subscriptionId">The expired subscription id.</param>
    /// <param name="correlationId">The renewal request correlation id.</param>
    /// <param name="errorDetail">The reauthorization failure detail.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordSubscriptionExpiredAsync(
        string mailbox,
        string subscriptionId,
        string? correlationId,
        string? errorDetail,
        CancellationToken ct
    );

    /// <summary>Records a <c>CloudSyncActivityType.SubscriptionRemoved</c> event.</summary>
    /// <param name="mailbox">The principal mailbox UPN.</param>
    /// <param name="subscriptionId">The removed subscription id.</param>
    /// <param name="correlationId">A freshly generated correlation id.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordSubscriptionRemovedAsync(
        string mailbox,
        string subscriptionId,
        string correlationId,
        CancellationToken ct
    );

    /// <summary>Records a <c>CloudSyncActivityType.WebhookReceived</c> event.</summary>
    /// <param name="mailbox">The subscription's mailbox.</param>
    /// <param name="messageId">The subscription id (lifecycle) or <c>resourceData.id</c> (change).</param>
    /// <param name="correlationId">The per-notification-item correlation id.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordWebhookReceivedAsync(
        string mailbox,
        string messageId,
        string correlationId,
        CancellationToken ct
    );

    /// <summary>Records a <c>CloudSyncActivityType.WebhookRejected</c> event.</summary>
    /// <param name="mailbox">The subscription's mailbox, or an unresolved identifier when no subscription is known.</param>
    /// <param name="messageId">The subscription id or unresolved identifier for the rejected item.</param>
    /// <param name="rejectionReasonCode">The <c>CloudSyncActivityResultCode</c> rejection-reason constant.</param>
    /// <param name="correlationId">The per-notification-item correlation id.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordWebhookRejectedAsync(
        string mailbox,
        string messageId,
        string rejectionReasonCode,
        string correlationId,
        CancellationToken ct
    );

    /// <summary>Records a <c>CloudSyncActivityType.DeltaReconciliationRun</c> event.</summary>
    /// <param name="mailbox">The reconciled mailbox.</param>
    /// <param name="requestId">The delta-reconcile request id (also used as the correlation id).</param>
    /// <param name="success">Whether the reconciliation run succeeded.</param>
    /// <param name="errorDetail">The failure detail, or <see langword="null"/> on success.</param>
    /// <param name="ct">Cancels the audit write.</param>
    Task RecordDeltaReconciliationRunAsync(
        string mailbox,
        string requestId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    );
}
