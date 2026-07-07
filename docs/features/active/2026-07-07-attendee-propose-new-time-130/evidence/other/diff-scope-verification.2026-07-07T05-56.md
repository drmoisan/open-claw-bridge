# Diff-Scope Verification (F19, #130)

Timestamp: 2026-07-07T05-56
Command: `git status --porcelain -- src/ tests/` (tracked + untracked) against the epic branch base `273c7df`; plus an explicit `git status --porcelain` over the enumerated prohibited files
EXIT_CODE: 0

Output Summary:

Diffed against the epic branch base `273c7df` (per the preflight diff-scope advisory; not the possibly-stale local `main`). The changed src/ + tests/ set is exactly the plan's Global Constraints diff scope:

Modified production (7): `ActionAuditResultCode.cs`, `ISchedulingService.cs`, `HostAdapterSchedulingService.cs`, `SchedulingWorker.Pipeline.cs`, `SentActionKey.cs`, `HostAdapterHttpClient.cs`, `IHostAdapterClient.cs`.

New production (2): `SchedulingWorker.ProposeNewTime.cs`, `GraphHostAdapterClient.ProposeNewTime.cs`.

New tests (6): `HostAdapterSchedulingServiceProposeNewTimeTests.cs`, `SchedulingWorkerProposeNewTimeEdgeTests.cs`, `SchedulingWorkerProposeNewTimeIntentPropertyTests.cs`, `SchedulingWorkerProposeNewTimeTests.cs`, `GraphHostAdapterClientProposeNewTimeTests.cs`, `HostAdapterHttpClientProposeNewTimeTests.cs`.

Prohibited files — zero changes confirmed. `git status --porcelain` over each of the enumerated prohibited paths returns empty:
`Program.cs`, `SchedulingWorker.cs`, `AgentPolicyOptions.cs`, `CalendarWritePolicy.cs`, `appsettings.json`, `OneOnOneMoveGuard.cs`, `NormalizedMeetingContext.cs`, `ActionAuditRecord.cs`, `PurviewActivityLogProjection.cs`, `GraphRequestExecutor.cs`, `SendOnBehalfAuthorizer.cs`, `quality-tiers.yml`, `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`.

Non-code changes outside src/ and tests/ are limited to this feature folder (`docs/features/active/2026-07-07-attendee-propose-new-time-130/`: the plan checklist updates and the `evidence/` artifacts), which is within the permitted scope.

Verdict: PASS. The changed-file set matches the Global Constraints diff scope exactly; zero production/test drift outside scope; zero prohibited-file changes.
