# Final QA Step 3 — dotnet test with coverage (single clean pass)

Timestamp: 2026-07-03T02-16
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
- Test results: 1154 passed, 0 failed, 5 skipped (Core.Tests 707/707 passed — includes the NetArchTest architecture suites and all new CloudSync tests; MailBridge.Tests 347 passed / 5 skipped; HostAdapter.Tests 100/100 passed).
- Pooled post-change coverage (all 3 Cobertura reports): line 5622/6056 = 92.83%, branch 1312/1576 = 83.25%.
- Core package (OpenClaw.Core.Tests report): line 2976/3149 = 94.51%, branch 725/850 = 85.29%.
- MailBridge report: line 1533/1638 = 93.59%, branch 417/473 = 88.16% (unchanged from baseline).
- HostAdapter report: line 1113/1269 = 87.71%, branch 170/253 = 67.19% (unchanged from baseline).
- Single clean pass: format (step 1), build (step 2), and this test run (step 3) all passed without any failure or file change, so no loop restart was required.
- Raw Cobertura reports retained at `artifacts/csharp/final-117/coverage.{core,mailbridge,hostadapter}.cobertura.xml`.
