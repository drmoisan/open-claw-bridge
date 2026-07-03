using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.Core.CloudGraph;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// CsCheck property tests for the pure <see cref="GraphMessageMapper"/> functions:
/// the importance/sensitivity/meetingMessageType enum maps round-trip against
/// <see cref="SchedulingDtoMapper"/>'s inverse maps, and generated recipient lists
/// serialized to OR-5 JSON survive the <c>SchedulingDtoMapper.ParseAttendees</c>
/// round-trip. Failing seeds are reported by CsCheck's default output.
/// </summary>
[TestClass]
public sealed class GraphMessageMapperPropertyTests
{
    private static readonly string[] ImportanceVocabulary = ["low", "normal", "high"];

    private static readonly string[] SensitivityVocabulary =
    [
        "normal",
        "personal",
        "private",
        "confidential",
    ];

    private static readonly string[] MeetingMessageTypeVocabulary =
    [
        "meetingRequest",
        "meetingCancelled",
        "meetingDeclined",
        "meetingAccepted",
        "meetingTentativelyAccepted",
    ];

    private static GraphMessage WireMessage(
        string? importance = null,
        string? meetingMessageType = null,
        IReadOnlyList<GraphRecipient>? toRecipients = null
    ) =>
        new(
            ODataType: null,
            Id: "msg-prop",
            Subject: null,
            BodyPreview: null,
            ReceivedDateTime: null,
            SentDateTime: null,
            Importance: importance,
            Sensitivity: null,
            IsRead: null,
            HasAttachments: null,
            ConversationId: null,
            From: null,
            Sender: null,
            ToRecipients: toRecipients,
            CcRecipients: null,
            MeetingMessageType: meetingMessageType
        );

    [TestMethod]
    public void ImportanceMap_RoundTripsAgainstSchedulingDtoMapperInverse()
    {
        var mapper = new SchedulingDtoMapper();

        Gen.Int[0, ImportanceVocabulary.Length - 1]
            .Sample(index =>
            {
                var wire = ImportanceVocabulary[index];

                var dto = GraphMessageMapper.Map(WireMessage(importance: wire));
                var agent = mapper.MapMessage(dto);

                agent.Importance.Should().Be(wire, "the Graph string survives the int round-trip");
            });
    }

    [TestMethod]
    public void MeetingMessageTypeMap_RoundTripsAgainstSchedulingDtoMapperInverse()
    {
        var mapper = new SchedulingDtoMapper();

        Gen.Int[0, MeetingMessageTypeVocabulary.Length - 1]
            .Sample(index =>
            {
                var wire = MeetingMessageTypeVocabulary[index];

                var dto = GraphMessageMapper.Map(WireMessage(meetingMessageType: wire));
                var agent = mapper.MapMessage(dto);

                agent
                    .MeetingMessageType.Should()
                    .Be(wire, "the Graph string survives the int round-trip");
            });
    }

    [TestMethod]
    public void SensitivityMap_RoundTripsAgainstSchedulingDtoMapperInverse()
    {
        var mapper = new SchedulingDtoMapper();
        var start = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);

        Gen.Int[0, SensitivityVocabulary.Length - 1]
            .Sample(index =>
            {
                var wire = SensitivityVocabulary[index];

                var evt = new EventDto(
                    BridgeId: "evt-prop",
                    GlobalAppointmentId: null,
                    Subject: null,
                    StartUtc: start,
                    EndUtc: start.AddHours(1),
                    Location: null,
                    BusyStatus: null,
                    MeetingStatus: null,
                    IsRecurring: false,
                    Sensitivity: GraphMessageMapper.MapSensitivity(wire),
                    Organizer: null,
                    RequiredAttendeesJson: null,
                    OptionalAttendeesJson: null,
                    ResourcesJson: null,
                    BodyPreview: null,
                    ProtectedFieldsAvailable: true,
                    IsRedacted: false
                );

                mapper
                    .MapEvent(evt)
                    .Sensitivity.Should()
                    .Be(wire, "the Graph string survives the int round-trip");
            });
    }

    [TestMethod]
    public void RecipientOr5Json_SurvivesParseAttendeesRoundTrip()
    {
        var mapper = new SchedulingDtoMapper();
        var recipientGen = Gen.Select(Gen.Int[0, 999], Gen.Int[0, 999])
            .Select(t => new GraphRecipient(
                new GraphEmailAddress($"Person {t.Item1}", $"user{t.Item2}@contoso.com")
            ));

        recipientGen
            .List[1, 5]
            .Sample(recipients =>
            {
                var dto = GraphMessageMapper.Map(WireMessage(toRecipients: recipients));
                var agent = mapper.MapMessage(dto);

                var expected = recipients
                    .Select(r => new AttendeeDto(r.EmailAddress!.Name!, r.EmailAddress.Address!))
                    .ToList();
                agent
                    .ToRecipients.Should()
                    .Equal(expected, "OR-5 JSON preserves order, names, and emails");
            });
    }
}
