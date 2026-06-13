using System.Text.Json;
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

    public async Task UpsertMessagesAsync(
        IReadOnlyList<MessageDto> messages,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        foreach (var message in messages)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                @"
INSERT INTO messages(
    bridge_id, item_kind, subject, received_utc, sent_utc, importance, sensitivity, unread,
    has_attachments, message_class, sender_name, sender_email, to_json, cc_json, body_preview,
    protected_fields_available, is_redacted, bridge_mode, cache_stale, stale_reason,
    adapter_request_id, observed_at_utc,
    sender_email_resolved, from_email_address, conversation_id, meeting_message_type)
VALUES(
    $bridge_id, $item_kind, $subject, $received_utc, $sent_utc, $importance, $sensitivity, $unread,
    $has_attachments, $message_class, $sender_name, $sender_email, $to_json, $cc_json, $body_preview,
    $protected_fields_available, $is_redacted, $bridge_mode, $cache_stale, $stale_reason,
    $adapter_request_id, $observed_at_utc,
    $sender_email_resolved, $from_email_address, $conversation_id, $meeting_message_type)
ON CONFLICT(bridge_id) DO UPDATE SET
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
    bridge_mode = excluded.bridge_mode,
    cache_stale = excluded.cache_stale,
    stale_reason = excluded.stale_reason,
    adapter_request_id = excluded.adapter_request_id,
    observed_at_utc = excluded.observed_at_utc,
    sender_email_resolved = excluded.sender_email_resolved,
    from_email_address = excluded.from_email_address,
    conversation_id = excluded.conversation_id,
    meeting_message_type = excluded.meeting_message_type;";
            AddMessageParameters(command, message, bridgeStatus, requestId, observedAtUtc);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task UpsertEventsAsync(
        IReadOnlyList<EventDto> events,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        foreach (var evt in events)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                @"
INSERT INTO events(
    bridge_id, global_appointment_id, subject, start_utc, end_utc, location, busy_status,
    meeting_status, is_recurring, sensitivity, organizer, required_attendees_json,
    optional_attendees_json, resources_json, body_preview, protected_fields_available,
    is_redacted, bridge_mode, cache_stale, stale_reason, adapter_request_id, observed_at_utc,
    last_modified_utc, categories_json, is_organizer, is_online_meeting, allow_new_time_proposals,
    ical_uid, series_master_id, body_full, sensitivity_label)
VALUES(
    $bridge_id, $global_appointment_id, $subject, $start_utc, $end_utc, $location, $busy_status,
    $meeting_status, $is_recurring, $sensitivity, $organizer, $required_attendees_json,
    $optional_attendees_json, $resources_json, $body_preview, $protected_fields_available,
    $is_redacted, $bridge_mode, $cache_stale, $stale_reason, $adapter_request_id, $observed_at_utc,
    $last_modified_utc, $categories_json, $is_organizer, $is_online_meeting, $allow_new_time_proposals,
    $ical_uid, $series_master_id, $body_full, $sensitivity_label)
ON CONFLICT(bridge_id) DO UPDATE SET
    global_appointment_id = excluded.global_appointment_id,
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
    bridge_mode = excluded.bridge_mode,
    cache_stale = excluded.cache_stale,
    stale_reason = excluded.stale_reason,
    adapter_request_id = excluded.adapter_request_id,
    observed_at_utc = excluded.observed_at_utc,
    last_modified_utc = excluded.last_modified_utc,
    categories_json = excluded.categories_json,
    is_organizer = excluded.is_organizer,
    is_online_meeting = excluded.is_online_meeting,
    allow_new_time_proposals = excluded.allow_new_time_proposals,
    ical_uid = excluded.ical_uid,
    series_master_id = excluded.series_master_id,
    body_full = excluded.body_full,
    sensitivity_label = excluded.sensitivity_label;";
            AddEventParameters(command, evt, bridgeStatus, requestId, observedAtUtc);
            await command.ExecuteNonQueryAsync();
        }
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

    public async Task<IReadOnlyList<MessageDto>> ListMessagesAsync(
        string kind,
        DateTimeOffset? sinceUtc,
        int limit
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
SELECT *
FROM messages
WHERE ($kind = 'all'
    OR ($kind = 'meeting' AND item_kind = 'meeting')
    OR ($kind = 'mail' AND item_kind <> 'meeting'))
  AND ($since_utc IS NULL OR received_utc IS NULL OR received_utc >= $since_utc)
ORDER BY COALESCE(received_utc, sent_utc, observed_at_utc) DESC, bridge_id ASC
LIMIT $limit;";
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue(
            "$since_utc",
            sinceUtc is null ? DBNull.Value : sinceUtc.Value.UtcDateTime.ToString("O")
        );
        command.Parameters.AddWithValue("$limit", limit);
        return await ReadMessagesAsync(command);
    }

    public async Task<MessageDto?> GetMessageAsync(string bridgeId)
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM messages WHERE bridge_id = $bridge_id LIMIT 1;";
        command.Parameters.AddWithValue("$bridge_id", bridgeId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadMessage(reader) : null;
    }

    public async Task<IReadOnlyList<EventDto>> ListEventsAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
SELECT *
FROM events
WHERE start_utc >= $start_utc
  AND start_utc < $end_utc
ORDER BY start_utc ASC, bridge_id ASC
LIMIT $limit;";
        command.Parameters.AddWithValue("$start_utc", startUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$end_utc", endUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$limit", limit);
        return await ReadEventsAsync(command);
    }

    public async Task<EventDto?> GetEventAsync(string bridgeId)
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM events WHERE bridge_id = $bridge_id LIMIT 1;";
        command.Parameters.AddWithValue("$bridge_id", bridgeId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadEvent(reader) : null;
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

    private static void AddMessageParameters(
        SqliteCommand command,
        MessageDto message,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        command.Parameters.AddWithValue("$bridge_id", message.BridgeId);
        command.Parameters.AddWithValue("$item_kind", message.ItemKind);
        command.Parameters.AddWithValue("$subject", (object?)message.Subject ?? DBNull.Value);
        command.Parameters.AddWithValue("$received_utc", ToDbValue(message.ReceivedUtc));
        command.Parameters.AddWithValue("$sent_utc", ToDbValue(message.SentUtc));
        command.Parameters.AddWithValue("$importance", ToDbValue(message.Importance));
        command.Parameters.AddWithValue("$sensitivity", ToDbValue(message.Sensitivity));
        command.Parameters.AddWithValue("$unread", message.Unread ? 1 : 0);
        command.Parameters.AddWithValue("$has_attachments", message.HasAttachments ? 1 : 0);
        command.Parameters.AddWithValue(
            "$message_class",
            (object?)message.MessageClass ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$sender_name",
            (object?)message.SenderName ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$sender_email",
            (object?)message.SenderEmail ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$to_json", (object?)message.ToJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$cc_json", (object?)message.CcJson ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$body_preview",
            (object?)message.BodyPreview ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$protected_fields_available",
            message.ProtectedFieldsAvailable ? 1 : 0
        );
        command.Parameters.AddWithValue("$is_redacted", message.IsRedacted ? 1 : 0);
        command.Parameters.AddWithValue("$bridge_mode", bridgeStatus.Mode);
        command.Parameters.AddWithValue("$cache_stale", bridgeStatus.CacheStale ? 1 : 0);
        command.Parameters.AddWithValue(
            "$stale_reason",
            (object?)bridgeStatus.StaleReason ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$adapter_request_id", requestId);
        command.Parameters.AddWithValue(
            "$observed_at_utc",
            observedAtUtc.UtcDateTime.ToString("O")
        );
        AddMessageResolvedFieldParameters(command, message);
    }

    private static void AddEventParameters(
        SqliteCommand command,
        EventDto evt,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        command.Parameters.AddWithValue("$bridge_id", evt.BridgeId);
        command.Parameters.AddWithValue(
            "$global_appointment_id",
            (object?)evt.GlobalAppointmentId ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$subject", (object?)evt.Subject ?? DBNull.Value);
        command.Parameters.AddWithValue("$start_utc", evt.StartUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$end_utc", evt.EndUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$location", (object?)evt.Location ?? DBNull.Value);
        command.Parameters.AddWithValue("$busy_status", ToDbValue(evt.BusyStatus));
        command.Parameters.AddWithValue("$meeting_status", ToDbValue(evt.MeetingStatus));
        command.Parameters.AddWithValue("$is_recurring", evt.IsRecurring ? 1 : 0);
        command.Parameters.AddWithValue("$sensitivity", ToDbValue(evt.Sensitivity));
        command.Parameters.AddWithValue("$organizer", (object?)evt.Organizer ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$required_attendees_json",
            (object?)evt.RequiredAttendeesJson ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$optional_attendees_json",
            (object?)evt.OptionalAttendeesJson ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$resources_json",
            (object?)evt.ResourcesJson ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$body_preview", (object?)evt.BodyPreview ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$protected_fields_available",
            evt.ProtectedFieldsAvailable ? 1 : 0
        );
        command.Parameters.AddWithValue("$is_redacted", evt.IsRedacted ? 1 : 0);
        command.Parameters.AddWithValue("$bridge_mode", bridgeStatus.Mode);
        command.Parameters.AddWithValue("$cache_stale", bridgeStatus.CacheStale ? 1 : 0);
        command.Parameters.AddWithValue(
            "$stale_reason",
            (object?)bridgeStatus.StaleReason ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$adapter_request_id", requestId);
        command.Parameters.AddWithValue(
            "$observed_at_utc",
            observedAtUtc.UtcDateTime.ToString("O")
        );
        command.Parameters.AddWithValue("$last_modified_utc", ToDbValue(evt.LastModifiedDateTime));
        command.Parameters.AddWithValue("$categories_json", CategoriesToDbValue(evt.Categories));
        command.Parameters.AddWithValue("$is_organizer", evt.IsOrganizer ? 1 : 0);
        command.Parameters.AddWithValue("$is_online_meeting", evt.IsOnlineMeeting ? 1 : 0);
        command.Parameters.AddWithValue(
            "$allow_new_time_proposals",
            evt.AllowNewTimeProposals ? 1 : 0
        );
        command.Parameters.AddWithValue("$ical_uid", (object?)evt.ICalUId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$series_master_id",
            (object?)evt.SeriesMasterId ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$body_full", (object?)evt.BodyFull ?? DBNull.Value);
        command.Parameters.AddWithValue(
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

    /// <summary>
    /// Deserializes the <c>categories_json</c> column to a string array. A NULL or unparseable
    /// value yields null, matching the optional <see cref="EventDto.Categories"/> default.
    /// </summary>
    private static string[]? ReadCategories(SqliteDataReader reader, string name)
    {
        var json = ReadString(reader, name);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string sql)
    {
        await using var command = new SqliteCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<IReadOnlyList<MessageDto>> ReadMessagesAsync(SqliteCommand command)
    {
        var rows = new List<MessageDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadMessage(reader));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<EventDto>> ReadEventsAsync(SqliteCommand command)
    {
        var rows = new List<EventDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadEvent(reader));
        }

        return rows;
    }

    private static MessageDto ReadMessage(SqliteDataReader reader) =>
        new(
            ReadString(reader, "bridge_id")!,
            ReadString(reader, "item_kind")!,
            ReadString(reader, "subject"),
            ReadDateTimeOffset(reader, "received_utc"),
            ReadDateTimeOffset(reader, "sent_utc"),
            ReadNullableInt(reader, "importance"),
            ReadNullableInt(reader, "sensitivity"),
            ReadBoolean(reader, "unread"),
            ReadBoolean(reader, "has_attachments"),
            ReadString(reader, "message_class"),
            ReadString(reader, "sender_name"),
            ReadString(reader, "sender_email"),
            ReadString(reader, "to_json"),
            ReadString(reader, "cc_json"),
            ReadString(reader, "body_preview"),
            ReadBoolean(reader, "protected_fields_available"),
            ReadBoolean(reader, "is_redacted"),
            SenderEmailResolved: ReadString(reader, "sender_email_resolved"),
            FromEmailAddress: ReadString(reader, "from_email_address"),
            ConversationId: ReadString(reader, "conversation_id"),
            MeetingMessageType: ReadNullableInt(reader, "meeting_message_type")
        );

    private static EventDto ReadEvent(SqliteDataReader reader) =>
        new(
            ReadString(reader, "bridge_id")!,
            ReadString(reader, "global_appointment_id"),
            ReadString(reader, "subject"),
            ReadDateTimeOffset(reader, "start_utc") ?? DateTimeOffset.MinValue,
            ReadDateTimeOffset(reader, "end_utc") ?? DateTimeOffset.MinValue,
            ReadString(reader, "location"),
            ReadNullableInt(reader, "busy_status"),
            ReadNullableInt(reader, "meeting_status"),
            ReadBoolean(reader, "is_recurring"),
            ReadNullableInt(reader, "sensitivity"),
            ReadString(reader, "organizer"),
            ReadString(reader, "required_attendees_json"),
            ReadString(reader, "optional_attendees_json"),
            ReadString(reader, "resources_json"),
            ReadString(reader, "body_preview"),
            ReadBoolean(reader, "protected_fields_available"),
            ReadBoolean(reader, "is_redacted"),
            ResponseStatus: null,
            Categories: ReadCategories(reader, "categories_json"),
            IsOrganizer: ReadBoolean(reader, "is_organizer"),
            IsOnlineMeeting: ReadBoolean(reader, "is_online_meeting"),
            AllowNewTimeProposals: ReadBoolean(reader, "allow_new_time_proposals"),
            ICalUId: ReadString(reader, "ical_uid"),
            SeriesMasterId: ReadString(reader, "series_master_id"),
            LastModifiedDateTime: ReadDateTimeOffset(reader, "last_modified_utc"),
            BodyFull: ReadString(reader, "body_full"),
            SensitivityLabel: ReadString(reader, "sensitivity_label")
        );

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
