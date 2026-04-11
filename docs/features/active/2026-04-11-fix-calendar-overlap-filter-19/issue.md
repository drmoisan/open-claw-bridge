# fix-calendar-overlap-filter (Issue #19)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Promoted -> docs/features/active/fix-calendar-overlap-filter/ (Issue #19)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #19
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/19
- Last Updated: 2026-04-11
- Work Mode: full-bug

## Summary

The calendar scan and the calendar window query both filter events by checking only that the event's start timestamp falls within the requested window. Events that begin before the window boundary but end within or after it — including multi-day events and all-day events — are silently excluded, causing the bridge to return an incomplete calendar snapshot.

## Environment

- OS/version: Windows (net10.0-windows target; Outlook COM required at runtime)
- Runtime: .NET 10
- Component: `OutlookScanner.BuildCalendarFilter` ([OutlookScanner.cs:338-339](src/OpenClaw.MailBridge/OutlookScanner.cs#L338-L339)) and `CacheRepository.ListCalendarWindowAsync` ([CacheRepository.cs:282-283](src/OpenClaw.MailBridge/CacheRepository.cs#L282-L283))
- Data source: Outlook calendar folder (COM) and SQLite events cache

## Steps to Reproduce

1. Create or ensure a calendar event that spans multiple days — for example, an event starting at 08:00 on day N−1 and ending at 17:00 on day N.
2. Issue a `list_calendar_window` request with `start = day N 00:00 UTC` and `end = day N+7 23:59 UTC`.
3. Observe the response.

## Expected Behavior

The multi-day event is included in the response because its end time (`day N 17:00`) falls within the requested window, satisfying the correct overlap condition: `event_start <= window_end AND event_end >= window_start`.

## Actual Behavior

The event is excluded. The current Outlook DASL filter `[Start] >= '{start}' AND [Start] < '{end}'` only admits events whose start falls inside the window. Because the event's start is on day N−1, it does not satisfy `[Start] >= day N 00:00` and is never loaded into the cache during the scan. The SQLite query (`start_utc >= $start_utc AND start_utc < $end_utc`) applies identical start-only logic and would likewise exclude the event even if it were in the cache.

No error is raised; the missing event is silently omitted from results.

## Logs / Screenshots

- [ ] Attached minimal logs or screenshot
- Snippet: No log output distinguishes a filtered-out event from one that never existed; the defect is observable only by comparing bridge output against a known appointment set.

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Listed as **Critical / Blocking** in the design audit exit-criteria matrix: "Calendar overlap filter per spec — **FAIL** — Events partially missed."

Events affected include:
- All-day events (which Outlook stores with `Start` at midnight and `End` at the following midnight).
- Multi-day events.
- Any event whose start predates the window start by even one second.

This means a routine `list_calendar_window` call for the current week can silently omit events that are actively ongoing or that started the previous day.

## Suspected Cause / Notes

The filter was written to use a simpler "start-within-window" condition rather than the correct interval-overlap condition. The defect exists in two independent locations that must both be corrected:

1. **`OutlookScanner.BuildCalendarFilter` ([OutlookScanner.cs:338-339](src/OpenClaw.MailBridge/OutlookScanner.cs#L338-L339))** — the Outlook DASL/RDO filter string used when querying the COM calendar folder during the background scan. Controls which events are loaded into the SQLite cache.

   Current:
   ```
   [Start] >= 'MM/dd/yyyy hh:mm tt' AND [Start] < 'MM/dd/yyyy hh:mm tt'
   ```
   Required:
   ```
   [Start] <= 'MM/dd/yyyy hh:mm tt' AND [End] >= 'MM/dd/yyyy hh:mm tt'
   ```

2. **`CacheRepository.ListCalendarWindowAsync` ([CacheRepository.cs:282-283](src/OpenClaw.MailBridge/CacheRepository.cs#L282-L283))** — the SQLite query used to serve `list_calendar_window` pipe requests from the cache.

   Current:
   ```sql
   WHERE start_utc >= $start_utc
     AND start_utc < $end_utc
   ```
   Required:
   ```sql
   WHERE start_utc <= $end_utc
     AND end_utc >= $start_utc
   ```

   The `events` table has an `end_utc` column (already stored) so no schema change is required.

The Outlook DASL filter uses `LocalDateTime` formatting; the corrected filter must apply the same formatting to both the `[End]` and `[Start]` sides. Outlook's `Restrict` method uses the same column names (`[Start]`, `[End]`) on the Items collection.

## Proposed Fix / Validation Ideas

- [x] Unit test: `BuildCalendarFilter` with a window of `(2026-04-10T00:00Z, 2026-04-17T00:00Z)` produces a filter string of the form `[Start] <= '...' AND [End] >= '...'` with the correctly formatted local-time strings for both sides.
- [x] Unit test: `BuildCalendarFilter` result does not contain `[Start] <` or `[Start] >=` predicates.
- [x] Unit test (CacheRepository): inserting an event with `start_utc = 2026-04-09` and `end_utc = 2026-04-11` and querying with `start = 2026-04-10`, `end = 2026-04-17` returns the event (currently returns empty).
- [x] Unit test (CacheRepository): an event with `start_utc = 2026-04-16` and `end_utc = 2026-04-18` is included in the same query window (event starts inside, ends outside).
- [x] Unit test (CacheRepository): an event with `start_utc = 2026-04-09` and `end_utc = 2026-04-18` (spans the entire window) is included.
- [x] Unit test (CacheRepository): an event with `start_utc = 2026-04-17T01:00Z` (starts after window end) is excluded.
- [x] Unit test (CacheRepository): an event with `end_utc = 2026-04-09T23:59Z` (ends before window start) is excluded.
- [ ] Integration scenario: run `list_calendar_window` against a bridge connected to an Outlook calendar containing a multi-day event that starts one day before the query window; verify the event appears in the response.
- [ ] Manual verification: confirm that Outlook `Restrict` with `[Start] <= '{end}' AND [End] >= '{start}'` is a valid DASL filter syntax for the Outlook Items collection (cannot be verified without a live Outlook instance).

## Next Step

- [ ] Promote to GitHub issue (bug-report template)
- [ ] Move to active fix folder / branch