using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Scanner population tests for issue #72 (AC2, AC5): <see cref="OutlookScanner.NormalizeEvent"/>
/// must populate the nine new <see cref="EventDto"/> Graph-shaped fields from their Outlook COM
/// analogs. Uses the shared COM-double pattern (no live COM, no temp files, deterministic clock).
/// </summary>
[TestClass]
public sealed class OutlookScannerGraphFieldsTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static OutlookScanner BuildScanner(BridgeSettings settings, FakeComActiveObject com) =>
        new(
            settings,
            new BridgeStateStore(settings),
            NullLogger<OutlookScanner>.Instance,
            com,
            _ => 0,
            () => FixedNow
        );

    private static FakeOutlookApplication BuildOutlookWithCalendar(FakeOutlookFolder calendar)
    {
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[9] = calendar;
        outlook.Namespace.DefaultFolders[6] = new FakeOutlookFolder();
        return outlook;
    }

    private static async Task<EventDto> ScanSingleEventAsync(FakeAppointmentItem appointment)
    {
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
        };
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(appointment);
        var com = new FakeComActiveObject { RunningObject = BuildOutlookWithCalendar(calendar) };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings, com);

        await scanner.ScanCalendarAsync(repo);

        repo.Events.Should().HaveCount(1);
        return repo.Events.Values.Single();
    }

    private static FakeAppointmentItem BaseAppointment() =>
        new()
        {
            EntryID = "entry-graph",
            GlobalAppointmentID = "gid-graph",
            Subject = "Graph fields",
            Start = FixedNow.AddDays(1),
            End = FixedNow.AddDays(1).AddHours(1),
        };

    [TestMethod]
    public async Task NormalizeEvent_should_split_categories_on_comma_space()
    {
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                Categories = "Red Category, Blue Category, Green Category",
            }
        );

        evt.Categories.Should().Equal("Red Category", "Blue Category", "Green Category");
    }

    [TestMethod]
    public async Task NormalizeEvent_should_yield_empty_categories_for_null_source()
    {
        var evt = await ScanSingleEventAsync(BaseAppointment() with { Categories = null });

        evt.Categories.Should().NotBeNull();
        evt.Categories.Should()
            .BeEmpty("a null Categories string maps to an empty array, not null");
    }

    [TestMethod]
    public async Task NormalizeEvent_should_set_isOrganizer_true_when_response_status_is_organized()
    {
        var evt = await ScanSingleEventAsync(BaseAppointment() with { ResponseStatus = 1 });

        evt.IsOrganizer.Should().BeTrue("ResponseStatus 1 (olResponseOrganized) means organizer");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task NormalizeEvent_should_set_isOrganizer_false_for_non_organized_status(
        int responseStatus
    )
    {
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                ResponseStatus = responseStatus,
            }
        );

        evt.IsOrganizer.Should().BeFalse();
    }

    [TestMethod]
    public async Task NormalizeEvent_should_populate_isOnlineMeeting_from_com()
    {
        var evt = await ScanSingleEventAsync(BaseAppointment() with { IsOnlineMeeting = true });

        evt.IsOnlineMeeting.Should().BeTrue();
    }

    [TestMethod]
    public async Task NormalizeEvent_should_populate_allowNewTimeProposals_from_singular_com_name()
    {
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                AllowNewTimeProposal = true,
            }
        );

        evt.AllowNewTimeProposals.Should().BeTrue();
    }

    [TestMethod]
    public async Task NormalizeEvent_should_set_iCalUId_to_global_appointment_id()
    {
        var evt = await ScanSingleEventAsync(BaseAppointment());

        evt.ICalUId.Should().Be("gid-graph", "iCalUId reuses GlobalAppointmentID per spec");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task NormalizeEvent_should_set_seriesMasterId_null_for_non_recurring_and_master(
        int recurrenceState
    )
    {
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                RecurrenceState = recurrenceState,
            }
        );

        evt.SeriesMasterId.Should().BeNull();
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task NormalizeEvent_should_set_seriesMasterId_to_gid_for_occurrence_and_exception(
        int recurrenceState
    )
    {
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                RecurrenceState = recurrenceState,
            }
        );

        evt.SeriesMasterId.Should().Be("gid-graph");
    }

    [TestMethod]
    public async Task NormalizeEvent_should_populate_lastModifiedDateTime_from_com()
    {
        var modified = new DateTime(2026, 4, 30, 8, 30, 0, DateTimeKind.Utc);
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                LastModificationTime = modified,
            }
        );

        evt.LastModifiedDateTime.Should().Be(new DateTimeOffset(modified));
    }

    [TestMethod]
    public async Task NormalizeEvent_should_carry_bodyFull_raw_untruncated()
    {
        var longBody = new string('x', BridgeSettings.Default.BodyPreviewMaxChars + 250);
        var evt = await ScanSingleEventAsync(BaseAppointment() with { Body = longBody });

        evt.BodyFull.Should()
            .Be(longBody, "bodyFull is the raw COM Body, not the truncated preview");
        evt.BodyFull!.Length.Should().BeGreaterThan(BridgeSettings.Default.BodyPreviewMaxChars);
    }

    [TestMethod]
    public async Task NormalizeEvent_should_map_sensitivityLabel_from_sensitivity_int()
    {
        var evt = await ScanSingleEventAsync(BaseAppointment() with { Sensitivity = 2 });

        evt.SensitivityLabel.Should().Be("private");
        evt.Sensitivity.Should().Be(2);
    }

    [TestMethod]
    public async Task NormalizeEvent_recurring_online_meeting_yields_expected_graph_fields()
    {
        // AC5: a recurring online meeting occurrence yields non-null iCalUId, isOnlineMeeting true,
        // and the correct sensitivityLabel.
        var evt = await ScanSingleEventAsync(
            BaseAppointment() with
            {
                IsRecurring = true,
                IsOnlineMeeting = true,
                RecurrenceState = 2,
                Sensitivity = 2,
            }
        );

        evt.ICalUId.Should().Be("gid-graph");
        evt.IsOnlineMeeting.Should().BeTrue();
        evt.SensitivityLabel.Should().Be("private");
        evt.SeriesMasterId.Should().Be("gid-graph");
    }
}
