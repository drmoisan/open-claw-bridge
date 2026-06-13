namespace OpenClaw.HostAdapter;

/// <summary>
/// Configuration for the Graph-shaped <c>GET /users/{id}/mailboxSettings</c> route. The
/// HostAdapter is the local configuration authority for these host-specific settings and does
/// not shell out to the CLI for them. Bound from the
/// <c>OpenClaw:HostAdapter:MailboxSettings</c> subsection; the documented defaults below apply
/// when the subsection is absent.
/// </summary>
public sealed class MailboxSettingsOptions
{
    /// <summary>
    /// The mailbox time-zone identifier. Default <c>"UTC"</c>.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// The working days of the week, by their English <see cref="DayOfWeek"/> names. Default
    /// Monday through Friday.
    /// </summary>
    public string[] WorkingDaysOfWeek { get; set; } =
    ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];

    /// <summary>
    /// The daily working-hours start time in <c>HH:mm</c> form. Default <c>"09:00"</c>.
    /// </summary>
    public string WorkingHoursStart { get; set; } = "09:00";

    /// <summary>
    /// The daily working-hours end time in <c>HH:mm</c> form. Default <c>"17:00"</c>.
    /// </summary>
    public string WorkingHoursEnd { get; set; } = "17:00";
}
