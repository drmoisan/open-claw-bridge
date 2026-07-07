using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Narrow seam through which the subscription manager triggers a full delta re-sync
/// for a mailbox on a <c>missed</c> lifecycle notification (delta is the recovery
/// mechanism, master §6.1/§6.2). Satisfied by <c>GraphDeltaReconciler</c>.
/// </summary>
internal interface IDeltaReconcileTrigger
{
    /// <summary>Runs a full re-sync (ignoring any stored delta link) for <paramref name="mailbox"/>.</summary>
    /// <param name="mailbox">The mailbox UPN to re-sync.</param>
    /// <param name="ct">Cancels the reconciliation.</param>
    Task TriggerResyncAsync(string mailbox, CancellationToken ct);
}

/// <summary>Graph subscription resource wire shape; only the consumed fields are modeled.</summary>
internal sealed record GraphSubscriptionResource(
    string? Id,
    string? Resource,
    DateTimeOffset? ExpirationDateTime
);

/// <summary>
/// Creates, renews, and lifecycle-routes the principal-Inbox Graph change-notification
/// subscription (issue #117). Constructs its own internal
/// <see cref="GraphRequestExecutor"/> exactly as <c>GraphHostAdapterClient</c> does
/// (D-8): typed <see cref="HttpClient"/> with <c>BaseAddress</c> from
/// <see cref="GraphAdapterOptions.BaseUrl"/>, app-only bearer tokens,
/// <c>client-request-id</c>, retry/backoff, and D5 error mapping arrive with zero new
/// pipeline code. Tokens and bodies are never logged.
/// </summary>
internal sealed class GraphSubscriptionManager
{
    private readonly GraphAdapterOptions graphOptions;
    private readonly CloudSyncOptions cloudSyncOptions;
    private readonly GraphRequestExecutor executor;
    private readonly IClientStateGenerator clientStateGenerator;
    private readonly ISubscriptionStore subscriptionStore;
    private readonly IDeltaReconcileTrigger reconcileTrigger;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<GraphSubscriptionManager> logger;
    private readonly IActionAuditLog actionAuditLog;

