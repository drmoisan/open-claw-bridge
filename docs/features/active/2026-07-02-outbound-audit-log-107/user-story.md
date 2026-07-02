# `outbound-audit-log` — User Story

- Issue: #107
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-02

## Story Statement

- As the operator of the scheduling assistant, I want every outbound-action decision (successful send, failed send, dedupe skip, kill-switch suppression) to write one durable structured audit record, so that I can answer "what did the assistant do for this message, and why" with a single query instead of reconstructing behavior from interleaved log lines.
- As a compliance reviewer, I want each audit record to carry the master-mandated fields (target mailbox, message id, event id, action type, acting feature flags, correlation id, result code, error detail, and provisioned original/new times), so that the audit-review precondition in master §13 step 12 / step 13 item 10 ("Broaden mailbox scope only after audit review") can actually be satisfied before rollout widens.
- As a developer investigating an outbound-mail incident, I want the audit record's correlation id to match the `X-Request-Id` the HostAdapter received for the send, so that I can join an audit row to the adapter's request logs without guesswork.

## Problem / Why

The master specification requires structured, queryable audit records for every outbound action before rollout broadens (`docs/open-claw-approach.master.md` §7.2 "Structured application logs with mailbox, event, message, correlation ID, and action outcome" and §13 step 12: "Structured logs for: target mailbox, event ID, original times, proposed/new times, acting feature flag, correlation ID, result code"). Today the pipeline emits `ILogger` message-template logs and `ingest_runs` records operation outcomes, but there is no per-action audit record with the mandated fields as a queryable unit — a dedupe hit, a successful send, and a failed send leave no durable, structured audit row. Identified as gap item 9 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Personas & Scenarios

- **Persona: Operations owner (runs the assistant against the pilot mailbox).**
  - Who: the person accountable for the assistant's day-to-day behavior in production.
  - Cares about: knowing exactly which messages triggered outbound mail and which were suppressed, without tailing logs.
  - Constraints: works from the Core cache database and service logs; no Graph activity logs or Purview exist at Stage 0.
  - Goals and frustrations: today a "did the assistant reply twice?" question requires correlating `ILogger` lines across restarts; log rotation can lose the answer entirely.
  - Context: kill switches (`SendEnabled`, `CalendarWriteEnabled`) are flipped during pilot operation, so "why did nothing go out" is a routine question.

- **Persona: Compliance reviewer (gatekeeper for rollout broadening).**
  - Who: reviews agent behavior before mailbox scope widens (master §13 step 13 item 10).
  - Cares about: a complete, immutable trail — every outbound action accounted for, including failures and suppressions, with the fields §13 step 12 mandates.
  - Constraints: needs queryable structured data, not prose logs; must be able to verify that suppressed periods show `send_disabled` records rather than gaps.
  - Goals: sign off that "audit review" is possible; frustration today is that there is nothing to review.

- **Scenario 1: Duplicate-send complaint.**
  - The operations owner receives a report that a requester got two proposal replies. They query `audit_log` by the message id. The result shows one `sent` record and one `dedupe_skipped` record, most recent first, each with mailbox, action type `proposal-reply`, acting flags, correlation id, and recorded-at timestamp. Outcome: the owner demonstrates the dedupe guard worked (the second evaluation skipped) and closes the report; if instead two `sent` rows appeared, the correlation ids give the exact adapter requests to investigate.

- **Scenario 2: Kill-switch suppression window.**
  - During an incident, the owner sets `SendEnabled=false`. Afterwards, the compliance reviewer asks what the assistant would have done. Querying by the affected message ids shows `send_disabled` records with `ActingFlags` = `SendEnabled=False;CalendarWriteEnabled=False` — positive evidence of suppression, not an absence of evidence. Outcome: the suppression window is documented per action.

- **Scenario 3: Failed send investigation.**
  - A send throws inside `SchedulingWorker`. The pipeline isolates the failure and moves on, but the audit trail retains a `send_failed` record with the exception detail and the correlation id that was forwarded to the HostAdapter as `X-Request-Id`. Outcome: the developer joins the audit row to the adapter's request log and finds the failing hop in minutes.

- **Scenario 4: Audit sink failure (obstacle case).**
  - The SQLite write fails (for example, disk contention). Message processing continues — sends are not blocked and the pipeline outcome is unchanged — and the audit failure itself is logged at Error with the message id and result code, so the gap in the trail is visible and attributable rather than silent.

## Acceptance Criteria

- [x] `audit_log` table with idempotent migration (fresh-database DDL plus lazy schema-ensure on pre-existing databases); records round-trip and are queryable by message id, most recent first, and survive restart (a new repository instance on the same database reads records written by the previous instance).
- [x] One audit record per Stage 0 decision point with the master §13 step 12 fields: successful send (`sent`), failed send (`send_failed` with error detail, written before the exception propagates to `ProcessMessageSafelyAsync`), dedupe hit (`dedupe_skipped`), kill-switch suppression (`send_disabled`).
- [x] Audit-write failure does not break message processing: the worker logs the failure at Error and continues; an audit-write failure on the send-failure path does not mask the original send exception.
- [x] Repository clock-free (recorded-at supplied by the worker from `TimeProvider`); correlation id is a worker-generated GUID per outbound-action evaluation, forwarded to the HostAdapter send call as its request id through the existing `IHostAdapterClient.SendMailAsync` `requestId` parameter (the `ApiMeta` request id does not surface to the worker today; see spec Design Decision D5).
- [x] Property-based round-trip test (CsCheck): `ActionAuditRecord` fields survive persistence unchanged, with timestamps normalized to UTC round-trip (O) form.
- [x] Full C# toolchain passes (format → lint → type-check → architecture → test); line coverage >= 85% and branch coverage >= 75% hold with changed lines covered.

## Non-Goals

- Graph activity logs, Azure Monitor streaming, or Microsoft Purview integration (master §7.2 Stage 1+; gap-analysis item 20).
- Reschedule/propose-new-time result codes or populated original/new time values — Stage 2 (F18/F19) adds those; this feature only provisions the columns and contract.
- Any query API, CLI, or UI over the audit log; Stage 0 consumption is direct SQLite queries.
- Retention, archival, or pruning of audit rows.
- Auditing of read-only pipeline stages (hydration, triage, slot proposal) or of early exits where no outbound action was evaluated (spec Design Decision D6).
- HostAdapter, MailBridge, or wire-protocol changes (the existing `requestId` parameter and `X-Request-Id` header are reused as-is).
