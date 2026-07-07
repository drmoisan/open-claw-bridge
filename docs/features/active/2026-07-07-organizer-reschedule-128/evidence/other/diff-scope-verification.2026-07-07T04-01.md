# Diff-Scope Verification (Issue #128, P5-T7)

Timestamp: 2026-07-07T04-01
Command: `git diff --name-only HEAD -- 'src/**' 'tests/**'` plus `git ls-files --others --exclude-standard`
EXIT_CODE: 0

Output Summary: The `src/` and `tests/` changed-file set matches the Global Constraints diff scope exactly; zero changes to any enumerated prohibited file.

## Production — modified (8, expected)

- src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs
- src/OpenClaw.Core/HostAdapterHttpClient.cs
- src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs
- src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs
- src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs
- src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs
- src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs
- src/OpenClaw.Core/Agent/SentActionKey.cs

## Production — added (2, expected)

- src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs
- src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs

## Tests — modified (4 mechanical, expected)

- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerAuditTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerFallbackTests.cs

## Tests — added (6, expected)

- tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs
- tests/OpenClaw.Core.Tests/HostAdapterHttpClientRescheduleTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceRescheduleTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleEdgeTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleIntentPropertyTests.cs

## Prohibited files — confirmed UNCHANGED

Program.cs, CalendarWritePolicy.cs, OneOnOneMoveGuard.cs, NormalizedMeetingContext.cs,
ActionAuditRecord.cs, PurviewActivityLogProjection.cs, GraphRequestExecutor.cs,
SendOnBehalfAuthorizer.cs, quality-tiers.yml — none appear in the changed set.
`HostAdapterHttpClientTests.cs` (616-line pre-existing violation) is also unmodified.

## Non-feature working-tree noise (pre-existing, not produced by this executor)

- `.claude/agent-memory/task-researcher/project_state_2026_07.md` (untracked agent-memory file, unrelated to F18).
- Session-start pre-existing edits under `.claude/` and `docs/features/epics/openclaw-vision/epic-status.md` (present before execution began).

These are outside `src/`/`tests/` and outside the feature folder; they are not feature code drift. Verdict: PASS — production/test drift is confined exactly to the allowed diff scope.
