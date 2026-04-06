using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Periodically runs the Outlook scan workflow on the dedicated STA executor.
/// </summary>
/// <param name="sta">Executor that serializes Outlook COM work onto an STA thread.</param>
/// <param name="scanner">Scanner that refreshes Outlook-derived cache metadata.</param>
/// <param name="repo">Repository that persists scan-state timestamps.</param>
/// <param name="settings">Settings that define the scan interval.</param>
[ExcludeFromCodeCoverage]
internal sealed class ScanWorker(
    OutlookStaExecutor sta,
    OutlookScanner scanner,
    CacheRepository repo,
    BridgeSettings settings
) : BackgroundService
{
    /// <summary>
    /// Initializes the cache and then repeats scan cycles until the host is asked to stop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token signaled during host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repo.InitializeAsync();

        // Keep scanning on a fixed cadence so the bridge status reflects Outlook health continuously.
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
