using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Recorded-payload tests for <see cref="GraphSchedulingMapper"/>: mailboxSettings
/// maps all four DTO fields; getSchedule maps <c>busy</c>/<c>oof</c>/<c>tentative</c>
/// items to intervals and excludes <c>free</c>/<c>workingElsewhere</c> (D11); an
/// empty window yields empty <c>BusyIntervals</c>; plus a CsCheck property asserting
/// an item appears in <c>BusyIntervals</c> iff its status is in
/// {busy, oof, tentative}.
/// </summary>
[TestClass]
public sealed class GraphSchedulingMapperTests
{
    private static readonly string[] AllStatuses =
    [
        "busy",
        "oof",
        "tentative",
        "free",
        "workingElsewhere",
    ];

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, GraphRequestExecutor.JsonOptions)!;

    [TestMethod]
    public void MapMailboxSettings_MapsAllFourDtoFields()
    {
        var wire = Deserialize<GraphMailboxSettings>(GraphPayloadFixtures.MailboxSettings);

        var dto = GraphSchedulingMapper.MapMailboxSettings(wire);

        dto.TimeZoneId.Should().Be("Pacific Standard Time");
        dto.WorkingDays.Should()
            .Equal(
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            );
        dto.WorkingHoursStart.Should().Be(new TimeOnly(8, 0));
        dto.WorkingHoursEnd.Should().Be(new TimeOnly(17, 0));
    }

    [DataTestMethod]
    [DataRow(
        """{ "workingHours": { "daysOfWeek": ["monday"], "startTime": "08:00:00", "endTime": "17:00:00" } }""",
        "timeZone",
        DisplayName = "missing timeZone"
    )]
    [DataRow("""{ "timeZone": "UTC" }""", "workingHours", DisplayName = "missing workingHours")]
    public void MapMailboxSettings_MissingRequiredField_FailsFast(string json, string fieldName)
    {
        var wire = Deserialize<GraphMailboxSettings>(json);

        var act = () => GraphSchedulingMapper.MapMailboxSettings(wire);

        act.Should().Throw<GraphMappingException>().WithMessage($"*required field '{fieldName}'*");
    }

    [TestMethod]
    public void MapMailboxSettings_UnknownDayName_FailsFast()
    {
        var wire = Deserialize<GraphMailboxSettings>(
            """{ "timeZone": "UTC", "workingHours": { "daysOfWeek": ["funday"], "startTime": "08:00:00", "endTime": "17:00:00" } }"""
        );

        var act = () => GraphSchedulingMapper.MapMailboxSettings(wire);

        act.Should().Throw<GraphMappingException>().WithMessage("*unknown day name*");
    }

    [TestMethod]
    public void MapMailboxSettings_UnparseableTime_FailsFast()
    {
        var wire = Deserialize<GraphMailboxSettings>(
            """{ "timeZone": "UTC", "workingHours": { "daysOfWeek": ["monday"], "startTime": "not-a-time", "endTime": "17:00:00" } }"""
        );

        var act = () => GraphSchedulingMapper.MapMailboxSettings(wire);

        act.Should().Throw<GraphMappingException>().WithMessage("*'workingHours.startTime'*");
    }

    [TestMethod]
    public void MapFreeBusy_IncludesBusyOofTentativeAndExcludesFreeWorkingElsewhere()
    {
        var wire = Deserialize<GraphScheduleResponse>(GraphPayloadFixtures.GetScheduleResponse);

        var dto = GraphSchedulingMapper.MapFreeBusy("paula@contoso.com", wire);

        dto.MailboxUpn.Should().Be("paula@contoso.com");
        dto.BusyIntervals.Should()
            .Equal(
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero)
                ),
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 6, 13, 0, 0, TimeSpan.Zero)
                ),
                new BusyIntervalDto(
                    new DateTimeOffset(2026, 7, 6, 14, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 6, 14, 30, 0, TimeSpan.Zero)
                )
            );
    }

    [TestMethod]
    public void MapFreeBusy_EmptyScheduleItems_YieldsEmptyBusyIntervals()
    {
        var wire = Deserialize<GraphScheduleResponse>(
            GraphPayloadFixtures.GetScheduleEmptyResponse
        );

        var dto = GraphSchedulingMapper.MapFreeBusy("paula@contoso.com", wire);

        dto.BusyIntervals.Should().BeEmpty("an empty window is an empty list, not an error");
    }

    [TestMethod]
    public void MapFreeBusy_EmptyValueArray_YieldsEmptyBusyIntervals()
    {
        var wire = Deserialize<GraphScheduleResponse>("""{ "value": [] }""");

        var dto = GraphSchedulingMapper.MapFreeBusy("paula@contoso.com", wire);

        dto.BusyIntervals.Should().BeEmpty();
    }

    [TestMethod]
    public void MapFreeBusy_ItemAppearsInBusyIntervals_IffStatusIsBusyOofOrTentative()
    {
        // D11 partition property: for generated status sequences, exactly the
        // busy/oof/tentative items produce intervals, in order.
        var baseStart = new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);

        Gen.Int[0, AllStatuses.Length - 1]
            .List[1, 12]
            .Sample(indices =>
            {
                var statuses = indices.Select(i => AllStatuses[i]).ToList();
                var items = statuses
                    .Select(
                        (status, i) =>
                            new GraphScheduleItem(
                                status,
                                new GraphDateTimeTimeZone(
                                    baseStart
                                        .AddHours(i)
                                        .ToString("s", CultureInfo.InvariantCulture),
                                    "UTC"
                                ),
                                new GraphDateTimeTimeZone(
                                    baseStart
                                        .AddHours(i)
                                        .AddMinutes(30)
                                        .ToString("s", CultureInfo.InvariantCulture),
                                    "UTC"
                                )
                            )
                    )
                    .ToList();
                var response = new GraphScheduleResponse(
                    new List<GraphScheduleInformation> { new(items) }
                );

                var dto = GraphSchedulingMapper.MapFreeBusy("paula@contoso.com", response);

                var expected = statuses
                    .Select((status, i) => (status, i))
                    .Where(t => t.status is "busy" or "oof" or "tentative")
                    .Select(t => new BusyIntervalDto(
                        baseStart.AddHours(t.i),
                        baseStart.AddHours(t.i).AddMinutes(30)
                    ))
                    .ToList();
                dto.BusyIntervals.Should()
                    .Equal(expected, "membership is exactly the D11 busy-status set");
            });
    }

    [DataTestMethod]
    [DataRow("busy", true)]
    [DataRow("oof", true)]
    [DataRow("tentative", true)]
    [DataRow("free", false)]
    [DataRow("workingElsewhere", false)]
    [DataRow(null, false)]
    [DataRow("unknown", false)]
    public void IsBusyStatus_MatchesTheD11Set(string? status, bool expected)
    {
        GraphSchedulingMapper.IsBusyStatus(status).Should().Be(expected);
    }
}
