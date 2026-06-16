using System.Text.Json.Serialization;

namespace OpenClaw.MailBridge.Contracts.Models;

public enum BridgeState
{
    starting,
    waiting_for_outlook,
    ready,
    degraded,
    error,
}

public enum BridgeMode
{
    safe,
    enhanced,
}

public static class BridgeMethods
{
    public const string GetStatus = "get_status";
    public const string ListRecentMessages = "list_recent_messages";
    public const string GetMessage = "get_message";
    public const string ListRecentMeetingRequests = "list_recent_meeting_requests";
    public const string ListCalendarWindow = "list_calendar_window";
    public const string GetEvent = "get_event";

    public static readonly HashSet<string> All =
    [
        GetStatus,
        ListRecentMessages,
        GetMessage,
        ListRecentMeetingRequests,
        ListCalendarWindow,
        GetEvent,
    ];
}

public sealed record RpcRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] Dictionary<string, string>? Params
);

public sealed record RpcError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message
);

public sealed record RpcResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] RpcError? Error
)
{
    public static RpcResponse Success(string id, object? result) => new(id, true, result, null);

    public static RpcResponse Failure(string id, string code, string message) =>
        new(id, false, null, new RpcError(code, message));
}

public sealed record BridgeStatusDto(
    string State,
    string Mode,
    bool OutlookConnected,
    bool CacheStale,
    string? StaleReason,
    DateTimeOffset? LastInboxScanUtc,
    DateTimeOffset? LastCalendarScanUtc
);

public sealed record MessageDto(
    string BridgeId,
    string ItemKind,
    string? Subject,
    DateTimeOffset? ReceivedUtc,
    DateTimeOffset? SentUtc,
    int? Importance,
    int? Sensitivity,
    bool Unread,
    bool HasAttachments,
    string? MessageClass,
    string? SenderName,
    string? SenderEmail,
    string? ToJson,
    string? CcJson,
    string? BodyPreview,
    bool ProtectedFieldsAvailable,
    bool IsRedacted,
    string? SenderEmailResolved = null,
    string? FromEmailAddress = null,
    string? ConversationId = null,
    int? MeetingMessageType = null
);

public sealed record EventDto(
    string BridgeId,
    string? GlobalAppointmentId,
    string? Subject,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Location,
    int? BusyStatus,
    int? MeetingStatus,
    bool IsRecurring,
    int? Sensitivity,
    string? Organizer,
    string? RequiredAttendeesJson,
    string? OptionalAttendeesJson,
    string? ResourcesJson,
    string? BodyPreview,
    bool ProtectedFieldsAvailable,
    bool IsRedacted,
    int? ResponseStatus = null,
    string[]? Categories = null,
    bool IsOrganizer = false,
    bool IsOnlineMeeting = false,
    bool AllowNewTimeProposals = false,
    string? ICalUId = null,
    string? SeriesMasterId = null,
    DateTimeOffset? LastModifiedDateTime = null,
    string? BodyFull = null,
    string? SensitivityLabel = null
);

public sealed record BridgeSettings(
    string PipeName,
    string Mode,
    bool AutostartOutlook,
    int InboxPollSeconds,
    int CalendarPollSeconds,
    int InboxOverlapMinutes,
    int CalendarPastDays,
    int CalendarFutureDays,
    int MaxItemsPerScan,
    int BodyPreviewMaxChars,
    int ComYieldBatchSize,
    int ComYieldMilliseconds,
    string LogLevel
)
{
    public static BridgeSettings Default =>
        new(
            "openclaw_mailbridge_v1",
            "safe",
            true,
            30,
            300,
            5,
            14,
            60,
            500,
            500,
            25,
            15,
            "Information"
        );
}

public static class BridgeErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string OutlookUnavailable = "OUTLOOK_UNAVAILABLE";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
}
