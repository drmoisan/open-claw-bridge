# Regression Surface — Existing Paths Unmodified (F19, #130)

Timestamp: 2026-07-07T05-56
Command: `git status --porcelain -- src/ tests/` and `git diff --name-only 273c7df -- src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs` plus `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`
EXIT_CODE: 0

Output Summary:

Verified against the epic branch base `273c7df`:

- Zero pre-existing test files under `tests/OpenClaw.Core.Tests/` are modified. The `git status` `M` filter for `tests/` returns none; every test-tree change is an addition (`??`): the six new `*ProposeNewTime*` files. `HostAdapterHttpClientTests.cs` (616-line pre-existing cap violation) and `HostAdapterSchedulingServiceTests.cs` (480 lines) are untouched.
- `SchedulingWorker.cs`, `SchedulingWorker.Audit.cs`, and `SchedulingWorker.Reschedule.cs` are textually unmodified (git diff against base returns empty for all three). Their `BuildActingFlags` / `BuildRescheduleActingFlags` builders are therefore byte-identical.
- The full test run passes with the send-path and F18 reschedule-path audit/dedupe suites green: OpenClaw.Core.Tests 930 passed / 0 failed; OpenClaw.HostAdapter.Tests 100 passed; OpenClaw.MailBridge.Tests 347 passed / 5 skipped. The F19 path-isolation test (`SendAndReschedulePaths_PersistUnmodifiedActingFlags_AfterF19`) asserts the send ActingFlags string `SendEnabled=True;CalendarWriteEnabled=True` and the reschedule ActingFlags string `CalendarWriteEnabled=True;EnableOrganizerReschedule=False` remain byte-identical after F19.

Modified production files (the 7 permitted modifies):
- `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`
- `src/OpenClaw.Core/HostAdapterHttpClient.cs`
- `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`
- `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`
- `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`
- `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs`
- `src/OpenClaw.Core/Agent/SentActionKey.cs`

New production files (the 2 permitted adds):
- `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs`
- `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs`

New test files (6 adds, zero modifies):
- `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientProposeNewTimeTests.cs`
- `tests/OpenClaw.Core.Tests/HostAdapterHttpClientProposeNewTimeTests.cs`
- `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceProposeNewTimeTests.cs`
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeTests.cs`
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeEdgeTests.cs`
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeIntentPropertyTests.cs`

Verdict: PASS. Zero modifications to pre-existing test files and to the named production files; the changed-file set is confined to the plan's diff scope.
