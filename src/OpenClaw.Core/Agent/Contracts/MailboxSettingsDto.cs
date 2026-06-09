namespace OpenClaw.Core.Agent;

/// <summary>
/// Graph-shaped mailbox settings (D6) used by the deterministic availability
/// algorithm in master Section 10.4 (time zone and working hours). Values are not yet
/// sourced from the bridge cache; the mailbox-settings endpoint is deferred to issues
/// #74/#75.
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
