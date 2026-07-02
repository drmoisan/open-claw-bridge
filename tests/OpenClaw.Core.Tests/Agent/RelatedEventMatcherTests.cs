using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the pure Section 9.2 matcher port (AC-2): scoring rows, threshold,
/// deterministic tie-breaks, degenerate inputs, normalization, and null-arg contracts.
/// </summary>
[TestClass]
public sealed class RelatedEventMatcherTests
{
    private static SchedulingMessageDto Message(
        string? subject = null,
        AttendeeDto? from = null,
        AttendeeDto? sender = null,
        IReadOnlyList<AttendeeDto>? to = null,
        IReadOnlyList<AttendeeDto>? cc = null
    ) =>
        new(
            Id: "msg-1",
            Subject: subject,
            BodyPreview: null,
            BodyContent: null,
            BodyContentType: null,
            From: from,
            Sender: sender,
            ToRecipients: to ?? Array.Empty<AttendeeDto>(),
            CcRecipients: cc ?? Array.Empty<AttendeeDto>(),
            ConversationId: "conv-1",
            ReceivedDateTime: new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
            MeetingMessageType: null,
            Importance: null
        );

    private static SchedulingEventDto Event(
        string? id = "evt-1",
        string? subject = null,
        AttendeeDto? organizer = null,
        IReadOnlyList<AttendeeDto>? required = null,
        IReadOnlyList<AttendeeDto>? optional = null,
        IReadOnlyList<AttendeeDto>? resource = null,
        DateTimeOffset? start = null
    ) =>
        new(
            Id: id,
            ICalUId: null,
            SeriesMasterId: null,
            Subject: subject,
            BodyPreview: null,
            BodyContent: null,
            BodyContentType: null,
            Organizer: organizer,
            RequiredAttendees: required ?? Array.Empty<AttendeeDto>(),
            OptionalAttendees: optional ?? Array.Empty<AttendeeDto>(),
            ResourceAttendees: resource ?? Array.Empty<AttendeeDto>(),
            Categories: Array.Empty<string>(),
            IsOrganizer: false,
            IsOnlineMeeting: false,
            AllowNewTimeProposals: true,
            Sensitivity: "normal",
            Start: start,
            StartTimeZone: null,
            End: start is null ? null : start.Value.AddHours(1),
            EndTimeZone: null,
            LastModifiedDateTime: null,
            Type: null
        );

    private static AttendeeDto Attendee(string email) => new(Name: string.Empty, Email: email);

    [TestMethod]
    public void Subject_only_match_with_two_shared_tokens_scores_four_and_is_accepted()
    {
        var message = Message(subject: "budget review");
        var candidate = Event(subject: "Quarterly budget review sync");

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeSameAs(candidate, "two shared subject tokens score 2 + 2 = 4");
    }

    [TestMethod]
    public void Attendee_only_match_with_two_shared_participants_scores_six_and_is_accepted()
    {
        var message = Message(
            from: Attendee("alice@contoso.com"),
            to: [Attendee("bob@contoso.com")]
        );
        var candidate = Event(
            required: [Attendee("alice@contoso.com")],
            optional: [Attendee("bob@contoso.com")]
        );

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeSameAs(candidate, "two shared participant emails score 3 + 3 = 6");
    }

    [TestMethod]
    public void Combined_token_and_participant_scores_five_and_is_accepted()
    {
        var message = Message(subject: "standup", from: Attendee("alice@contoso.com"));
        var candidate = Event(subject: "Team standup", required: [Attendee("alice@contoso.com")]);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeSameAs(candidate, "one token (2) plus one participant (3) scores 5");
    }

    [TestMethod]
    public void Single_participant_match_scores_three_and_returns_null()
    {
        var message = Message(from: Attendee("alice@contoso.com"));
        var candidate = Event(required: [Attendee("alice@contoso.com")]);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeNull("a single participant match scores 3, below the threshold of 4");
    }

    [TestMethod]
    public void Exact_threshold_score_of_four_is_accepted()
    {
        var message = Message(subject: "planning session");
        var candidate = Event(subject: "planning session");

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeSameAs(candidate, "the threshold is inclusive: score >= 4 accepts");
    }

    [TestMethod]
    public void Tie_break_selects_earliest_start_and_null_start_sorts_last()
    {
        var message = Message(subject: "budget review");
        var later = Event(
            id: "evt-later",
            subject: "budget review",
            start: new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero)
        );
        var earlier = Event(
            id: "evt-earlier",
            subject: "budget review",
            start: new DateTimeOffset(2026, 7, 3, 9, 0, 0, TimeSpan.Zero)
        );
        var nullStart = Event(id: "evt-null-start", subject: "budget review", start: null);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
            message,
            [nullStart, later, earlier]
        );

        result.Should().BeSameAs(earlier, "the earliest Start wins and null Start sorts last");
    }

    [TestMethod]
    public void Tie_break_selects_smallest_ordinal_id_when_start_ties()
    {
        var message = Message(subject: "budget review");
        var start = new DateTimeOffset(2026, 7, 3, 9, 0, 0, TimeSpan.Zero);
        var idB = Event(id: "evt-b", subject: "budget review", start: start);
        var idA = Event(id: "evt-a", subject: "budget review", start: start);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [idB, idA]);

        result.Should().BeSameAs(idA, "when Start ties the smallest ordinal Id wins");
    }

    [TestMethod]
    public void Empty_event_list_returns_null()
    {
        var message = Message(subject: "budget review");

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
            message,
            Array.Empty<SchedulingEventDto>()
        );

        result.Should().BeNull("there is no candidate to select");
    }

    [TestMethod]
    public void Null_subject_contributes_zero_so_single_participant_stays_below_threshold()
    {
        var message = Message(subject: null, from: Attendee("alice@contoso.com"));
        var candidate = Event(subject: "budget review", required: [Attendee("alice@contoso.com")]);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result
            .Should()
            .BeNull("a null subject yields no tokens, leaving only 3 from the participant");
    }

    [TestMethod]
    public void Short_tokens_are_ignored_so_single_participant_stays_below_threshold()
    {
        var message = Message(subject: "re: fix it now", from: Attendee("alice@contoso.com"));
        var candidate = Event(subject: "re: fix it now", required: [Attendee("alice@contoso.com")]);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeNull("tokens shorter than 4 characters score nothing");
    }

    [TestMethod]
    public void Token_and_email_matching_is_case_insensitive()
    {
        var message = Message(subject: "BUDGET Review", from: Attendee("ALICE@Contoso.COM"));
        var candidate = Event(subject: "budget review", required: [Attendee("alice@contoso.com")]);

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeSameAs(candidate, "tokens and emails are compared case-insensitively");
    }

    [TestMethod]
    public void Organizer_email_is_not_counted_toward_the_score()
    {
        var message = Message(
            from: Attendee("alice@contoso.com"),
            to: [Attendee("bob@contoso.com")]
        );
        var candidate = Event(
            organizer: Attendee("alice@contoso.com"),
            required: [Attendee("bob@contoso.com")]
        );

        var result = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, [candidate]);

        result.Should().BeNull("the organizer is excluded, so only one participant (3) matches");
    }

    [TestMethod]
    public void Null_message_throws_argument_null_exception()
    {
        var act = () =>
            RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
                null!,
                Array.Empty<SchedulingEventDto>()
            );

        act.Should().Throw<ArgumentNullException>().WithParameterName("message");
    }

    [TestMethod]
    public void Null_events_throws_argument_null_exception()
    {
        var act = () => RelatedEventMatcher.ChooseMostLikelyRelatedEvent(Message(), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("events");
    }
}
