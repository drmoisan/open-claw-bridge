# graph-activity-log-purview — Spec

- **Issue:** #124
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T01-00
- **Status:** Draft
- **Version:** 0.1

## Overview

Master `docs/open-claw-approach.master.md` and gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` ("Proposed Epic and Feature Breakdown", Epic D / F20) identify a gap: the CloudSync subsystem delivered by F14 (`OpenClaw.Core.CloudSync` — `GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`, the notification queue seam) performs Graph-facing activity (subscription creation/renewal, webhook receipt, delta reconciliation) with no structured, Purview-auditable activity log of that traffic. Separately, F9 (`#107`) delivered a local structured `audit_log` store (`CoreCacheRepository.AuditLog.cs`, `IActionAuditLog`, `ActionAuditRecord`) with correlation-id threading for outbound send actions, but it does not yet capture CloudSync/Graph activity events, and nothing projects local audit records toward Microsoft Purview / Graph activity log conventions.

This feature closes that gap additively: it extends the existing F9 audit seam to record CloudSync/Graph activity events (subscription lifecycle, webhook receipt, delta reconciliation outcomes) with correlation-id threading, and adds a Purview-oriented activity-log projection contract, without changing any send or calendar behavior path.


## Behavior

- Extend the F9 audit seam (`IActionAuditLog` / `ActionAuditRecord` / `CoreCacheRepository.AuditLog.cs`) with CloudSync-scoped activity event types (subscription created/renewed/expired, webhook notification received, delta reconciliation run/outcome), each carrying a correlation id threaded from the originating CloudSync operation.
- Instrument the F14 CloudSync components (`GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`) to emit these activity events through the extended audit seam, additively (no change to their existing subscription/webhook/reconciliation behavior or return contracts).
- Add a host-neutral Purview-activity-log projection: a pure mapping from the local `ActionAuditRecord` audit shape to a Microsoft Purview / Graph activity-log-compatible record shape (field names/structure aligned to the Graph `auditLogs` / Purview unified audit log conventions), with no direct network call to Purview from this feature (export/shipping is out of scope; the projection is the auditable contract).
- All tenant-dependent verification (that a real Purview/Graph activity log ingests the projected record) ships as mocked-Graph/Purview contract tests plus a human runbook recorded as a `human_interaction` exception, mirroring the F11 HI-1 / F17 precedent, because no Azure/Exchange/Purview credentials exist in this environment or CI.


## Inputs / Outputs

- Inputs (CLI flags, files, env vars)
- Outputs (artifacts, logs, telemetry)
- Config keys and defaults:
- Versioning or backward-compatibility constraints:

## API / CLI Surface

List commands, flags, request/response shapes, and examples.
- Example invocations with expected outputs (concise):
- Contracts and validation rules:

## Data & State

Data flow, storage, or state changes introduced by this feature.
- Data transformations and invariants:
- Caching or persistence details:
- Migration or backfill requirements (if any):

## Constraints & Risks

- No live tenant: every tenant-dependent step (actual Purview ingestion, Graph activity log query) ships mocked; no Azure/Exchange/Purview credentials in this environment or CI.
- Additive only: must not alter send or calendar behavior paths, and must not change the existing F9 `IActionAuditLog` contract signature for current consumers (`SchedulingWorker.Audit.cs` and other existing callers).
- Must not alter F14 CloudSync subscription/webhook/reconciliation behavior; instrumentation is additive logging only.
- Correlation-id threading must be consistent with the F9 precedent so audit records remain joinable across the outbound-send and CloudSync activity domains.


## Implementation Strategy

- Implementation scope (what changes, not sequencing):
- New classes/functions/commands to add or update:
- Dependency changes (new/removed packages) and rationale:
- Logging/telemetry additions and locations:
- Rollout plan (feature flags, staged deploys, fallback path):

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)
- [ ] Unit: CloudSync activity event construction and correlation-id threading for subscription lifecycle, webhook receipt, and delta reconciliation outcomes.
- [ ] Unit: Purview-activity-log projection mapping — pure-function coverage of the record-shape transform, including edge cases (missing optional fields, multiple event types).
- [ ] Contract: mocked-Graph/Purview activity ingestion contract test confirms the projected record shape matches the target schema.
- [ ] Regression: existing F14 CloudSync test suite and F9 audit-log test suite pass unchanged (no behavior regression from additive instrumentation).
- [ ] Human runbook: live-tenant Purview ingestion verification procedure (out of CI).
