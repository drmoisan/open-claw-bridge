# Remediation Baseline — Test + Coverage

Timestamp: 2026-06-16T07-57
Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --filter "TestCategory!=Integration"`
EXIT_CODE: 0

Output Summary:
- Result: Passed. Failed: 0, Skipped: 3, Total passing: 587 (Integration excluded).
  - OpenClaw.HostAdapter.Tests: 100 passed.
  - OpenClaw.Core.Tests: 210 passed.
  - OpenClaw.MailBridge.Tests: 277 passed, 3 skipped (Com_active_object_create_and_logon_should_throw_on_non_windows; two PublishOutput tests).
- Combined coverage (sum of per-assembly cobertura covered/valid across the three test projects):
  - Line: 4028/4463 = 90.25% (>= 85% PASS)
  - Branch: 911/1148 = 79.36% (>= 75% PASS)
- Per-project line/branch rates:
  - Core.Tests: line 89.61%, branch 78.44%.
  - HostAdapter.Tests: line 87.70%, branch 67.19%.
  - MailBridge.Tests: line 93.08%, branch 86.92%.
- Baseline passing test count 587 confirmed (matches expected baseline).
