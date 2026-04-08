using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Focused unit tests for <see cref="OutlookScanner"/> to cover inbox scan,
/// calendar scan, normalization, EnsureOutlook branches, and edge cases.
/// </summary>
public partial class MailBridgeRuntimeTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 4, 7, 12, 0, 0, TimeSpan.Zero);

    private static OutlookScanner BuildScanner(
        BridgeSettings? settings = null,
        BridgeStateStore? state = null,
        FakeComActiveObject? com = null,
        Func<string, int>? processCount = null,
        Func<DateTimeOffset>? utcNow = null
    )
    {
        var s = settings ?? BridgeSettings.Default;
        return new OutlookScanner(
            s,
            state ?? new BridgeStateStore(s),
            NullLogger<OutlookScanner>.Instance,
            com ?? new FakeComActiveObject(),
            processCount ?? (_ => 0),
            utcNow ?? (() => FixedNow)
        );
    }

    private static FakeOutlookApplication BuildOutlookWithFolders(
        FakeOutlookFolder? inbox = null,
        FakeOutlookFolder? calendar = null
    )
    {
        var outlook = new FakeOutlookApplication();
        if (inbox is not null)
            outlook.Namespace.DefaultFolders[6] = inbox;
        if (calendar is not null)
            outlook.Namespace.DefaultFolders[9] = calendar;
        return outlook;
    }

    // ── EnsureOutlook branches ──────────────────────────────────────────

    [TestMethod]
    public void EnsureOutlook_should_short_circuit_when_outlook_is_already_set()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var outlook = new FakeOutlookApplication();
        var com = new FakeComActiveObject { RunningObject = outlook };
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        // First call sets _outlookApp via TryGet
        scanner.EnsureOutlook();
        // Second call should short-circuit without querying COM again
        com.RunningObject = null;
        scanner.EnsureOutlook();

        // If it queried COM again, _outlookApp would be null and state would change
        state.State.Should().Be(BridgeState.starting);
    }

    [TestMethod]
    public void EnsureOutlook_should_mark_unavailable_when_process_running_but_com_fails()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var com = new FakeComActiveObject { RunningObject = null };
        var scanner = BuildScanner(
            settings: settings,
            state: state,
            com: com,
            processCount: _ => 1
        );

        scanner.EnsureOutlook();

        state.OutlookConnected.Should().BeFalse();
        state.CacheStale.Should().BeTrue();
        state.StaleReason.Should().Be("running_instance_unavailable");
    }

    // ── ScanInboxAsync / ScanCalendarAsync entry points ─────────────────

    [TestMethod]
    public async Task ScanInboxAsync_should_only_touch_inbox_scan_state()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanInboxAsync(repo);

        state.State.Should().Be(BridgeState.ready);
        state.OutlookConnected.Should().BeTrue();
        repo.Values.Should().ContainKey("last_inbox_scan_utc");
        repo.Values.Should().ContainKey("last_successful_scan_utc");
        // Calendar scan state should not be touched by inbox-only scan
        repo.Values.Should().NotContainKey("last_calendar_scan_utc");
    }

    [TestMethod]
    public async Task ScanCalendarAsync_should_only_touch_calendar_scan_state()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanCalendarAsync(repo);

        state.State.Should().Be(BridgeState.ready);
        repo.Values.Should().ContainKey("last_calendar_scan_utc");
        repo.Values.Should().ContainKey("last_successful_scan_utc");
        repo.Values.Should().NotContainKey("last_inbox_scan_utc");
    }

    // ── Folder resolution failures ──────────────────────────────────────

    [TestMethod]
    public async Task ScanAsync_should_return_early_when_inbox_folder_unavailable()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        // Namespace has no folder 6 registered → GetDefaultFolder throws
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[9] = new FakeOutlookFolder();
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        state.CacheStale.Should().BeTrue();
        state.StaleReason.Should().Be("default_inbox_unavailable");
        repo.Touches.Should().Be(0, "no scan state should be touched when inbox is unavailable");
    }

    [TestMethod]
    public async Task ScanAsync_should_return_early_when_calendar_folder_unavailable()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var outlook = new FakeOutlookApplication();
        // Inbox available but no calendar folder
        outlook.Namespace.DefaultFolders[6] = new FakeOutlookFolder();
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        state.CacheStale.Should().BeTrue();
        state.StaleReason.Should().Be("default_calendar_unavailable");
    }

    [TestMethod]
    public async Task ScanInboxAsync_should_not_fail_when_calendar_folder_is_missing()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        // Only inbox folder registered, no calendar
        var outlook = BuildOutlookWithFolders(inbox: new FakeOutlookFolder());
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanInboxAsync(repo);

        // Should succeed because only inbox path is exercised
        state.State.Should().Be(BridgeState.ready);
    }

    // ── Inbox scan with mail items ──────────────────────────────────────

    [TestMethod]
    public async Task ScanAsync_should_upsert_mail_items_from_inbox()
    {
        var settings = BridgeSettings.Default with { BodyPreviewMaxChars = 100 };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "entry-mail-1",
                Subject = "Hello",
                ReceivedTime = FixedNow.AddHours(-1),
                SentOn = FixedNow.AddHours(-2),
                Unread = true,
                HasAttachments = false,
                MessageClass = "IPM.Note",
                SenderName = "Alice",
                SenderEmailAddress = "alice@test.com",
                Body = "Test body content",
            }
        );
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        state.State.Should().Be(BridgeState.ready);
        repo.Messages.Should().HaveCount(1);
        var msg = repo.Messages.Values.First();
        msg.Subject.Should().Be("Hello");
        msg.ItemKind.Should().Be("mail");
        msg.SenderName.Should().Be("Alice");
        msg.SenderEmail.Should().Be("alice@test.com");
        msg.Unread.Should().BeTrue();
    }

    [TestMethod]
    public async Task ScanAsync_should_upsert_meeting_items_with_meeting_kind()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(
            new FakeMeetingItem
            {
                EntryID = "entry-mtg-1",
                Subject = "Team Standup",
                ReceivedTime = FixedNow,
                SentOn = FixedNow.AddMinutes(-5),
                MessageClass = "IPM.Schedule.Meeting.Request",
                SenderName = "Bob",
            }
        );
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().HaveCount(1);
        var msg = repo.Messages.Values.First();
        msg.ItemKind.Should().Be("meeting");
        msg.Subject.Should().Be("Team Standup");
    }

    [TestMethod]
    public async Task ScanAsync_should_skip_inbox_items_with_blank_entry_id()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        inbox.Items.Add(
            new FakeMailItem
            {
                EntryID = "",
                Subject = "Blank",
                MessageClass = "IPM.Note",
            }
        );
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        repo.Messages.Should().BeEmpty("items with blank EntryID should be skipped");
        state.State.Should().Be(BridgeState.ready);
    }

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
        var com = new FakeComActiveObject { ThrowOnCreate = true };
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        // First scan fails
        await scanner.ScanAsync(new FakeScanStateRepository());
        state.CacheStale.Should().BeTrue();

        // Provide a real outlook on retry
        var inbox = new FakeOutlookFolder();
        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        com.ThrowOnCreate = false;
        com.CreatedObject = outlook;
        com.RunningObject = null;

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
