# outbound-audit-log (Issue #107)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/outbound-audit-log/ (Issue #107)

- Issue: #107
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/107
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

The master specification requires structured, queryable audit records for every outbound action before rollout broadens (`docs/open-claw-approach.master.md` §7.2 "Structured application logs with mailbox, event, message, correlation ID, and action outcome" and §13 step 12: "Structured logs for: target mailbox, event ID, original times, proposed/new times, acting feature flag, correlation ID, result code"). Today the pipeline emits `ILogger` message-template logs and `ingest_runs` records operation outcomes, but there is no per-action audit record with the mandated fields as a queryable unit — a dedupe hit, a successful send, and a failed send leave no durable, structured audit row. Identified as gap F9 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Proposed Behavior

- Add an `audit_log` table to the Core cache (`CoreCacheRepository.AuditLog` partial, mirroring the sent_actions/series_moves pattern) capturing per action: mailbox, message id, event id (nullable), action type, acting feature flag(s), correlation/request id, result code (e.g. `sent`, `dedupe_skipped`, `send_failed`, `send_disabled`), error detail (nullable), original/proposed times (nullable, for future calendar writes), and recorded-at timestamp (caller-supplied; repository clock-free).
- Add an `IActionAuditLog` contract in `Agent/Contracts`; `SchedulingWorker` writes one audit record per outbound-action decision point: send attempted/succeeded, send failed, dedupe skip, kill-switch suppression.
- Queryable by message id (minimum); audit writes must never crash the pipeline (failure to audit is logged and swallowed with an explicit, narrow catch documented as a deliberate boundary).
- Stage 2 calendar writes (F18/F19) reuse the same contract for reschedule actions (original/new times columns already provisioned).

## Acceptance Criteria

- [x] `audit_log` table with idempotent migration (fresh-database DDL plus lazy schema-ensure on pre-existing databases); records round-trip and are queryable by message id, most recent first, and survive restart (a new repository instance on the same database reads records written by the previous instance).
- [x] One audit record per Stage 0 decision point with the master §13 step 12 fields: successful send (`sent`), failed send (`send_failed` with error detail, written before the exception propagates to `ProcessMessageSafelyAsync`), dedupe hit (`dedupe_skipped`), kill-switch suppression (`send_disabled`).
- [x] Audit-write failure does not break message processing: the worker logs the failure at Error and continues; an audit-write failure on the send-failure path does not mask the original send exception.
- [x] Repository clock-free (recorded-at supplied by the worker from `TimeProvider`); correlation id is a worker-generated GUID per outbound-action evaluation, forwarded to the HostAdapter send call as its request id through the existing `IHostAdapterClient.SendMailAsync` `requestId` parameter (the `ApiMeta` request id does not surface to the worker today; see spec Design Decision D5).
- [x] Property-based round-trip test (CsCheck): `ActionAuditRecord` fields survive persistence unchanged, with timestamps normalized to UTC round-trip (O) form.
- [x] Full C# toolchain passes (format → lint → type-check → architecture → test); line coverage >= 85% and branch coverage >= 75% hold with changed lines covered.

## Constraints & Risks

- Keep the audit sink out of the hot path's failure mode (never throw upward); this is the one sanctioned narrow catch-and-log.
- MSTest + FluentAssertions + Moq + CsCheck; no temp files; 500-line cap; pure/contract in Agent, persistence in a Core partial; no HostAdapter/MailBridge/wire changes.

## Test Conditions to Consider

- [ ] Unit: repository round-trip, migration idempotency, query-by-message-id ordering.
- [ ] Unit: worker emits the correct record per decision point (mocked store), audit-failure resilience.
- [ ] Property: record fields survive round-trip unchanged.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create active feature folder from the template
