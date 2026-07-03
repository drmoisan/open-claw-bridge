namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Queue seam between the webhook endpoint and the dispatch worker (D-4). The
/// in-process implementation is <see cref="ChannelNotificationQueue"/>; Azure Service
/// Bus / Storage Queue implementations are F16 deployment concerns behind this same
/// interface (no Azure SDK dependency in this feature).
/// </summary>
internal interface INotificationQueue
{
    /// <summary>
    /// Enqueues <paramref name="item"/> without ever blocking the caller. When the
    /// queue is at capacity the write is dropped (and logged by the implementation);
    /// dropped wake signals are recovered by delta reconciliation.
    /// </summary>
    /// <param name="item">The work item to enqueue.</param>
    /// <returns><c>true</c> when the write was accepted by the queue.</returns>
    bool TryEnqueue(CloudSyncWorkItem item);

    /// <summary>
    /// Asynchronously reads the next work item, completing when one is available.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    ValueTask<CloudSyncWorkItem> DequeueAsync(CancellationToken cancellationToken);
}
