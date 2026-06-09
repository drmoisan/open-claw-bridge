using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

internal interface IOutlookScanner
{
    Task ScanAsync(IBridgeRepository repo);
    Task ScanInboxAsync(IBridgeRepository repo);
    Task ScanCalendarAsync(IBridgeRepository repo);
}

/// <summary>
/// Coordinates Outlook discovery and updates the cache metadata that signals scan freshness.
/// </summary>
internal sealed partial class OutlookScanner : IOutlookScanner
{
    private readonly BridgeSettings _settings;
    private readonly BridgeStateStore _state;
    private readonly ILogger<OutlookScanner> _logger;
    private readonly ComActiveObject _com;
    private readonly Func<string, int> _processCount;
    private readonly Func<DateTimeOffset> _utcNow;
    private object? _outlookApp;

    public OutlookScanner(
        BridgeSettings settings,
        BridgeStateStore state,
        ILogger<OutlookScanner> logger
    )
        : this(
            settings,
            state,
            logger,
            new ComActiveObject(),
            name => Process.GetProcessesByName(name).Length,
            () => DateTimeOffset.UtcNow
        ) { }

    internal OutlookScanner(
        BridgeSettings settings,
        BridgeStateStore state,
        ILogger<OutlookScanner> logger,
        ComActiveObject com,
        Func<string, int> processCount,
        Func<DateTimeOffset> utcNow
    )
    {
        _settings = settings;
        _state = state;
        _logger = logger;
        _com = com;
        _processCount = processCount;
        _utcNow = utcNow;
    }

    public async Task ScanAsync(IBridgeRepository repo)
    {
        await ExecuteScanAsync(repo, scanInbox: true, scanCalendar: true);
    }

    public async Task ScanInboxAsync(IBridgeRepository repo) =>
        await ExecuteScanAsync(repo, scanInbox: true, scanCalendar: false);

    public async Task ScanCalendarAsync(IBridgeRepository repo) =>
        await ExecuteScanAsync(repo, scanInbox: false, scanCalendar: true);

    internal void EnsureOutlook()
    {
        if (_outlookApp is not null)
            return;

        _outlookApp = _com.TryGet("Outlook.Application");
        if (_outlookApp is not null)
        {
            _logger.LogInformation("Attached to a running Outlook instance.");
            return;
        }

        var outlookRunning = _processCount("OUTLOOK") > 0;
        if (!_settings.AutostartOutlook)
        {
            if (outlookRunning)
            {
                _state.MarkOutlookUnavailable("running_instance_unavailable");
                return;
            }

            _state.SetState(BridgeState.waiting_for_outlook);
            _state.OutlookConnected = false;
            return;
        }

        try
        {
            _outlookApp = _com.CreateAndLogonOutlook();
            _logger.LogInformation(
                "Created and logged on to Outlook because autostart is enabled."
            );
        }
        catch (Exception ex)
        {
            // Autostart logon can fail even when no Outlook process is running (for example a
            // headless MAPI Logon with no logged-on session). Handle the failure deterministically
            // for both cases so the bridge never remains stuck in the starting state.
            if (outlookRunning)
            {
                _state.MarkOutlookUnavailable("running_instance_unavailable");
            }
            else
            {
                _state.SetState(BridgeState.waiting_for_outlook);
                _state.OutlookConnected = false;
            }

            _logger.LogWarning(
                "Unable to attach to the running Outlook instance or create a fallback session: {Message}",
                ex.Message
            );
        }
    }

    private async Task ExecuteScanAsync(IBridgeRepository repo, bool scanInbox, bool scanCalendar)
    {
        object? outlookNamespace = null;
        object? inboxFolder = null;
        object? calendarFolder = null;

        try
        {
            EnsureOutlook();
            _state.OutlookConnected = _outlookApp is not null;
            if (_outlookApp is null)
            {
                return;
            }

            outlookNamespace = GetNamespace(_outlookApp);
            if (scanInbox)
            {
                inboxFolder = ResolveDefaultFolder(
                    outlookNamespace,
                    6,
                    "default_inbox_unavailable"
                );
                if (inboxFolder is null)
                {
                    return;
                }
            }

            if (scanCalendar)
            {
                calendarFolder = ResolveDefaultFolder(
                    outlookNamespace,
                    9,
                    "default_calendar_unavailable"
                );
                if (calendarFolder is null)
                {
                    return;
                }
            }

            DateTimeOffset? lastInboxScanUtc = _state.LastInboxScanUtc;
            DateTimeOffset? lastCalendarScanUtc = _state.LastCalendarScanUtc;

            if (scanInbox && inboxFolder is not null)
            {
                lastInboxScanUtc = await ScanInboxFolderAsync(repo, inboxFolder);
                await repo.TouchScanStateAsync("last_inbox_scan_utc", lastInboxScanUtc.Value);
            }

            if (scanCalendar && calendarFolder is not null)
            {
                lastCalendarScanUtc = await ScanCalendarFolderAsync(repo, calendarFolder);
                await repo.TouchScanStateAsync("last_calendar_scan_utc", lastCalendarScanUtc.Value);
            }

            var lastSuccessfulScanUtc = new[] { lastInboxScanUtc, lastCalendarScanUtc }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty(_utcNow())
                .Max();
            await repo.TouchScanStateAsync("last_successful_scan_utc", lastSuccessfulScanUtc);

            _state.MarkReady(lastInboxScanUtc, lastCalendarScanUtc);
        }
        catch (Exception ex)
        {
            _state.MarkCacheStale("scan_failure");
            _logger.LogError("Scan failed: {Message}", ex.Message);
        }
        finally
        {
            _com.ReleaseAll(calendarFolder, inboxFolder, outlookNamespace, _outlookApp);
            _outlookApp = null;
        }
    }

