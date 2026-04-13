using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenClaw.HostAdapter;

internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        logger.LogInformation(
            "HostAdapter request {RequestId} {Route} completed with {StatusCode} in {DurationMs} ms. BridgeState={BridgeState}; BridgeErrorCode={BridgeErrorCode}; CliExitCode={CliExitCode}",
            context.GetRequestId(),
            context.Request.Path.Value ?? "/",
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            context.GetBridgeState() ?? "unknown",
            context.GetBridgeErrorCode() ?? "none",
            context.GetCliExitCode()
        );
    }
}
