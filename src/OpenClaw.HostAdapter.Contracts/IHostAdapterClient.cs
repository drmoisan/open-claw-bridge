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

    /// <summary>
    /// Retrieves the mailbox time zone and working hours via
    /// <c>GET /users/{id}/mailboxSettings</c>.
    /// </summary>
    /// <remarks>
    /// The HostAdapter sources these values from its own configuration
    /// (<c>OpenClaw:HostAdapter:MailboxSettings</c>); this route does not perform a bridge
    /// round-trip. The defaults are <c>TimeZoneId</c> UTC, working days Monday–Friday, and
    /// working hours 09:00–17:00.
    /// </remarks>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>The mailbox settings wrapped in an API envelope.</returns>
    Task<ApiEnvelope<MailboxSettingsDto>> GetMailboxSettingsAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves the free/busy schedule for the given UTC window via
    /// <c>GET /users/{id}/calendar/getSchedule?startDateTime={iso8601}&amp;endDateTime={iso8601}</c>.
    /// </summary>
    /// <remarks>
    /// The HostAdapter computes the busy intervals from the bridge calendar data it fetches
    /// through the CLI client process; an empty window yields an empty
    /// <c>BusyIntervals</c> list (not an error). The window is passed as typed
    /// <paramref name="startUtc"/>/<paramref name="endUtc"/> parameters: this signature is the
    /// portability boundary (decision D2). For Stage 0 the wire request is a GET with query
    /// parameters; when the PI-1 Microsoft Graph backend replaces the local adapter, only the
    /// <c>HostAdapterHttpClient</c> wire construction changes to the Graph POST-with-body form
    /// while this signature and every caller remain unchanged.
    /// </remarks>
    /// <param name="startUtc">The inclusive UTC window start emitted as the <c>startDateTime</c> parameter.</param>
    /// <param name="endUtc">The exclusive UTC window end emitted as the <c>endDateTime</c> parameter.</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>The free/busy schedule wrapped in an API envelope.</returns>
    Task<ApiEnvelope<FreeBusyScheduleDto>> GetFreeBusyAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sends an outbound message via <c>POST /users/{assistantMailbox}/sendMail</c>.
    /// </summary>
    /// <remarks>
    /// On success the HostAdapter returns <c>202 Accepted</c> with an empty payload
    /// (<c>ApiEnvelope&lt;object?&gt;</c> with <c>ok: true</c> and <c>data: null</c>) (D-A); a 202
    /// indicates the message was accepted for send, not guaranteed delivery (it may queue to the
    /// Outbox when offline). Validation failures return <c>400 INVALID_REQUEST</c>; a bridge/COM
    /// send failure maps to <c>502</c>.
    /// <para>
    /// Send-on-behalf is deferred to PI-1 (AC-09). A future <c>fromEmailAddress</c> will be added at
    /// the bridge mail-sender seam without changing this signature, so existing callers are not
    /// broken when send-on-behalf lands.
    /// </para>
    /// </remarks>
    /// <param name="request">The Graph-shaped send request (message plus <c>saveToSentItems</c>).</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>An empty API envelope; <c>ok: true</c> on a 202 success.</returns>
    Task<ApiEnvelope<object?>> SendMailAsync(
        SendMailRequest request,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates only the start and end times of an organizer-owned event via
    /// <c>PATCH /users/{principal}/events/{id}</c> (member 10, the first calendar-write
    /// route; issue #128).
    /// </summary>
    /// <remarks>
    /// The update scope is deliberately narrow: the request body carries exactly the
    /// <c>start</c> and <c>end</c> <c>dateTimeTimeZone</c> pairs and nothing else. The
    /// body structurally cannot carry <c>body</c>, <c>subject</c>, <c>location</c>, or
    /// <c>attendees</c>, which realizes the master §11.1 guardrail against clobbering the
    /// online-meeting blob on an online meeting.
    /// <para>
    /// The Graph-backed adapter issues the real PATCH through the shared request pipeline
    /// (bearer auth, <c>client-request-id</c> from <paramref name="requestId"/>,
    /// 429/5xx retry, D5 error mapping) and maps a 200 response to the updated
    /// <see cref="EventDto"/>. The local Stage-0 HostAdapter backend has no calendar-write
    /// route (master line 108, deferred), so it fails closed with a synthesized
    /// non-retryable <c>NOT_SUPPORTED</c> failure envelope and performs no I/O; organizer
    /// reschedule therefore requires the Graph adapter.
    /// </para>
    /// </remarks>
    /// <param name="bridgeId">The event identifier rendered into the <c>{id}</c> route segment.</param>
    /// <param name="newStartUtc">The new event start (UTC instant).</param>
    /// <param name="newEndUtc">The new event end (UTC instant).</param>
    /// <param name="requestId">An optional caller-supplied correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the outbound operation.</param>
    /// <returns>The updated event wrapped in an API envelope; a fail-closed error envelope otherwise.</returns>
    Task<ApiEnvelope<EventDto>> UpdateEventTimesAsync(
        string bridgeId,
        DateTimeOffset newStartUtc,
        DateTimeOffset newEndUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );
}
