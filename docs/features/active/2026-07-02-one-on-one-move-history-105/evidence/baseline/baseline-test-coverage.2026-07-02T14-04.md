# Baseline — Tests and Coverage (dotnet test, XPlat Code Coverage)

Timestamp: 2026-07-02T14-04
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-105` (repo root)
EXIT_CODE: 0
Output Summary:
- Tests: 731 passed, 0 failed, 5 skipped (environment-gated), 736 total.
  - OpenClaw.HostAdapter.Tests: 100 passed / 100 total
  - OpenClaw.Core.Tests: 284 passed / 284 total
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped / 352 total
- Coverage (Cobertura reports under `artifacts/csharp/baseline-105/`):
  - Pooled across three reports: line coverage 90.81% (4298/4733), branch coverage 80.62% (990/1228).
  - Per-report roots: HostAdapter run line 87.70% / branch 67.19%; Core run line 90.47% / branch 80.27%; MailBridge run line 93.58% / branch 88.16%.
  - Package `OpenClaw.Core` (feature-target package, Core run): line 98.74%, branch 91.79%.
- Baseline thresholds context: pooled line 90.81% >= 85% and pooled branch 80.62% >= 75%.

Raw artifacts: `artifacts/csharp/baseline-105/5d98fc69-17b9-4716-ab5a-2aa4324da81c/coverage.cobertura.xml` (Core.Tests), `artifacts/csharp/baseline-105/2a1d3686-eef2-49da-bc2e-1fb155dfc8d7/coverage.cobertura.xml` (HostAdapter.Tests), `artifacts/csharp/baseline-105/cc8ea1a9-8465-49be-bda4-f8ec5dd26744/coverage.cobertura.xml` (MailBridge.Tests).
