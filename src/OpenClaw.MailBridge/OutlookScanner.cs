using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

internal interface IOutlookScanner
{
    Task ScanAsync(IScanStateRepository repo);
}

/// <summary>
/// Coordinates Outlook discovery and updates the cache metadata that signals scan freshness.
/// </summary>
internal sealed class OutlookScanner : IOutlookScanner
{
    private readonly BridgeSettings _settings;
    private readonly BridgeStateStore _state;
    private readonly ILogger<OutlookScanner> _logger;
    private readonly ComActiveObject _com;
    private readonly Func<string, int> _processCount;
    private readonly Func<DateTimeOffset> _utcNow;
    private object? _outlookApp;

    public OutlookScanner(
        BridgeSettings settings,
        BridgeStateStore state,
        ILogger<OutlookScanner> logger
    )
        : this(
            settings,
            state,
            logger,
            new ComActiveObject(),
            name => Process.GetProcessesByName(name).Length,
            () => DateTimeOffset.UtcNow
        ) { }

    internal OutlookScanner(
        BridgeSettings settings,
        BridgeStateStore state,
        ILogger<OutlookScanner> logger,
        ComActiveObject com,
        Func<string, int> processCount,
        Func<DateTimeOffset> utcNow
    )
    {
        _settings = settings;
        _state = state;
        _logger = logger;
        _com = com;
        _processCount = processCount;
        _utcNow = utcNow;
    }

    public async Task ScanAsync(IScanStateRepository repo)
    {
        try
        {
            EnsureOutlook();
            _state.OutlookConnected = _outlookApp is not null;

            if (_outlookApp is null)
                return;

            _state.SetState(BridgeState.ready);
            var now = _utcNow();
            await repo.TouchScanStateAsync("last_inbox_scan_utc", now);
            await repo.TouchScanStateAsync("last_calendar_scan_utc", now);
            _state.LastInboxScanUtc = now;
            _state.LastCalendarScanUtc = now;
        }
        catch (Exception ex)
        {
            _state.CacheStale = true;
            _state.StaleReason = "scan_failure";
            _state.SetState(BridgeState.degraded);
            _logger.LogError("Scan failed: {Message}", ex.Message);
        }
    }

    internal void EnsureOutlook()
    {
        if (_outlookApp is not null)
            return;

        var outlookRunning = _processCount("OUTLOOK") > 0;
        if (outlookRunning)
        {
            _outlookApp = _com.TryGet("Outlook.Application");
            if (_outlookApp is null)
            {
                _state.SetState(BridgeState.degraded);
            }
            return;
        }

        if (!_settings.AutostartOutlook)
        {
            _state.SetState(BridgeState.waiting_for_outlook);
            return;
        }

        _outlookApp = _com.CreateAndLogonOutlook();
    }
}
