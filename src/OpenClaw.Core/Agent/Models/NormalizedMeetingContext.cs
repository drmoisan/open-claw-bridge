namespace OpenClaw.Core.Agent;

/// <summary>
/// The flat, string-typed normalized meeting context (D1) produced by
/// <see cref="MeetingContextNormalizer"/>, mirroring the
/// <c>NormalizedMeetingContext</c> type in master Section 9.2. All email addresses are
/// trimmed and lowercased; body text is HTML-stripped where applicable. This value
/// type is the single input to the deterministic triage, priority, and move-policy
/// layers and carries no dependency on the bridge, host adapter, or COM.
/// </summary>
/// <param name="MailboxUpn">The mailbox owner UPN.</param>
/// <param name="MessageId">The source message identifier.</param>
/// <param name="ConversationId">The conversation identifier.</param>
/// <param name="EventId">The associated event identifier, or null.</param>
/// <param name="Subject">The normalized (trimmed) subject.</param>
/// <param name="BodyText">The normalized body text.</param>
/// <param name="MessageSender">The normalized actual sender address.</param>
/// <param name="MessageFrom">The normalized logical from address.</param>
/// <param name="Organizer">The normalized organizer address.</param>
/// <param name="RequiredAttendees">The required attendee addresses.</param>
/// <param name="OptionalAttendees">The optional attendee addresses.</param>
/// <param name="ResourceAttendees">The resource attendee addresses.</param>
/// <param name="AllAttendees">All attendee addresses (required, then optional, then resource).</param>
/// <param name="Categories">The trimmed, non-empty categories.</param>
/// <param name="IsMeetingMessage">Whether the source message is a meeting message.</param>
/// <param name="IsOrganizer">Whether the mailbox owner is the organizer.</param>
/// <param name="IsRecurring">Whether the event is recurring.</param>
/// <param name="IsOnlineMeeting">Whether the event is an online meeting.</param>
/// <param name="AllowNewTimeProposals">Whether new time proposals are allowed.</param>
/// <param name="Sensitivity">The lowercased sensitivity (defaults to <c>normal</c>).</param>
/// <param name="ICalUId">The cross-store iCalendar identifier, or null.</param>
/// <param name="SeriesMasterId">The series-master identifier, or null.</param>
/// <param name="ReceivedDateTime">The message received timestamp, or null.</param>
/// <param name="LastModifiedDateTime">The event last-modified timestamp, or null.</param>
public sealed record NormalizedMeetingContext(
    string MailboxUpn,
    string MessageId,
    string ConversationId,
    string? EventId,
    string Subject,
    string BodyText,
    string MessageSender,
    string MessageFrom,
    string Organizer,
    IReadOnlyList<string> RequiredAttendees,
    IReadOnlyList<string> OptionalAttendees,
    IReadOnlyList<string> ResourceAttendees,
    IReadOnlyList<string> AllAttendees,
    IReadOnlyList<string> Categories,
    bool IsMeetingMessage,
    bool IsOrganizer,
    bool IsRecurring,
    bool IsOnlineMeeting,
    bool AllowNewTimeProposals,
    string Sensitivity,
    string? ICalUId,
    string? SeriesMasterId,
    DateTimeOffset? ReceivedDateTime,
    DateTimeOffset? LastModifiedDateTime
);
