using Microsoft.Extensions.Hosting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Periodically runs the Outlook scan workflow on the dedicated STA executor.
/// </summary>
internal sealed class ScanWorker(
    IOutlookStaExecutor sta,
    IOutlookScanner scanner,
    IBridgeRepository repo,
    BridgeSettings settings
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repo.InitializeAsync();

        var nextInboxScanUtc = DateTimeOffset.UtcNow;
        var nextCalendarScanUtc = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            if (now >= nextInboxScanUtc)
            {
                await sta.InvokeAsync(() =>
                {
                    scanner.ScanInboxAsync(repo).GetAwaiter().GetResult();
                    return 0;
                });
                nextInboxScanUtc = now.AddSeconds(settings.InboxPollSeconds);
            }

            if (now >= nextCalendarScanUtc)
            {
                await sta.InvokeAsync(() =>
                {
                    scanner.ScanCalendarAsync(repo).GetAwaiter().GetResult();
                    return 0;
                });
                nextCalendarScanUtc = now.AddSeconds(settings.CalendarPollSeconds);
            }

            var nextDueUtc =
                nextInboxScanUtc < nextCalendarScanUtc ? nextInboxScanUtc : nextCalendarScanUtc;
            var delay = nextDueUtc - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.FromMilliseconds(50))
            {
                delay = TimeSpan.FromMilliseconds(50);
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
