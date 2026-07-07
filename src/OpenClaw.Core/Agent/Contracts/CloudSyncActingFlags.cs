namespace OpenClaw.Core.Agent;

/// <summary>
/// Fixed <see cref="ActionAuditRecord.ActingFlags"/> value for CloudSync/Graph activity events
/// (issue #124, spec.md decision 1). The send/calendar acting-flags string
/// (<c>SendEnabled=&lt;bool&gt;;CalendarWriteEnabled=&lt;bool&gt;</c>) has no CloudSync
/// analogue; this constant documents that explicitly rather than reusing a send/calendar
/// value or relaxing the store's required-non-empty-field validation.
/// </summary>
public static class CloudSyncActingFlags
{
    /// <summary>The CloudSync-domain analogue of the send/calendar acting-flags string.</summary>
    public const string NotApplicable = "N/A:CloudSyncActivity";
}
