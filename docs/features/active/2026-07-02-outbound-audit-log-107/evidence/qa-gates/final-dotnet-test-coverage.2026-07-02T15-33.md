# Final QA — dotnet test with coverage

Timestamp: 2026-07-02T15-33
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final-107`
EXIT_CODE: 0
Output Summary:
- Tests: 807 passed, 0 failed, 5 skipped (pre-existing COM/publish integration skips), 812 total.
  - OpenClaw.Core.Tests: 360 passed / 360 (baseline 327; +33 new: 22 repository audit tests, 1 CsCheck property, 8 worker audit tests, 2 correlation-forwarding seam tests)
  - OpenClaw.HostAdapter.Tests: 100 passed / 100
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped / 352
- Includes architecture-boundary tests (`AgentArchitectureBoundaryTests`), unit, property (CsCheck), and contract tests.
- Coverage (Cobertura, per-package best across the three test-run reports):
  - OpenClaw.Core: line 3338/3378 = 98.82%, branch 834/906 = 92.05%
  - Pooled (all packages): line 8484/8762 = 96.83%, branch 2008/2232 = 89.96%
  - Per package: MailBridge 93.10%/86.36%; HostAdapter 98.64%/89.47%; MailBridge.Client 90.48%/93.10%; MailBridge.Contracts 98.14%/93.65%; HostAdapter.Contracts 100%/n-a
- Both uniform thresholds satisfied (line >= 85%, branch >= 75%).
- Raw intermediates: `artifacts/csharp/final-107/*/coverage.cobertura.xml` (3 files).
