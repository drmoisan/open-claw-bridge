using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Row materialization and value-conversion helpers for <see cref="CacheRepository"/>.
/// Split from the main partial class to keep each file within the project's 500-line guideline
/// and to isolate pure reader/encoder helpers from persistence workflow code.
/// </summary>
internal sealed partial class CacheRepository
{
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
            GetBoolean(reader, "is_redacted"),
            SenderEmailResolved: GetString(reader, "sender_email_resolved"),
            FromEmailAddress: GetString(reader, "from_email_address"),
            ConversationId: GetString(reader, "conversation_id"),
            MeetingMessageType: GetNullableInt(reader, "meeting_message_type"),
            LinkedGlobalAppointmentId: GetString(reader, "linked_global_appointment_id")
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
            GetBoolean(reader, "is_redacted"),
            GetNullableInt(reader, "response_status"),
            Categories: GetCategories(reader, "categories_json"),
            IsOrganizer: GetBoolean(reader, "is_organizer"),
            IsOnlineMeeting: GetBoolean(reader, "is_online_meeting"),
            AllowNewTimeProposals: GetBoolean(reader, "allow_new_time_proposals"),
            ICalUId: GetString(reader, "ical_uid"),
            SeriesMasterId: GetString(reader, "series_master_id"),
            LastModifiedDateTime: GetDateTimeOffset(reader, "last_modified_utc"),
            BodyFull: GetString(reader, "body_full"),
            SensitivityLabel: GetString(reader, "sensitivity_label")
        );

    /// <summary>
    /// Deserializes the <c>categories_json</c> column to a string array. A NULL or unparseable
    /// value yields null, matching the optional <see cref="EventDto.Categories"/> default.
    /// </summary>
    private static string[]? GetCategories(SqliteDataReader reader, string name)
    {
        var json = GetString(reader, name);
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
