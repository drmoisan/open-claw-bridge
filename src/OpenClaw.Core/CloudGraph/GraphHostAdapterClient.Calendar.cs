using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Interface members 5-8: <c>ListCalendarWindowAsync</c>, <c>GetEventAsync</c>, and
/// (added in Phase 4) <c>GetMailboxSettingsAsync</c>/<c>GetFreeBusyAsync</c>.
/// </summary>
internal sealed partial class GraphHostAdapterClient
{
    /// <summary>
    /// Lists calendar occurrences, exceptions, and single instances in the UTC window
    /// via <c>GET /users/{p}/calendarView</c> with the D3 paging bounds.
    /// </summary>
    public Task<ApiEnvelope<ItemsResponse<EventDto>>> ListCalendarWindowAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var top = Math.Min(limit, options.PageSize);
        var url =
            $"users/{Principal}/calendarView"
            + $"?startDateTime={IsoUtc(startUtc)}"
            + $"&endDateTime={IsoUtc(endUtc)}"
            + $"&$top={top}"
            + $"&$select={EventSelect}";
        return ListPagedAsync<GraphEvent, EventDto>(
            url,
            _ => true,
            GraphEventMapper.Map,
            limit,
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Retrieves a single event by Graph id via <c>GET /users/{p}/events/{id}</c>.
    /// </summary>
    public Task<ApiEnvelope<EventDto>> GetEventAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url =
            $"users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}?$select={EventSelect}";
        return executor.ExecuteAsync(
            () => BuildReadRequest(url),
            body => GraphEventMapper.Map(DeserializeWire<GraphEvent>(body)),
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Retrieves the mailbox time zone and working hours via
    /// <c>GET /users/{p}/mailboxSettings?$select=timeZone,workingHours</c>.
    /// </summary>
    public Task<ApiEnvelope<MailboxSettingsDto>> GetMailboxSettingsAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"users/{Principal}/mailboxSettings?$select=timeZone,workingHours";
        return executor.ExecuteAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            body =>
                GraphSchedulingMapper.MapMailboxSettings(
                    DeserializeWire<GraphMailboxSettings>(body)
                ),
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Retrieves the free/busy schedule via
    /// <c>POST /users/{p}/calendar/getSchedule</c> with the spec JSON body (the
    /// GET-to-POST wire change anticipated by the interface D2 portability note).
    /// </summary>
    public Task<ApiEnvelope<FreeBusyScheduleDto>> GetFreeBusyAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"users/{Principal}/calendar/getSchedule";
        var bodyJson = JsonSerializer.Serialize(
            new
            {
                schedules = new[] { options.PrincipalMailboxUpn },
                startTime = new { dateTime = SchedulingDateTime(startUtc), timeZone = "UTC" },
                endTime = new { dateTime = SchedulingDateTime(endUtc), timeZone = "UTC" },
                availabilityViewInterval = options.AvailabilityViewIntervalMinutes,
            },
            GraphRequestExecutor.JsonOptions
        );

        return executor.ExecuteAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("Prefer", $"outlook.timezone=\"{options.PreferredTimeZone}\"");
                return request;
            },
            body =>
                GraphSchedulingMapper.MapFreeBusy(
                    options.PrincipalMailboxUpn,
                    DeserializeWire<GraphScheduleResponse>(body)
                ),
            requestId,
            cancellationToken
        );
    }

    /// <summary>Seconds-precision wall-clock rendering for the getSchedule body (spec example).</summary>
    private static string SchedulingDateTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("s", CultureInfo.InvariantCulture);
}
