using Microsoft.Data.Sqlite;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core;

internal sealed record CoreCacheCounts(int Messages, int MeetingRequests, int Events);

internal sealed record StoredBridgeStatusSnapshot(
    BridgeStatusDto BridgeStatus,
    string RequestId,
    DateTimeOffset ObservedAtUtc
);

internal sealed partial class CoreCacheRepository : IDisposable
{
    private readonly string connectionString;
    private readonly SqliteConnection? anchor;

    /// <summary>
    /// Exposes the connection string so that test code can open a second connection to the
    /// same in-memory database when verifying persisted state directly.
    /// </summary>
    internal string ConnectionString => connectionString;

    public CoreCacheRepository(OpenClawOptions options)
        : this($"Data Source={options.Storage.DbPath}")
    {
        var dbPath = options.Storage.DbPath;
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    internal CoreCacheRepository(string connectionString)
    {
        this.connectionString = connectionString;
        if (connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            anchor = new SqliteConnection(connectionString);
            anchor.Open();
        }
    }

    public void Dispose()
    {
        anchor?.Dispose();
    }

    public async Task InitializeAsync()
    {
        await using var connection = Open();
        await connection.OpenAsync();

        await new SqliteCommand(CreateTablesSql, connection).ExecuteNonQueryAsync();

        await MigrateEventsSchemaAsync(connection);
        await MigrateMessagesSchemaAsync(connection);
    }

    public async Task UpsertBridgeStatusSnapshotAsync(
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO bridge_status_snapshots(
    request_id, observed_at_utc, state, mode, outlook_connected, cache_stale, stale_reason,
    last_inbox_scan_utc, last_calendar_scan_utc)
VALUES(
    $request_id, $observed_at_utc, $state, $mode, $outlook_connected, $cache_stale, $stale_reason,
    $last_inbox_scan_utc, $last_calendar_scan_utc);";
        AddBridgeStatusParameters(command, bridgeStatus, requestId, observedAtUtc);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<DateTimeOffset?> GetCursorAsync(string key)
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM poll_cursors WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        var value = (string?)await command.ExecuteScalarAsync();
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    public async Task SetCursorAsync(string key, DateTimeOffset value)
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO poll_cursors(key, value, observed_at_utc)
VALUES($key, $value, $observed_at_utc)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, observed_at_utc = excluded.observed_at_utc;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue(
            "$observed_at_utc",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        );
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddIngestRunAsync(
        string operationName,
        string outcome,
        string? requestId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        string? errorMessage
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO ingest_runs(operation_name, outcome, request_id, started_at_utc, finished_at_utc, error_message)
VALUES($operation_name, $outcome, $request_id, $started_at_utc, $finished_at_utc, $error_message);";
        command.Parameters.AddWithValue("$operation_name", operationName);
        command.Parameters.AddWithValue("$outcome", outcome);
        command.Parameters.AddWithValue("$request_id", (object?)requestId ?? DBNull.Value);
        command.Parameters.AddWithValue("$started_at_utc", startedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue(
            "$finished_at_utc",
            finishedAtUtc.UtcDateTime.ToString("O")
        );
        command.Parameters.AddWithValue("$error_message", (object?)errorMessage ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CoreCacheCounts> GetCountsAsync()
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var messages = await CountAsync(
            connection,
            "SELECT COUNT(*) FROM messages WHERE item_kind <> 'meeting';"
        );
        var meetingRequests = await CountAsync(
            connection,
            "SELECT COUNT(*) FROM messages WHERE item_kind = 'meeting';"
        );
        var events = await CountAsync(connection, "SELECT COUNT(*) FROM events;");
        return new CoreCacheCounts(messages, meetingRequests, events);
    }

    public async Task<StoredBridgeStatusSnapshot?> GetLatestBridgeStatusSnapshotAsync()
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
SELECT *
FROM bridge_status_snapshots
ORDER BY observed_at_utc DESC, id DESC
LIMIT 1;";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var bridgeStatus = new BridgeStatusDto(
            ReadString(reader, "state")!,
            ReadString(reader, "mode")!,
            ReadBoolean(reader, "outlook_connected"),
            ReadBoolean(reader, "cache_stale"),
            ReadString(reader, "stale_reason"),
            ReadDateTimeOffset(reader, "last_inbox_scan_utc"),
            ReadDateTimeOffset(reader, "last_calendar_scan_utc")
        );
        return new StoredBridgeStatusSnapshot(
            bridgeStatus,
            ReadString(reader, "request_id")!,
            ReadDateTimeOffset(reader, "observed_at_utc") ?? DateTimeOffset.MinValue
        );
    }

    public async Task<DateTimeOffset?> GetLastSuccessfulPollUtcAsync()
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT MAX(finished_at_utc) FROM ingest_runs WHERE outcome = 'success';";
        var value = (string?)await command.ExecuteScalarAsync();
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private SqliteConnection Open() => new(connectionString);

    private static void AddBridgeStatusParameters(
        SqliteCommand command,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        command.Parameters.AddWithValue("$request_id", requestId);
        command.Parameters.AddWithValue(
            "$observed_at_utc",
            observedAtUtc.UtcDateTime.ToString("O")
        );
        command.Parameters.AddWithValue("$state", bridgeStatus.State);
        command.Parameters.AddWithValue("$mode", bridgeStatus.Mode);
        command.Parameters.AddWithValue(
            "$outlook_connected",
            bridgeStatus.OutlookConnected ? 1 : 0
        );
        command.Parameters.AddWithValue("$cache_stale", bridgeStatus.CacheStale ? 1 : 0);
        command.Parameters.AddWithValue(
            "$stale_reason",
            (object?)bridgeStatus.StaleReason ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$last_inbox_scan_utc",
            ToDbValue(bridgeStatus.LastInboxScanUtc)
        );
        command.Parameters.AddWithValue(
            "$last_calendar_scan_utc",
            ToDbValue(bridgeStatus.LastCalendarScanUtc)
        );
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string sql)
    {
        await using var command = new SqliteCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static object ToDbValue(DateTimeOffset? value) =>
        value is null ? DBNull.Value : value.Value.UtcDateTime.ToString("O");

    private static object ToDbValue(int? value) => value is null ? DBNull.Value : value.Value;

    private static string? ReadString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? ReadDateTimeOffset(SqliteDataReader reader, string name)
    {
        var value = ReadString(reader, name);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? ReadNullableInt(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static bool ReadBoolean(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) != 0;
    }
}
