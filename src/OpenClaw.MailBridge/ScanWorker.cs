using Microsoft.Extensions.Hosting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Periodically runs the Outlook scan workflow on the dedicated STA executor.
/// </summary>
internal sealed class ScanWorker(
    IOutlookStaExecutor sta,
    IOutlookScanner scanner,
    IScanStateRepository repo,
    BridgeSettings settings
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repo.InitializeAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await sta.InvokeAsync(() =>
            {
                scanner.ScanAsync(repo).GetAwaiter().GetResult();
                return 0;
            });
            await Task.Delay(TimeSpan.FromSeconds(settings.InboxPollSeconds), stoppingToken);
        }
    }
}
