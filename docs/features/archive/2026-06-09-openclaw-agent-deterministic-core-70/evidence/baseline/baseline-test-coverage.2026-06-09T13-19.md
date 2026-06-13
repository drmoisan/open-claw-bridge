# Baseline — Test + Coverage (OpenClaw.Core.Tests) — Issue #70 FIX-1

Timestamp: 2026-06-09T13-19
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
- Test result: Passed! Failed: 0, Passed: 176, Skipped: 0, Total: 176.
- Coverage (per-package, Cobertura `coverage.cobertura.xml`):
  - OpenClaw.Core: line-rate 0.9857 (98.57%), branch-rate 0.9032 (90.32%).
  - OpenClaw.HostAdapter.Contracts: line 100%, branch 100%.
  - OpenClaw.MailBridge.Contracts: line 24.87%, branch 0% (DTO surface exercised indirectly; not the FIX-1 target).
- Authoritative baseline for the FIX-1 no-regression check is the OpenClaw.Core package: line 98.57% / branch 90.32% (matches the feature-audit expectation of ~98.57% line / ~90.32% branch).
- RecurringMeetingClassifier.Classify is reported at 100% line/branch in the baseline (six example-based tests already exercise all four partitions plus the two null-guard branches). The FIX-1 change is test-only; production Classify coverage must not drop.
- Note: the in-line "No code coverage data available. Profiler was not initialized." message is from the default MSTest dynamic code-coverage data collector; numeric coverage is supplied by the coverlet/XPlat Cobertura attachment, which is present and parsed above.
