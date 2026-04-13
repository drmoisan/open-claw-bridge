using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal static class HostAdapterResponseMapper
{
    public static AdapterCommandResult<T> MapFailure<T>(
        string requestId,
        string adapterVersion,
        BridgeStatusDto? bridge,
        RpcError? error,
        string stderr,
        int cliExitCode
    )
    {
        if (
            string.Equals(error?.Code, BridgeErrorCodes.Unauthorized, StringComparison.Ordinal)
            || cliExitCode == 3
        )
        {
            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status401Unauthorized,
                requestId,
                adapterVersion,
                BridgeErrorCodes.Unauthorized,
                error?.Message ?? "The bridge rejected the request as unauthorized.",
                bridge,
                BridgeErrorCodes.Unauthorized,
                false,
                cliExitCode
            );
        }

        if (string.Equals(error?.Code, BridgeErrorCodes.NotFound, StringComparison.Ordinal))
        {
            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status404NotFound,
                requestId,
                adapterVersion,
                BridgeErrorCodes.NotFound,
                error?.Message ?? "The requested bridge resource was not found.",
                bridge,
                BridgeErrorCodes.NotFound,
                false,
                cliExitCode
            );
        }

        if (
            string.Equals(
                error?.Code,
                BridgeErrorCodes.OutlookUnavailable,
                StringComparison.Ordinal
            )
            || cliExitCode == 4
        )
        {
            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status503ServiceUnavailable,
                requestId,
                adapterVersion,
                BridgeErrorCodes.OutlookUnavailable,
                error?.Message ?? "Outlook is currently unavailable.",
                bridge,
                BridgeErrorCodes.OutlookUnavailable,
                true,
                cliExitCode
            );
        }

        if (
            string.Equals(error?.Code, BridgeErrorCodes.InvalidRequest, StringComparison.Ordinal)
            || string.Equals(
                error?.Code,
                BridgeErrorCodes.PayloadTooLarge,
                StringComparison.Ordinal
            )
            || cliExitCode == 5
        )
        {
            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status400BadRequest,
                requestId,
                adapterVersion,
                BridgeErrorCodes.InvalidRequest,
                error?.Message ?? "The request was rejected by the bridge.",
                bridge,
                error?.Code ?? BridgeErrorCodes.InvalidRequest,
                false,
                cliExitCode
            );
        }

        if (cliExitCode == 2)
        {
            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status502BadGateway,
                requestId,
                adapterVersion,
                "TRANSPORT_FAILURE",
                string.IsNullOrWhiteSpace(stderr)
                    ? "The bridge transport was unavailable."
                    : stderr.Trim(),
                bridge,
                retryable: true,
                cliExitCode: cliExitCode
            );
        }

        return HostAdapterResponses.Failure<T>(
            StatusCodes.Status502BadGateway,
            requestId,
            adapterVersion,
            error?.Code ?? "TRANSPORT_FAILURE",
            error?.Message ?? "The bridge request failed.",
            bridge,
            error?.Code,
            cliExitCode == 2,
            cliExitCode
        );
    }
}
