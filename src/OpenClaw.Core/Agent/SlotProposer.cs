namespace OpenClaw.Core.Agent;

/// <summary>
/// Deterministic availability algorithm (D4). <see cref="ProposeTimes"/> is a pure
/// function over its inputs that returns an ordered list of candidate slots per master
/// Section 10.4. All current-time reads go through the injected
/// <see cref="TimeProvider"/>; no wall-clock time is read directly.
/// </summary>
public static partial class SlotProposer
{
    private const int StepMinutes = 30;
    private const int DefaultMaxResults = 5;

    private static readonly TimeSpan Step = TimeSpan.FromMinutes(StepMinutes);

    /// <summary>
    /// Proposes candidate slots per master Section 10.4: step through the policy window
    /// in 30-minute increments, including a slot only when it is inside working hours,
    /// not in a no-meeting block, free per <paramref name="freeBusy"/>, and starts at
    /// least <c>MinNoticeMinutes</c> after <c>timeProvider.GetUtcNow()</c>; normalize to
    /// the mailbox time zone; order by day preference then start time; and return the
    /// first <paramref name="maxResults"/> slots.
    /// </summary>
    /// <param name="request">The scheduling request. Must not be null.</param>
    /// <param name="mailboxSettings">The mailbox settings (time zone, working days/hours). Must not be null.</param>
    /// <param name="freeBusy">The free/busy schedule. Must not be null.</param>
    /// <param name="policy">The working-hours policy. Must not be null.</param>
    /// <param name="timeProvider">The clock seam. Must not be null.</param>
    /// <param name="maxResults">The maximum number of slots to return (default 5).</param>
    /// <returns>The ordered candidate slots.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxResults"/> is not positive.
    /// </exception>
    public static IReadOnlyList<CandidateSlot> ProposeTimes(
        SchedulingRequest request,
        MailboxSettingsDto mailboxSettings,
        FreeBusyScheduleDto freeBusy,
        WorkingHoursPolicy policy,
        TimeProvider timeProvider,
        int maxResults = DefaultMaxResults
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mailboxSettings);
        ArgumentNullException.ThrowIfNull(freeBusy);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        var timeZone = ResolveTimeZone(mailboxSettings.TimeZoneId);
        var nowUtc = timeProvider.GetUtcNow();
        var earliestStartUtc = nowUtc.AddMinutes(policy.MinNoticeMinutes);
        var windowEndUtc = nowUtc.Add(request.Horizon);

        var workingDays =
            mailboxSettings.WorkingDays.Count > 0
                ? new HashSet<DayOfWeek>(mailboxSettings.WorkingDays)
                : DefaultWorkingDays;

        var candidates = new List<CandidateSlot>();
        var cursorUtc = AlignToStep(nowUtc);

        while (cursorUtc < windowEndUtc)
        {
            var localStart = TimeZoneInfo.ConvertTime(cursorUtc, timeZone);
            var localEnd = localStart.Add(request.Duration);

            if (
                cursorUtc >= earliestStartUtc
                && IsWorkingDay(localStart, workingDays)
                && IsInsideWorkingHours(
                    localStart,
                    localEnd,
                    mailboxSettings.WorkingHoursStart,
                    mailboxSettings.WorkingHoursEnd
                )
                && !OverlapsNoMeetingBlock(localStart, localEnd, policy.NoMeetingBlocks)
                && IsFree(cursorUtc, cursorUtc.Add(request.Duration), freeBusy)
            )
            {
                candidates.Add(new CandidateSlot(localStart, localEnd, mailboxSettings.TimeZoneId));
            }

            cursorUtc = cursorUtc.Add(Step);
        }

        return OrderByPreference(candidates, policy.PreferredDays).Take(maxResults).ToList();
    }
}
