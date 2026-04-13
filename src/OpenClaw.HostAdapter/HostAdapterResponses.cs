using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal sealed record AdapterCommandResult<T>(
    ApiEnvelope<T> Envelope,
    int StatusCode,
    int? CliExitCode = null
);

internal static class HostAdapterResponses
{
    public static AdapterCommandResult<T> Success<T>(
        T data,
        string requestId,
        string adapterVersion,
        BridgeStatusDto? bridge,
        int? cliExitCode = null
    )
    {
        var bridgeSnapshot = data is BridgeStatusDto status ? status : bridge;
        return new AdapterCommandResult<T>(
            new ApiEnvelope<T>(
                true,
                data,
                new ApiMeta(requestId, adapterVersion, bridgeSnapshot),
                null
            ),
            StatusCodes.Status200OK,
            cliExitCode
        );
    }

    public static AdapterCommandResult<T> Failure<T>(
        int statusCode,
        string requestId,
        string adapterVersion,
        string code,
        string message,
        BridgeStatusDto? bridge = null,
        string? bridgeErrorCode = null,
        bool retryable = false,
        int? cliExitCode = null
    )
    {
        return new AdapterCommandResult<T>(
            new ApiEnvelope<T>(
                false,
                default,
                new ApiMeta(requestId, adapterVersion, bridge),
                new ApiError(code, message, bridgeErrorCode, retryable)
            ),
            statusCode,
            cliExitCode
        );
    }

    public static AdapterCommandResult<T> InvalidRequest<T>(
        string requestId,
        string adapterVersion,
        string message,
        BridgeStatusDto? bridge = null
    )
    {
        return Failure<T>(
            StatusCodes.Status400BadRequest,
            requestId,
            adapterVersion,
            BridgeErrorCodes.InvalidRequest,
            message,
            bridge,
            BridgeErrorCodes.InvalidRequest,
            false
        );
    }

    public static AdapterCommandResult<T> BridgeNotReady<T>(
        string requestId,
        string adapterVersion,
        BridgeStatusDto bridge,
        int? cliExitCode = null
    )
    {
        return Failure<T>(
            StatusCodes.Status409Conflict,
            requestId,
            adapterVersion,
            "BRIDGE_NOT_READY",
            "The bridge is not ready to serve live data.",
            bridge,
            bridge.State,
            true,
            cliExitCode
        );
    }

    public static AdapterCommandResult<T> ConfigurationError<T>(
        string requestId,
        string adapterVersion,
        string message
    )
    {
        return Failure<T>(
            StatusCodes.Status500InternalServerError,
            requestId,
            adapterVersion,
            "CONFIGURATION_ERROR",
            message
        );
    }
}
