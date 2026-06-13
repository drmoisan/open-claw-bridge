# Final QA Gate: Test + Coverage

Timestamp: 2026-06-12T23-17

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary:
- Result: PASS in a single clean toolchain pass.
- Totals: 428 passed, 0 failed, 3 skipped.
  - OpenClaw.HostAdapter.Tests: 74 passed.
  - OpenClaw.Core.Tests: 178 passed.
  - OpenClaw.MailBridge.Tests: 176 passed, 3 skipped (platform-gated COM + 2 publish-output).
- Whole-assembly coverage headline (cobertura):
  - OpenClaw.HostAdapter assembly: line-rate 0.8462 (84.62%), branch-rate 0.6237 (62.37%).
  - OpenClaw.Core assembly: line-rate 0.8936 (89.36%), branch-rate 0.7758 (77.58%).
- Changed-code coverage (the gate surface; see coverage-delta artifact): aggregate lines 517/523 = 98.85%, branches 27/34 = 79.41%. Both above the line >= 85% / branch >= 75% thresholds.

Coverage attachment paths:
- tests/OpenClaw.HostAdapter.Tests/TestResults/8d218b5b-4811-4254-a89b-8c263055367c/coverage.cobertura.xml
- tests/OpenClaw.Core.Tests/TestResults/93de4c82-c23c-4300-93ab-8aa779dbcf9e/coverage.cobertura.xml
