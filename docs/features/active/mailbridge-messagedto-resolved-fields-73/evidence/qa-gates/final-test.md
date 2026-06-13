# Final QA — Test + Coverage

Timestamp: 2026-06-13T13-34
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
- Test result: Passed. Failed: 0.
  - OpenClaw.HostAdapter.Tests: 89 passed / 89 total
  - OpenClaw.Core.Tests: 206 passed / 206 total
  - OpenClaw.MailBridge.Tests: 235 passed / 3 skipped / 238 total
  - Aggregate: 530 passed, 3 skipped, 533 total.
- Coverage (cobertura per-package = per-project assembly):
  - OpenClaw.MailBridge: line-rate 0.909 (90.90%), branch-rate 0.8039 (80.39%).
  - OpenClaw.Core: line-rate 0.986 (98.60%), branch-rate 0.9168 (91.68%).
  - OpenClaw.MailBridge.Contracts: line-rate 0.9813 (98.13%), branch-rate 0.9365 (93.65%).
- Test-assembly aggregate headlines:
  - MailBridge.Tests closure: line 0.9204 (92.04%), branch 0.8329 (83.29%); 1262/1371 lines, 334/401 branches.
  - Core.Tests closure: line 0.8957 (89.57%), branch 0.7844 (78.44%); 1486/1659 lines, 342/436 branches.
- Both affected projects (OpenClaw.MailBridge, OpenClaw.Core) exceed the uniform thresholds:
  line >= 85% and branch >= 75%.

Note: the OpenClaw.MailBridge.Contracts 28.5% line entry under Core.Tests is the partial-closure
view of the Contracts assembly loaded by the Core test project; the authoritative Contracts coverage
is the 98.13% figure from the MailBridge.Tests run.
