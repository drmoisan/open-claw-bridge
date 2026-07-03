using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Runs <see cref="GraphDeltaReconciler.ReconcileAsync"/> for the principal mailbox on
/// a periodic <see cref="CloudSyncOptions.ReconcileIntervalMinutes"/> schedule driven
/// entirely by the injected <see cref="TimeProvider"/> (master §6.2: webhooks are wake
/// signals, delta is truth). A failing reconcile logs a Warning and the loop
/// continues; zero direct clock reads; every wait is cancellable.
/// </summary>
internal sealed class DeltaReconciliationWorker(
    GraphDeltaReconciler reconciler,
    IOptions<GraphAdapterOptions> graphOptionsAccessor,
    IOptions<CloudSyncOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<DeltaReconciliationWorker> logger
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(optionsAccessor.Value.ReconcileIntervalMinutes);
        var mailbox = graphOptionsAccessor.Value.PrincipalMailboxUpn;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await reconciler.ReconcileAsync(mailbox, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Delta reconciliation for {Mailbox} failed; the next scheduled tick will retry.",
                    mailbox
                );
            }
        }
    }
}
