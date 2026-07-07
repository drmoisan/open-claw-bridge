# Final QA — C# Test / Architecture / Coverage Gate (Issue #128, P5-T5)

Timestamp: 2026-07-07T04-01
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final`
EXIT_CODE: 0

Output Summary:

All tests pass, including the NetArchTest architecture-boundary suites (part of the OpenClaw.Core.Tests run) and the CsCheck property suites.

- OpenClaw.Core.Tests: Failed 0, Passed 893, Skipped 0, Total 893.
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352 (5 skipped are COM/publish integration tests).
- Aggregate: 1340 passed, 5 skipped, 0 failed.

Post-change coverage (Cobertura):

- OpenClaw.Core (T1 target module) — line-rate 0.9927 (99.27%), branch-rate 0.9224 (92.24%). Both above policy thresholds (line >= 85%, branch >= 75%).

Changed/new production file coverage:

- GraphHostAdapterClient.RescheduleEvent.cs (add) — line 100%, branch 100%.
- SchedulingWorker.Reschedule.cs (add) — line 100%, branch 92.85%.
- SchedulingWorker.cs (modified) — line 100%, branch 100%.
- SchedulingWorker.Pipeline.cs (modified) — line 100%, branch 50% (unchanged from baseline; pre-existing partial-class branch artifact, not introduced by this change).
- HostAdapterHttpClient.cs (modified) — line 100%, branch 100%.
- HostAdapterSchedulingService.cs (modified) — line 100%, branch 100%.
- SentActionKey.cs (modified) — line 100%, branch 100%.
- ActionAuditResultCode.cs (modified) — const-only, no executable branches.
- ISchedulingService.cs (modified) — interface-only, no executable behavior.

Raw intermediates under `artifacts/csharp/final/` (three coverage.cobertura.xml plus the .coverage binary). No QA-loop restart remained after the coverage remediation (the null-requestId branch of the local `UpdateEventTimesAsync` was covered, restoring HostAdapterHttpClient.cs branch to 100%).
