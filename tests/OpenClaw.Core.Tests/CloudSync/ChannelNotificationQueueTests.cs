using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Unit tests for <see cref="ChannelNotificationQueue"/> (D-4): FIFO round-trip for
/// both work-item shapes, non-blocking drop-with-Warning at capacity, and a pending
/// dequeue completing when an item arrives — all deterministic, no timers or real
/// waits.
/// </summary>
[TestClass]
public sealed class ChannelNotificationQueueTests
{
    private static ChannelNotificationQueue Queue(
        int capacity,
        ILogger<ChannelNotificationQueue>? logger = null
    ) =>
        new(
            Options.Create(new CloudSyncOptions { QueueCapacity = capacity }),
            logger ?? NullLogger<ChannelNotificationQueue>.Instance
        );

    [TestMethod]
    public async Task Enqueue_then_dequeue_round_trips_both_shapes_in_fifo_order()
    {
        // Arrange
        var queue = Queue(capacity: 10);
        var change = new NotificationWorkItem("paula@contoso.com", "msg-1", "created");
        var lifecycle = new LifecycleWorkItem("sub-1", LifecycleEvents.Removed);

        // Act
        queue.TryEnqueue(change).Should().BeTrue();
        queue.TryEnqueue(lifecycle).Should().BeTrue();
        var first = await queue.DequeueAsync(CancellationToken.None);
        var second = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        first.Should().Be(change, "the queue is FIFO");
        second.Should().Be(lifecycle, "both work-item shapes travel on the same queue");
    }

    [TestMethod]
    public async Task Enqueue_at_capacity_drops_the_write_without_blocking_and_logs_warning()
    {
        // Arrange
        var logger = new CapturingLogger();
        var queue = Queue(capacity: 1, logger);
        var kept = new NotificationWorkItem("paula@contoso.com", "msg-keep", "created");
        var dropped = new NotificationWorkItem("paula@contoso.com", "msg-drop", "created");

        // Act: both calls return synchronously; the second write is dropped.
        var firstAccepted = queue.TryEnqueue(kept);
        var secondAccepted = queue.TryEnqueue(dropped);

        // Assert
        firstAccepted.Should().BeTrue("the first write fits within capacity");
        secondAccepted.Should().BeTrue("DropWrite accepts the call without blocking");
        logger
            .Entries.Should()
            .ContainSingle("exactly one dropped write occurred")
            .Which.Level.Should()
            .Be(LogLevel.Warning);
        logger
            .Entries[0]
            .Message.Should()
            .Contain(nameof(NotificationWorkItem), "the log names the dropped item kind");

        var remaining = await queue.DequeueAsync(CancellationToken.None);
        remaining.Should().Be(kept, "the newest write is the one dropped at capacity");
    }

    [TestMethod]
    public async Task Pending_dequeue_completes_when_an_item_arrives()
    {
        // Arrange
        var queue = Queue(capacity: 4);
        var item = new LifecycleWorkItem("sub-9", LifecycleEvents.Missed);

        // Act: start the read before any item exists, then satisfy it by enqueueing.
        var pending = queue.DequeueAsync(CancellationToken.None).AsTask();
        pending.IsCompleted.Should().BeFalse("nothing has been enqueued yet");
        queue.TryEnqueue(item).Should().BeTrue();
        var result = await pending;

        // Assert
        result.Should().Be(item, "the pending read completes with the enqueued item");
    }

    /// <summary>
    /// A minimal capturing logger (Moq cannot proxy <c>ILogger&lt;T&gt;</c> closed
    /// over an internal type without granting InternalsVisibleTo to DynamicProxy).
    /// </summary>
    private sealed class CapturingLogger : ILogger<ChannelNotificationQueue>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
