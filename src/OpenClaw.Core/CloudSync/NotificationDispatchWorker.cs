using Microsoft.Extensions.Logging;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Consumes <see cref="INotificationQueue"/> (D-2): each message work item is fetched
/// through <see cref="IHostAdapterClient.GetMessageAsync"/> and upserted via
/// <see cref="CoreCacheRepository.UpsertMessagesAsync"/> with the D-3 synthesized
/// ready/graph status; a failed fetch logs Warning and drops the item (delta
/// reconciliation is the recovery path — no fabricated healthy status). Lifecycle work
/// items route to <see cref="GraphSubscriptionManager.HandleLifecycleAsync"/>. The
/// loop survives individual item failures.
/// </summary>
internal sealed class NotificationDispatchWorker(
    INotificationQueue queue,
    IHostAdapterClient hostAdapterClient,
    GraphSubscriptionManager subscriptionManager,
    CoreCacheRepository repository,
    TimeProvider timeProvider,
    ILogger<NotificationDispatchWorker> logger
) : BackgroundService
{
    /// <summary>D-3: a successful Graph fetch is itself the liveness evidence.</summary>
    private static readonly BridgeStatusDto ReadyGraphStatus = new(
        State: "ready",
        Mode: "graph",
        OutlookConnected: true,
        CacheStale: false,
        StaleReason: null,
        LastInboxScanUtc: null,
        LastCalendarScanUtc: null
    );

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CloudSyncWorkItem item;
            try
            {
                item = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Dispatching a {ItemKind} work item failed; the item is dropped and delta reconciliation recovers the change.",
                    item.GetType().Name
                );
            }
        }
    }

    /// <summary>Routes one work item: message fetch-and-upsert, or lifecycle handling.</summary>
    internal async Task ProcessItemAsync(CloudSyncWorkItem item, CancellationToken ct)
    {
        switch (item)
        {
            case LifecycleWorkItem lifecycle:
                await subscriptionManager.HandleLifecycleAsync(lifecycle, ct);
                break;
            case NotificationWorkItem notification:
                await DispatchMessageAsync(notification, ct);
                break;
            default:
                logger.LogWarning(
                    "Ignored an unknown queue item kind {ItemKind}.",
                    item.GetType().Name
                );
                break;
        }
    }

    private async Task DispatchMessageAsync(NotificationWorkItem notification, CancellationToken ct)
    {
        var envelope = await hostAdapterClient.GetMessageAsync(
            notification.MessageId,
            requestId: null,
            ct
        );
        if (!envelope.Ok || envelope.Data is null)
        {
            logger.LogWarning(
                "Fetch for notified message {MessageId} failed ({Code}); item dropped — delta reconciliation is the recovery path.",
                notification.MessageId,
                envelope.Error?.Code
            );
            return;
        }

        await repository.UpsertMessagesAsync(
            [envelope.Data],
            ReadyGraphStatus,
            envelope.Meta.RequestId,
            timeProvider.GetUtcNow()
        );
    }
}
