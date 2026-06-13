# Baseline — Test + Coverage (Issue #71)

Timestamp: 2026-06-13T14-41
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
- Test result: PASS. Failed: 0, Passed: 462 (MailBridge.Tests 204, Core.Tests 184, HostAdapter.Tests 74), Skipped: 3, Total: 465.
- Coverage source: tests/OpenClaw.MailBridge.Tests/TestResults/3d4eee58-3dff-4d91-bed2-deeeb54968dd/coverage.cobertura.xml (coverlet XPlat Code Coverage; covers OpenClaw.MailBridge + OpenClaw.MailBridge.Contracts + OpenClaw.MailBridge.Client).
- Baseline MailBridge solution coverage (pre-change reference for the no-regression check in [P4-T6]):
  - Line coverage: 93.55% (lines-covered 973 / lines-valid 1040)
  - Branch coverage: 85.47% (branches-covered 259 / branches-valid 303)
- Per-file baseline for files to be modified:
  - OpenClaw.MailBridge\OutlookScanner.GraphFields.cs: line-rate 100%, branch-rate 100%
  - OpenClaw.MailBridge\ResponseShaper.cs: line-rate 100%, branch-rate 100%
  - OpenClaw.MailBridge\OutlookScanner.Attendees.cs: not present (new file in this feature)
- Note: the secondary "Code Coverage" (Vanguard) collector reported "Profiler was not initialized"; the coverlet XPlat collector produced the authoritative cobertura values above.

Baseline thresholds satisfied: line 93.55% >= 85%, branch 85.47% >= 75%.
