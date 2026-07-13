# Baseline — dotnet test with coverage

Timestamp: 2026-07-12T21-38
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
All tests pass. Test counts by project:
- OpenClaw.HostAdapter.Tests: 100 passed, 0 failed, 0 skipped
- OpenClaw.Core.Tests: 931 passed, 0 failed, 0 skipped
- OpenClaw.MailBridge.Tests: 347 passed, 0 failed, 5 skipped (Windows/COM/publish-output integration tests skipped by environment guard)
- Total: 1378 passed, 0 failed, 5 skipped

Baseline coverage (XPlat / coverlet cobertura, per test project):
- OpenClaw.Core: line-rate 0.9524 (95.24%), branch-rate 0.8659 (86.59%); lines 3587/3766, branches 885/1022
- OpenClaw.MailBridge: line-rate 0.9358 (93.58%), branch-rate 0.8816 (88.16%); lines 1533/1638, branches 417/473
- OpenClaw.HostAdapter: line-rate 0.8770 (87.70%), branch-rate 0.6719 (67.19%); lines 1113/1269, branches 170/253

Aggregate (sum across the three cobertura reports):
- Line: 6233/6673 = 93.41%
- Branch: 1472/1748 = 84.21%

Note: OpenClaw.HostAdapter project-level branch-rate (67.19%) is below the 75% branch threshold at baseline. This is a pre-existing state, not introduced by this feature. The feature coverage gate is enforced on changed/new code (line >= 85%, branch >= 75%) plus no-regression on changed lines; the pre-existing project-level branch shortfall is recorded here for the coverage-delta comparison (P6-T4).

Raw cobertura reports retained:
- artifacts/csharp/baseline-core.cobertura.xml
- artifacts/csharp/baseline-mailbridge.cobertura.xml
- artifacts/csharp/baseline-hostadapter.cobertura.xml
