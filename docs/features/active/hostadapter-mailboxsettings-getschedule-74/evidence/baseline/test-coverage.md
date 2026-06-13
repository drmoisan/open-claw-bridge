# Baseline — Test + Coverage

Timestamp: 2026-06-13T10-30
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
- Tests: PASS. 474 passed, 0 failed, 3 skipped (3 skipped are non-Windows/publish-output guards).
  - OpenClaw.HostAdapter.Tests: 74 passed.
  - OpenClaw.Core.Tests: 184 passed.
  - OpenClaw.MailBridge.Tests: 216 passed, 3 skipped.
- Coverage (cobertura, per in-scope test project):
  - OpenClaw.Core.Tests coverage: line-rate 0.8913 (89.13%), branch-rate 0.7759 (77.59%); lines-covered 1435/1610, branches-covered 329/424.
  - OpenClaw.HostAdapter.Tests coverage: line-rate 0.8410 (84.10%), branch-rate 0.6028 (60.28%); lines-covered 873/1038, branches-covered 126/209.
  - OpenClaw.MailBridge.Tests: cobertura produced; not in feature scope.

Baseline headline numbers (for P9-T6 delta):
- Core: line 89.13%, branch 77.59%.
- HostAdapter: line 84.10%, branch 60.28%.

Notes:
- The HostAdapter `.coverage` binary attachment reported "Profiler was not initialized" for the MS Code Coverage collector, but the XPlat (coverlet) cobertura.xml was produced successfully and is the authoritative coverage source for this gate.
- Coverage gates apply to changed code (line >= 85%, branch >= 75%) with no regression on changed lines. Whole-project HostAdapter branch baseline (60.28%) reflects pre-existing uncovered route-wiring surface and is recorded here as the no-regression reference.
