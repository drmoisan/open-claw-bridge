using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

/// <summary>
/// Pure projection from fetched bridge calendar events to a Graph-shaped free/busy schedule
/// (decision D1). An event contributes a <see cref="BusyIntervalDto"/> when its
/// <see cref="EventDto.BusyStatus"/> is not 0 (Outlook <c>OlBusyStatus</c>: 0=free,
/// 1=tentative, 2=busy, 3=outOfOffice). A null <c>BusyStatus</c> is treated as busy
/// (conservative default) so an unannotated event does not silently mark time as free. The
/// projection has no wall-clock dependency and is deterministic for a fixed event list.
/// </summary>
internal static class FreeBusyProjection
{
    /// <summary>
    /// Projects the supplied events to a <see cref="FreeBusyScheduleDto"/> for the given mailbox.
    /// </summary>
    /// <param name="mailboxUpn">The mailbox identifier carried through to <see cref="FreeBusyScheduleDto.MailboxUpn"/>.</param>
    /// <param name="events">The fetched calendar events to project.</param>
    /// <returns>
    /// A free/busy schedule whose <see cref="FreeBusyScheduleDto.BusyIntervals"/> contains one
    /// interval per non-free event, in the order the events were supplied. An empty event list
    /// yields an empty <c>BusyIntervals</c> list.
    /// </returns>
    public static FreeBusyScheduleDto Project(string mailboxUpn, IReadOnlyList<EventDto> events)
    {
        ArgumentNullException.ThrowIfNull(mailboxUpn);
        ArgumentNullException.ThrowIfNull(events);

        var intervals = new List<BusyIntervalDto>(events.Count);
        foreach (var calendarEvent in events)
        {
            if (IsBusy(calendarEvent.BusyStatus))
            {
                intervals.Add(new BusyIntervalDto(calendarEvent.StartUtc, calendarEvent.EndUtc));
            }
        }

        return new FreeBusyScheduleDto(mailboxUpn, intervals);
    }

    /// <summary>
    /// Determines whether an event's <paramref name="busyStatus"/> marks busy time. Any value
    /// other than 0 (free) is busy; a null value is treated as busy (conservative default).
    /// </summary>
    private static bool IsBusy(int? busyStatus) => busyStatus is not 0;
}
