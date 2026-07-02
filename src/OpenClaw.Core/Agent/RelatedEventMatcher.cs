using System.Text.RegularExpressions;

namespace OpenClaw.Core.Agent;

/// <summary>
/// Pure port of master Section 9.2 <c>chooseMostLikelyRelatedEvent</c>. Scores each
/// candidate event against the message by shared subject tokens (+2 each) and shared
/// participant emails (+3 each), returning the best event only when its score is at
/// least 4. Ties among max-scoring qualifiers resolve deterministically by earliest
/// <see cref="SchedulingEventDto.Start"/> (null last), then smallest ordinal
/// <see cref="SchedulingEventDto.Id"/> (null treated as empty).
/// </summary>
public static partial class RelatedEventMatcher
{
    private const int SubjectTokenWeight = 2;
    private const int ParticipantEmailWeight = 3;
    private const int AcceptThreshold = 4;
    private const int MinimumTokenLength = 4;

    // Master Section 9.2 tokenizes subjects by lowercasing and splitting on runs of
    // non-alphanumeric characters; inputs are lowercased before this regex applies.
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRunRegex();

    /// <summary>
    /// Selects the calendar event most likely related to <paramref name="message"/>,
    /// or null when no event scores at least 4.
    /// </summary>
    /// <param name="message">The hydrated scheduling message.</param>
    /// <param name="events">The candidate calendar-window events.</param>
    /// <returns>The best-scoring event with score &gt;= 4, or null.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="events"/> is null.
    /// </exception>
    public static SchedulingEventDto? ChooseMostLikelyRelatedEvent(
        SchedulingMessageDto message,
        IReadOnlyList<SchedulingEventDto> events
    ) => ChooseMostLikelyRelatedEventWithScore(message, events).Event;

    /// <summary>
    /// Selects the most likely related event together with its score so callers can
    /// log the score without recomputing. Returns (null, 0) when no event qualifies.
    /// </summary>
    internal static (SchedulingEventDto? Event, int Score) ChooseMostLikelyRelatedEventWithScore(
        SchedulingMessageDto message,
        IReadOnlyList<SchedulingEventDto> events
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(events);

        var messageTokens = TokenizeSubject(message.Subject);
        var participantEmails = CollectParticipantEmails(message);

        SchedulingEventDto? best = null;
        var bestScore = 0;
        foreach (var candidate in events)
        {
            var score = ScoreEvent(messageTokens, participantEmails, candidate);
            if (score < AcceptThreshold)
            {
                continue;
            }

            if (
                best is null
                || score > bestScore
                || (score == bestScore && PrecedesInTieBreak(candidate, best))
            )
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best is null ? (null, 0) : (best, bestScore);
    }

    private static int ScoreEvent(
        HashSet<string> messageTokens,
        HashSet<string> participantEmails,
        SchedulingEventDto candidate
    )
    {
        var eventTokens = TokenizeSubject(candidate.Subject);
        var attendeeEmails = CollectAttendeeEmails(candidate);
        var sharedTokens = messageTokens.Count(eventTokens.Contains);
        var sharedParticipants = participantEmails.Count(attendeeEmails.Contains);
        return (sharedTokens * SubjectTokenWeight) + (sharedParticipants * ParticipantEmailWeight);
    }

    /// <summary>
    /// Lowercases the subject, splits on runs of non-alphanumeric characters, and keeps
    /// distinct tokens of length at least 4 (master Section 9.2 set semantics).
    /// </summary>
    private static HashSet<string> TokenizeSubject(string? subject)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(subject))
        {
            return tokens;
        }

        var lowered = subject.ToLowerInvariant();
        foreach (var token in NonAlphanumericRunRegex().Split(lowered))
        {
            if (token.Length >= MinimumTokenLength)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Collects the distinct normalized emails of From, Sender, To, and Cc, dropping
    /// empties, using the same normalization as <see cref="MeetingContextNormalizer"/>.
    /// </summary>
    private static HashSet<string> CollectParticipantEmails(SchedulingMessageDto message)
    {
        var emails = new HashSet<string>(StringComparer.Ordinal);
        AddNonEmpty(emails, MeetingContextNormalizer.EmailOf(message.From));
        AddNonEmpty(emails, MeetingContextNormalizer.EmailOf(message.Sender));
        AddAttendeeEmails(emails, message.ToRecipients);
        AddAttendeeEmails(emails, message.CcRecipients);
        return emails;
    }

    /// <summary>
    /// Collects the distinct normalized attendee emails of an event as the union of
    /// required, optional, and resource attendees. The organizer is intentionally
    /// excluded, matching the master reference which scores <c>event.attendees</c>.
    /// </summary>
    private static HashSet<string> CollectAttendeeEmails(SchedulingEventDto candidate)
    {
        var emails = new HashSet<string>(StringComparer.Ordinal);
        AddAttendeeEmails(emails, candidate.RequiredAttendees);
        AddAttendeeEmails(emails, candidate.OptionalAttendees);
        AddAttendeeEmails(emails, candidate.ResourceAttendees);
        return emails;
    }

    private static void AddAttendeeEmails(
        HashSet<string> emails,
        IReadOnlyList<AttendeeDto> attendees
    )
    {
        foreach (var attendee in attendees)
        {
            AddNonEmpty(emails, MeetingContextNormalizer.EmailOf(attendee));
        }
    }

    private static void AddNonEmpty(HashSet<string> emails, string email)
    {
        if (email.Length > 0)
        {
            emails.Add(email);
        }
    }

    /// <summary>
    /// Deterministic tie-break among equal-scoring qualifiers: earliest
    /// <see cref="SchedulingEventDto.Start"/> wins (null sorts last); when Start ties,
    /// the smallest ordinal <see cref="SchedulingEventDto.Id"/> wins (null as empty).
    /// </summary>
    private static bool PrecedesInTieBreak(SchedulingEventDto candidate, SchedulingEventDto current)
    {
        if (candidate.Start is not null && current.Start is null)
        {
            return true;
        }

        if (candidate.Start is null && current.Start is not null)
        {
            return false;
        }

        if (
            candidate.Start is not null
            && current.Start is not null
            && candidate.Start != current.Start
        )
        {
            return candidate.Start < current.Start;
        }

        return string.CompareOrdinal(candidate.Id ?? string.Empty, current.Id ?? string.Empty) < 0;
    }
}
