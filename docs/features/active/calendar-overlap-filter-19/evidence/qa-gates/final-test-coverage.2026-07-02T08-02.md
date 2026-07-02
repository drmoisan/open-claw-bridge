# Final QA — Test Run with Coverage ([P4-T4])

Timestamp: 2026-07-02T08-02
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
- Test results: 596 passed, 0 failed, 5 skipped (OpenClaw.HostAdapter.Tests 100/100; OpenClaw.Core.Tests 213/213; OpenClaw.MailBridge.Tests 283 passed, 5 skipped of 288). Architecture-boundary, unit, and integration tests all pass.
- Solution-wide (pooled across the three cobertura reports): line coverage 90.26% (4029/4464 lines), branch coverage 79.36% (911/1148 branches).
- Per-assembly cobertura totals: Core.Tests report line 89.62% / branch 78.44%; MailBridge.Tests report line 93.08% / branch 86.92%; HostAdapter.Tests report line 87.70% / branch 67.19%.
- OpenClaw.MailBridge package (module) values: line coverage 92.40%, branch coverage 84.61%.
- Raw cobertura intermediates staged under `artifacts/csharp/post-fix/`.
