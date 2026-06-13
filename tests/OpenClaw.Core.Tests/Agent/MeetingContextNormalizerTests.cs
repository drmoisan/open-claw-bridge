using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic context normalizer (D1, AC-1).
/// </summary>
[TestClass]
public sealed class MeetingContextNormalizerTests
{
    private const string MailboxUpn = "owner@contoso.com";

    private static SchedulingMessageDto Message(
        string? subject = "Subject",
        string? bodyPreview = null,
        string? bodyContent = null,
        string? bodyContentType = null,
        AttendeeDto? from = null,
        AttendeeDto? sender = null,
        string? meetingMessageType = "meetingRequest"
    ) =>
        new(
            Id: "msg-1",
            Subject: subject,
            BodyPreview: bodyPreview,
            BodyContent: bodyContent,
            BodyContentType: bodyContentType,
            From: from,
            Sender: sender,
            ToRecipients: Array.Empty<AttendeeDto>(),
            CcRecipients: Array.Empty<AttendeeDto>(),
            ConversationId: "conv-1",
            ReceivedDateTime: new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
            MeetingMessageType: meetingMessageType,
            Importance: null
        );

    private static SchedulingEventDto Event(
        string? subject = "Event subject",
        string? bodyContent = null,
        string? bodyContentType = null,
        AttendeeDto? organizer = null,
        IReadOnlyList<AttendeeDto>? required = null,
        IReadOnlyList<AttendeeDto>? optional = null,
        IReadOnlyList<AttendeeDto>? resource = null,
        IReadOnlyList<string>? categories = null,
        string? sensitivity = "normal",
        string? seriesMasterId = null,
        string? type = null,
        bool isOnlineMeeting = false
    ) =>
        new(
            Id: "evt-1",
            ICalUId: "ical-1",
            SeriesMasterId: seriesMasterId,
            Subject: subject,
            BodyPreview: null,
            BodyContent: bodyContent,
            BodyContentType: bodyContentType,
            Organizer: organizer,
            RequiredAttendees: required ?? Array.Empty<AttendeeDto>(),
            OptionalAttendees: optional ?? Array.Empty<AttendeeDto>(),
            ResourceAttendees: resource ?? Array.Empty<AttendeeDto>(),
            Categories: categories ?? Array.Empty<string>(),
            IsOrganizer: false,
            IsOnlineMeeting: isOnlineMeeting,
            AllowNewTimeProposals: true,
            Sensitivity: sensitivity,
            Start: null,
            StartTimeZone: null,
            End: null,
            EndTimeZone: null,
            LastModifiedDateTime: null,
            Type: type
        );

    [TestMethod]
    public void Normalize_MeetingMessageWithFullEvent_PopulatesContext()
    {
        // Arrange
        var message = Message(
            subject: "Mail subject",
            from: new AttendeeDto("From", "From@Contoso.com"),
            sender: new AttendeeDto("Sender", "Sender@Contoso.com")
        );
        var meetingEvent = Event(
            subject: "Project Sync",
            organizer: new AttendeeDto("Org", "Organizer@Contoso.com"),
            required: new[] { new AttendeeDto("R", "Req@Contoso.com") },
            categories: new[] { "Executive" },
            isOnlineMeeting: true
        );

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, message, meetingEvent);

