using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the pure Section 9.2 matcher port (AC-3): the result is
/// null or scores at least 4; the selection is invariant under permutation of the
/// event list; scoring is case-insensitive; duplicates do not double-count.
/// </summary>
[TestClass]
public sealed class RelatedEventMatcherPropertyTests
{
    // Small overlapping pools so generated messages and events frequently share
    // tokens/emails and both accept and reject branches are exercised.
    private static readonly Gen<string> GenSubject = Gen.OneOfConst(
            "budget",
            "review",
            "planning",
            "sync",
            "standup",
            "fix",
            "re",
            "it"
        )
        .List[0, 4]
        .Select(tokens => string.Join(' ', tokens));

    private static readonly Gen<AttendeeDto> GenAttendee = Gen.OneOfConst(
            "alice@contoso.com",
            "bob@contoso.com",
            "carol@contoso.com",
            "dave@contoso.com",
            string.Empty
        )
        .Select(email => new AttendeeDto(Name: string.Empty, Email: email));

    private static readonly Gen<IReadOnlyList<AttendeeDto>> GenAttendees = GenAttendee
        .List[0, 3]
        .Select(list => (IReadOnlyList<AttendeeDto>)list);

    private static readonly Gen<SchedulingMessageDto> GenMessage = Gen.Select(
        GenSubject,
        GenAttendee,
        GenAttendees,
        GenAttendees,
        (subject, from, to, cc) =>
            new SchedulingMessageDto(
                Id: "msg",
                Subject: subject,
                BodyPreview: null,
                BodyContent: null,
                BodyContentType: null,
                From: from,
                Sender: null,
                ToRecipients: to,
                CcRecipients: cc,
                ConversationId: "conv",
                ReceivedDateTime: null,
                MeetingMessageType: null,
                Importance: null
            )
    );

    private static readonly Gen<SchedulingEventDto> GenEvent = Gen.Select(
        Gen.String[Gen.Char['a', 'z'], 1, 6],
        GenSubject,
        GenAttendees,
        GenAttendees,
        Gen.DateTimeOffset.Nullable(),
        (id, subject, required, optional, start) =>
            new SchedulingEventDto(
                Id: id,
                ICalUId: null,
                SeriesMasterId: null,
                Subject: subject,
                BodyPreview: null,
                BodyContent: null,
                BodyContentType: null,
                Organizer: new AttendeeDto("O", "organizer@contoso.com"),
                RequiredAttendees: required,
                OptionalAttendees: optional,
                ResourceAttendees: Array.Empty<AttendeeDto>(),
                Categories: Array.Empty<string>(),
                IsOrganizer: false,
                IsOnlineMeeting: false,
                AllowNewTimeProposals: true,
                Sensitivity: "normal",
                Start: start,
                StartTimeZone: null,
                End: null,
                EndTimeZone: null,
                LastModifiedDateTime: null,
                Type: null
            )
    );

    private static readonly Gen<IReadOnlyList<SchedulingEventDto>> GenEvents = GenEvent
        .List[0, 6]
        .Select(list => (IReadOnlyList<SchedulingEventDto>)list);

    [TestMethod]
    public void Result_is_null_or_the_selected_event_scores_at_least_four()
    {
        Gen.Select(GenMessage, GenEvents)
            .Sample(
                tuple =>
                {
                    var (message, events) = tuple;

                    var (selected, score) =
                        RelatedEventMatcher.ChooseMostLikelyRelatedEventWithScore(message, events);

                    if (selected is null)
                    {
                        score.Should().Be(0, "no qualifying event reports score 0");
                    }
                    else
                    {
                        score
                            .Should()
                            .BeGreaterThanOrEqualTo(4, "accepted events must clear the threshold");
                        events.Should().Contain(selected);
                    }
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Selection_is_invariant_under_permutation_of_the_event_list()
    {
        Gen.Select(GenMessage, GenEvents, Gen.Int)
            .Sample(
                tuple =>
                {
                    var (message, events, seed) = tuple;
                    var shuffled = FisherYates(events, seed);

                    var original = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
                        message,
                        events
                    );
                    var permuted = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
                        message,
                        shuffled
                    );

                    permuted
                        .Should()
                        .BeSameAs(original, "the deterministic tie-break is order-independent");
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Scoring_is_case_insensitive()
    {
        Gen.Select(GenMessage, GenEvents)
            .Sample(
                tuple =>
                {
                    var (message, events) = tuple;
                    var upperMessage = message with
                    {
                        Subject = message.Subject?.ToUpperInvariant(),
                        From = Upper(message.From),
                        Sender = Upper(message.Sender),
                        ToRecipients = Upper(message.ToRecipients),
                        CcRecipients = Upper(message.CcRecipients),
                    };

                    var original = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
                        message,
                        events
                    );
                    var uppercased = RelatedEventMatcher.ChooseMostLikelyRelatedEvent(
                        upperMessage,
                        events
                    );

                    uppercased
                        .Should()
                        .BeSameAs(
                            original,
                            "uppercasing subjects and emails must not change the selection"
                        );
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Duplicated_tokens_and_emails_do_not_change_the_score()
    {
        Gen.Select(GenMessage, GenEvents)
            .Sample(
                tuple =>
                {
                    var (message, events) = tuple;
                    var duplicatedMessage = message with
                    {
                        Subject = message.Subject is null
                            ? null
                            : message.Subject + " " + message.Subject,
                        ToRecipients = Doubled(message.ToRecipients),
                        CcRecipients = Doubled(message.CcRecipients),
                    };
                    var duplicatedEvents =
                        (IReadOnlyList<SchedulingEventDto>)
                            events
                                .Select(candidate =>
                                    candidate with
                                    {
                                        Subject = candidate.Subject is null
                                            ? null
                                            : candidate.Subject + " " + candidate.Subject,
                                        RequiredAttendees = Doubled(candidate.RequiredAttendees),
                                        OptionalAttendees = Doubled(candidate.OptionalAttendees),
                                    }
                                )
                                .ToList();

                    var (original, originalScore) =
                        RelatedEventMatcher.ChooseMostLikelyRelatedEventWithScore(message, events);
                    var (duplicated, duplicatedScore) =
                        RelatedEventMatcher.ChooseMostLikelyRelatedEventWithScore(
                            duplicatedMessage,
                            duplicatedEvents
                        );

                    duplicatedScore
                        .Should()
                        .Be(originalScore, "duplicated tokens and emails must not double-count");
                    (duplicated?.Id)
                        .Should()
                        .Be(original?.Id, "set semantics preserve the selection");
                },
                iter: 1000
            );
    }

    private static IReadOnlyList<SchedulingEventDto> FisherYates(
        IReadOnlyList<SchedulingEventDto> events,
        int seed
    )
    {
        var random = new Random(seed);
        var array = events.ToArray();
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }

        return array;
    }

    private static AttendeeDto? Upper(AttendeeDto? attendee) =>
        attendee is null ? null : attendee with { Email = attendee.Email.ToUpperInvariant() };

    private static IReadOnlyList<AttendeeDto> Upper(IReadOnlyList<AttendeeDto> attendees) =>
        attendees.Select(attendee => Upper(attendee)!).ToList();

    private static IReadOnlyList<AttendeeDto> Doubled(IReadOnlyList<AttendeeDto> attendees) =>
        attendees.Concat(attendees).ToList();
}
