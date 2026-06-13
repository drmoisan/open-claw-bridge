using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Contract/round-trip tests (AC-8) asserting each D6 DTO serializes and deserializes
/// via System.Text.Json to identical field values.
/// </summary>
[TestClass]
public sealed class SchedulingDtoContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    [TestMethod]
    public void AttendeeDto_RoundTrips()
    {
        var original = new AttendeeDto("Ada Lovelace", "ada@contoso.com");

        var result = RoundTrip(original);

        // AttendeeDto carries only scalar fields, so record value-equality applies.
        result.Should().Be(original);
    }

    [TestMethod]
    public void SchedulingMessageDto_RoundTrips()
    {
        var original = new SchedulingMessageDto(
            Id: "msg-1",
            Subject: "Project sync",
            BodyPreview: "Let's meet",
            BodyContent: "<p>Let's meet</p>",
            BodyContentType: "html",
            From: new AttendeeDto("Sender", "sender@contoso.com"),
            Sender: new AttendeeDto("Delegate", "delegate@contoso.com"),
            ToRecipients: new[] { new AttendeeDto("Owner", "owner@contoso.com") },
            CcRecipients: new[] { new AttendeeDto("Cc", "cc@contoso.com") },
            ConversationId: "conv-1",
            ReceivedDateTime: new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
            MeetingMessageType: "meetingRequest",
            Importance: "high"
        );

        var result = RoundTrip(original);

        // DTOs carrying collection properties are compared by field value (deep
        // structural equivalence) because record equality on IReadOnlyList<> members
        // is reference-based; the round-trip yields new list instances.
        result.Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public void SchedulingEventDto_RoundTrips()
    {
        var original = new SchedulingEventDto(
            Id: "evt-1",
            ICalUId: "ical-1",
            SeriesMasterId: "series-1",
            Subject: "Board",
            BodyPreview: "Agenda",
            BodyContent: "Agenda body",
            BodyContentType: "text",
            Organizer: new AttendeeDto("CEO", "ceo@contoso.com"),
            RequiredAttendees: new[] { new AttendeeDto("R", "r@contoso.com") },
            OptionalAttendees: new[] { new AttendeeDto("O", "o@contoso.com") },
            ResourceAttendees: new[] { new AttendeeDto("Room", "room@contoso.com") },
            Categories: new[] { "Executive", "Board" },
            IsOrganizer: true,
            IsOnlineMeeting: true,
            AllowNewTimeProposals: false,
            Sensitivity: "normal",
            Start: new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero),
            StartTimeZone: "Pacific Standard Time",
            End: new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            EndTimeZone: "Pacific Standard Time",
            LastModifiedDateTime: new DateTimeOffset(2026, 6, 9, 8, 0, 0, TimeSpan.Zero),
            Type: "seriesMaster"
        );

        var result = RoundTrip(original);

        // DTOs carrying collection properties are compared by field value (deep
        // structural equivalence) because record equality on IReadOnlyList<> members
        // is reference-based; the round-trip yields new list instances.
        result.Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public void MailboxSettingsDto_RoundTrips()
    {
        var original = new MailboxSettingsDto(
            TimeZoneId: "Pacific Standard Time",
            WorkingDays: new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
            },
            WorkingHoursStart: new TimeOnly(9, 0),
            WorkingHoursEnd: new TimeOnly(17, 0)
        );

        var result = RoundTrip(original);

        // DTOs carrying collection properties are compared by field value (deep
        // structural equivalence) because record equality on IReadOnlyList<> members
        // is reference-based; the round-trip yields new list instances.
        result.Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public void FreeBusyScheduleDto_RoundTrips()
    {
        var original = new FreeBusyScheduleDto(
            MailboxUpn: "owner@contoso.com",
            BusyIntervals: new List<BusyIntervalDto>
            {
                new(
                    new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 6, 10, 9, 30, 0, TimeSpan.Zero)
                ),
            }
        );

        var result = RoundTrip(original);

        // DTOs carrying collection properties are compared by field value (deep
        // structural equivalence) because record equality on IReadOnlyList<> members
        // is reference-based; the round-trip yields new list instances.
        result.Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public void ApiEnvelope_MailboxSettingsDto_RoundTrips()
    {
        var original = new ApiEnvelope<MailboxSettingsDto>(
            Ok: true,
            Data: new MailboxSettingsDto(
                TimeZoneId: "Pacific Standard Time",
                WorkingDays: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
                WorkingHoursStart: new TimeOnly(8, 0),
                WorkingHoursEnd: new TimeOnly(16, 30)
            ),
            Meta: new ApiMeta("req-mbx", "1.0", null),
            Error: null
        );

        var result = RoundTrip(original);

        // The outer envelope (Ok, Data, Meta, Error) and the inner MailboxSettingsDto fields are
        // compared by structural equivalence because collection members are reference-compared by
        // record equality and the round-trip yields new instances.
        result.Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public void ApiEnvelope_FreeBusyScheduleDto_RoundTrips()
    {
        var original = new ApiEnvelope<FreeBusyScheduleDto>(
            Ok: true,
            Data: new FreeBusyScheduleDto(
                MailboxUpn: "owner@contoso.com",
                BusyIntervals: new List<BusyIntervalDto>
                {
                    new(
                        new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero)
                    ),
                    new(
                        new DateTimeOffset(2026, 6, 15, 13, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 6, 15, 13, 30, 0, TimeSpan.Zero)
                    ),
                }
            ),
            Meta: new ApiMeta("req-fb", "1.0", null),
            Error: null
        );

        var result = RoundTrip(original);

        // The outer envelope and the inner FreeBusyScheduleDto (including BusyIntervals) are
        // compared by structural equivalence.
        result.Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public void SendMailRequest_RoundTrips()
    {
        var original = new SendMailRequest(
            Subject: "Re: Project sync",
            BodyContent: "Proposed times below.",
            BodyContentType: "text",
            ToRecipients: new[] { new AttendeeDto("Owner", "owner@contoso.com") },
            CcRecipients: Array.Empty<AttendeeDto>(),
            InReplyToMessageId: "msg-1"
        );

        var result = RoundTrip(original);

        // DTOs carrying collection properties are compared by field value (deep
        // structural equivalence) because record equality on IReadOnlyList<> members
        // is reference-based; the round-trip yields new list instances.
        result.Should().BeEquivalentTo(original);
    }
}
