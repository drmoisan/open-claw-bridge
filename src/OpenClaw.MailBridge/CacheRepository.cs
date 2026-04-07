using Microsoft.Data.Sqlite;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

internal interface IScanStateRepository
{
    Task InitializeAsync();
    Task TouchScanStateAsync(string key, DateTimeOffset value);
    Task<DateTimeOffset?> GetScanStateAsync(string key);
}

internal interface IBridgeRepository : IScanStateRepository
{
    Task UpsertMessageAsync(string entryId, string? storeId, MessageDto message);
    Task<IReadOnlyList<MessageDto>> ListRecentMessagesAsync(DateTimeOffset sinceUtc, int limit);
    Task<IReadOnlyList<MessageDto>> ListRecentMeetingRequestsAsync(
        DateTimeOffset sinceUtc,
        int limit
    );
    Task<MessageDto?> GetMessageAsync(string bridgeId);
    Task UpsertEventAsync(
        string entryId,
        string? storeId,
        string? globalAppointmentId,
        EventDto evt
    );
    Task<IReadOnlyList<EventDto>> ListCalendarWindowAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit
    );
    Task<EventDto?> GetEventAsync(string bridgeId);
    Task<ScanStateSnapshot> GetScanStateSnapshotAsync();
}

internal sealed record ScanStateSnapshot(
    DateTimeOffset? LastInboxScanUtc,
    DateTimeOffset? LastCalendarScanUtc,
    DateTimeOffset? LastSuccessfulScanUtc
);

/// <summary>
/// Persists cached bridge metadata in a local SQLite database under the user's profile.
/// </summary>
internal sealed class CacheRepository : IBridgeRepository
{
    private readonly string _dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenClaw",
        "MailBridge",
        "cache.db"
    );

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    public async Task InitializeAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var sql =
            @"
CREATE TABLE IF NOT EXISTS messages(bridge_id TEXT PRIMARY KEY,entry_id TEXT NOT NULL,store_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,received_utc TEXT NULL,sent_utc TEXT NULL,importance INTEGER NULL,sensitivity INTEGER NULL,unread INTEGER NOT NULL,has_attachments INTEGER NOT NULL,message_class TEXT NULL,sender_name TEXT NULL,sender_email TEXT NULL,to_json TEXT NULL,cc_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_seen_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS events(bridge_id TEXT PRIMARY KEY,entry_id TEXT NULL,store_id TEXT NULL,global_appointment_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,start_utc TEXT NOT NULL,end_utc TEXT NOT NULL,location TEXT NULL,busy_status INTEGER NULL,meeting_status INTEGER NULL,is_recurring INTEGER NOT NULL,sensitivity INTEGER NULL,organizer TEXT NULL,required_attendees_json TEXT NULL,optional_attendees_json TEXT NULL,resources_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_modified_utc TEXT NULL,last_seen_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS scan_state(key TEXT PRIMARY KEY,value TEXT NOT NULL);";
        await new SqliteCommand(sql, conn).ExecuteNonQueryAsync();
    }

    public async Task TouchScanStateAsync(string key, DateTimeOffset value)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO scan_state(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value.UtcDateTime.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DateTimeOffset?> GetScanStateAsync(string key)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM scan_state WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);

        var val = (string?)await cmd.ExecuteScalarAsync();
        return DateTimeOffset.TryParse(val, out var parsed) ? parsed : null;
    }

    public async Task UpsertMessageAsync(string entryId, string? storeId, MessageDto message)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
