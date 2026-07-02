# outbound-audit-log — Spec

- **Issue:** #107
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02
- **Status:** Ready
- **Version:** 1.0

## Overview

The master specification requires structured, queryable audit records for every outbound action before rollout broadens (`docs/open-claw-approach.master.md` §7.2 "Structured application logs with mailbox, event, message, correlation ID, and action outcome" and §13 step 12: "Structured logs for: target mailbox, event ID, original times, proposed/new times, acting feature flag, correlation ID, result code"). Today the pipeline emits `ILogger` message-template logs and `ingest_runs` records operation outcomes, but there is no per-action audit record with the mandated fields as a queryable unit — a dedupe hit, a successful send, and a failed send leave no durable, structured audit row. Identified as gap item 9 ("Structured audit log for outbound actions") in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Behavior

- Add an `audit_log` table to the Core cache via a new `CoreCacheRepository.AuditLog` partial that mirrors the established persistence-partial pattern (`CoreCacheRepository.SentActions.cs`, `CoreCacheRepository.SeriesMoves.cs`): per-call connection, caller-supplied timestamps (clock-free), and a lazy once-per-instance schema-ensure guard.
- Add an `IActionAuditLog` contract and an immutable `ActionAuditRecord` model in `src/OpenClaw.Core/Agent/Contracts/` (namespace `OpenClaw.Core.Agent`, matching `ISentActionStore` / `ISeriesMoveHistory`).
- `SchedulingWorker` writes exactly one audit record at each of the four Stage 0 outbound-action decision points in `ProposeAndActAsync` (`src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`):
  1. **Kill-switch suppression** — the `!options.SendEnabled` branch (currently lines 183–189) writes `send_disabled`.
  2. **Dedupe hit** — the `sentActionStore.IsRecordedAsync` hit branch (currently lines 201–213) writes `dedupe_skipped`.
  3. **Send success** — after `schedulingService.SendMailAsync` returns (currently line 215–217), writes `sent`. The `sent` record is written immediately after the send completes and before `sentActionStore.RecordAsync`, so the audit trail reflects the actual outbound side effect even if the dedupe bookkeeping subsequently fails.
  4. **Send failure** — a catch around the `SendMailAsync` call (filtered `when (exception is not OperationCanceledException)`) writes `send_failed` with error detail, then rethrows the original exception so per-message isolation in `ProcessMessageSafelyAsync` (`SchedulingWorker.cs` lines 80–103) is unchanged.
- Audit records are queryable by message id (minimum), most recent first.
- Audit writes never crash the pipeline: all four emissions go through a single `WriteAuditSafelyAsync` helper whose narrow catch logs the failure at Error and continues (Design Decision D4).
- Stage 2 calendar writes (F18/F19) reuse the same contract and table for reschedule actions: the original/new time columns are provisioned now and remain null for Stage 0 send actions.

## Inputs / Outputs

- Inputs: no new CLI flags, env vars, or config keys. Existing `OpenClaw:AgentPolicy` options (`SendEnabled`, `CalendarWriteEnabled`) are read to populate the acting-flags field; existing `Storage.DbPath` locates the SQLite database.
- Outputs: rows in the new `audit_log` table of the Core cache database; `ILogger` Error entries when an audit write fails.
- Config keys and defaults: none added.
- Versioning / backward compatibility: schema change is additive (`CREATE TABLE IF NOT EXISTS` + `CREATE INDEX IF NOT EXISTS`); pre-existing databases upgrade through both `InitializeAsync` and the lazy schema-ensure guard. One in-repo contract change: `ISchedulingService.SendMailAsync` gains an optional `correlationId` parameter (Design Decision D5); all in-repo callers and test doubles are updated in the same change per `.claude/rules/general-code-change.md`.

## API / CLI Surface

No HTTP or CLI surface changes. New in-process contracts:

