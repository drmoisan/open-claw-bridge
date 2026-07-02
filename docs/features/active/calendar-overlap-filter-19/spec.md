# calendar-overlap-filter (Spec)

- **Issue:** #19
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T08-01
- **Status:** Draft
- **Version:** 0.2

## Context
- Summary of the bug and its impact (link to repro/playbook entry): `OutlookScanner.BuildCalendarFilter` (`src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs`, lines 48-49) restricts the calendar scan with a start-time-only predicate (`[Start] >= windowStart AND [Start] < windowEnd`). Events that begin before the query window but are still in progress during it (end inside the window, or spanning the whole window) are excluded from the scan. The cached calendar window therefore misses in-progress events, and `FreeBusyProjection` (`src/OpenClaw.HostAdapter/FreeBusyProjection.cs`) plus the deterministic scheduler can treat occupied time as free. Evidence: `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (gap table entry for issue #19), confirmed by feature review 2026-07-02. See `issue.md` in this folder.
- Observed environment(s): Any environment running the MailBridge calendar scan (`OutlookScanner.ScanCalendarAsync`); the defect is in pure filter-string construction and is environment-independent.
- Customer impact and severity (who is affected, how often, how bad): High. Any free/busy query over a window that overlaps an in-progress or window-spanning event returns incorrect availability, so the scheduling agent can propose times that conflict with existing meetings. Violates the master-document requirement that availability derives from actual calendar contents (`docs/open-claw-approach.master.md` section 10.4, Deterministic Availability Algorithm).
- First observed date and version(s) impacted: Identified 2026-07-01 (gap-analysis research); confirmed 2026-07-02. Present since `BuildCalendarFilter` was introduced; all current versions impacted.

## Repro & Evidence
- Steps to reproduce (with data/flags/inputs):
  1. Create a calendar event whose `Start` precedes the scan window start (`utcNow - CalendarPastDays`) and whose `End` falls inside the window (or spans it entirely).
  2. Run `OutlookScanner.ScanCalendarAsync` (window: `utcNow - CalendarPastDays` to `utcNow + CalendarFutureDays`, `src/OpenClaw.MailBridge/OutlookScanner.cs`, lines 281-284).
  3. Inspect the cached events / the Restrict filter string: the event is excluded because its `Start` is before the window start.
  4. Query free/busy over an interval covered by that event: the interval is reported free.
- Expected vs actual behavior: Expected — every event overlapping `[windowStart, windowEnd)` is included in the scan. Actual — only events whose `Start` lies inside the window are included.
- Logs/screenshots/error snippets: No error is raised; this is a silent exclusion. The defective predicate is directly visible in the filter string: `[Start] >= '<start>' AND [Start] < '<end>'`.
- Frequency / determinism (always, intermittent, data-dependent): Deterministic; occurs for every event whose start precedes the window start while it overlaps the window.

## Scope & Non-Goals
- In scope: The window-membership predicate produced by `BuildCalendarFilter` (and, only if required for recurring-occurrence edge cases, a documented post-filter pass over materialized occurrences); one regression test in `tests/OpenClaw.MailBridge.Tests/`.
- Out of scope / non-goals: Timezone/UTC normalization (fixed by issue #55 and unchanged here); the inbox filter (`BuildInboxFilter`); scanner scheduling, cache schema, `FreeBusyProjection` logic, and HostAdapter routes; any opportunistic refactor of `OutlookScanner`.
- Explicitly excluded systems, integrations, or datasets: Microsoft Graph paths, HostAdapter endpoints, live Outlook COM in unit tests.

## Root Cause Analysis
- Current hypothesis or confirmed root cause: Confirmed. The Restrict filter tests only `[Start]` membership in the window instead of interval overlap. Correct membership: an event overlaps the window when `Start < windowEnd AND End > windowStart`.
- Signals/evidence supporting it: Direct code reading of `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs` lines 48-49; gap-analysis research `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`; feature-review confirmation 2026-07-02.
- Affected components/modules (paths, services, pipelines): `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs` (`BuildCalendarFilter`); consumer `src/OpenClaw.MailBridge/OutlookScanner.cs` (`ScanCalendarAsync`, Restrict call with `IncludeRecurrences = true`); downstream consumers of the cached window: `src/OpenClaw.HostAdapter/FreeBusyProjection.cs`, `src/OpenClaw.HostAdapter/SchedulingRoutes.cs`.

## Proposed Fix

### Design summary (what changes where):
Change `BuildCalendarFilter` to emit the interval-overlap predicate:

```
[Start] < '<windowEnd>' AND [End] > '<windowStart>'
```

preserving the existing date formatting exactly: each boundary is rendered as `'{value.LocalDateTime:MM/dd/yyyy hh:mm tt}'` (UTC window edge converted to local time, minute precision, US-style format expected by Outlook Restrict). The strict `<` / `>` operators give the required boundary semantics directly: an event with `End == windowStart` fails `End > windowStart` (excluded), and an event with `Start == windowEnd` fails `Start < windowEnd` (excluded). Events starting inside the window, ending inside the window, or spanning it all satisfy the predicate.

This is the minimal Restrict-string change and is the documented Outlook pattern for time-range queries used with `Items.Sort("[Start]")` plus `IncludeRecurrences = true`, both already set by `ScanCalendarAsync` before `Restrict` is invoked. If verification shows recurring-occurrence edge cases the Restrict string cannot express, a small post-filter pass over materialized occurrences applying the same overlap predicate is acceptable; the planner decides, but the Restrict-string-only change is preferred.

### Boundaries and invariants to preserve:
- COM interop stays confined to `OpenClaw.MailBridge` (architecture-boundaries rule).
- Date formatting and timezone handling are unchanged: `LocalDateTime` conversion and `MM/dd/yyyy hh:mm tt` format are preserved; the #55 `GetOptionalUtcDateTimeOffset` normalization path is untouched.
- `Sort("[Start]")` and `IncludeRecurrences = true` ordering relative to `Restrict` in `ScanCalendarAsync` is unchanged.
- No production or test file exceeds 500 lines.

### Dependencies or blocked work:
None. The gap-analysis research lists this fix as independent of other epics.

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:
- `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs` — the `BuildCalendarFilter` expression body (the only production change expected).
- `tests/OpenClaw.MailBridge.Tests/` — one new regression test file (suggested: `OutlookScannerCalendarOverlapFilterTests.cs`).

#### Functions/classes/CLI commands impacted:
- `OutlookScanner.BuildCalendarFilter(DateTimeOffset startUtc, DateTimeOffset endUtc)`. If the regression test asserts the builder directly, its visibility may be raised from `private` to `internal` (the test assembly already has `InternalsVisibleTo` via `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`); alternatively the test asserts the filter string captured by the existing fakes through `ScanCalendarAsync`. Planner decides; no public API surface changes either way.

#### Data flow and validation changes:
The Restrict filter string sent to Outlook changes from start-membership to interval-overlap. The cached window gains previously-excluded in-progress and window-spanning events. No schema, DTO, or cache-shape change.

#### Error handling and logging updates:
None. Existing null-check on the restricted items collection in `ScanCalendarAsync` is sufficient and unchanged.

#### Rollback/feature-flag considerations (if applicable):
None needed. Single-expression change; rollback is a revert.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:
- Input: `startUtc`, `endUtc` (`DateTimeOffset`, UTC window edges computed in `ScanCalendarAsync`).
- Output: Outlook Restrict filter string `[Start] < '<endLocal>' AND [End] > '<startLocal>'` where each boundary uses the existing `MM/dd/yyyy hh:mm tt` local-time format.

#### Required configuration keys and defaults:
None new. Window size continues to come from `BridgeSettings.CalendarPastDays` / `CalendarFutureDays`.

#### Backward-compatibility expectations:
No public API change. Cached-window contents become a strict superset of the previous behavior (all previously included events remain included).

#### Performance constraints (latency/throughput/memory):
No measurable change expected: same single `Restrict` call; the result set grows only by the overlapping events that were incorrectly excluded.

## Assumptions, Constraints, Dependencies
- Assumptions (environment, data, access): Outlook Restrict supports `[End]` comparisons combined with `IncludeRecurrences` for appointment items (documented Outlook time-range pattern); the existing fakes (`FakeOutlookItems.Restrict` capturing `LastFilter` in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`) are sufficient to assert the emitted filter without live COM.
- Constraints (budget, performance, compatibility): Minimal targeted fix per bugfix workflow; MSTest + FluentAssertions; no temporary files in tests; 500-line file cap; COM interop confined to `OpenClaw.MailBridge`.
- External dependencies (services, libraries, releases): None new.

