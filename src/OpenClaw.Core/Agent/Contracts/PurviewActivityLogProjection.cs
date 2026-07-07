namespace OpenClaw.Core.Agent;

/// <summary>
/// Pure, host-neutral mapping from <see cref="ActionAuditRecord"/> to the illustrative
/// <see cref="PurviewActivityLogRecord"/> shape (issue #124, spec.md decision 4). No I/O, no
/// network call, no clock read, no randomness — every output field is derived solely and
/// deterministically from the input record. <see cref="Project"/> is total over every
/// <c>ActionType</c>/<c>ResultCode</c> value currently in use (existing send/calendar values
/// plus the seven new CloudSync values) and never throws for a valid
/// <see cref="ActionAuditRecord"/>: unrecognized values fall back to a documented default
/// rather than throwing, so the projection remains total as new action/result codes are added
/// (per the const-string extensibility pattern already used by <see cref="SentActionKey"/> and
/// <see cref="ActionAuditResultCode"/>).
/// </summary>
public static class PurviewActivityLogProjection
{
    private const string CloudSyncCategory = "CloudSyncActivity";
    private const string SendCategory = "SendActivity";
    private const string UnknownCategory = "UnknownActivity";
    private const string AppOnlyInitiatedBy = "app-only-service-principal";
    private const string UnknownOperationType = "Unknown";
    private const string UnknownResult = "unknown";

    /// <summary>Projects <paramref name="record"/> to its illustrative Purview/Graph activity-log shape.</summary>
    /// <param name="record">The source audit record.</param>
    /// <returns>The projected, illustrative Purview/Graph <c>directoryAudit</c>-style record.</returns>
    public static PurviewActivityLogRecord Project(ActionAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var (activityDisplayName, operationType, category) = MapActionType(record.ActionType);
        var result = MapResultCode(record.ResultCode);
        IReadOnlyList<string> targetResources = [record.Mailbox, record.MessageId];
        IReadOnlyDictionary<string, string> additionalDetails = new Dictionary<string, string>
        {
            ["actionType"] = record.ActionType,
            ["resultCode"] = record.ResultCode,
        };

        return new PurviewActivityLogRecord(
            Id: record.CorrelationId,
            ActivityDateTime: record.RecordedAtUtc,
            ActivityDisplayName: activityDisplayName,
            Category: category,
            CorrelationId: record.CorrelationId,
            OperationType: operationType,
            Result: result,
            ResultReason: record.ErrorDetail,
            InitiatedBy: AppOnlyInitiatedBy,
            TargetResources: targetResources,
            AdditionalDetails: additionalDetails
        );
    }

    /// <summary>Maps an <c>ActionType</c> constant to a display name, operation type, and category.</summary>
    private static (string DisplayName, string OperationType, string Category) MapActionType(
        string actionType
    ) =>
        actionType switch
        {
            SentActionKey.ProposalReply => ("Send proposal reply", "Add", SendCategory),
            CloudSyncActivityType.SubscriptionCreated => (
                "Create Graph subscription",
                "Add",
                CloudSyncCategory
            ),
            CloudSyncActivityType.SubscriptionRenewed => (
                "Renew Graph subscription",
                "Update",
                CloudSyncCategory
            ),
            CloudSyncActivityType.SubscriptionExpired => (
                "Graph subscription reauthorization failed",
                "Update",
                CloudSyncCategory
            ),
            CloudSyncActivityType.SubscriptionRemoved => (
                "Remove Graph subscription",
                "Delete",
                CloudSyncCategory
            ),
            CloudSyncActivityType.WebhookReceived => (
                "Receive Graph webhook notification",
                "Notify",
                CloudSyncCategory
            ),
            CloudSyncActivityType.WebhookRejected => (
                "Reject Graph webhook notification",
                "Reject",
                CloudSyncCategory
            ),
            CloudSyncActivityType.DeltaReconciliationRun => (
                "Run delta reconciliation",
                "Reconcile",
                CloudSyncCategory
            ),
            _ => (actionType, UnknownOperationType, UnknownCategory),
        };

    /// <summary>Maps a <c>ResultCode</c> constant to the illustrative Purview <c>result</c> value.</summary>
    private static string MapResultCode(string resultCode) =>
        resultCode switch
        {
            ActionAuditResultCode.Sent => "success",
            ActionAuditResultCode.DedupeSkipped => "success",
            ActionAuditResultCode.SendDisabled => "success",
            ActionAuditResultCode.SendFailed => "failure",
            CloudSyncActivityResultCode.Success => "success",
            CloudSyncActivityResultCode.Failure => "failure",
            CloudSyncActivityResultCode.UnknownSubscription => "failure",
            CloudSyncActivityResultCode.ClientStateMismatch => "failure",
            CloudSyncActivityResultCode.MissingResourceId => "failure",
            _ => UnknownResult,
        };
}
