using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// CsCheck property tests for the pure <see cref="GraphEventMapper"/> functions: the
/// <c>showAs</c> and <c>responseStatus</c> maps are injective over their vocabularies
/// (test-local inverse tables recover the original string), the sensitivity map
/// round-trips against <see cref="SchedulingDtoMapper"/>'s inverse through the full
/// event pipeline, and generated attendee lists partitioned by type produce OR-5 JSON
/// that survives the <c>ParseAttendees</c> round-trip with no cross-partition leakage.
/// </summary>
[TestClass]
public sealed class GraphEventMapperPropertyTests
{
    // Ordered so that the array index equals the mapped integer.
    private static readonly string[] ShowAsVocabulary =
    [
        "free",
        "tentative",
        "busy",
        "oof",
        "workingElsewhere",
    ];

    private static readonly string[] ResponseStatusVocabulary =
    [
        "none",
        "organizer",
        "tentativelyAccepted",
        "accepted",
        "declined",
        "notResponded",
    ];

    private static readonly string[] SensitivityVocabulary =
    [
        "normal",
        "personal",
        "private",
        "confidential",
    ];

    private static readonly string[] AttendeeTypes = ["required", "optional", "resource"];

    private static GraphEvent WireEvent(
        string? sensitivity = null,
        IReadOnlyList<GraphAttendee>? attendees = null
    ) =>
        new(
            Id: "evt-prop",
            ICalUId: null,
            SeriesMasterId: null,
            Subject: null,
            BodyPreview: null,
            Body: null,
            Organizer: null,
            Attendees: attendees,
            Categories: null,
            IsOrganizer: null,
            IsOnlineMeeting: null,
            AllowNewTimeProposals: null,
            Sensitivity: sensitivity,
            ShowAs: null,
            ResponseStatus: null,
            Location: null,
            Start: new GraphDateTimeTimeZone("2026-07-06T10:00:00", "UTC"),
            End: new GraphDateTimeTimeZone("2026-07-06T11:00:00", "UTC"),
            Type: null,
            LastModifiedDateTime: null
        );

    [TestMethod]
    public void ShowAsMap_IsRecoveredByItsInverseTable()
    {
        Gen.Int[0, ShowAsVocabulary.Length - 1]
            .Sample(index =>
            {
                var wire = ShowAsVocabulary[index];

                var mapped = GraphEventMapper.MapShowAs(wire);

                mapped.Should().Be(index, "the vocabulary array is ordered by mapped value");
                ShowAsVocabulary[mapped!.Value].Should().Be(wire);
            });
    }

    [TestMethod]
    public void ResponseStatusMap_IsRecoveredByItsInverseTable()
    {
        Gen.Int[0, ResponseStatusVocabulary.Length - 1]
            .Sample(index =>
            {
                var wire = ResponseStatusVocabulary[index];

                var mapped = GraphEventMapper.MapResponseStatus(wire);

                mapped.Should().Be(index, "the vocabulary array is ordered by mapped value");
                ResponseStatusVocabulary[mapped!.Value].Should().Be(wire);
            });
    }

    [TestMethod]
    public void SensitivityMap_RoundTripsThroughTheFullEventPipeline()
    {
        var mapper = new SchedulingDtoMapper();

        Gen.Int[0, SensitivityVocabulary.Length - 1]
            .Sample(index =>
            {
                var wire = SensitivityVocabulary[index];

                var dto = GraphEventMapper.Map(WireEvent(sensitivity: wire));
                var agent = mapper.MapEvent(dto);

                agent.Sensitivity.Should().Be(wire, "the Graph string survives the int round-trip");
            });
    }

    [TestMethod]
    public void AttendeePartitioning_SurvivesParseAttendeesWithNoCrossPartitionLeakage()
    {
        var mapper = new SchedulingDtoMapper();
        var attendeeGen = Gen.Select(Gen.Int[0, 2], Gen.Int[0, 999], Gen.Int[0, 999])
            .Select(t => new GraphAttendee(
                AttendeeTypes[t.Item1],
                new GraphEmailAddress($"Person {t.Item2}", $"user{t.Item3}@contoso.com")
            ));

        attendeeGen
            .List[1, 8]
            .Sample(attendees =>
            {
                var dto = GraphEventMapper.Map(WireEvent(attendees: attendees));
                var agent = mapper.MapEvent(dto);

                ExpectedPartition(attendees, "required")
                    .Should()
                    .Equal(agent.RequiredAttendees, "required attendees survive in order");
                ExpectedPartition(attendees, "optional")
                    .Should()
                    .Equal(agent.OptionalAttendees, "optional attendees survive in order");
                ExpectedPartition(attendees, "resource")
                    .Should()
                    .Equal(agent.ResourceAttendees, "resource attendees survive in order");

                var total =
                    agent.RequiredAttendees.Count
                    + agent.OptionalAttendees.Count
                    + agent.ResourceAttendees.Count;
                total.Should().Be(attendees.Count, "every attendee lands in exactly one partition");
            });
    }

    private static List<AttendeeDto> ExpectedPartition(
        IReadOnlyList<GraphAttendee> attendees,
        string type
    ) =>
        attendees
            .Where(a => a.Type == type)
            .Select(a => new AttendeeDto(a.EmailAddress!.Name!, a.EmailAddress.Address!))
            .ToList();
}