```csharp
// src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs
public sealed record ActionAuditRecord(
    string Mailbox,               // required, non-empty
    string MessageId,             // required, non-empty
    string? EventId,              // nullable (message-only pipeline runs)
    string ActionType,            // required, e.g. SentActionKey.ProposalReply
    string ActingFlags,           // required, "SendEnabled=<bool>;CalendarWriteEnabled=<bool>"
    string CorrelationId,         // required, worker-generated GUID string
    string ResultCode,            // required, see ActionAuditResultCode
    string? ErrorDetail,          // nullable; exception type + message for send_failed
    DateTimeOffset? OriginalStartUtc, // nullable; Stage 2 (F18/F19) reschedules
    DateTimeOffset? OriginalEndUtc,   // nullable; Stage 2
    DateTimeOffset? NewStartUtc,      // nullable; Stage 2
    DateTimeOffset? NewEndUtc,        // nullable; Stage 2
    DateTimeOffset RecordedAtUtc  // caller-supplied; store is clock-free
);

// src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs
public static class ActionAuditResultCode
{
    public const string Sent = "sent";
    public const string SendFailed = "send_failed";
    public const string DedupeSkipped = "dedupe_skipped";
    public const string SendDisabled = "send_disabled";
}

// src/OpenClaw.Core/Agent/Contracts/IActionAuditLog.cs
public interface IActionAuditLog
{
    Task RecordAsync(ActionAuditRecord record, CancellationToken ct);
    Task<IReadOnlyList<ActionAuditRecord>> GetByMessageIdAsync(string messageId, CancellationToken ct);
}
```

Validation rules (fail fast, `ArgumentException`, mirroring the `SeriesMoves` guard style): `Mailbox`, `MessageId`, `ActionType`, `ActingFlags`, `CorrelationId`, and `ResultCode` must be non-empty and non-whitespace. The store does **not** validate `ResultCode` membership in the Stage 0 closed set, so F18/F19 can add reschedule codes without a contract or schema change; the closed set is enforced at the worker (the only writer) by construction.

Modified contract:

```csharp
// src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs
Task SendMailAsync(SendMailRequest request, string? correlationId, CancellationToken ct);
```

`HostAdapterSchedulingService.SendMailAsync` forwards `correlationId` as the existing `requestId` argument of `IHostAdapterClient.SendMailAsync` (see D5). No HostAdapter, MailBridge, or wire changes: the `requestId` parameter and the `X-Request-Id` header already exist.

## Data & State

New table appended to `CreateTablesSql` in `CoreCacheRepository.Schema.cs` and mirrored in the partial's lazy schema-ensure DDL:

```sql
CREATE TABLE IF NOT EXISTS audit_log(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    mailbox TEXT NOT NULL,
    message_id TEXT NOT NULL,
    event_id TEXT NULL,
    action_type TEXT NOT NULL,
    acting_flags TEXT NOT NULL,
    correlation_id TEXT NOT NULL,
    result_code TEXT NOT NULL,
    error_detail TEXT NULL,
    original_start_utc TEXT NULL,
    original_end_utc TEXT NULL,
    new_start_utc TEXT NULL,
    new_end_utc TEXT NULL,
    recorded_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_audit_log_message_id ON audit_log(message_id);
```

- Data transformations and invariants:
  - Timestamps are stored as UTC in round-trip (`O`) form via `value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)`, identical to `sent_actions.recorded_at_utc` and `series_moves.*_utc`. Read-back parses with `DateTimeStyles.RoundtripKind`, so a record written with a non-UTC offset reads back as the equivalent UTC instant (invariant verified by the property test).
  - `GetByMessageIdAsync` orders `ORDER BY recorded_at_utc DESC, id DESC` — the `id` tie-break keeps ordering deterministic when a fixed test clock produces identical timestamps.
  - Rows are append-only; there is no update or delete surface in this feature.
- Caching / persistence: per-call `SqliteConnection` via the repository's existing `Open()` helper; lazy once-per-instance `auditLogSchemaEnsured` guard identical in shape to `sentActionsSchemaEnsured`.
- Migration / backfill: additive and idempotent only (`IF NOT EXISTS` DDL on both the `InitializeAsync` path and the lazy path). No backfill: actions performed before this feature have no audit rows, by design.

## Design Decisions

### D1 — Immutable record `ActionAuditRecord` with the master-mandated fields

