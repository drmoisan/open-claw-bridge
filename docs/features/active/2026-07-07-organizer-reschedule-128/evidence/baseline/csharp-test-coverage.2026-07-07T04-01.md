# C# Test / Architecture-Boundary / Coverage Baseline (Issue #128)

Timestamp: 2026-07-07T04-01
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline`
EXIT_CODE: 0

Output Summary:

All tests pass; the NetArchTest architecture-boundary suites are part of the OpenClaw.Core.Tests run and pass.

- OpenClaw.Core.Tests: Failed 0, Passed 857, Skipped 0, Total 857.
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352 (5 skipped are COM/publish integration tests, non-Windows-CI conditional).
- Aggregate: 1304 passed, 5 skipped, 0 failed.

Baseline coverage (Cobertura, XPlat Code Coverage):

- OpenClaw.Core (T1 target module) — line-rate 0.9925 (99.25%), branch-rate 0.9221 (92.21%).
- OpenClaw.Core.Tests run overall (Core + referenced contracts) — line-rate 0.9502 (95.02%), branch-rate 0.8633 (86.33%).

Both OpenClaw.Core figures are well above the policy thresholds (line >= 85%, branch >= 75%). Raw intermediates under `artifacts/csharp/baseline/` (three coverage.cobertura.xml plus the .coverage binary).
