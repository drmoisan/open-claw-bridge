using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// In-process <see cref="INotificationQueue"/> over a bounded
/// <see cref="System.Threading.Channels.Channel"/> (D-4): capacity comes from
/// <see cref="CloudSyncOptions.QueueCapacity"/>, full-queue writes use
/// <see cref="BoundedChannelFullMode.DropWrite"/> so the webhook never blocks, and
/// every dropped write logs a Warning naming the dropped item kind. Dropped wake
/// signals are recovered by the periodic delta reconciliation (master §6.2).
/// </summary>
internal sealed class ChannelNotificationQueue : INotificationQueue
{
    private readonly Channel<CloudSyncWorkItem> channel;

    /// <summary>
    /// Creates the bounded queue sized by <see cref="CloudSyncOptions.QueueCapacity"/>.
    /// </summary>
    public ChannelNotificationQueue(
        IOptions<CloudSyncOptions> optionsAccessor,
        ILogger<ChannelNotificationQueue> logger
    )
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        var capacity = optionsAccessor.Value.QueueCapacity;
        channel = Channel.CreateBounded<CloudSyncWorkItem>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropWrite },
            dropped =>
                logger.LogWarning(
                    "CloudSync notification queue is full (capacity {Capacity}); dropped a {ItemKind} work item. Delta reconciliation will recover the change.",
                    capacity,
                    dropped.GetType().Name
                )
        );
    }

    /// <inheritdoc />
    public bool TryEnqueue(CloudSyncWorkItem item) => channel.Writer.TryWrite(item);

    /// <inheritdoc />
    public ValueTask<CloudSyncWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAsync(cancellationToken);
}
