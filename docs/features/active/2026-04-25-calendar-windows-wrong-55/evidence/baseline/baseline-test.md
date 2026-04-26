---
Timestamp: 2026-04-25T00-00
Command: dotnet test OpenClaw.MailBridge.sln --no-build -c Debug --collect:"Code Coverage"
EXIT_CODE: 0
Output Summary: All tests passed. Failed: 0, Passed: 280 (71+51+158), Skipped: 3, Total: 283. Line coverage: 94.1% (9619/10222 lines covered).
---

# Baseline Test Run

## Test Results

| Assembly | Passed | Skipped | Total |
|---|---|---|---|
| OpenClaw.HostAdapter.Tests | 71 | 0 | 71 |
| OpenClaw.Core.Tests | 51 | 0 | 51 |
| OpenClaw.MailBridge.Tests | 158 | 3 | 161 |
| **Total** | **280** | **3** | **283** |

All tests passed. 3 tests skipped (platform-specific: non-Windows COM and publish output tests).

## Coverage (Cobertura via dotnet-coverage)

- Line coverage: 94.1%
- Lines covered: 9619 / 10222
- Branch coverage: 75.7%

Repository-wide line coverage is above the 80% policy threshold.
