namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Graph-shaped mailbox settings (D6) used by the deterministic availability
/// algorithm in master Section 10.4 (time zone and working hours). Sourced from
/// HostAdapter configuration (<c>OpenClaw:HostAdapter:MailboxSettings</c>) and served
/// over <c>GET /users/{id}/mailboxSettings</c>.
/// </summary>
/// <param name="TimeZoneId">The mailbox time-zone identifier (for example <c>Pacific Standard Time</c>).</param>
/// <param name="WorkingDays">The working days of the week.</param>
/// <param name="WorkingHoursStart">The daily working-hours start time.</param>
/// <param name="WorkingHoursEnd">The daily working-hours end time.</param>
public sealed record MailboxSettingsDto(
    string TimeZoneId,
    IReadOnlyList<DayOfWeek> WorkingDays,
    TimeOnly WorkingHoursStart,
    TimeOnly WorkingHoursEnd
);

/// <summary>
/// A single busy interval in a free/busy schedule (D6).
/// </summary>
/// <param name="Start">The inclusive start of the busy interval (UTC).</param>
/// <param name="End">The exclusive end of the busy interval (UTC).</param>
public sealed record BusyIntervalDto(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Graph-shaped free/busy schedule (D6) consumed by the deterministic availability
/// algorithm in master Section 10.4. A candidate slot is free only when it overlaps
/// no busy interval. Computed in the HostAdapter from bridge calendar data fetched via
/// the CLI client process and served over <c>GET /users/{id}/calendar/getSchedule</c>.
/// </summary>
/// <param name="MailboxUpn">The mailbox the schedule belongs to.</param>
/// <param name="BusyIntervals">The busy intervals (UTC), in any order.</param>
public sealed record FreeBusyScheduleDto(
    string MailboxUpn,
    IReadOnlyList<BusyIntervalDto> BusyIntervals
);
