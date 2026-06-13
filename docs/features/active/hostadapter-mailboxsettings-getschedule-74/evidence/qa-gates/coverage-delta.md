# Final QA — Coverage Delta and No-Regression Verification

Timestamp: 2026-06-13T10-30
Baseline source: evidence/baseline/test-coverage.md (P0-T6)
Post-change source: evidence/qa-gates/final-test-coverage.md (P9-T5)

## Whole-project coverage delta

| Project | Baseline line | Final line | Δ line | Baseline branch | Final branch | Δ branch |
|---|---|---|---|---|---|---|
| OpenClaw.Core | 89.13% | 89.17% | +0.04 | 77.59% | 77.59% | 0.00 |
| OpenClaw.HostAdapter | 84.10% | 86.81% | +2.71 | 60.28% | 65.95% | +5.67 |

Both projects' line coverage is >= 85%. Core branch coverage is >= 75%. HostAdapter whole-project
branch (65.95%) is below the 75% gate at the whole-project level, but the gate applies to changed
code (line >= 85%, branch >= 75%) with no regression on changed lines; the whole-project
HostAdapter branch IMPROVED by +5.67 points from baseline (no regression), and the sub-75%
remainder is pre-existing surface outside this feature's changed scope.

## Changed/new-code coverage (the gate-bearing surface)

Every file changed or added by this feature is at 100% line and 100% branch coverage:

| File | Line | Branch |
|---|---|---|
| SchedulingContracts.cs | 100.00% | 100.00% |
| MailboxSettingsOptions.cs | 100.00% | 100.00% |
| FreeBusyProjection.cs | 100.00% | 100.00% |
| SchedulingRoutes.cs | 100.00% | 100.00% |
| HostAdapterHttpClient.cs | 100.00% | 100.00% |
| HostAdapterSchedulingService.cs | 100.00% | 100.00% |
| IHostAdapterClient.cs | interface (no executable lines) | n/a |

## Verdict

PASS. Changed-code line coverage = 100% (>= 85%) and branch coverage = 100% (>= 75%). No
regression on changed lines (the changed files moved from stub/absent to fully covered).
Whole-project line coverage remains >= 85% for both in-scope projects; both projects' branch
coverage improved or held steady relative to baseline.
