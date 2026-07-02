# Final QA — dotnet test with coverage

Timestamp: 2026-07-02T13-21
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0
Output Summary:
- OpenClaw.HostAdapter.Tests: Passed 100, Failed 0, Skipped 0 (total 100)
- OpenClaw.Core.Tests: Passed 284, Failed 0, Skipped 0 (total 284; +30 vs baseline — 14 matcher unit, 4 matcher property, 4 candidate-source, 7 fallback, 1 ordinary-mail dedupe)
- OpenClaw.MailBridge.Tests: Passed 347, Failed 0, Skipped 5 (total 352)
- Suite total: 731 passed, 0 failed, 5 skipped (736 total)
- Pooled post-change line coverage: 90.81% (4298/4733)
- Pooled post-change branch coverage: 80.62% (990/1228)
- Per-report: Core.Tests line 90.47% / branch 80.27%; MailBridge.Tests line 93.58% / branch 88.16%; HostAdapter.Tests line 87.70% / branch 67.19%
- Architecture-boundary tests, unit tests, property tests, and dedupe tests all run in this suite.
- Raw Cobertura copies: `artifacts/csharp/final-2026-07-02T13-21/coverage.{core,mailbridge,hostadapter}.cobertura.xml`