## Data / API / Config Impact
- User-facing or API changes: None. Free/busy results become correct for windows overlapping in-progress events.
- Data or migration considerations: None; the cache repopulates on the next scan.
- Logging/telemetry updates (if any): None.
- Compatibility notes (CLI flags, config schemas, versioning): None.

## Test Strategy
- Regression tests to add or update: New MSTest class in `tests/OpenClaw.MailBridge.Tests/` (suggested: `OutlookScannerCalendarOverlapFilterTests.cs`) that fails against the current start-only predicate and passes with the overlap predicate. It must exercise the pure filter-string builder and/or the filter string captured by `FakeOutlookItems.LastFilter` after `ScanCalendarAsync` with `FakeComActiveObject` and a fixed `_utcNow` (pattern: `OutlookScannerCalendarUtcTests.cs`). No live Outlook COM.
- Unit tests (MSTest + FluentAssertions) for the fixed behavior and boundaries: Assert the emitted filter contains `[Start] <` against the window end and `[End] >` against the window start, with boundaries formatted as `MM/dd/yyyy hh:mm tt` local time. If a post-filter pass is added, unit-test its predicate directly for all five interval cases below.
- Edge cases and negative scenarios (invalid inputs, missing data, boundary values): (a) event starting inside the window — included; (b) event starting before the window, ending inside it — included; (c) event spanning the entire window — included; (d) event with `End == windowStart` — excluded; (e) event with `Start == windowEnd` — excluded.
- Error handling and logging verification: Existing `ScanCalendarAsync` tests covering the unavailable-items error path continue to pass unchanged.
- Coverage impact and targets for changed lines/modules: Line coverage >= 85% and branch coverage >= 75% (uniform, `.claude/rules/quality-tiers.md`); all changed lines covered; baseline, post-change, and comparison artifacts recorded under `docs/features/active/calendar-overlap-filter-19/evidence/coverage/`.
- Toolchain commands to run (format → lint → type-check → test): `dotnet tool restore && dotnet csharpier check .` → `dotnet build` (analyzers + nullable, warnings as errors) → architecture tests → `dotnet test --collect:"XPlat Code Coverage"`. Restart the loop from formatting if any stage fails or changes files.
- Manual validation steps (if required): Optional — on a machine with Outlook, run a bridge calendar scan while a meeting is in progress that started before the window start and confirm the event appears in the cache. Not required for merge; unit evidence is authoritative.

