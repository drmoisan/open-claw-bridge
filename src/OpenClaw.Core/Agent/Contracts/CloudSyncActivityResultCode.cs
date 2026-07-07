namespace OpenClaw.Core.Agent;

/// <summary>
/// The closed set of CloudSync/Graph activity result codes recorded in
/// <see cref="ActionAuditRecord.ResultCode"/> (issue #124), alongside the existing
/// <see cref="ActionAuditResultCode"/> constants used by send/calendar actions. Modeled as
/// <see langword="const"/> strings for the same reasons as <see cref="ActionAuditResultCode"/>:
/// TEXT round-trip to SQLite with no mapping layer, and future rejection reasons can be
/// appended without a contract or schema change. <see cref="UnknownSubscription"/>,
/// <see cref="ClientStateMismatch"/>, and <see cref="MissingResourceId"/> identify the three
/// named webhook-rejection reasons (spec.md decision 3).
/// </summary>
public static class CloudSyncActivityResultCode
{
    /// <summary>The CloudSync/Graph activity completed successfully.</summary>
    public const string Success = "success";

    /// <summary>The CloudSync/Graph activity failed.</summary>
    public const string Failure = "failure";

    /// <summary>A webhook notification was rejected because its subscription id is unknown.</summary>
    public const string UnknownSubscription = "unknown-subscription";

    /// <summary>A webhook notification was rejected because its clientState did not match.</summary>
    public const string ClientStateMismatch = "client-state-mismatch";

    /// <summary>A webhook change notification was rejected because it had no resourceData.id.</summary>
    public const string MissingResourceId = "missing-resource-id";
}
