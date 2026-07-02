namespace OpenClaw.Core.Agent;

/// <summary>
/// One structured audit record per outbound-action decision point (issue #107, spec D1).
/// Field mapping to master specification §13 step 12: target mailbox →
/// <paramref name="Mailbox"/>; event ID → <paramref name="EventId"/>; original times →
/// <paramref name="OriginalStartUtc"/>/<paramref name="OriginalEndUtc"/>; proposed/new times →
/// <paramref name="NewStartUtc"/>/<paramref name="NewEndUtc"/>; acting feature flag →
/// <paramref name="ActingFlags"/>; correlation ID → <paramref name="CorrelationId"/>; result
/// code → <paramref name="ResultCode"/>. The record carries no store-side logic; validation
/// lives in the persistence layer.
/// </summary>
/// <param name="Mailbox">The target mailbox. Required, non-empty.</param>
/// <param name="MessageId">The message identifier. Required, non-empty.</param>
/// <param name="EventId">The associated event identifier; <see langword="null"/> for message-only pipeline runs.</param>
/// <param name="ActionType">The action type, e.g. <see cref="SentActionKey.ProposalReply"/>. Required, non-empty.</param>
/// <param name="ActingFlags">The acting-flags snapshot <c>SendEnabled=&lt;bool&gt;;CalendarWriteEnabled=&lt;bool&gt;</c>. Required, non-empty.</param>
/// <param name="CorrelationId">The worker-generated GUID string for this outbound-action evaluation. Required, non-empty.</param>
/// <param name="ResultCode">The result code; see <see cref="ActionAuditResultCode"/>. Required, non-empty.</param>
/// <param name="ErrorDetail">Exception type and message for <see cref="ActionAuditResultCode.SendFailed"/>; otherwise <see langword="null"/>.</param>
/// <param name="OriginalStartUtc">The original interval start for Stage 2 (F18/F19) reschedules; <see langword="null"/> for Stage 0 send actions.</param>
/// <param name="OriginalEndUtc">The original interval end for Stage 2 reschedules; <see langword="null"/> for Stage 0 send actions.</param>
/// <param name="NewStartUtc">The proposed/new interval start for Stage 2 reschedules; <see langword="null"/> for Stage 0 send actions.</param>
/// <param name="NewEndUtc">The proposed/new interval end for Stage 2 reschedules; <see langword="null"/> for Stage 0 send actions.</param>
/// <param name="RecordedAtUtc">The caller-supplied recording timestamp; the store is clock-free.</param>
public sealed record ActionAuditRecord(
    string Mailbox,
    string MessageId,
    string? EventId,
    string ActionType,
    string ActingFlags,
    string CorrelationId,
    string ResultCode,
    string? ErrorDetail,
    DateTimeOffset? OriginalStartUtc,
    DateTimeOffset? OriginalEndUtc,
    DateTimeOffset? NewStartUtc,
    DateTimeOffset? NewEndUtc,
    DateTimeOffset RecordedAtUtc
);
