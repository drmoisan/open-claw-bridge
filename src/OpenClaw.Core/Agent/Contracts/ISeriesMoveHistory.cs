namespace OpenClaw.Core.Agent;

/// <summary>
/// Durable per-series move-history store (issue #105). Records which occurrences of a
/// recurring series have been moved so the one-on-one move guard can answer its history
/// questions. The store has no clock dependency: all timestamps are caller-supplied.
/// </summary>
public interface ISeriesMoveHistory
{
    /// <summary>
    /// Records that the occurrence of the series starting at
    /// <paramref name="occurrenceStartUtc"/> was moved. Idempotent: recording an identical
    /// (<paramref name="seriesKey"/>, <paramref name="occurrenceStartUtc"/>) pair again
    /// succeeds without error and leaves a single record.
    /// </summary>
    /// <param name="seriesKey">The stable series key (see <c>OneOnOneMoveGuard.ResolveSeriesKey</c>).</param>
    /// <param name="occurrenceStartUtc">The caller-supplied UTC start of the moved occurrence (pre-move).</param>
    /// <param name="movedAtUtc">
    /// The caller-supplied UTC timestamp of the move (the store has no clock dependency).
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the record is durable.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="seriesKey"/> is null, empty, or whitespace-only.
    /// </exception>
    Task RecordMoveAsync(
        string seriesKey,
        DateTimeOffset occurrenceStartUtc,
        DateTimeOffset movedAtUtc,
        CancellationToken ct
    );

    /// <summary>
    /// Returns the distinct recorded occurrence-start timestamps for the given series,
    /// most recent first.
    /// </summary>
    /// <param name="seriesKey">The stable series key (see <c>OneOnOneMoveGuard.ResolveSeriesKey</c>).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The distinct recorded occurrence starts, most recent first.</returns>
    Task<IReadOnlyList<DateTimeOffset>> GetMovedOccurrenceStartsAsync(
        string seriesKey,
        CancellationToken ct
    );
}
