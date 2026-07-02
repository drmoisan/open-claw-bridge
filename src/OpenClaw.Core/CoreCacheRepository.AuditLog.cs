using System.Globalization;
using Microsoft.Data.Sqlite;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core;

/// <summary>
/// <see cref="IActionAuditLog"/> implementation for <see cref="CoreCacheRepository"/>
/// (issue #107). Persists structured outbound-action audit records in the <c>audit_log</c>
/// table using the repository's per-call connection pattern. Timestamps are caller-supplied
/// and stored in UTC round-trip (<c>O</c>) form; this partial has no clock dependency. A lazy
/// once-per-instance schema-ensure guard creates the <c>audit_log</c> table and its
/// <c>message_id</c> index before the first store operation so the store is safe on databases
/// that have not run <see cref="InitializeAsync"/> since the table was added.
/// </summary>
internal sealed partial class CoreCacheRepository : IActionAuditLog
{
    private const string CreateAuditLogTableSql =
        @"
CREATE TABLE IF NOT EXISTS audit_log(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    mailbox TEXT NOT NULL,
    message_id TEXT NOT NULL,
    event_id TEXT NULL,
    action_type TEXT NOT NULL,
    acting_flags TEXT NOT NULL,
    correlation_id TEXT NOT NULL,
    result_code TEXT NOT NULL,
    error_detail TEXT NULL,
    original_start_utc TEXT NULL,
    original_end_utc TEXT NULL,
    new_start_utc TEXT NULL,
    new_end_utc TEXT NULL,
    recorded_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_audit_log_message_id ON audit_log(message_id);";

    private bool auditLogSchemaEnsured;

    /// <inheritdoc />
    public async Task RecordAsync(ActionAuditRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ThrowIfEmpty(record.Mailbox, nameof(record.Mailbox));
        ThrowIfEmpty(record.MessageId, nameof(record.MessageId));
        ThrowIfEmpty(record.ActionType, nameof(record.ActionType));
        ThrowIfEmpty(record.ActingFlags, nameof(record.ActingFlags));
        ThrowIfEmpty(record.CorrelationId, nameof(record.CorrelationId));
        ThrowIfEmpty(record.ResultCode, nameof(record.ResultCode));

        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureAuditLogSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO audit_log(
    mailbox, message_id, event_id, action_type, acting_flags, correlation_id,
    result_code, error_detail, original_start_utc, original_end_utc,
    new_start_utc, new_end_utc, recorded_at_utc)
VALUES(
    $mailbox, $message_id, $event_id, $action_type, $acting_flags, $correlation_id,
    $result_code, $error_detail, $original_start_utc, $original_end_utc,
    $new_start_utc, $new_end_utc, $recorded_at_utc);";
        command.Parameters.AddWithValue("$mailbox", record.Mailbox);
        command.Parameters.AddWithValue("$message_id", record.MessageId);
        command.Parameters.AddWithValue("$event_id", (object?)record.EventId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action_type", record.ActionType);
        command.Parameters.AddWithValue("$acting_flags", record.ActingFlags);
        command.Parameters.AddWithValue("$correlation_id", record.CorrelationId);
        command.Parameters.AddWithValue("$result_code", record.ResultCode);
        command.Parameters.AddWithValue(
            "$error_detail",
            (object?)record.ErrorDetail ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$original_start_utc",
            ToDbTimestamp(record.OriginalStartUtc)
        );
        command.Parameters.AddWithValue("$original_end_utc", ToDbTimestamp(record.OriginalEndUtc));
        command.Parameters.AddWithValue("$new_start_utc", ToDbTimestamp(record.NewStartUtc));
        command.Parameters.AddWithValue("$new_end_utc", ToDbTimestamp(record.NewEndUtc));
        command.Parameters.AddWithValue(
            "$recorded_at_utc",
            record.RecordedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        );
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActionAuditRecord>> GetByMessageIdAsync(
        string messageId,
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureAuditLogSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
SELECT mailbox, message_id, event_id, action_type, acting_flags, correlation_id,
       result_code, error_detail, original_start_utc, original_end_utc,
       new_start_utc, new_end_utc, recorded_at_utc
FROM audit_log
WHERE message_id = $message_id
ORDER BY recorded_at_utc DESC, id DESC;";
        command.Parameters.AddWithValue("$message_id", messageId);

        var results = new List<ActionAuditRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(
                new ActionAuditRecord(
                    Mailbox: reader.GetString(0),
                    MessageId: reader.GetString(1),
                    EventId: reader.IsDBNull(2) ? null : reader.GetString(2),
                    ActionType: reader.GetString(3),
                    ActingFlags: reader.GetString(4),
                    CorrelationId: reader.GetString(5),
                    ResultCode: reader.GetString(6),
                    ErrorDetail: reader.IsDBNull(7) ? null : reader.GetString(7),
                    OriginalStartUtc: ReadNullableTimestamp(reader, 8),
                    OriginalEndUtc: ReadNullableTimestamp(reader, 9),
                    NewStartUtc: ReadNullableTimestamp(reader, 10),
                    NewEndUtc: ReadNullableTimestamp(reader, 11),
                    RecordedAtUtc: ParseTimestamp(reader.GetString(12))
                )
            );
        }
        return results;
    }

    /// <summary>
    /// Fail-fast guard for the required <see cref="ActionAuditRecord"/> string fields,
    /// mirroring the <c>series_moves</c> guard style.
    /// </summary>
    /// <exception cref="ArgumentException">The value is null, empty, or whitespace-only.</exception>
    private static void ThrowIfEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"Audit record field '{fieldName}' must be a non-empty, non-whitespace string.",
                fieldName
            );
        }
    }

    /// <summary>
    /// Converts an optional timestamp to its stored form: UTC round-trip (<c>O</c>) text, or
    /// <see cref="DBNull.Value"/> when absent.
    /// </summary>
    private static object ToDbTimestamp(DateTimeOffset? value) =>
        value is null
            ? DBNull.Value
            : value.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ReadNullableTimestamp(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseTimestamp(reader.GetString(ordinal));

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>
    /// Lazily ensures the <c>audit_log</c> table and its <c>message_id</c> index exist, once
    /// per repository instance. The DDL is <c>IF NOT EXISTS</c>, so a concurrent duplicate
    /// execution is harmless; the flag only avoids repeated DDL round-trips.
    /// </summary>
    private async Task EnsureAuditLogSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        if (auditLogSchemaEnsured)
        {
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = CreateAuditLogTableSql;
        await command.ExecuteNonQueryAsync(ct);
        auditLogSchemaEnsured = true;
    }
}
