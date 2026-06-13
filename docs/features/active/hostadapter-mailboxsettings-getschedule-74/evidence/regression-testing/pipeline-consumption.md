# Phase 7 — Pipeline Consumption Regression (SlotProposer / SchedulingWorker)

Timestamp: 2026-06-13T10-30
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~SchedulingWorkerTests|FullyQualifiedName~SlotProposer"
EXIT_CODE: 0

Output Summary: PASS. 15 SlotProposer/SchedulingWorker tests pass unchanged against the relocated
DTOs and the now-implemented scheduling methods.

Pipeline consumption confirmation:
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs` sets up
  `ISchedulingService.GetMailboxSettingsAsync` (line 83) and `GetFreeBusyAsync` (line 87) on the
  mocked service and verifies `GetMailboxSettingsAsync` is invoked `Times.Once` (line 166). The
  `propose_times` pipeline (SchedulingWorker -> SlotProposer.ProposeTimes) therefore consumes
  both methods.
- SlotProposer and SlotProposerProperty tests pass after the DTO `using` updates (P1-T4) with no
  behavioral change.

Full-solution test run (same phase): 498 passed, 0 failed, 3 skipped
(HostAdapter 89, Core 193, MailBridge 216/3 skipped).
