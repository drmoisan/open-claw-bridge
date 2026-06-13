# Baseline — Test + Coverage

Timestamp: 2026-06-12T22-30

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary:
- Result: PASS. All test projects green.
- Totals: 425 passed, 0 failed, 3 skipped (platform-gated COM test + 2 publish-output tests).
  - OpenClaw.HostAdapter.Tests: 71 passed, 0 failed, 0 skipped.
  - OpenClaw.Core.Tests: 178 passed, 0 failed, 0 skipped.
  - OpenClaw.MailBridge.Tests: 176 passed, 0 failed, 3 skipped.
- Assembly-level coverage headline (cobertura, whole assembly — the no-regression reference for the touched projects):
  - OpenClaw.HostAdapter.Tests cobertura: line-rate 0.8499 (84.99%), branch-rate 0.6288 (62.88%).
  - OpenClaw.Core.Tests cobertura: line-rate 0.8932 (89.32%), branch-rate 0.7758 (77.58%).
- Note: cobertura `coverage` element rates are whole-assembly aggregates. The repository coverage gate (line >= 85% / branch >= 75%) and the no-regression rule apply to changed code; changed-code coverage is computed in P3-T5 against these baseline figures.

Coverage attachment paths:
- tests/OpenClaw.HostAdapter.Tests/TestResults/e8dc7a2e-5085-4b24-b5c2-f6fec564fa30/coverage.cobertura.xml
- tests/OpenClaw.Core.Tests/TestResults/2c39c1a6-8dbb-49a3-a07c-fec09ee0608d/coverage.cobertura.xml
