# Baseline — dotnet test with coverage

Timestamp: 2026-07-03T01-26
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
- Test results: 1063 passed, 0 failed, 5 skipped (Core.Tests 616/616 passed; MailBridge.Tests 347 passed / 5 skipped; HostAdapter.Tests 100/100 passed).
- Pooled coverage (all 3 Cobertura reports): line 5234/5668 = 92.34%, branch 1269/1526 = 83.16%.
- Core package (OpenClaw.Core.Tests report): line 2588/2761 = 93.73%, branch 682/800 = 85.25%.
- MailBridge report: line 1533/1638 = 93.59%, branch 417/473 = 88.16%.
- HostAdapter report: line 1113/1269 = 87.71%, branch 170/253 = 67.19%.
- Raw Cobertura reports retained at `artifacts/csharp/baseline-117/coverage.{core,mailbridge,hostadapter}.cobertura.xml`.
