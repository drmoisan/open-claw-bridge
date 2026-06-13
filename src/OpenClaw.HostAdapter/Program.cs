using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

var builder = WebApplication.CreateBuilder(args);
var externalAppSettingsPath =
    builder.Configuration[$"{HostAdapterOptions.SectionName}:AppSettingsPath"]
    ?? HostAdapterOptions.DefaultAppSettingsPath;

if (File.Exists(externalAppSettingsPath))
{
    builder.Configuration.AddJsonFile(
        externalAppSettingsPath,
        optional: true,
        reloadOnChange: false
    );
}

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Services.AddMemoryCache();
builder
    .Services.AddOptions<HostAdapterOptions>()
    .Bind(builder.Configuration.GetSection(HostAdapterOptions.SectionName))
    .PostConfigure(options =>
    {
        options.AppSettingsPath = string.IsNullOrWhiteSpace(options.AppSettingsPath)
            ? externalAppSettingsPath
            : options.AppSettingsPath;
        options.TokenFilePath = string.IsNullOrWhiteSpace(options.TokenFilePath)
            ? HostAdapterOptions.DefaultTokenFilePath
            : options.TokenFilePath;
        options.ClientExecutablePath = string.IsNullOrWhiteSpace(options.ClientExecutablePath)
            ? HostAdapterOptions.DefaultClientExecutablePath
            : options.ClientExecutablePath;
        options.DefaultLimit = options.DefaultLimit <= 0 ? 100 : options.DefaultLimit;
        options.MaxLimit = options.MaxLimit <= 0 ? 250 : options.MaxLimit;
        options.AdapterVersion = string.IsNullOrWhiteSpace(options.AdapterVersion)
            ? HostAdapterOptions.DefaultAdapterVersion
            : options.AdapterVersion;
        options.MailboxId = string.IsNullOrWhiteSpace(options.MailboxId) ? "me" : options.MailboxId;

        // The configuration binder appends array elements onto a pre-initialized default
        // array rather than replacing it. When an operator supplies MailboxSettings:
        // WorkingDaysOfWeek, replace the default Monday–Friday array with exactly the
        // configured entries so config overrides (not augments) the defaults.
        var workingDaysSection = builder.Configuration.GetSection(
            $"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingDaysOfWeek"
        );
        if (workingDaysSection.Exists())
        {
            options.MailboxSettings.WorkingDaysOfWeek =
            [
                .. workingDaysSection
                    .GetChildren()
                    .Select(child => child.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!),
            ];
        }
    });
builder.Services.AddSingleton<HostAdapterCommandBuilder>();
builder.Services.AddSingleton<IHostAdapterProcessRunner, HostAdapterProcessRunner>();
builder.Services.AddSingleton<IHostAdapterTokenProvider, FileHostAdapterTokenProvider>();
builder.Services.AddSingleton<StatusCacheService>();

var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<BearerTokenMiddleware>();

app.MapGet(
    "/status",
    async (
        HttpContext context,
        StatusCacheService statusCache,
        CancellationToken cancellationToken
    ) =>
    {
        var result = await statusCache.GetStatusAsync(context.GetRequestId(), cancellationToken);
        return ToHttpResult(context, result);
    }
);

