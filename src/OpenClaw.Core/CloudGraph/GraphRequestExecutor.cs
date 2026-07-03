using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.Core.CloudAuth;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Thrown by the pure Graph-to-DTO mappers when a required Graph field (for example
/// <c>id</c>, or event <c>start</c>/<c>end</c>) is missing. The request pipeline maps
/// this to an <c>INTERNAL_ERROR</c> envelope rather than fabricating data (fail-fast
/// rule).
/// </summary>
internal sealed class GraphMappingException(string message) : Exception(message);

/// <summary>
/// Shared Microsoft Graph request pipeline (D5/D6): executes a rebuilt-per-attempt
/// request with an app-only bearer token and <c>client-request-id</c>, retries only
/// HTTP 429/502/503/504 with <c>Retry-After</c> precedence over exponential backoff
/// (all delays through the injected <see cref="TimeProvider"/>), maps failures per the
/// D5 error matrix, and synthesizes <see cref="ApiEnvelope{T}"/> values with
/// <c>ApiMeta(requestId, "cloudgraph", null)</c>. Tokens and response bodies are never
/// logged.
/// </summary>
internal sealed class GraphRequestExecutor(
    HttpClient httpClient,
    IAppTokenProvider tokenProvider,
    TimeProvider timeProvider,
    GraphAdapterOptions options,
    ILogger logger
)
{
    /// <summary>Adapter identifier stamped into <see cref="ApiMeta.AdapterVersion"/>.</summary>
    internal const string AdapterVersion = "cloudgraph";

    /// <summary>Web-default (camelCase, case-insensitive) serializer options shared by the adapter.</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Executes the request produced by <paramref name="requestFactory"/> (invoked once
    /// per attempt; an <see cref="HttpRequestMessage"/> is single-use) and parses the
    /// success body with <paramref name="parseSuccessBody"/>.
    /// </summary>
    /// <typeparam name="T">The mapped success payload type.</typeparam>
    /// <param name="requestFactory">Builds a fresh request for each attempt.</param>
    /// <param name="parseSuccessBody">
    /// Parses the raw 2xx response body into <typeparamref name="T"/>. A thrown
    /// <see cref="JsonException"/> maps to <c>TRANSPORT_FAILURE</c> (unparseable body);
    /// a thrown <see cref="GraphMappingException"/> maps to <c>INTERNAL_ERROR</c>.
    /// </param>
    /// <param name="requestId">Caller-supplied correlation id; blank generates a GUID.</param>
    /// <param name="cancellationToken">Cancels the request and any backoff delay.</param>
    public async Task<ApiEnvelope<T>> ExecuteAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        Func<string, T> parseSuccessBody,
        string? requestId,
        CancellationToken cancellationToken
    )
    {
        var actualRequestId = string.IsNullOrWhiteSpace(requestId)
            ? Guid.NewGuid().ToString()
            : requestId;
        var meta = new ApiMeta(actualRequestId, AdapterVersion, null);

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            AppAccessToken token;
            try
            {
                token = await tokenProvider.GetTokenAsync(cancellationToken);
            }
            catch (TokenAcquisitionException)
            {
                logger.LogError(
                    "Graph request {RequestId}: app-only token acquisition failed.",
                    actualRequestId
                );
                return Failure<T>(
                    meta,
                    new ApiError(
                        "CONFIGURATION_ERROR",
                        "App-only token acquisition failed; check the OpenClaw:CloudAuth configuration.",
                        null,
                        false
                    )
                );
            }

            using var request = requestFactory();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.Add("client-request-id", actualRequestId);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException)
            {
                logger.LogError(
                    "Graph request {RequestId}: network failure on attempt {Attempt}.",
                    actualRequestId,
                    attempt
                );
                return Failure<T>(
                    meta,
                    new ApiError(
                        "TRANSPORT_FAILURE",
                        "A network error prevented the Microsoft Graph request from completing.",
                        null,
                        true
                    )
                );
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return ParseSuccess(response.StatusCode, body, parseSuccessBody, meta);
                }

                if (IsRetryableStatus(response.StatusCode) && attempt < options.MaxAttempts)
                {
                    var delay = ComputeDelay(response, attempt);
                    logger.LogWarning(
                        "Graph request {RequestId}: HTTP {StatusCode} on attempt {Attempt} of {MaxAttempts}; retrying after {Delay}.",
                        actualRequestId,
                        (int)response.StatusCode,
                        attempt,
                        options.MaxAttempts,
                        delay
                    );
                    await Task.Delay(delay, timeProvider, cancellationToken);
                    continue;
                }

                var error = MapStatusError(response.StatusCode, body, attempt);
                logger.LogError(
                    "Graph request {RequestId}: terminal HTTP {StatusCode} ({Code}) after {Attempt} attempt(s).",
                    actualRequestId,
                    (int)response.StatusCode,
                    error.Code,
                    attempt
                );
                return Failure<T>(meta, error);
            }
        }

        // Unreachable: the loop always returns on the final attempt (MaxAttempts >= 1
        // is enforced by GraphAdapterOptionsValidator).
        throw new InvalidOperationException("The Graph retry loop exited without a result.");
    }

    private static ApiEnvelope<T> ParseSuccess<T>(
        HttpStatusCode statusCode,
        string body,
        Func<string, T> parseSuccessBody,
        ApiMeta meta
    )
    {
        try
        {
            return new ApiEnvelope<T>(true, parseSuccessBody(body), meta, null);
        }
        catch (JsonException)
        {
            return Failure<T>(
                meta,
                new ApiError(
                    "TRANSPORT_FAILURE",
                    $"Microsoft Graph returned HTTP {(int)statusCode} without a parseable body.",
                    null,
                    false
                )
            );
        }
        catch (GraphMappingException ex)
        {
            return Failure<T>(meta, new ApiError("INTERNAL_ERROR", ex.Message, null, false));
        }
    }

    private static ApiEnvelope<T> Failure<T>(ApiMeta meta, ApiError error) =>
        new(false, default, meta, error);

    private static bool IsRetryableStatus(HttpStatusCode statusCode) =>
        statusCode
            is HttpStatusCode.TooManyRequests
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;

    /// <summary>
    /// Computes the backoff before the next attempt: a <c>Retry-After</c> header
    /// (delta-seconds or HTTP-date, evaluated against the injected clock) wins;
    /// otherwise exponential fallback <c>BaseDelay * 2^(attempt-1)</c> capped at
    /// <c>MaxDelay</c> (D6).
    /// </summary>
    private TimeSpan ComputeDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var untilDate = date - timeProvider.GetUtcNow();
            return untilDate < TimeSpan.Zero ? TimeSpan.Zero : untilDate;
        }

        var seconds = Math.Min(
            options.BaseDelaySeconds * Math.Pow(2, attempt - 1),
            options.MaxDelaySeconds
        );
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Maps a terminal non-success status per the D5 error matrix.</summary>
    private ApiError MapStatusError(HttpStatusCode statusCode, string body, int attempt)
    {
        var graphCode = TryReadGraphErrorCode(body);
        var exhausted = IsRetryableStatus(statusCode);
        var message = exhausted
            ? $"Microsoft Graph returned HTTP {(int)statusCode} after {attempt} attempt(s)."
            : $"Microsoft Graph returned HTTP {(int)statusCode}.";

        var code = statusCode switch
        {
            HttpStatusCode.BadRequest => "INVALID_REQUEST",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "UNAUTHORIZED",
            HttpStatusCode.NotFound => "NOT_FOUND",
            HttpStatusCode.TooManyRequests => "THROTTLED",
            HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout => "TRANSPORT_FAILURE",
            _ => "INTERNAL_ERROR",
        };

        return new ApiError(code, message, graphCode, exhausted);
    }

    /// <summary>
    /// Extracts the Graph <c>error.code</c> passthrough for
    /// <see cref="ApiError.BridgeErrorCode"/>; an unparseable error body yields null.
    /// </summary>
    private static string? TryReadGraphErrorCode(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GraphErrorBody>(body, JsonOptions)?.Error?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