A C# `record` in `Agent/Contracts` (namespace `OpenClaw.Core.Agent`) matching the existing contract conventions (`SchedulingEventDto`, `SendMailRequest` are positional records in the same folder). Field-by-field mapping to master §13 step 12: target mailbox → `Mailbox`; event ID → `EventId` (nullable, sourced from `NormalizedMeetingContext.EventId`, which is nullable because the pipeline supports message-only runs); original times → `OriginalStartUtc`/`OriginalEndUtc`; proposed/new times → `NewStartUtc`/`NewEndUtc`; acting feature flag → `ActingFlags`; correlation ID → `CorrelationId`; result code → `ResultCode`. `MessageId`, `ActionType`, `ErrorDetail`, and `RecordedAtUtc` complete §7.2's "mailbox, event, message, correlation ID, and action outcome". The original/new times are start/end **pairs** because a Stage 2 reschedule moves an interval, matching the `events` table's `start_utc`/`end_utc` shape; provisioning all four now avoids a Stage 2 migration.

### D2 — Result codes as a static class of string constants (not an enum)

`ActionAuditResultCode` holds `const string` members `sent`, `send_failed`, `dedupe_skipped`, `send_disabled`. Rationale, from observed code conventions: the repository already models closed string sets this way (`SentActionKey.ProposalReply` in `src/OpenClaw.Core/Agent/SentActionKey.cs`), and every persisted column in the cache is `TEXT` — strings round-trip to SQLite with no mapping layer. An enum was rejected because it would require serialization code, risk integer storage or rename-breaks against already-persisted rows, and force a contract change when F18/F19 add reschedule codes. Compile-time safety is preserved in practice because the worker (the only writer) references the constants, and tests assert against the same constants.

### D3 — Acting-flags snapshot format

`ActingFlags` is the deterministic string `SendEnabled=<bool>;CalendarWriteEnabled=<bool>`, built by a pure static helper from `AgentPolicyOptions`. The master mandates "acting feature flag"; recording both Stage 0 switches captures the acting flag and the suppression context in one self-describing value (a `send_disabled` row shows `SendEnabled=False` as its cause). A single-flag-name field was rejected because it loses the co-state of the other switch and would need a format change when calendar writes land.

### D4 — Resilience boundary: the one sanctioned narrow catch

All four emissions call one helper on the worker:

```csharp
private async Task WriteAuditSafelyAsync(ActionAuditRecord record, CancellationToken ct)
{
    try { await actionAuditLog.RecordAsync(record, ct).ConfigureAwait(false); }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.LogError(exception, "Audit write failed for message {MessageId} with result {ResultCode}; continuing.",
            record.MessageId, record.ResultCode);
    }
}
```

Why this deliberate swallow does not violate the fail-fast policy: `.claude/rules/csharp.md` permits a broad catch "at a defined boundary with added context", and this is that boundary — defined once, documented here and in XML docs, and scoped to a single call. The audit sink is an observability side channel; a SQLite fault in it must not take down message processing, suppress a completed send, or mask the original send exception on the `send_failed` path (the audit write happens inside the catch, and `throw;` rethrows the original). The failure is not silent: it is logged at Error with the message id and result code, satisfying "do not silently ignore errors" in `.claude/rules/general-code-change.md`. `OperationCanceledException` is excluded so shutdown still propagates.

### D5 — Correlation id source: worker-generated GUID, forwarded through the send seam

Evidence that no correlation id surfaces to the worker today:

- `ISchedulingService.SendMailAsync(SendMailRequest, CancellationToken)` returns `Task` (`ISchedulingService.cs` line 63) — no envelope reaches the worker.
- `HostAdapterSchedulingService.SendMailAsync` (lines 124–142) receives an `ApiEnvelope<object?>` whose `Meta.RequestId` it discards after checking `Ok`; on failure the request id is not embedded in the thrown `InvalidOperationException`.
- It calls `hostAdapterClient.SendMailAsync(wireRequest, cancellationToken: ct)` **without** a `requestId`, so `HostAdapterHttpClient.PostAsync` (lines 164–167) generates `Guid.NewGuid().ToString()` internally and sends it as `X-Request-Id`.
- Three of the four decision points (`send_disabled`, `dedupe_skipped`, and `send_failed` when no response arrives) involve no adapter request at all, so no adapter-issued id can exist for them.

