using System;
using System.Collections.Generic;
using System.Linq;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Shared test helpers for building deterministic <see cref="NormalizedMeetingContext"/>
/// and <see cref="AgentPolicyOptions"/> instances without I/O.
/// </summary>
internal static class TestContextBuilder
{
    /// <summary>
    /// Builds an <see cref="AgentPolicyOptions"/> with the master Section 9.2 CONFIG
    /// defaults (contoso internal domain, sample VIP organizers, protected categories,
    /// and protected subject patterns).
    /// </summary>
    public static AgentPolicyOptions DefaultPolicyOptions() =>
        new()
        {
            InternalDomains = new[] { "contoso.com" },
            VipOrganizers = new[]
            {
                "ceo@contoso.com",
                "chief.of.staff@contoso.com",
                "cfo@contoso.com",
            },
            ProtectedCategories = new[]
            {
                "Executive",
                "Board",
                "Customer",
                "Launch",
                "Hiring",
                "FinanceClose",
            },
            ProtectedSubjectPatterns = new[]
            {
                @"\b(board|steerco|steering committee|exec staff|staff meeting)\b",
                @"\b(qbr|ebr|renewal|customer escalation|customer review)\b",
                @"\b(launch review|go[- ]live|change advisory|cab|incident review)\b",
                @"\b(interview loop|candidate debrief|performance review|finance close)\b",
                @"\b(1:1|one on one)\b",
            },
            LargeMeetingThreshold = 6,
            VipEmails = new[] { "ceo@contoso.com" },
            DirectReports = new[] { "report@contoso.com" },
            Priority1 = new[] { "p1@contoso.com" },
            Priority2 = new[] { "p2@contoso.com" },
            Priority3 = new[] { "p3@partner.com" },
            InternalDomain = "contoso.com",
            EmblemEmailDomain = "emblem.email",
        };

    /// <summary>
    /// Builds a <see cref="NormalizedMeetingContext"/> with overridable fields. Defaults
    /// produce an internal, non-private, low-dependency meeting.
    /// </summary>
    public static NormalizedMeetingContext Context(
        string subject = "Project sync",
        string bodyText = "Let us meet",
        string messageSender = "colleague@contoso.com",
        string messageFrom = "colleague@contoso.com",
        string organizer = "colleague@contoso.com",
        IReadOnlyList<string>? required = null,
        IReadOnlyList<string>? optional = null,
        IReadOnlyList<string>? resource = null,
        IReadOnlyList<string>? categories = null,
        bool isRecurring = false,
        bool isOnlineMeeting = false,
        string sensitivity = "normal"
    )
    {
        var requiredList = required ?? Array.Empty<string>();
        var optionalList = optional ?? Array.Empty<string>();
        var resourceList = resource ?? Array.Empty<string>();
        var all = requiredList.Concat(optionalList).Concat(resourceList).ToList();

        return new NormalizedMeetingContext(
            MailboxUpn: "owner@contoso.com",
            MessageId: "msg-1",
            ConversationId: "conv-1",
            EventId: "evt-1",
            Subject: subject,
            BodyText: bodyText,
            MessageSender: messageSender,
            MessageFrom: messageFrom,
            Organizer: organizer,
            RequiredAttendees: requiredList,
            OptionalAttendees: optionalList,
            ResourceAttendees: resourceList,
            AllAttendees: all,
            Categories: categories ?? Array.Empty<string>(),
            IsMeetingMessage: true,
            IsOrganizer: false,
            IsRecurring: isRecurring,
            IsOnlineMeeting: isOnlineMeeting,
            AllowNewTimeProposals: true,
            Sensitivity: sensitivity,
            ICalUId: "ical-1",
            SeriesMasterId: isRecurring ? "series-1" : null,
            ReceivedDateTime: new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
            LastModifiedDateTime: null
        );
    }
}
