# Baseline — dotnet test with coverage

Timestamp: 2026-07-02T13-02
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0
Output Summary:
- OpenClaw.HostAdapter.Tests: Passed 100, Failed 0, Skipped 0 (total 100)
- OpenClaw.Core.Tests: Passed 254, Failed 0, Skipped 0 (total 254)
- OpenClaw.MailBridge.Tests: Passed 347, Failed 0, Skipped 5 (total 352)
- Suite total: 701 passed, 0 failed, 5 skipped (706 total)
- Pooled line coverage: 90.63% (4207/4642 lines covered across the three Cobertura reports)
- Pooled branch coverage: 80.25% (947/1180 branches covered)
- Per-report: Core.Tests line 89.97% / branch 79.29%; MailBridge.Tests line 93.58% / branch 88.16%; HostAdapter.Tests line 87.70% / branch 67.19%
- Raw Cobertura copies: `artifacts/csharp/baseline-2026-07-02T13-02/coverage.{core,mailbridge,hostadapter}.cobertura.xml`
