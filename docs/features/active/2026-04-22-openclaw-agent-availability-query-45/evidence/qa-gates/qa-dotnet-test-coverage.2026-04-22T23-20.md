# Phase 4 — Solution-wide Post-change Coverage

Timestamp: 2026-04-22T23-20
Command: dotnet test OpenClaw.MailBridge.sln --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/post-change
EXIT_CODE: 0

## Output Summary

Test totals: Passed 280 / Failed 0 / Skipped 3 / Total 283.

- `OpenClaw.HostAdapter.Tests`: Passed 71 / Failed 0 / Skipped 0 / Total 71
- `OpenClaw.Core.Tests`: Passed 51 / Failed 0 / Skipped 0 / Total 51
- `OpenClaw.MailBridge.Tests`: Passed 158 / Failed 0 / Skipped 3 / Total 161

MailBridge project delta vs. baseline: +6 passing tests (152 → 158), matching the 5 plan-enumerated new tests plus the 1 added ALTER-branch idempotency test that was needed to bring `MigrateEventsSchemaAsync` coverage to 100%.

### Repository-wide post-change coverage (best-per-package union)

| Production Package | Covered | Valid | Line Coverage |
|---|---|---|---|
| OpenClaw.Core | 1097 | 1193 | 91.95% |
| OpenClaw.HostAdapter | 917 | 1073 | 85.46% |
| OpenClaw.HostAdapter.Contracts | 9 | 9 | 100.00% |
| OpenClaw.MailBridge | 1086 | 1234 | 88.01% |
| OpenClaw.MailBridge.Client | 145 | 152 | 95.39% |
| OpenClaw.MailBridge.Contracts | 189 | 193 | 97.93% |

**Repo line coverage: 3443 / 3854 = 89.34%** (>= 80% required ✓).

### Cobertura Artifacts

- `artifacts/coverage/post-change/c6af0f89-c687-4cf4-bfec-9106933c95f9/coverage.cobertura.xml`
- `artifacts/coverage/post-change/f59e16d3-b78a-479e-9b4a-8cd52c8be296/coverage.cobertura.xml`
- `artifacts/coverage/post-change/e1807a0b-894d-4217-ba42-66314fe041ac/coverage.cobertura.xml`
