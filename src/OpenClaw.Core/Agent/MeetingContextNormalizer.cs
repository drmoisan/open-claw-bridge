namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic context normalizer (D1). <see cref="Normalize"/> is a pure function
/// that projects a Graph-shaped message (and optional event) into a flat
/// <see cref="NormalizedMeetingContext"/>, ported from <c>normalizeContext</c> in
/// master Section 9.2. It performs no I/O and carries no dependency on the bridge, host
/// adapter, or COM.
/// </summary>
public static partial class MeetingContextNormalizer
{
    private static readonly IReadOnlyList<AttendeeDto> EmptyAttendees = Array.Empty<AttendeeDto>();
    private static readonly IReadOnlyList<string> EmptyStrings = Array.Empty<string>();

    /// <summary>
    /// Normalizes the supplied message and optional event into a
    /// <see cref="NormalizedMeetingContext"/>. Subject and body fall back from event to
    /// message; HTML is stripped from the event body when its content type is HTML;
    /// emails are trimmed and lowercased; attendees are partitioned; sensitivity
    /// defaults to <c>normal</c>.
    /// </summary>
    /// <param name="mailboxUpn">The mailbox owner UPN. Must not be null.</param>
    /// <param name="message">The Graph-shaped message. Must not be null.</param>
    /// <param name="meetingEvent">The associated event, or null for ordinary mail.</param>
    /// <returns>The normalized meeting context.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="mailboxUpn"/> or <paramref name="message"/> is null.
    /// </exception>
    public static NormalizedMeetingContext Normalize(
        string mailboxUpn,
        SchedulingMessageDto message,
        SchedulingEventDto? meetingEvent
    )
    {
        ArgumentNullException.ThrowIfNull(mailboxUpn);
        ArgumentNullException.ThrowIfNull(message);

        var (required, optional, resource, all) = NormalizeAttendees(
            meetingEvent?.RequiredAttendees ?? EmptyAttendees,
            meetingEvent?.OptionalAttendees ?? EmptyAttendees,
            meetingEvent?.ResourceAttendees ?? EmptyAttendees
        );

        var subject = (meetingEvent?.Subject ?? message.Subject ?? string.Empty).Trim();
        var bodyText = ResolveBodyText(message, meetingEvent);
        var categories = NormalizeCategories(meetingEvent?.Categories ?? EmptyStrings);

        return new NormalizedMeetingContext(
            MailboxUpn: mailboxUpn,
            MessageId: message.Id ?? string.Empty,
            ConversationId: message.ConversationId ?? string.Empty,
            EventId: meetingEvent?.Id,
            Subject: subject,
            BodyText: bodyText,
            MessageSender: EmailOf(message.Sender),
            MessageFrom: EmailOf(message.From),
            Organizer: EmailOf(meetingEvent?.Organizer),
            RequiredAttendees: required,
            OptionalAttendees: optional,
            ResourceAttendees: resource,
            AllAttendees: all,
            Categories: categories,
            IsMeetingMessage: !string.IsNullOrEmpty(message.MeetingMessageType),
            IsOrganizer: meetingEvent?.IsOrganizer ?? false,
            IsRecurring: !string.IsNullOrEmpty(meetingEvent?.SeriesMasterId)
                || string.Equals(meetingEvent?.Type, "seriesMaster", StringComparison.Ordinal),
            IsOnlineMeeting: meetingEvent?.IsOnlineMeeting ?? false,
            AllowNewTimeProposals: meetingEvent?.AllowNewTimeProposals ?? false,
            Sensitivity: (meetingEvent?.Sensitivity ?? "normal").ToLowerInvariant(),
            ICalUId: meetingEvent?.ICalUId,
            SeriesMasterId: meetingEvent?.SeriesMasterId,
            ReceivedDateTime: message.ReceivedDateTime,
            LastModifiedDateTime: meetingEvent?.LastModifiedDateTime
        );
    }

    /// <summary>
    /// Resolves body text per master Section 9.2: when the event body is HTML, strip
    /// it; otherwise prefer the event body, then the message body preview, then the
    /// stripped message body.
    /// </summary>
    private static string ResolveBodyText(
        SchedulingMessageDto message,
        SchedulingEventDto? meetingEvent
    )
    {
        if (
            meetingEvent is not null
            && string.Equals(
                meetingEvent.BodyContentType,
                "html",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return StripHtml(meetingEvent.BodyContent);
        }

        var eventBody = meetingEvent?.BodyContent?.Trim();
        if (!string.IsNullOrEmpty(eventBody))
        {
            return eventBody;
        }

        var preview = message.BodyPreview?.Trim();
        if (!string.IsNullOrEmpty(preview))
        {
            return preview;
        }

        return StripHtml(message.BodyContent);
    }

    private static IReadOnlyList<string> NormalizeCategories(IReadOnlyList<string> categories)
    {
        var result = new List<string>(categories.Count);
        foreach (var category in categories)
        {
            var trimmed = category.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