app.MapGet(
    "/users/{id}/messages",
    async (
        string id,
        HttpContext context,
        StatusCacheService statusCache,
        HostAdapterCommandBuilder commandBuilder,
        IHostAdapterProcessRunner processRunner,
        IOptions<HostAdapterOptions> optionsAccessor,
        CancellationToken cancellationToken
    ) =>
    {
        var requestId = context.GetRequestId();
        var options = optionsAccessor.Value;
        var (bridgeStatus, failure) = await RequireReadyBridgeAsync<ItemsResponse<MessageDto>>(
            requestId,
            options,
            statusCache,
            cancellationToken
        );
        if (failure is not null)
        {
            return ToHttpResult(context, failure);
        }

        var filter = context.Request.Query["$filter"];
        var sinceValues = HostAdapterRequestValidation.ExtractReceivedDateTimeLowerBound(filter);
        if (
            !HostAdapterRequestValidation.TryGetUtcTimestamp<ItemsResponse<MessageDto>>(
                sinceValues,
                "$filter (receivedDateTime ge)",
                requestId,
                options,
                bridgeStatus,
                out var sinceUtc,
                out var timestampFailure
            )
        )
        {
            return ToHttpResult(context, timestampFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryGetLimit<ItemsResponse<MessageDto>>(
                context.Request.Query["$top"],
                requestId,
                options,
                bridgeStatus,
                out var limit,
                out var limitFailure
            )
        )
        {
            return ToHttpResult(context, limitFailure!);
        }

        var command = HostAdapterRequestValidation.FilterSelectsMeetingRequests(filter)
            ? commandBuilder.BuildListMeetingRequests(sinceUtc, limit)
            : commandBuilder.BuildListMessages(sinceUtc, limit);

        var result = await processRunner.ExecuteAsync<ItemsResponse<MessageDto>>(
            command,
            requestId,
            bridgeStatus,
            DeserializeItemsResponse<MessageDto>,
            cancellationToken
        );
        return ToHttpResult(context, result);
    }
);

app.MapGet(
    "/users/{id}/messages/{messageId}",
    async (
        string id,
        string messageId,
        HttpContext context,
        StatusCacheService statusCache,
        HostAdapterCommandBuilder commandBuilder,
        IHostAdapterProcessRunner processRunner,
        IOptions<HostAdapterOptions> optionsAccessor,
        CancellationToken cancellationToken
    ) =>
    {
        var requestId = context.GetRequestId();
        var options = optionsAccessor.Value;
        var (bridgeStatus, failure) = await RequireReadyBridgeAsync<MessageDto>(
            requestId,
            options,
            statusCache,
            cancellationToken
        );
        if (failure is not null)
        {
            return ToHttpResult(context, failure);
        }

        if (
            !HostAdapterRequestValidation.TryGetBridgeId<MessageDto>(
                messageId,
                requestId,
                options,
                bridgeStatus,
                out var normalizedBridgeId,
                out var bridgeIdFailure
            )
        )
        {
            return ToHttpResult(context, bridgeIdFailure!);
        }

        var result = await processRunner.ExecuteAsync(
            commandBuilder.BuildGetMessage(normalizedBridgeId),
            requestId,
            bridgeStatus,
            HostAdapterProcessRunner.DeserializePayload<MessageDto>,
            cancellationToken
        );
        return ToHttpResult(context, result);
    }
);

app.MapGet(
    "/users/{id}/calendarView",
    async (
        string id,
        HttpContext context,
        StatusCacheService statusCache,
        HostAdapterCommandBuilder commandBuilder,
        IHostAdapterProcessRunner processRunner,
        IOptions<HostAdapterOptions> optionsAccessor,
        CancellationToken cancellationToken
    ) =>
    {
        var requestId = context.GetRequestId();
        var options = optionsAccessor.Value;
        var (bridgeStatus, failure) = await RequireReadyBridgeAsync<ItemsResponse<EventDto>>(
            requestId,
            options,
            statusCache,
            cancellationToken
        );
        if (failure is not null)
        {
            return ToHttpResult(context, failure);
        }

        if (
            !HostAdapterRequestValidation.TryGetUtcTimestamp<ItemsResponse<EventDto>>(
                context.Request.Query["startDateTime"],
                "startDateTime",
                requestId,
                options,
                bridgeStatus,
                out var startUtc,
                out var startFailure
            )
        )
        {
            return ToHttpResult(context, startFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryGetUtcTimestamp<ItemsResponse<EventDto>>(
                context.Request.Query["endDateTime"],
                "endDateTime",
                requestId,
                options,
                bridgeStatus,
                out var endUtc,
                out var endFailure
            )
        )
        {
            return ToHttpResult(context, endFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryValidateWindow<ItemsResponse<EventDto>>(
                startUtc,
                endUtc,
                requestId,
                options,
                bridgeStatus,
                out var windowFailure
            )
        )
        {
            return ToHttpResult(context, windowFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryGetLimit<ItemsResponse<EventDto>>(
                context.Request.Query["$top"],
                requestId,
                options,
                bridgeStatus,
                out var limit,
                out var limitFailure
            )
        )
        {
            return ToHttpResult(context, limitFailure!);
        }

        var result = await processRunner.ExecuteAsync<ItemsResponse<EventDto>>(
            commandBuilder.BuildListCalendar(startUtc, endUtc, limit),
            requestId,
            bridgeStatus,
            DeserializeItemsResponse<EventDto>,
            cancellationToken
        );
        return ToHttpResult(context, result);
    }
);

app.MapGet(
    "/users/{id}/events/{eventId}",
    async (
        string id,
        string eventId,
        HttpContext context,
        StatusCacheService statusCache,
        HostAdapterCommandBuilder commandBuilder,
        IHostAdapterProcessRunner processRunner,
        IOptions<HostAdapterOptions> optionsAccessor,
        CancellationToken cancellationToken
    ) =>
    {
        var requestId = context.GetRequestId();
        var options = optionsAccessor.Value;
        var (bridgeStatus, failure) = await RequireReadyBridgeAsync<EventDto>(
            requestId,
            options,
            statusCache,
            cancellationToken
        );
        if (failure is not null)
        {
            return ToHttpResult(context, failure);
        }

        if (
            !HostAdapterRequestValidation.TryGetBridgeId<EventDto>(
                eventId,
                requestId,
                options,
                bridgeStatus,
                out var normalizedBridgeId,
                out var bridgeIdFailure
            )
        )
        {
            return ToHttpResult(context, bridgeIdFailure!);
        }

        var result = await processRunner.ExecuteAsync(
            commandBuilder.BuildGetEvent(normalizedBridgeId),
            requestId,
            bridgeStatus,
            HostAdapterProcessRunner.DeserializePayload<EventDto>,
            cancellationToken
        );
        return ToHttpResult(context, result);
    }
);

app.MapSchedulingRoutes();

app.Run();

internal partial class Program
{
    internal static IResult ToHttpResult<T>(HttpContext context, AdapterCommandResult<T> result)
    {
        context.SetHostAdapterTelemetry(
            result.Envelope.Meta.Bridge?.State,
            result.Envelope.Error?.BridgeErrorCode ?? result.Envelope.Error?.Code,
            result.CliExitCode
        );
        return Results.Json(result.Envelope, statusCode: result.StatusCode);
    }

    internal static async Task<(
        BridgeStatusDto? BridgeStatus,
        AdapterCommandResult<T>? Failure
    )> RequireReadyBridgeAsync<T>(
        string requestId,
        HostAdapterOptions options,
        StatusCacheService statusCache,
        CancellationToken cancellationToken
    )
    {
        var statusResult = await statusCache.GetStatusAsync(requestId, cancellationToken);
        if (
            statusResult.StatusCode != StatusCodes.Status200OK
            || statusResult.Envelope.Data is null
        )
        {
            return (
                null,
                HostAdapterResponses.Failure<T>(
                    statusResult.StatusCode,
                    requestId,
                    options.AdapterVersion,
                    statusResult.Envelope.Error?.Code ?? "DOWNSTREAM_FAILURE",
                    statusResult.Envelope.Error?.Message ?? "Unable to obtain bridge status.",
                    statusResult.Envelope.Meta.Bridge,
                    statusResult.Envelope.Error?.BridgeErrorCode,
                    statusResult.Envelope.Error?.Retryable ?? false,
                    statusResult.CliExitCode
                )
            );
        }

        if (IsBridgeNotReady(statusResult.Envelope.Data))
        {
            return (
                statusResult.Envelope.Data,
                HostAdapterResponses.BridgeNotReady<T>(
                    requestId,
                    options.AdapterVersion,
                    statusResult.Envelope.Data,
                    statusResult.CliExitCode
                )
            );
        }

        return (statusResult.Envelope.Data, null);
    }

    internal static bool IsBridgeNotReady(BridgeStatusDto bridgeStatus)
    {
        return string.Equals(
                bridgeStatus.State,
                BridgeState.starting.ToString(),
                StringComparison.OrdinalIgnoreCase
            )
            || string.Equals(
                bridgeStatus.State,
                BridgeState.waiting_for_outlook.ToString(),
                StringComparison.OrdinalIgnoreCase
            );
    }

    internal static ItemsResponse<TItem> DeserializeItemsResponse<TItem>(JsonElement element)
    {
        return HostAdapterProcessRunner.DeserializePayload<ItemsResponse<TItem>>(element);
    }
}
