namespace OpenClaw.Core.Agent;

/// <summary>
/// Composition-root-registered adapter implementing <see cref="ICloudSyncActivityAuditor"/>
/// (issue #124, architecture-boundary revision). Owns the mapping from the port's semantic
/// method calls to <see cref="ActionAuditRecord"/>/<see cref="IActionAuditLog.RecordAsync"/>,
/// applying the decision-1 field conventions: <see cref="ActionAuditRecord.ActingFlags"/> is
/// always <see cref="CloudSyncActingFlags.NotApplicable"/>; <see cref="ActionAuditRecord.MessageId"/>
/// carries the subject-resource identifier available at each call site;
/// <see cref="ActionAuditRecord.CorrelationId"/> threads the caller-supplied correlation id
/// or request id. Registered at the composition root (<c>Program.cs</c>), not inside
/// <c>CloudSyncServiceCollectionExtensions</c>, so <c>OpenClaw.Core.CloudSync</c> never
/// depends on <c>OpenClaw.Core.Agent</c>.
/// </summary>
internal sealed class CloudSyncActivityAuditor : ICloudSyncActivityAuditor
{
    private readonly IActionAuditLog actionAuditLog;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the adapter; both dependencies are required.</summary>
    /// <param name="actionAuditLog">The durable audit sink to write through.</param>
    /// <param name="timeProvider">Supplies <see cref="ActionAuditRecord.RecordedAtUtc"/>.</param>
    public CloudSyncActivityAuditor(IActionAuditLog actionAuditLog, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(actionAuditLog);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.actionAuditLog = actionAuditLog;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task RecordSubscriptionCreatedAsync(
        string mailbox,
        string? subscriptionId,
        string? correlationId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    ) =>
        RecordAsync(
            mailbox,
            messageId: subscriptionId ?? mailbox,
            CloudSyncActivityType.SubscriptionCreated,
            correlationId,
            success,
            errorDetail,
            ct
        );

    /// <inheritdoc />
    public Task RecordSubscriptionRenewedAsync(
        string mailbox,
        string subscriptionId,
        string? correlationId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    ) =>
        RecordAsync(
            mailbox,
            subscriptionId,
            CloudSyncActivityType.SubscriptionRenewed,
            correlationId,
            success,
            errorDetail,
            ct
        );

    /// <inheritdoc />
    public Task RecordSubscriptionExpiredAsync(
        string mailbox,
        string subscriptionId,
        string? correlationId,
        string? errorDetail,
        CancellationToken ct
    ) =>
        RecordAsync(
            mailbox,
            subscriptionId,
            CloudSyncActivityType.SubscriptionExpired,
            correlationId,
            success: false,
            errorDetail,
            ct
        );

    /// <inheritdoc />
    public Task RecordSubscriptionRemovedAsync(
        string mailbox,
        string subscriptionId,
        string correlationId,
        CancellationToken ct
    ) =>
        RecordAsync(
            mailbox,
            subscriptionId,
            CloudSyncActivityType.SubscriptionRemoved,
            correlationId,
            success: true,
            errorDetail: null,
            ct
        );

    /// <inheritdoc />
    public Task RecordWebhookReceivedAsync(
        string mailbox,
        string messageId,
        string correlationId,
        CancellationToken ct
    ) =>
        RecordAsync(
            mailbox,
            messageId,
            CloudSyncActivityType.WebhookReceived,
            correlationId,
            success: true,
            errorDetail: null,
            ct
        );

    /// <inheritdoc />
    public Task RecordWebhookRejectedAsync(
        string mailbox,
        string messageId,
        string rejectionReasonCode,
        string correlationId,
        CancellationToken ct
    ) =>
        actionAuditLog.RecordAsync(
            new ActionAuditRecord(
                Mailbox: mailbox,
                MessageId: messageId,
                EventId: null,
                ActionType: CloudSyncActivityType.WebhookRejected,
                ActingFlags: CloudSyncActingFlags.NotApplicable,
                CorrelationId: correlationId,
                ResultCode: rejectionReasonCode,
                ErrorDetail: null,
                OriginalStartUtc: null,
                OriginalEndUtc: null,
                NewStartUtc: null,
                NewEndUtc: null,
                RecordedAtUtc: timeProvider.GetUtcNow()
            ),
            ct
        );

    /// <inheritdoc />
    public Task RecordDeltaReconciliationRunAsync(
        string mailbox,
        string requestId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    ) =>
        RecordAsync(
            mailbox,
            requestId,
            CloudSyncActivityType.DeltaReconciliationRun,
            correlationId: requestId,
            success,
            errorDetail,
            ct
        );

    private Task RecordAsync(
        string mailbox,
        string messageId,
        string actionType,
        string? correlationId,
        bool success,
        string? errorDetail,
        CancellationToken ct
    ) =>
        actionAuditLog.RecordAsync(
            new ActionAuditRecord(
                Mailbox: mailbox,
                MessageId: messageId,
                EventId: null,
                ActionType: actionType,
                ActingFlags: CloudSyncActingFlags.NotApplicable,
                CorrelationId: correlationId ?? string.Empty,
                ResultCode: success
                    ? CloudSyncActivityResultCode.Success
                    : CloudSyncActivityResultCode.Failure,
                ErrorDetail: errorDetail,
                OriginalStartUtc: null,
                OriginalEndUtc: null,
                NewStartUtc: null,
                NewEndUtc: null,
                RecordedAtUtc: timeProvider.GetUtcNow()
            ),
            ct
        );
}
