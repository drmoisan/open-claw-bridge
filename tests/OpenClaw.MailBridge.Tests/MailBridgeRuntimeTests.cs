using System.Reflection;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Security.AccessControl;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class MailBridgeRuntimeTests
{
    [TestMethod]
    public void Bridge_application_get_arg_should_return_value_after_key()
    {
        var value = BridgeApplication.GetArg(new[] { "--config", "abc.json" }, "--config");
        value.Should().Be("abc.json");
    }

    [TestMethod]
    public void Bridge_application_load_settings_should_create_default_file_when_missing()
    {
        var app = new BridgeApplication();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"mailbridge-test-{Guid.NewGuid():N}");
        var path = Path.Combine(tempRoot, "bridge.settings.json");
        try
        {
            var settings = app.LoadSettings(path);
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
    public async Task Bridge_application_run_async_should_return_two_for_invalid_settings()
    {
        var app = new BridgeApplication();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"mailbridge-bad-{Guid.NewGuid():N}");
        var path = Path.Combine(tempRoot, "bridge.settings.json");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(path, """{"pipeName":"x","mode":"bad"}""");

        var code = await app.RunAsync(["--config", path]);
        code.Should().Be(2);

        Directory.Delete(tempRoot, true);
    }

    [TestMethod]
    public async Task Bridge_application_run_async_should_use_host_for_valid_settings()
    {
        var app = new TestBridgeApplication();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"mailbridge-good-{Guid.NewGuid():N}");
        var path = Path.Combine(tempRoot, "bridge.settings.json");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(
            path,
            """{"PipeName":"x","Mode":"safe","AutostartOutlook":false,"InboxPollSeconds":5,"CalendarPollSeconds":30,"InboxOverlapMinutes":5,"CalendarPastDays":1,"CalendarFutureDays":1,"MaxItemsPerScan":5,"BodyPreviewMaxChars":20,"LogLevel":"Information"}"""
        );

        var code = await app.RunAsync(["--config", path]);

        code.Should().Be(0);
        app.BuildHostCalls.Should().Be(1);
        Directory.Delete(tempRoot, true);
    }

    [TestMethod]
    public async Task Program_main_should_delegate_to_bridge_application()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"mailbridge-main-{Guid.NewGuid():N}");
        var path = Path.Combine(tempRoot, "bridge.settings.json");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(path, """{"pipeName":"x","mode":"bad"}""");

        var code = await Program.Main(["--config", path]);
        code.Should().Be(2);
        Directory.Delete(tempRoot, true);
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

        var act = async () => await executor.InvokeAsync<int>(() => throw new InvalidOperationException("boom"));
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
        var com = new FakeComActiveObject { RunningObject = new object() };
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
        repo.Touches.Should().Be(2);
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
        var worker = new ScanWorker(sta, scanner, repo, BridgeSettings.Default with { InboxPollSeconds = 1 });

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
        var repo = new CacheRepository();
        await repo.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        await repo.TouchScanStateAsync("test-key", now);
        var loaded = await repo.GetScanStateAsync("test-key");

        loaded.Should().NotBeNull();
        loaded!.Value.UtcDateTime.Should().BeCloseTo(now.UtcDateTime, TimeSpan.FromSeconds(5));
    }


    [TestMethod]
    public async Task Pipe_rpc_worker_handle_client_should_return_invalid_request_for_unknown_method()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Named pipe message mode integration test is Windows-specific.");
        }

        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );
        var response = await InvokeHandleClientAsync(worker, """{"id":"1","method":"unknown","params":null}""");
        response.Should().Contain(BridgeErrorCodes.InvalidRequest);
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_client_should_return_payload_too_large_error()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Named pipe message mode integration test is Windows-specific.");
        }

        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );
        var oversizedParams = new string('a', 70000);
        var payload =
            $"{{\"id\":\"1\",\"method\":\"{BridgeMethods.GetStatus}\",\"params\":{{\"x\":\"{oversizedParams}\"}}}}";

        var response = await InvokeHandleClientAsync(worker, payload);
        response.Should().Contain(BridgeErrorCodes.PayloadTooLarge);
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_client_should_return_status_for_get_status_method()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Named pipe message mode integration test is Windows-specific.");
        }

        var repo = new FakeScanStateRepository
        {
            Values =
            {
                ["last_inbox_scan_utc"] = DateTimeOffset.UtcNow,
                ["last_calendar_scan_utc"] = DateTimeOffset.UtcNow,
            },
        };
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            repo,
            NullLogger<PipeRpcWorker>.Instance
        );

        var response = await InvokeHandleClientAsync(
            worker,
            $"{{\"id\":\"1\",\"method\":\"{BridgeMethods.GetStatus}\",\"params\":null}}"
        );
        response.Should().Contain("\"ok\":true");
        response.Should().Contain("\"state\"");
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_should_return_status_and_default_items()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings) { CacheStale = true, StaleReason = "unit-test" };
        var repo = new FakeScanStateRepository
        {
            Values =
            {
                ["last_inbox_scan_utc"] = DateTimeOffset.UtcNow,
                ["last_calendar_scan_utc"] = DateTimeOffset.UtcNow,
            },
        };

        var worker = new PipeRpcWorker(settings, state, repo, NullLogger<PipeRpcWorker>.Instance);

        var status = await worker.Handle(new RpcRequest("1", BridgeMethods.GetStatus, null));
        status.Ok.Should().BeTrue();

        var list = await worker.Handle(new RpcRequest("2", BridgeMethods.ListRecentMessages, null));
        list.Ok.Should().BeTrue();
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_write_response_should_downgrade_oversize_payload()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        await using var stream = new MemoryStream();
        var huge = new string('a', 1024 * 1024 + 128);
        await worker.WriteResponse(stream, RpcResponse.Success("id", new { huge }), CancellationToken.None);

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        json.Should().Contain(BridgeErrorCodes.InternalError);
        json.Should().Contain("Response too large");
    }

    [TestMethod]
    public void Pipe_rpc_worker_build_pipe_security_should_return_descriptor()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        if (!OperatingSystem.IsWindows())
        {
            var act = () => worker.BuildPipeSecurity();
            act.Should().Throw<PlatformNotSupportedException>();
            return;
        }

        var buildSecurity = () => worker.BuildPipeSecurity();
        buildSecurity.Should().NotThrow();
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
    public void Com_active_object_try_get_should_return_null_for_unknown_prog_id()
    {
        new ComActiveObject().TryGet("Definitely.Not.A.Real.ProgId").Should().BeNull();
    }


    private static async Task<string> InvokeHandleClientAsync(PipeRpcWorker worker, string payload)
    {
        var pipeName = $"test_pipe_{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var method = typeof(PipeRpcWorker).GetMethod("HandleClientAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cts.Token);
            await (Task)method.Invoke(worker, [server, cts.Token])!;
        });

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous
        );
        await client.ConnectAsync(5000, cts.Token);

        var bytes = Encoding.UTF8.GetBytes(payload);
        await client.WriteAsync(bytes, 0, bytes.Length, cts.Token);
        await client.FlushAsync(cts.Token);

        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        do
        {
            var read = await client.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            ms.Write(buffer, 0, read);
        } while (!client.IsMessageComplete);

        await serverTask;
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private sealed class FakeComActiveObject : ComActiveObject
    {
        public object? RunningObject { get; set; }
        public bool ThrowOnCreate { get; set; }

        public override object? TryGet(string progId) => RunningObject;

        public override object CreateAndLogonOutlook()
        {
            if (ThrowOnCreate)
            {
                throw new InvalidOperationException("failed");
            }

            return new object();
        }
    }

    private sealed class FakeScanStateRepository : IScanStateRepository
    {
        public bool Initialized { get; private set; }
        public int Touches { get; private set; }
        public Dictionary<string, DateTimeOffset?> Values { get; } = new();

        public Task InitializeAsync()
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task TouchScanStateAsync(string key, DateTimeOffset value)
        {
            Touches++;
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetScanStateAsync(string key)
        {
            Values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }
    }

    private sealed class FakeOutlookScanner : IOutlookScanner
    {
        public int Calls { get; private set; }

        public Task ScanAsync(IScanStateRepository repo)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStaExecutor : IOutlookStaExecutor
    {
        public int Calls { get; private set; }

        public Task<T> InvokeAsync<T>(Func<T> operation)
        {
            Calls++;
            return Task.FromResult(operation());
        }

        public void Dispose() { }
    }


    private sealed class TestBridgeApplication : BridgeApplication
    {
        public int BuildHostCalls { get; private set; }

        internal override Microsoft.Extensions.Hosting.IHost BuildHost(string[] args, BridgeSettings settings)
        {
            BuildHostCalls++;
            return new NoOpHost();
        }

        internal override Task RunHostAsync(Microsoft.Extensions.Hosting.IHost host) => Task.CompletedTask;
    }

    private sealed class NoOpHost : Microsoft.Extensions.Hosting.IHost
    {
        public IServiceProvider Services => new ServiceCollection().BuildServiceProvider();

        public void Dispose() { }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

}
