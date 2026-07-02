# Baseline — Test and Coverage

Timestamp: 2026-07-02T18-51
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-113
EXIT_CODE: 0
Output Summary:
- All test projects passed. OpenClaw.Core.Tests: 377 passed / 0 failed / 0 skipped (total 377). OpenClaw.HostAdapter.Tests: 100 passed / 0 failed / 0 skipped (total 100). OpenClaw.MailBridge.Tests: 347 passed / 0 failed / 5 skipped (total 352; skips are pre-existing COM/publish environment guards).
- Baseline coverage, Core.Tests run (Cobertura root): line 91.00% (1761/1935), branch 80.96% (421/520). OpenClaw.Core package: line 98.82%, branch 92.12%.
- Baseline pooled across all three runs: line 91.02% (4407/4842), branch 80.90% (1008/1246).
- Thresholds satisfied at baseline (line >= 85%, branch >= 75%).

Raw Cobertura files (under artifacts/csharp/):
- artifacts/csharp/baseline-113/506b1420-beba-4450-8d5c-e8cc577e0476/coverage.cobertura.xml (OpenClaw.Core.Tests run — authoritative for OpenClaw.Core)
- artifacts/csharp/baseline-113/a5d9fb03-e7df-46e9-a33d-58e6e8ff1441/coverage.cobertura.xml (OpenClaw.HostAdapter.Tests run)
- artifacts/csharp/baseline-113/bb5ab1c9-1dab-4d18-929f-416f93c196d6/coverage.cobertura.xml (OpenClaw.MailBridge.Tests run)
