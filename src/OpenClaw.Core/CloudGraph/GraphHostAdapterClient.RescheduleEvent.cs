using System.Text;
using System.Text.Json;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Interface member 10: <c>UpdateEventTimesAsync</c> issuing the organizer-reschedule
/// <c>PATCH /users/{p}/events/{id}</c> (issue #128, the first calendar-write route). The
/// request body carries exactly the <c>start</c> and <c>end</c> <c>dateTimeTimeZone</c>
/// pairs, so it structurally cannot touch the online-meeting blob (master §11.1
/// guardrail). All auth, retry/backoff, and D5 error mapping are inherited from the
/// shared <see cref="GraphRequestExecutor"/> unchanged.
/// </summary>
internal sealed partial class GraphHostAdapterClient
{
    /// <summary>
    /// Moves an organizer-owned event to <paramref name="newStartUtc"/>/<paramref name="newEndUtc"/>
    /// via <c>PATCH users/{Principal}/events/{id}</c>. The body has exactly two top-level
    /// properties, <c>start</c> and <c>end</c>, each a <c>dateTimeTimeZone</c> pair whose
    /// <c>dateTime</c> is the UTC instant at seconds precision and whose <c>timeZone</c>
    /// is <c>"UTC"</c>; no <c>Prefer</c> headers are sent. A 200 response is mapped to the
    /// updated <see cref="EventDto"/> through <see cref="GraphEventMapper.Map"/>; an
    /// unparseable 2xx body maps to <c>TRANSPORT_FAILURE</c> and a mapping gap to
    /// <c>INTERNAL_ERROR</c> (no fabricated data), both via the executor.
    /// </summary>
    public Task<ApiEnvelope<EventDto>> UpdateEventTimesAsync(
        string bridgeId,
        DateTimeOffset newStartUtc,
        DateTimeOffset newEndUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}";
        var bodyJson = JsonSerializer.Serialize(
            new
            {
                start = new { dateTime = SchedulingDateTime(newStartUtc), timeZone = "UTC" },
                end = new { dateTime = SchedulingDateTime(newEndUtc), timeZone = "UTC" },
            },
            GraphRequestExecutor.JsonOptions
        );

        return executor.ExecuteAsync(
            () =>
                new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
                },
            body => GraphEventMapper.Map(DeserializeWire<GraphEvent>(body)),
            requestId,
            cancellationToken
        );
    }
}
