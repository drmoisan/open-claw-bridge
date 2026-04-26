# Baseline — dotnet test (solution) with coverage

Timestamp: 2026-04-22T23-20
Command: dotnet test OpenClaw.MailBridge.sln --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/baseline
EXIT_CODE: 0

## Output Summary

Test results by project:

- `OpenClaw.HostAdapter.Tests`: Passed 71 / Failed 0 / Skipped 0 / Total 71 (Duration 475 ms)
- `OpenClaw.Core.Tests`:        Passed 51 / Failed 0 / Skipped 0 / Total 51 (Duration 750 ms)
- `OpenClaw.MailBridge.Tests`:  Passed 152 / Failed 0 / Skipped 3 / Total 155 (Duration 12 s)

Aggregate: Passed 274 / Failed 0 / Skipped 3 / Total 277.

Skipped tests (not regressions): `Com_active_object_create_and_logon_should_throw_on_non_windows`, `PublishOutput_BridgeDirectory_ContainsBridgeExecutable`, `PublishOutput_ClientDirectory_ContainsClientExecutable`.

### Coverage — per-project Cobertura top-line values

- `OpenClaw.MailBridge.Tests` run (report `b4889ad9…`): line-rate 0.8911, lines-covered 1383 / lines-valid 1552 (89.11%)
- `OpenClaw.HostAdapter.Tests` run (report `c872bb18…`): line-rate 0.7731, lines-covered 985 / lines-valid 1274 (77.31%)
- `OpenClaw.Core.Tests` run (report `df94d69b…`): line-rate 0.8256, lines-covered 1151 / lines-valid 1394 (82.56%)

### Repository-wide coverage (best-per-production-package union)

For each production package, the best (highest-covered) report is used so overlapping coverage of `OpenClaw.MailBridge.Contracts` is not double-counted:

| Production Package | Covered | Valid | Line Coverage |
|---|---|---|---|
| OpenClaw.Core | 1097 | 1193 | 91.95% |
| OpenClaw.HostAdapter | 917 | 1073 | 85.46% |
| OpenClaw.HostAdapter.Contracts | 9 | 9 | 100.00% |
| OpenClaw.MailBridge | 1050 | 1208 | 86.92% |
| OpenClaw.MailBridge.Client | 145 | 152 | 95.39% |
| OpenClaw.MailBridge.Contracts | 188 | 192 | 97.92% |

**Baseline repo-wide line coverage: 3406 / 3827 = 89.00%** (>= 80% required; headroom 9.00 pts).

### Cobertura Artifacts

- `artifacts/coverage/baseline/b4889ad9-4b45-494d-8e08-48e7e3bf6d8f/coverage.cobertura.xml`
- `artifacts/coverage/baseline/c872bb18-c69a-43be-8357-54cfc07179b0/coverage.cobertura.xml`
- `artifacts/coverage/baseline/df94d69b-bfe1-4cbe-bbd0-e3d74bbeedbf/coverage.cobertura.xml`
