# graph-activity-log-purview — Spec

- **Issue:** #124
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T03-00
- **Status:** Draft
- **Version:** 0.2 (decision 1 amended: boundary-preserving `ICloudSyncActivityAuditor` mediation added post-execution; see `evidence/other/architecture-boundary-conflict.md`)

## Overview

Master `docs/open-claw-approach.master.md` and gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` ("Proposed Epic and Feature Breakdown", Epic D / F20) identify a gap: the CloudSync subsystem delivered by F14 (`OpenClaw.Core.CloudSync` — `GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`, the notification queue seam) performs Graph-facing activity (subscription creation/renewal, webhook receipt, delta reconciliation) with no structured, Purview-auditable activity log of that traffic. Separately, F9 (`#107`) delivered a local structured `audit_log` store (`CoreCacheRepository.AuditLog.cs`, `IActionAuditLog`, `ActionAuditRecord`) with correlation-id threading for outbound send actions, but it does not yet capture CloudSync/Graph activity events, and nothing projects local audit records toward Microsoft Purview / Graph activity log conventions.

This feature closes that gap additively: it extends the existing F9 audit seam to record CloudSync/Graph activity events (subscription lifecycle, webhook receipt, delta reconciliation outcomes) with correlation-id threading, and adds a Purview-oriented activity-log projection contract, without changing any send or calendar behavior path.


## Behavior

- Extend the F9 audit seam (`IActionAuditLog` / `ActionAuditRecord` / `CoreCacheRepository.AuditLog.cs`) with CloudSync-scoped activity event types (subscription created/renewed/expired, webhook notification received, delta reconciliation run/outcome), each carrying a correlation id threaded from the originating CloudSync operation.
- Instrument the F14 CloudSync components (`GraphSubscriptionManager`, `GraphNotificationsEndpoint`, `DeltaReconciliationWorker`, `GraphDeltaReconciler`) to emit these activity events through the extended audit seam, additively (no change to their existing subscription/webhook/reconciliation behavior or return contracts).
- Add a host-neutral Purview-activity-log projection: a pure mapping from the local `ActionAuditRecord` audit shape to a Microsoft Purview / Graph activity-log-compatible record shape (field names/structure aligned to the Graph `auditLogs` / Purview unified audit log conventions), with no direct network call to Purview from this feature (export/shipping is out of scope; the projection is the auditable contract).
- All tenant-dependent verification (that a real Purview/Graph activity log ingests the projected record) ships as mocked-Graph/Purview contract tests plus a human runbook recorded as a `human_interaction` exception, mirroring the F11 HI-1 / F17 precedent, because no Azure/Exchange/Purview credentials exist in this environment or CI.


## Design Decisions (resolved from research 2026-07-07T05-10)

These decisions are binding for planning and implementation; they resolve the four open ambiguities recorded in `research/2026-07-07T05-10-graph-activity-log-purview-research.md` §"Open Ambiguities for the Planner".

