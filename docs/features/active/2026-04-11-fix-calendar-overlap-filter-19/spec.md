# 2026-04-11-fix-calendar-overlap-filter (Spec)

- **Issue:** #19
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-11T11-02
- **Status:** Draft
- **Version:** 0.1

## Context
The calendar scan and the calendar window query both filter events by checking only that the event's start timestamp falls within the requested window. Events that begin before the window boundary but end within or after it — including multi-day events and all-day events — are silently excluded, causing the bridge to return an incomplete calendar snapshot.

Environment:
- OS/version: Windows (net10.0-windows target; Outlook COM required at runtime)
- Runtime: .NET 10
- Component: `OutlookScanner.BuildCalendarFilter` ([OutlookScanner.cs:338-339](src/OpenClaw.MailBridge/OutlookScanner.cs#L338-L339)) and `CacheRepository.ListCalendarWindowAsync` ([CacheRepository.cs:282-283](src/OpenClaw.MailBridge/CacheRepository.cs#L282-L283))
- Data source: Outlook calendar folder (COM) and SQLite events cache

Impact / Severity:
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


## Repro & Evidence
Steps to Reproduce:
1. Create or ensure a calendar event that spans multiple days — for example, an event starting at 08:00 on day N−1 and ending at 17:00 on day N.
2. Issue a `list_calendar_window` request with `start = day N 00:00 UTC` and `end = day N+7 23:59 UTC`.
3. Observe the response.

Expected:
The multi-day event is included in the response because its end time (`day N 17:00`) falls within the requested window, satisfying the correct overlap condition: `event_start <= window_end AND event_end >= window_start`.

Actual:
The event is excluded. The current Outlook DASL filter `[Start] >= '{start}' AND [Start] < '{end}'` only admits events whose start falls inside the window. Because the event's start is on day N−1, it does not satisfy `[Start] >= day N 00:00` and is never loaded into the cache during the scan. The SQLite query (`start_utc >= $start_utc AND start_utc < $end_utc`) applies identical start-only logic and would likewise exclude the event even if it were in the cache.

No error is raised; the missing event is silently omitted from results.

Logs / Screenshots:
- [ ] Attached minimal logs or screenshot
- Snippet: No log output distinguishes a filtered-out event from one that never existed; the defect is observable only by comparing bridge output against a known appointment set.


## Scope & Non-Goals
- In scope:
- Out of scope / non-goals:
- Explicitly excluded systems, integrations, or datasets:

## Root Cause Analysis
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


## Proposed Fix

### Design summary (what changes where):

### Boundaries and invariants to preserve:

### Dependencies or blocked work:

### Implementation strategy (what changes, not sequencing):
	
#### Files/modules to change:

#### Functions/classes/CLI commands impacted:

#### Data flow and validation changes:

#### Error handling and logging updates:

#### Rollback/feature-flag considerations (if applicable):

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:

#### Required configuration keys and defaults:

#### Backward-compatibility expectations:

#### Performance constraints (latency/throughput/memory):

## Assumptions, Constraints, Dependencies
- Assumptions (environment, data, access):
- Constraints (budget, performance, compatibility):
- External dependencies (services, libraries, releases):

## Data / API / Config Impact
- User-facing or API changes:
- Data or migration considerations:
- Logging/telemetry updates (if any):
- Compatibility notes (CLI flags, config schemas, versioning):

## Test Strategy
Seeded from issue:

- [x] Unit test: `BuildCalendarFilter` with a window of `(2026-04-10T00:00Z, 2026-04-17T00:00Z)` produces a filter string of the form `[Start] <= '...' AND [End] >= '...'` with the correctly formatted local-time strings for both sides.
- [x] Unit test: `BuildCalendarFilter` result does not contain `[Start] <` or `[Start] >=` predicates.
- [x] Unit test (CacheRepository): inserting an event with `start_utc = 2026-04-09` and `end_utc = 2026-04-11` and querying with `start = 2026-04-10`, `end = 2026-04-17` returns the event (currently returns empty).
- [x] Unit test (CacheRepository): an event with `start_utc = 2026-04-16` and `end_utc = 2026-04-18` is included in the same query window (event starts inside, ends outside).
- [x] Unit test (CacheRepository): an event with `start_utc = 2026-04-09` and `end_utc = 2026-04-18` (spans the entire window) is included.
- [x] Unit test (CacheRepository): an event with `start_utc = 2026-04-17T01:00Z` (starts after window end) is excluded.
- [x] Unit test (CacheRepository): an event with `end_utc = 2026-04-09T23:59Z` (ends before window start) is excluded.
- [ ] Integration scenario: run `list_calendar_window` against a bridge connected to an Outlook calendar containing a multi-day event that starts one day before the query window; verify the event appears in the response.
- [ ] Manual verification: confirm that Outlook `Restrict` with `[Start] <= '{end}' AND [End] >= '{start}'` is a valid DASL filter syntax for the Outlook Items collection (cannot be verified without a live Outlook instance).

- Regression tests to add or update:
- Unit tests (pytest) for the fixed behavior and boundaries:
- Edge cases and negative scenarios (invalid inputs, missing data, boundary values):
- Error handling and logging verification:
- Coverage impact and targets for changed lines/modules:
- Toolchain commands to run (format → lint → type-check → test):
- Manual validation steps (if required):


## Acceptance Criteria
- [ ] Repro steps now produce the expected behavior in all documented environments.
- [ ] Regression test(s) added and passing (list file path and test name).
- [ ] Edge cases and invalid inputs are handled with correct errors or fallbacks.
- [ ] No unintended behavior changes outside the defined scope.
- [ ] Required logs/telemetry updated and validated (if applicable).
- [ ] Performance constraints met or explicitly waived with rationale.
- [ ] Full toolchain pass completed (format → lint → type-check → test).
- [ ] Docs/config references updated to match the new behavior.

## Risks & Mitigations
- Technical or operational risks:
- Mitigations and rollbacks:

## Rollout & Follow-up
- Release/rollout steps:
- Post-fix monitoring or clean-up tasks:
- Links: issue, PRs, related docs