INSERT INTO messages(
    bridge_id, entry_id, store_id, item_kind, subject, received_utc, sent_utc, importance,
    sensitivity, unread, has_attachments, message_class, sender_name, sender_email, to_json,
    cc_json, body_preview, protected_fields_available, is_redacted, last_seen_utc
) VALUES(
    $bridge_id, $entry_id, $store_id, $item_kind, $subject, $received_utc, $sent_utc, $importance,
    $sensitivity, $unread, $has_attachments, $message_class, $sender_name, $sender_email, $to_json,
    $cc_json, $body_preview, $protected_fields_available, $is_redacted, $last_seen_utc
)
ON CONFLICT(bridge_id) DO UPDATE SET
    entry_id = excluded.entry_id,
    store_id = excluded.store_id,
    item_kind = excluded.item_kind,
    subject = excluded.subject,
    received_utc = excluded.received_utc,
    sent_utc = excluded.sent_utc,
    importance = excluded.importance,
    sensitivity = excluded.sensitivity,
    unread = excluded.unread,
    has_attachments = excluded.has_attachments,
    message_class = excluded.message_class,
    sender_name = excluded.sender_name,
    sender_email = excluded.sender_email,
    to_json = excluded.to_json,
    cc_json = excluded.cc_json,
    body_preview = excluded.body_preview,
    protected_fields_available = excluded.protected_fields_available,
    is_redacted = excluded.is_redacted,
    last_seen_utc = excluded.last_seen_utc;";

        AddMessageParameters(cmd, entryId, storeId, message);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<MessageDto>> ListRecentMessagesAsync(
        DateTimeOffset sinceUtc,
        int limit
    )
    {
        return await ListMessagesAsync(
            @"
SELECT *
FROM messages
WHERE received_utc IS NOT NULL
  AND received_utc >= $since_utc
ORDER BY received_utc DESC, bridge_id ASC
LIMIT $limit;",
            sinceUtc,
            limit
        );
    }

    public async Task<IReadOnlyList<MessageDto>> ListRecentMeetingRequestsAsync(
        DateTimeOffset sinceUtc,
        int limit
    )
    {
        return await ListMessagesAsync(
            @"
SELECT *
FROM messages
WHERE received_utc IS NOT NULL
  AND received_utc >= $since_utc
  AND item_kind = 'meeting'
ORDER BY received_utc DESC, bridge_id ASC
LIMIT $limit;",
            sinceUtc,
            limit
        );
    }

    public async Task<MessageDto?> GetMessageAsync(string bridgeId)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM messages WHERE bridge_id = $bridge_id LIMIT 1;";
        cmd.Parameters.AddWithValue("$bridge_id", bridgeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadMessage(reader) : null;
    }

    public async Task UpsertEventAsync(
        string entryId,
        string? storeId,
        string? globalAppointmentId,
        EventDto evt
    )
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
INSERT INTO events(
    bridge_id, entry_id, store_id, global_appointment_id, item_kind, subject, start_utc, end_utc,
    location, busy_status, meeting_status, is_recurring, sensitivity, organizer,
    required_attendees_json, optional_attendees_json, resources_json, body_preview,
    protected_fields_available, is_redacted, last_modified_utc, last_seen_utc
) VALUES(
    $bridge_id, $entry_id, $store_id, $global_appointment_id, $item_kind, $subject, $start_utc, $end_utc,
    $location, $busy_status, $meeting_status, $is_recurring, $sensitivity, $organizer,
    $required_attendees_json, $optional_attendees_json, $resources_json, $body_preview,
    $protected_fields_available, $is_redacted, $last_modified_utc, $last_seen_utc
)
ON CONFLICT(bridge_id) DO UPDATE SET
    entry_id = excluded.entry_id,
    store_id = excluded.store_id,
    global_appointment_id = excluded.global_appointment_id,
    item_kind = excluded.item_kind,
    subject = excluded.subject,
    start_utc = excluded.start_utc,
    end_utc = excluded.end_utc,
    location = excluded.location,
    busy_status = excluded.busy_status,
    meeting_status = excluded.meeting_status,
    is_recurring = excluded.is_recurring,
    sensitivity = excluded.sensitivity,
    organizer = excluded.organizer,
    required_attendees_json = excluded.required_attendees_json,
    optional_attendees_json = excluded.optional_attendees_json,
    resources_json = excluded.resources_json,
    body_preview = excluded.body_preview,
    protected_fields_available = excluded.protected_fields_available,
    is_redacted = excluded.is_redacted,
    last_modified_utc = excluded.last_modified_utc,
    last_seen_utc = excluded.last_seen_utc;";

        AddEventParameters(cmd, entryId, storeId, globalAppointmentId, evt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<EventDto>> ListCalendarWindowAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit
    )
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
SELECT *
FROM events
WHERE start_utc >= $start_utc
  AND start_utc < $end_utc
