using System.Text;
using System.Text.Json;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Interface member 11: <c>ProposeNewMeetingTimeAsync</c> issuing the attendee
/// propose-new-time <c>POST /users/{p}/events/{id}/tentativelyAccept</c> (issue #130, the
/// attendee-side calendar-write route mirroring F18's organizer reschedule). The request
/// body carries exactly <c>sendResponse</c> (hardcoded <c>true</c>) and
/// <c>proposedNewTime</c> (a <c>start</c>/<c>end</c> <c>dateTimeTimeZone</c> pair), so it
/// structurally cannot rewrite the event. All auth, retry/backoff, and D5 error mapping
/// are inherited from the shared <see cref="GraphRequestExecutor"/> unchanged; a 202
/// response with an empty body maps to <c>ok: true, data: null</c> (the
/// <see cref="SendMailAsync"/> precedent), no fabricated data.
/// </summary>
internal sealed partial class GraphHostAdapterClient
{
    /// <summary>
    /// Proposes <paramref name="proposedStartUtc"/>/<paramref name="proposedEndUtc"/> as an
    /// alternative time for the principal's invited event via
    /// <c>POST users/{Principal}/events/{id}/tentativelyAccept</c>. The body has exactly two
    /// top-level properties, <c>sendResponse</c> (<c>true</c>) and <c>proposedNewTime</c>,
    /// the latter a <c>start</c>/<c>end</c> pair whose <c>dateTime</c> is the UTC instant at
    /// seconds precision and whose <c>timeZone</c> is <c>"UTC"</c>; no <c>comment</c> and no
    /// <c>Prefer</c> headers are sent. A 202 response with an empty body maps to
    /// <c>ok: true, data: null</c> through the executor.
    /// </summary>
    public Task<ApiEnvelope<object?>> ProposeNewMeetingTimeAsync(
        string bridgeId,
        DateTimeOffset proposedStartUtc,
        DateTimeOffset proposedEndUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}/tentativelyAccept";
        var bodyJson = JsonSerializer.Serialize(
            new
            {
                sendResponse = true,
                proposedNewTime = new
                {
                    start = new
                    {
                        dateTime = SchedulingDateTime(proposedStartUtc),
                        timeZone = "UTC",
                    },
                    end = new { dateTime = SchedulingDateTime(proposedEndUtc), timeZone = "UTC" },
                },
            },
            GraphRequestExecutor.JsonOptions
        );

        return executor.ExecuteAsync<object?>(
            () =>
                new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
                },
            _ => null,
            requestId,
            cancellationToken
        );
    }
}
