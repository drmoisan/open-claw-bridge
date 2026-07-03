using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Keeps the principal-Inbox Graph subscription alive (master §6.1): an immediate
/// startup sweep creates a subscription when none exists, recreates expired ones
/// (expired-while-down recovery), and renews due ones; the sweep then repeats on a
/// <see cref="TimeProvider"/>-driven schedule. Zero direct clock reads; every delay
/// flows through the injected <see cref="TimeProvider"/> and is cancellable.
/// </summary>
internal sealed class SubscriptionRenewalWorker(
    GraphSubscriptionManager manager,
    ISubscriptionStore subscriptionStore,
    IOptions<CloudSyncOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<SubscriptionRenewalWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Pure schedule computation: re-check every half renewal lead (minimum one
    /// minute) so a subscription entering its renewal window is always seen before
    /// expiration.
    /// </summary>
    /// <param name="renewalLeadMinutes">The configured renewal lead.</param>
    internal static TimeSpan ComputeCheckInterval(int renewalLeadMinutes) =>
        TimeSpan.FromMinutes(Math.Max(1.0, renewalLeadMinutes / 2.0));

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = ComputeCheckInterval(optionsAccessor.Value.RenewalLeadMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Subscription renewal sweep failed; the next scheduled sweep will retry."
                );
            }

            try
            {
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One sweep: create when no subscription is stored, recreate expired
    /// subscriptions, renew due ones (renew-vs-recreate decided from the stored
    /// expiration and the pure <see cref="GraphSubscriptionManager.IsRenewalDue"/>).
    /// </summary>
    /// <param name="ct">Cancels the sweep.</param>
    internal async Task RunSweepOnceAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var lead = optionsAccessor.Value.RenewalLeadMinutes;
        var subscriptions = await subscriptionStore.ListSubscriptionsAsync(ct);
        if (subscriptions.Count == 0)
        {
            logger.LogInformation("No Graph subscription is stored; creating one.");
            await manager.CreateAsync(null, ct);
            return;
        }

        foreach (var subscription in subscriptions)
        {
            if (now >= subscription.ExpirationUtc)
            {
                logger.LogInformation(
                    "Graph subscription {SubscriptionId} expired at {ExpirationUtc:O}; recreating.",
                    subscription.SubscriptionId,
                    subscription.ExpirationUtc
                );
                await subscriptionStore.DeleteSubscriptionAsync(subscription.SubscriptionId, ct);
                await manager.CreateAsync(null, ct);
            }
            else if (GraphSubscriptionManager.IsRenewalDue(now, subscription.ExpirationUtc, lead))
            {
                await manager.RenewAsync(subscription.SubscriptionId, null, ct);
            }
        }
    }
}
