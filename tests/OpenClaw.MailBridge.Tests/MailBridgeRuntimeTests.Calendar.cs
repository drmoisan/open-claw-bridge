using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Scanner tests covering calendar events, max-items-per-scan, inbox filter state,
/// attachment detection, meeting-kind classification, and error-recovery behavior.
/// Split from <c>MailBridgeRuntimeTests.OutlookScanner.cs</c> to stay under 500 lines.
/// </summary>
public partial class MailBridgeRuntimeTests
{
    // ── Calendar scan with appointment items ────────────────────────────

    [TestMethod]
    public async Task ScanAsync_should_upsert_calendar_events()
    {
        var settings = BridgeSettings.Default with
        {
            CalendarPastDays = 7,
            CalendarFutureDays = 30,
            BodyPreviewMaxChars = 50,
        };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "entry-evt-1",
                GlobalAppointmentID = "gid-1",
                Subject = "Sprint Planning",
                Start = FixedNow.AddDays(1),
                End = FixedNow.AddDays(1).AddHours(1),
                Location = "Room A",
                IsRecurring = false,
                Organizer = "Alice",
                Body = "Agenda for sprint planning",
            }
        );
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        state.State.Should().Be(BridgeState.ready);
        repo.Events.Should().HaveCount(1);
        var evt = repo.Events.Values.First();
        evt.Subject.Should().Be("Sprint Planning");
        evt.Location.Should().Be("Room A");
    }

    [TestMethod]
    public async Task ScanCalendarAsync_should_skip_events_with_missing_start_or_end()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var calendar = new FakeOutlookFolder();
        // Item with EntryID but no Start/End properties → NormalizeEvent returns null
        calendar.Items.Add(new FakeNoDateAppointment { EntryID = "entry-x", Subject = "No dates" });
        var outlook = BuildOutlookWithFolders(calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanCalendarAsync(repo);

        repo.Events.Should().BeEmpty("events missing start/end should be skipped");
        state.State.Should().Be(BridgeState.ready);
    }

    // ── MaxItemsPerScan limit ───────────────────────────────────────────

    [TestMethod]
    public async Task ScanAsync_should_respect_max_items_per_scan_limit()
    {
        var settings = BridgeSettings.Default with { MaxItemsPerScan = 2 };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        for (var i = 0; i < 5; i++)
        {
            inbox.Items.Add(
                new FakeMailItem
                {
                    EntryID = $"entry-{i}",
                    Subject = $"Mail {i}",
                    ReceivedTime = FixedNow,
                    MessageClass = "IPM.Note",
                }
            );
        }
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().HaveCount(2, "only MaxItemsPerScan items should be processed");
    }

    // ── Inbox filter uses last scan state ───────────────────────────────

    [TestMethod]
    public async Task ScanInboxAsync_should_use_last_scan_state_for_filter()
    {
        var settings = BridgeSettings.Default with { InboxOverlapMinutes = 10 };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "entry-1",
                Subject = "Recent",
                ReceivedTime = FixedNow,
                MessageClass = "IPM.Note",
            }
        );
        var outlook = BuildOutlookWithFolders(inbox: inbox);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository
        {
            Values = { ["last_inbox_scan_utc"] = FixedNow.AddHours(-1) },
        };
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanInboxAsync(repo);

        // The filter was applied to the items; restricted items contain our item
        repo.Messages.Should().HaveCount(1);
        inbox.Items.LastFilter.Should().NotBeNullOrEmpty("a restrict filter should be applied");
    }

    // ── Multiple mail and calendar items together ───────────────────────

    [TestMethod]
    public async Task ScanAsync_should_process_multiple_inbox_and_calendar_items()
    {
        var settings = BridgeSettings.Default with
        {
            MaxItemsPerScan = 10,
            BodyPreviewMaxChars = 200,
        };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "mail-1",
                Subject = "Mail 1",
                ReceivedTime = FixedNow,
                MessageClass = "IPM.Note",
                SenderName = "S1",
                Body = "Body 1",
            }
        );
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "mail-2",
                Subject = "Mail 2",
                ReceivedTime = FixedNow,
                MessageClass = "IPM.Note",
                SenderName = "S2",
            }
        );
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "evt-1",
                GlobalAppointmentID = "gid-1",
                Subject = "Event 1",
                Start = FixedNow.AddDays(1),
                End = FixedNow.AddDays(1).AddHours(1),
            }
        );
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().HaveCount(2);
        repo.Events.Should().HaveCount(1);
        state.State.Should().Be(BridgeState.ready);
        repo.Touches.Should().Be(3, "inbox + calendar + last_successful scan states");
    }

    // ── Mail item with attachments ──────────────────────────────────────

    [TestMethod]
    public async Task ScanAsync_should_detect_attachments_on_mail_items()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "att-1",
                Subject = "With Attachment",
                ReceivedTime = FixedNow,
                MessageClass = "IPM.Note",
                HasAttachments = true,
            }
        );
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().HaveCount(1);
        repo.Messages.Values.First().HasAttachments.Should().BeTrue();
    }

    // ── Calendar event without GlobalAppointmentID ──────────────────────

    [TestMethod]
    public async Task ScanCalendarAsync_should_handle_event_without_global_appointment_id()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "entry-no-gid",
                GlobalAppointmentID = null,
                Subject = "Local Event",
                Start = FixedNow.AddDays(1),
                End = FixedNow.AddDays(1).AddHours(1),
            }
        );
        var outlook = BuildOutlookWithFolders(calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanCalendarAsync(repo);

        repo.Events.Should().HaveCount(1);
        var evt = repo.Events.Values.First();
        evt.GlobalAppointmentId.Should().BeNull();
    }

    // ── Recurring calendar event ────────────────────────────────────────

    [TestMethod]
    public async Task ScanCalendarAsync_should_handle_recurring_events()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var calendar = new FakeOutlookFolder();
        calendar.Items.Add(
            new FakeAppointmentItem
            {
                EntryID = "entry-recur",
                GlobalAppointmentID = "gid-recur",
                Subject = "Daily Standup",
                Start = FixedNow.AddDays(1),
                End = FixedNow.AddDays(1).AddMinutes(15),
                IsRecurring = true,
                Organizer = "Manager",
            }
        );
        var outlook = BuildOutlookWithFolders(calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanCalendarAsync(repo);

        repo.Events.Should().HaveCount(1);
        var evt = repo.Events.Values.First();
        evt.IsRecurring.Should().BeTrue();
        evt.Organizer.Should().Be("Manager");
    }

    // ── Mail item with meeting class (IsMeetingItem via MessageClass) ───

    [TestMethod]
    public async Task ScanAsync_should_detect_meeting_kind_via_message_class()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        // FakeMailItem whose MessageClass contains "Meeting" but type name does not
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "mtg-class-1",
                Subject = "Meeting via class",
                ReceivedTime = FixedNow,
                MessageClass = "IPM.Schedule.Meeting.Resp.Pos",
            }
        );
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().HaveCount(1);
        repo.Messages.Values.First().ItemKind.Should().Be("meeting");
    }

    // ── Scan after exception should release COM and clear _outlookApp ───

    [TestMethod]
    public async Task ScanAsync_should_clear_outlook_ref_after_exception()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        // First attach succeeds but the namespace is unavailable, so the scan throws after attach.
        var com = new FakeComActiveObject
        {
            RunningObject = new FakeOutlookApplicationWithNullNamespace(),
        };
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        // First scan fails
        await scanner.ScanAsync(new FakeScanStateRepository());
        state.CacheStale.Should().BeTrue();

        // Provide a working outlook on retry; _outlookApp must have been cleared after the failure.
        var inbox = new FakeOutlookFolder();
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        com.RunningObject = outlook;

        var repo2 = new FakeScanStateRepository();
        await scanner.ScanAsync(repo2);

        state.State.Should().Be(BridgeState.ready);
    }
}

/// <summary>
/// Fake appointment item without Start/End date properties to exercise
/// the null-check branch in NormalizeEvent.
/// </summary>
internal sealed class FakeNoDateAppointment
{
    public required string EntryID { get; init; }
    public required string Subject { get; init; }
    public FakeOutlookParent Parent { get; init; } = new();
}
