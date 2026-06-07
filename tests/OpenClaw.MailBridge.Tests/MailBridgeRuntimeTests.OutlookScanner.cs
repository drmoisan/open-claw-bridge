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
    public void EnsureOutlook_should_wait_for_outlook_when_autostart_logon_fails_and_no_process_running()
    {
        // Reproduces the stuck-in-starting defect: autostart enabled, Outlook NOT running,
        // and the headless CreateAndLogonOutlook throws. EnsureOutlook must not propagate and
        // must leave a defined non-starting state.
        var settings = BridgeSettings.Default with
        {
            AutostartOutlook = true,
        };
        var state = new BridgeStateStore(settings);
        var com = new FakeComActiveObject { RunningObject = null, ThrowOnCreate = true };
        var scanner = BuildScanner(
            settings: settings,
            state: state,
            com: com,
            processCount: _ => 0
        );

        var act = () => scanner.EnsureOutlook();

        act.Should().NotThrow("autostart logon failure must be handled, not propagated");
        com.CreateAndLogonCalls.Should().Be(1);
        state.State.Should().Be(BridgeState.waiting_for_outlook);
        state.OutlookConnected.Should().BeFalse();
        state.State.Should().NotBe(BridgeState.starting);
    }

    [TestMethod]
    public async Task ScanInboxAsync_should_not_throw_and_should_wait_when_autostart_logon_fails_without_running_process()
    {
        // End-to-end through ScanInboxAsync: the worker path must not fault and the bridge must
        // leave the starting state when autostart logon fails with no running Outlook process.
        var settings = BridgeSettings.Default with
        {
            AutostartOutlook = true,
        };
        var state = new BridgeStateStore(settings);
        var com = new FakeComActiveObject { RunningObject = null, ThrowOnCreate = true };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(
            settings: settings,
            state: state,
            com: com,
            processCount: _ => 0
        );

        var act = () => scanner.ScanInboxAsync(repo);

        await act.Should().NotThrowAsync();
        state.State.Should().Be(BridgeState.waiting_for_outlook);
        state.OutlookConnected.Should().BeFalse();
        state.State.Should().NotBe(BridgeState.starting);
    }

    [TestMethod]
    public void EnsureOutlook_should_attach_to_running_instance_without_create_and_logon()
    {
        // Regression: the happy attach path (TryGet returns a non-null instance) must connect and
        // must never invoke CreateAndLogonOutlook, regardless of autostart.
        var settings = BridgeSettings.Default with
        {
            AutostartOutlook = true,
        };
        var state = new BridgeStateStore(settings);
        var outlook = new FakeOutlookApplication();
        var com = new FakeComActiveObject { RunningObject = outlook };
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        scanner.EnsureOutlook();

        com.TryGetCalls.Should().Be(1);
        com.CreateAndLogonCalls.Should().Be(0, "attach path must not create a new session");
        state.CacheStale.Should().BeFalse();
        state.StaleReason.Should().BeNull();
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

    // ── COM yield boundary tests ────────────────────────────────────────

    [TestMethod]
    public async Task EnumerateItems_should_yield_after_ComYieldBatchSize_items()
    {
        var settings = BridgeSettings.Default with
        {
            ComYieldBatchSize = 5,
            ComYieldMilliseconds = 0,
            MaxItemsPerScan = 10,
            BodyPreviewMaxChars = 50,
        };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        for (var i = 0; i < 10; i++)
        {
            inbox.Items.Add(
                new FakeMailItem
                {
                    EntryID = $"entry-yield-{i}",
                    Subject = $"Item {i}",
                    MessageClass = "IPM.Note",
                    SenderName = "Tester",
                }
            );
        }

        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        // All 10 items should be processed despite yield points at items 5 and 10
        repo.Messages.Should().HaveCount(10);
        state.State.Should().Be(BridgeState.ready);
    }

    [TestMethod]
    public async Task EnumerateItems_should_not_yield_when_count_is_below_batch_size()
    {
        var settings = BridgeSettings.Default with
        {
            ComYieldBatchSize = 50,
            ComYieldMilliseconds = 100,
            MaxItemsPerScan = 100,
            BodyPreviewMaxChars = 50,
        };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        for (var i = 0; i < 3; i++)
        {
            inbox.Items.Add(
                new FakeMailItem
                {
                    EntryID = $"entry-small-{i}",
                    Subject = $"Small {i}",
                    MessageClass = "IPM.Note",
                    SenderName = "Tester",
                }
            );
        }

        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await scanner.ScanAsync(repo);
        sw.Stop();

        // With only 3 items and batch size 50, no Thread.Sleep should fire.
        // If it did, elapsed time would be >= 100ms.
        repo.Messages.Should().HaveCount(3);
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "no yield should occur below batch size");
    }

    [TestMethod]
    public async Task EnumerateItems_should_yield_multiple_times_for_large_item_sets()
    {
        var settings = BridgeSettings.Default with
        {
            ComYieldBatchSize = 25,
            ComYieldMilliseconds = 0,
            MaxItemsPerScan = 100,
            BodyPreviewMaxChars = 50,
        };
        var state = new BridgeStateStore(settings);
        var inbox = new FakeOutlookFolder();
        for (var i = 0; i < 75; i++)
        {
            inbox.Items.Add(
                new FakeMailItem
                {
                    EntryID = $"entry-large-{i}",
                    Subject = $"Large {i}",
                    MessageClass = "IPM.Note",
                    SenderName = "Tester",
                }
            );
        }

        var calendar = new FakeOutlookFolder();
        var outlook = BuildOutlookWithFolders(inbox: inbox, calendar: calendar);
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var scanner = BuildScanner(settings: settings, state: state, com: com);

        await scanner.ScanAsync(repo);

        // All 75 items processed; yield points at 25, 50, 75
        repo.Messages.Should().HaveCount(75);
        state.State.Should().Be(BridgeState.ready);
    }

    [TestMethod]
    public void BridgeSettingsValidator_should_reject_ComYieldBatchSize_less_than_1()
    {
        var settings = BridgeSettings.Default with { ComYieldBatchSize = 0 };

        var errors = BridgeSettingsValidator.Validate(settings);

        errors.Should().Contain(e => e.Contains("comYieldBatchSize"));
    }

    [TestMethod]
    public void BridgeSettingsValidator_should_reject_negative_ComYieldMilliseconds()
    {
        var settings = BridgeSettings.Default with { ComYieldMilliseconds = -1 };

        var errors = BridgeSettingsValidator.Validate(settings);

        errors.Should().Contain(e => e.Contains("comYieldMilliseconds"));
    }

    [TestMethod]
    public void BridgeSettingsValidator_should_accept_valid_yield_settings()
    {
        var settings = BridgeSettings.Default with
        {
            ComYieldBatchSize = 25,
            ComYieldMilliseconds = 15,
        };

        var errors = BridgeSettingsValidator.Validate(settings);

        errors
            .Should()
            .NotContain(
                e => e.Contains("comYieldBatchSize") || e.Contains("comYieldMilliseconds"),
                "valid yield settings should produce no yield-related errors"
            );
    }
}
