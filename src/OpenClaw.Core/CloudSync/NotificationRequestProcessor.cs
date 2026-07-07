using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.CloudSync;

/// <summary>A host-neutral HTTP outcome: status code plus optional content.</summary>
/// <param name="StatusCode">The HTTP status code to return.</param>
/// <param name="ContentType">The response content type, or null for an empty response.</param>
/// <param name="Body">The response body, or null for an empty response.</param>
internal sealed record NotificationProcessorResult(
    int StatusCode,
    string? ContentType,
    string? Body
);

/// <summary>
/// Host-neutral webhook logic for <c>POST /graph/notifications</c> (master §6.1/§8.2):
/// validation handshake, constant-time <c>clientState</c> validation with 202-and-drop
/// semantics (D-1), and enqueue-only processing — no Graph I/O, no database writes
/// (subscription store reads only), and no <see cref="HttpClient"/> dependency. The
/// ASP.NET glue lives in <see cref="GraphNotificationsEndpoint"/>.
/// </summary>
internal sealed class NotificationRequestProcessor(
    ISubscriptionStore subscriptionStore,
    INotificationQueue queue,
    ILogger<NotificationRequestProcessor> logger,
    IActionAuditLog actionAuditLog,
    TimeProvider timeProvider
)
{
    private const string InvalidRequestCode = "INVALID_REQUEST";

    private readonly IActionAuditLog actionAuditLog =
        actionAuditLog ?? throw new ArgumentNullException(nameof(actionAuditLog));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Handles the Graph validation handshake: HTTP 200, <c>text/plain</c>, body = the
    /// URL-decoded token HTML-encoded (the token is opaque untrusted input; master
    /// §6.1). No Graph or database I/O occurs on this path.
    /// </summary>
    /// <param name="validationToken">The <c>validationToken</c> query value.</param>
    public static NotificationProcessorResult HandleHandshake(string validationToken)
    {
        var decoded = WebUtility.UrlDecode(validationToken);
        return new NotificationProcessorResult(200, "text/plain", WebUtility.HtmlEncode(decoded));
    }

    /// <summary>
    /// Processes a change/lifecycle notification batch: parses the body, validates each
    /// item's subscription and <c>clientState</c> (constant-time), enqueues one work
    /// item per valid notification, and returns 202 — including for batches containing
    /// only invalid items (D-1). Malformed JSON returns 400 with the repository's
    /// <c>{ code, message }</c> error shape.
    /// </summary>
    /// <param name="body">The raw request body.</param>
    /// <param name="ct">Cancels store lookups.</param>
    public async Task<NotificationProcessorResult> ProcessNotificationsAsync(
        string body,
        CancellationToken ct
    )
    {
        GraphNotificationCollection? batch;
        try
        {
            batch = JsonSerializer.Deserialize<GraphNotificationCollection>(
                body,
                GraphRequestExecutor.JsonOptions
            );
        }
        catch (JsonException)
        {
            return InvalidRequest("The notification body is not valid JSON.");
        }

        foreach (var item in batch?.Value ?? [])
        {
            await ProcessItemAsync(item, ct);
        }

        return new NotificationProcessorResult(202, null, null);
    }

    /// <summary>
    /// Pure constant-time <c>clientState</c> comparison (D-1):
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> over the UTF-8 bytes of
    /// the candidate and the stored value. A null or empty candidate never matches and
    /// never throws.
    /// </summary>
    /// <param name="candidate">The notification's <c>clientState</c> value.</param>
    /// <param name="stored">The stored per-subscription secret.</param>
    internal static bool ClientStateMatches(string? candidate, string stored)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(candidate),
            Encoding.UTF8.GetBytes(stored)
        );
    }

    /// <summary>Validates one item and enqueues its work item; invalid items drop with a Warning.</summary>
    private async Task ProcessItemAsync(GraphNotification item, CancellationToken ct)
    {
        var correlationId = Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(item.SubscriptionId))
        {
            logger.LogWarning(
                "Dropped a Graph notification with no subscriptionId (D-1 202-and-drop)."
            );
            var unresolvedId = item.ResourceData?.Id ?? item.SubscriptionId ?? "(unresolvable)";
            await RecordWebhookRejectedAsync(
                unresolvedId,
                unresolvedId,
                CloudSyncActivityResultCode.UnknownSubscription,
                correlationId,
                ct
            );
            return;
        }

        var subscription = await subscriptionStore.GetSubscriptionAsync(item.SubscriptionId, ct);
        if (subscription is null)
        {
            logger.LogWarning(
                "Dropped a Graph notification for unknown subscription {SubscriptionId} (D-1 202-and-drop).",
                item.SubscriptionId
            );
            await RecordWebhookRejectedAsync(
                item.SubscriptionId,
                item.SubscriptionId,
                CloudSyncActivityResultCode.UnknownSubscription,
                correlationId,
                ct
            );
            return;
        }

        if (!ClientStateMatches(item.ClientState, subscription.ClientState))
        {
            logger.LogWarning(
                "Dropped a Graph notification with mismatched clientState for subscription {SubscriptionId} (D-1 202-and-drop).",
                item.SubscriptionId
            );
            await RecordWebhookRejectedAsync(
                subscription.Mailbox,
                item.SubscriptionId,
                CloudSyncActivityResultCode.ClientStateMismatch,
                correlationId,
                ct
            );
            return;
        }

        if (item.LifecycleEvent is not null)
        {
            queue.TryEnqueue(new LifecycleWorkItem(item.SubscriptionId, item.LifecycleEvent));
            await RecordWebhookReceivedAsync(
                subscription.Mailbox,
                item.SubscriptionId,
                correlationId,
                ct
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ResourceData?.Id))
        {
            logger.LogWarning(
                "Dropped a Graph change notification without resourceData.id for subscription {SubscriptionId} (fail-visible).",
                item.SubscriptionId
            );
            await RecordWebhookRejectedAsync(
                subscription.Mailbox,
                item.SubscriptionId,
                CloudSyncActivityResultCode.MissingResourceId,
                correlationId,
                ct
            );
            return;
        }

        queue.TryEnqueue(
            new NotificationWorkItem(
                subscription.Mailbox,
                item.ResourceData.Id,
                item.ChangeType ?? string.Empty
            )
        );
        await RecordWebhookReceivedAsync(
            subscription.Mailbox,
            item.ResourceData.Id,
            correlationId,
            ct
        );
    }

    /// <summary>Records a <see cref="CloudSyncActivityType.WebhookReceived"/> audit record.</summary>
    private Task RecordWebhookReceivedAsync(
        string mailbox,
        string messageId,
        string correlationId,
        CancellationToken ct
    ) =>
        actionAuditLog.RecordAsync(
            new ActionAuditRecord(
                Mailbox: mailbox,
                MessageId: messageId,
                EventId: null,
                ActionType: CloudSyncActivityType.WebhookReceived,
                ActingFlags: CloudSyncActingFlags.NotApplicable,
                CorrelationId: correlationId,
                ResultCode: CloudSyncActivityResultCode.Success,
                ErrorDetail: null,
                OriginalStartUtc: null,
                OriginalEndUtc: null,
                NewStartUtc: null,
                NewEndUtc: null,
                RecordedAtUtc: timeProvider.GetUtcNow()
            ),
            ct
        );

    /// <summary>Records a <see cref="CloudSyncActivityType.WebhookRejected"/> audit record.</summary>
    private Task RecordWebhookRejectedAsync(
        string mailbox,
        string messageId,
        string resultCode,
        string correlationId,
        CancellationToken ct
    ) =>
        actionAuditLog.RecordAsync(
            new ActionAuditRecord(
                Mailbox: mailbox,
                MessageId: messageId,
                EventId: null,
                ActionType: CloudSyncActivityType.WebhookRejected,
                ActingFlags: CloudSyncActingFlags.NotApplicable,
                CorrelationId: correlationId,
                ResultCode: resultCode,
                ErrorDetail: null,
                OriginalStartUtc: null,
                OriginalEndUtc: null,
                NewStartUtc: null,
                NewEndUtc: null,
                RecordedAtUtc: timeProvider.GetUtcNow()
            ),
            ct
        );

    private static NotificationProcessorResult InvalidRequest(string message) =>
        new(
            400,
            "application/json",
            JsonSerializer.Serialize(
                new { code = InvalidRequestCode, message },
                GraphRequestExecutor.JsonOptions
            )
        );
}
