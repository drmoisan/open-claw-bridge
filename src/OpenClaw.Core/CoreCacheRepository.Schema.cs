using Microsoft.Data.Sqlite;

namespace OpenClaw.Core;

/// <summary>
/// Schema DDL and idempotent migration helpers for <see cref="CoreCacheRepository"/>. Split into a
/// partial-class file so the (pre-existing over-cap) <c>CoreCacheRepository.cs</c> does not grow
/// further after the issue-#72 column additions. The Core <c>events</c> schema mirrors the bridge
/// cache's Graph-field columns and adds <c>last_modified_utc</c>, which the Core schema previously
/// lacked. The <c>response_status</c> column is present (added by issue #80) on both the
/// fresh-database DDL path and the guarded-ALTER upgrade path.
/// </summary>
internal sealed partial class CoreCacheRepository
{
    private const string CreateTablesSql =
        @"
CREATE TABLE IF NOT EXISTS bridge_status_snapshots(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    request_id TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    state TEXT NOT NULL,
    mode TEXT NOT NULL,
    outlook_connected INTEGER NOT NULL,
    cache_stale INTEGER NOT NULL,
    stale_reason TEXT NULL,
    last_inbox_scan_utc TEXT NULL,
    last_calendar_scan_utc TEXT NULL
);
CREATE TABLE IF NOT EXISTS messages(
    bridge_id TEXT PRIMARY KEY,
    item_kind TEXT NOT NULL,
    subject TEXT NULL,
    received_utc TEXT NULL,
    sent_utc TEXT NULL,
    importance INTEGER NULL,
    sensitivity INTEGER NULL,
    unread INTEGER NOT NULL,
    has_attachments INTEGER NOT NULL,
    message_class TEXT NULL,
    sender_name TEXT NULL,
    sender_email TEXT NULL,
    to_json TEXT NULL,
    cc_json TEXT NULL,
    body_preview TEXT NULL,
    protected_fields_available INTEGER NOT NULL,
    is_redacted INTEGER NOT NULL,
    bridge_mode TEXT NOT NULL,
    cache_stale INTEGER NOT NULL,
    stale_reason TEXT NULL,
    adapter_request_id TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    sender_email_resolved TEXT NULL,
    from_email_address TEXT NULL,
    conversation_id TEXT NULL,
    meeting_message_type INTEGER NULL
);
CREATE TABLE IF NOT EXISTS events(
    bridge_id TEXT PRIMARY KEY,
    global_appointment_id TEXT NULL,
    subject TEXT NULL,
    start_utc TEXT NOT NULL,
    end_utc TEXT NOT NULL,
    location TEXT NULL,
    busy_status INTEGER NULL,
    meeting_status INTEGER NULL,
    is_recurring INTEGER NOT NULL,
    sensitivity INTEGER NULL,
    organizer TEXT NULL,
    required_attendees_json TEXT NULL,
    optional_attendees_json TEXT NULL,
    resources_json TEXT NULL,
    body_preview TEXT NULL,
    protected_fields_available INTEGER NOT NULL,
    is_redacted INTEGER NOT NULL,
    bridge_mode TEXT NOT NULL,
    cache_stale INTEGER NOT NULL,
    stale_reason TEXT NULL,
    adapter_request_id TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    last_modified_utc TEXT NULL,
    categories_json TEXT NULL,
    is_organizer INTEGER NOT NULL DEFAULT 0,
    is_online_meeting INTEGER NOT NULL DEFAULT 0,
    allow_new_time_proposals INTEGER NOT NULL DEFAULT 0,
    ical_uid TEXT NULL,
    series_master_id TEXT NULL,
    body_full TEXT NULL,
    sensitivity_label TEXT NULL,
    response_status INTEGER NULL
);
CREATE TABLE IF NOT EXISTS poll_cursors(
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ingest_runs(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    operation_name TEXT NOT NULL,
    outcome TEXT NOT NULL,
    request_id TEXT NULL,
    started_at_utc TEXT NOT NULL,
    finished_at_utc TEXT NOT NULL,
    error_message TEXT NULL
);
CREATE TABLE IF NOT EXISTS sent_actions(dedupe_key TEXT PRIMARY KEY, mailbox TEXT NOT NULL, message_id TEXT NOT NULL, action_type TEXT NOT NULL, recorded_at_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS series_moves(series_key TEXT NOT NULL, occurrence_start_utc TEXT NOT NULL, moved_at_utc TEXT NOT NULL, PRIMARY KEY(series_key, occurrence_start_utc));
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

    /// <summary>
    /// The issue-#72 columns added to the Core <c>events</c> table on existing databases via
    /// guarded ALTER. Includes <c>last_modified_utc</c> because the Core schema did not previously
    /// have it. The <c>response_status</c> column is not part of this issue-#72 set; it is added
    /// separately in <see cref="MigrateEventsSchemaAsync"/> (issue #80). Each entry is a column
    /// name plus its column definition.
    /// </summary>
    private static readonly (string Name, string Definition)[] GraphFieldColumns =
    [
        ("last_modified_utc", "last_modified_utc TEXT NULL"),
        ("categories_json", "categories_json TEXT NULL"),
        ("is_organizer", "is_organizer INTEGER NOT NULL DEFAULT 0"),
        ("is_online_meeting", "is_online_meeting INTEGER NOT NULL DEFAULT 0"),
        ("allow_new_time_proposals", "allow_new_time_proposals INTEGER NOT NULL DEFAULT 0"),
        ("ical_uid", "ical_uid TEXT NULL"),
        ("series_master_id", "series_master_id TEXT NULL"),
        ("body_full", "body_full TEXT NULL"),
        ("sensitivity_label", "sensitivity_label TEXT NULL"),
    ];

    /// <summary>
    /// Idempotent schema migration for the Core <c>events</c> table. Adds the
    /// <c>response_status</c> column (issue #80) and the issue-#72 Graph-field columns (and
    /// <c>last_modified_utc</c>) when absent on an existing database. Running this twice is safe:
    /// each ALTER is guarded by a <c>PRAGMA table_info</c> check, so no "duplicate column" error
    /// occurs.
    /// </summary>
    private static async Task MigrateEventsSchemaAsync(SqliteConnection connection)
    {
        if (!await EventsColumnExistsAsync(connection, "response_status"))
        {
            var alterResponseStatus = connection.CreateCommand();
            alterResponseStatus.CommandText =
                "ALTER TABLE events ADD COLUMN response_status INTEGER NULL;";
            await alterResponseStatus.ExecuteNonQueryAsync();
        }

        foreach (var (name, definition) in GraphFieldColumns)
        {
            if (!await EventsColumnExistsAsync(connection, name))
            {
                var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE events ADD COLUMN {definition};";
                await alter.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<bool> EventsColumnExistsAsync(
        SqliteConnection connection,
        string column
    )
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(events);";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // table_info column index 1 is the column name.
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The issue-#73 resolved-field columns added to the Core <c>messages</c> table on existing
    /// databases via guarded ALTER. Each entry is a column name plus its column definition.
    /// </summary>
    private static readonly (string Name, string Definition)[] MessageFieldColumns =
    [
        ("sender_email_resolved", "sender_email_resolved TEXT NULL"),
        ("from_email_address", "from_email_address TEXT NULL"),
        ("conversation_id", "conversation_id TEXT NULL"),
        ("meeting_message_type", "meeting_message_type INTEGER NULL"),
    ];

    /// <summary>
    /// Idempotent schema migration for the Core <c>messages</c> table. Adds the four issue-#73
    /// resolved-field columns when absent on an existing database. Running this twice is safe: each
    /// ALTER is guarded by a <c>PRAGMA table_info</c> check, so no "duplicate column" error occurs.
    /// </summary>
    private static async Task MigrateMessagesSchemaAsync(SqliteConnection connection)
    {
        foreach (var (name, definition) in MessageFieldColumns)
        {
            if (!await MessagesColumnExistsAsync(connection, name))
            {
                var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE messages ADD COLUMN {definition};";
                await alter.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<bool> MessagesColumnExistsAsync(
        SqliteConnection connection,
        string column
    )
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(messages);";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // table_info column index 1 is the column name.
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Binds the four issue-#73 resolved-field parameters for the <c>messages</c> upsert. Lives in
    /// this partial file so the (already over-cap) main <c>CoreCacheRepository.cs</c> does not grow
    /// further. String columns bind null as <see cref="DBNull.Value"/>; <c>meeting_message_type</c>
    /// binds via <c>ToDbValue(int?)</c>.
    /// </summary>
    private static void AddMessageResolvedFieldParameters(
        SqliteCommand command,
        OpenClaw.MailBridge.Contracts.Models.MessageDto message
    )
    {
        command.Parameters.AddWithValue(
            "$sender_email_resolved",
            (object?)message.SenderEmailResolved ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$from_email_address",
            (object?)message.FromEmailAddress ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$conversation_id",
            (object?)message.ConversationId ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$meeting_message_type",
            ToDbValue(message.MeetingMessageType)
        );
    }
}
