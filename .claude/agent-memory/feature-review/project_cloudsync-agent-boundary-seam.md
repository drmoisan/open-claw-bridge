---
name: cloudsync-agent-boundary-seam
description: OpenClaw.Core.CloudSync must never depend on OpenClaw.Core.Agent; #124 established the ICloudSyncActivityAuditor port+adapter precedent for crossing this boundary
metadata:
  type: project
---

`tests/OpenClaw.Core.Tests/CloudSync/CloudSyncArchitectureBoundaryTests.cs` (issue #117) enforces
that `OpenClaw.Core.CloudSync` types depend only on an explicit namespace allowlist
(`OpenClaw.Core.CloudSync`, `OpenClaw.Core.CloudGraph`, `OpenClaw.Core.CloudAuth`,
`OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, and the exact bare `OpenClaw.Core`
namespace) and explicitly asserts `ShouldNot().HaveDependencyOn("OpenClaw.Core.Agent")`.

**Why this matters for review:** any future feature that wants CloudSync components
(`GraphSubscriptionManager`, `NotificationRequestProcessor`, `GraphDeltaReconciler`, etc.) to call
into `OpenClaw.Core.Agent` functionality (e.g., `IActionAuditLog`, scheduling, or any other
Agent-partition service) WILL fail this test if it takes the naive direct-dependency path. On #124
(graph-activity-log-purview) the initial implementation did exactly that and failed 2/4
architecture-boundary tests (confirmed 14/14 -> 12/14 against the Phase 0 baseline). The accepted
resolution (Phase 9 revision, `evidence/other/architecture-boundary-conflict.md`) was: define a
narrow port in the bare `OpenClaw.Core` namespace (the one non-CloudSync namespace CloudSync is
allowed to depend on) with one semantic method per event/use-case, then register a composition-root
(`Program.cs`) adapter implementing that port in `OpenClaw.Core.Agent` — never register the adapter
inside `CloudSyncServiceCollectionExtensions.cs`, which lives in the disallowed-dependency direction
and would itself need to reference the Agent-partition concrete type.

**How to apply in review:** when a diff touches any `OpenClaw.Core.CloudSync` production file and
also touches `OpenClaw.Core.Agent`, do three checks: (1) `grep -rn "OpenClaw.Core.Agent"
src/OpenClaw.Core/CloudSync/` for a `using`/type reference (not just a substring match — check
whether the match is inside an XML-doc comment, which does not create a real dependency, as
happened harmlessly on #124); (2) `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary"`
for 14/14; (3) grep `CloudSyncServiceCollectionExtensions.cs` for the Agent-partition adapter type
name to confirm it is NOT registered there (registration belongs only in `Program.cs`). Do not
accept an executor's "zero production matches" claim without independently re-running the grep and
build/test yourself — the literal string can still appear in a comment, which is fine, but must be
distinguished from an actual dependency by checking whether the architecture tests still pass.

See also [[review-env-fallbacks]] and [[t2-property-test-gate]] for the #124 review's other
findings (CsCheck property tests again present and used correctly for the new pure
`PurviewActivityLogProjection.Project` function).
