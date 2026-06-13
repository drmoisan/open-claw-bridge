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
        int? sensitivity = null,
        string? senderEmailResolved = "sender@contoso.com",
        string? fromEmailAddress = "sender@contoso.com",
        string? conversationId = null,
        int? meetingMessageType = 0
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
            IsRedacted: false,
            SenderEmailResolved: senderEmailResolved,
            FromEmailAddress: fromEmailAddress,
            ConversationId: conversationId,
            MeetingMessageType: meetingMessageType
        );

    private static EventDto Event(
        string? required = null,
        string? optional = null,
        string? resources = null,
        int? sensitivity = null,
        bool isRecurring = false,
        string[]? categories = null,
        bool isOrganizer = false,
        bool isOnlineMeeting = false,
        bool allowNewTimeProposals = false,
        string? iCalUId = "global-1",
        string? seriesMasterId = null,
        DateTimeOffset? lastModifiedDateTime = null
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
            IsRedacted: false,
            ResponseStatus: null,
            Categories: categories,
            IsOrganizer: isOrganizer,
            IsOnlineMeeting: isOnlineMeeting,
            AllowNewTimeProposals: allowNewTimeProposals,
            ICalUId: iCalUId,
            SeriesMasterId: seriesMasterId,
            LastModifiedDateTime: lastModifiedDateTime
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
    public void MapMessage_UsesResolvedSenderForSenderAndFromForFrom()
    {
        // Sender reflects SenderEmailResolved; From reflects FromEmailAddress (D-A): distinct values.
        var result = mapper.MapMessage(
            Message(
                senderEmailResolved: "resolved.sender@contoso.com",
                fromEmailAddress: "delegate.boss@contoso.com"
            )
        );

        result.Sender!.Email.Should().Be("resolved.sender@contoso.com");
        result.From!.Email.Should().Be("delegate.boss@contoso.com");
    }

    [DataTestMethod]
    [DataRow(0, "meetingRequest")]
    [DataRow(1, "meetingCancelled")]
    [DataRow(2, "meetingDeclined")]
    [DataRow(3, "meetingAccepted")]
    [DataRow(4, "meetingTentativelyAccepted")]
    public void MapMessage_MapsMeetingMessageTypeIntToGraphString(
        int olMeetingType,
        string expected
    )
    {
        var result = mapper.MapMessage(Message(meetingMessageType: olMeetingType));

        result.MeetingMessageType.Should().Be(expected);
    }

    [DataTestMethod]
    [DataRow(5)]
    [DataRow(99)]
    [DataRow(-1)]
    public void MapMessage_UnknownMeetingType_MapsToNull(int olMeetingType)
    {
        var result = mapper.MapMessage(Message(meetingMessageType: olMeetingType));

        result.MeetingMessageType.Should().BeNull();
    }

    [TestMethod]
    public void MapMessage_NonMeetingKind_HasNullMeetingType()
    {
        // Ordinary mail carries no OlMeetingType (D-B): MeetingMessageType is null on the DTO.
        var result = mapper.MapMessage(Message(itemKind: "mail", meetingMessageType: null));

        result.MeetingMessageType.Should().BeNull();
    }

    [TestMethod]
    public void MapMessage_FlowsConversationIdFromDto()
    {
        var result = mapper.MapMessage(Message(conversationId: "conv-abc"));

        result.ConversationId.Should().Be("conv-abc");
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
        // ConversationId now flows from the DTO (issue #73); body content remains deferred.
        var result = mapper.MapMessage(Message(conversationId: null));

        result.ConversationId.Should().BeNull("a null DTO ConversationId maps through as null");
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
    public void MapEvent_DefaultGraphFields_MapThroughAsNullOrEmpty()
    {
        // With an EventDto carrying default (unset) graph fields, the mapper passes the defaults
        // through: null seriesMasterId, empty categories, false flags, null last-modified.
        var result = mapper.MapEvent(Event());

        result.SeriesMasterId.Should().BeNull();
        result.Categories.Should().BeEmpty();
        result.IsOnlineMeeting.Should().BeFalse();
        result.AllowNewTimeProposals.Should().BeFalse();
        result.LastModifiedDateTime.Should().BeNull();
    }

    [TestMethod]
    public void MapEvent_PopulatedGraphFields_MapThroughDirectly()
    {
        // #72: the mapper wires the populated EventDto graph fields into the SchedulingEventDto
        // in place of the former hardcoded placeholders.
        var lastModified = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var result = mapper.MapEvent(
            Event(
                categories: ["Customer", "Critical"],
                isOrganizer: true,
                isOnlineMeeting: true,
                allowNewTimeProposals: true,
                seriesMasterId: "series-1",
                lastModifiedDateTime: lastModified
            )
        );

        result.Categories.Should().Equal("Customer", "Critical");
        result.IsOrganizer.Should().BeTrue();
        result.IsOnlineMeeting.Should().BeTrue();
        result.AllowNewTimeProposals.Should().BeTrue();
        result.SeriesMasterId.Should().Be("series-1");
        result.LastModifiedDateTime.Should().Be(lastModified);
    }

    [TestMethod]
    public void MapEvent_NullCategories_MapToEmptyArray()
    {
        var result = mapper.MapEvent(Event(categories: null));

        result.Categories.Should().NotBeNull();
        result.Categories.Should().BeEmpty();
    }

    [TestMethod]
    public void MapEvent_RecurringOnlineMeeting_MapsExpectedGraphFields()
    {
        // AC5: a recurring online meeting occurrence maps to a SchedulingEventDto with non-null
        // ICalUId, IsOnlineMeeting=true, private sensitivity, and the occurrence seriesMasterId.
        var result = mapper.MapEvent(
            Event(
                sensitivity: 2,
                isRecurring: true,
                isOnlineMeeting: true,
                iCalUId: "gid-recurring",
                seriesMasterId: "gid-recurring"
            )
        );

        result.ICalUId.Should().Be("gid-recurring");
        result.IsOnlineMeeting.Should().BeTrue();
        result.Sensitivity.Should().Be("private");
        result.SeriesMasterId.Should().Be("gid-recurring");
        result.Type.Should().Be("seriesMaster");
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
