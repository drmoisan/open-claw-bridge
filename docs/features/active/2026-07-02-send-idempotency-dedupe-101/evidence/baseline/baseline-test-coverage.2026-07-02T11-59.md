# Baseline — Tests and Coverage

Timestamp: 2026-07-02T11-59
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-101` (repo root)
EXIT_CODE: 0

Output Summary:
- Test results (all pass, 0 failed):
  - OpenClaw.HostAdapter.Tests: 100 passed / 0 failed / 0 skipped (total 100)
  - OpenClaw.Core.Tests: 224 passed / 0 failed / 0 skipped (total 224)
  - OpenClaw.MailBridge.Tests: 347 passed / 0 failed / 5 skipped (total 352; skips are Windows-COM/publish-output integration tests skipped by design)
  - Totals: 671 passed, 0 failed, 5 skipped (676 tests)
- Coverage (Cobertura reports under `artifacts/csharp/baseline-101/`):
  - Per-report: MailBridge.Tests run (3be2727f): line 93.58% (1533/1638), branch 88.16% (417/473); HostAdapter.Tests run (771270cc): line 87.70% (1113/1269), branch 67.19% (170/253); Core.Tests run (eb2dc395): line 89.77% (1528/1702), branch 78.73% (348/442)
  - Pooled (summed across the three reports): line coverage 90.56% (4174/4609), branch coverage 80.05% (935/1168)
  - `OpenClaw.Core` package (Core.Tests report): line 98.63%, branch 91.82%
- Baseline verdict: pooled line 90.56% >= 85% and pooled branch 80.05% >= 75% — baseline thresholds satisfied.

Raw artifacts: `artifacts/csharp/baseline-101/3be2727f-f531-452b-8c88-7bb9236edd2e/coverage.cobertura.xml`, `artifacts/csharp/baseline-101/771270cc-29b4-4391-971f-fcbe017fd385/coverage.cobertura.xml`, `artifacts/csharp/baseline-101/eb2dc395-bb4c-4b23-92d1-1928292c423a/coverage.cobertura.xml`
