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
/// The event Graph fields iCalUId, seriesMasterId, categories, isOrganizer, online-meeting and
/// new-time-proposal flags, and last-modified time are supplied by the bridge cache as of #72 and
/// are mapped through directly. Some message Graph fields remain unavailable until later issues
/// (#71-#76) land — message conversation id, from/sender split nuances, and body content type —
/// and are mapped to null deterministically.
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

        // Sender reflects the resolved SMTP sender; From reflects the on-behalf-of/delegate
        // identity (or the resolved sender when not delegate-sent) per Master 9.2 and issue #73
        // (decisions D-A/D-C). The two are distinct values populated by the bridge scanner.
        var sender = BuildAttendee(message.SenderName, message.SenderEmailResolved);
        var from = BuildAttendee(message.SenderName, message.FromEmailAddress);

        return new SchedulingMessageDto(
            Id: message.BridgeId,
            Subject: message.Subject,
            BodyPreview: message.BodyPreview,
            // Full body content is not exposed by the bridge cache (#71-#76); only the
            // preview is available.
            BodyContent: null,
            BodyContentType: null,
            From: from,
            Sender: sender,
            ToRecipients: ParseAttendees(message.ToJson),
            CcRecipients: ParseAttendees(message.CcJson),
            ConversationId: message.ConversationId,
            ReceivedDateTime: message.ReceivedUtc ?? message.SentUtc,
            MeetingMessageType: MapMeetingMessageType(message.MeetingMessageType),
            Importance: MapImportance(message.Importance)
        );
    }

    /// <summary>
    /// Maps a bridge <see cref="EventDto"/> to a Graph-shaped
    /// <see cref="SchedulingEventDto"/>. The event Graph fields supplied by #72 (iCalUId,
    /// seriesMasterId, categories, isOrganizer, online-meeting and new-time-proposal flags,
    /// last-modified time) map through directly; body content type remains null.
    /// </summary>
    /// <param name="evt">The bridge event. Must not be null.</param>
    /// <returns>The Graph-shaped scheduling event.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evt"/> is null.</exception>
    public SchedulingEventDto MapEvent(EventDto evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new SchedulingEventDto(
            Id: evt.BridgeId,
            // iCalUId reuses GlobalAppointmentID, populated by the bridge scanner into
            // EventDto.ICalUId (#72); the mapper consumes the contract field directly.
            ICalUId: evt.ICalUId,
            SeriesMasterId: evt.SeriesMasterId,
            Subject: evt.Subject,
            BodyPreview: evt.BodyPreview,
            BodyContent: null,
            BodyContentType: null,
            Organizer: BuildAttendee(null, evt.Organizer),
            RequiredAttendees: ParseAttendees(evt.RequiredAttendeesJson),
            OptionalAttendees: ParseAttendees(evt.OptionalAttendeesJson),
            ResourceAttendees: ParseAttendees(evt.ResourcesJson),
            Categories: evt.Categories ?? Array.Empty<string>(),
            IsOrganizer: evt.IsOrganizer,
            IsOnlineMeeting: evt.IsOnlineMeeting,
            AllowNewTimeProposals: evt.AllowNewTimeProposals,
            Sensitivity: MapSensitivity(evt.Sensitivity),
            Start: evt.StartUtc,
            StartTimeZone: "UTC",
            End: evt.EndUtc,
            EndTimeZone: "UTC",
            LastModifiedDateTime: evt.LastModifiedDateTime,
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

    /// <summary>
    /// Maps the raw <c>OlMeetingType</c> integer carried by <see cref="MessageDto.MeetingMessageType"/>
    /// (issue #73, decision D-B) to the Graph <c>meetingMessageType</c> vocabulary. Ordinary mail
    /// (null) and unknown values map to null.
    /// </summary>
    private static string? MapMeetingMessageType(int? type) =>
        type switch
        {
            0 => "meetingRequest",
            1 => "meetingCancelled",
            2 => "meetingDeclined",
            3 => "meetingAccepted",
            4 => "meetingTentativelyAccepted",
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
