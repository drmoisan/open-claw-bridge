using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core;

internal sealed class HostAdapterHttpClient(
    HttpClient httpClient,
    IOptions<OpenClawOptions> optionsAccessor
) : IHostAdapterClient
{
    private readonly OpenClawOptions options = optionsAccessor.Value;

    // Seam for token acquisition; overridden in unit tests to avoid filesystem I/O.
    // The func receives the configured TokenFile path and returns the trimmed bearer
    // token string, or null when the path is invalid or the file is absent.
    internal Func<string, CancellationToken, Task<string?>> TokenReader { get; init; } =
        static async (tokenPath, cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(tokenPath) || !File.Exists(tokenPath))
                return null;

            return (await File.ReadAllTextAsync(tokenPath, cancellationToken)).Trim();
        };

    public Task<ApiEnvelope<BridgeStatusDto>> GetStatusAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        return SendAsync<BridgeStatusDto>("status", requestId, cancellationToken);
    }

    public Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMessagesAsync(
        DateTimeOffset sinceUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<ItemsResponse<MessageDto>>(
            $"users/{id}/messages?$filter=receivedDateTime ge {Uri.EscapeDataString(sinceUtc.ToString("O"))}&$top={limit}",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<MessageDto>> GetMessageAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<MessageDto>(
            $"users/{id}/messages/{Uri.EscapeDataString(bridgeId)}",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMeetingRequestsAsync(
        DateTimeOffset sinceUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<ItemsResponse<MessageDto>>(
            $"users/{id}/messages?$filter=meetingMessageType ne null and receivedDateTime ge {Uri.EscapeDataString(sinceUtc.ToString("O"))}&$top={limit}",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<ItemsResponse<EventDto>>> ListCalendarWindowAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit = 100,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<ItemsResponse<EventDto>>(
            $"users/{id}/calendarView?startDateTime={Uri.EscapeDataString(startUtc.ToString("O"))}&endDateTime={Uri.EscapeDataString(endUtc.ToString("O"))}&$top={limit}",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<EventDto>> GetEventAsync(
        string bridgeId,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<EventDto>(
            $"users/{id}/events/{Uri.EscapeDataString(bridgeId)}",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<MailboxSettingsDto>> GetMailboxSettingsAsync(
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<MailboxSettingsDto>(
            $"users/{id}/mailboxSettings",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<FreeBusyScheduleDto>> GetFreeBusyAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return SendAsync<FreeBusyScheduleDto>(
            $"users/{id}/calendar/getSchedule?startDateTime={Uri.EscapeDataString(startUtc.ToString("O"))}&endDateTime={Uri.EscapeDataString(endUtc.ToString("O"))}",
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<object?>> SendMailAsync(
        SendMailRequest request,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        var id = Uri.EscapeDataString(options.HostAdapter.MailboxId);
        return PostAsync<SendMailRequest, object?>(
            $"users/{id}/sendMail",
            request,
            requestId,
            cancellationToken
        );
    }

    public Task<ApiEnvelope<EventDto>> UpdateEventTimesAsync(
        string bridgeId,
        DateTimeOffset newStartUtc,
        DateTimeOffset newEndUtc,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        // Fail closed with no I/O: the local Stage-0 HostAdapter backend has no
        // calendar-write route (master line 108, deferred behind feature flags), so a
        // real PATCH would 404 and misreport a permanent capability gap as a transient
        // TRANSPORT_FAILURE. The synthesized non-retryable NOT_SUPPORTED envelope names
        // the Graph adapter as the required backend. No HttpClient invocation and no
        // token acquisition occur on this path.
        var actualRequestId = string.IsNullOrWhiteSpace(requestId)
            ? Guid.NewGuid().ToString()
            : requestId;
        return Task.FromResult(
            new ApiEnvelope<EventDto>(
                false,
                default,
                new ApiMeta(actualRequestId, "hostadapter", null),
                new ApiError(
                    "NOT_SUPPORTED",
                    "The local HostAdapter backend has no calendar-write route; organizer "
                        + "reschedule requires the Graph adapter.",
                    null,
                    Retryable: false
                )
            )
        );
    }

    private async Task<ApiEnvelope<TResponse>> PostAsync<TBody, TResponse>(
        string relativePath,
        TBody body,
        string? requestId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath)
        {
            Content = JsonContent.Create(body),
        };
        var actualRequestId = string.IsNullOrWhiteSpace(requestId)
            ? Guid.NewGuid().ToString()
            : requestId;
        request.Headers.Add("X-Request-Id", actualRequestId);

        var token = await TokenReader(options.HostAdapter.TokenFile, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ApiEnvelope<TResponse>(
                false,
                default,
                new ApiMeta(actualRequestId, "hostadapter", null),
                new ApiError(
                    "CONFIGURATION_ERROR",
                    "The HostAdapter token file is missing or empty."
                )
            );
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await httpClient.SendAsync(request, cancellationToken);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TResponse>>(
            cancellationToken: cancellationToken
        );
        if (envelope is not null)
        {
            return envelope;
        }

        return new ApiEnvelope<TResponse>(
            false,
            default,
            new ApiMeta(actualRequestId, "hostadapter", null),
            new ApiError(
                "TRANSPORT_FAILURE",
                $"The HostAdapter returned HTTP {(int)response.StatusCode} without a parseable envelope."
            )
        );
    }

    private async Task<ApiEnvelope<T>> SendAsync<T>(
        string relativePath,
        string? requestId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        var actualRequestId = string.IsNullOrWhiteSpace(requestId)
            ? Guid.NewGuid().ToString()
            : requestId;
        request.Headers.Add("X-Request-Id", actualRequestId);

        var token = await TokenReader(options.HostAdapter.TokenFile, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ApiEnvelope<T>(
                false,
                default,
                new ApiMeta(actualRequestId, "hostadapter", null),
                new ApiError(
                    "CONFIGURATION_ERROR",
                    "The HostAdapter token file is missing or empty."
                )
            );
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await httpClient.SendAsync(request, cancellationToken);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(
            cancellationToken: cancellationToken
        );
        if (envelope is not null)
        {
            return envelope;
        }

        return new ApiEnvelope<T>(
            false,
            default,
            new ApiMeta(actualRequestId, "hostadapter", null),
            new ApiError(
                "TRANSPORT_FAILURE",
                $"The HostAdapter returned HTTP {(int)response.StatusCode} without a parseable envelope."
            )
        );
    }
}
