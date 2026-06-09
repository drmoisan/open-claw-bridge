namespace OpenClaw.Core.Agent;

/// <summary>
/// Graph-shaped scheduling event (D6), mirroring the <c>GraphEvent</c> type in master
/// Section 9.2. Fields not yet available from the bridge cache (issues #71-#76) are
/// mapped to null/empty by the runtime mapper and remain so until those issues land.
/// </summary>
/// <param name="Id">The event identifier, or null.</param>
/// <param name="ICalUId">The cross-store iCalendar identifier, or null.</param>
/// <param name="SeriesMasterId">The series-master identifier when this is a recurring occurrence, or null.</param>
/// <param name="Subject">The event subject, or null.</param>
/// <param name="BodyPreview">A short text preview of the body, or null.</param>
/// <param name="BodyContent">The full body content, or null.</param>
/// <param name="BodyContentType">The body content type (for example <c>html</c> or <c>text</c>), or null.</param>
/// <param name="Organizer">The meeting organizer, or null.</param>
/// <param name="RequiredAttendees">The required attendees.</param>
/// <param name="OptionalAttendees">The optional attendees.</param>
/// <param name="ResourceAttendees">The resource (room/equipment) attendees.</param>
/// <param name="Categories">The event categories.</param>
/// <param name="IsOrganizer">Whether the mailbox owner is the organizer.</param>
/// <param name="IsOnlineMeeting">Whether the event is an online meeting.</param>
/// <param name="AllowNewTimeProposals">Whether attendees may propose new times.</param>
/// <param name="Sensitivity">The event sensitivity (for example <c>normal</c> or <c>private</c>), or null.</param>
/// <param name="Start">The event start, or null.</param>
/// <param name="StartTimeZone">The start time-zone identifier, or null.</param>
/// <param name="End">The event end, or null.</param>
/// <param name="EndTimeZone">The end time-zone identifier, or null.</param>
/// <param name="LastModifiedDateTime">The last-modified timestamp, or null.</param>
/// <param name="Type">The event type (for example <c>seriesMaster</c>), or null.</param>
public sealed record SchedulingEventDto(
    string? Id,
    string? ICalUId,
    string? SeriesMasterId,
    string? Subject,
    string? BodyPreview,
    string? BodyContent,
    string? BodyContentType,
    AttendeeDto? Organizer,
    IReadOnlyList<AttendeeDto> RequiredAttendees,
    IReadOnlyList<AttendeeDto> OptionalAttendees,
    IReadOnlyList<AttendeeDto> ResourceAttendees,
    IReadOnlyList<string> Categories,
    bool IsOrganizer,
    bool IsOnlineMeeting,
    bool AllowNewTimeProposals,
    string? Sensitivity,
    DateTimeOffset? Start,
    string? StartTimeZone,
    DateTimeOffset? End,
    string? EndTimeZone,
    DateTimeOffset? LastModifiedDateTime,
    string? Type
);
