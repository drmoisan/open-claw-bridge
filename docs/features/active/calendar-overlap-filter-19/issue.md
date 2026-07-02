# Bug: fix-calendar-overlap-filter (Issue #19)

- Issue: #19
- Type: bug
- Work Mode: full-bug
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/19
- Date captured: 2026-07-02
- Author: drmoisan
- Status: Active — docs/features/active/calendar-overlap-filter-19/

## Summary

`OutlookScanner.BuildCalendarFilter` (`src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs`, lines 48-49) builds the Outlook `Items.Restrict` filter using a start-time-only predicate:

```csharp
private string BuildCalendarFilter(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
    $"[Start] >= '{startUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}' AND [Start] < '{endUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}'";
```

Only events whose `Start` falls inside the query window are selected. An event that begins before the window but is still in progress during it — its `End` falls inside `[windowStart, windowEnd)`, or it spans the entire window — is excluded from the calendar scan.

The defect was identified in orchestrator research (`docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, gap table entry "Calendar overlap-window correctness (issue #19)") and confirmed by feature review on 2026-07-02.

## Impact

The calendar cache populated by `OutlookScanner.ScanCalendarAsync` misses in-progress and window-spanning events. Downstream, `FreeBusyProjection` (`src/OpenClaw.HostAdapter/FreeBusyProjection.cs`) and the deterministic scheduler compute availability from that cache, so occupied time can be reported as free. This is a correctness defect against the master-document requirement that availability derives from actual calendar contents (`docs/open-claw-approach.master.md` section 10.4, Deterministic Availability Algorithm).

## Root Cause

The Restrict predicate tests only `[Start]`. The correct membership test for "event overlaps window" is the interval-overlap predicate: an event is in the window when `Start < windowEnd AND End > windowStart`.

## Relationship to Issue #55

Issue #55 (archived at `docs/features/archive/2026-04-25-calendar-windows-wrong-55/`) fixed a UTC double-shift in `OutlookComHelpers` when normalizing `StartUTC`/`EndUTC` values read from COM. This bug is distinct: it concerns the window-membership predicate in the Restrict filter string, not timezone conversion. The date formatting established by the current helper (`'{value.LocalDateTime:MM/dd/yyyy hh:mm tt}'`) and the #55 normalization behavior must be preserved.

## Expected Fix Shape (suggested; planner decides final)

Change the restriction to the overlap predicate `[Start] < windowEnd AND [End] > windowStart`, preserving the existing local-time date formatting. The filter is passed to Outlook `Items.Restrict` with `IncludeRecurrences = true` (`src/OpenClaw.MailBridge/OutlookScanner.cs`, around lines 278-284). If a pure Restrict-string change is insufficient for edge cases (for example, recurring occurrences), a documented post-filter pass over materialized occurrences is acceptable, but the minimal Restrict-string change is preferred.

## Acceptance Criteria

Authoritative acceptance criteria for this `full-bug` feature live in `spec.md` (per the acceptance-criteria tracking protocol). The criteria below mirror them for issue-level visibility.

- [x] The calendar filter/selection includes events starting within the window, events starting before the window but ending inside it, and events spanning the entire window.
- [x] The calendar filter/selection still excludes events ending at-or-before windowStart and events starting at-or-after windowEnd (boundary semantics: `End == windowStart` excluded, `Start == windowEnd` excluded).
- [x] A regression test in `tests/OpenClaw.MailBridge.Tests/` fails before the fix and passes after, without any live Outlook COM dependency. Recorded: file `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs`; tests `ScanCalendarAsync_emits_interval_overlap_restrict_filter` and `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (5 DataRow boundary cases).
- [x] Date formatting and timezone handling established by the #55 fix are unchanged; existing tests pass unchanged.
- [x] Full C# toolchain passes (CSharpier → analyzers/nullable build → architecture → tests); line coverage >= 85% and branch coverage >= 75% hold, with changed lines covered.

## Severity

High — availability computed from the cached calendar window can mark occupied time as free whenever an in-progress or window-spanning event exists, producing incorrect scheduling recommendations.
