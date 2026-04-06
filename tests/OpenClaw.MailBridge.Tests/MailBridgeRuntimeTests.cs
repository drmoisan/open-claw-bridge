using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class MailBridgeRuntimeTests
{
    [TestMethod]
    public void Program_get_arg_should_return_value_after_key()
    {
        var method = typeof(Program).GetMethod("GetArg", BindingFlags.NonPublic | BindingFlags.Static)!;
        var value = (string?)method.Invoke(null, [new[] { "--config", "abc.json" }, "--config"]);
        value.Should().Be("abc.json");
    }

    [TestMethod]
    public void Program_load_settings_should_create_default_file_when_missing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"mailbridge-test-{Guid.NewGuid():N}");
        var path = Path.Combine(tempRoot, "bridge.settings.json");
        try
        {
            var method = typeof(Program).GetMethod(
                "LoadSettings",
                BindingFlags.NonPublic | BindingFlags.Static
            )!;
            var settings = (BridgeSettings)method.Invoke(null, [path])!;
            settings.Should().Be(BridgeSettings.Default);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
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
        store.LastInboxScanUtc.Should().NotBeNull();
        store.LastCalendarScanUtc.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Outlook_sta_executor_should_run_work_and_propagate_exceptions()
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

        var act = async () => await executor.InvokeAsync<int>(() => throw new InvalidOperationException("boom"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [TestMethod]
    public async Task Outlook_scanner_should_set_waiting_state_when_autostart_disabled()
    {
        var settings = BridgeSettings.Default with { AutostartOutlook = false };
        var state = new BridgeStateStore(settings);
        var scanner = new OutlookScanner(settings, state, NullLogger<OutlookScanner>.Instance);

        await scanner.ScanAsync(new CacheRepository());

        state.State.Should().Be(BridgeState.waiting_for_outlook);
        state.OutlookConnected.Should().BeFalse();
    }

    [TestMethod]
    public async Task Scan_worker_should_start_and_stop_cleanly()
    {
        if (!OperatingSystem.IsWindows())
        {
            var settings = BridgeSettings.Default with { AutostartOutlook = false, InboxPollSeconds = 1 };
            var state = new BridgeStateStore(settings);
            var scanner = new OutlookScanner(settings, state, NullLogger<OutlookScanner>.Instance);
            await scanner.ScanAsync(new CacheRepository());
            state.State.Should().Be(BridgeState.waiting_for_outlook);
            return;
        }

        var runtimeSettings = BridgeSettings.Default with { AutostartOutlook = false, InboxPollSeconds = 1 };
        var runtimeState = new BridgeStateStore(runtimeSettings);
        using var sta = new OutlookStaExecutor();
        var runtimeScanner = new OutlookScanner(runtimeSettings, runtimeState, NullLogger<OutlookScanner>.Instance);
        var repo = new CacheRepository();
        var worker = new ScanWorker(sta, runtimeScanner, repo, runtimeSettings);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        runtimeState.State.Should().Be(BridgeState.waiting_for_outlook);
    }

    [TestMethod]
    public async Task Cache_repository_should_store_and_load_scan_state()
    {
        var repo = new CacheRepository();
        await repo.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        await repo.TouchScanStateAsync("test-key", now);
        var loaded = await repo.GetScanStateAsync("test-key");

        loaded.Should().NotBeNull();
        loaded!.Value.UtcDateTime.Should().BeCloseTo(now.UtcDateTime, TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_should_return_status_and_default_items()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings) { CacheStale = true, StaleReason = "unit-test" };
        var repo = new CacheRepository();
        await repo.InitializeAsync();
        await repo.TouchScanStateAsync("last_inbox_scan_utc", DateTimeOffset.UtcNow);
        await repo.TouchScanStateAsync("last_calendar_scan_utc", DateTimeOffset.UtcNow);

        var worker = new PipeRpcWorker(settings, state, repo, NullLogger<PipeRpcWorker>.Instance);
        var handle = typeof(PipeRpcWorker).GetMethod("Handle", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var status = (RpcResponse)await (Task<RpcResponse>)
            handle.Invoke(worker, [new RpcRequest("1", BridgeMethods.GetStatus, null)])!;
        status.Ok.Should().BeTrue();

        var list = (RpcResponse)await (Task<RpcResponse>)
            handle.Invoke(worker, [new RpcRequest("2", BridgeMethods.ListRecentMessages, null)])!;
        list.Ok.Should().BeTrue();
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_write_response_should_downgrade_oversize_payload()
    {
        var settings = BridgeSettings.Default;
        var worker = new PipeRpcWorker(
            settings,
            new BridgeStateStore(settings),
            new CacheRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );
        var method = typeof(PipeRpcWorker).GetMethod(
            "WriteResponse",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        await using var stream = new MemoryStream();
        var huge = new string('a', 1024 * 1024 + 128);
        await (Task)
            method.Invoke(
                worker,
                [stream, RpcResponse.Success("id", new { huge }), CancellationToken.None]
            )!;

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        json.Should().Contain(BridgeErrorCodes.InternalError);
        json.Should().Contain("Response too large");
    }

    [TestMethod]
    public void Com_active_object_try_get_should_return_null_for_unknown_prog_id()
    {
        ComActiveObject.TryGet("Definitely.Not.A.Real.ProgId").Should().BeNull();
    }
}
