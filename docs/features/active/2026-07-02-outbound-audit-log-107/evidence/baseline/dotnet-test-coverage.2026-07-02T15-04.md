# Baseline — dotnet test with coverage

Timestamp: 2026-07-02T15-04
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-107`
EXIT_CODE: 0
Output Summary:
- Tests: 774 passed, 0 failed, 5 skipped (integration/COM tests), 779 total.
  - OpenClaw.Core.Tests: 327 passed / 327
  - OpenClaw.HostAdapter.Tests: 100 passed / 100
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped / 352
- Coverage (Cobertura, per-package best across the three test-run reports; each package measured by the test project that exercises it):
  - OpenClaw.Core: line 3246/3286 = 98.78%, branch 822/894 = 91.95%
  - Pooled (all packages): line 8392/8670 = 96.79%, branch 1996/2220 = 89.91%
  - Per package: MailBridge 93.10%/86.36%; HostAdapter 98.64%/89.47%; MailBridge.Client 90.48%/93.10%; MailBridge.Contracts 98.14%/93.65%; HostAdapter.Contracts 100%/n-a (no branches)
- Raw intermediates: `artifacts/csharp/baseline-107/*/coverage.cobertura.xml` (3 files).
- Both uniform thresholds satisfied at baseline (line >= 85%, branch >= 75%).