        // Assert
        context.Subject.Should().Be("Project Sync");
        context.Organizer.Should().Be("organizer@contoso.com");
        context.MessageSender.Should().Be("sender@contoso.com");
        context.MessageFrom.Should().Be("from@contoso.com");
        context.RequiredAttendees.Should().ContainSingle().Which.Should().Be("req@contoso.com");
        context.Categories.Should().ContainSingle().Which.Should().Be("Executive");
        context.IsMeetingMessage.Should().BeTrue();
        context.IsOnlineMeeting.Should().BeTrue();
        context.EventId.Should().Be("evt-1");
    }

    [TestMethod]
    public void Normalize_NoEvent_FallsBackToMessageSubjectAndBody()
    {
        // Arrange
        var message = Message(
            subject: "Ordinary mail",
            bodyContent: "Plain body",
            meetingMessageType: null
        );

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, message, meetingEvent: null);

        // Assert
        context.Subject.Should().Be("Ordinary mail");
        context.BodyText.Should().Be("Plain body");
        context.IsMeetingMessage.Should().BeFalse();
        context.EventId.Should().BeNull();
        context.AllAttendees.Should().BeEmpty();
    }

    [TestMethod]
    public void Normalize_HtmlEventBody_IsStripped()
    {
        // Arrange
        var meetingEvent = Event(
            bodyContent: "<style>.x{}</style><p>Hello&nbsp;&amp;&nbsp;welcome</p>",
            bodyContentType: "HTML"
        );

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.BodyText.Should().Be("Hello & welcome");
    }

    [TestMethod]
    public void Normalize_BodyPreviewUsed_WhenEventBodyAbsent()
    {
        // Arrange
        var message = Message(bodyPreview: "Preview text", bodyContent: "Ignored full body");
        var meetingEvent = Event(bodyContent: null, bodyContentType: null);

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, message, meetingEvent);

        // Assert
        context.BodyText.Should().Be("Preview text");
    }

    [TestMethod]
    public void Normalize_Emails_AreTrimmedAndLowercased()
    {
        // Arrange
        var meetingEvent = Event(organizer: new AttendeeDto("Org", "  Mixed.Case@Contoso.COM  "));

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.Organizer.Should().Be("mixed.case@contoso.com");
    }

    [TestMethod]
    public void Normalize_PartitionsAttendees_IntoRequiredOptionalResource()
    {
        // Arrange
        var meetingEvent = Event(
            required: new[] { new AttendeeDto("R", "r@contoso.com") },
            optional: new[] { new AttendeeDto("O", "o@contoso.com") },
            resource: new[] { new AttendeeDto("Room", "room@contoso.com") }
        );

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.RequiredAttendees.Should().Equal("r@contoso.com");
        context.OptionalAttendees.Should().Equal("o@contoso.com");
        context.ResourceAttendees.Should().Equal("room@contoso.com");
        context.AllAttendees.Should().Equal("r@contoso.com", "o@contoso.com", "room@contoso.com");
    }

    [TestMethod]
    public void Normalize_EmptyAttendeeArrays_ProduceEmptyLists()
    {
        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), Event());

        // Assert
        context.RequiredAttendees.Should().BeEmpty();
        context.OptionalAttendees.Should().BeEmpty();
        context.ResourceAttendees.Should().BeEmpty();
        context.AllAttendees.Should().BeEmpty();
    }

    [TestMethod]
    public void Normalize_SkipsAttendeesWithEmptyEmail()
    {
        // Arrange
        var meetingEvent = Event(
            required: new[]
            {
                new AttendeeDto("Valid", "valid@contoso.com"),
                new AttendeeDto("Empty", "   "),
            }
        );

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.RequiredAttendees.Should().Equal("valid@contoso.com");
    }

    [TestMethod]
    public void Normalize_PrivateSensitivity_IsPreservedLowercase()
    {
        // Arrange
        var meetingEvent = Event(sensitivity: "Private");

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.Sensitivity.Should().Be("private");
    }

    [TestMethod]
    public void Normalize_NullSensitivity_DefaultsToNormal()
    {
        // Arrange
        var meetingEvent = Event(sensitivity: null);

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.Sensitivity.Should().Be("normal");
    }

    [TestMethod]
    public void Normalize_RecurringFromSeriesMasterId_IsTrue()
    {
        // Arrange
        var meetingEvent = Event(seriesMasterId: "series-1");

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.IsRecurring.Should().BeTrue();
    }

    [TestMethod]
    public void Normalize_RecurringFromSeriesMasterType_IsTrue()
    {
        // Arrange
        var meetingEvent = Event(type: "seriesMaster");

        // Act
        var context = MeetingContextNormalizer.Normalize(MailboxUpn, Message(), meetingEvent);

        // Assert
        context.IsRecurring.Should().BeTrue();
    }

    [TestMethod]
    public void Normalize_NullMailboxUpn_Throws()
    {
        // Act
        var act = () => MeetingContextNormalizer.Normalize(null!, Message(), Event());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Normalize_NullMessage_Throws()
    {
        // Act
        var act = () => MeetingContextNormalizer.Normalize(MailboxUpn, null!, Event());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
