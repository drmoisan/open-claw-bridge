# Regression Evidence — Full Suite After Fix ([P2-T3])

Timestamp: 2026-07-02T08-00
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings
EXIT_CODE: 0
Output Summary:
- OpenClaw.HostAdapter.Tests: Passed! Failed: 0, Passed: 100, Total: 100.
- OpenClaw.Core.Tests: Passed! Failed: 0, Passed: 213, Total: 213.
- OpenClaw.MailBridge.Tests: Passed! Failed: 0, Passed: 283, Skipped: 5, Total: 288 (baseline 277 passed + the 6 new regression tests).
- Solution totals: 596 passed, 0 failed, 5 skipped. All pre-existing tests, including `OutlookScannerCalendarUtcTests`, pass unchanged; no existing test file was modified (git diff confirms tests/ changes limited to the new `OutlookScannerCalendarOverlapFilterTests.cs`).
