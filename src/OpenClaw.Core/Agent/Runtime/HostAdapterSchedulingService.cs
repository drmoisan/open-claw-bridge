using OpenClaw.Core.Agent;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// HostAdapter-backed <see cref="ISchedulingService"/> implementation (OR-4). Wraps
/// <see cref="IHostAdapterClient"/> and the <see cref="SchedulingDtoMapper"/> for both the
/// read delegation and the outbound send delegation. This is part of the runtime seam
/// (namespace <c>OpenClaw.Core.Agent.Runtime</c>) and may reference
/// <c>OpenClaw.HostAdapter.Contracts</c>, which <c>OpenClaw.Core</c> already references.
/// </summary>
public sealed class HostAdapterSchedulingService(
    IHostAdapterClient hostAdapterClient,
    SchedulingDtoMapper mapper
) : ISchedulingService
{
    /// <inheritdoc />
    public async Task<SchedulingMessageDto?> GetSchedulingMessageAsync(
        string messageId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var envelope = await hostAdapterClient
            .GetMessageAsync(messageId, cancellationToken: ct)
            .ConfigureAwait(false);
        return envelope is { Ok: true, Data: not null } ? mapper.MapMessage(envelope.Data) : null;
    }

    /// <inheritdoc />
    public async Task<SchedulingEventDto?> GetEventForMessageAsync(
        string messageId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        // Resolve the event linked to the message via the dedicated linkage route (issue #146). A
        // genuinely unlinked message yields an ok:true / data:null envelope, which maps to null so
        // the deterministic pipeline degrades to its calendar-view fallback exactly as before.
        var envelope = await hostAdapterClient
            .GetEventForMessageAsync(messageId, cancellationToken: ct)
            .ConfigureAwait(false);
        return envelope is { Ok: true, Data: not null } ? mapper.MapEvent(envelope.Data) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SchedulingEventDto>> GetCalendarViewAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    )
    {
        var envelope = await hostAdapterClient
            .ListCalendarWindowAsync(start, end, cancellationToken: ct)
            .ConfigureAwait(false);
        if (envelope is not { Ok: true, Data: not null })
        {
            return Array.Empty<SchedulingEventDto>();
        }

        return envelope.Data.Items.Select(mapper.MapEvent).ToList();
    }

    /// <inheritdoc />
    public async Task<SchedulingEventDto?> GetEventAsync(string eventId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        var envelope = await hostAdapterClient
            .GetEventAsync(eventId, cancellationToken: ct)
            .ConfigureAwait(false);
        return envelope is { Ok: true, Data: not null } ? mapper.MapEvent(envelope.Data) : null;
    }

    /// <inheritdoc />
    public async Task<MailboxSettingsDto> GetMailboxSettingsAsync(CancellationToken ct)
    {
        var envelope = await hostAdapterClient
            .GetMailboxSettingsAsync(cancellationToken: ct)
            .ConfigureAwait(false);
        if (envelope is { Ok: true, Data: not null })
        {
            return envelope.Data;
        }

        // Graceful degradation (consistent with GetCalendarViewAsync): when the route is
        // unavailable, return the documented defaults so the deterministic pipeline still runs.
        return new MailboxSettingsDto(
            "UTC",
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
            ],
            new TimeOnly(9, 0),
            new TimeOnly(17, 0)
        );
    }

    /// <inheritdoc />
    public async Task<FreeBusyScheduleDto> GetFreeBusyAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    )
    {
        var envelope = await hostAdapterClient
            .GetFreeBusyAsync(start, end, cancellationToken: ct)
            .ConfigureAwait(false);
        if (envelope is { Ok: true, Data: not null })
        {
            return envelope.Data;
        }

        // Graceful degradation: an unavailable route yields an empty busy grid so the
        // SlotProposer treats the window as fully free rather than throwing.
        return new FreeBusyScheduleDto(string.Empty, Array.Empty<BusyIntervalDto>());
    }

    /// <inheritdoc />
    public async Task SendMailAsync(
        SendMailRequest request,
        string? correlationId = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var wireRequest = mapper.MapSendMailRequest(request);
        var envelope = await hostAdapterClient
            .SendMailAsync(wireRequest, requestId: correlationId, cancellationToken: ct)
            .ConfigureAwait(false);
        if (envelope is not { Ok: true })
        {
            // Fail fast on a failure envelope; client exceptions (including
            // OperationCanceledException) propagate unwrapped and unhandled.
            var code = envelope.Error?.Code ?? "UNKNOWN_ERROR";
            var message =
                envelope.Error?.Message
                ?? "The HostAdapter returned a failure envelope with no error detail.";
            throw new InvalidOperationException($"Outbound sendMail failed: {code}: {message}");
        }
    }

    /// <inheritdoc />
    public async Task RescheduleEventAsync(
        string eventId,
        DateTimeOffset newStartUtc,
        DateTimeOffset newEndUtc,
        string? correlationId = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        var envelope = await hostAdapterClient
            .UpdateEventTimesAsync(
                eventId,
                newStartUtc,
                newEndUtc,
                requestId: correlationId,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        if (envelope is not { Ok: true })
        {
            // Fail fast on a failure envelope; client exceptions (including
            // OperationCanceledException) propagate unwrapped and unhandled.
            var code = envelope.Error?.Code ?? "UNKNOWN_ERROR";
            var message =
                envelope.Error?.Message
                ?? "The HostAdapter returned a failure envelope with no error detail.";
            throw new InvalidOperationException($"Organizer reschedule failed: {code}: {message}");
        }
    }

    /// <inheritdoc />
    public async Task ProposeNewMeetingTimeAsync(
        string eventId,
        DateTimeOffset proposedStartUtc,
        DateTimeOffset proposedEndUtc,
        string? correlationId = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        var envelope = await hostAdapterClient
            .ProposeNewMeetingTimeAsync(
                eventId,
                proposedStartUtc,
                proposedEndUtc,
                requestId: correlationId,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        if (envelope is not { Ok: true })
        {
            // Fail fast on a failure envelope; client exceptions (including
            // OperationCanceledException) propagate unwrapped and unhandled.
            var code = envelope.Error?.Code ?? "UNKNOWN_ERROR";
            var message =
                envelope.Error?.Message
                ?? "The HostAdapter returned a failure envelope with no error detail.";
            throw new InvalidOperationException(
                $"Attendee propose-new-time failed: {code}: {message}"
            );
        }
    }
}
