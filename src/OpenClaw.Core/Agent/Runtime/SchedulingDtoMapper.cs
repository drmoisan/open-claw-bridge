using System.Text.Json;
using OpenClaw.Core.Agent;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// Translates bridge-cache DTOs (<see cref="MessageDto"/>, <see cref="EventDto"/>) into
/// the Graph-shaped agent DTOs (OR-4). This is part of the runtime seam (namespace
/// <c>OpenClaw.Core.Agent.Runtime</c>) and may reference
/// <c>OpenClaw.MailBridge.Contracts</c>, which <c>OpenClaw.Core</c> already references;
/// no new project reference is added.
/// </summary>
/// <remarks>
/// Several Graph fields are not yet available from the bridge cache and remain
/// null/empty until issues #71-#76 land: message conversation id, meeting-message type,
/// from/sender split, To/Cc recipients, body content type, event iCalUId,
/// seriesMasterId, categories, online-meeting and new-time-proposal flags, last-modified
/// time, and event type. The mapper maps these to null or empty deterministically.
/// </remarks>
public sealed class SchedulingDtoMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps a bridge <see cref="MessageDto"/> to a Graph-shaped
    /// <see cref="SchedulingMessageDto"/>. Absent Graph fields map to null/empty (#71-#76).
    /// </summary>
    /// <param name="message">The bridge message. Must not be null.</param>
    /// <returns>The Graph-shaped scheduling message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public SchedulingMessageDto MapMessage(MessageDto message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sender = BuildAttendee(message.SenderName, message.SenderEmail);

        return new SchedulingMessageDto(
            Id: message.BridgeId,
            Subject: message.Subject,
            BodyPreview: message.BodyPreview,
            // Full body content is not exposed by the bridge cache (#71-#76); only the
            // preview is available.
            BodyContent: null,
            BodyContentType: null,
            From: sender,
            Sender: sender,
            ToRecipients: ParseAttendees(message.ToJson),
            CcRecipients: ParseAttendees(message.CcJson),
            // Conversation id and meeting-message type are not yet available (#71-#76).
            ConversationId: null,
            ReceivedDateTime: message.ReceivedUtc ?? message.SentUtc,
            MeetingMessageType: message.ItemKind == "meeting" ? "meetingRequest" : null,
            Importance: MapImportance(message.Importance)
        );
    }

    /// <summary>
    /// Maps a bridge <see cref="EventDto"/> to a Graph-shaped
    /// <see cref="SchedulingEventDto"/>. Absent Graph fields map to null/empty (#71-#76).
    /// </summary>
    /// <param name="evt">The bridge event. Must not be null.</param>
    /// <returns>The Graph-shaped scheduling event.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evt"/> is null.</exception>
    public SchedulingEventDto MapEvent(EventDto evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new SchedulingEventDto(
            Id: evt.BridgeId,
            // iCalUId, seriesMasterId, categories, online-meeting and new-time-proposal
            // flags, last-modified time, and event type are not yet available (#71-#76).
            ICalUId: evt.GlobalAppointmentId,
            SeriesMasterId: null,
            Subject: evt.Subject,
            BodyPreview: evt.BodyPreview,
            BodyContent: null,
            BodyContentType: null,
            Organizer: BuildAttendee(null, evt.Organizer),
            RequiredAttendees: ParseAttendees(evt.RequiredAttendeesJson),
            OptionalAttendees: ParseAttendees(evt.OptionalAttendeesJson),
            ResourceAttendees: ParseAttendees(evt.ResourcesJson),
            Categories: Array.Empty<string>(),
            // The bridge cache does not yet expose the owner-organizer flag (#71-#76).
            IsOrganizer: false,
            IsOnlineMeeting: false,
            AllowNewTimeProposals: false,
            Sensitivity: MapSensitivity(evt.Sensitivity),
            Start: evt.StartUtc,
            StartTimeZone: "UTC",
            End: evt.EndUtc,
            EndTimeZone: "UTC",
            LastModifiedDateTime: null,
            // The series-master type is inferred from the recurrence flag where possible.
            Type: evt.IsRecurring ? "seriesMaster" : null
        );
    }

    private static AttendeeDto? BuildAttendee(string? name, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new AttendeeDto(name ?? string.Empty, email);
    }

    /// <summary>
    /// Parses an attendee JSON array of the form
    /// <c>[{"name":"...","email":"..."}]</c> (OR-5) into agent attendees. A null, empty,
    /// or malformed payload yields an empty list rather than throwing, because the bridge
    /// cache fields are optional until #71-#76 land.
    /// </summary>
    private static IReadOnlyList<AttendeeDto> ParseAttendees(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AttendeeDto>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<AttendeeJson>>(json, JsonOptions);
            if (parsed is null)
            {
                return Array.Empty<AttendeeDto>();
            }

            var result = new List<AttendeeDto>(parsed.Count);
            foreach (var item in parsed)
            {
                if (!string.IsNullOrWhiteSpace(item.Email))
                {
                    result.Add(new AttendeeDto(item.Name ?? string.Empty, item.Email));
                }
            }

            return result;
        }
        catch (JsonException)
        {
            // Malformed attendee JSON is treated as no attendees; the deterministic
            // pipeline degrades gracefully against partial upstream data.
            return Array.Empty<AttendeeDto>();
        }
    }

    private static string? MapSensitivity(int? sensitivity) =>
        sensitivity switch
        {
            0 => "normal",
            1 => "personal",
            2 => "private",
            3 => "confidential",
            _ => null,
        };

    private static string? MapImportance(int? importance) =>
        importance switch
        {
            0 => "low",
            1 => "normal",
            2 => "high",
            _ => null,
        };

    private sealed record AttendeeJson(string? Name, string? Email);
}
