namespace OpenClaw.Core.Agent;

/// <summary>
/// Window-computation and ordering helpers for <see cref="SlotProposer"/> (D4). Kept in
/// a separate partial to hold the main proposer file under the 500-line limit.
/// </summary>
public static partial class SlotProposer
{
    private static readonly IReadOnlySet<DayOfWeek> DefaultWorkingDays = new HashSet<DayOfWeek>
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    };

    /// <summary>
    /// Resolves the mailbox time zone by identifier, falling back to UTC when the
    /// identifier is empty or cannot be resolved on the host.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Rounds <paramref name="value"/> up to the next 30-minute step boundary (in UTC),
    /// so candidate starts fall on deterministic half-hour marks.
    /// </summary>
    private static DateTimeOffset AlignToStep(DateTimeOffset value)
    {
        var minutesPastStep =
            (value.Minute % StepMinutes) != 0 || value.Second != 0 || value.Millisecond != 0;
        var truncated = new DateTimeOffset(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute - (value.Minute % StepMinutes),
            0,
            value.Offset
        );
        return minutesPastStep ? truncated.Add(Step) : truncated;
    }

    private static bool IsWorkingDay(
        DateTimeOffset localStart,
        IReadOnlySet<DayOfWeek> workingDays
    ) => workingDays.Contains(localStart.DayOfWeek);

    /// <summary>
    /// Returns whether the half-open local interval <c>[localStart, localEnd)</c> fits
    /// entirely within the daily working-hours window and does not cross midnight.
    /// </summary>
    private static bool IsInsideWorkingHours(
        DateTimeOffset localStart,
        DateTimeOffset localEnd,
        TimeOnly workingStart,
        TimeOnly workingEnd
    )
    {
        // A meeting that crosses midnight is rejected (localEnd on a later day).
        if (localEnd.Date != localStart.Date)
        {
            return localEnd.TimeOfDay == TimeSpan.Zero
                && TimeOnly.FromTimeSpan(localStart.TimeOfDay) >= workingStart
                && workingEnd == new TimeOnly(0, 0);
        }

        var startTime = TimeOnly.FromTimeSpan(localStart.TimeOfDay);
        var endTime = TimeOnly.FromTimeSpan(localEnd.TimeOfDay);
        return startTime >= workingStart && endTime <= workingEnd;
    }

    /// <summary>
    /// Returns whether the local interval overlaps any no-meeting block. Two half-open
    /// intervals overlap when each starts before the other ends.
    /// </summary>
    private static bool OverlapsNoMeetingBlock(
        DateTimeOffset localStart,
        DateTimeOffset localEnd,
        IReadOnlyList<NoMeetingBlock> blocks
    )
    {
        var startTime = TimeOnly.FromTimeSpan(localStart.TimeOfDay);
        var endTime =
            localEnd.Date != localStart.Date
                ? new TimeOnly(23, 59, 59)
                : TimeOnly.FromTimeSpan(localEnd.TimeOfDay);

        foreach (var block in blocks)
        {
            if (startTime < block.End && block.Start < endTime)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether the UTC interval <c>[startUtc, endUtc)</c> overlaps no busy
    /// interval in the supplied free/busy schedule.
    /// </summary>
    private static bool IsFree(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        FreeBusyScheduleDto freeBusy
    )
    {
        foreach (var busy in freeBusy.BusyIntervals)
        {
            if (startUtc < busy.End && busy.Start < endUtc)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Orders candidates by day preference (most-preferred day first; unlisted days
    /// after listed ones) then by start time, deterministically.
    /// </summary>
    private static IEnumerable<CandidateSlot> OrderByPreference(
        IReadOnlyList<CandidateSlot> candidates,
        IReadOnlyList<DayOfWeek> preferredDays
    )
    {
        var rank = new Dictionary<DayOfWeek, int>();
        for (var i = 0; i < preferredDays.Count; i++)
        {
            rank.TryAdd(preferredDays[i], i);
        }

        var unlistedRank = preferredDays.Count;
        return candidates
            .OrderBy(slot => rank.TryGetValue(slot.Start.DayOfWeek, out var r) ? r : unlistedRank)
            .ThenBy(slot => slot.Start);
    }
}
