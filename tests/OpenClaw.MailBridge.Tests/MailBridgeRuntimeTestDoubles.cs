using Microsoft.Extensions.DependencyInjection;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

internal sealed class FakeComActiveObject : ComActiveObject
{
    public object? RunningObject { get; set; }
    public object? CreatedObject { get; set; }
    public bool ThrowOnCreate { get; set; }
    public int TryGetCalls { get; private set; }
    public int CreateAndLogonCalls { get; private set; }

    public override object? TryGet(string progId)
    {
        TryGetCalls++;
        return RunningObject;
    }

    public override object CreateAndLogonOutlook()
    {
        CreateAndLogonCalls++;
        if (ThrowOnCreate)
        {
            throw new InvalidOperationException("failed");
        }

        return CreatedObject ?? new FakeOutlookApplication();
    }
}

internal sealed class FakeOutlookApplication
{
    public FakeOutlookNamespace Namespace { get; } = new();

    public object GetNamespace(string name) => Namespace;
}

/// <summary>
/// Attaches like a running Outlook instance but returns a null MAPI namespace so the
/// scan pipeline throws after attach, exercising the general scan-failure catch in
/// <see cref="OutlookScanner"/> (state degraded with reason "scan_failure").
/// </summary>
internal sealed class FakeOutlookApplicationWithNullNamespace
{
    public object? GetNamespace(string name) => null;
}

internal sealed class FakeOutlookNamespace
{
    public Dictionary<int, object> DefaultFolders { get; } = new();

    public object GetDefaultFolder(int folderType)
    {
        if (!DefaultFolders.TryGetValue(folderType, out var folder))
        {
            throw new InvalidOperationException($"Folder {folderType} missing.");
        }

        return folder;
    }
}

internal sealed class FakeOutlookFolder
{
    public FakeOutlookItems Items { get; } = new();
}

internal sealed class FakeOutlookItems : List<object>
{
    public bool IncludeRecurrences { get; set; }
    public string? LastSort { get; private set; }
    public string? LastFilter { get; private set; }

    public void Sort(string expression) => LastSort = expression;

    public FakeOutlookItems Restrict(string filter)
    {
        LastFilter = filter;
        return this;
    }
}

internal sealed class FakeMailItem
{
    public required string EntryID { get; init; }
    public required string Subject { get; init; }
    public DateTimeOffset ReceivedTime { get; init; }
    public DateTimeOffset SentOn { get; init; }
    public bool Unread { get; init; }
    public bool HasAttachments { get; init; }
    public string? MessageClass { get; init; }
    public string? SenderName { get; init; }
    public string? SenderEmailAddress { get; init; }
    public string? Body { get; init; }
    public FakeOutlookParent Parent { get; init; } = new();
}

internal sealed class FakeMeetingItem
{
    public required string EntryID { get; init; }
    public required string Subject { get; init; }
    public DateTimeOffset ReceivedTime { get; init; }
    public DateTimeOffset SentOn { get; init; }
    public bool Unread { get; init; }
    public bool HasAttachments { get; init; }
    public string MessageClass { get; init; } = "IPM.Schedule.Meeting.Request";
    public string? SenderName { get; init; }
    public string? SenderEmailAddress { get; init; }
    public string? Body { get; init; }
    public FakeOutlookParent Parent { get; init; } = new();
}

internal sealed record FakeAppointmentItem
{
    public required string EntryID { get; init; }
    public string? GlobalAppointmentID { get; init; }
    public required string Subject { get; init; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public DateTime? StartUTC { get; init; }
    public DateTime? EndUTC { get; init; }
    public string? Location { get; init; }
    public bool IsRecurring { get; init; }
    public string? Organizer { get; init; }
    public string? Body { get; init; }
    public int? ResponseStatus { get; init; }

    // Issue #72 Graph-shaped COM analogs.
    public int? Sensitivity { get; init; }
    public string? Categories { get; init; }
    public bool IsOnlineMeeting { get; init; }
    public bool AllowNewTimeProposal { get; init; }
    public int? RecurrenceState { get; init; }
    public DateTime? LastModificationTime { get; init; }
    public FakeOutlookParent Parent { get; init; } = new();
}

internal sealed class FakeOutlookParent
{
    public FakeOutlookStore Store { get; init; } = new();
}

internal sealed class FakeOutlookStore
{
    public string StoreID { get; init; } = "store-1";
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

internal sealed class FakeScanStateRepository : IBridgeRepository
{
    public bool Initialized { get; private set; }
    public int Touches { get; private set; }
    public Dictionary<string, DateTimeOffset?> Values { get; } = new();
    public Dictionary<string, MessageDto> Messages { get; } = new();
    public Dictionary<string, EventDto> Events { get; } = new();

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

    public Task UpsertMessageAsync(string entryId, string? storeId, MessageDto message)
    {
        Messages[message.BridgeId] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageDto>> ListRecentMessagesAsync(
        DateTimeOffset sinceUtc,
        int limit
    ) =>
        Task.FromResult<IReadOnlyList<MessageDto>>(
            Messages
                .Values.OrderByDescending(x => x.ReceivedUtc)
                .ThenBy(x => x.BridgeId)
                .Take(limit)
                .ToArray()
        );

    public Task<IReadOnlyList<MessageDto>> ListRecentMeetingRequestsAsync(
        DateTimeOffset sinceUtc,
        int limit
    ) =>
        Task.FromResult<IReadOnlyList<MessageDto>>(
            Messages
                .Values.Where(x => x.ItemKind == "meeting")
                .OrderByDescending(x => x.ReceivedUtc)
                .ThenBy(x => x.BridgeId)
                .Take(limit)
                .ToArray()
        );

    public Task<MessageDto?> GetMessageAsync(string bridgeId)
    {
        Messages.TryGetValue(bridgeId, out var message);
        return Task.FromResult(message);
    }

    public Task UpsertEventAsync(
        string entryId,
        string? storeId,
        string? globalAppointmentId,
        EventDto evt
    )
    {
        Events[evt.BridgeId] = evt;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventDto>> ListCalendarWindowAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit
    ) =>
        Task.FromResult<IReadOnlyList<EventDto>>(
            Events
                .Values.Where(x => x.StartUtc >= startUtc && x.StartUtc < endUtc)
                .OrderBy(x => x.StartUtc)
                .ThenBy(x => x.BridgeId)
                .Take(limit)
                .ToArray()
        );

    public Task<EventDto?> GetEventAsync(string bridgeId)
    {
        Events.TryGetValue(bridgeId, out var evt);
        return Task.FromResult(evt);
    }

    public Task<ScanStateSnapshot> GetScanStateSnapshotAsync() =>
        Task.FromResult(
            new ScanStateSnapshot(
                Values.GetValueOrDefault("last_inbox_scan_utc"),
                Values.GetValueOrDefault("last_calendar_scan_utc"),
                Values.GetValueOrDefault("last_successful_scan_utc")
            )
        );
}

internal sealed class FakeOutlookScanner : IOutlookScanner
{
    public int Calls { get; private set; }
    public int InboxCalls { get; private set; }
    public int CalendarCalls { get; private set; }

    public Task ScanAsync(IBridgeRepository repo)
    {
        Calls++;
        return Task.CompletedTask;
    }

    public Task ScanInboxAsync(IBridgeRepository repo)
    {
        Calls++;
        InboxCalls++;
        return Task.CompletedTask;
    }

    public Task ScanCalendarAsync(IBridgeRepository repo)
    {
        Calls++;
        CalendarCalls++;
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