    private async Task<DateTimeOffset> ScanInboxFolderAsync(
        IBridgeRepository repo,
        object inboxFolder
    )
    {
        object? items = null;
        object? restrictedItems = null;
        var processedCount = 0;

        try
        {
            items = OutlookComHelpers.GetMemberValue(inboxFolder, "Items");
            if (items is null)
            {
                throw new InvalidOperationException("Inbox items collection was unavailable.");
            }

            var lastSuccessfulInboxScan = await repo.GetScanStateAsync("last_inbox_scan_utc");
            var filter = BuildInboxFilter(lastSuccessfulInboxScan, _settings.InboxOverlapMinutes);
            restrictedItems = OutlookComHelpers.InvokeMember(items, "Restrict", filter);
            if (restrictedItems is null)
            {
                throw new InvalidOperationException(
                    "Inbox restricted items collection was unavailable."
                );
            }

            foreach (var item in EnumerateItems(restrictedItems, _settings.MaxItemsPerScan))
            {
                var message = NormalizeMessage(item);
                if (message is null)
                {
                    continue;
                }

                await repo.UpsertMessageAsync(message.EntryId, message.StoreId, message.Dto);
                processedCount++;
            }

            _logger.LogInformation(
                "Inbox scan completed with {ProcessedCount} item(s).",
                processedCount
            );
            return _utcNow();
        }
        finally
        {
            _com.ReleaseAll(restrictedItems, items);
        }
    }

    private async Task<DateTimeOffset> ScanCalendarFolderAsync(
        IBridgeRepository repo,
        object calendarFolder
    )
    {
        object? items = null;
        object? restrictedItems = null;
        var processedCount = 0;

        try
        {
            items = OutlookComHelpers.GetMemberValue(calendarFolder, "Items");
            if (items is null)
            {
                throw new InvalidOperationException("Calendar items collection was unavailable.");
            }

            OutlookComHelpers.InvokeMember(items, "Sort", "[Start]");
            OutlookComHelpers.SetMemberValue(items, "IncludeRecurrences", true);

            var windowStartUtc = _utcNow().AddDays(-_settings.CalendarPastDays);
            var windowEndUtc = _utcNow().AddDays(_settings.CalendarFutureDays);
            var filter = BuildCalendarFilter(windowStartUtc, windowEndUtc);
            restrictedItems = OutlookComHelpers.InvokeMember(items, "Restrict", filter);
            if (restrictedItems is null)
            {
                throw new InvalidOperationException(
                    "Calendar restricted items collection was unavailable."
                );
            }

            foreach (var item in EnumerateItems(restrictedItems, _settings.MaxItemsPerScan))
            {
                var evt = NormalizeEvent(item);
                if (evt is null)
                {
                    continue;
                }

                await repo.UpsertEventAsync(
                    evt.EntryId,
                    evt.StoreId,
                    evt.GlobalAppointmentId,
                    evt.Dto
                );
                processedCount++;
            }

            _logger.LogInformation(
                "Calendar scan completed with {ProcessedCount} item(s).",
                processedCount
            );
            return _utcNow();
        }
        finally
        {
            _com.ReleaseAll(restrictedItems, items);
        }
    }

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
        $"[Start] >= '{startUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}' AND [Start] < '{endUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}'";

