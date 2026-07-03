using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudAuth;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Microsoft Graph-backed implementation of the host-adapter client surface (issue
/// #115). Speaks Graph REST v1.0 directly (D1) through the shared
/// <see cref="GraphRequestExecutor"/> pipeline (auth, <c>client-request-id</c>,
/// retry/backoff, D5 error mapping, envelope synthesis). Endpoint members live in the
/// <c>Messages</c>/<c>Calendar</c>/<c>SendMail</c> partials; this file holds the
/// constructor seams, the <c>$select</c> lists (D4), the Prefer-header and paging
/// helpers (D3), and the status substitute (D2).
/// </summary>
internal sealed partial class GraphHostAdapterClient : IHostAdapterClient
{
    /// <summary>Message <c>$select</c> list (spec MessageDto table + D10 <c>meetingMessageType</c>).</summary>
    internal const string MessageSelect =
        "id,subject,bodyPreview,receivedDateTime,sentDateTime,importance,sensitivity,"
        + "isRead,hasAttachments,conversationId,from,sender,toRecipients,ccRecipients,"
        + "meetingMessageType";

    /// <summary>Event <c>$select</c> list (spec EventDto table).</summary>
    internal const string EventSelect =
        "id,iCalUId,seriesMasterId,subject,bodyPreview,body,organizer,attendees,"
        + "categories,isOrganizer,isOnlineMeeting,allowNewTimeProposals,sensitivity,"
        + "showAs,responseStatus,location,start,end,type,lastModifiedDateTime";

    private readonly GraphAdapterOptions options;
    private readonly GraphRequestExecutor executor;
    private readonly ILogger<GraphHostAdapterClient> logger;

    /// <summary>
    /// Creates the Graph-backed client. All seams are injected: HTTP transport,
    /// options, app-only token acquisition, and the clock driving retry backoff.
    /// </summary>
    public GraphHostAdapterClient(
        HttpClient httpClient,
        IOptions<GraphAdapterOptions> optionsAccessor,
        IAppTokenProvider tokenProvider,
        TimeProvider timeProvider,
        ILogger<GraphHostAdapterClient> logger
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        options = optionsAccessor.Value;
        this.logger = logger;
        executor = new GraphRequestExecutor(
            httpClient,
            tokenProvider,
            timeProvider,
            options,
            logger
        );
    }

    /// <summary>
    /// D2 status substitute: Graph has no bridge-status analog, so the cheapest stable
    /// read (<c>GET /users/{p}/mailboxSettings?$select=timeZone</c>) probes liveness
    /// through the shared auth/retry pipeline. Probe success synthesizes
    /// <c>BridgeStatusDto("ready", "graph", OutlookConnected: true, CacheStale: false,
    /// ...)</c>; probe failure returns the mapped error envelope — no fabricated
    /// healthy status.
    /// </summary>
    public Task<ApiEnvelope<BridgeStatusDto>> GetStatusAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"users/{Principal}/mailboxSettings?$select=timeZone";
        return executor.ExecuteAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            _ => new BridgeStatusDto(
                State: "ready",
                Mode: "graph",
                OutlookConnected: true,
                CacheStale: false,
                StaleReason: null,
                LastInboxScanUtc: null,
                LastCalendarScanUtc: null
            ),
            requestId,
            cancellationToken
        );
    }

    /// <summary>The URL-escaped principal mailbox UPN (<c>{p}</c> in read routes).</summary>
    private string Principal => Uri.EscapeDataString(options.PrincipalMailboxUpn);

    /// <summary>The URL-escaped assistant mailbox UPN (<c>{a}</c> on send).</summary>
    private string Assistant => Uri.EscapeDataString(options.AssistantMailboxUpn);

    /// <summary>Round-trip ISO-8601 rendering of a UTC instant, URL-escaped.</summary>
    private static string IsoUtc(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    /// <summary>
    /// Builds a GET request for a read route returning message/event bodies, applying
    /// <c>Prefer: outlook.timezone="{PreferredTimeZone}"</c> and
    /// <c>Prefer: outlook.body-content-type="text"</c> so time rendering is
    /// deterministic and bodies arrive as text.
    /// </summary>
    private HttpRequestMessage BuildReadRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Prefer", $"outlook.timezone=\"{options.PreferredTimeZone}\"");
        request.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");
        return request;
    }

    /// <summary>Resolves the effective request id exactly as the executor will.</summary>
    private static string ResolveRequestId(string? requestId) =>
        string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString() : requestId;

    /// <summary>
    /// D3 paging: fetches <paramref name="firstUrl"/> and follows
    /// <c>@odata.nextLink</c> until <paramref name="limit"/> included items are
    /// accumulated, the server stops paging, or <c>MaxPages</c> is hit (logged at
    /// warning). Results are truncated to <paramref name="limit"/>.
    /// </summary>
    private async Task<ApiEnvelope<ItemsResponse<TDto>>> ListPagedAsync<TWire, TDto>(
        string firstUrl,
        Func<TWire, bool> include,
        Func<TWire, TDto> map,
        int limit,
        string? requestId,
        CancellationToken cancellationToken
    )
    {
        var actualRequestId = ResolveRequestId(requestId);
        var meta = new ApiMeta(actualRequestId, GraphRequestExecutor.AdapterVersion, null);
        var accumulated = new List<TDto>();
        var nextUrl = firstUrl;

        for (
            var page = 1;
            page <= options.MaxPages && nextUrl is not null && accumulated.Count < limit;
            page++
        )
        {
            var pageUrl = nextUrl;
            var envelope = await executor.ExecuteAsync(
                () => BuildReadRequest(pageUrl),
                body => ParseListPage(body, include, map),
                actualRequestId,
                cancellationToken
            );

            if (!envelope.Ok)
            {
                return new ApiEnvelope<ItemsResponse<TDto>>(
                    false,
                    null,
                    envelope.Meta,
                    envelope.Error
                );
            }

            accumulated.AddRange(envelope.Data.Items);
            nextUrl = envelope.Data.NextLink;
        }

        if (nextUrl is not null && accumulated.Count < limit)
        {
            logger.LogWarning(
                "Graph request {RequestId}: list truncated at the MaxPages bound ({MaxPages} pages) with {Count} of {Limit} items.",
                actualRequestId,
                options.MaxPages,
                accumulated.Count,
                limit
            );
        }

        var items = accumulated.Count > limit ? accumulated.GetRange(0, limit) : accumulated;
        return new ApiEnvelope<ItemsResponse<TDto>>(
            true,
            new ItemsResponse<TDto>(items),
            meta,
            null
        );
    }

    /// <summary>Deserializes a single Graph resource body; a null result is unparseable.</summary>
    private static TWire DeserializeWire<TWire>(string body) =>
        JsonSerializer.Deserialize<TWire>(body, GraphRequestExecutor.JsonOptions)
        ?? throw new JsonException("The Graph resource body deserialized to null.");

    /// <summary>Deserializes one OData list page and maps the included items.</summary>
    private static (IReadOnlyList<TDto> Items, string? NextLink) ParseListPage<TWire, TDto>(
        string body,
        Func<TWire, bool> include,
        Func<TWire, TDto> map
    )
    {
        var page =
            JsonSerializer.Deserialize<GraphListPage<TWire>>(body, GraphRequestExecutor.JsonOptions)
            ?? throw new JsonException("The Graph list page body deserialized to null.");

        var items = new List<TDto>();
        foreach (var wire in page.Value ?? [])
        {
            if (include(wire))
            {
                items.Add(map(wire));
            }
        }

        return (items, page.NextLink);
    }
}
