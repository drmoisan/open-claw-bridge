namespace OpenClaw.Core.Agent;

/// <summary>
/// A proposed candidate meeting slot (D4), normalized to the mailbox time zone.
/// </summary>
/// <param name="Start">The slot start.</param>
/// <param name="End">The slot end.</param>
/// <param name="TimeZoneId">The time-zone identifier the start/end are expressed in.</param>
public sealed record CandidateSlot(DateTimeOffset Start, DateTimeOffset End, string TimeZoneId);
