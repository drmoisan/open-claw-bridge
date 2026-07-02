# Baseline — Tests and Coverage (dotnet test + XPlat Code Coverage)

Timestamp: 2026-07-02T10-54
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline
EXIT_CODE: 0
Output Summary:
- Tests: 660 passed, 0 failed, 5 skipped (environment-gated COM/publish tests), 665 total across 3 test assemblies (OpenClaw.Core.Tests 213 passed, OpenClaw.HostAdapter.Tests 100 passed, OpenClaw.MailBridge.Tests 347 passed / 5 skipped).
- Solution-wide pooled coverage (3 Cobertura files summed): line 4149/4584 = 90.51%, branch 929/1162 = 79.95%.
- OpenClaw.Core package coverage: line 1419/1439 = 98.61%, branch 342/373 = 91.69%.
- Thresholds (uniform T1-T4): line >= 85% PASS, branch >= 75% PASS at baseline.
- Raw Cobertura staged under `artifacts/csharp/baseline/` (3 coverage.cobertura.xml files).
