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

    /// <summary>
    /// Extracts the <c>receivedDateTime ge {iso8601}</c> lower bound from a Graph-shaped
    /// OData <c>$filter</c> expression. The returned value is the raw timestamp token, which the
    /// caller validates with <see cref="TryGetUtcTimestamp{T}"/>. When the predicate is absent the
    /// returned value is empty, which surfaces the standard required-parameter error downstream.
    /// </summary>
    public static StringValues ExtractReceivedDateTimeLowerBound(StringValues filterValues)
    {
        var filter = filterValues.ToString();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return StringValues.Empty;
        }

        const string predicate = "receivedDateTime ge ";
        var index = filter.IndexOf(predicate, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return StringValues.Empty;
        }

        var start = index + predicate.Length;
        var remainder = filter[start..].TrimStart();
        var end = remainder.IndexOf(" and ", StringComparison.OrdinalIgnoreCase);
        var token = end < 0 ? remainder : remainder[..end];
        return token.Trim();
    }

    /// <summary>
    /// Determines whether a Graph-shaped OData <c>$filter</c> selects the meeting-requests branch
    /// via the <c>meetingMessageType ne null</c> predicate.
    /// </summary>
    public static bool FilterSelectsMeetingRequests(StringValues filterValues)
    {
        var filter = filterValues.ToString();
        return !string.IsNullOrWhiteSpace(filter)
            && filter.Contains("meetingMessageType ne null", StringComparison.OrdinalIgnoreCase);
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
                "Query parameter '$top' must be an integer.",
                bridge
            );
            return false;
        }

        if (limit <= 0)
        {
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                "Query parameter '$top' must be greater than zero.",
                bridge
            );
            return false;
        }

        if (limit > options.MaxLimit)
        {
            failure = HostAdapterResponses.InvalidRequest<T>(
                requestId,
                options.AdapterVersion,
                $"Query parameter '$top' must not exceed {options.MaxLimit}.",
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
                "Query parameter 'endDateTime' must be later than 'startDateTime'.",
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
