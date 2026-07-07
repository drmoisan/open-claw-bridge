using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Agent;

/// <summary>
/// The middleware seam (D6) that the deterministic agent components depend on. It
/// exposes Graph-shaped scheduling data through value-typed DTOs so the implementation
/// can be swapped from HostAdapter-backed to Graph-backed without changing the
/// deterministic components (D1-D4). This contract carries no dependency on
/// <c>OpenClaw.MailBridge</c>, <c>OpenClaw.HostAdapter</c>, or Outlook COM.
/// </summary>
public interface ISchedulingService
{
    /// <summary>Hydrates the Graph-shaped message for the given message identifier.</summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The message, or <see langword="null"/> when it cannot be found.</returns>
    Task<SchedulingMessageDto?> GetSchedulingMessageAsync(string messageId, CancellationToken ct);

    /// <summary>Hydrates the event associated with the given message, when present.</summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The associated event, or <see langword="null"/> when none exists.</returns>
    Task<SchedulingEventDto?> GetEventForMessageAsync(string messageId, CancellationToken ct);

    /// <summary>Lists events in the given UTC window (the calendar-view fallback).</summary>
    /// <param name="start">The inclusive UTC window start.</param>
    /// <param name="end">The exclusive UTC window end.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The events in the window.</returns>
    Task<IReadOnlyList<SchedulingEventDto>> GetCalendarViewAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    );

    /// <summary>Retrieves a single event by its identifier.</summary>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The event, or <see langword="null"/> when it cannot be found.</returns>
    Task<SchedulingEventDto?> GetEventAsync(string eventId, CancellationToken ct);

    /// <summary>Retrieves the mailbox settings (time zone and working hours).</summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The mailbox settings.</returns>
    Task<MailboxSettingsDto> GetMailboxSettingsAsync(CancellationToken ct);

    /// <summary>Retrieves the free/busy schedule for the given UTC window.</summary>
    /// <param name="start">The inclusive UTC window start.</param>
    /// <param name="end">The exclusive UTC window end.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The free/busy schedule.</returns>
    Task<FreeBusyScheduleDto> GetFreeBusyAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    );

    /// <summary>Sends an outbound mail (gated by the send kill switch upstream).</summary>
    /// <param name="request">The send-mail request.</param>
    /// <param name="correlationId">
    /// The worker-generated GUID for this outbound-action evaluation (issue #107, D5),
    /// forwarded as the HostAdapter request id so the audit row correlates with the
    /// adapter's <c>X-Request-Id</c>. When <see langword="null"/>, the underlying client
    /// self-generates a request id (the pre-#107 behavior).
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the send finishes.</returns>
    Task SendMailAsync(
        SendMailRequest request,
        string? correlationId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reschedules an organizer-owned event to the given UTC start/end (issue #128, the
    /// first calendar-write path; gated upstream by the worker's
    /// <c>CalendarWritePolicy.OrganizerRescheduleAllowed</c> check). Mirrors the
    /// <see cref="SendMailAsync"/> seam shape: returns <see cref="Task"/> (not the updated
    /// DTO) because the worker already holds the times it recorded, and fails fast on a
    /// non-<c>Ok</c> adapter envelope so a failure is surfaced as an exception the worker
    /// audits as <c>reschedule_failed</c>.
    /// </summary>
    /// <param name="eventId">The event identifier to reschedule.</param>
    /// <param name="newStartUtc">The new event start (UTC instant).</param>
    /// <param name="newEndUtc">The new event end (UTC instant).</param>
    /// <param name="correlationId">
    /// The worker-generated GUID for this outbound-action evaluation (issue #107),
    /// forwarded to the adapter as the Graph <c>client-request-id</c>. When
    /// <see langword="null"/>, the underlying client self-generates a request id.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the reschedule finishes.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// The adapter returned a non-<c>Ok</c> envelope; the message carries the error code
    /// and message.
    /// </exception>
    Task RescheduleEventAsync(
        string eventId,
        DateTimeOffset newStartUtc,
        DateTimeOffset newEndUtc,
        string? correlationId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Proposes a new time for a meeting the principal is invited to but does not organize
    /// (issue #130, the attendee-side calendar-write path; gated upstream by the worker's
    /// <c>CalendarWritePolicy.AttendeeProposeNewTimeAllowed</c> check). Mirrors the
    /// <see cref="RescheduleEventAsync"/> seam shape: returns <see cref="Task"/> (not a DTO)
    /// because the wire response has no body and the worker already holds the times it
    /// audits, and fails fast on a non-<c>Ok</c> adapter envelope so a failure is surfaced
    /// as an exception the worker audits as <c>propose_new_time_failed</c>.
    /// </summary>
    /// <param name="eventId">The event identifier to propose a new time for.</param>
    /// <param name="proposedStartUtc">The proposed event start (UTC instant).</param>
    /// <param name="proposedEndUtc">The proposed event end (UTC instant).</param>
    /// <param name="correlationId">
    /// The worker-generated GUID for this outbound-action evaluation (issue #107),
    /// forwarded to the adapter as the Graph <c>client-request-id</c>. When
    /// <see langword="null"/>, the underlying client self-generates a request id.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the proposal finishes.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// The adapter returned a non-<c>Ok</c> envelope; the message carries the error code
    /// and message.
    /// </exception>
    Task ProposeNewMeetingTimeAsync(
        string eventId,
        DateTimeOffset proposedStartUtc,
        DateTimeOffset proposedEndUtc,
        string? correlationId = null,
        CancellationToken ct = default
    );
}
