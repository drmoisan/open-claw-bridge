namespace OpenClaw.Core.Agent;

/// <summary>
/// A single busy interval in a free/busy schedule (D6).
/// </summary>
/// <param name="Start">The inclusive start of the busy interval (UTC).</param>
/// <param name="End">The exclusive end of the busy interval (UTC).</param>
public sealed record BusyIntervalDto(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Graph-shaped free/busy schedule (D6) consumed by the deterministic availability
/// algorithm in master Section 10.4. A candidate slot is free only when it overlaps
/// no busy interval. The free/busy endpoint is deferred to issues #74/#75; the runtime
/// adapter throws until it is available.
/// </summary>
/// <param name="MailboxUpn">The mailbox the schedule belongs to.</param>
/// <param name="BusyIntervals">The busy intervals (UTC), in any order.</param>
public sealed record FreeBusyScheduleDto(
    string MailboxUpn,
    IReadOnlyList<BusyIntervalDto> BusyIntervals
);
