Timestamp: 2026-07-07T02-10

Command: dotnet test --filter "FullyQualifiedName~OpenClaw.Core.Tests.CloudSync"

EXIT_CODE: 1

Output Summary: FAIL. 99 passed, 2 failed, 0 skipped, Total 101 (Duration 594 ms).

The 2 failures are both in the pre-existing `CloudSyncArchitectureBoundaryTests.cs`
(issue #117), not in any behavioral CloudSync test:

- `CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces` — fails because
  `OpenClaw.Core.CloudSync` now depends on `OpenClaw.Core.Agent` (offending
  dependency: `OpenClaw.Core.Agent`).
- `CloudSync_DoesNotDependOnTheAgentPartition` — fails because
  `GraphSubscriptionManager`, `NotificationRequestProcessor`, and
  `GraphDeltaReconciler` (all in `OpenClaw.Core.CloudSync`) now depend on
  `OpenClaw.Core.Agent` (`IActionAuditLog`, `ActionAuditRecord`,
  `CloudSyncActivityType`, `CloudSyncActivityResultCode`,
  `CloudSyncActingFlags`).

Root cause: this feature's binding design decision (spec.md decision 1; research
§1-2) places the new CloudSync activity constants and reuses `IActionAuditLog`/
`ActionAuditRecord` from `src/OpenClaw.Core/Agent/Contracts/` (namespace
`OpenClaw.Core.Agent`), and directs `GraphSubscriptionManager`,
`NotificationRequestProcessor`, and `GraphDeltaReconciler` to take `IActionAuditLog`
as a constructor parameter and call `RecordAsync` directly. This is a direct,
structural conflict with the pre-existing, explicitly-enforced architecture
boundary rule (`CloudSyncArchitectureBoundaryTests.cs`, issue #117) that CloudSync
must never depend on the Agent partition. Neither the plan nor its research phase
checked the instrumentation design against this pre-existing test before committing
to the binding decision.

All 99 passing tests are the CloudSync behavioral/regression tests (Phases 3-5
instrumentation, plus every pre-existing F14 CloudSync test file) — no behavioral
regression exists. The failure is confined to the two architecture-boundary
assertions.

This is reported as a BLOCKING finding requiring a plan/architecture-rule revision
(see `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/other/architecture-boundary-conflict.md`
for the full analysis and remediation options). Per policy, this task's literal
acceptance criterion (`EXIT_CODE: 0`) is not met and is NOT checked off in the plan.
