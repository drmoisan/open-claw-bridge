using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for the bridge-to-agent DTO mapper (OR-4, AC-9, AC-13).
/// </summary>
[TestClass]
public sealed class SchedulingDtoMapperTests
{
    private readonly SchedulingDtoMapper mapper = new();

    private static MessageDto Message(
        string itemKind = "meeting",
        string? toJson = null,
        string? ccJson = null,
        int? importance = null,
        int? sensitivity = null
    ) =>
        new(
            BridgeId: "msg-1",
            ItemKind: itemKind,
            Subject: "Project sync",
            ReceivedUtc: new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
            SentUtc: new DateTimeOffset(2026, 6, 9, 9, 0, 0, TimeSpan.Zero),
            Importance: importance,
            Sensitivity: sensitivity,
            Unread: true,
            HasAttachments: false,
            MessageClass: "IPM.Schedule.Meeting.Request",
            SenderName: "Sender",
            SenderEmail: "sender@contoso.com",
            ToJson: toJson,
            CcJson: ccJson,
            BodyPreview: "Let us meet",
            ProtectedFieldsAvailable: true,
            IsRedacted: false
        );

    private static EventDto Event(
        string? required = null,
        string? optional = null,
        string? resources = null,
        int? sensitivity = null,
        bool isRecurring = false
    ) =>
        new(
            BridgeId: "evt-1",
            GlobalAppointmentId: "global-1",
            Subject: "Board review",
            StartUtc: new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero),
            EndUtc: new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            Location: "Room A",
            BusyStatus: 2,
            MeetingStatus: 1,
            IsRecurring: isRecurring,
            Sensitivity: sensitivity,
            Organizer: "organizer@contoso.com",
            RequiredAttendeesJson: required,
            OptionalAttendeesJson: optional,
            ResourcesJson: resources,
            BodyPreview: "Agenda",
            ProtectedFieldsAvailable: true,
            IsRedacted: false
        );

    [TestMethod]
    public void MapMessage_MapsCoreFields()
    {
        var result = mapper.MapMessage(Message());

        result.Id.Should().Be("msg-1");
        result.Subject.Should().Be("Project sync");
        result.BodyPreview.Should().Be("Let us meet");
        result.Sender!.Email.Should().Be("sender@contoso.com");
        result.From!.Email.Should().Be("sender@contoso.com");
        result.MeetingMessageType.Should().Be("meetingRequest");
    }

    [TestMethod]
    public void MapMessage_NonMeetingKind_HasNullMeetingType()
    {
        var result = mapper.MapMessage(Message(itemKind: "mail"));

        result.MeetingMessageType.Should().BeNull();
    }

    [TestMethod]
    public void MapMessage_ParsesToAndCcAttendeeJson()
    {
        var result = mapper.MapMessage(
            Message(
                toJson: """[{"name":"Owner","email":"owner@contoso.com"}]""",
                ccJson: """[{"name":"Cc","email":"cc@contoso.com"}]"""
            )
        );

        result.ToRecipients.Should().ContainSingle();
        result.ToRecipients[0].Email.Should().Be("owner@contoso.com");
        result.CcRecipients[0].Name.Should().Be("Cc");
    }

    [TestMethod]
    public void MapMessage_DeferredFields_AreNull()
    {
        var result = mapper.MapMessage(Message());

        // Fields deferred to #71-#76.
        result.ConversationId.Should().BeNull();
        result.BodyContent.Should().BeNull();
        result.BodyContentType.Should().BeNull();
    }

    [TestMethod]
    public void MapMessage_MapsImportance()
    {
        mapper.MapMessage(Message(importance: 2)).Importance.Should().Be("high");
        mapper.MapMessage(Message(importance: 1)).Importance.Should().Be("normal");
        mapper.MapMessage(Message(importance: null)).Importance.Should().BeNull();
    }

    [TestMethod]
    public void MapEvent_MapsCoreFields()
    {
        var result = mapper.MapEvent(Event(isRecurring: true));

        result.Id.Should().Be("evt-1");
        result.Subject.Should().Be("Board review");
        result.Organizer!.Email.Should().Be("organizer@contoso.com");
        result.Start.Should().Be(new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero));
        result.Type.Should().Be("seriesMaster");
    }

    [TestMethod]
    public void MapEvent_ParsesRequiredOptionalResourceAttendees()
    {
        var result = mapper.MapEvent(
            Event(
                required: """[{"name":"R","email":"r@contoso.com"}]""",
                optional: """[{"name":"O","email":"o@contoso.com"}]""",
                resources: """[{"name":"Room","email":"room@contoso.com"}]"""
            )
        );

        result.RequiredAttendees.Single().Email.Should().Be("r@contoso.com");
        result.OptionalAttendees.Single().Email.Should().Be("o@contoso.com");
        result.ResourceAttendees.Single().Email.Should().Be("room@contoso.com");
    }

    [TestMethod]
    public void MapEvent_NullAttendeeJson_ProducesEmptyLists()
    {
        var result = mapper.MapEvent(Event());

        result.RequiredAttendees.Should().BeEmpty();
        result.OptionalAttendees.Should().BeEmpty();
        result.ResourceAttendees.Should().BeEmpty();
    }

    [TestMethod]
    public void MapEvent_MalformedAttendeeJson_ProducesEmptyList()
    {
        var result = mapper.MapEvent(Event(required: "not-json"));

        result.RequiredAttendees.Should().BeEmpty();
    }

    [TestMethod]
    public void MapEvent_MapsSensitivity()
    {
        mapper.MapEvent(Event(sensitivity: 2)).Sensitivity.Should().Be("private");
        mapper.MapEvent(Event(sensitivity: 0)).Sensitivity.Should().Be("normal");
        mapper.MapEvent(Event(sensitivity: null)).Sensitivity.Should().BeNull();
    }

    [TestMethod]
    public void MapEvent_DeferredFields_AreNullOrEmpty()
    {
        var result = mapper.MapEvent(Event());

        result.SeriesMasterId.Should().BeNull();
        result.Categories.Should().BeEmpty();
        result.IsOnlineMeeting.Should().BeFalse();
        result.AllowNewTimeProposals.Should().BeFalse();
        result.LastModifiedDateTime.Should().BeNull();
    }

    [TestMethod]
    public void MapMessage_Null_Throws()
    {
        var act = () => mapper.MapMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void MapEvent_Null_Throws()
    {
        var act = () => mapper.MapEvent(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
