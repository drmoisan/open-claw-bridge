using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core;

public sealed record CoreHealthSnapshot(
    bool DatabaseReady,
    bool HostAdapterReachable,
    DateTimeOffset? LastSuccessfulPollUtc,
    DateTimeOffset? LastFailedPollUtc,
    string? LastFailureReason,
    BridgeStatusDto? BridgeStatus,
    DateTimeOffset? BridgeObservedAtUtc
);

internal sealed class CoreHealthState
{
    private readonly object syncRoot = new();
    private bool databaseReady;
    private bool hostAdapterReachable;
    private DateTimeOffset? lastSuccessfulPollUtc;
    private DateTimeOffset? lastFailedPollUtc;
    private string? lastFailureReason;
    private BridgeStatusDto? bridgeStatus;
    private DateTimeOffset? bridgeObservedAtUtc;

    public void MarkDatabaseReady()
    {
        lock (syncRoot)
        {
            databaseReady = true;
        }
    }

    public void MarkDatabaseFailure(string reason)
    {
        lock (syncRoot)
        {
            databaseReady = false;
            lastFailedPollUtc = DateTimeOffset.UtcNow;
            lastFailureReason = reason;
        }
    }

    public void MarkPollSuccess(BridgeStatusDto? latestBridgeStatus, DateTimeOffset observedAtUtc)
    {
        lock (syncRoot)
        {
            hostAdapterReachable = true;
            lastSuccessfulPollUtc = observedAtUtc;
            lastFailureReason = null;
            if (latestBridgeStatus is not null)
            {
                bridgeStatus = latestBridgeStatus;
                bridgeObservedAtUtc = observedAtUtc;
            }
        }
    }

    public void MarkPollFailure(string reason, BridgeStatusDto? latestBridgeStatus = null)
    {
        lock (syncRoot)
        {
            hostAdapterReachable = false;
            lastFailedPollUtc = DateTimeOffset.UtcNow;
            lastFailureReason = reason;
            if (latestBridgeStatus is not null)
            {
                bridgeStatus = latestBridgeStatus;
                bridgeObservedAtUtc = DateTimeOffset.UtcNow;
            }
        }
    }

    public CoreHealthSnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            return new CoreHealthSnapshot(
                databaseReady,
                hostAdapterReachable,
                lastSuccessfulPollUtc,
                lastFailedPollUtc,
                lastFailureReason,
                bridgeStatus,
                bridgeObservedAtUtc
            );
        }
    }
}
