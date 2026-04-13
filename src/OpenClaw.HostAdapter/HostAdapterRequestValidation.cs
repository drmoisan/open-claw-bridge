using System.Globalization;
using Microsoft.Extensions.Primitives;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal static class HostAdapterRequestValidation
{
    public static bool TryGetUtcTimestamp<T>(
        StringValues values,
        string parameterName,
        string requestId,
        HostAdapterOptions options,
        BridgeStatusDto? bridge,
        out DateTimeOffset timestamp,
        out AdapterCommandResult<T>? failure
    )
    {
        var rawValue = values.ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            timestamp = default;
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                $"Query parameter '{parameterName}' is required.",
                bridge
            );
            return false;
        }

        if (
            !DateTimeOffset.TryParse(
                rawValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp
            )
            || timestamp.Offset != TimeSpan.Zero
        )
        {
            timestamp = default;
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                $"Query parameter '{parameterName}' must be an ISO-8601 UTC timestamp.",
                bridge
            );
            return false;
        }

        failure = null;
        return true;
    }

    public static bool TryGetLimit<T>(
        StringValues values,
        string requestId,
        HostAdapterOptions options,
        BridgeStatusDto? bridge,
        out int limit,
        out AdapterCommandResult<T>? failure
    )
    {
        var rawValue = values.ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            limit = options.DefaultLimit;
            failure = null;
            return true;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit))
        {
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                "Query parameter 'limit' must be an integer.",
                bridge
            );
            return false;
        }

        if (limit <= 0)
        {
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                "Query parameter 'limit' must be greater than zero.",
                bridge
            );
            return false;
        }

        if (limit > options.MaxLimit)
        {
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                $"Query parameter 'limit' must not exceed {options.MaxLimit}.",
                bridge
            );
            return false;
        }

        failure = null;
        return true;
    }

    public static bool TryValidateWindow<T>(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string requestId,
        HostAdapterOptions options,
        BridgeStatusDto? bridge,
        out AdapterCommandResult<T>? failure
    )
    {
        if (endUtc <= startUtc)
        {
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                "Query parameter 'end' must be later than 'start'.",
                bridge
            );
            return false;
        }

        failure = null;
        return true;
    }

    public static bool TryGetBridgeId<T>(
        string? bridgeId,
        string requestId,
        HostAdapterOptions options,
        BridgeStatusDto? bridge,
        out string normalizedBridgeId,
        out AdapterCommandResult<T>? failure
    )
    {
        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            normalizedBridgeId = string.Empty;
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                "Route parameter 'bridgeId' is required.",
                bridge
            );
            return false;
        }

        normalizedBridgeId = bridgeId;
        failure = null;
        return true;
    }
}
