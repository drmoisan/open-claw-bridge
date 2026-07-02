# Final QA — Tests and Coverage (dotnet test, XPlat Code Coverage)

Timestamp: 2026-07-02T14-20
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final-105` (repo root)
EXIT_CODE: 0
Output Summary:
- Tests: 774 passed, 0 failed, 5 skipped (environment-gated), 779 total.
  - OpenClaw.HostAdapter.Tests: 100 passed / 100 total
  - OpenClaw.Core.Tests: 327 passed / 327 total (284 baseline + 43 new: 12 SeriesMoves repository, 27 OneOnOneMoveGuard unit incl. DataRow expansions, 4 CsCheck properties)
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped / 352 total
- Includes the architecture-boundary suite (`AgentArchitectureBoundaryTests`), unit tests, and property tests, in coverage mode.
- Coverage (Cobertura reports under `artifacts/csharp/final-105/`):
  - Pooled across three reports: line coverage 90.92% (4353/4788), branch coverage 80.74% (998/1236).
  - Package `OpenClaw.Core` (feature-target package, Core run): line 98.78%, branch 91.94%.
- Thresholds: pooled line 90.92% >= 85% PASS; pooled branch 80.74% >= 75% PASS.

Raw artifacts: `artifacts/csharp/final-105/2b6c1b8d-819f-4ae5-8ab2-634538dc0ae2/coverage.cobertura.xml` (Core.Tests), `artifacts/csharp/final-105/1bbf5877-1c32-400e-ac09-665432662e83/coverage.cobertura.xml` (HostAdapter.Tests), `artifacts/csharp/final-105/8d7b7cfb-8d4e-4142-9640-923197373362/coverage.cobertura.xml` (MailBridge.Tests).
