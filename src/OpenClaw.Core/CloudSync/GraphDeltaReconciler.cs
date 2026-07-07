using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Reconciles the Core message cache against the mailbox truth via
/// <c>messages/delta</c> (master §6.2: webhooks are wake signals, delta is truth).
/// Walks <c>@odata.nextLink</c> pages (bounded by
/// <see cref="GraphAdapterOptions.MaxPages"/>) from the stored delta link — or from
/// the initial delta request when no link is stored or a full re-sync is triggered —
/// maps each message with <see cref="GraphMessageMapper"/>, upserts through the same
/// <see cref="CoreCacheRepository.UpsertMessagesAsync"/> sink the poller uses (D-2)
/// with the D-3 synthesized ready/graph status, skips <c>@removed</c> entries at
/// Debug, persists the terminal <c>@odata.deltaLink</c>, and records a
/// <c>delta_reconcile</c> ingest run for success and failure. Uses its own internal
/// <see cref="GraphRequestExecutor"/> (D-8).
/// </summary>
internal sealed class GraphDeltaReconciler : IDeltaReconcileTrigger
{
    /// <summary>D-3: a successful Graph call is itself the liveness evidence.</summary>
    private static readonly BridgeStatusDto ReadyGraphStatus = new(
        State: "ready",
        Mode: "graph",
        OutlookConnected: true,
        CacheStale: false,
        StaleReason: null,
        LastInboxScanUtc: null,
        LastCalendarScanUtc: null
    );

    private readonly GraphAdapterOptions graphOptions;
    private readonly GraphRequestExecutor executor;
    private readonly IDeltaLinkStore deltaLinkStore;
    private readonly CoreCacheRepository repository;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<GraphDeltaReconciler> logger;

    /// <summary>Creates the reconciler; all seams are injected (D-8 executor reuse).</summary>
    public GraphDeltaReconciler(
        HttpClient httpClient,
        IOptions<GraphAdapterOptions> graphOptionsAccessor,
        IAppTokenProvider tokenProvider,
        IDeltaLinkStore deltaLinkStore,
        CoreCacheRepository repository,
        TimeProvider timeProvider,
        ILogger<GraphDeltaReconciler> logger
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(graphOptionsAccessor);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(deltaLinkStore);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        graphOptions = graphOptionsAccessor.Value;
        this.deltaLinkStore = deltaLinkStore;
        this.repository = repository;
        this.timeProvider = timeProvider;
        this.logger = logger;
        executor = new GraphRequestExecutor(
            httpClient,
            tokenProvider,
            timeProvider,
            graphOptions,
            logger
        );
    }

    /// <summary>
    /// Reconciles <paramref name="mailbox"/> starting from its stored delta link when
    /// one exists (an absent/empty link starts a full re-sync).
    /// </summary>
    /// <param name="mailbox">The mailbox UPN to reconcile.</param>
    /// <param name="ct">Cancels the walk.</param>
    public Task ReconcileAsync(string mailbox, CancellationToken ct) =>
        RunAsync(mailbox, useStoredLink: true, ct);

    /// <summary>
    /// Full re-sync entry point (<c>missed</c> lifecycle trigger): ignores any stored
    /// link and walks from the initial delta request.
    /// </summary>
    /// <param name="mailbox">The mailbox UPN to re-sync.</param>
    /// <param name="ct">Cancels the walk.</param>
    public Task TriggerResyncAsync(string mailbox, CancellationToken ct) =>
        RunAsync(mailbox, useStoredLink: false, ct);

