using System.Globalization;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

/// <summary>
/// Registration for the two Graph-shaped scheduling routes added by issue #74:
/// <c>GET /users/{id}/mailboxSettings</c> (config-sourced) and
/// <c>GET /users/{id}/calendar/getSchedule</c> (free/busy computed from bridge calendar data).
/// Extracted from <c>Program.cs</c> to keep that file under the 500-line cap.
/// </summary>
internal static class SchedulingRoutes
{
    public static void MapSchedulingRoutes(this WebApplication app)
    {
        app.MapGet(
            "/users/{id}/mailboxSettings",
            (string id, HttpContext context, IOptions<HostAdapterOptions> optionsAccessor) =>
                HandleMailboxSettings(context, optionsAccessor.Value)
        );

        app.MapGet(
            "/users/{id}/calendar/getSchedule",
            async (
                string id,
                HttpContext context,
                StatusCacheService statusCache,
                HostAdapterCommandBuilder commandBuilder,
                IHostAdapterProcessRunner processRunner,
                IOptions<HostAdapterOptions> optionsAccessor,
                CancellationToken cancellationToken
            ) =>
                await HandleGetScheduleAsync(
                    context,
                    statusCache,
                    commandBuilder,
                    processRunner,
                    optionsAccessor.Value,
                    cancellationToken
                )
        );
    }

    private static IResult HandleMailboxSettings(HttpContext context, HostAdapterOptions options)
    {
        var requestId = context.GetRequestId();
        var settings = options.MailboxSettings;

        if (!TryParseWorkingDays(settings.WorkingDaysOfWeek, out var workingDays))
        {
            return Program.ToHttpResult(
                context,
                HostAdapterResponses.ConfigurationError<MailboxSettingsDto>(
                    requestId,
                    options.AdapterVersion,
                    "MailboxSettings.WorkingDaysOfWeek contains an unrecognized day name."
                )
            );
        }

        if (
            !TryParseTime(settings.WorkingHoursStart, out var start)
            || !TryParseTime(settings.WorkingHoursEnd, out var end)
        )
        {
            return Program.ToHttpResult(
                context,
                HostAdapterResponses.ConfigurationError<MailboxSettingsDto>(
                    requestId,
                    options.AdapterVersion,
                    "MailboxSettings.WorkingHoursStart/WorkingHoursEnd must be HH:mm values."
                )
            );
        }

        var dto = new MailboxSettingsDto(settings.TimeZoneId, workingDays, start, end);
        return Program.ToHttpResult(
            context,
            HostAdapterResponses.Success(dto, requestId, options.AdapterVersion, bridge: null)
        );
    }

    private static async Task<IResult> HandleGetScheduleAsync(
        HttpContext context,
        StatusCacheService statusCache,
        HostAdapterCommandBuilder commandBuilder,
        IHostAdapterProcessRunner processRunner,
        HostAdapterOptions options,
        CancellationToken cancellationToken
    )
    {
        var requestId = context.GetRequestId();
        var (bridgeStatus, failure) = await Program.RequireReadyBridgeAsync<FreeBusyScheduleDto>(
            requestId,
            options,
            statusCache,
            cancellationToken
        );
        if (failure is not null)
        {
            return Program.ToHttpResult(context, failure);
        }

        if (
            !HostAdapterRequestValidation.TryGetUtcTimestamp<FreeBusyScheduleDto>(
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
            return Program.ToHttpResult(context, startFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryGetUtcTimestamp<FreeBusyScheduleDto>(
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
            return Program.ToHttpResult(context, endFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryValidateWindow<FreeBusyScheduleDto>(
                startUtc,
                endUtc,
                requestId,
                options,
                bridgeStatus,
                out var windowFailure
            )
        )
        {
            return Program.ToHttpResult(context, windowFailure!);
        }

        if (
            !HostAdapterRequestValidation.TryGetLimit<FreeBusyScheduleDto>(
                context.Request.Query["$top"],
                requestId,
                options,
                bridgeStatus,
                out var limit,
                out var limitFailure
            )
        )
        {
            return Program.ToHttpResult(context, limitFailure!);
        }

        var eventsResult = await processRunner.ExecuteAsync<ItemsResponse<EventDto>>(
            commandBuilder.BuildListCalendar(startUtc, endUtc, limit),
            requestId,
            bridgeStatus,
            Program.DeserializeItemsResponse<EventDto>,
            cancellationToken
        );

        if (!eventsResult.Envelope.Ok || eventsResult.Envelope.Data is null)
        {
            // Propagate the downstream failure envelope as-is, re-typed to the schedule shape.
            return Program.ToHttpResult(
                context,
                HostAdapterResponses.Failure<FreeBusyScheduleDto>(
                    eventsResult.StatusCode,
                    requestId,
                    options.AdapterVersion,
                    eventsResult.Envelope.Error?.Code ?? "DOWNSTREAM_FAILURE",
                    eventsResult.Envelope.Error?.Message ?? "Unable to fetch calendar events.",
                    eventsResult.Envelope.Meta.Bridge,
                    eventsResult.Envelope.Error?.BridgeErrorCode,
                    eventsResult.Envelope.Error?.Retryable ?? false,
                    eventsResult.CliExitCode
                )
            );
        }

        var schedule = FreeBusyProjection.Project(
            options.MailboxId,
            eventsResult.Envelope.Data.Items
        );
        return Program.ToHttpResult(
            context,
            HostAdapterResponses.Success(
                schedule,
                requestId,
                options.AdapterVersion,
                bridgeStatus,
                eventsResult.CliExitCode
            )
        );
    }

    private static bool TryParseWorkingDays(
        IReadOnlyList<string> names,
        out IReadOnlyList<DayOfWeek> days
    )
    {
        var parsed = new List<DayOfWeek>(names.Count);
        foreach (var name in names)
        {
            if (!Enum.TryParse<DayOfWeek>(name, ignoreCase: true, out var day))
            {
                days = Array.Empty<DayOfWeek>();
                return false;
            }

            parsed.Add(day);
        }

        days = parsed;
        return true;
    }

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out time);
}
