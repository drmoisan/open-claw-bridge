# Phase 0 — Baseline Test + Coverage State

Timestamp: 2026-06-16T07-00
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
All tests pass. Totals:
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 89, Skipped 0 (Total 89)
- OpenClaw.Core.Tests: Failed 0, Passed 206, Skipped 0 (Total 206)
- OpenClaw.MailBridge.Tests: Failed 0, Passed 263, Skipped 3 (Total 266)
- Combined: 558 passed, 3 skipped (skipped = non-Windows COM test + 2 publish-output tests).

## Baseline Coverage Headline (per-project cobertura line-rate / branch-rate)

| Project | line-rate | branch-rate | lines-covered/valid | branches-covered/valid |
|---|---|---|---|---|
| OpenClaw.Core.Tests | 89.57% | 78.44% | 1486/1659 | 342/436 |
| OpenClaw.HostAdapter.Tests | 86.86% | 65.95% | 1025/1180 | 155/235 |
| OpenClaw.MailBridge.Tests | 93.87% | 87.03% | 1287/1371 | 349/401 |

Combined (sum across projects):
- Line coverage: (1486+1025+1287)/(1659+1180+1371) = 3798/4210 = 90.21%
- Branch coverage: (342+155+349)/(436+235+401) = 846/1072 = 78.92%

Note: The HostAdapter.Tests per-project branch-rate (65.95%) reflects the raw cobertura over the full HostAdapter assembly closure (including ASP.NET Program/host plumbing). The uniform branch gate (>= 75%) is evaluated on application code and on the combined surface (78.92%). This baseline value is recorded for the no-regression-on-changed-lines comparison in P11-T6; the feature must not regress changed lines and must keep combined thresholds (line >= 85%, branch >= 75%).
