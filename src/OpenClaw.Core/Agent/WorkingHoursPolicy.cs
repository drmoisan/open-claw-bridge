using System.Globalization;

namespace OpenClaw.Core.Agent;

/// <summary>
/// A local-time no-meeting block, expressed as a half-open <c>[Start, End)</c> range
/// within a working day.
/// </summary>
/// <param name="Start">The inclusive block start (local time).</param>
/// <param name="End">The exclusive block end (local time).</param>
public sealed record NoMeetingBlock(TimeOnly Start, TimeOnly End);

/// <summary>
/// The value model holding the working-hours scheduling policy (D4), projected from
/// <see cref="AgentPolicyOptions"/>. Mirrors the working-hours, minimum-notice,
/// no-meeting-block, and day-preference inputs of master Section 10.4. Working days and
/// the daily start/end window are sourced from the mailbox settings at proposal time;
/// this policy carries the no-meeting blocks, minimum notice, and day preference.
/// </summary>
public sealed class WorkingHoursPolicy
{
    private WorkingHoursPolicy(
        IReadOnlyList<NoMeetingBlock> noMeetingBlocks,
        int minNoticeMinutes,
        IReadOnlyList<DayOfWeek> preferredDays
    )
    {
        NoMeetingBlocks = noMeetingBlocks;
        MinNoticeMinutes = minNoticeMinutes;
        PreferredDays = preferredDays;
    }

    /// <summary>The local-time no-meeting blocks.</summary>
    public IReadOnlyList<NoMeetingBlock> NoMeetingBlocks { get; }

    /// <summary>The minimum notice, in minutes, before a proposed slot may start.</summary>
    public int MinNoticeMinutes { get; }

    /// <summary>The preferred days of the week, most preferred first.</summary>
    public IReadOnlyList<DayOfWeek> PreferredDays { get; }

    /// <summary>
    /// Projects an <see cref="AgentPolicyOptions"/> instance into a
    /// <see cref="WorkingHoursPolicy"/>. No-meeting blocks are parsed from
    /// <c>HH:mm-HH:mm</c> strings; preferred days are parsed from day-of-week names.
    /// </summary>
    /// <param name="options">The source options. Must not be null.</param>
    /// <returns>The projected working-hours policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when a no-meeting block or preferred day is malformed.
    /// </exception>
    public static WorkingHoursPolicy FromOptions(AgentPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var blocks = options.NoMeetingBlocks.Select(ParseBlock).ToList();
        var preferredDays = options.PreferredDays.Select(ParseDay).ToList();

        return new WorkingHoursPolicy(blocks, options.MinNoticeMinutes, preferredDays);
    }

    private static NoMeetingBlock ParseBlock(string raw)
    {
        var parts = raw.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new FormatException(
                $"No-meeting block '{raw}' must be in the form 'HH:mm-HH:mm'."
            );
        }

        var start = TimeOnly.ParseExact(parts[0], "HH:mm", CultureInfo.InvariantCulture);
        var end = TimeOnly.ParseExact(parts[1], "HH:mm", CultureInfo.InvariantCulture);
        return new NoMeetingBlock(start, end);
    }

    private static DayOfWeek ParseDay(string raw)
    {
        if (Enum.TryParse<DayOfWeek>(raw.Trim(), ignoreCase: true, out var day))
        {
            return day;
        }

        throw new FormatException($"Preferred day '{raw}' is not a valid day of week.");
    }
}
