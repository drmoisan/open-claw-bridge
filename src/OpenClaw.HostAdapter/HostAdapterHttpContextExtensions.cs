using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace OpenClaw.HostAdapter;

internal static class HostAdapterHttpContextExtensions
{
    private const string RequestIdItemKey = "OpenClaw.HostAdapter.RequestId";
    private const string BridgeStateItemKey = "OpenClaw.HostAdapter.BridgeState";
    private const string BridgeErrorCodeItemKey = "OpenClaw.HostAdapter.BridgeErrorCode";
    private const string CliExitCodeItemKey = "OpenClaw.HostAdapter.CliExitCode";
    private const string RequestIdHeaderName = "X-Request-Id";

    public static string EnsureRequestId(this HttpContext context)
    {
        if (
            context.Items.TryGetValue(RequestIdItemKey, out var existingValue)
            && existingValue is string existingRequestId
            && !string.IsNullOrWhiteSpace(existingRequestId)
        )
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Headers[RequestIdHeaderName] = existingRequestId;
            }

            return existingRequestId;
        }

        var requestId =
            context.Request.Headers.TryGetValue(RequestIdHeaderName, out StringValues headerValue)
            && !StringValues.IsNullOrEmpty(headerValue)
                ? headerValue.ToString()
                : Guid.NewGuid().ToString();

        context.Items[RequestIdItemKey] = requestId;
        if (!context.Response.HasStarted)
        {
            context.Response.Headers[RequestIdHeaderName] = requestId;
        }

        return requestId;
    }

    public static string GetRequestId(this HttpContext context) => context.EnsureRequestId();

    public static void SetHostAdapterTelemetry(
        this HttpContext context,
        string? bridgeState,
        string? bridgeErrorCode,
        int? cliExitCode
    )
    {
        if (!string.IsNullOrWhiteSpace(bridgeState))
        {
            context.Items[BridgeStateItemKey] = bridgeState;
        }

        if (!string.IsNullOrWhiteSpace(bridgeErrorCode))
        {
            context.Items[BridgeErrorCodeItemKey] = bridgeErrorCode;
        }

        if (cliExitCode.HasValue)
        {
            context.Items[CliExitCodeItemKey] = cliExitCode.Value;
        }
    }

    public static string? GetBridgeState(this HttpContext context) =>
        context.Items.TryGetValue(BridgeStateItemKey, out var value) ? value as string : null;

    public static string? GetBridgeErrorCode(this HttpContext context) =>
        context.Items.TryGetValue(BridgeErrorCodeItemKey, out var value) ? value as string : null;

    public static int? GetCliExitCode(this HttpContext context) =>
        context.Items.TryGetValue(CliExitCodeItemKey, out var value) && value is int exitCode
            ? exitCode
            : null;
}
