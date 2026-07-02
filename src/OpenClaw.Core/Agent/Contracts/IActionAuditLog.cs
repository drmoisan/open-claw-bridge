namespace OpenClaw.Core.Agent;

/// <summary>
/// Durable structured audit log for outbound actions (issue #107). The scheduling worker
/// writes exactly one record per Stage 0 decision point. The store has no clock dependency:
/// <see cref="ActionAuditRecord.RecordedAtUtc"/> is caller-supplied. Resilience boundary
/// (spec D4): implementations may throw on failure; the worker wraps every write in its one
/// sanctioned catch-and-log helper so an audit-sink fault never breaks message processing.
/// </summary>
public interface IActionAuditLog
{
    /// <summary>Durably records the given audit record.</summary>
    /// <param name="record">The audit record to persist.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the record is durable.</returns>
    /// <exception cref="ArgumentException">
    /// A required field of <paramref name="record"/> is empty or whitespace-only.
    /// </exception>
    Task RecordAsync(ActionAuditRecord record, CancellationToken ct);

    /// <summary>Returns the audit records for the given message id, most recent first.</summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The matching records, ordered most recent first.</returns>
    Task<IReadOnlyList<ActionAuditRecord>> GetByMessageIdAsync(
        string messageId,
        CancellationToken ct
    );
}