## Acceptance Criteria
- [x] The calendar filter/selection includes: (a) events starting within the window, (b) events starting before the window but ending inside it, and (c) events spanning the entire window.
- [x] The calendar filter/selection excludes events ending at-or-before windowStart and events starting at-or-after windowEnd (boundary semantics explicit: `End == windowStart` excluded, `Start == windowEnd` excluded).
- [x] A regression test in `tests/OpenClaw.MailBridge.Tests/` fails before the fix and passes after it, exercising the pure filter-string builder and/or a post-filter predicate, with no live Outlook COM dependency (file path and test names recorded here on completion). Recorded: file `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs`; tests `ScanCalendarAsync_emits_interval_overlap_restrict_filter` and `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (5 DataRow boundary cases).
- [x] Date formatting (`MM/dd/yyyy hh:mm tt`, `LocalDateTime` conversion) and the timezone handling established by the #55 fix are unchanged; all existing tests pass unchanged.
- [x] No behavior change outside `BuildCalendarFilter` (and, only if required, a documented post-filter pass in the calendar scan path).
- [x] Full C# toolchain passes in a single clean pass: CSharpier check → `dotnet build` (analyzers, nullable, warnings-as-errors) → architecture tests → `dotnet test` with coverage.
- [x] Line coverage >= 85% and branch coverage >= 75% hold, with all changed lines covered; coverage baseline/post/comparison evidence stored under `docs/features/active/calendar-overlap-filter-19/evidence/coverage/`.

## Risks & Mitigations
- Technical or operational risks: (1) Outlook Restrict behavior for `[End]` combined with `IncludeRecurrences` may differ for recurring occurrences versus master items; mitigation — if the Restrict string proves insufficient, add the documented post-filter pass over materialized occurrences applying the same overlap predicate. (2) A larger result set from the corrected predicate; mitigation — the growth is bounded to genuinely overlapping events, and the scan already iterates the restricted collection.
- Mitigations and rollbacks: Single-expression change; revert restores prior behavior. Regression test pins the corrected predicate against reintroduction.

## Rollout & Follow-up
- Release/rollout steps: Standard PR to `main` (merge commit) after feature review; no deployment steps beyond the normal bridge release.
- Post-fix monitoring or clean-up tasks: None required; optionally confirm during the next live bridge session that in-progress events appear in free/busy output.
- Links: issue #19 (`issue.md` in this folder); plan `plan.2026-07-02T07-41.md`; research `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`; related archived fix `docs/features/archive/2026-04-25-calendar-windows-wrong-55/`; master document `docs/open-claw-approach.master.md` section 10.4.