ORDER BY start_utc ASC, bridge_id ASC
LIMIT $limit;";
        cmd.Parameters.AddWithValue("$start_utc", startUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$end_utc", endUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$limit", limit);

        var rows = new List<EventDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadEvent(reader));
        }

        return rows;
    }

    public async Task<EventDto?> GetEventAsync(string bridgeId)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM events WHERE bridge_id = $bridge_id LIMIT 1;";
        cmd.Parameters.AddWithValue("$bridge_id", bridgeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadEvent(reader) : null;
    }

    public async Task<ScanStateSnapshot> GetScanStateSnapshotAsync()
    {
        var lastInboxScanUtc = await GetScanStateAsync("last_inbox_scan_utc");
        var lastCalendarScanUtc = await GetScanStateAsync("last_calendar_scan_utc");
        var lastSuccessfulScanUtc = await GetScanStateAsync("last_successful_scan_utc");
        return new ScanStateSnapshot(lastInboxScanUtc, lastCalendarScanUtc, lastSuccessfulScanUtc);
    }

    private async Task<IReadOnlyList<MessageDto>> ListMessagesAsync(
        string sql,
        DateTimeOffset sinceUtc,
        int limit
    )
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$since_utc", sinceUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$limit", limit);

        var rows = new List<MessageDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadMessage(reader));
        }

        return rows;
    }

    private static void AddMessageParameters(
        SqliteCommand cmd,
        string entryId,
        string? storeId,
        MessageDto message
    )
    {
        cmd.Parameters.AddWithValue("$bridge_id", message.BridgeId);
        cmd.Parameters.AddWithValue("$entry_id", entryId);
        cmd.Parameters.AddWithValue("$store_id", (object?)storeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$item_kind", message.ItemKind);
        cmd.Parameters.AddWithValue("$subject", (object?)message.Subject ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$received_utc", ToDbValue(message.ReceivedUtc));
        cmd.Parameters.AddWithValue("$sent_utc", ToDbValue(message.SentUtc));
        cmd.Parameters.AddWithValue("$importance", ToDbValue(message.Importance));
        cmd.Parameters.AddWithValue("$sensitivity", ToDbValue(message.Sensitivity));
        cmd.Parameters.AddWithValue("$unread", message.Unread ? 1 : 0);
        cmd.Parameters.AddWithValue("$has_attachments", message.HasAttachments ? 1 : 0);
        cmd.Parameters.AddWithValue(
            "$message_class",
            (object?)message.MessageClass ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue("$sender_name", (object?)message.SenderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sender_email", (object?)message.SenderEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$to_json", (object?)message.ToJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cc_json", (object?)message.CcJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$body_preview", (object?)message.BodyPreview ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$protected_fields_available",
            message.ProtectedFieldsAvailable ? 1 : 0
        );
        cmd.Parameters.AddWithValue("$is_redacted", message.IsRedacted ? 1 : 0);
        cmd.Parameters.AddWithValue(
            "$last_seen_utc",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        );
    }

    private static void AddEventParameters(
        SqliteCommand cmd,
        string entryId,
        string? storeId,
        string? globalAppointmentId,
        EventDto evt
    )
    {
        cmd.Parameters.AddWithValue("$bridge_id", evt.BridgeId);
        cmd.Parameters.AddWithValue("$entry_id", entryId);
        cmd.Parameters.AddWithValue("$store_id", (object?)storeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$global_appointment_id",
            (object?)globalAppointmentId ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue("$item_kind", "appointment");
        cmd.Parameters.AddWithValue("$subject", (object?)evt.Subject ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$start_utc", evt.StartUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$end_utc", evt.EndUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$location", (object?)evt.Location ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$busy_status", ToDbValue(evt.BusyStatus));
        cmd.Parameters.AddWithValue("$meeting_status", ToDbValue(evt.MeetingStatus));
        cmd.Parameters.AddWithValue("$is_recurring", evt.IsRecurring ? 1 : 0);
        cmd.Parameters.AddWithValue("$sensitivity", ToDbValue(evt.Sensitivity));
        cmd.Parameters.AddWithValue("$organizer", (object?)evt.Organizer ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$required_attendees_json",
            (object?)evt.RequiredAttendeesJson ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue(
            "$optional_attendees_json",
            (object?)evt.OptionalAttendeesJson ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue("$resources_json", (object?)evt.ResourcesJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$body_preview", (object?)evt.BodyPreview ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$protected_fields_available",
            evt.ProtectedFieldsAvailable ? 1 : 0
        );
        cmd.Parameters.AddWithValue("$is_redacted", evt.IsRedacted ? 1 : 0);
        cmd.Parameters.AddWithValue("$last_modified_utc", DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$last_seen_utc",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        );
    }

    private static MessageDto ReadMessage(SqliteDataReader reader) =>
        new(
            GetString(reader, "bridge_id")!,
            GetString(reader, "item_kind")!,
            GetString(reader, "subject"),
            GetDateTimeOffset(reader, "received_utc"),
            GetDateTimeOffset(reader, "sent_utc"),
            GetNullableInt(reader, "importance"),
            GetNullableInt(reader, "sensitivity"),
            GetBoolean(reader, "unread"),
            GetBoolean(reader, "has_attachments"),
            GetString(reader, "message_class"),
            GetString(reader, "sender_name"),
            GetString(reader, "sender_email"),
            GetString(reader, "to_json"),
            GetString(reader, "cc_json"),
            GetString(reader, "body_preview"),
            GetBoolean(reader, "protected_fields_available"),
            GetBoolean(reader, "is_redacted")
        );

    private static EventDto ReadEvent(SqliteDataReader reader) =>
        new(
            GetString(reader, "bridge_id")!,
            GetString(reader, "global_appointment_id"),
            GetString(reader, "subject"),
            GetDateTimeOffset(reader, "start_utc") ?? DateTimeOffset.MinValue,
            GetDateTimeOffset(reader, "end_utc") ?? DateTimeOffset.MinValue,
            GetString(reader, "location"),
            GetNullableInt(reader, "busy_status"),
            GetNullableInt(reader, "meeting_status"),
            GetBoolean(reader, "is_recurring"),
            GetNullableInt(reader, "sensitivity"),
            GetString(reader, "organizer"),
            GetString(reader, "required_attendees_json"),
            GetString(reader, "optional_attendees_json"),
            GetString(reader, "resources_json"),
            GetString(reader, "body_preview"),
            GetBoolean(reader, "protected_fields_available"),
            GetBoolean(reader, "is_redacted")
        );

    private static object ToDbValue(DateTimeOffset? value) =>
        value is null ? DBNull.Value : value.Value.UtcDateTime.ToString("O");

    private static object ToDbValue(int? value) => value is null ? DBNull.Value : value.Value;

    private static string? GetString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? GetDateTimeOffset(SqliteDataReader reader, string name)
    {
        var value = GetString(reader, name);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? GetNullableInt(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static bool GetBoolean(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) != 0;
    }
}
