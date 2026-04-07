using Microsoft.Extensions.DependencyInjection;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

internal sealed class FakeComActiveObject : ComActiveObject
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

internal sealed class PlatformProbeComActiveObject : ComActiveObject
{
    public object CoreResult { get; } = new();
    public int PlatformProbeCalls { get; private set; }
    public bool PlatformProbeResult { get; set; }

    protected override bool IsWindowsPlatform()
    {
        PlatformProbeCalls++;
        return PlatformProbeResult;
    }

    protected override object CreateAndLogonOutlookCore() => CoreResult;
}

internal sealed class CoreOnlyComActiveObject : ComActiveObject
{
    public object CoreResult { get; } = new();

    protected override object CreateAndLogonOutlookCore() => CoreResult;
}

internal sealed class TryGetComActiveObject : ComActiveObject
{
    public object CoreResult { get; } = new();
    public Exception? CoreException { get; set; }

    protected override object TryGetCore(string progId)
    {
        if (CoreException is not null)
        {
            throw CoreException;
        }

        return CoreResult;
    }
}

internal sealed class FakeScanStateRepository : IScanStateRepository
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

internal sealed class FakeOutlookScanner : IOutlookScanner
{
    public int Calls { get; private set; }

    public Task ScanAsync(IScanStateRepository repo)
    {
        Calls++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeStaExecutor : IOutlookStaExecutor
{
    public int Calls { get; private set; }

    public Task<T> InvokeAsync<T>(Func<T> operation)
    {
        Calls++;
        return Task.FromResult(operation());
    }

    public void Dispose() { }
}

internal class TestBridgeApplication : BridgeApplication
{
    public int BuildHostCalls { get; private set; }
    public int RunHostCalls { get; private set; }

    internal override Microsoft.Extensions.Hosting.IHost BuildHost(
        string[] args,
        BridgeSettings settings
    )
    {
        BuildHostCalls++;
        return new NoOpHost();
    }

    internal override Task RunHostAsync(Microsoft.Extensions.Hosting.IHost host)
    {
        RunHostCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryBridgeApplication : TestBridgeApplication
{
    public bool StoreExists { get; set; }
    public string? StoreContent { get; set; }
    public int EnsureSettingsDirectoryCalls { get; private set; }

    internal override void EnsureSettingsDirectory(string path) => EnsureSettingsDirectoryCalls++;

    internal override bool SettingsStoreExists(string path) => StoreExists;

    internal override void WriteSettingsStore(string path, string content)
    {
        StoreExists = true;
        StoreContent = content;
    }

    internal override string ReadSettingsStore(string path) =>
        StoreContent
        ?? throw new InvalidOperationException("No in-memory settings content configured.");
}

internal sealed class NoOpHost : Microsoft.Extensions.Hosting.IHost
{
    public IServiceProvider Services => new ServiceCollection().BuildServiceProvider();

    public void Dispose() { }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class RunLifecycleTracker
{
    public int StartCalls { get; set; }
    public int StopCalls { get; set; }
}

internal sealed class ImmediateStopHostedService : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime;
    private readonly RunLifecycleTracker tracker;

    public ImmediateStopHostedService(
        Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime,
        RunLifecycleTracker tracker
    )
    {
        this.lifetime = lifetime;
        this.tracker = tracker;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        tracker.StartCalls++;
        lifetime.StopApplication();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        tracker.StopCalls++;
        return Task.CompletedTask;
    }
}
