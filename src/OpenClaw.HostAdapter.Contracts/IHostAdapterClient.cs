using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Defines the read-only HTTP operations exposed by the HostAdapter.
/// </summary>
public interface IHostAdapterClient
{
    /// <summary>
    /// Retrieves the current bridge status snapshot.
    /// </summary>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>The bridge status wrapped in an API envelope.</returns>
    Task<ApiEnvelope<BridgeStatusDto>> GetStatusAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists recent messages from the bridge cache after the provided UTC timestamp.
    /// </summary>
    /// <param name="sinceUtc">The inclusive UTC lower bound for returned messages.</param>
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
    /// Retrieves a single message by its opaque bridge identifier.
    /// </summary>
    /// <param name="bridgeId">The bridge-generated message identifier.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>A message detail wrapped in an API envelope.</returns>
    Task<ApiEnvelope<MessageDto>> GetMessageAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists recent meeting requests after the provided UTC timestamp.
    /// </summary>
    /// <param name="sinceUtc">The inclusive UTC lower bound for returned meeting requests.</param>
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
    /// Lists calendar events within the provided UTC window.
    /// </summary>
    /// <param name="startUtc">The inclusive UTC window start.</param>
    /// <param name="endUtc">The exclusive UTC window end.</param>
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
    /// Retrieves a single event by its opaque bridge identifier.
    /// </summary>
    /// <param name="bridgeId">The bridge-generated event identifier.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>An event detail wrapped in an API envelope.</returns>
    Task<ApiEnvelope<EventDto>> GetEventAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );
}
