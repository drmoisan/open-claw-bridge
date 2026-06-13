namespace OpenClaw.Core.Agent;

/// <summary>
/// A request to propose candidate meeting times (D4), per master Section 10.4.
/// </summary>
/// <param name="Duration">The required meeting duration.</param>
/// <param name="RequestedPriority">The owner priority governing the scheduling horizon.</param>
/// <param name="Horizon">The window, from now, within which to search for slots.</param>
/// <param name="RequesterEmail">The requester email.</param>
public sealed record SchedulingRequest(
    TimeSpan Duration,
    OwnerPriority RequestedPriority,
    TimeSpan Horizon,
    string RequesterEmail
);