1. **Required-non-empty-field gap (`MessageId`, `ActingFlags`).** No schema (`audit_log` DDL) or `IActionAuditLog` interface change. Reuse both fields with legitimately meaningful values rather than arbitrary sentinels or a relaxed `NOT NULL` constraint:
   - `MessageId` carries the CloudSync event's subject-resource identifier: the Graph subscription id for subscription lifecycle events, the notification's `resourceData.id` for webhook-received/rejected events, and the delta-reconcile `requestId` for reconciliation-outcome events.
   - `ActingFlags` carries a new fixed constant, `CloudSyncActingFlags.NotApplicable = "N/A:CloudSyncActivity"`, documented as the CloudSync-domain analogue of the send/calendar acting-flags string (which has no meaning for CloudSync events).
   - **Boundary-preserving mediation (added post-execution, issue #117 AC-4 conflict).** `OpenClaw.Core.CloudSync` components do not construct `ActionAuditRecord` or reference `IActionAuditLog`/`CloudSyncActivityType`/`CloudSyncActivityResultCode`/`CloudSyncActingFlags` directly, and never reference `OpenClaw.Core.Agent`. Instead they depend on a narrow port, `ICloudSyncActivityAuditor` (bare `OpenClaw.Core` namespace — the one non-CloudSync namespace `CloudSyncArchitectureBoundaryTests` explicitly allows CloudSync to depend on), exposing one semantic async method per `CloudSyncActivityType` value. The Agent-side adapter `CloudSyncActivityAuditor` (`OpenClaw.Core.Agent`) implements the port, performs the `MessageId`/`ActingFlags` mapping described above, constructs the `ActionAuditRecord`, and calls `IActionAuditLog.RecordAsync`. This seam was introduced after initial execution surfaced a direct `CloudSync -> Agent` architecture-boundary violation against the pre-existing, issue-#117 `CloudSyncArchitectureBoundaryTests`; see `evidence/other/architecture-boundary-conflict.md` for the conflict record and the chosen resolution (Option 2: interface seam + composition-root-registered adapter).
2. **Webhook correlation-id generation point.** Generate a new `Guid.NewGuid().ToString()` once per notification item, at each `queue.TryEnqueue(...)` call site in `NotificationRequestProcessor.ProcessItemAsync` (and at the corresponding rejection point per decision 3), matching the existing "one GUID per operation" pattern already used in `GraphDeltaReconciler.RunAsync`.
3. **Dropped/invalid webhook audit scope.** In scope. Rejected webhook deliveries (unknown `subscriptionId`, mismatched `clientState`, missing `resourceData.id`) also emit an audit record, with `ResultCode` set to a new `CloudSyncActivityResultCode` constant identifying the specific rejection reason (e.g., `unknown-subscription`, `client-state-mismatch`, `missing-resource-id`). This is a security-relevant signal and is required for a complete Purview-oriented audit trail.
4. **Target Purview/Graph field set.** Pin to the Microsoft Graph `directoryAudit` resource shape (`id`, `activityDateTime`, `activityDisplayName`, `category`, `correlationId`, `operationType`, `result`, `resultReason`, `initiatedBy`, `targetResources`, `additionalDetails`) per research §3, verified against Microsoft Learn (`https://learn.microsoft.com/en-us/graph/api/resources/directoryaudit`). The mapping is explicitly illustrative/aspirational — no live Purview or Graph activity-log endpoint exists in this environment or CI; live-tenant ingestion verification is deferred to a `human_interaction` exception runbook (F11 HI-1 / F17 precedent).

New extensibility constants (new `const string` classes paralleling `SentActionKey`/`ActionAuditResultCode`, per the existing extensibility pattern — no enum, no interface change):
- `CloudSyncActivityType` (new `ActionType` values): `SubscriptionCreated`, `SubscriptionRenewed`, `SubscriptionExpired`, `SubscriptionRemoved`, `WebhookReceived`, `WebhookRejected`, `DeltaReconciliationRun`.
- `CloudSyncActivityResultCode` (new `ResultCode` values, alongside existing `ActionAuditResultCode`): rejection-reason codes for `WebhookRejected`, plus `success`/`failure` for the others (reuse `ActionAuditResultCode.Sent`/equivalent where semantically apt, otherwise add new constants).
- `CloudSyncActingFlags.NotApplicable`.

Test framework for new tests: MSTest + Moq + FluentAssertions, matching the actual established convention in `SchedulingWorkerAuditTests.cs` / `GraphSubscriptionManagerTests.cs` (per research §6 factual note), not the aspirational xUnit/NSubstitute wording in `.claude/rules/csharp.md`.

## Inputs / Outputs

- Inputs: `ActionAuditRecord` instances constructed by CloudSync components (`GraphSubscriptionManager`, `NotificationRequestProcessor`, `GraphDeltaReconciler`) via named-argument construction with the new optional fields above; no CLI flags or env vars introduced.
- Outputs: new rows in the existing `audit_log` table (no schema change); a new pure projection function's output is an in-memory Purview-activity-log-shaped record (no I/O, no network call — export/shipping is out of scope for this feature).
- Config keys and defaults: none new.
- Versioning or backward-compatibility constraints: `ActionAuditRecord` gains new optional (nullable/default-valued) positional parameters appended after the existing 13; all existing call sites use named arguments and are unaffected. `IActionAuditLog`'s two methods are unchanged.

## API / CLI Surface

No new CLI commands or HTTP endpoints. The Purview-activity-log projection is a pure, host-neutral mapping function (e.g., `PurviewActivityLogProjection.Project(ActionAuditRecord record) -> PurviewActivityLogRecord`) consumed only by tests and, potentially, a future export path (out of scope here).
- Contracts and validation rules: the projection must be total over all `ActionType`/`ResultCode` values currently in use (existing send/calendar values plus the new CloudSync values) and must not throw on any valid `ActionAuditRecord`.

## Data & State

- Data transformations and invariants: `audit_log` schema is unchanged; `MessageId`/`ActingFlags` remain `NOT NULL`/non-empty and are satisfied per decision 1 above for every new CloudSync event type. `CorrelationId` is always populated per decision 2/existing mechanisms (never empty for the new event types).
- Caching or persistence details: no new tables, no new caching; all new audit rows persist through the existing `CoreCacheRepository.AuditLog.cs` `RecordAsync` path.
- Migration or backfill requirements: none — no schema change, so no migration is needed.

## Constraints & Risks

- No live tenant: every tenant-dependent step (actual Purview ingestion, Graph activity log query) ships mocked; no Azure/Exchange/Purview credentials in this environment or CI.
- Additive only: must not alter send or calendar behavior paths, and must not change the existing F9 `IActionAuditLog` contract signature for current consumers (`SchedulingWorker.Audit.cs` and other existing callers).
- Must not alter F14 CloudSync subscription/webhook/reconciliation behavior; instrumentation is additive logging only.
- Correlation-id threading must be consistent with the F9 precedent so audit records remain joinable across the outbound-send and CloudSync activity domains.


## Implementation Strategy

- Implementation scope: (1) extend `ActionAuditRecord` with new optional fields/constants for CloudSync event types; (2) instrument `GraphSubscriptionManager` (create/renew/lifecycle), `NotificationRequestProcessor` (webhook received/rejected), and `GraphDeltaReconciler` (reconciliation outcome) to emit audit records additively; (3) add a new pure `PurviewActivityLogProjection` mapping; (4) add mocked-Graph/Purview contract tests; (5) author a human runbook + `human_interaction` exception for live-tenant Purview ingestion verification.
- New classes/functions to add: `CloudSyncActivityType` (const strings), `CloudSyncActivityResultCode` (const strings), `CloudSyncActingFlags.NotApplicable` (const string) in `src/OpenClaw.Core/Agent/Contracts/`; `PurviewActivityLogRecord` (record type) and `PurviewActivityLogProjection` (pure static mapper) in `src/OpenClaw.Core/Agent/Contracts/`.
- Dependency changes: none — all instrumentation uses the existing `IActionAuditLog` singleton already resolvable via DI; no new package.
- Logging/telemetry additions and locations: new `audit_log` rows only (see instrumentation table in research §2); no new logger calls required beyond what CloudSync components already emit, though `WebhookRejected` audit emission is new observability for previously silent (Warning-log-only) rejection paths.
- Rollout plan: no feature flag needed — additive audit-only change with no behavior change to subscription/webhook/delta-reconciliation outcomes; safe to ship directly once tests pass.

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
