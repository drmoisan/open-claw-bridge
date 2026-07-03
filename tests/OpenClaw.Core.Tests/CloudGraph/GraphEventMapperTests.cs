using System;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Recorded-payload mapping tests for <see cref="GraphEventMapper"/>: every field row
/// of the spec EventDto table (parity minimum set including sensitivity
/// <c>private</c> -> 2, <c>iCalUId</c>/<c>seriesMasterId</c>, attendee-type
/// partitioning including <c>resource</c>, categories, boolean flags,
/// <c>lastModifiedDateTime</c>, <c>BodyFull</c>), <c>IsRecurring</c> for
/// <c>occurrence</c> vs <c>singleInstance</c>, and the missing
/// <c>id</c>/<c>start</c>/<c>end</c> fail-fast behavior.
/// </summary>
[TestClass]
public sealed class GraphEventMapperTests
{
    private static GraphEvent Deserialize(string json) =>
        JsonSerializer.Deserialize<GraphEvent>(json, GraphRequestExecutor.JsonOptions)!;

    [TestMethod]
    public void Map_PrivateOccurrence_PopulatesEveryFieldRowOfTheSpecTable()
    {
        var dto = GraphEventMapper.Map(Deserialize(GraphPayloadFixtures.EventPrivateOccurrence));

        dto.BridgeId.Should().Be("evt-001");
        dto.GlobalAppointmentId.Should().BeNull("COM-specific; iCalUId is the portable identity");
        dto.Subject.Should().Be("Private 1:1");
        dto.StartUtc.Should().Be(new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero));
        dto.EndUtc.Should().Be(new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero));
        dto.Location.Should().Be("Room 4");
        dto.BusyStatus.Should().Be(2, "busy maps to 2");
        dto.MeetingStatus.Should().BeNull("no direct Graph analog");
        dto.IsRecurring.Should().BeTrue("type occurrence is recurring");
        dto.Sensitivity.Should().Be(2, "private maps to 2 (the private-meeting-rule signal)");
        dto.Organizer.Should().Be("olive@contoso.com");
        dto.RequiredAttendeesJson.Should()
            .Be("""[{"name":"Alice A","email":"alice@contoso.com"}]""");
        dto.OptionalAttendeesJson.Should().Be("""[{"name":"Bob B","email":"bob@contoso.com"}]""");
        dto.ResourcesJson.Should().Be("""[{"name":"Room 4","email":"room4@contoso.com"}]""");
        dto.BodyPreview.Should().Be("Weekly private sync");
        dto.ProtectedFieldsAvailable.Should().BeTrue();
        dto.IsRedacted.Should().BeFalse();
        dto.ResponseStatus.Should().Be(3, "accepted maps to 3");
        dto.Categories.Should().Equal("Focus", "OneOnOne");
        dto.IsOrganizer.Should().BeTrue();
        dto.IsOnlineMeeting.Should().BeTrue();
        dto.AllowNewTimeProposals.Should().BeTrue();
        dto.ICalUId.Should().Be("ical-001");
        dto.SeriesMasterId.Should().Be("master-001");
        dto.LastModifiedDateTime.Should()
            .Be(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));
        dto.BodyFull.Should().Be("Full agenda text", "body.content is text via the Prefer header");
        dto.SensitivityLabel.Should().BeNull("Purview label names are not on the v1.0 event");
    }

    [TestMethod]
    public void Map_SingleInstance_IsNotRecurring()
    {
        var dto = GraphEventMapper.Map(Deserialize(GraphPayloadFixtures.EventSingleInstance));

        dto.BridgeId.Should().Be("evt-002");
        dto.IsRecurring.Should().BeFalse("type singleInstance is not recurring");
        dto.SeriesMasterId.Should().BeNull();
        dto.BusyStatus.Should().Be(1, "tentative maps to 1");
        dto.ResponseStatus.Should().Be(5, "notResponded maps to 5");
        dto.Sensitivity.Should().Be(0, "normal maps to 0");
        dto.IsOrganizer.Should().BeFalse();
        dto.OptionalAttendeesJson.Should().BeNull("an empty partition maps to null");
        dto.ResourcesJson.Should().BeNull("an empty partition maps to null");
    }

    [DataTestMethod]
    [DataRow("free", 0)]
    [DataRow("tentative", 1)]
    [DataRow("busy", 2)]
    [DataRow("oof", 3)]
    [DataRow("workingElsewhere", 4)]
    [DataRow(null, null)]
    [DataRow("unknown", null)]
    public void MapShowAs_CoversTheVocabulary(string? wire, int? expected)
    {
        GraphEventMapper.MapShowAs(wire).Should().Be(expected);
    }

    [DataTestMethod]
    [DataRow("none", 0)]
    [DataRow("organizer", 1)]
    [DataRow("tentativelyAccepted", 2)]
    [DataRow("accepted", 3)]
    [DataRow("declined", 4)]
    [DataRow("notResponded", 5)]
    [DataRow(null, null)]
    [DataRow("unknown", null)]
    public void MapResponseStatus_CoversTheVocabulary(string? wire, int? expected)
    {
        GraphEventMapper.MapResponseStatus(wire).Should().Be(expected);
    }

    [DataTestMethod]
    [DataRow("occurrence", true)]
    [DataRow("exception", true)]
    [DataRow("seriesMaster", true)]
    [DataRow("singleInstance", false)]
    [DataRow(null, false)]
    public void IsRecurringType_MatchesTheSpecRule(string? type, bool expected)
    {
        GraphEventMapper.IsRecurringType(type).Should().Be(expected);
    }

    [TestMethod]
    public void ToUtc_NonUtcZone_ConvertsThroughTimeZoneInfo()
    {
        // 2026-07-06 is in Pacific daylight time (UTC-7).
        var value = new GraphDateTimeTimeZone("2026-07-06T10:00:00", "Pacific Standard Time");

        var utc = GraphEventMapper.ToUtc(value, "start");

        utc.Should().Be(new DateTimeOffset(2026, 7, 6, 17, 0, 0, TimeSpan.Zero));
    }

    [TestMethod]
    public void ToUtc_UnknownZone_FailsFast()
    {
        var value = new GraphDateTimeTimeZone("2026-07-06T10:00:00", "Not/AZone");

        var act = () => GraphEventMapper.ToUtc(value, "start");

        act.Should().Throw<GraphMappingException>().WithMessage("*unknown time zone*");
    }

    [TestMethod]
    public void ToUtc_UnparseableDateTime_FailsFast()
    {
        var value = new GraphDateTimeTimeZone("not-a-date", "UTC");

        var act = () => GraphEventMapper.ToUtc(value, "end");

        act.Should().Throw<GraphMappingException>().WithMessage("*unparseable dateTime*");
    }

    [DataTestMethod]
    [DataRow(
        """{ "start": { "dateTime": "2026-07-06T10:00:00", "timeZone": "UTC" }, "end": { "dateTime": "2026-07-06T11:00:00", "timeZone": "UTC" } }""",
        "id",
        DisplayName = "missing id"
    )]
    [DataRow(
        """{ "id": "evt-x", "end": { "dateTime": "2026-07-06T11:00:00", "timeZone": "UTC" } }""",
        "start",
        DisplayName = "missing start"
    )]
    [DataRow(
        """{ "id": "evt-x", "start": { "dateTime": "2026-07-06T10:00:00", "timeZone": "UTC" } }""",
        "end",
        DisplayName = "missing end"
    )]
    public void Map_MissingRequiredField_FailsFast(string json, string fieldName)
    {
        var act = () => GraphEventMapper.Map(Deserialize(json));

        act.Should().Throw<GraphMappingException>().WithMessage($"*required field '{fieldName}'*");
    }

    [TestMethod]
    public void Map_NullEvent_Throws()
    {
        var act = () => GraphEventMapper.Map(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
