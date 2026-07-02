namespace OpenClaw.Core.Agent;

/// <summary>
/// Pure, stateless composition predicates for the calendar write paths (issue #109).
/// Encodes the three-flag truth table: a calendar write path is allowed only when the
/// <see cref="AgentPolicyOptions.CalendarWriteEnabled"/> global kill switch is
/// <see langword="true"/> AND the path-specific enable flag is <see langword="true"/>;
/// every other combination yields <see langword="false"/>. No I/O, no clock, no state.
/// </summary>
public static class CalendarWritePolicy
{
    /// <summary>
    /// Returns <see langword="true"/> only when
    /// <see cref="AgentPolicyOptions.CalendarWriteEnabled"/> AND
    /// <see cref="AgentPolicyOptions.EnableOrganizerReschedule"/> are both
    /// <see langword="true"/> (truth-table composition; all other rows are
    /// <see langword="false"/>).
    /// </summary>
    /// <param name="options">The bound agent policy options. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static bool OrganizerRescheduleAllowed(AgentPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.CalendarWriteEnabled && options.EnableOrganizerReschedule;
    }

    /// <summary>
    /// Returns <see langword="true"/> only when
    /// <see cref="AgentPolicyOptions.CalendarWriteEnabled"/> AND
    /// <see cref="AgentPolicyOptions.EnableAttendeeProposeNewTime"/> are both
    /// <see langword="true"/> (truth-table composition; all other rows are
    /// <see langword="false"/>).
    /// </summary>
    /// <param name="options">The bound agent policy options. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static bool AttendeeProposeNewTimeAllowed(AgentPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.CalendarWriteEnabled && options.EnableAttendeeProposeNewTime;
    }
}
