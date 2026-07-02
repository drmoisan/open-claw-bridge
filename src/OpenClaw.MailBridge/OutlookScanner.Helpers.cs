using Microsoft.Extensions.Logging;

namespace OpenClaw.MailBridge;

/// <summary>
/// Outlook COM resolution and restriction-filter helpers for <see cref="OutlookScanner"/>.
/// Split into a partial file to keep each source file under the 500-line cap; COM interop remains
/// confined to <c>OpenClaw.MailBridge</c>.
/// </summary>
internal sealed partial class OutlookScanner
{
    private object GetNamespace(object outlookApp) =>
        OutlookComHelpers.InvokeMember(outlookApp, "GetNamespace", "MAPI")
        ?? throw new InvalidOperationException("Outlook MAPI namespace was unavailable.");

    private object? ResolveDefaultFolder(
        object outlookNamespace,
        int folderType,
        string staleReason
    )
    {
        try
        {
            return OutlookComHelpers.InvokeMember(outlookNamespace, "GetDefaultFolder", folderType);
        }
        catch (Exception ex)
        {
            _state.MarkOutlookUnavailable(staleReason);
            _logger.LogWarning(
                "Required Outlook folder {FolderType} was unavailable: {Message}",
                folderType,
                ex.Message
            );
            return null;
        }
    }

    private string BuildInboxFilter(
        DateTimeOffset? lastSuccessfulInboxScan,
        int inboxOverlapMinutes
    )
    {
        var lastScanUtc = lastSuccessfulInboxScan ?? _utcNow().AddDays(-1);
        var filterStartUtc = lastScanUtc.AddMinutes(-inboxOverlapMinutes);
        return $"[ReceivedTime] >= '{filterStartUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}'";
    }

    private string BuildCalendarFilter(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        $"[Start] < '{endUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}' AND [End] > '{startUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}'";
}
