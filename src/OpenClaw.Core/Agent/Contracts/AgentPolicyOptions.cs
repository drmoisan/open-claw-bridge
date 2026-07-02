namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic-agent policy configuration (D5). Bound from the
/// <c>OpenClaw:AgentPolicy</c> configuration section. The values mirror the
/// <c>CONFIG</c> block in master Section 9.2 and the priority/scheduling lists in
/// Sections 10.1 and 10.4. This type is a plain options bag with no dependency on
/// <c>OpenClaw.MailBridge</c>, <c>OpenClaw.HostAdapter</c>, or Outlook COM.
/// </summary>
public sealed class AgentPolicyOptions
{
    // --- Triage lists (master Section 9.2 CONFIG) ---

    /// <summary>Domains treated as internal (for example <c>contoso.com</c>).</summary>
    public IReadOnlyList<string> InternalDomains { get; set; } = Array.Empty<string>();

    /// <summary>Email addresses whose meetings are always protected.</summary>
    public IReadOnlyList<string> VipOrganizers { get; set; } = Array.Empty<string>();

    /// <summary>Categories that mark a meeting as dependency-bearing.</summary>
    public IReadOnlyList<string> ProtectedCategories { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Regular-expression patterns matched against subject + body text to detect
    /// protected meeting series.
    /// </summary>
    public IReadOnlyList<string> ProtectedSubjectPatterns { get; set; } = Array.Empty<string>();

    /// <summary>Attendee count at or above which a meeting counts as large. Default 6.</summary>
    public int LargeMeetingThreshold { get; set; } = 6;

    // --- Owner priority lists (master Section 10.1) ---

    /// <summary>Senders whose requests are immediate (Priority 0).</summary>
    public IReadOnlyList<string> VipEmails { get; set; } = Array.Empty<string>();

    /// <summary>The owner's direct reports.</summary>
    public IReadOnlyList<string> DirectReports { get; set; } = Array.Empty<string>();

    /// <summary>Explicit Priority 1 sender list.</summary>
    public IReadOnlyList<string> Priority1 { get; set; } = Array.Empty<string>();

    /// <summary>Explicit Priority 2 sender list.</summary>
    public IReadOnlyList<string> Priority2 { get; set; } = Array.Empty<string>();

    /// <summary>Explicit Priority 3 sender list.</summary>
    public IReadOnlyList<string> Priority3 { get; set; } = Array.Empty<string>();

    /// <summary>The owner's internal domain (for example <c>contoso.com</c>).</summary>
    public string InternalDomain { get; set; } = string.Empty;

    /// <summary>The non-VIP partner domain treated as Priority 1 (for example <c>emblem.email</c>).</summary>
    public string EmblemEmailDomain { get; set; } = string.Empty;

    // --- Working-hours lists (master Section 10.4) ---

    /// <summary>
    /// Time ranges during the working day in which meetings must not be scheduled,
    /// expressed as local <c>HH:mm-HH:mm</c> strings.
    /// </summary>
    public IReadOnlyList<string> NoMeetingBlocks { get; set; } = Array.Empty<string>();

    /// <summary>Minimum notice, in minutes, before a proposed slot may start.</summary>
    public int MinNoticeMinutes { get; set; }

    /// <summary>
    /// Preferred days of the week, most preferred first (for example
    /// <c>Tuesday, Wednesday, Thursday, Monday, Friday</c>), used for slot ordering.
    /// </summary>
    public IReadOnlyList<string> PreferredDays { get; set; } = Array.Empty<string>();

    // --- Calendar-view fallback (master Section 9.2) ---

    /// <summary>
    /// The forward calendar-view window, in days, fetched when the direct
    /// event-for-message lookup misses (master Section 9.2 uses a 14-day forward
    /// window). Default 14. A non-positive value skips the fallback entirely
    /// (documented opt-out).
    /// </summary>
    public int CalendarViewFallbackDays { get; set; } = 14;

    // --- Kill switches (master Section 7.5) ---

    /// <summary>
    /// When <see langword="false"/> (the default), the agent never sends mail; the
    /// deterministic pipeline still computes and logs.
    /// </summary>
    public bool SendEnabled { get; set; }

    /// <summary>
    /// When <see langword="false"/> (the default), the agent never writes to the
    /// calendar; the deterministic pipeline still computes and logs.
    /// </summary>
    public bool CalendarWriteEnabled { get; set; }
}
