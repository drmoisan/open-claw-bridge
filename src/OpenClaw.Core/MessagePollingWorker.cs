using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core;

internal sealed class MessagePollingWorker(
    IHostAdapterClient hostAdapterClient,
    CoreCacheRepository repository,
    CoreHealthState healthState,
    IOptions<OpenClawOptions> optionsAccessor,
    ILogger<MessagePollingWorker> logger
) : BackgroundService
{
    private readonly OpenClawOptions options = optionsAccessor.Value;

    internal Task RunMessagePollOnceAsync(
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default
    ) => PollMessagesAsync(startedAtUtc, cancellationToken);

    internal Task RunMeetingRequestPollOnceAsync(
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default
    ) => PollMeetingRequestsAsync(startedAtUtc, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeRepositoryAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            await PollMessagesAsync(startedAtUtc, stoppingToken);
            await PollMeetingRequestsAsync(startedAtUtc, stoppingToken);

            var nextDelaySeconds = Math.Min(
                options.Polling.MessagesIntervalSeconds,
                options.Polling.MeetingRequestsIntervalSeconds
            );
            await Task.Delay(TimeSpan.FromSeconds(nextDelaySeconds), stoppingToken);
        }
    }

    private async Task InitializeRepositoryAsync()
    {
        try
        {
            await repository.InitializeAsync();
            healthState.MarkDatabaseReady();
        }
        catch (Exception exception)
        {
            healthState.MarkDatabaseFailure(exception.Message);
            throw;
        }
    }

    private async Task PollMessagesAsync(
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken
    )
    {
        var requestId = Guid.NewGuid().ToString();
        var sinceUtc =
            await repository.GetCursorAsync("messages_since_utc")
            ?? DateTimeOffset.UtcNow.AddHours(-options.Polling.MessageLookbackHours);
        var envelope = await hostAdapterClient.ListMessagesAsync(
            sinceUtc,
            options.Defaults.Limit,
            requestId,
            cancellationToken
        );
        await PersistPollResultAsync(
            "messages",
            envelope,
            startedAtUtc,
            cancellationToken,
            async bridgeStatus =>
            {
                var observedAtUtc = DateTimeOffset.UtcNow;
                await repository.UpsertBridgeStatusSnapshotAsync(
                    bridgeStatus,
                    envelope.Meta.RequestId,
                    observedAtUtc
                );
                var messages = envelope.Data?.Items ?? Array.Empty<MessageDto>();
                await repository.UpsertMessagesAsync(
                    messages,
                    bridgeStatus,
                    envelope.Meta.RequestId,
                    observedAtUtc
                );
                var nextCursor =
                    messages.MaxBy(message => message.ReceivedUtc ?? message.SentUtc)?.ReceivedUtc
                    ?? messages.MaxBy(message => message.ReceivedUtc ?? message.SentUtc)?.SentUtc
                    ?? observedAtUtc;
                await repository.SetCursorAsync("messages_since_utc", nextCursor);
            }
        );
    }

    private async Task PollMeetingRequestsAsync(
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken
    )
    {
        var requestId = Guid.NewGuid().ToString();
        var sinceUtc =
            await repository.GetCursorAsync("meeting_requests_since_utc")
            ?? DateTimeOffset.UtcNow.AddHours(-options.Polling.MessageLookbackHours);
        var envelope = await hostAdapterClient.ListMeetingRequestsAsync(
            sinceUtc,
            options.Defaults.Limit,
            requestId,
            cancellationToken
        );
        await PersistPollResultAsync(
            "meeting_requests",
            envelope,
            startedAtUtc,
            cancellationToken,
            async bridgeStatus =>
            {
                var observedAtUtc = DateTimeOffset.UtcNow;
                await repository.UpsertBridgeStatusSnapshotAsync(
                    bridgeStatus,
                    envelope.Meta.RequestId,
                    observedAtUtc
                );
                var items = envelope.Data?.Items ?? Array.Empty<MessageDto>();
                await repository.UpsertMessagesAsync(
                    items,
                    bridgeStatus,
                    envelope.Meta.RequestId,
                    observedAtUtc
                );
                var nextCursor =
                    items.MaxBy(message => message.ReceivedUtc ?? message.SentUtc)?.ReceivedUtc
                    ?? items.MaxBy(message => message.ReceivedUtc ?? message.SentUtc)?.SentUtc
                    ?? observedAtUtc;
                await repository.SetCursorAsync("meeting_requests_since_utc", nextCursor);
            }
        );
    }

    private async Task PersistPollResultAsync<T>(
        string operationName,
        ApiEnvelope<T> envelope,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken,
        Func<BridgeStatusDto, Task> onSuccess
    )
    {
        var finishedAtUtc = DateTimeOffset.UtcNow;
        if (envelope.Ok && envelope.Meta.Bridge is not null)
        {
            await onSuccess(envelope.Meta.Bridge);
            healthState.MarkPollSuccess(envelope.Meta.Bridge, finishedAtUtc);
            await repository.AddIngestRunAsync(
                operationName,
                "success",
                envelope.Meta.RequestId,
                startedAtUtc,
                finishedAtUtc,
                null
            );
            return;
        }

        logger.LogWarning(
            "Core poll {OperationName} failed for request {RequestId}: {Message}",
            operationName,
            envelope.Meta.RequestId,
            envelope.Error?.Message ?? "Unknown HostAdapter failure."
        );
        healthState.MarkPollFailure(
            envelope.Error?.Message ?? "Unknown HostAdapter failure.",
            envelope.Meta.Bridge
        );
        if (envelope.Meta.Bridge is not null)
        {
            await repository.UpsertBridgeStatusSnapshotAsync(
                envelope.Meta.Bridge,
                envelope.Meta.RequestId,
                finishedAtUtc
            );
        }

        await repository.AddIngestRunAsync(
            operationName,
            "failed",
            envelope.Meta.RequestId,
            startedAtUtc,
            finishedAtUtc,
            envelope.Error?.Message
        );
    }
}
