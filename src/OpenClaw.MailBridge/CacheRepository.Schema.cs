using Microsoft.Data.Sqlite;

namespace OpenClaw.MailBridge;

/// <summary>
/// Schema DDL and idempotent migration helpers for <see cref="CacheRepository"/>. Split into a
/// partial-class file to keep <c>CacheRepository.cs</c> within the repository 500-line cap after
/// the issue-#72 column additions.
/// </summary>
internal sealed partial class CacheRepository
{
    private const string CreateTablesSql =
        @"
CREATE TABLE IF NOT EXISTS messages(bridge_id TEXT PRIMARY KEY,entry_id TEXT NOT NULL,store_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,received_utc TEXT NULL,sent_utc TEXT NULL,importance INTEGER NULL,sensitivity INTEGER NULL,unread INTEGER NOT NULL,has_attachments INTEGER NOT NULL,message_class TEXT NULL,sender_name TEXT NULL,sender_email TEXT NULL,to_json TEXT NULL,cc_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_seen_utc TEXT NOT NULL,sender_email_resolved TEXT NULL,from_email_address TEXT NULL,conversation_id TEXT NULL,meeting_message_type INTEGER NULL,linked_global_appointment_id TEXT NULL);
CREATE TABLE IF NOT EXISTS events(bridge_id TEXT PRIMARY KEY,entry_id TEXT NULL,store_id TEXT NULL,global_appointment_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,start_utc TEXT NOT NULL,end_utc TEXT NOT NULL,location TEXT NULL,busy_status INTEGER NULL,meeting_status INTEGER NULL,is_recurring INTEGER NOT NULL,sensitivity INTEGER NULL,organizer TEXT NULL,required_attendees_json TEXT NULL,optional_attendees_json TEXT NULL,resources_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_modified_utc TEXT NULL,last_seen_utc TEXT NOT NULL,response_status INTEGER NULL,categories_json TEXT NULL,is_organizer INTEGER NOT NULL DEFAULT 0,is_online_meeting INTEGER NOT NULL DEFAULT 0,allow_new_time_proposals INTEGER NOT NULL DEFAULT 0,ical_uid TEXT NULL,series_master_id TEXT NULL,body_full TEXT NULL,sensitivity_label TEXT NULL);
CREATE TABLE IF NOT EXISTS scan_state(key TEXT PRIMARY KEY,value TEXT NOT NULL);";

    /// <summary>
    /// The issue-#72 columns added to the <c>events</c> table on existing databases via guarded
    /// ALTER. <c>last_modified_utc</c> already exists in the original schema and is not listed here
    /// (only its write path is newly wired). Each entry is a column name plus its column definition.
    /// </summary>
    private static readonly (string Name, string Definition)[] GraphFieldColumns =
    [
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
    /// Idempotent schema migration for the <c>events</c> table. Adds the <c>response_status</c>
    /// column and the eight issue-#72 Graph-field columns when absent on an existing database.
    /// Running this twice is safe: each ALTER is guarded by a <c>PRAGMA table_info</c> check so no
    /// "duplicate column" error occurs.
    /// </summary>
    private static async Task MigrateEventsSchemaAsync(SqliteConnection conn)
    {
        if (!await EventsColumnExistsAsync(conn, "response_status"))
        {
            await AddEventsColumnAsync(conn, "response_status INTEGER NULL");
        }

        foreach (var (name, definition) in GraphFieldColumns)
        {
            if (!await EventsColumnExistsAsync(conn, name))
            {
                await AddEventsColumnAsync(conn, definition);
            }
        }
    }

    private static async Task AddEventsColumnAsync(SqliteConnection conn, string columnDefinition)
    {
        var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE events ADD COLUMN {columnDefinition};";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task<bool> EventsColumnExistsAsync(SqliteConnection conn, string column)
    {
        var cmd = conn.CreateCommand();
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
    /// The issue-#73 resolved-field columns added to the <c>messages</c> table on existing databases
    /// via guarded ALTER, plus the issue-#146 <c>linked_global_appointment_id</c> message-to-event
    /// linkage column. Each entry is a column name plus its column definition.
    /// </summary>
    private static readonly (string Name, string Definition)[] MessageFieldColumns =
    [
        ("sender_email_resolved", "sender_email_resolved TEXT NULL"),
        ("from_email_address", "from_email_address TEXT NULL"),
        ("conversation_id", "conversation_id TEXT NULL"),
        ("meeting_message_type", "meeting_message_type INTEGER NULL"),
        ("linked_global_appointment_id", "linked_global_appointment_id TEXT NULL"),
    ];

    /// <summary>
    /// Idempotent schema migration for the <c>messages</c> table. Adds the four issue-#73
    /// resolved-field columns when absent on an existing database. Running this twice is safe: each
    /// ALTER is guarded by a <c>PRAGMA table_info</c> check so no "duplicate column" error occurs.
    /// </summary>
    private static async Task MigrateMessagesSchemaAsync(SqliteConnection conn)
    {
        foreach (var (name, definition) in MessageFieldColumns)
        {
            if (!await MessagesColumnExistsAsync(conn, name))
            {
                await AddMessagesColumnAsync(conn, definition);
            }
        }
    }

    private static async Task AddMessagesColumnAsync(SqliteConnection conn, string columnDefinition)
    {
        var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE messages ADD COLUMN {columnDefinition};";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task<bool> MessagesColumnExistsAsync(SqliteConnection conn, string column)
    {
        var cmd = conn.CreateCommand();
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
}
