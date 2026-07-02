# Regression Evidence — Pass After Fix ([P2-T2])

Timestamp: 2026-07-02T08-00
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerCalendarOverlapFilterTests"
EXIT_CODE: 0
Output Summary:
- Result: Passed! Failed: 0, Passed: 6, Skipped: 0, Total: 6 (OpenClaw.MailBridge.Tests).
- Passing tests:
  1. `ScanCalendarAsync_emits_interval_overlap_restrict_filter`
  2. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (1, 1.5, True — fully within window)
  3. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (-1, 0.5, True — in-progress event)
  4. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (-1, 38, True — window-spanning event)
  5. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (-1, 0, False — End == windowStart excluded)
  6. `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (37, 38, False — Start == windowEnd excluded)
