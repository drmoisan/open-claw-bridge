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
    public void EnsureOutlook_should_mark_unavailable_when_process_running_and_create_and_logon_fails()
    {
        var settings = BridgeSettings.Default with { AutostartOutlook = true };
        var state = new BridgeStateStore(settings);
        var com = new FakeComActiveObject { RunningObject = null, ThrowOnCreate = true };
        var scanner = BuildScanner(
            settings: settings,
            state: state,
            com: com,
            processCount: _ => 1
        );

        scanner.EnsureOutlook();

        com.CreateAndLogonCalls.Should().Be(1);
        state.OutlookConnected.Should().BeFalse();
        state.CacheStale.Should().BeTrue();
        state.StaleReason.Should().Be("running_instance_unavailable");
    }

    [TestMethod]
    public void EnsureOutlook_should_fall_back_to_create_and_logon_when_AutostartOutlook_is_true_and_rot_attachment_fails_for_a_running_process()
    {
        var settings = BridgeSettings.Default with { AutostartOutlook = true };
        var state = new BridgeStateStore(settings);
        var createdOutlook = new FakeOutlookApplication();
        var com = new FakeComActiveObject { RunningObject = null, CreatedObject = createdOutlook };
        var scanner = BuildScanner(
            settings: settings,
            state: state,
            com: com,
            processCount: _ => 1
        );

        scanner.EnsureOutlook();

        com.TryGetCalls.Should().Be(1);
        com.CreateAndLogonCalls.Should().Be(1);
        state.CacheStale.Should().BeFalse();
        state.StaleReason.Should().BeNull();
        state.State.Should().Be(BridgeState.starting);
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
}
