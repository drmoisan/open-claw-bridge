# Baseline Test & Coverage State — Issue #92

Timestamp: 2026-07-01T19-46

Command: dotnet test OpenClaw.MailBridge.sln -c Release --settings mailbridge.runsettings --collect:"XPlat Code Coverage"

EXIT_CODE: 0

Output Summary:
- Tests executed successfully at baseline (this command does not use /warnaserror, so NU1903 is a warning here and does not block test execution).
- Test results (587 passed, 5 skipped, 0 failed across 3 test assemblies):
  - OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
  - OpenClaw.Core.Tests: Failed 0, Passed 210, Skipped 0, Total 210.
  - OpenClaw.MailBridge.Tests: Failed 0, Passed 277, Skipped 5, Total 282.
- Coverage (cobertura, `mailbridge.runsettings` excludes `[*.Tests]*`):
  - OpenClaw.Core.Tests report: line-rate 0.9006 (90.06%), branch-rate 0.7769 (77.69%); lines 1323/1469, branches 317/408.
  - OpenClaw.MailBridge.Tests report: line-rate 0.9357 (93.57%), branch-rate 0.8775 (87.75%); lines 1166/1246, branches 387/441.
  - OpenClaw.HostAdapter.Tests report: line-rate 0.8846 (88.46%), branch-rate 0.6719 (67.19%); lines 997/1127, branches 170/253.
  - Pooled across all three reports: line coverage 90.73% (3486/3842), branch coverage 79.31% (874/1102).
- Baseline pooled numbers meet policy thresholds (line >= 85%, branch >= 75%). These are the pre-bump baseline figures for the P2-T6 delta comparison.