    private IEnumerable<object> EnumerateItems(object items, int maxItems)
    {
        if (items is not IEnumerable enumerable)
        {
            yield break;
        }

        var count = 0;
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            yield return item;
            count++;

            // Yield control back to Outlook's UI thread at batch boundaries
            // to prevent COM cross-apartment call starvation.
            if (count > 0 && count % _settings.ComYieldBatchSize == 0)
            {
                Thread.Sleep(_settings.ComYieldMilliseconds);
            }

            if (count >= maxItems)
            {
                yield break;
            }
        }
    }

    private NormalizedMessage? NormalizeMessage(object item)
    {
        var entryId = OutlookComHelpers.GetOptionalString(item, "EntryID");
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return null;
        }

        var messageClass = OutlookComHelpers.GetOptionalString(item, "MessageClass");
        var isMeeting = IsMeetingItem(item, messageClass);
        var bridgeId = BridgeIdCodec.MessageId(entryId, isMeeting);
        var storeId = GetStoreId(item);
        var senderName = OutlookComHelpers.GetOptionalString(item, "SenderName");
        var senderEmail =
            OutlookComHelpers.GetOptionalString(item, "SenderEmailAddress")
            ?? OutlookComHelpers.GetOptionalString(item, "SenderEmailType");
        var bodyPreview = ResponseShaper.ShapePreview(
            OutlookComHelpers.GetOptionalString(item, "Body"),
            _settings
        );
        var dto = new MessageDto(
            bridgeId,
            isMeeting ? "meeting" : "mail",
            OutlookComHelpers.GetOptionalString(item, "Subject"),
            OutlookComHelpers.GetOptionalDateTimeOffset(item, "ReceivedTime"),
            OutlookComHelpers.GetOptionalDateTimeOffset(item, "SentOn"),
            OutlookComHelpers.GetOptionalInt(item, "Importance"),
            OutlookComHelpers.GetOptionalInt(item, "Sensitivity"),
            OutlookComHelpers.GetOptionalBool(item, "Unread"),
            OutlookComHelpers.GetOptionalBool(item, "Attachments")
                || OutlookComHelpers.GetOptionalBool(item, "HasAttachments"),
            messageClass,
            senderName,
            senderEmail,
            null,
            null,
            bodyPreview,
            !string.IsNullOrWhiteSpace(senderName)
                || !string.IsNullOrWhiteSpace(senderEmail)
                || !string.IsNullOrWhiteSpace(bodyPreview),
            false
        );

        return new NormalizedMessage(entryId, storeId, dto);
    }

    private NormalizedEvent? NormalizeEvent(object item)
    {
        var entryId = OutlookComHelpers.GetOptionalString(item, "EntryID");
        var startUtc =
            OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "StartUTC")
            ?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "Start");
        var endUtc =
            OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "EndUTC")
            ?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "End");
        if (string.IsNullOrWhiteSpace(entryId) || startUtc is null || endUtc is null)
        {
            return null;
        }

        var globalAppointmentId = OutlookComHelpers.GetOptionalString(item, "GlobalAppointmentID");
        var bridgeId = BridgeIdCodec.EventId(globalAppointmentId, entryId, startUtc.Value);
        var dto = new EventDto(
            bridgeId,
            globalAppointmentId,
            OutlookComHelpers.GetOptionalString(item, "Subject"),
            startUtc.Value,
            endUtc.Value,
            OutlookComHelpers.GetOptionalString(item, "Location"),
            OutlookComHelpers.GetOptionalInt(item, "BusyStatus"),
            OutlookComHelpers.GetOptionalInt(item, "MeetingStatus"),
            OutlookComHelpers.GetOptionalBool(item, "IsRecurring"),
            OutlookComHelpers.GetOptionalInt(item, "Sensitivity"),
            OutlookComHelpers.GetOptionalString(item, "Organizer"),
            null,
            null,
            null,
            ResponseShaper.ShapePreview(
                OutlookComHelpers.GetOptionalString(item, "Body"),
                _settings
            ),
            !string.IsNullOrWhiteSpace(OutlookComHelpers.GetOptionalString(item, "Body")),
            false,
            OutlookComHelpers.GetOptionalInt(item, "ResponseStatus")
        );

        return new NormalizedEvent(entryId, GetStoreId(item), globalAppointmentId, dto);
    }

    private static bool IsMeetingItem(object item, string? messageClass)
    {
        if (item.GetType().Name.Contains("Meeting", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(messageClass)
            && messageClass.Contains("Meeting", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetStoreId(object item)
    {
        object? parent = null;
        object? store = null;
        try
        {
            parent = OutlookComHelpers.GetOptionalMemberValue(item, "Parent");
            if (parent is null)
            {
                return null;
            }

            store = OutlookComHelpers.GetOptionalMemberValue(parent, "Store");
            return store is null
                ? OutlookComHelpers.GetOptionalString(parent, "StoreID")
                : OutlookComHelpers.GetOptionalString(store, "StoreID");
        }
        finally
        {
            _com.ReleaseAll(store, parent);
        }
    }
}