    private async Task RunAsync(string mailbox, bool useStoredLink, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString();
        var startedAtUtc = timeProvider.GetUtcNow();
        var storedLink = useStoredLink ? await deltaLinkStore.GetDeltaLinkAsync(mailbox, ct) : null;
        var url = string.IsNullOrWhiteSpace(storedLink) ? InitialDeltaUrl(mailbox) : storedLink;

        string? terminalDeltaLink = null;
        var upsertedCount = 0;
        for (var page = 1; page <= graphOptions.MaxPages && url is not null; page++)
        {
            var pageUrl = url;
            var envelope = await executor.ExecuteAsync(
                () => new HttpRequestMessage(HttpMethod.Get, pageUrl),
                ParseDeltaPage,
                requestId,
                ct
            );
            if (!envelope.Ok)
            {
                logger.LogWarning(
                    "Delta reconcile {RequestId} for {Mailbox} failed on page {Page}: {Code}.",
                    requestId,
                    mailbox,
                    page,
                    envelope.Error?.Code
                );
                await RecordRunAsync("failed", requestId, startedAtUtc, envelope.Error?.Message);
                return;
            }

            var parsed = envelope.Data!;
            foreach (var removedId in parsed.RemovedIds)
            {
                logger.LogDebug(
                    "Delta reconcile {RequestId}: skipped @removed entry {MessageId} (cache deletion propagation is a non-goal).",
                    requestId,
                    removedId
                );
            }

            if (parsed.Messages.Count > 0)
            {
                // D-3: the immediately-preceding successful Graph call justifies the
                // synthesized ready/graph status; no fabricated status on failures.
                await repository.UpsertMessagesAsync(
                    parsed.Messages,
                    ReadyGraphStatus,
                    requestId,
                    timeProvider.GetUtcNow()
                );
                upsertedCount += parsed.Messages.Count;
            }

            if (parsed.DeltaLink is not null)
            {
                terminalDeltaLink = parsed.DeltaLink;
                break;
            }

            url = parsed.NextLink;
        }

        if (terminalDeltaLink is null)
        {
            logger.LogWarning(
                "Delta reconcile {RequestId} for {Mailbox} stopped at the MaxPages bound ({MaxPages}) without reaching a deltaLink.",
                requestId,
                mailbox,
                graphOptions.MaxPages
            );
            await RecordRunAsync(
                "failed",
                requestId,
                startedAtUtc,
                $"The delta walk hit the MaxPages bound ({graphOptions.MaxPages}) before the terminal deltaLink."
            );
            return;
        }

        await deltaLinkStore.SetDeltaLinkAsync(
            mailbox,
            terminalDeltaLink,
            timeProvider.GetUtcNow(),
            ct
        );
        await RecordRunAsync("success", requestId, startedAtUtc, null);
        logger.LogInformation(
            "Delta reconcile {RequestId} for {Mailbox} completed: {UpsertedCount} message(s) upserted.",
            requestId,
            mailbox,
            upsertedCount
        );
    }

    /// <summary>The initial (full re-sync) delta request URL for <paramref name="mailbox"/>.</summary>
    private static string InitialDeltaUrl(string mailbox) =>
        $"users/{Uri.EscapeDataString(mailbox)}/mailFolders/Inbox/messages/delta"
        + $"?$select={GraphHostAdapterClient.MessageSelect}";

    private Task RecordRunAsync(
        string outcome,
        string requestId,
        DateTimeOffset startedAtUtc,
        string? errorMessage
    ) =>
        repository.AddIngestRunAsync(
            "delta_reconcile",
            outcome,
            requestId,
            startedAtUtc,
            timeProvider.GetUtcNow(),
            errorMessage
        );

    /// <summary>One parsed delta page: mapped messages, skipped removals, and links.</summary>
    private sealed record DeltaPage(
        IReadOnlyList<MessageDto> Messages,
        IReadOnlyList<string> RemovedIds,
        string? NextLink,
        string? DeltaLink
    );

    /// <summary>
    /// Parses one delta page body: <c>@removed</c> entries are collected for Debug
    /// logging and skipped; every other entry maps through
    /// <see cref="GraphMessageMapper.Map"/> (a missing <c>id</c> fails fast via
    /// <see cref="GraphMappingException"/>).
    /// </summary>
    private static DeltaPage ParseDeltaPage(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var messages = new List<MessageDto>();
        var removedIds = new List<string>();
        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
            {
                if (element.TryGetProperty("@removed", out _))
                {
                    removedIds.Add(
                        element.TryGetProperty("id", out var removedId)
                            ? removedId.GetString() ?? "(unknown)"
                            : "(unknown)"
                    );
                    continue;
                }

                var wire =
                    element.Deserialize<GraphMessage>(GraphRequestExecutor.JsonOptions)
                    ?? throw new JsonException("A delta page entry deserialized to null.");
                messages.Add(GraphMessageMapper.Map(wire));
            }
        }

        return new DeltaPage(
            messages,
            removedIds,
            ReadLink(root, "@odata.nextLink"),
            ReadLink(root, "@odata.deltaLink")
        );
    }

    private static string? ReadLink(JsonElement root, string name) =>
        root.TryGetProperty(name, out var link) ? link.GetString() : null;
}
