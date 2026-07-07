namespace OpenClaw.Core.Agent;

/// <summary>
/// Illustrative Microsoft Graph <c>directoryAudit</c>-style activity-log record (issue #124,
/// spec.md decision 4). Field names/structure are pinned to the <c>directoryAudit</c> resource
/// shape (Microsoft Learn,
/// <c>https://learn.microsoft.com/en-us/graph/api/resources/directoryaudit</c>, captured
/// 2026-07-07). This mapping is aspirational/illustrative only: no live Microsoft Purview or
/// Graph activity-log endpoint exists in this environment or CI, so this record is never sent
/// to a real endpoint by this feature — it is the auditable, testable projection contract
/// itself, produced by <see cref="PurviewActivityLogProjection"/>.
/// </summary>
/// <param name="Id">A unique activity id.</param>
/// <param name="ActivityDateTime">When the activity occurred, in UTC.</param>
/// <param name="ActivityDisplayName">A human-readable operation name.</param>
/// <param name="Category">The resource category grouping for this activity.</param>
/// <param name="CorrelationId">The correlation id threading this activity to related records.</param>
/// <param name="OperationType">The operation semantics (for example Add/Update/Delete-like).</param>
/// <param name="Result">The outcome, for example <c>success</c> or <c>failure</c>.</param>
/// <param name="ResultReason">The failure reason, or <see langword="null"/> when <see cref="Result"/> did not fail.</param>
/// <param name="InitiatedBy">A minimal descriptor of the initiating identity (always app-only for this service).</param>
/// <param name="TargetResources">The resource identifiers this activity acted on.</param>
/// <param name="AdditionalDetails">An open-ended extension bag for fields outside the fixed shape.</param>
public sealed record PurviewActivityLogRecord(
    string Id,
    DateTimeOffset ActivityDateTime,
    string ActivityDisplayName,
    string Category,
    string CorrelationId,
    string OperationType,
    string Result,
    string? ResultReason,
    string InitiatedBy,
    IReadOnlyList<string> TargetResources,
    IReadOnlyDictionary<string, string> AdditionalDetails
);
