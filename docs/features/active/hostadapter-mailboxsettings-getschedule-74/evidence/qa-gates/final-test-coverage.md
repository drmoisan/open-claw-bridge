# Final QA — Test + Coverage

Timestamp: 2026-06-13T10-30
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary: PASS. 498 passed, 0 failed, 3 skipped.
- OpenClaw.HostAdapter.Tests: 89 passed.
- OpenClaw.Core.Tests: 193 passed.
- OpenClaw.MailBridge.Tests: 216 passed, 3 skipped (non-Windows/publish-output guards).

## Post-change project coverage (cobertura)
- OpenClaw.Core.Tests: line-rate 0.8917 (89.17%), branch-rate 0.7759 (77.59%);
  lines-covered 1441/1616, branches-covered 329/424.
- OpenClaw.HostAdapter.Tests: line-rate 0.8681 (86.81%), branch-rate 0.6595 (65.95%);
  lines-covered 1021/1176, branches-covered 155/235.

## Changed/new-code coverage (per-file) — all 100%
| File | Line | Branch |
|---|---|---|
| src/OpenClaw.HostAdapter.Contracts/SchedulingContracts.cs (relocated DTOs) | 100.00% | 100.00% |
| src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs (2 new methods) | interface (no executable lines) | n/a |
| src/OpenClaw.HostAdapter/MailboxSettingsOptions.cs | 100.00% | 100.00% |
| src/OpenClaw.HostAdapter/FreeBusyProjection.cs | 100.00% | 100.00% |
| src/OpenClaw.HostAdapter/SchedulingRoutes.cs | 100.00% | 100.00% |
| src/OpenClaw.Core/HostAdapterHttpClient.cs (2 new methods) | 100.00% | 100.00% |
| src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs (2 delegations) | 100.00% | 100.00% |

All changed code meets line >= 85% and branch >= 75% (every changed file is at 100%/100%).
Whole-project totals also satisfy the uniform line >= 85% gate (Core 89.17%, HostAdapter
86.81%). The whole-project HostAdapter branch total (65.95%) reflects pre-existing uncovered
surface outside this feature's changed scope; it improved from the 60.28% baseline (no
regression). Coverage delta and no-regression verification: see coverage-delta.md (P9-T6).
