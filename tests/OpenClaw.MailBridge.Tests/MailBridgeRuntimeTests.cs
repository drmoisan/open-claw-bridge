using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public partial class MailBridgeRuntimeTests
{
    [TestMethod]
    public void Bridge_application_get_arg_should_return_value_after_key()
    {
        var value = BridgeApplication.GetArg(new[] { "--config", "abc.json" }, "--config");
        value.Should().Be("abc.json");
    }

    [TestMethod]
    public void Bridge_application_get_arg_should_return_null_when_key_is_missing_or_trailing()
    {
        BridgeApplication.GetArg(new[] { "--mode", "safe" }, "--config").Should().BeNull();
        BridgeApplication.GetArg(["--config"], "--config").Should().BeNull();
    }

    [TestMethod]
    public void Bridge_application_build_host_should_register_bridge_settings_and_state_store()
    {
        var settings = BridgeSettings.Default with { PipeName = "coverage-pipe" };
        using var host = new BridgeApplication().BuildHost([], settings);

        host.Services.GetRequiredService<BridgeSettings>().Should().BeSameAs(settings);
        host.Services.GetRequiredService<BridgeStateStore>().Mode.Should().Be(settings.Mode);
    }

    [TestMethod]
    public async Task Bridge_application_run_host_async_should_delegate_to_host_run_lifecycle()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(new RunLifecycleTracker());
        builder.Services.AddHostedService<ImmediateStopHostedService>();
        using var host = builder.Build();
        var tracker = host.Services.GetRequiredService<RunLifecycleTracker>();

        await new BridgeApplication().RunHostAsync(host);

        tracker.StartCalls.Should().Be(1);
        tracker.StopCalls.Should().Be(1);
    }

    [TestMethod]
    public void Bridge_application_load_settings_should_return_default_settings_when_store_is_missing_without_touching_disk()
    {
        var app = new InMemoryBridgeApplication();

        var settings = app.LoadSettings("memory://bridge.settings.json");

        settings.Should().Be(BridgeSettings.Default);
        app.EnsureSettingsDirectoryCalls.Should().Be(1);
        app.StoreExists.Should().BeTrue();
    }

    [TestMethod]
    public void Bridge_application_load_settings_should_deserialize_stored_settings_from_in_memory_store()
    {
        var app = new InMemoryBridgeApplication
        {
            StoreExists = true,
            StoreContent = """{"PipeName":"x","Mode":"safe"}""",
        };

        var settings = app.LoadSettings("memory://bridge.settings.json");

        settings.PipeName.Should().Be("x");
        settings.Mode.Should().Be("safe");
    }

    [TestMethod]
    public async Task Bridge_application_run_async_should_return_two_for_invalid_settings_from_in_memory_store()
    {
        var app = new InMemoryBridgeApplication
        {
            StoreExists = true,
            StoreContent = """{"PipeName":"x","Mode":"bad"}""",
        };

        var code = await app.RunAsync(["--config", "memory://invalid.json"]);

        code.Should().Be(2);
        app.BuildHostCalls.Should().Be(0);
    }

    [TestMethod]
    public async Task Bridge_application_run_async_should_use_host_for_valid_settings_from_in_memory_store()
    {
        var app = new InMemoryBridgeApplication
        {
            StoreExists = true,
            StoreContent =
                """{"PipeName":"x","Mode":"safe","AutostartOutlook":false,"InboxPollSeconds":5,"CalendarPollSeconds":30,"InboxOverlapMinutes":5,"CalendarPastDays":1,"CalendarFutureDays":1,"MaxItemsPerScan":5,"BodyPreviewMaxChars":20,"ComYieldBatchSize":25,"ComYieldMilliseconds":15,"LogLevel":"Information"}""",
        };

        var code = await app.RunAsync(["--config", "memory://valid.json"]);

        code.Should().Be(0);
        app.BuildHostCalls.Should().Be(1);
        app.RunHostCalls.Should().Be(1);
    }

    [TestMethod]
    public void Bridge_state_store_should_track_mutable_state()
    {
        var settings = BridgeSettings.Default with { Mode = "enhanced" };
        var store = new BridgeStateStore(settings)
        {
            OutlookConnected = true,
            CacheStale = true,
            StaleReason = "reason",
            LastInboxScanUtc = DateTimeOffset.UtcNow,
            LastCalendarScanUtc = DateTimeOffset.UtcNow,
        };

        store.Mode.Should().Be("enhanced");
        store.SetState(BridgeState.ready);
        store.State.Should().Be(BridgeState.ready);
        store.OutlookConnected.Should().BeTrue();
        store.CacheStale.Should().BeTrue();
        store.StaleReason.Should().Be("reason");
    }

    [TestMethod]
    public async Task Outlook_sta_executor_should_run_work_and_propagate_exceptions_or_throw_on_non_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            var create = () => new OutlookStaExecutor();
            create.Should().Throw<PlatformNotSupportedException>();
            return;
        }

        using var executor = new OutlookStaExecutor();
        var value = await executor.InvokeAsync(() => 42);
        value.Should().Be(42);

        var act = async () =>
            await executor.InvokeAsync<int>(() => throw new InvalidOperationException("boom"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [TestMethod]
    public async Task Outlook_scanner_should_set_waiting_state_when_autostart_disabled()
    {
        var settings = BridgeSettings.Default with { AutostartOutlook = false };
        var state = new BridgeStateStore(settings);
        var scanner = new OutlookScanner(
            settings,
            state,
            NullLogger<OutlookScanner>.Instance,
            new FakeComActiveObject(),
            _ => 0,
            () => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );

        await scanner.ScanAsync(new FakeScanStateRepository());

        state.State.Should().Be(BridgeState.waiting_for_outlook);
        state.OutlookConnected.Should().BeFalse();
    }

    [TestMethod]
    public async Task Outlook_scanner_should_set_ready_and_touch_repo_when_outlook_is_available()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var outlook = new FakeOutlookApplication();
        outlook.Namespace.DefaultFolders[6] = new FakeOutlookFolder();
        outlook.Namespace.DefaultFolders[9] = new FakeOutlookFolder();
        var com = new FakeComActiveObject { RunningObject = outlook };
        var repo = new FakeScanStateRepository();
        var now = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var scanner = new OutlookScanner(
            settings,
            state,
            NullLogger<OutlookScanner>.Instance,
            com,
            _ => 1,
            () => now
        );

        await scanner.ScanAsync(repo);

        state.State.Should().Be(BridgeState.ready);
        state.OutlookConnected.Should().BeTrue();
        state.LastInboxScanUtc.Should().Be(now);
        state.LastCalendarScanUtc.Should().Be(now);
        repo.Touches.Should().Be(3);
    }

    [TestMethod]
    public async Task Outlook_scanner_should_degrade_when_scan_throws()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var scanner = new OutlookScanner(
            settings,
            state,
            NullLogger<OutlookScanner>.Instance,
            new FakeComActiveObject { ThrowOnCreate = true },
            _ => 0,
            () => DateTimeOffset.UtcNow
        );

        await scanner.ScanAsync(new FakeScanStateRepository());

        state.State.Should().Be(BridgeState.degraded);
        state.CacheStale.Should().BeTrue();
        state.StaleReason.Should().Be("scan_failure");
    }

    [TestMethod]
    public async Task Scan_worker_should_invoke_repo_initialize_and_scanner_once_before_cancel()
    {
        var repo = new FakeScanStateRepository();
        var scanner = new FakeOutlookScanner();
        var sta = new FakeStaExecutor();
        var worker = new ScanWorker(
            sta,
            scanner,
            repo,
            BridgeSettings.Default with
            {
                InboxPollSeconds = 1,
            }
        );

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        repo.Initialized.Should().BeTrue();
        scanner.Calls.Should().BeGreaterThan(0);
        sta.Calls.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task Cache_repository_should_store_and_load_scan_state()
    {
        using var repo = new CacheRepository(
            $"Data Source=scan-state-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        await repo.TouchScanStateAsync("test-key", now);
        var loaded = await repo.GetScanStateAsync("test-key");

        loaded.Should().NotBeNull();
        loaded!.Value.UtcDateTime.Should().BeCloseTo(now.UtcDateTime, TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Com_active_object_create_and_logon_should_throw_on_non_windows()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test targets non-Windows behavior only.");
        }

        var sut = new ComActiveObject();
        var act = () => sut.CreateAndLogonOutlook();
        act.Should().Throw<PlatformNotSupportedException>();
    }

    [TestMethod]
    public void Com_active_object_create_and_logon_should_return_core_result_when_platform_probe_is_true()
    {
        var sut = new PlatformProbeComActiveObject { PlatformProbeResult = true };

        var result = sut.CreateAndLogonOutlook();

        result.Should().BeSameAs(sut.CoreResult);
        sut.PlatformProbeCalls.Should().Be(1);
    }

    [TestMethod]
    public void Com_active_object_create_and_logon_should_throw_when_platform_probe_reports_non_windows()
    {
        var sut = new PlatformProbeComActiveObject { PlatformProbeResult = false };
        var act = () => sut.CreateAndLogonOutlook();

        act.Should().Throw<PlatformNotSupportedException>();
        sut.PlatformProbeCalls.Should().Be(1);
    }

    [TestMethod]
    public void Com_active_object_create_and_logon_should_use_base_platform_probe_when_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test targets Windows base-platform behavior only.");
        }

        var sut = new CoreOnlyComActiveObject();

        var result = sut.CreateAndLogonOutlook();

        result.Should().BeSameAs(sut.CoreResult);
    }

    [TestMethod]
    public void Com_active_object_try_get_should_return_running_object_when_core_succeeds()
    {
        var sut = new TryGetComActiveObject();

        var result = sut.TryGet("Outlook.Application");

        result.Should().BeSameAs(sut.CoreResult);
    }

    [TestMethod]
    public void Com_active_object_try_get_should_return_null_when_core_throws()
    {
        var sut = new TryGetComActiveObject
        {
            CoreException = new InvalidOperationException("boom"),
        };

        var result = sut.TryGet("Outlook.Application");

        result.Should().BeNull();
    }

    [TestMethod]
    public void Com_active_object_try_get_should_return_null_for_unknown_prog_id()
    {
        new ComActiveObject().TryGet("Definitely.Not.A.Real.ProgId").Should().BeNull();
    }
}
