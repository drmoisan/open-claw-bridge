---
Timestamp: 2026-04-25T00-00
Command: dotnet test OpenClaw.MailBridge.sln --no-build -c Debug --collect:"Code Coverage"
EXIT_CODE: 0
Output Summary: All tests passed. Failed: 0, Passed: 287 (71+51+165), Skipped: 3, Total: 290. Post-change line coverage: 94.2% (9730/10334 lines covered). OutlookComHelpers class: 90.0% line coverage.
---

# QA Test Gate

## Test Results

| Assembly | Passed | Skipped | Total |
|---|---|---|---|
| OpenClaw.HostAdapter.Tests | 71 | 0 | 71 |
| OpenClaw.Core.Tests | 51 | 0 | 51 |
| OpenClaw.MailBridge.Tests | 165 | 3 | 168 |
| **Total** | **287** | **3** | **290** |

New tests added:
- OutlookComHelpersDateTimeKindTests: 6 tests (Unspecified/Utc/Local/DateTimeOffset/MissingMember/StringValue)
- OutlookScannerCalendarUtcTests: 1 test (ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc)

## Coverage (Cobertura via dotnet-coverage)

- Repo-wide line coverage: 94.2% (9730 / 10334 lines covered)
- Repo-wide branch coverage: 76.2%
- OutlookComHelpers class: 90.0% line coverage

Test gate: PASS.
