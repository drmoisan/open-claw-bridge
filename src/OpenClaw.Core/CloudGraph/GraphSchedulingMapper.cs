using System.Globalization;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Pure static mapping for the scheduling endpoints: Graph <c>mailboxSettings</c> to
/// <see cref="MailboxSettingsDto"/> and the Graph <c>getSchedule</c> response to
/// <see cref="FreeBusyScheduleDto"/>. Busy classification is conservative (D11):
/// <c>busy</c>, <c>oof</c>, and <c>tentative</c> block; <c>free</c> and
/// <c>workingElsewhere</c> do not. No I/O and no mutation; fields the DTOs require
/// fail fast with <see cref="GraphMappingException"/> rather than fabricating data.
/// </summary>
internal static class GraphSchedulingMapper
{
    /// <summary>
    /// Maps Graph <c>mailboxSettings</c> to <see cref="MailboxSettingsDto"/>
    /// (<c>TimeZoneId</c>, working days, working-hours start/end).
    /// </summary>
    /// <exception cref="GraphMappingException">A DTO-required field is missing or unparseable.</exception>
    public static MailboxSettingsDto MapMailboxSettings(GraphMailboxSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.TimeZone))
        {
            throw new GraphMappingException(
                "The Graph mailboxSettings response is missing the required field 'timeZone'."
            );
        }

        var workingHours =
            settings.WorkingHours
            ?? throw new GraphMappingException(
                "The Graph mailboxSettings response is missing the required field 'workingHours'."
            );

        return new MailboxSettingsDto(
            settings.TimeZone,
            MapWorkingDays(workingHours.DaysOfWeek),
            ParseTime(workingHours.StartTime, "workingHours.startTime"),
            ParseTime(workingHours.EndTime, "workingHours.endTime")
        );
    }

    /// <summary>
    /// Maps a Graph <c>getSchedule</c> response to <see cref="FreeBusyScheduleDto"/>:
    /// <c>value[0].scheduleItems</c> with a D11 busy status become UTC
    /// <see cref="BusyIntervalDto"/> values. An empty window yields an empty list, not
    /// an error (interface contract).
    /// </summary>
    /// <param name="mailboxUpn">The principal mailbox the schedule belongs to.</param>
    /// <param name="response">The deserialized <c>getSchedule</c> response.</param>
    public static FreeBusyScheduleDto MapFreeBusy(string mailboxUpn, GraphScheduleResponse response)
    {
        ArgumentNullException.ThrowIfNull(mailboxUpn);
        ArgumentNullException.ThrowIfNull(response);

        var scheduleItems = response.Value is { Count: > 0 }
            ? response.Value[0].ScheduleItems
            : null;

        var busyIntervals = new List<BusyIntervalDto>();
        foreach (var item in scheduleItems ?? [])
        {
            if (IsBusyStatus(item.Status))
            {
                busyIntervals.Add(
                    new BusyIntervalDto(
                        GraphEventMapper.ToUtc(item.Start, "scheduleItems[].start"),
                        GraphEventMapper.ToUtc(item.End, "scheduleItems[].end")
                    )
                );
            }
        }

        return new FreeBusyScheduleDto(mailboxUpn, busyIntervals);
    }

    /// <summary>
    /// D11 partition: <c>busy</c>/<c>oof</c>/<c>tentative</c> block the slot proposer;
    /// <c>free</c>/<c>workingElsewhere</c> (and unknown values) do not.
    /// </summary>
    internal static bool IsBusyStatus(string? status) => status is "busy" or "oof" or "tentative";

    private static IReadOnlyList<DayOfWeek> MapWorkingDays(IReadOnlyList<string>? daysOfWeek)
    {
        var days = new List<DayOfWeek>();
        foreach (var day in daysOfWeek ?? [])
        {
            if (!Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var parsed))
            {
                throw new GraphMappingException(
                    "The Graph mailboxSettings workingHours.daysOfWeek list carries an unknown day name."
                );
            }

            days.Add(parsed);
        }

        return days;
    }

    private static TimeOnly ParseTime(string? value, string fieldName)
    {
        if (
            string.IsNullOrWhiteSpace(value)
            || !TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
        )
        {
            throw new GraphMappingException(
                $"The Graph mailboxSettings field '{fieldName}' is missing or unparseable."
            );
        }

        return parsed;
    }
}