Decision: the worker generates `Guid.NewGuid().ToString()` once per outbound-action evaluation, at the top of the gated block in `ProposeAndActAsync`, and stamps it on every audit record for that action. To make the id correlate beyond the audit table, `ISchedulingService.SendMailAsync` gains an optional `string? correlationId` parameter that `HostAdapterSchedulingService` forwards as the existing `requestId` argument — the audit row's correlation id then equals the `X-Request-Id` the HostAdapter logs for `sent`/`send_failed` actions. The alternative (worker-local GUID recorded only in the audit row, no seam change) was rejected because the id would correlate with nothing outside the table, failing the intent of §7.2. `Guid.NewGuid()` is not a banned API (`BannedSymbols.txt` bans `Random.Shared`, not GUID creation) and is the same mechanism `HostAdapterHttpClient` already uses; tests assert the captured value parses as a GUID rather than pinning a value.

### D6 — Exactly four Stage 0 emission points; `send_failed` written before propagation

The four emissions map one-to-one onto the existing branches of `ProposeAndActAsync` (see Behavior). Early pipeline exits — hydration miss, triage below `RequiresPriorityLayer`, and the `NotSupportedException` deferral for mailbox settings/free-busy (#74/#75) — are **not** audited: no outbound action was evaluated, and master §7.2/§13 scope the audit to outbound actions. The `send_failed` record is written inside the catch before `throw;`, so it is durable before the exception reaches `ProcessMessageSafelyAsync`. The `CalendarWriteEnabled` branch writes no record in Stage 0 because no calendar action type exists yet; F18/F19 add those emissions with the provisioned time columns.

### D7 — Stage 2 reuse

`IActionAuditLog`, the table, and the four time columns are the reuse surface for F18/F19: reschedule actions add new action types and result codes (constants appended to `ActionAuditResultCode`) and populate `original_*`/`new_*`; no schema migration or contract change is required (see D2 on non-enforcement of result-code membership at the store).

## Constraints & Risks

- Keep the audit sink out of the hot path's failure mode (never throw upward); this is the one sanctioned narrow catch-and-log (D4).
- MSTest + FluentAssertions + Moq + CsCheck; no temp files (in-memory shared-cache SQLite, as in `CoreCacheRepositorySentActionsTests.cs`); 500-line cap per file; pure/contract code in `Agent`, persistence in a Core partial; no HostAdapter/MailBridge/wire changes.
- Risk: the `ISchedulingService.SendMailAsync` signature change touches every existing worker test that mocks the seam (`SchedulingWorkerTests.cs`, `SchedulingWorkerDedupeTests.cs`); mitigated by updating Moq setups in the same change — a compile error, not a silent break.
- Risk: audit rows grow unboundedly. Accepted for Stage 0 (one row per outbound-action decision, low volume on a single test mailbox); retention is a non-goal recorded in the user story.
- Note: `.claude/rules/csharp.md` names xUnit + NSubstitute, but the actual repository test stack is MSTest + FluentAssertions + Moq + CsCheck (verified in `tests/OpenClaw.Core.Tests/`); this spec follows the code as found.

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - **New files:**
    - `src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs` — positional record + non-empty guards.
    - `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` — result-code constants.
    - `src/OpenClaw.Core/Agent/Contracts/IActionAuditLog.cs` — store contract.
    - `src/OpenClaw.Core/CoreCacheRepository.AuditLog.cs` — `IActionAuditLog` implementation partial (per-call connection, lazy schema-ensure, UTC `O`-format timestamps).
    - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs` — record builder + `WriteAuditSafelyAsync` in a new partial, keeping `SchedulingWorker.Pipeline.cs` (currently 258 lines) and the new file each well under the 500-line cap.
  - **Modified files:**
    - `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` — append `audit_log` DDL and index to `CreateTablesSql`.
    - `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` — optional `correlationId` parameter on `SendMailAsync` (D5).
    - `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — forward `correlationId` as `requestId`.
    - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` — add `IActionAuditLog actionAuditLog` primary-constructor parameter.
    - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` — four emission calls in `ProposeAndActAsync`.
    - `src/OpenClaw.Core/Program.cs` — register `builder.Services.AddSingleton<IActionAuditLog>(sp => sp.GetRequiredService<CoreCacheRepository>());` beside the existing `ISentActionStore`/`ISeriesMoveHistory` forwards (lines 65–68).
  - **Test files:**
    - `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogTests.cs` (new) — mirrors `CoreCacheRepositorySentActionsTests.cs` conventions.
    - `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogPropertyTests.cs` (new) — CsCheck round-trip property.
    - `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerAuditTests.cs` (new) — worker emission and resilience tests. New-file placement is justified by size: `SchedulingWorkerTests.cs` and `SchedulingWorkerDedupeTests.cs` already carry 330 and 335 non-blank lines respectively; adding the audit scenarios in place would approach the 500-line cap.
    - Existing worker tests updated for the new constructor parameter and `SendMailAsync` signature.
- Dependency changes: none. All packages (Microsoft.Data.Sqlite, MSTest, FluentAssertions, Moq, CsCheck) are already in use.
- Logging/telemetry: one new Error-level log in `WriteAuditSafelyAsync` (D4). Existing decision-point logs are unchanged.
- Rollout: no flag needed — the audit sink is passive and additive; the schema migrates idempotently on first run. Kill switches (`SendEnabled`, `CalendarWriteEnabled`) are inputs to the records, not gates on writing them.

## Acceptance Criteria

- [x] `audit_log` table with idempotent migration (fresh-database DDL plus lazy schema-ensure on pre-existing databases); records round-trip and are queryable by message id, most recent first, and survive restart (a new repository instance on the same database reads records written by the previous instance).
- [x] One audit record per Stage 0 decision point with the master §13 step 12 fields: successful send (`sent`), failed send (`send_failed` with error detail, written before the exception propagates to `ProcessMessageSafelyAsync`), dedupe hit (`dedupe_skipped`), kill-switch suppression (`send_disabled`).
- [x] Audit-write failure does not break message processing: the worker logs the failure at Error and continues; an audit-write failure on the send-failure path does not mask the original send exception.
- [x] Repository clock-free (recorded-at supplied by the worker from `TimeProvider`); correlation id is a worker-generated GUID per outbound-action evaluation, forwarded to the HostAdapter send call as its request id through the existing `IHostAdapterClient.SendMailAsync` `requestId` parameter (the `ApiMeta` request id does not surface to the worker today; see Design Decision D5).
- [x] Property-based round-trip test (CsCheck): `ActionAuditRecord` fields survive persistence unchanged, with timestamps normalized to UTC round-trip (O) form.
- [x] Full C# toolchain passes (format → lint → type-check → architecture → test); line coverage >= 85% and branch coverage >= 75% hold with changed lines covered.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Unit: repository round-trip, migration idempotency (double `InitializeAsync`, pre-existing-database upgrade, lazy schema-ensure without `InitializeAsync`), query-by-message-id ordering (`recorded_at_utc DESC, id DESC`), non-empty-field guards throw `ArgumentException`.
- [ ] Unit: worker emits the correct record per decision point (Moq-mocked `IActionAuditLog`, capturing the record): `send_disabled` when `SendEnabled=false`; `dedupe_skipped` on a dedupe hit; `sent` after a successful send (and before the dedupe-store record); `send_failed` with error detail when `SendMailAsync` throws, with the original exception still propagating; correlation id parses as a GUID and matches the value forwarded to `SendMailAsync`.
- [ ] Unit: audit-failure resilience — `RecordAsync` throwing does not stop message processing on the success path, and does not replace the original exception on the failure path; failure logged at Error.
- [ ] Property (CsCheck): generated `ActionAuditRecord` values (including non-UTC offsets and null optional fields) survive the persistence round-trip unchanged after UTC normalization.
