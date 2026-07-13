# Final QC — dotnet test with coverage

Timestamp: 2026-07-12T22-10
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
All tests pass in a single clean pass. Test counts by project:
- OpenClaw.HostAdapter.Tests: 107 passed, 0 failed, 0 skipped
- OpenClaw.Core.Tests: 940 passed, 0 failed, 0 skipped
- OpenClaw.MailBridge.Tests: 366 passed, 0 failed, 5 skipped (Windows/COM/publish-output integration tests skipped by environment guard)
- Total: 1413 passed, 0 failed, 5 skipped

Post-change coverage (XPlat / coverlet cobertura, per test project):
- OpenClaw.Core: line-rate 0.9524 (95.24%), branch-rate 0.8659 (86.59%); lines 3609/3789, branches 885/1022
- OpenClaw.MailBridge: line-rate 0.9375 (93.75%), branch-rate 0.8865 (88.65%); lines 1577/1682, branches 430/485
- OpenClaw.HostAdapter: line-rate 0.8821 (88.21%), branch-rate 0.6745 (67.45%); lines 1175/1332, branches 172/255

Aggregate (sum across the three cobertura reports):
- Line: 6361/6803 = 93.50%
- Branch: 1487/1762 = 84.39%

Raw cobertura reports retained:
- artifacts/csharp/final-core.cobertura.xml
- artifacts/csharp/final-mailbridge.cobertura.xml
- artifacts/csharp/final-hostadapter.cobertura.xml

Note: The OpenClaw.HostAdapter project-level branch-rate (67.45%) remains below the 75% branch threshold at the project level. This is a pre-existing condition (baseline 67.19%, see baseline-test-coverage-2026-07-12T21-38.md), not introduced by this feature; the value improved (+0.26 points). The feature's changed/new code meets the thresholds (see coverage-delta-2026-07-12T22-10.md).
