using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the deterministic context normalizer (D1, AC-1, AC-12).
/// </summary>
[TestClass]
public sealed class MeetingContextNormalizerPropertyTests
{
    private static readonly Gen<AttendeeDto> GenAttendee = Gen.Select(
        Gen.String[0, 8],
        Gen.OneOfConst(
            "  USER@Contoso.COM ",
            "person@example.com",
            "Room@contoso.com",
            "   ",
            string.Empty
        ),
        (name, email) => new AttendeeDto(name, email)
    );

    private static readonly Gen<IReadOnlyList<AttendeeDto>> GenAttendees = GenAttendee
        .List[0, 5]
        .Select(list => (IReadOnlyList<AttendeeDto>)list);

    private static readonly Gen<SchedulingMessageDto> GenMessage = Gen.Select(
        Gen.String[0, 12],
        Gen.String[0, 12],
        (subject, preview) =>
            new SchedulingMessageDto(
                Id: "msg",
                Subject: subject,
                BodyPreview: preview,
                BodyContent: null,
                BodyContentType: null,
                From: new AttendeeDto("F", "from@contoso.com"),
                Sender: new AttendeeDto("S", "sender@contoso.com"),
                ToRecipients: Array.Empty<AttendeeDto>(),
                CcRecipients: Array.Empty<AttendeeDto>(),
                ConversationId: "conv",
                ReceivedDateTime: null,
                MeetingMessageType: "meetingRequest",
                Importance: null
            )
    );

    private static readonly Gen<SchedulingEventDto> GenEvent = Gen.Select(
        GenAttendees,
        GenAttendees,
        GenAttendees,
        (required, optional, resource) =>
            new SchedulingEventDto(
                Id: "evt",
                ICalUId: null,
                SeriesMasterId: null,
                Subject: "Event",
                BodyPreview: null,
                BodyContent: "body",
                BodyContentType: "text",
                Organizer: new AttendeeDto("O", "org@contoso.com"),
                RequiredAttendees: required,
                OptionalAttendees: optional,
                ResourceAttendees: resource,
                Categories: Array.Empty<string>(),
                IsOrganizer: false,
                IsOnlineMeeting: false,
                AllowNewTimeProposals: true,
                Sensitivity: "normal",
                Start: null,
                StartTimeZone: null,
                End: null,
                EndTimeZone: null,
                LastModifiedDateTime: null,
                Type: null
            )
    );

    [TestMethod]
    public void Normalize_ReturnsNonNullPartitionsWithNormalizedEmails()
    {
        Gen.Select(GenMessage, GenEvent)
            .Sample(
                tuple =>
                {
                    var (message, meetingEvent) = tuple;

                    var context = MeetingContextNormalizer.Normalize(
                        "owner@contoso.com",
                        message,
                        meetingEvent
                    );

                    context.RequiredAttendees.Should().NotBeNull();
                    context.OptionalAttendees.Should().NotBeNull();
                    context.ResourceAttendees.Should().NotBeNull();
                    context.AllAttendees.Should().NotBeNull();

                    foreach (var email in context.AllAttendees)
                    {
                        email.Should().Be(email.Trim().ToLowerInvariant());
                        email.Should().NotBeEmpty();
                    }

                    var concatenated = context
                        .RequiredAttendees.Concat(context.OptionalAttendees)
                        .Concat(context.ResourceAttendees);
                    context.AllAttendees.Should().Equal(concatenated);
                },
                iter: 1000
            );
    }
}
