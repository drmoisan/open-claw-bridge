using System.Collections;
using System.Diagnostics;
using System.Reflection;
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
internal sealed class OutlookScanner : IOutlookScanner
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
        catch (Exception ex) when (outlookRunning)
        {
            _state.MarkOutlookUnavailable("running_instance_unavailable");
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
            items = GetMemberValue(inboxFolder, "Items");
            if (items is null)
            {
                throw new InvalidOperationException("Inbox items collection was unavailable.");
            }

            var lastSuccessfulInboxScan = await repo.GetScanStateAsync("last_inbox_scan_utc");
            var filter = BuildInboxFilter(lastSuccessfulInboxScan, _settings.InboxOverlapMinutes);
            restrictedItems = InvokeMember(items, "Restrict", filter);
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
            items = GetMemberValue(calendarFolder, "Items");
            if (items is null)
            {
                throw new InvalidOperationException("Calendar items collection was unavailable.");
            }

            InvokeMember(items, "Sort", "[Start]");
            SetMemberValue(items, "IncludeRecurrences", true);

            var windowStartUtc = _utcNow().AddDays(-_settings.CalendarPastDays);
            var windowEndUtc = _utcNow().AddDays(_settings.CalendarFutureDays);
            var filter = BuildCalendarFilter(windowStartUtc, windowEndUtc);
            restrictedItems = InvokeMember(items, "Restrict", filter);
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
        InvokeMember(outlookApp, "GetNamespace", "MAPI")
        ?? throw new InvalidOperationException("Outlook MAPI namespace was unavailable.");

    private object? ResolveDefaultFolder(
        object outlookNamespace,
        int folderType,
        string staleReason
    )
    {
        try
        {
            return InvokeMember(outlookNamespace, "GetDefaultFolder", folderType);
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
            if (count >= maxItems)
            {
                yield break;
            }
        }
    }

    private NormalizedMessage? NormalizeMessage(object item)
    {
        var entryId = GetOptionalString(item, "EntryID");
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return null;
        }

        var messageClass = GetOptionalString(item, "MessageClass");
        var isMeeting = IsMeetingItem(item, messageClass);
        var bridgeId = BridgeIdCodec.MessageId(entryId, isMeeting);
        var storeId = GetStoreId(item);
        var senderName = GetOptionalString(item, "SenderName");
        var senderEmail =
            GetOptionalString(item, "SenderEmailAddress")
            ?? GetOptionalString(item, "SenderEmailType");
        var bodyPreview = ResponseShaper.ShapePreview(GetOptionalString(item, "Body"), _settings);
        var dto = new MessageDto(
            bridgeId,
            isMeeting ? "meeting" : "mail",
            GetOptionalString(item, "Subject"),
            GetOptionalDateTimeOffset(item, "ReceivedTime"),
            GetOptionalDateTimeOffset(item, "SentOn"),
            GetOptionalInt(item, "Importance"),
            GetOptionalInt(item, "Sensitivity"),
            GetOptionalBool(item, "Unread"),
            GetOptionalBool(item, "Attachments") || GetOptionalBool(item, "HasAttachments"),
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
        var entryId = GetOptionalString(item, "EntryID");
        var startUtc =
            GetOptionalDateTimeOffset(item, "StartUTC") ?? GetOptionalDateTimeOffset(item, "Start");
        var endUtc =
            GetOptionalDateTimeOffset(item, "EndUTC") ?? GetOptionalDateTimeOffset(item, "End");
        if (string.IsNullOrWhiteSpace(entryId) || startUtc is null || endUtc is null)
        {
            return null;
        }

        var globalAppointmentId = GetOptionalString(item, "GlobalAppointmentID");
        var bridgeId = BridgeIdCodec.EventId(globalAppointmentId, entryId, startUtc.Value);
        var dto = new EventDto(
            bridgeId,
            globalAppointmentId,
            GetOptionalString(item, "Subject"),
            startUtc.Value,
            endUtc.Value,
            GetOptionalString(item, "Location"),
            GetOptionalInt(item, "BusyStatus"),
            GetOptionalInt(item, "MeetingStatus"),
            GetOptionalBool(item, "IsRecurring"),
            GetOptionalInt(item, "Sensitivity"),
            GetOptionalString(item, "Organizer"),
            null,
            null,
            null,
            ResponseShaper.ShapePreview(GetOptionalString(item, "Body"), _settings),
            !string.IsNullOrWhiteSpace(GetOptionalString(item, "Body")),
            false
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
            parent = GetOptionalMemberValue(item, "Parent");
            if (parent is null)
            {
                return null;
            }

            store = GetOptionalMemberValue(parent, "Store");
            return store is null
                ? GetOptionalString(parent, "StoreID")
                : GetOptionalString(store, "StoreID");
        }
        finally
        {
            _com.ReleaseAll(store, parent);
        }
    }

    private static object? InvokeMember(object target, string memberName, params object?[] args) =>
        target
            .GetType()
            .InvokeMember(memberName, BindingFlags.InvokeMethod, binder: null, target, args);

    private static object? GetMemberValue(object target, string memberName) =>
        target.GetType().InvokeMember(memberName, BindingFlags.GetProperty, null, target, null);

    private static object? GetOptionalMemberValue(object target, string memberName)
    {
        try
        {
            return GetMemberValue(target, memberName);
        }
        catch
        {
            return null;
        }
    }

    private static void SetMemberValue(object target, string memberName, object? value) =>
        target
            .GetType()
            .InvokeMember(
                memberName,
                BindingFlags.SetProperty,
                binder: null,
                target,
                args: [value]
            );

    private static string? GetOptionalString(object target, string memberName)
    {
        try
        {
            return GetMemberValue(target, memberName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool GetOptionalBool(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value switch
            {
                bool boolValue => boolValue,
                int intValue => intValue != 0,
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    private static int? GetOptionalInt(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value is null ? null : Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? GetOptionalDateTimeOffset(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value switch
            {
                DateTimeOffset dto => dto,
                DateTime dateTime => new DateTimeOffset(dateTime.ToUniversalTime()),
                _ when DateTimeOffset.TryParse(value?.ToString(), out var parsed) => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed record NormalizedMessage(string EntryId, string? StoreId, MessageDto Dto);

    private sealed record NormalizedEvent(
        string EntryId,
        string? StoreId,
        string? GlobalAppointmentId,
        EventDto Dto
    );
}
