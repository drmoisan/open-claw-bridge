# graph-activity-log-purview (Issue #124)

- Date captured: 2026-07-07
- Author: drmoisan
- Status: Promoted -> docs/features/active/graph-activity-log-purview/ (Issue #124)

- Epic: openclaw-vision (Epic D, F20); depends on F14 (graph-subscriptions-delta, #117, merged as PR #121); extends F9 (outbound-audit-log, #107)

- Issue: #124
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/124
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Problem / Why

Master `docs/open-claw-approach.master.md` and gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` ("Proposed Epic and Feature Breakdown", Epic D / F20) identify a gap: the CloudSync subsystem delivered by F14 (`OpenClaw.Core.CloudSync` — `GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`, the notification queue seam) performs Graph-facing activity (subscription creation/renewal, webhook receipt, delta reconciliation) with no structured, Purview-auditable activity log of that traffic. Separately, F9 (`#107`) delivered a local structured `audit_log` store (`CoreCacheRepository.AuditLog.cs`, `IActionAuditLog`, `ActionAuditRecord`) with correlation-id threading for outbound send actions, but it does not yet capture CloudSync/Graph activity events, and nothing projects local audit records toward Microsoft Purview / Graph activity log conventions.

This feature closes that gap additively: it extends the existing F9 audit seam to record CloudSync/Graph activity events (subscription lifecycle, webhook receipt, delta reconciliation outcomes) with correlation-id threading, and adds a Purview-oriented activity-log projection contract, without changing any send or calendar behavior path.

## Proposed Behavior

- Extend the F9 audit seam (`IActionAuditLog` / `ActionAuditRecord` / `CoreCacheRepository.AuditLog.cs`) with CloudSync-scoped activity event types (subscription created/renewed/expired, webhook notification received, delta reconciliation run/outcome), each carrying a correlation id threaded from the originating CloudSync operation.
- Instrument the F14 CloudSync components (`GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`) to emit these activity events through the extended audit seam, additively (no change to their existing subscription/webhook/reconciliation behavior or return contracts).
- Add a host-neutral Purview-activity-log projection: a pure mapping from the local `ActionAuditRecord` audit shape to a Microsoft Purview / Graph activity-log-compatible record shape (field names/structure aligned to the Graph `auditLogs` / Purview unified audit log conventions), with no direct network call to Purview from this feature (export/shipping is out of scope; the projection is the auditable contract).
- All tenant-dependent verification (that a real Purview/Graph activity log ingests the projected record) ships as mocked-Graph/Purview contract tests plus a human runbook recorded as a `human_interaction` exception, mirroring the F11 HI-1 / F17 precedent, because no Azure/Exchange/Purview credentials exist in this environment or CI.

## Acceptance Criteria (early draft)

- [x] The F9 audit seam is extended with CloudSync activity event types (subscription lifecycle, webhook received, delta reconciliation outcome), each carrying a correlation id, without changing the existing `IActionAuditLog` contract for prior consumers.
- [ ] The F14 CloudSync components emit these activity events through the extended audit seam additively, with no change to existing subscription/webhook/delta-reconciliation behavior, return values, or contracts (verified by the existing F14 test suite passing unchanged).
- [x] A host-neutral, pure Purview-activity-log projection maps `ActionAuditRecord` (including the new CloudSync event types) to a Purview/Graph-activity-log-compatible record shape, testable without network access.
- [ ] Mocked-Graph/Purview contract tests cover the projection mapping and the CloudSync event-emission paths, with coverage thresholds held (line >= 85%, branch >= 75%).
- [ ] A human runbook documents live-tenant Purview ingestion verification and is recorded in orchestrator state as a `human_interaction` exception with a valid runbook_path.

## Constraints & Risks

- No live tenant: every tenant-dependent step (actual Purview ingestion, Graph activity log query) ships mocked; no Azure/Exchange/Purview credentials in this environment or CI.
- Additive only: must not alter send or calendar behavior paths, and must not change the existing F9 `IActionAuditLog` contract signature for current consumers (`SchedulingWorker.Audit.cs` and other existing callers).
- Must not alter F14 CloudSync subscription/webhook/reconciliation behavior; instrumentation is additive logging only.
- Correlation-id threading must be consistent with the F9 precedent so audit records remain joinable across the outbound-send and CloudSync activity domains.

## Test Conditions to Consider

- [ ] Unit: CloudSync activity event construction and correlation-id threading for subscription lifecycle, webhook receipt, and delta reconciliation outcomes.
- [ ] Unit: Purview-activity-log projection mapping — pure-function coverage of the record-shape transform, including edge cases (missing optional fields, multiple event types).
- [ ] Contract: mocked-Graph/Purview activity ingestion contract test confirms the projected record shape matches the target schema.
- [ ] Regression: existing F14 CloudSync test suite and F9 audit-log test suite pass unchanged (no behavior regression from additive instrumentation).
- [ ] Human runbook: live-tenant Purview ingestion verification procedure (out of CI).

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/graph-activity-log-purview/` folder from the template

