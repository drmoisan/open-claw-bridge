using System.Globalization;
using Microsoft.Data.Sqlite;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core;

/// <summary>
/// <see cref="ISeriesMoveHistory"/> implementation for <see cref="CoreCacheRepository"/>
/// (issue #105). Persists per-series move records in the <c>series_moves</c> table using
/// the repository's per-call connection pattern. Timestamps are caller-supplied; this
/// partial has no clock dependency. A lazy once-per-instance schema-ensure guard creates
/// the <c>series_moves</c> table before the first store operation so the store is safe on
/// databases that have not run <see cref="InitializeAsync"/> since the table was added.
/// </summary>
internal sealed partial class CoreCacheRepository : ISeriesMoveHistory
{
    private const string CreateSeriesMovesTableSql =
        "CREATE TABLE IF NOT EXISTS series_moves(series_key TEXT NOT NULL, occurrence_start_utc TEXT NOT NULL, moved_at_utc TEXT NOT NULL, PRIMARY KEY(series_key, occurrence_start_utc));";

    private bool seriesMovesSchemaEnsured;

    /// <inheritdoc />
    public async Task RecordMoveAsync(
        string seriesKey,
        DateTimeOffset occurrenceStartUtc,
        DateTimeOffset movedAtUtc,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(seriesKey))
        {
            throw new ArgumentException(
                "Series key must be a non-empty, non-whitespace string.",
                nameof(seriesKey)
            );
        }

        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureSeriesMovesSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO series_moves(series_key, occurrence_start_utc, moved_at_utc)
VALUES($series_key, $occurrence_start_utc, $moved_at_utc)
ON CONFLICT(series_key, occurrence_start_utc) DO NOTHING;";
        command.Parameters.AddWithValue("$series_key", seriesKey);
        command.Parameters.AddWithValue(
            "$occurrence_start_utc",
            occurrenceStartUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        );
        command.Parameters.AddWithValue(
            "$moved_at_utc",
            movedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        );
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DateTimeOffset>> GetMovedOccurrenceStartsAsync(
        string seriesKey,
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureSeriesMovesSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT DISTINCT occurrence_start_utc FROM series_moves WHERE series_key = $key ORDER BY occurrence_start_utc DESC;";
        command.Parameters.AddWithValue("$key", seriesKey);

        var results = new List<DateTimeOffset>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(
                DateTimeOffset.Parse(
                    reader.GetString(0),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                )
            );
        }
        return results;
    }

    /// <summary>
    /// Lazily ensures the <c>series_moves</c> table exists, once per repository instance.
    /// The DDL is <c>CREATE TABLE IF NOT EXISTS</c>, so a concurrent duplicate execution
    /// is harmless; the flag only avoids repeated DDL round-trips.
    /// </summary>
    private async Task EnsureSeriesMovesSchemaAsync(
        SqliteConnection connection,
        CancellationToken ct
    )
    {
        if (seriesMovesSchemaEnsured)
        {
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = CreateSeriesMovesTableSql;
        await command.ExecuteNonQueryAsync(ct);
        seriesMovesSchemaEnsured = true;
    }
}
