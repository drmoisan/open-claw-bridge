using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Scanner-path attendee population tests for issue #71 (US-AC1, US-AC3, US-AC5, SP-B1, SP-B2):
/// <see cref="OutlookScanner"/> must populate <see cref="EventDto.RequiredAttendeesJson"/>,
/// <see cref="EventDto.OptionalAttendeesJson"/>, and <see cref="EventDto.ResourcesJson"/> from the COM
/// <c>Recipients</c> collection in enhanced mode. Exercised through the reflection-readable
/// <c>FakeAppointmentItem.Recipients</c> double — no live COM, no temp files, deterministic clock.
/// </summary>
[TestClass]
public sealed class OutlookScannerAttendeesTests
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

    private static async Task<EventDto> ScanSingleEventAsync(
        FakeAppointmentItem appointment,
        string mode = "enhanced"
    )
    {
        var settings = BridgeSettings.Default with
        {
            Mode = mode,
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

    private static FakeAppointmentItem BaseAppointment(FakeRecipients? recipients) =>
        new()
        {
            EntryID = "entry-att",
            GlobalAppointmentID = "gid-att",
            Subject = "Attendees",
            Start = FixedNow.AddDays(1),
            End = FixedNow.AddDays(1).AddHours(1),
            Recipients = recipients,
        };

    private static IReadOnlyList<(string Name, string Email)> Parse(string? json)
    {
        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        return doc
            .RootElement.EnumerateArray()
            .Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("email").GetString()!))
            .ToArray();
    }

    [TestMethod]
    public async Task ScanCalendar_should_populate_all_three_attendee_fields_in_enhanced_mode()
    {
        // Arrange: one required, one optional, one resource recipient.
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = "Req Person",
                Address = "req@example.com",
            },
            new FakeRecipient
            {
                Type = 2,
                Name = "Opt Person",
                Address = "opt@example.com",
            },
            new FakeRecipient
            {
                Type = 3,
                Name = "Room A",
                Address = "rooma@example.com",
            }
        );

        // Act
        var evt = await ScanSingleEventAsync(BaseAppointment(recipients));

        // Assert: each field non-null with the correct name and email.
        Parse(evt.RequiredAttendeesJson).Should().Equal(("Req Person", "req@example.com"));
        Parse(evt.OptionalAttendeesJson).Should().Equal(("Opt Person", "opt@example.com"));
        Parse(evt.ResourcesJson).Should().Equal(("Room A", "rooma@example.com"));
    }

    [TestMethod]
    public async Task ScanCalendar_should_classify_by_type_and_exclude_out_of_range()
    {
        // Arrange: types 1/2/3 plus two out-of-range types (0 and 4) that must be excluded.
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = "R",
                Address = "r@example.com",
            },
            new FakeRecipient
            {
                Type = 2,
                Name = "O",
                Address = "o@example.com",
            },
            new FakeRecipient
            {
                Type = 3,
                Name = "Res",
                Address = "res@example.com",
            },
            new FakeRecipient
            {
                Type = 0,
                Name = "Unknown0",
                Address = "zero@example.com",
            },
            new FakeRecipient
            {
                Type = 4,
                Name = "Unknown4",
                Address = "four@example.com",
            }
        );

        // Act
        var evt = await ScanSingleEventAsync(BaseAppointment(recipients));

        // Assert: each list holds only its own type; out-of-range recipients appear nowhere.
        Parse(evt.RequiredAttendeesJson).Should().ContainSingle().Which.Name.Should().Be("R");
        Parse(evt.OptionalAttendeesJson).Should().ContainSingle().Which.Name.Should().Be("O");
        Parse(evt.ResourcesJson).Should().ContainSingle().Which.Name.Should().Be("Res");

        var allNames = Parse(evt.RequiredAttendeesJson)
            .Concat(Parse(evt.OptionalAttendeesJson))
            .Concat(Parse(evt.ResourcesJson))
            .Select(a => a.Name)
            .ToArray();
        allNames.Should().NotContain("Unknown0");
        allNames.Should().NotContain("Unknown4");
    }

    [TestMethod]
    public async Task ScanCalendar_should_emit_both_keys_when_name_or_email_missing()
    {
        // Arrange: a required recipient with no name, an optional recipient with no resolvable email.
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = null,
                Address = "noname@example.com",
            },
            new FakeRecipient
            {
                Type = 2,
                Name = "No Email",
                Address = null,
            }
        );

        // Act
        var evt = await ScanSingleEventAsync(BaseAppointment(recipients));

        // Assert: both keys present, missing value is empty string.
        Parse(evt.RequiredAttendeesJson).Should().Equal((string.Empty, "noname@example.com"));
        Parse(evt.OptionalAttendeesJson).Should().Equal(("No Email", string.Empty));
    }

    [TestMethod]
    public async Task ScanCalendar_should_resolve_email_from_address_entry_fallback()
    {
        // Arrange: recipient with no direct Address but a resolvable AddressEntry.Address.
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = "Fallback Person",
                Address = null,
                AddressEntry = new FakeAddressEntry { Address = "fallback@example.com" },
            }
        );

        // Act
        var evt = await ScanSingleEventAsync(BaseAppointment(recipients));

        // Assert: email resolved from the AddressEntry fallback.
        Parse(evt.RequiredAttendeesJson)
            .Should()
            .Equal(("Fallback Person", "fallback@example.com"));
    }

    [TestMethod]
    public async Task ScanCalendar_should_emit_empty_array_for_type_with_no_recipients()
    {
        // Arrange: only a required recipient; optional and resource types are absent.
        var recipients = new FakeRecipients(
            new FakeRecipient
            {
                Type = 1,
                Name = "Only Required",
                Address = "only@example.com",
            }
        );

        // Act
        var evt = await ScanSingleEventAsync(BaseAppointment(recipients));

        // Assert: empty types are "[]" (not null); distinguishes "no attendees" from "redacted".
        evt.OptionalAttendeesJson.Should().Be("[]");
        evt.ResourcesJson.Should().Be("[]");
        Parse(evt.RequiredAttendeesJson).Should().ContainSingle();
    }

    [TestMethod]
    public async Task ScanCalendar_should_emit_empty_arrays_when_recipients_absent()
    {
        // Arrange: appointment with no Recipients collection at all.
        var evt = await ScanSingleEventAsync(BaseAppointment(recipients: null));

        // Assert: all three fields are "[]" (not null).
        evt.RequiredAttendeesJson.Should().Be("[]");
        evt.OptionalAttendeesJson.Should().Be("[]");
        evt.ResourcesJson.Should().Be("[]");
    }

    [TestMethod]
    public async Task ScanCalendar_should_fail_soft_when_a_recipient_read_throws()
    {
        // Arrange (spec SP-B3): a Recipients collection whose Item(index) throws must not abort the
        // event scan; the unreadable recipient is skipped and the scan completes.
        var appointment = new FakeAppointmentItem
        {
            EntryID = "entry-att",
            GlobalAppointmentID = "gid-att",
            Subject = "Attendees",
            Start = FixedNow.AddDays(1),
            End = FixedNow.AddDays(1).AddHours(1),
            Recipients = new FakeThrowingRecipients(),
        };

        // Act
        var evt = await ScanSingleEventAsync(appointment);

        // Assert: the event was still produced; the unreadable recipient yields no attendees ("[]").
        evt.RequiredAttendeesJson.Should().Be("[]");
        evt.OptionalAttendeesJson.Should().Be("[]");
        evt.ResourcesJson.Should().Be("[]");
    }
}
