Timestamp: 2026-07-07T02-15

## Architecture-Boundary Conflict (Blocking Finding)

### Summary

Instrumenting `GraphSubscriptionManager`, `NotificationRequestProcessor`, and
`GraphDeltaReconciler` (all in `OpenClaw.Core.CloudSync`) to call
`IActionAuditLog.RecordAsync` with `ActionAuditRecord` and the new
`CloudSyncActivityType`/`CloudSyncActivityResultCode`/`CloudSyncActingFlags`
constants — all of which live in namespace `OpenClaw.Core.Agent` per this
feature's binding design decision (spec.md decision 1) and the existing F9
placement of `IActionAuditLog`/`ActionAuditRecord` — creates a direct
`OpenClaw.Core.CloudSync -> OpenClaw.Core.Agent` namespace dependency.

This directly and unavoidably fails two pre-existing, explicitly-enforced
architecture-boundary tests in `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncArchitectureBoundaryTests.cs`
(authored for issue #117, predating this feature):

- `CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces` — CloudSync's allowed
  OpenClaw namespace prefixes are `OpenClaw.Core.CloudSync`,
  `OpenClaw.Core.CloudGraph`, `OpenClaw.Core.CloudAuth`,
  `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, plus the
  exact `OpenClaw.Core` namespace. `OpenClaw.Core.Agent` is not in this list.
- `CloudSync_DoesNotDependOnTheAgentPartition` — an explicit
  `ShouldNot().HaveDependencyOn("OpenClaw.Core.Agent")` assertion over every
  type in `OpenClaw.Core.CloudSync`.

Verified failure (isolated run):

```
dotnet test --filter "FullyQualifiedName~CloudSyncArchitectureBoundaryTests"
Failed!  - Failed: 2, Passed: 2, Skipped: 0, Total: 4
```

Both failures name exactly `GraphDeltaReconciler`, `GraphSubscriptionManager`,
and `NotificationRequestProcessor` as the offending types, and
`OpenClaw.Core.Agent` as the offending namespace — confirming the root cause
is precisely the audit-emission instrumentation added by this feature's Phases
3-5, not an unrelated pre-existing issue.

### Why This Is a Blocking Finding, Not a Task-Level Gap

Per `.claude/rules/architecture-boundaries.md`: "Architecture boundary
enforcement is a uniform gate across all tiers (T1-T4). Violations block
PRs." Per `.claude/rules/quality-tiers.md`: "Architecture violations: 0"
(uniform across all tiers). This is not a lower-priority style nit; it is one
of the seven mandatory toolchain stages (`general-code-change.md` stage 4)
and a hard merge gate.

This was not caught at this plan's preflight because Phase 0's baseline
architecture-boundary run (P0-T4) executed *before* any instrumentation code
existed, so the pre-existing `CloudSyncArchitectureBoundaryTests.cs` correctly
passed 14/14 at baseline. The conflict only manifests once the plan's own
binding design decision is implemented. Neither `spec.md`'s design-decision
section nor the research document
(`research/2026-07-07T05-10-graph-activity-log-purview-research.md`, section
2) checked the CloudSync instrumentation design against this pre-existing
architecture test before committing to "any CloudSync class can take
IActionAuditLog as a new constructor parameter."

### What This Executor Did

Per the atomic-executor contract ("complete the plan as written and escalate
at completion" when a task-level conflict is discovered mid-execution, since
inventing a new architectural seam is a new independent outcome not described
by the plan and not a permissible micro-action), this executor:

1. Implemented every Phase 1-5 task exactly as literally specified in the
   plan, including the direct `IActionAuditLog` constructor dependencies on
   the three CloudSync classes.
2. Did NOT invent a boundary-respecting seam/adapter (e.g., a CloudSync-local
   audit-port interface with an adapter to `IActionAuditLog` at the
   composition root) to route around the conflict, because that is a
   substantive, unauthorized design change outside this plan's binding
   decisions and task list.
3. Recorded the true (failing) architecture-boundary and CloudSync-suite
   regression results in
   `evidence/regression-testing/cloudsync-suite-regression.md` rather than
   fabricating a passing result.
4. Left the plan tasks whose literal acceptance criteria require
   `EXIT_CODE: 0` on this command unchecked (P6-T2; and, at Phase 8,
   P8-T3/P8-T6 for the final architecture-boundary and toolchain-clean-pass
   gates) pending a plan or architecture-rule revision.

### Remediation Options (for atomic-planner / architect decision, not this executor)

1. **Revise `CloudSyncArchitectureBoundaryTests.cs`'s allowlist** to
   explicitly permit `OpenClaw.Core.Agent` as an allowed CloudSync dependency
   surface, with an explicit rationale recorded in that test file's XML doc
   (this loosens an enforced architecture rule and should be a deliberate,
   reviewed decision, not a silent change by this executor).
2. **Introduce a narrow seam** (per `.claude/rules/csharp.md`'s "DI Seams"
   section, "Interface seam (preferred)"): define a CloudSync-local or
   shared-root audit-emission port (e.g., `ICloudSyncActivityAuditor` living
   outside `OpenClaw.Core.Agent`) that `GraphSubscriptionManager`,
   `NotificationRequestProcessor`, and `GraphDeltaReconciler` depend on
   instead of `IActionAuditLog` directly; register an adapter implementation
   at the composition root (`Program.cs`, which is already exempted from the
   `NothingOutsideCloudSync_DependsOnCloudSyncInternals` boundary) that
   bridges the port to `IActionAuditLog`. This preserves the existing
   boundary at the cost of an additional seam layer across all three
   production classes and their test factories/audit-test files.
3. **Move the CloudSync-scoped constants and record types out of
   `OpenClaw.Core.Agent`** into a new namespace CloudSync is already
   permitted to depend on (for example a new `OpenClaw.Core.CloudSync`-local
   set of constants), while leaving `IActionAuditLog`/`ActionAuditRecord`
   themselves in `OpenClaw.Core.Agent` and only crossing the boundary at the
   composition root via an adapter (a variant of option 2).

This executor does not select among these options; each represents a
different design trade-off (test-rule change vs. new seam vs. relocated
types) that changes the plan's binding design decisions and therefore
requires atomic-planner (or human) sign-off before implementation.
