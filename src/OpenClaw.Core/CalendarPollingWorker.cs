using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core;

internal sealed class CalendarPollingWorker(
    IHostAdapterClient hostAdapterClient,
    CoreCacheRepository repository,
    CoreHealthState healthState,
    IOptions<OpenClawOptions> optionsAccessor,
    ILogger<CalendarPollingWorker> logger
) : BackgroundService
{
    private readonly OpenClawOptions options = optionsAccessor.Value;

    internal async Task RunCalendarPollOnceAsync(CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid().ToString();
        var startUtc = DateTimeOffset.UtcNow.AddDays(-options.Polling.CalendarPastDays);
        var endUtc = DateTimeOffset.UtcNow.AddDays(options.Polling.CalendarFutureDays);
        var envelope = await hostAdapterClient.ListCalendarWindowAsync(
            startUtc,
            endUtc,
            options.Defaults.Limit,
            requestId,
            cancellationToken
        );
        var finishedAtUtc = DateTimeOffset.UtcNow;

        if (envelope.Ok && envelope.Meta.Bridge is not null)
        {
            await repository.UpsertBridgeStatusSnapshotAsync(
                envelope.Meta.Bridge,
                envelope.Meta.RequestId,
                finishedAtUtc
            );
            await repository.UpsertEventsAsync(
                envelope.Data?.Items
                    ?? Array.Empty<OpenClaw.MailBridge.Contracts.Models.EventDto>(),
                envelope.Meta.Bridge,
                envelope.Meta.RequestId,
                finishedAtUtc
            );
            await repository.SetCursorAsync("calendar_window_last_run_utc", finishedAtUtc);
            await repository.AddIngestRunAsync(
                "calendar_window",
                "success",
                envelope.Meta.RequestId,
                startedAtUtc,
                finishedAtUtc,
                null
            );
            healthState.MarkPollSuccess(envelope.Meta.Bridge, finishedAtUtc);
            return;
        }

        logger.LogWarning(
            "Core calendar poll failed for request {RequestId}: {Message}",
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
            "calendar_window",
            "failed",
            envelope.Meta.RequestId,
            startedAtUtc,
            finishedAtUtc,
            envelope.Error?.Message
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repository.InitializeAsync();
        healthState.MarkDatabaseReady();

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var requestId = Guid.NewGuid().ToString();
            var startUtc = DateTimeOffset.UtcNow.AddDays(-options.Polling.CalendarPastDays);
            var endUtc = DateTimeOffset.UtcNow.AddDays(options.Polling.CalendarFutureDays);
            var envelope = await hostAdapterClient.ListCalendarWindowAsync(
                startUtc,
                endUtc,
                options.Defaults.Limit,
                requestId,
                stoppingToken
            );
            var finishedAtUtc = DateTimeOffset.UtcNow;

            if (envelope.Ok && envelope.Meta.Bridge is not null)
            {
                await repository.UpsertBridgeStatusSnapshotAsync(
                    envelope.Meta.Bridge,
                    envelope.Meta.RequestId,
                    finishedAtUtc
                );
                await repository.UpsertEventsAsync(
                    envelope.Data?.Items
                        ?? Array.Empty<OpenClaw.MailBridge.Contracts.Models.EventDto>(),
                    envelope.Meta.Bridge,
                    envelope.Meta.RequestId,
                    finishedAtUtc
                );
                await repository.SetCursorAsync("calendar_window_last_run_utc", finishedAtUtc);
                await repository.AddIngestRunAsync(
                    "calendar_window",
                    "success",
                    envelope.Meta.RequestId,
                    startedAtUtc,
                    finishedAtUtc,
                    null
                );
                healthState.MarkPollSuccess(envelope.Meta.Bridge, finishedAtUtc);
            }
            else
            {
                logger.LogWarning(
                    "Core calendar poll failed for request {RequestId}: {Message}",
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
                    "calendar_window",
                    "failed",
                    envelope.Meta.RequestId,
                    startedAtUtc,
                    finishedAtUtc,
                    envelope.Error?.Message
                );
            }

            await Task.Delay(
                TimeSpan.FromSeconds(options.Polling.CalendarIntervalSeconds),
                stoppingToken
            );
        }
    }
}