    /// <summary>Creates the manager; all seams are injected (D-8 executor reuse).</summary>
    public GraphSubscriptionManager(
        HttpClient httpClient,
        IOptions<GraphAdapterOptions> graphOptionsAccessor,
        IOptions<CloudSyncOptions> cloudSyncOptionsAccessor,
        IAppTokenProvider tokenProvider,
        IClientStateGenerator clientStateGenerator,
        ISubscriptionStore subscriptionStore,
        IDeltaReconcileTrigger reconcileTrigger,
        TimeProvider timeProvider,
        ILogger<GraphSubscriptionManager> logger,
        IActionAuditLog actionAuditLog
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(graphOptionsAccessor);
        ArgumentNullException.ThrowIfNull(cloudSyncOptionsAccessor);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(clientStateGenerator);
        ArgumentNullException.ThrowIfNull(subscriptionStore);
        ArgumentNullException.ThrowIfNull(reconcileTrigger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(actionAuditLog);

        graphOptions = graphOptionsAccessor.Value;
        cloudSyncOptions = cloudSyncOptionsAccessor.Value;
        this.clientStateGenerator = clientStateGenerator;
        this.subscriptionStore = subscriptionStore;
        this.reconcileTrigger = reconcileTrigger;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.actionAuditLog = actionAuditLog;
        executor = new GraphRequestExecutor(
            httpClient,
            tokenProvider,
            timeProvider,
            graphOptions,
            logger
        );
    }

    /// <summary>
    /// Pure renewal-due computation: due when
    /// <c>now &gt;= expiration - renewalLeadMinutes</c>. No I/O, no clock, no state.
    /// </summary>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="expirationUtc">The subscription's stored expiration.</param>
    /// <param name="renewalLeadMinutes">The configured renewal lead.</param>
    internal static bool IsRenewalDue(
        DateTimeOffset nowUtc,
        DateTimeOffset expirationUtc,
        int renewalLeadMinutes
    ) => nowUtc >= expirationUtc - TimeSpan.FromMinutes(renewalLeadMinutes);

    /// <summary>
    /// Creates the principal-Inbox subscription via <c>POST {BaseUrl}subscriptions</c>
    /// (spec API surface) with a freshly generated <c>clientState</c> and
    /// <c>expirationDateTime = now + SubscriptionLifetimeMinutes</c>, persisting the
    /// resulting record through <see cref="ISubscriptionStore"/>.
    /// </summary>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="ct">Cancels the operation.</param>
    public async Task<ApiEnvelope<GraphSubscriptionRecord>> CreateAsync(
        string? requestId,
        CancellationToken ct
    )
    {
        var clientState = clientStateGenerator.Generate();
        var requestedExpiration =
            timeProvider.GetUtcNow()
            + TimeSpan.FromMinutes(cloudSyncOptions.SubscriptionLifetimeMinutes);
        var resource = $"users/{graphOptions.PrincipalMailboxUpn}/mailFolders('Inbox')/messages";
        var payload = JsonSerializer.Serialize(
            new
            {
                changeType = "created,updated",
                resource,
                notificationUrl = cloudSyncOptions.NotificationUrl,
                lifecycleNotificationUrl = cloudSyncOptions.NotificationUrl,
                clientState,
                expirationDateTime = RenderIsoUtc(requestedExpiration),
            },
            GraphRequestExecutor.JsonOptions
        );

        var envelope = await executor.ExecuteAsync(
            () =>
                new HttpRequestMessage(HttpMethod.Post, "subscriptions")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                },
            ParseSubscription,
            requestId,
            ct
        );
        if (!envelope.Ok)
        {
            await actionAuditLog.RecordAsync(
                new ActionAuditRecord(
                    Mailbox: graphOptions.PrincipalMailboxUpn,
                    MessageId: graphOptions.PrincipalMailboxUpn,
                    EventId: null,
                    ActionType: CloudSyncActivityType.SubscriptionCreated,
                    ActingFlags: CloudSyncActingFlags.NotApplicable,
                    CorrelationId: envelope.Meta.RequestId,
                    ResultCode: CloudSyncActivityResultCode.Failure,
                    ErrorDetail: envelope.Error?.Message,
                    OriginalStartUtc: null,
                    OriginalEndUtc: null,
                    NewStartUtc: null,
                    NewEndUtc: null,
                    RecordedAtUtc: timeProvider.GetUtcNow()
                ),
                ct
            );
            return new ApiEnvelope<GraphSubscriptionRecord>(
                false,
                null,
                envelope.Meta,
                envelope.Error
            );
        }

        var record = new GraphSubscriptionRecord(
            envelope.Data!.Id!,
            resource,
            graphOptions.PrincipalMailboxUpn,
            clientState,
            envelope.Data.ExpirationDateTime ?? requestedExpiration,
            SubscriptionStatus.Active
        );
        await subscriptionStore.UpsertSubscriptionAsync(record, timeProvider.GetUtcNow(), ct);
        await actionAuditLog.RecordAsync(
            new ActionAuditRecord(
                Mailbox: graphOptions.PrincipalMailboxUpn,
                MessageId: record.SubscriptionId,
                EventId: null,
                ActionType: CloudSyncActivityType.SubscriptionCreated,
                ActingFlags: CloudSyncActingFlags.NotApplicable,
                CorrelationId: envelope.Meta.RequestId,
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
        logger.LogInformation(
            "Created Graph subscription {SubscriptionId} expiring {ExpirationUtc:O}.",
            record.SubscriptionId,
            record.ExpirationUtc
        );
        return new ApiEnvelope<GraphSubscriptionRecord>(true, record, envelope.Meta, null);
    }

    /// <summary>
    /// Renews a subscription via <c>PATCH {BaseUrl}subscriptions/{id}</c> with a body
    /// carrying <c>expirationDateTime</c> only, then updates the stored record.
    /// </summary>
    /// <param name="subscriptionId">The Graph subscription id to renew.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="ct">Cancels the operation.</param>
    public async Task<ApiEnvelope<GraphSubscriptionRecord>> RenewAsync(
        string subscriptionId,
        string? requestId,
        CancellationToken ct
    )
    {
        var requestedExpiration =
            timeProvider.GetUtcNow()
            + TimeSpan.FromMinutes(cloudSyncOptions.SubscriptionLifetimeMinutes);
        var payload = JsonSerializer.Serialize(
            new { expirationDateTime = RenderIsoUtc(requestedExpiration) },
            GraphRequestExecutor.JsonOptions
        );

        var envelope = await executor.ExecuteAsync(
            () =>
                new HttpRequestMessage(
                    HttpMethod.Patch,
                    $"subscriptions/{Uri.EscapeDataString(subscriptionId)}"
                )
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                },
            ParseSubscription,
            requestId,
            ct
        );
        if (!envelope.Ok)
        {
            await actionAuditLog.RecordAsync(
                new ActionAuditRecord(
                    Mailbox: graphOptions.PrincipalMailboxUpn,
                    MessageId: subscriptionId,
                    EventId: null,
                    ActionType: CloudSyncActivityType.SubscriptionRenewed,
                    ActingFlags: CloudSyncActingFlags.NotApplicable,
                    CorrelationId: envelope.Meta.RequestId,
                    ResultCode: CloudSyncActivityResultCode.Failure,
                    ErrorDetail: envelope.Error?.Message,
                    OriginalStartUtc: null,
                    OriginalEndUtc: null,
                    NewStartUtc: null,
                    NewEndUtc: null,
                    RecordedAtUtc: timeProvider.GetUtcNow()
                ),
                ct
            );
            return new ApiEnvelope<GraphSubscriptionRecord>(
                false,
                null,
                envelope.Meta,
                envelope.Error
            );
        }

        var stored = await subscriptionStore.GetSubscriptionAsync(subscriptionId, ct);
        if (stored is null)
        {
            logger.LogWarning(
                "Renewed Graph subscription {SubscriptionId} has no local record to update.",
                subscriptionId
            );
            var noRecordError = new ApiError(
                "INTERNAL_ERROR",
                "The renewed subscription has no local record.",
                null,
                false
            );
            await actionAuditLog.RecordAsync(
                new ActionAuditRecord(
                    Mailbox: graphOptions.PrincipalMailboxUpn,
                    MessageId: subscriptionId,
                    EventId: null,
                    ActionType: CloudSyncActivityType.SubscriptionRenewed,
                    ActingFlags: CloudSyncActingFlags.NotApplicable,
                    CorrelationId: envelope.Meta.RequestId,
                    ResultCode: CloudSyncActivityResultCode.Failure,
                    ErrorDetail: noRecordError.Message,
                    OriginalStartUtc: null,
                    OriginalEndUtc: null,
                    NewStartUtc: null,
                    NewEndUtc: null,
                    RecordedAtUtc: timeProvider.GetUtcNow()
                ),
                ct
            );
            return new ApiEnvelope<GraphSubscriptionRecord>(
                false,
                null,
                envelope.Meta,
                noRecordError
            );
        }

        var updated = stored with
        {
            ExpirationUtc = envelope.Data!.ExpirationDateTime ?? requestedExpiration,
            Status = SubscriptionStatus.Active,
        };
        await subscriptionStore.UpsertSubscriptionAsync(updated, timeProvider.GetUtcNow(), ct);
        await actionAuditLog.RecordAsync(
            new ActionAuditRecord(
                Mailbox: graphOptions.PrincipalMailboxUpn,
                MessageId: updated.SubscriptionId,
                EventId: null,
                ActionType: CloudSyncActivityType.SubscriptionRenewed,
                ActingFlags: CloudSyncActingFlags.NotApplicable,
                CorrelationId: envelope.Meta.RequestId,
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
        logger.LogInformation(
            "Renewed Graph subscription {SubscriptionId} to {ExpirationUtc:O}.",
            updated.SubscriptionId,
            updated.ExpirationUtc
        );
        return new ApiEnvelope<GraphSubscriptionRecord>(true, updated, envelope.Meta, null);
    }

    /// <summary>
    /// Lifecycle routing table (spec Behavior): <c>reauthorizationRequired</c> renews
    /// now and marks the record <c>reauthorize_failed</c> on an auth-mapped failure;
    /// <c>removed</c> deletes the local record and recreates the subscription;
    /// <c>missed</c> triggers a full delta re-sync for the subscription's mailbox.
    /// Unknown lifecycle values log a Warning and are ignored (never thrown).
    /// </summary>
    /// <param name="item">The validated lifecycle work item.</param>
    /// <param name="ct">Cancels the operation.</param>
    public async Task HandleLifecycleAsync(LifecycleWorkItem item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        switch (item.LifecycleEvent)
        {
            case LifecycleEvents.ReauthorizationRequired:
                var renewal = await RenewAsync(item.SubscriptionId, null, ct);
                if (!renewal.Ok && renewal.Error?.Code is "CONFIGURATION_ERROR" or "UNAUTHORIZED")
                {
                    var renewalError = renewal.Error!;
                    await subscriptionStore.UpdateSubscriptionStatusAsync(
                        item.SubscriptionId,
                        SubscriptionStatus.ReauthorizeFailed,
                        timeProvider.GetUtcNow(),
                        ct
                    );
                    await actionAuditLog.RecordAsync(
                        new ActionAuditRecord(
                            Mailbox: graphOptions.PrincipalMailboxUpn,
                            MessageId: item.SubscriptionId,
                            EventId: null,
                            ActionType: CloudSyncActivityType.SubscriptionExpired,
                            ActingFlags: CloudSyncActingFlags.NotApplicable,
                            CorrelationId: renewal.Meta.RequestId,
                            ResultCode: CloudSyncActivityResultCode.Failure,
                            ErrorDetail: renewalError.Message,
                            OriginalStartUtc: null,
                            OriginalEndUtc: null,
                            NewStartUtc: null,
                            NewEndUtc: null,
                            RecordedAtUtc: timeProvider.GetUtcNow()
                        ),
                        ct
                    );
                    logger.LogWarning(
                        "Reauthorization renewal failed with {Code} for subscription {SubscriptionId}; marked reauthorize_failed.",
                        renewalError.Code,
                        item.SubscriptionId
                    );
                }

                break;
            case LifecycleEvents.Removed:
                await subscriptionStore.DeleteSubscriptionAsync(item.SubscriptionId, ct);
                var correlationId = Guid.NewGuid().ToString();
                await actionAuditLog.RecordAsync(
                    new ActionAuditRecord(
                        Mailbox: graphOptions.PrincipalMailboxUpn,
                        MessageId: item.SubscriptionId,
                        EventId: null,
                        ActionType: CloudSyncActivityType.SubscriptionRemoved,
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
                logger.LogInformation(
                    "Graph subscription {SubscriptionId} was removed; recreating.",
                    item.SubscriptionId
                );
                await CreateAsync(null, ct);
                break;
            case LifecycleEvents.Missed:
                var record = await subscriptionStore.GetSubscriptionAsync(item.SubscriptionId, ct);
                await reconcileTrigger.TriggerResyncAsync(
                    record?.Mailbox ?? graphOptions.PrincipalMailboxUpn,
                    ct
                );
                break;
            default:
                logger.LogWarning(
                    "Ignored unknown Graph lifecycle event {LifecycleEvent} for subscription {SubscriptionId}.",
                    item.LifecycleEvent,
                    item.SubscriptionId
                );
                break;
        }
    }

    /// <summary>Round-trip ISO-8601 UTC rendering for <c>expirationDateTime</c>.</summary>
    private static string RenderIsoUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>Deserializes a subscription resource body; a missing <c>id</c> fails fast.</summary>
    private static GraphSubscriptionResource ParseSubscription(string body)
    {
        var wire =
            JsonSerializer.Deserialize<GraphSubscriptionResource>(
                body,
                GraphRequestExecutor.JsonOptions
            ) ?? throw new JsonException("The Graph subscription body deserialized to null.");
        return string.IsNullOrWhiteSpace(wire.Id)
            ? throw new GraphMappingException(
                "The Graph subscription is missing the required field 'id'."
            )
            : wire;
    }
}
