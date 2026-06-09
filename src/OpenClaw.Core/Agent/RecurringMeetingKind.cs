namespace OpenClaw.Core.Agent;

/// <summary>
/// The recurring-meeting sub-classification (D3), per master Section 10.3
/// <c>classify_recurring</c>.
/// </summary>
public enum RecurringMeetingKind
{
    /// <summary>Not a recurring meeting.</summary>
    NON_RECURRING,

    /// <summary>Recurring meeting whose only other attendee is the owner.</summary>
    ONE_ON_ONE,

    /// <summary>Recurring meeting with more than five attendees.</summary>
    RECURRING_FORUM,

    /// <summary>Any other recurring meeting.</summary>
    RECURRING_OTHER,
}
