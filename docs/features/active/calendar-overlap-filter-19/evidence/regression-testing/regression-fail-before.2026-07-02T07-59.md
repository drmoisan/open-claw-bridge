# Regression Evidence — Fail Before Fix ([P1-T3], expect-fail)

Timestamp: 2026-07-02T07-59
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerCalendarOverlapFilterTests"
EXIT_CODE: 1
Output Summary:
- Run against the unfixed predicate (`[Start] >= '<start>' AND [Start] < '<end>'`) at baseline commit 1bc4148867bd757b724af503b59a3a19bc6f37b4.
- Result: Failed: 3, Passed: 3, Total: 6 (OpenClaw.MailBridge.Tests).
- Failing tests (as predicted by the plan):
  1. `ScanCalendarAsync_emits_interval_overlap_restrict_filter` — emitted filter is the start-only predicate, not the interval-overlap string.
  2. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` DataRow (-1, 0.5, True, "an in-progress event starting before the window and ending inside it must be included") — old predicate excludes in-progress events.
  3. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` DataRow (-1, 38, True, "an event spanning the entire window must be included") — old predicate excludes window-spanning events.
- Passing rows under the old predicate (expected): fully-within row and both strict-boundary exclusion rows (End == windowStart; Start == windowEnd).
