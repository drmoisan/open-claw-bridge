using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core;

/// <summary>
/// Event-persistence members for <see cref="CoreCacheRepository"/>. Split into this partial-class
/// file (issue #73 RF-2) so the previously over-cap <c>CoreCacheRepository.cs</c> falls under the
/// 500-line limit, following the partial-split convention established by
/// <c>CoreCacheRepository.Schema.cs</c> and <c>CoreCacheRepository.Messages.cs</c>. The move is
/// behavior-preserving: same partial class, same member signatures and bodies. The shared reader
/// helpers (<c>ReadString</c>, <c>ReadDateTimeOffset</c>, <c>ReadNullableInt</c>,
/// <c>ReadBoolean</c>, <c>ToDbValue</c>, <c>CountAsync</c>) and <c>GetCountsAsync</c> remain in
/// the base file and are reached via the shared partial class.
/// </summary>
internal sealed partial class CoreCacheRepository
{
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
    ical_uid, series_master_id, body_full, sensitivity_label, response_status)
VALUES(
    $bridge_id, $global_appointment_id, $subject, $start_utc, $end_utc, $location, $busy_status,
    $meeting_status, $is_recurring, $sensitivity, $organizer, $required_attendees_json,
    $optional_attendees_json, $resources_json, $body_preview, $protected_fields_available,
    $is_redacted, $bridge_mode, $cache_stale, $stale_reason, $adapter_request_id, $observed_at_utc,
    $last_modified_utc, $categories_json, $is_organizer, $is_online_meeting, $allow_new_time_proposals,
    $ical_uid, $series_master_id, $body_full, $sensitivity_label, $response_status)
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
    sensitivity_label = excluded.sensitivity_label,
    response_status = excluded.response_status;";
            AddEventParameters(command, evt, bridgeStatus, requestId, observedAtUtc);
            await command.ExecuteNonQueryAsync();
        }
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
        command.Parameters.AddWithValue("$response_status", ToDbValue(evt.ResponseStatus));
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
            ResponseStatus: ReadNullableInt(reader, "response_status"),
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
}
