using System.Globalization;
using OpenClaw.MailBridge.Contracts.Models;
using Or5Recipient = OpenClaw.Core.CloudGraph.GraphMessageMapper.Or5Recipient;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Pure static mapping from the internal Graph event wire record to
/// <see cref="EventDto"/> per the spec "Data &amp; State" EventDto table:
/// <c>start</c>/<c>end</c> convert to UTC, the <c>showAs</c>/<c>responseStatus</c>/
/// sensitivity enum maps are the local vocabulary, attendees partition by <c>type</c>
/// into OR-5 JSON, and <c>type != "singleInstance"</c> drives <c>IsRecurring</c>. No
/// I/O and no mutation; missing required <c>id</c>/<c>start</c>/<c>end</c> fail fast
/// with <see cref="GraphMappingException"/>.
/// </summary>
internal static class GraphEventMapper
{
    /// <summary>
    /// Maps a deserialized Graph event to the wire <see cref="EventDto"/>.
    /// </summary>
    /// <param name="graphEvent">The Graph wire record. Must not be null.</param>
    /// <exception cref="GraphMappingException">
    /// A required field (<c>id</c>, <c>start</c>, <c>end</c>) is missing or unusable.
    /// </exception>
    public static EventDto Map(GraphEvent graphEvent)
    {
        ArgumentNullException.ThrowIfNull(graphEvent);

        if (string.IsNullOrWhiteSpace(graphEvent.Id))
        {
            throw new GraphMappingException("The Graph event is missing the required field 'id'.");
        }

        var (requiredJson, optionalJson, resourcesJson) = PartitionAttendees(graphEvent.Attendees);

        return new EventDto(
            BridgeId: graphEvent.Id,
            GlobalAppointmentId: null,
            Subject: graphEvent.Subject,
            StartUtc: ToUtc(graphEvent.Start, "start"),
            EndUtc: ToUtc(graphEvent.End, "end"),
            Location: graphEvent.Location?.DisplayName,
            BusyStatus: MapShowAs(graphEvent.ShowAs),
            MeetingStatus: null,
            IsRecurring: IsRecurringType(graphEvent.Type),
            Sensitivity: GraphMessageMapper.MapSensitivity(graphEvent.Sensitivity),
            Organizer: graphEvent.Organizer?.EmailAddress?.Address,
            RequiredAttendeesJson: requiredJson,
            OptionalAttendeesJson: optionalJson,
            ResourcesJson: resourcesJson,
            BodyPreview: graphEvent.BodyPreview,
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            ResponseStatus: MapResponseStatus(graphEvent.ResponseStatus?.Response),
            Categories: graphEvent.Categories?.ToArray(),
            IsOrganizer: graphEvent.IsOrganizer ?? false,
            IsOnlineMeeting: graphEvent.IsOnlineMeeting ?? false,
            AllowNewTimeProposals: graphEvent.AllowNewTimeProposals ?? false,
            ICalUId: graphEvent.ICalUId,
            SeriesMasterId: graphEvent.SeriesMasterId,
            LastModifiedDateTime: graphEvent.LastModifiedDateTime,
            BodyFull: graphEvent.Body?.Content,
            SensitivityLabel: null
        );
    }

    /// <summary>Maps Graph <c>showAs</c> to the local busy-status integers; unknown maps to null.</summary>
    internal static int? MapShowAs(string? showAs) =>
        showAs switch
        {
            "free" => 0,
            "tentative" => 1,
            "busy" => 2,
            "oof" => 3,
            "workingElsewhere" => 4,
            _ => null,
        };

    /// <summary>
    /// Maps Graph <c>responseStatus.response</c> to the local response integers;
    /// unknown maps to null.
    /// </summary>
    internal static int? MapResponseStatus(string? response) =>
        response switch
        {
            "none" => 0,
            "organizer" => 1,
            "tentativelyAccepted" => 2,
            "accepted" => 3,
            "declined" => 4,
            "notResponded" => 5,
            _ => null,
        };

    /// <summary>
    /// <c>occurrence</c>, <c>exception</c>, and <c>seriesMaster</c> are recurring;
    /// <c>singleInstance</c>, absent, and unknown values are not.
    /// </summary>
    internal static bool IsRecurringType(string? type) =>
        type is "occurrence" or "exception" or "seriesMaster";

    /// <summary>
    /// Partitions Graph attendees by <c>type</c> into the three OR-5 JSON strings
    /// (required / optional / resource). An attendee with an absent or unknown type
    /// defaults to required (the Graph default); an empty partition maps to null.
    /// </summary>
    internal static (
        string? RequiredJson,
        string? OptionalJson,
        string? ResourcesJson
    ) PartitionAttendees(IReadOnlyList<GraphAttendee>? attendees)
    {
        if (attendees is null || attendees.Count == 0)
        {
            return (null, null, null);
        }

        var required = new List<Or5Recipient>();
        var optional = new List<Or5Recipient>();
        var resources = new List<Or5Recipient>();

        foreach (var attendee in attendees)
        {
            var recipient = new Or5Recipient(
                attendee.EmailAddress?.Name ?? string.Empty,
                attendee.EmailAddress?.Address ?? string.Empty
            );

            var bucket = attendee.Type switch
            {
                "optional" => optional,
                "resource" => resources,
                _ => required,
            };
            bucket.Add(recipient);
        }

        return (
            GraphMessageMapper.SerializeOr5(required),
            GraphMessageMapper.SerializeOr5(optional),
            GraphMessageMapper.SerializeOr5(resources)
        );
    }

    /// <summary>
    /// Converts a Graph <c>dateTimeTimeZone</c> to a UTC <see cref="DateTimeOffset"/>.
    /// <c>UTC</c> (the deterministic Prefer-header default) is handled directly; any
    /// other zone resolves through <see cref="TimeZoneInfo"/>.
    /// </summary>
    internal static DateTimeOffset ToUtc(GraphDateTimeTimeZone? value, string fieldName)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.DateTime))
        {
            throw new GraphMappingException(
                $"The Graph event is missing the required field '{fieldName}'."
            );
        }

        if (
            !System.DateTime.TryParse(
                value.DateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var wallClock
            )
        )
        {
            throw new GraphMappingException(
                $"The Graph event field '{fieldName}' carries an unparseable dateTime value."
            );
        }

        var zoneName = value.TimeZone;
        if (
            string.IsNullOrWhiteSpace(zoneName)
            || string.Equals(zoneName, "UTC", StringComparison.OrdinalIgnoreCase)
        )
        {
            return new DateTimeOffset(System.DateTime.SpecifyKind(wallClock, DateTimeKind.Utc));
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
            var offset = zone.GetUtcOffset(wallClock);
            return new DateTimeOffset(wallClock, offset).ToUniversalTime();
        }
        catch (TimeZoneNotFoundException)
        {
            throw new GraphMappingException(
                $"The Graph event field '{fieldName}' names an unknown time zone."
            );
        }
    }
}
