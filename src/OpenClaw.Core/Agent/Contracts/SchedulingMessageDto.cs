namespace OpenClaw.Core.Agent;

/// <summary>
/// Graph-shaped scheduling message (D6), mirroring the <c>GraphMessage</c> type in
/// master Section 9.2. Fields not yet available from the bridge cache (issues
/// #71-#76) are mapped to null/empty by the runtime mapper and remain so until those
/// issues land.
/// </summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Subject">The message subject, or null.</param>
/// <param name="BodyPreview">A short text preview of the body, or null.</param>
/// <param name="BodyContent">The full body content, or null.</param>
/// <param name="BodyContentType">The body content type (for example <c>html</c> or <c>text</c>), or null.</param>
/// <param name="From">The logical from address, or null.</param>
/// <param name="Sender">The actual submitting address, or null.</param>
/// <param name="ToRecipients">The To recipients.</param>
/// <param name="CcRecipients">The Cc recipients.</param>
/// <param name="ConversationId">The conversation identifier, or null.</param>
/// <param name="ReceivedDateTime">The received timestamp, or null.</param>
/// <param name="MeetingMessageType">The meeting-message type when the message is a meeting message, or null.</param>
/// <param name="Importance">The message importance (for example <c>high</c>), or null.</param>
public sealed record SchedulingMessageDto(
    string Id,
    string? Subject,
    string? BodyPreview,
    string? BodyContent,
    string? BodyContentType,
    AttendeeDto? From,
    AttendeeDto? Sender,
    IReadOnlyList<AttendeeDto> ToRecipients,
    IReadOnlyList<AttendeeDto> CcRecipients,
    string? ConversationId,
    DateTimeOffset? ReceivedDateTime,
    string? MeetingMessageType,
    string? Importance
);
