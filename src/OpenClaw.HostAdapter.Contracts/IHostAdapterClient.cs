using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Defines the read-only HTTP operations exposed by the HostAdapter using
/// Microsoft Graph-shaped routes (<c>/users/{id}/messages</c>,
/// <c>/users/{id}/messages/{messageId}</c>, <c>/users/{id}/calendarView</c>,
/// <c>/users/{id}/events/{eventId}</c>, and the operational <c>/status</c> probe).
/// </summary>
public interface IHostAdapterClient
{
    /// <summary>
    /// Retrieves the current bridge status snapshot via <c>GET /status</c>.
    /// </summary>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>The bridge status wrapped in an API envelope.</returns>
    Task<ApiEnvelope<BridgeStatusDto>> GetStatusAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists recent messages from the bridge cache after the provided UTC timestamp via
    /// <c>GET /users/{id}/messages?$filter=receivedDateTime ge {iso8601}&amp;$top={limit}</c>.
    /// </summary>
    /// <param name="sinceUtc">The inclusive UTC lower bound emitted as the <c>$filter</c> <c>receivedDateTime ge</c> predicate.</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>A message collection wrapped in an API envelope.</returns>
    Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMessagesAsync(
        DateTimeOffset sinceUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves a single message by its opaque bridge identifier via
    /// <c>GET /users/{id}/messages/{messageId}</c>.
    /// </summary>
    /// <param name="bridgeId">The bridge-generated message identifier rendered into the <c>{messageId}</c> route segment.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>A message detail wrapped in an API envelope.</returns>
    Task<ApiEnvelope<MessageDto>> GetMessageAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists recent meeting requests after the provided UTC timestamp via the messages-filtered form
    /// <c>GET /users/{id}/messages?$filter=meetingMessageType ne null and receivedDateTime ge {iso8601}&amp;$top={limit}</c>.
    /// Microsoft Graph has no dedicated meeting-requests collection, so this is a filtered messages query.
    /// </summary>
    /// <param name="sinceUtc">The inclusive UTC lower bound emitted as the <c>$filter</c> <c>receivedDateTime ge</c> predicate.</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>A meeting-request collection wrapped in an API envelope.</returns>
    Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMeetingRequestsAsync(
        DateTimeOffset sinceUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists calendar events within the provided UTC window via
    /// <c>GET /users/{id}/calendarView?startDateTime={iso8601}&amp;endDateTime={iso8601}&amp;$top={limit}</c>.
    /// </summary>
    /// <param name="startUtc">The inclusive UTC window start emitted as the <c>startDateTime</c> parameter.</param>
    /// <param name="endUtc">The exclusive UTC window end emitted as the <c>endDateTime</c> parameter.</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>An event collection wrapped in an API envelope.</returns>
    Task<ApiEnvelope<ItemsResponse<EventDto>>> ListCalendarWindowAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves a single event by its opaque bridge identifier via
    /// <c>GET /users/{id}/events/{eventId}</c>.
    /// </summary>
    /// <param name="bridgeId">The bridge-generated event identifier rendered into the <c>{eventId}</c> route segment.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>An event detail wrapped in an API envelope.</returns>
    Task<ApiEnvelope<EventDto>> GetEventAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );
}
