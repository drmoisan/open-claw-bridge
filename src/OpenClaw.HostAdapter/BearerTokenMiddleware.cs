using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal sealed class BearerTokenMiddleware(
    RequestDelegate next,
    IOptions<HostAdapterOptions> optionsAccessor,
    IHostAdapterTokenProvider tokenProvider
)
{
    private readonly HostAdapterOptions options = optionsAccessor.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.EnsureRequestId();
        var expectedToken = tokenProvider.ReadExpectedToken();
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            var configurationError = HostAdapterResponses.ConfigurationError<object>(
                requestId,
                options.AdapterVersion,
                "The configured HostAdapter token file is missing or empty."
            );
            context.SetHostAdapterTelemetry(null, configurationError.Envelope.Error?.Code, null);
            context.Response.StatusCode = configurationError.StatusCode;
            await context.Response.WriteAsJsonAsync(configurationError.Envelope);
            return;
        }

        if (
            !TryGetBearerToken(context, out var actualToken)
            || !TokensMatch(expectedToken, actualToken)
        )
        {
            var unauthorized = HostAdapterResponses.Failure<object>(
                StatusCodes.Status401Unauthorized,
                requestId,
                options.AdapterVersion,
                BridgeErrorCodes.Unauthorized,
                "Missing or invalid bearer token.",
                bridgeErrorCode: BridgeErrorCodes.Unauthorized
            );
            context.SetHostAdapterTelemetry(null, BridgeErrorCodes.Unauthorized, null);
            context.Response.StatusCode = unauthorized.StatusCode;
            await context.Response.WriteAsJsonAsync(unauthorized.Envelope);
            return;
        }

        await next(context);
    }

    private static bool TryGetBearerToken(HttpContext context, out string token)
    {
        token = string.Empty;
        if (!context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return false;
        }

        var headerValue = authorizationHeader.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = headerValue["Bearer ".Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    private static bool TokensMatch(string expectedToken, string actualToken)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var actualBytes = Encoding.UTF8.GetBytes(actualToken);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
