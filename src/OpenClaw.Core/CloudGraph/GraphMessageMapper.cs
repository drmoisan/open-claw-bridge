using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Pure static mapping from the internal Graph message wire record to
/// <see cref="MessageDto"/> per the spec "Data &amp; State" MessageDto table. The enum
/// maps are the inverses of <c>SchedulingDtoMapper</c>'s string maps; recipient lists
/// serialize to the OR-5 JSON shape <c>[{"name":"...","email":"..."}]</c>. No I/O and
/// no mutation; a missing required <c>id</c> fails fast with
/// <see cref="GraphMappingException"/>.
/// </summary>
internal static class GraphMessageMapper
{
    /// <summary>The <c>@odata.type</c> discriminator for meeting-related messages (D10).</summary>
    internal const string EventMessageODataType = "#microsoft.graph.eventMessage";

    private static readonly JsonSerializerOptions Or5JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>OR-5 attendee JSON element: <c>{"name":"...","email":"..."}</c>.</summary>
    internal sealed record Or5Recipient(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email
    );

    /// <summary>
    /// Maps a deserialized Graph message to the wire <see cref="MessageDto"/>.
    /// </summary>
    /// <param name="message">The Graph wire record. Must not be null.</param>
    /// <exception cref="GraphMappingException">The required <c>id</c> is missing.</exception>
    public static MessageDto Map(GraphMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.Id))
        {
            throw new GraphMappingException(
                "The Graph message is missing the required field 'id'."
            );
        }

        var isMeeting = string.Equals(
            message.ODataType,
            EventMessageODataType,
            StringComparison.Ordinal
        );

        return new MessageDto(
            BridgeId: message.Id,
            ItemKind: isMeeting ? "meeting" : "mail",
            Subject: message.Subject,
            ReceivedUtc: message.ReceivedDateTime,
            SentUtc: message.SentDateTime,
            Importance: MapImportance(message.Importance),
            Sensitivity: MapSensitivity(message.Sensitivity),
            // An absent isRead is treated as not-yet-read: unknown read state must not
            // silently drop a message from unread-driven triage.
            Unread: !(message.IsRead ?? false),
            HasAttachments: message.HasAttachments ?? false,
            MessageClass: null,
            SenderName: message.Sender?.EmailAddress?.Name,
            SenderEmail: message.Sender?.EmailAddress?.Address,
            ToJson: ToOr5Json(message.ToRecipients),
            CcJson: ToOr5Json(message.CcRecipients),
            BodyPreview: message.BodyPreview,
            // App-only Graph reads full fields; there is no COM redaction path.
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            SenderEmailResolved: message.Sender?.EmailAddress?.Address,
            FromEmailAddress: message.From?.EmailAddress?.Address,
            ConversationId: message.ConversationId,
            MeetingMessageType: MapMeetingMessageType(message.MeetingMessageType)
        );
    }

    /// <summary>Inverse of <c>SchedulingDtoMapper.MapImportance</c>; unknown maps to null.</summary>
    internal static int? MapImportance(string? importance) =>
        importance switch
        {
            "low" => 0,
            "normal" => 1,
            "high" => 2,
            _ => null,
        };

    /// <summary>Inverse of <c>SchedulingDtoMapper.MapSensitivity</c>; unknown maps to null.</summary>
    internal static int? MapSensitivity(string? sensitivity) =>
        sensitivity switch
        {
            "normal" => 0,
            "personal" => 1,
            "private" => 2,
            "confidential" => 3,
            _ => null,
        };

    /// <summary>
    /// Inverse of <c>SchedulingDtoMapper.MapMeetingMessageType</c>; <c>none</c>,
    /// absent, and unknown values map to null (ordinary mail).
    /// </summary>
    internal static int? MapMeetingMessageType(string? meetingMessageType) =>
        meetingMessageType switch
        {
            "meetingRequest" => 0,
            "meetingCancelled" => 1,
            "meetingDeclined" => 2,
            "meetingAccepted" => 3,
            "meetingTentativelyAccepted" => 4,
            _ => null,
        };

    /// <summary>
    /// Serializes Graph recipients to the OR-5 JSON shape consumed by
    /// <c>SchedulingDtoMapper.ParseAttendees</c>; an absent or empty list maps to null
    /// deterministically.
    /// </summary>
    internal static string? ToOr5Json(IReadOnlyList<GraphRecipient>? recipients)
    {
        if (recipients is null || recipients.Count == 0)
        {
            return null;
        }

        var mapped = new List<Or5Recipient>(recipients.Count);
        foreach (var recipient in recipients)
        {
            mapped.Add(
                new Or5Recipient(
                    recipient.EmailAddress?.Name ?? string.Empty,
                    recipient.EmailAddress?.Address ?? string.Empty
                )
            );
        }

        return SerializeOr5(mapped);
    }

    /// <summary>Serializes prepared OR-5 elements (shared with the event mapper).</summary>
    internal static string? SerializeOr5(IReadOnlyList<Or5Recipient> recipients) =>
        recipients.Count == 0 ? null : JsonSerializer.Serialize(recipients, Or5JsonOptions);
}
