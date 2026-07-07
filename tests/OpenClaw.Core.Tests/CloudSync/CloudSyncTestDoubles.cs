using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Shared hand-rolled test doubles for the CloudSync suite. Moq cannot proxy internal
/// interfaces or <c>ILogger&lt;T&gt;</c> closed over internal types without granting
/// InternalsVisibleTo to DynamicProxy, so these fakes implement the internal CloudSync
/// seams directly.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    /// <summary>The captured levels, for concise assertions.</summary>
    public IEnumerable<LogLevel> Levels => Entries.Select(e => e.Level);

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

/// <summary>
/// In-memory <see cref="ISubscriptionStore"/> recording every mutation for
/// assertions. All operations are synchronous over a dictionary.
/// </summary>
internal sealed class FakeSubscriptionStore : ISubscriptionStore
{
    public Dictionary<string, GraphSubscriptionRecord> Records { get; } =
        new(StringComparer.Ordinal);

    public List<GraphSubscriptionRecord> Upserts { get; } = [];

    public List<(string SubscriptionId, string Status)> StatusUpdates { get; } = [];

    public List<string> Deletes { get; } = [];

    public Task<GraphSubscriptionRecord?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken ct
    ) => Task.FromResult(Records.GetValueOrDefault(subscriptionId));

    public Task<IReadOnlyList<GraphSubscriptionRecord>> ListSubscriptionsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult<IReadOnlyList<GraphSubscriptionRecord>>(
            Records.Values.OrderBy(r => r.SubscriptionId, StringComparer.Ordinal).ToList()
        );

    public Task UpsertSubscriptionAsync(
        GraphSubscriptionRecord record,
        DateTimeOffset nowUtc,
        CancellationToken ct
    )
    {
        Upserts.Add(record);
        Records[record.SubscriptionId] = record;
        return Task.CompletedTask;
    }

    public Task UpdateSubscriptionStatusAsync(
        string subscriptionId,
        string status,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct
    )
    {
        StatusUpdates.Add((subscriptionId, status));
        if (Records.TryGetValue(subscriptionId, out var record))
        {
            Records[subscriptionId] = record with { Status = status };
        }

        return Task.CompletedTask;
    }

    public Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        Deletes.Add(subscriptionId);
        Records.Remove(subscriptionId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Recording <see cref="INotificationQueue"/> over an unbounded channel: every
/// enqueued item is captured in <see cref="Enqueued"/> and also readable through
/// <see cref="DequeueAsync"/>.
/// </summary>
internal sealed class RecordingNotificationQueue : INotificationQueue
{
    private readonly Channel<CloudSyncWorkItem> channel =
        Channel.CreateUnbounded<CloudSyncWorkItem>();

    public List<CloudSyncWorkItem> Enqueued { get; } = [];

    public bool TryEnqueue(CloudSyncWorkItem item)
    {
        Enqueued.Add(item);
        return channel.Writer.TryWrite(item);
    }

    public ValueTask<CloudSyncWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAsync(cancellationToken);
}

/// <summary>Deterministic <see cref="IClientStateGenerator"/> returning a fixed value.</summary>
internal sealed class FixedClientStateGenerator(string value) : IClientStateGenerator
{
    /// <summary>How many times <see cref="Generate"/> was called.</summary>
    public int Calls { get; private set; }

    public string Generate()
    {
        Calls++;
        return value;
    }
}

/// <summary>Recording <see cref="IDeltaReconcileTrigger"/> capturing triggered mailboxes.</summary>
internal sealed class RecordingReconcileTrigger : IDeltaReconcileTrigger
{
    public List<string> TriggeredMailboxes { get; } = [];

    public Task TriggerResyncAsync(string mailbox, CancellationToken ct)
    {
        TriggeredMailboxes.Add(mailbox);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="IDeltaLinkStore"/> recording every set for assertions.
/// </summary>
internal sealed class FakeDeltaLinkStore : IDeltaLinkStore
{
    public Dictionary<string, string> Links { get; } = new(StringComparer.Ordinal);

    public List<(string Mailbox, string DeltaLink)> Sets { get; } = [];

    public Task<string?> GetDeltaLinkAsync(string mailbox, CancellationToken ct) =>
        Task.FromResult(Links.GetValueOrDefault(mailbox));

    public Task SetDeltaLinkAsync(
        string mailbox,
        string deltaLink,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct
    )
    {
        Sets.Add((mailbox, deltaLink));
        Links[mailbox] = deltaLink;
        return Task.CompletedTask;
    }
}
