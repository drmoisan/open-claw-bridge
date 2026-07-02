namespace OpenClaw.Core.Agent;

/// <summary>
/// Durable send-idempotency store (issue #101). The scheduling worker consults the store
/// before sending an outbound action and records the action after a successful send so a
/// restart does not resend the same proposal. The store has no clock dependency: the
/// caller supplies the timestamp for <see cref="RecordAsync"/>.
/// </summary>
public interface ISentActionStore
{
    /// <summary>Returns whether the given dedupe key has already been recorded.</summary>
    /// <param name="dedupeKey">The dedupe key built by <see cref="SentActionKey.Build"/>.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> when the key is recorded; otherwise <see langword="false"/>.</returns>
    Task<bool> IsRecordedAsync(string dedupeKey, CancellationToken ct);

    /// <summary>
    /// Records the given dedupe key. Idempotent: recording an already-recorded key
    /// succeeds without error and leaves a single record.
    /// </summary>
    /// <param name="dedupeKey">The dedupe key built by <see cref="SentActionKey.Build"/>.</param>
    /// <param name="recordedAtUtc">
    /// The caller-supplied UTC timestamp of the recorded action (the store has no clock
    /// dependency).
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the record is durable.</returns>
    Task RecordAsync(string dedupeKey, DateTimeOffset recordedAtUtc, CancellationToken ct);
}
