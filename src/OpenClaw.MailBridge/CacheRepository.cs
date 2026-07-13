using System.Text.Json;
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

    /// <summary>Resolves the calendar event linked to a message (issue #146); see the implementation
    /// in <c>CacheRepository.EventForMessage.cs</c> for the full contract and null semantics.</summary>
    Task<EventDto?> GetEventForMessageAsync(
        string messageBridgeId,
        CancellationToken cancellationToken = default
    );

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
internal sealed partial class CacheRepository : IBridgeRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _anchor;

    public CacheRepository()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "MailBridge",
            "cache.db"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Creates a repository with a custom connection string.
    /// For in-memory databases, an anchor connection is kept open so the database survives
    /// across individual Open/Close cycles.
    /// </summary>
    internal CacheRepository(string connectionString)
    {
        _connectionString = connectionString;
        if (connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            _anchor = new SqliteConnection(connectionString);
            _anchor.Open();
        }
    }

    public void Dispose()
    {
        _anchor?.Dispose();
    }

    private SqliteConnection Open() => new SqliteConnection(_connectionString);

    public async Task InitializeAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();

        await new SqliteCommand(CreateTablesSql, conn).ExecuteNonQueryAsync();

        await MigrateEventsSchemaAsync(conn);
        await MigrateMessagesSchemaAsync(conn);
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
    cc_json, body_preview, protected_fields_available, is_redacted, last_seen_utc,
    sender_email_resolved, from_email_address, conversation_id, meeting_message_type,
    linked_global_appointment_id
) VALUES(
    $bridge_id, $entry_id, $store_id, $item_kind, $subject, $received_utc, $sent_utc, $importance,
    $sensitivity, $unread, $has_attachments, $message_class, $sender_name, $sender_email, $to_json,
    $cc_json, $body_preview, $protected_fields_available, $is_redacted, $last_seen_utc,
    $sender_email_resolved, $from_email_address, $conversation_id, $meeting_message_type,
    $linked_global_appointment_id
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
    last_seen_utc = excluded.last_seen_utc,
    sender_email_resolved = excluded.sender_email_resolved,
    from_email_address = excluded.from_email_address,
    conversation_id = excluded.conversation_id,
    meeting_message_type = excluded.meeting_message_type,
    linked_global_appointment_id = excluded.linked_global_appointment_id;";

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
    protected_fields_available, is_redacted, last_modified_utc, last_seen_utc, response_status,
    categories_json, is_organizer, is_online_meeting, allow_new_time_proposals, ical_uid,
    series_master_id, body_full, sensitivity_label
) VALUES(
    $bridge_id, $entry_id, $store_id, $global_appointment_id, $item_kind, $subject, $start_utc, $end_utc,
    $location, $busy_status, $meeting_status, $is_recurring, $sensitivity, $organizer,
    $required_attendees_json, $optional_attendees_json, $resources_json, $body_preview,
    $protected_fields_available, $is_redacted, $last_modified_utc, $last_seen_utc, $response_status,
    $categories_json, $is_organizer, $is_online_meeting, $allow_new_time_proposals, $ical_uid,
    $series_master_id, $body_full, $sensitivity_label
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
    last_seen_utc = excluded.last_seen_utc,
    response_status = excluded.response_status,
    categories_json = excluded.categories_json,
    is_organizer = excluded.is_organizer,
    is_online_meeting = excluded.is_online_meeting,
    allow_new_time_proposals = excluded.allow_new_time_proposals,
    ical_uid = excluded.ical_uid,
    series_master_id = excluded.series_master_id,
    body_full = excluded.body_full,
    sensitivity_label = excluded.sensitivity_label;";

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
        cmd.Parameters.AddWithValue(
            "$sender_email_resolved",
            (object?)message.SenderEmailResolved ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue(
            "$from_email_address",
            (object?)message.FromEmailAddress ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue(
            "$conversation_id",
            (object?)message.ConversationId ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue("$meeting_message_type", ToDbValue(message.MeetingMessageType));
        cmd.Parameters.AddWithValue(
            "$linked_global_appointment_id",
            (object?)message.LinkedGlobalAppointmentId ?? DBNull.Value
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
        cmd.Parameters.AddWithValue("$last_modified_utc", ToDbValue(evt.LastModifiedDateTime));
        cmd.Parameters.AddWithValue(
            "$last_seen_utc",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        );
        cmd.Parameters.AddWithValue("$response_status", ToDbValue(evt.ResponseStatus));
        cmd.Parameters.AddWithValue("$categories_json", CategoriesToDbValue(evt.Categories));
        cmd.Parameters.AddWithValue("$is_organizer", evt.IsOrganizer ? 1 : 0);
        cmd.Parameters.AddWithValue("$is_online_meeting", evt.IsOnlineMeeting ? 1 : 0);
        cmd.Parameters.AddWithValue("$allow_new_time_proposals", evt.AllowNewTimeProposals ? 1 : 0);
        cmd.Parameters.AddWithValue("$ical_uid", (object?)evt.ICalUId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$series_master_id",
            (object?)evt.SeriesMasterId ?? DBNull.Value
        );
        cmd.Parameters.AddWithValue("$body_full", (object?)evt.BodyFull ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "$sensitivity_label",
            (object?)evt.SensitivityLabel ?? DBNull.Value
        );
    }

    /// <summary>
    /// Serializes the optional <c>Categories</c> array to a JSON array column value, consistent
    /// with the existing attendee/resource JSON columns. A null array stores SQL NULL.
    /// </summary>
    private static object CategoriesToDbValue(string[]? categories) =>
        categories is null ? DBNull.Value : JsonSerializer.Serialize(categories);
}
