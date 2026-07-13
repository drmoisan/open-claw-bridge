using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Interface members 2-4: <c>ListMessagesAsync</c>, <c>GetMessageAsync</c>, and
/// <c>ListMeetingRequestsAsync</c> (D10 client-side <c>eventMessage</c> filter).
/// </summary>
internal sealed partial class GraphHostAdapterClient
{
    /// <summary>
    /// Lists messages received at or after <paramref name="sinceUtc"/> via
    /// <c>GET /users/{p}/messages</c> with the D3 paging bounds.
    /// </summary>
    public Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMessagesAsync(
        DateTimeOffset sinceUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        return ListPagedAsync<GraphMessage, MessageDto>(
            MessageListUrl(sinceUtc, limit),
            _ => true,
            GraphMessageMapper.Map,
            limit,
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Retrieves a single message by Graph id via <c>GET /users/{p}/messages/{id}</c>.
    /// </summary>
    public Task<ApiEnvelope<MessageDto>> GetMessageAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url =
            $"users/{Principal}/messages/{Uri.EscapeDataString(bridgeId)}?$select={MessageSelect}";
        return executor.ExecuteAsync(
            () => BuildReadRequest(url),
            body => GraphMessageMapper.Map(DeserializeWire<GraphMessage>(body)),
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Resolves the calendar event linked to a message (issue #146). Message-to-event linkage is a
    /// bridge-cache concept (a stored <c>GlobalAppointmentID</c> joined to the cached events) with no
    /// equivalent single Graph round-trip on this read path. To preserve the graceful-degradation
    /// contract shared with <see cref="HostAdapterHttpClient"/> — an unlinked message must resolve to
    /// a clean <see langword="null"/>, never an error — the cloud client returns a structurally valid
    /// <c>ok:true</c> / <c>data:null</c> envelope rather than a <c>NOT_SUPPORTED</c> error, so the
    /// scheduling pipeline degrades to its calendar-view fallback exactly as it does today.
    /// </summary>
    public Task<ApiEnvelope<EventDto>> GetEventForMessageAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        _ = bridgeId;
        _ = cancellationToken;
        var resolvedRequestId = ResolveRequestId(requestId);
        return Task.FromResult(
            new ApiEnvelope<EventDto>(
                true,
                null,
                new ApiMeta(resolvedRequestId, GraphRequestExecutor.AdapterVersion, null),
                null
            )
        );
    }

    /// <summary>
    /// Lists meeting-request messages: the same server query as
    /// <see cref="ListMessagesAsync"/> (Graph has no dedicated meeting-requests
    /// collection) with the D10 client-side filter
    /// <c>@odata.type == "#microsoft.graph.eventMessage"</c>, paging until
    /// <paramref name="limit"/> meeting messages are found or the D3 bounds are hit.
    /// </summary>
    public Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMeetingRequestsAsync(
        DateTimeOffset sinceUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        return ListPagedAsync<GraphMessage, MessageDto>(
            MessageListUrl(sinceUtc, limit),
            wire =>
                string.Equals(
                    wire.ODataType,
                    GraphMessageMapper.EventMessageODataType,
                    StringComparison.Ordinal
                ),
            GraphMessageMapper.Map,
            limit,
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Composes the shared message-list URL: <c>receivedDateTime</c> filter, newest
    /// first, <c>$top = min(limit, PageSize)</c>, and the spec <c>$select</c> list
    /// including <c>meetingMessageType</c> (D10 primary form).
    /// </summary>
    private string MessageListUrl(DateTimeOffset sinceUtc, int limit)
    {
        var top = Math.Min(limit, options.PageSize);
        return $"users/{Principal}/messages"
            + $"?$filter=receivedDateTime ge {IsoUtc(sinceUtc)}"
            + "&$orderby=receivedDateTime desc"
            + $"&$top={top}"
            + $"&$select={MessageSelect}";
    }
}
