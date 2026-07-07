# `graph-activity-log-purview` — User Story

- Issue: #124
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-07T01-00

## Story Statement

- As a ..., I want ..., so that ...
- As a ..., I want ..., so that ...

## Problem / Why

Master `docs/open-claw-approach.master.md` and gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` ("Proposed Epic and Feature Breakdown", Epic D / F20) identify a gap: the CloudSync subsystem delivered by F14 (`OpenClaw.Core.CloudSync` — `GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`, the notification queue seam) performs Graph-facing activity (subscription creation/renewal, webhook receipt, delta reconciliation) with no structured, Purview-auditable activity log of that traffic. Separately, F9 (`#107`) delivered a local structured `audit_log` store (`CoreCacheRepository.AuditLog.cs`, `IActionAuditLog`, `ActionAuditRecord`) with correlation-id threading for outbound send actions, but it does not yet capture CloudSync/Graph activity events, and nothing projects local audit records toward Microsoft Purview / Graph activity log conventions.

This feature closes that gap additively: it extends the existing F9 audit seam to record CloudSync/Graph activity events (subscription lifecycle, webhook receipt, delta reconciliation outcomes) with correlation-id threading, and adds a Purview-oriented activity-log projection contract, without changing any send or calendar behavior path.


## Personas & Scenarios

- Persona: ...
  - who the user is
  - what they care about
  - their constraints
  - their goals and frustrations
  - their context and motivations
- Scenario: ...
  - A concrete, step-by-step narrative that describes how a user accomplishes a goal in a real-world context using the system.
  - who is acting?
  - what triggered the action?
  - what steps do they take?
  - what obstacles or decisions occur?
  - what outcome do they expect?


## Acceptance Criteria

- [ ] The F9 audit seam is extended with CloudSync activity event types (subscription lifecycle, webhook received, delta reconciliation outcome), each carrying a correlation id, without changing the existing `IActionAuditLog` contract for prior consumers.
- [ ] The F14 CloudSync components emit these activity events through the extended audit seam additively, with no change to existing subscription/webhook/delta-reconciliation behavior, return values, or contracts (verified by the existing F14 test suite passing unchanged).
- [ ] A host-neutral, pure Purview-activity-log projection maps `ActionAuditRecord` (including the new CloudSync event types) to a Purview/Graph-activity-log-compatible record shape, testable without network access.
- [ ] Mocked-Graph/Purview contract tests cover the projection mapping and the CloudSync event-emission paths, with coverage thresholds held (line >= 85%, branch >= 75%).
- [ ] A human runbook documents live-tenant Purview ingestion verification and is recorded in orchestrator state as a `human_interaction` exception with a valid runbook_path.


## Non-Goals

Call out what is explicitly excluded from this feature.
