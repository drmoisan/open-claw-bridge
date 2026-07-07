---
name: cloudsync-agent-partition-architecture-boundary
description: OpenClaw.Core.CloudSync has a pre-existing, enforced NetArchTest rule forbidding any dependency on OpenClaw.Core.Agent; check it before wiring CloudSync classes to IActionAuditLog/ActionAuditRecord.
metadata:
  type: project
---

`tests/OpenClaw.Core.Tests/CloudSync/CloudSyncArchitectureBoundaryTests.cs` (authored
for issue #117, predates F20/#124) enforces via NetArchTest that no type in
`OpenClaw.Core.CloudSync` may depend on `OpenClaw.Core.Agent`
(`CloudSync_DoesNotDependOnTheAgentPartition`) and that CloudSync's allowed OpenClaw
namespace prefixes are limited to `OpenClaw.Core.CloudSync`, `OpenClaw.Core.CloudGraph`,
`OpenClaw.Core.CloudAuth`, `OpenClaw.HostAdapter.Contracts`,
`OpenClaw.MailBridge.Contracts`, and the exact `OpenClaw.Core` namespace
(`CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces`).

`IActionAuditLog`, `ActionAuditRecord`, and every F9 audit constant class
(`SentActionKey`, `ActionAuditResultCode`) live in namespace `OpenClaw.Core.Agent`
(under `src/OpenClaw.Core/Agent/Contracts/`), even though `ActionAuditRecord.cs`
physically sits in the `Contracts` folder alongside CloudSync-adjacent files.

**Why:** During feature-124 (graph-activity-log-purview), the plan's binding design
decision (spec.md decision 1, informed by research that explicitly recommended
"any CloudSync class can take IActionAuditLog as a new constructor parameter with no
change needed") directed `GraphSubscriptionManager`, `NotificationRequestProcessor`,
and `GraphDeltaReconciler` (all `OpenClaw.Core.CloudSync`) to depend on
`IActionAuditLog`/`ActionAuditRecord`/new `CloudSyncActivityType` constants directly.
This was only discovered to violate the architecture boundary during Phase 6
regression testing — the Phase 0 baseline architecture-test run passed 14/14 because
it ran before the instrumentation code existed. Neither the plan nor its research
phase grepped or ran `CloudSyncArchitectureBoundaryTests.cs` before committing to the
design. Result: 2 confirmed, unavoidable architecture-boundary test failures
(`CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces`,
`CloudSync_DoesNotDependOnTheAgentPartition`) that could not be resolved without
either revising the architecture test's allowlist or introducing a boundary-respecting
seam/adapter — both of which are design decisions requiring atomic-planner/architect
sign-off, not something an executor should invent mid-execution. Full writeup:
`docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/other/architecture-boundary-conflict.md`.

**How to apply:** Before planning or executing any feature that wires
`OpenClaw.Core.CloudSync` classes to F9 audit-seam types (or any other
`OpenClaw.Core.Agent` type), run
`dotnet test --filter "FullyQualifiedName~CloudSyncArchitectureBoundaryTests"` (or read
the test file directly) at research/preflight time, before committing to a binding
design decision. If the design requires crossing this boundary, flag it explicitly as
an open question for the planner rather than assuming direct DI wiring is
architecture-clean just because DI *resolution* would succeed.
