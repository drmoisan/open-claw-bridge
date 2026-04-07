using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Tracks the bridge's current operating mode and the latest health information shared with RPC callers.
/// </summary>
internal sealed class BridgeStateStore
{
    /// <summary>
    /// Initializes state storage with the configured bridge mode and a startup lifecycle state.
    /// </summary>
    /// <param name="settings">Settings that define the advertised bridge mode.</param>
    public BridgeStateStore(BridgeSettings settings)
    {
        Mode = settings.Mode;
        State = BridgeState.starting;
    }

    /// <summary>
    /// Gets the lifecycle state that the bridge currently reports to clients.
    /// </summary>
    public BridgeState State { get; private set; }

    /// <summary>
    /// Gets the configured bridge mode exposed through status responses.
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// Gets or sets a value indicating whether Outlook is currently reachable through COM.
    /// </summary>
    public bool OutlookConnected { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cached scan data should be treated as stale.
    /// </summary>
    public bool CacheStale { get; set; }

    /// <summary>
    /// Gets or sets the reason associated with the most recent stale-cache transition.
    /// </summary>
    public string? StaleReason { get; set; }

    /// <summary>
    /// Gets or sets the last successful inbox scan timestamp.
    /// </summary>
    public DateTimeOffset? LastInboxScanUtc { get; set; }

    /// <summary>
    /// Gets or sets the last successful calendar scan timestamp.
    /// </summary>
    public DateTimeOffset? LastCalendarScanUtc { get; set; }

    /// <summary>
    /// Updates the bridge lifecycle state that downstream components expose to callers.
    /// </summary>
    /// <param name="state">New lifecycle state.</param>
    public void SetState(BridgeState state) => State = state;

    /// <summary>
    /// Marks the bridge ready and clears stale-cache flags after a successful scan cycle.
    /// </summary>
    /// <param name="lastInboxScanUtc">Latest successful inbox scan timestamp.</param>
    /// <param name="lastCalendarScanUtc">Latest successful calendar scan timestamp.</param>
    public void MarkReady(DateTimeOffset? lastInboxScanUtc, DateTimeOffset? lastCalendarScanUtc)
    {
        LastInboxScanUtc = lastInboxScanUtc;
        LastCalendarScanUtc = lastCalendarScanUtc;
        CacheStale = false;
        StaleReason = null;
        OutlookConnected = true;
        State = BridgeState.ready;
    }

    /// <summary>
    /// Marks the bridge degraded while preserving the last known timestamps.
    /// </summary>
    /// <param name="reason">Human-readable stale-cache reason.</param>
    public void MarkCacheStale(string reason)
    {
        CacheStale = true;
        StaleReason = reason;
        State = BridgeState.degraded;
    }

    /// <summary>
    /// Marks Outlook unavailable without discarding the last known cache timestamps.
    /// </summary>
    /// <param name="reason">Reason that Outlook could not be used.</param>
    public void MarkOutlookUnavailable(string reason)
    {
        OutlookConnected = false;
        MarkCacheStale(reason);
    }
}
