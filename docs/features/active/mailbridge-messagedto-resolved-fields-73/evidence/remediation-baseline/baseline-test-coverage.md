# Baseline — Test + Coverage State (Issue #73, Cycle 1)

Timestamp: 2026-06-14T09-16
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
- Test totals (all projects): Passed 530, Failed 0, Skipped 3, Total 533.
  - OpenClaw.HostAdapter.Tests: Passed 89, Failed 0, Skipped 0.
  - OpenClaw.Core.Tests: Passed 206, Failed 0, Skipped 0.
  - OpenClaw.MailBridge.Tests: Passed 235, Failed 0, Skipped 3 (platform-gated COM / publish-output tests).
- Per-project aggregate coverage (cobertura):
  - OpenClaw.MailBridge package: line-rate 0.9204 (92.04%), branch-rate 0.8329 (83.29%); lines-covered 1262 / 1371, branches-covered 334 / 401.
  - OpenClaw.Core package: line-rate 0.8957 (89.57%), branch-rate 0.7844 (78.44%); lines-covered 1486 / 1659, branches-covered 342 / 436.
- ComMessageSource.cs new-file coverage (parsed from MailBridge cobertura):
  - line-rate 0.8013 (80.13%), branch-rate 0.6086 (60.86%), complexity 52.
  - This is below the uniform thresholds (line >= 85%, branch >= 75%) and confirms the RF-1 shortfall (expected near 80.1% / 60.9%).

Coverage sources:
- tests/OpenClaw.MailBridge.Tests/TestResults/c5e5e4a2-6b01-4277-afe8-cfac6b23be17/coverage.cobertura.xml
- tests/OpenClaw.Core.Tests/TestResults/a53392db-dc3c-41bc-8f8c-c29cdb527c50/coverage.cobertura.xml
