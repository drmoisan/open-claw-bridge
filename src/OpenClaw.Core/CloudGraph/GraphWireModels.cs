using System.Text.Json.Serialization;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Internal System.Text.Json wire records for the Microsoft Graph v1.0 shapes this
/// adapter requests (D1/D4). Only the <c>$select</c>-listed fields are modeled;
/// deserialization uses <see cref="System.Text.Json.JsonSerializerDefaults.Web"/>
/// (camelCase, case-insensitive), with explicit names only for the OData annotations.
/// </summary>
internal sealed record GraphListPage<T>(
    [property: JsonPropertyName("value")] IReadOnlyList<T>? Value,
    [property: JsonPropertyName("@odata.nextLink")] string? NextLink
);

/// <summary>Top-level Graph error body: <c>{ "error": { "code", "message" } }</c>.</summary>
internal sealed record GraphErrorBody(GraphErrorDetail? Error);

/// <summary>The Graph error detail; <c>code</c> passes through to <c>ApiError.BridgeErrorCode</c>.</summary>
internal sealed record GraphErrorDetail(string? Code, string? Message);

/// <summary>Graph <c>emailAddress</c> shape shared by recipients, senders, and attendees.</summary>
internal sealed record GraphEmailAddress(string? Name, string? Address);

/// <summary>Graph <c>recipient</c> wrapper (<c>from</c>, <c>sender</c>, <c>toRecipients</c>, ...).</summary>
internal sealed record GraphRecipient(GraphEmailAddress? EmailAddress);

/// <summary>
/// Graph message resource limited to the spec <c>$select</c> list plus the
/// <c>@odata.type</c> discriminator and <c>meetingMessageType</c> (D10).
/// </summary>
internal sealed record GraphMessage(
    [property: JsonPropertyName("@odata.type")] string? ODataType,
    string? Id,
    string? Subject,
    string? BodyPreview,
    DateTimeOffset? ReceivedDateTime,
    DateTimeOffset? SentDateTime,
    string? Importance,
    string? Sensitivity,
    bool? IsRead,
    bool? HasAttachments,
    string? ConversationId,
    GraphRecipient? From,
    GraphRecipient? Sender,
    IReadOnlyList<GraphRecipient>? ToRecipients,
    IReadOnlyList<GraphRecipient>? CcRecipients,
    string? MeetingMessageType
);

/// <summary>Graph <c>dateTimeTimeZone</c>: a wall-clock string plus a time-zone name.</summary>
internal sealed record GraphDateTimeTimeZone(string? DateTime, string? TimeZone);

/// <summary>Graph event attendee: <c>type</c> is <c>required</c>/<c>optional</c>/<c>resource</c>.</summary>
internal sealed record GraphAttendee(string? Type, GraphEmailAddress? EmailAddress);

/// <summary>Graph event <c>responseStatus</c>; only <c>response</c> is consumed.</summary>
internal sealed record GraphResponseStatus(string? Response);

/// <summary>Graph event <c>location</c>; only <c>displayName</c> is consumed.</summary>
internal sealed record GraphLocation(string? DisplayName);

/// <summary>Graph <c>itemBody</c>; <c>content</c> is text via the Prefer header.</summary>
internal sealed record GraphItemBody(string? ContentType, string? Content);

/// <summary>Graph event resource limited to the spec <c>$select</c> list.</summary>
internal sealed record GraphEvent(
    string? Id,
    string? ICalUId,
    string? SeriesMasterId,
    string? Subject,
    string? BodyPreview,
    GraphItemBody? Body,
    GraphRecipient? Organizer,
    IReadOnlyList<GraphAttendee>? Attendees,
    IReadOnlyList<string>? Categories,
    bool? IsOrganizer,
    bool? IsOnlineMeeting,
    bool? AllowNewTimeProposals,
    string? Sensitivity,
    string? ShowAs,
    GraphResponseStatus? ResponseStatus,
    GraphLocation? Location,
    GraphDateTimeTimeZone? Start,
    GraphDateTimeTimeZone? End,
    string? Type,
    DateTimeOffset? LastModifiedDateTime
);

/// <summary>Graph <c>workingHours</c> block inside <c>mailboxSettings</c>.</summary>
internal sealed record GraphWorkingHours(
    IReadOnlyList<string>? DaysOfWeek,
    string? StartTime,
    string? EndTime
);

/// <summary>Graph <c>mailboxSettings</c> limited to <c>timeZone</c> and <c>workingHours</c>.</summary>
internal sealed record GraphMailboxSettings(string? TimeZone, GraphWorkingHours? WorkingHours);

/// <summary>A <c>getSchedule</c> schedule item; <c>status</c> partitions busy vs free (D11).</summary>
internal sealed record GraphScheduleItem(
    string? Status,
    GraphDateTimeTimeZone? Start,
    GraphDateTimeTimeZone? End
);

/// <summary>One schedule in a <c>getSchedule</c> response.</summary>
internal sealed record GraphScheduleInformation(IReadOnlyList<GraphScheduleItem>? ScheduleItems);

/// <summary>The <c>getSchedule</c> response: <c>value[].scheduleItems[]</c>.</summary>
internal sealed record GraphScheduleResponse(
    [property: JsonPropertyName("value")] IReadOnlyList<GraphScheduleInformation>? Value
);
