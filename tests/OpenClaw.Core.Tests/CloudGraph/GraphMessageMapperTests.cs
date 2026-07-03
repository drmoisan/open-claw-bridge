using System;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Recorded-payload mapping tests for <see cref="GraphMessageMapper"/>: every field
/// row of the spec MessageDto table (parity minimum set plus Sensitivity, Unread,
/// HasAttachments, ItemKind), the full <c>meetingMessageType</c> vocabulary,
/// missing-optional-field defaults from the sparse fixture, and the missing-<c>id</c>
/// fail-fast behavior.
/// </summary>
[TestClass]
public sealed class GraphMessageMapperTests
{
    private static GraphMessage Deserialize(string json) =>
        JsonSerializer.Deserialize<GraphMessage>(json, GraphRequestExecutor.JsonOptions)!;

    [TestMethod]
    public void Map_FullMessage_PopulatesEveryFieldRowOfTheSpecTable()
    {
        var dto = GraphMessageMapper.Map(Deserialize(GraphPayloadFixtures.MessageFull));

        dto.BridgeId.Should().Be("msg-001");
        dto.ItemKind.Should().Be("mail", "no @odata.type discriminator means plain mail");
        dto.Subject.Should().Be("Quarterly budget review");
        dto.ReceivedUtc.Should().Be(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        dto.SentUtc.Should().Be(new DateTimeOffset(2026, 7, 1, 11, 59, 0, TimeSpan.Zero));
        dto.Importance.Should().Be(2, "high maps to 2");
        dto.Sensitivity.Should().Be(3, "confidential maps to 3");
        dto.Unread.Should().BeTrue("isRead false means unread");
        dto.HasAttachments.Should().BeTrue();
        dto.MessageClass.Should().BeNull("Graph has no message-class analog");
        dto.SenderName.Should().Be("Sally Sender");
        dto.SenderEmail.Should().Be("sally@contoso.com");
        dto.ToJson.Should()
            .Be(
                """[{"name":"Alice A","email":"alice@contoso.com"},{"name":"Bob B","email":"bob@contoso.com"}]"""
            );
        dto.CcJson.Should().Be("""[{"name":"Carol C","email":"carol@contoso.com"}]""");
        dto.BodyPreview.Should().Be("Please review the attached figures");
        dto.ProtectedFieldsAvailable.Should().BeTrue("app-only Graph reads full fields");
        dto.IsRedacted.Should().BeFalse("there is no COM redaction path");
        dto.SenderEmailResolved.Should().Be("sally@contoso.com");
        dto.FromEmailAddress.Should().Be("frank@contoso.com");
        dto.ConversationId.Should().Be("conv-1");
        dto.MeetingMessageType.Should().BeNull("a plain mail message has no meeting type");
    }

    [TestMethod]
    public void Map_EventMessage_SetsMeetingItemKindAndMeetingMessageType()
    {
        var dto = GraphMessageMapper.Map(Deserialize(GraphPayloadFixtures.MeetingRequestMessage));

        dto.BridgeId.Should().Be("msg-mtg-001");
        dto.ItemKind.Should().Be("meeting", "@odata.type eventMessage classifies as meeting");
        dto.MeetingMessageType.Should().Be(0, "meetingRequest maps to 0");
        dto.Unread.Should().BeFalse("isRead true means read");
        dto.Importance.Should().Be(1, "normal maps to 1");
        dto.Sensitivity.Should().Be(0, "normal maps to 0");
        dto.CcJson.Should().BeNull("an empty recipient list maps to null deterministically");
    }

    [DataTestMethod]
    [DataRow("meetingRequest", 0)]
    [DataRow("meetingCancelled", 1)]
    [DataRow("meetingDeclined", 2)]
    [DataRow("meetingAccepted", 3)]
    [DataRow("meetingTentativelyAccepted", 4)]
    [DataRow("none", null)]
    [DataRow(null, null, DisplayName = "absent meetingMessageType")]
    [DataRow("somethingUnknown", null, DisplayName = "unknown meetingMessageType")]
    public void MapMeetingMessageType_CoversTheFullVocabulary(string? wire, int? expected)
    {
        GraphMessageMapper.MapMeetingMessageType(wire).Should().Be(expected);
    }

    [DataTestMethod]
    [DataRow("low", 0)]
    [DataRow("normal", 1)]
    [DataRow("high", 2)]
    [DataRow(null, null)]
    [DataRow("unknown", null)]
    public void MapImportance_CoversTheVocabulary(string? wire, int? expected)
    {
        GraphMessageMapper.MapImportance(wire).Should().Be(expected);
    }

    [DataTestMethod]
    [DataRow("normal", 0)]
    [DataRow("personal", 1)]
    [DataRow("private", 2)]
    [DataRow("confidential", 3)]
    [DataRow(null, null)]
    [DataRow("unknown", null)]
    public void MapSensitivity_CoversTheVocabulary(string? wire, int? expected)
    {
        GraphMessageMapper.MapSensitivity(wire).Should().Be(expected);
    }

    [TestMethod]
    public void Map_SparseMessage_DefaultsEveryOptionalFieldDeterministically()
    {
        var dto = GraphMessageMapper.Map(Deserialize(GraphPayloadFixtures.MessageSparse));

        dto.BridgeId.Should().Be("msg-sparse-001");
        dto.ItemKind.Should().Be("mail");
        dto.Subject.Should().BeNull();
        dto.ReceivedUtc.Should().BeNull();
        dto.SentUtc.Should().BeNull();
        dto.Importance.Should().BeNull();
        dto.Sensitivity.Should().BeNull();
        dto.Unread.Should().BeTrue("an absent isRead is treated as not yet read");
        dto.HasAttachments.Should().BeFalse();
        dto.MessageClass.Should().BeNull();
        dto.SenderName.Should().BeNull();
        dto.SenderEmail.Should().BeNull();
        dto.ToJson.Should().BeNull();
        dto.CcJson.Should().BeNull();
        dto.BodyPreview.Should().BeNull();
        dto.ProtectedFieldsAvailable.Should().BeTrue();
        dto.IsRedacted.Should().BeFalse();
        dto.SenderEmailResolved.Should().BeNull();
        dto.FromEmailAddress.Should().BeNull();
        dto.ConversationId.Should().BeNull();
        dto.MeetingMessageType.Should().BeNull();
    }

    [DataTestMethod]
    [DataRow("""{ "subject": "no id" }""", DisplayName = "id absent")]
    [DataRow("""{ "id": "   ", "subject": "blank id" }""", DisplayName = "id whitespace")]
    public void Map_MissingRequiredId_FailsFast(string json)
    {
        var act = () => GraphMessageMapper.Map(Deserialize(json));

        act.Should()
            .Throw<GraphMappingException>()
            .WithMessage("*missing the required field 'id'*");
    }

    [TestMethod]
    public void Map_NullMessage_Throws()
    {
        var act = () => GraphMessageMapper.Map(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
