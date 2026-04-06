using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Coordinates Outlook discovery and updates the cache metadata that signals scan freshness.
/// </summary>
/// <param name="settings">Bridge settings that control Outlook startup behavior.</param>
/// <param name="state">Shared state store updated with scan health information.</param>
/// <param name="logger">Logger used to capture scan failures.</param>
[ExcludeFromCodeCoverage]
internal sealed class OutlookScanner(
    BridgeSettings settings,
    BridgeStateStore state,
    ILogger<OutlookScanner> logger
)
{
    private object? _outlookApp;

    /// <summary>
    /// Ensures Outlook is reachable and records the timestamps for a successful scan cycle.
    /// </summary>
    /// <param name="repo">Repository that persists scan-state timestamps.</param>
    public async Task ScanAsync(CacheRepository repo)
    {
        try
        {
            await EnsureOutlookAsync();
            state.OutlookConnected = _outlookApp is not null;

            // Stop the cycle early when Outlook is still unavailable; the state store already reflects why.
            if (_outlookApp is null)
                return;

            state.SetState(BridgeState.ready);
            await repo.TouchScanStateAsync("last_inbox_scan_utc", DateTimeOffset.UtcNow);
            await repo.TouchScanStateAsync("last_calendar_scan_utc", DateTimeOffset.UtcNow);
            state.LastInboxScanUtc = DateTimeOffset.UtcNow;
            state.LastCalendarScanUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            state.CacheStale = true;
            state.StaleReason = "scan_failure";
            state.SetState(BridgeState.degraded);
            logger.LogError("Scan failed: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Connects to an existing Outlook instance or launches Outlook when configuration allows it.
    /// </summary>
    /// <returns>A completed task once the Outlook connection attempt has finished.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Outlook must be started but COM activation fails.
    /// </exception>
    private Task EnsureOutlookAsync()
    {
        if (_outlookApp is not null)
            return Task.CompletedTask;

        var outlookRunning = Process.GetProcessesByName("OUTLOOK").Length > 0;

        // Prefer attaching to an already-running Outlook instance to avoid duplicate desktop sessions.
        if (outlookRunning)
        {
            _outlookApp = ComActiveObject.TryGet("Outlook.Application");
            if (_outlookApp is null)
            {
                state.SetState(BridgeState.degraded);
            }
            return Task.CompletedTask;
        }

        // When auto-start is disabled, stay in a waiting state instead of creating Outlook implicitly.
        if (!settings.AutostartOutlook)
        {
            state.SetState(BridgeState.waiting_for_outlook);
            return Task.CompletedTask;
        }

        // Bootstrap a new Outlook session only after attach and wait paths have been ruled out.
        var t =
            Type.GetTypeFromProgID("Outlook.Application", false)
            ?? throw new InvalidOperationException("Outlook COM unavailable");
        var app =
            Activator.CreateInstance(t)
            ?? throw new InvalidOperationException("Outlook activation failed");
        var ns = t.InvokeMember("GetNamespace", BindingFlags.InvokeMethod, null, app, ["MAPI"]);
        ns!
            .GetType()
            .InvokeMember(
                "Logon",
                BindingFlags.InvokeMethod,
                null,
                ns,
                ["", "", Type.Missing, Type.Missing]
            );
        _outlookApp = app;
        return Task.CompletedTask;
    }
}
