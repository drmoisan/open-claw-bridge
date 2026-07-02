# send-idempotency-dedupe — Spec

- **Issue:** #101
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T11-40
- **Status:** Draft
- **Version:** 0.2

## Overview

With `HostAdapterSchedulingService.SendMailAsync` now wired (issue #99 / PR #100), a retried or repeated scheduling cycle can resend the same reply: no dedupe-key concept exists in `SchedulingWorker`'s pipeline, `MailRoutes`, or `SendMailRpcHandler`. The master specification requires idempotency keys for mail sends with an internal dedupe key shaped like `{tenantId}:{mailboxUpn}:{messageId}:{actionType}` (`docs/open-claw-approach.master.md` §6.3); the Local MVP equivalent is a persisted per-message/action guard consulted before send. This was recorded as the accepted interim risk in the #99 spec, to be closed by this feature. Identified as gap F6 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Behavior

- Add a persisted `sent_actions` unit to `CoreCacheRepository` (new table via the established guarded-migration pattern) keyed by a deterministic dedupe key: `{mailbox}:{messageId}:{actionType}` (local single-mailbox form of the master's key; the `{tenantId}` component is deferred to Stage 1).
- `SchedulingWorker.ProposeAndActAsync` consults the store before invoking `ISchedulingService.SendMailAsync`. The consult/record logic lives entirely inside the existing `SendEnabled`-gated `else` branch (`SchedulingWorker.Pipeline.cs`, currently lines 120–132), so the kill switch remains the outer boundary:
  - **Dedupe hit:** the key is already recorded. The send is skipped, a structured dedupe-hit message is logged (`LogInformation` with named template parameters `{MessageId}` and `{DedupeKey}`, matching the file's existing logging pattern), and the message completes as a normal — not failed — processing outcome (the method returns normally).
  - **Dedupe miss:** `SendMailAsync` is invoked; on success the key is recorded in the same cycle with a timestamp taken from the injected `TimeProvider` (`timeProvider.GetUtcNow()`).
  - **Failed send:** `SendMailAsync` throws before the record step runs, so nothing is recorded and retry remains possible. The exception propagates to the existing `ProcessMessageSafelyAsync` isolation boundary in `SchedulingWorker.cs`, which logs and continues with the next candidate — unchanged behavior.
- Dedupe state survives process restart (persisted in the Core SQLite cache, not in-memory).
- When `SendEnabled` is false, the store is neither consulted nor written; the existing "SendEnabled is false" log line is unchanged.

### Dedupe key

- Format: `{mailbox}:{messageId}:{actionType}`, joined with `:` in that fixed order.
- `mailbox` — the worker's `MailboxUpn()` value (derived from `AgentPolicyOptions.InternalDomain`, e.g. `owner@contoso.com`).
- `messageId` — the candidate message identifier already threaded through `ProposeAndActAsync`.
- `actionType` — a fixed constant for the only Stage 0 outbound action: `proposal-reply`, exposed as a constant on the key builder.
- The builder is a pure static class (`SentActionKey` in `OpenClaw.Core.Agent`, file `src/OpenClaw.Core/Agent/SentActionKey.cs`) with a single `Build(string mailbox, string messageId, string actionType)` method. It throws `ArgumentException` for null, empty, or whitespace-only components (fail fast per `.claude/rules/general-code-change.md`).
- Ambiguity note: components are not escaped. In practice `mailbox` is a UPN and `actionType` is a fixed constant, neither of which contains `:`; message identifiers are bridge EntryID strings (hex). Key distinctness therefore holds for all real inputs; the property test asserts distinctness for colon-free inputs and this limitation is documented on the builder.

## Inputs / Outputs

- Inputs (CLI flags, files, env vars): none new. Existing configuration is reused: `OpenClaw:AgentPolicy:SendEnabled` (kill switch, unchanged semantics) and `OpenClaw:Storage:DbPath` (location of the Core SQLite cache that now also holds `sent_actions`).
- Outputs (artifacts, logs, telemetry):
  - New rows in the `sent_actions` table of the Core cache database (one per successful send).
  - New structured log line (Information) on a dedupe hit: message template with `{MessageId}` and `{DedupeKey}` named parameters.
- Config keys and defaults: no new keys; no default changes.
- Versioning or backward-compatibility constraints: the migration is additive (new table only). Existing databases upgrade in place via `CREATE TABLE IF NOT EXISTS`; no existing table, column, or row is modified. No wire-contract (`OpenClaw.MailBridge.Contracts` / HostAdapter HTTP surface) change.

## API / CLI Surface

No HTTP route, CLI command, or wire-contract change. The new surface is internal to `OpenClaw.Core`:

- `ISentActionStore` (new interface, `src/OpenClaw.Core/Agent/Contracts/ISentActionStore.cs`, alongside `ISchedulingService`):
  ```csharp
  public interface ISentActionStore
  {
      Task<bool> IsRecordedAsync(string dedupeKey, CancellationToken ct);
      Task RecordAsync(string dedupeKey, DateTimeOffset recordedAtUtc, CancellationToken ct);
  }
  ```
  The worker depends on this abstraction so unit tests mock it with Moq, matching the existing `ISchedulingService`/`ISchedulingCandidateSource` conventions in `SchedulingWorkerTests.cs`. The store takes the timestamp as a parameter — the caller supplies `timeProvider.GetUtcNow()` — so the repository gains no clock dependency and the timestamp is deterministic under `FakeTimeProvider`.
- `SentActionKey` (new pure static builder, see Behavior): `SentActionKey.Build(mailbox, messageId, actionType)` and the `SentActionKey.ProposalReply` action-type constant.
- Contracts and validation rules: `Build` rejects null/empty/whitespace components with `ArgumentException`. `RecordAsync` is idempotent (`INSERT ... ON CONFLICT DO NOTHING`), so a duplicate record during the accepted at-least-once window does not throw.

## Data & State

- Data transformations and invariants:
  - Key construction is a pure transformation of `(mailbox, messageId, actionType)`; same inputs always produce the same key.
  - Invariant: a key present in `sent_actions` means the corresponding send completed successfully at least once; the worker never sends again for that key.
- Caching or persistence details — new table in the Core SQLite cache:
  ```sql
  CREATE TABLE IF NOT EXISTS sent_actions(
      dedupe_key TEXT PRIMARY KEY,
      mailbox TEXT NOT NULL,
      message_id TEXT NOT NULL,
      action_type TEXT NOT NULL,
      recorded_at_utc TEXT NOT NULL
  );
  ```
  - `dedupe_key` is the primary key (exact-match lookup). The component columns (`mailbox`, `message_id`, `action_type`) are stored redundantly for audit/debug queries, following the repository's habit of readable columns.
  - `recorded_at_utc` is stored as an ISO-8601 round-trip string (`UtcDateTime.ToString("O")`), matching every other timestamp column in the repository (`ToDbValue` convention).
  - Implementation lives in a new partial, `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs`, which declares `CoreCacheRepository : ISentActionStore` and follows the existing per-call `Open()`/parameterized-command pattern. This keeps `CoreCacheRepository.cs` (271 lines) and `CoreCacheRepository.Schema.cs` (253 lines) under the 500-line cap.
- Migration or backfill requirements:
  - The DDL above is appended to `CreateTablesSql` in `CoreCacheRepository.Schema.cs`. Because it is `CREATE TABLE IF NOT EXISTS`, the same statement covers both the fresh-database path and the pre-existing-database upgrade path idempotently — the same guarantee the PRAGMA-guarded ALTER pattern provides for column additions. No backfill: an empty table is the correct initial state (nothing has been dedupe-recorded yet).
  - Startup-ordering guard: `CoreCacheRepository.InitializeAsync` is currently awaited only inside `MessagePollingWorker`/`CalendarPollingWorker` `ExecuteAsync`, which run concurrently with `SchedulingWorker`. To remove the ordering dependency, the `SentActions` partial ensures its own schema before first use (a lazy, once-per-repository-instance `CREATE TABLE IF NOT EXISTS sent_actions...` execution guarded by an initialization flag). This is cheap, idempotent, and keeps the store safe regardless of which hosted service touches the database first.

## Constraints & Risks

- Keep the store consult/record inside the existing `SendEnabled`-gated block so the kill switch remains the outer boundary. When `SendEnabled` is false the store is not touched.
- No temp files in tests (in-memory shared-cache SQLite pattern, as in `CoreCacheRepositoryResponseStatusTests.cs`); MSTest + FluentAssertions + Moq (the repository's actual test stack); CsCheck for property tests (already referenced by `OpenClaw.Core.Tests.csproj`); 500-line cap (new table logic goes in the new `CoreCacheRepository.SentActions.cs` partial; new worker dedupe tests go in a new test file rather than growing `SchedulingWorkerTests.cs`, currently 311 lines).
- Recording after send introduces a small at-least-once window (crash between send and record could resend once on restart). Accepted for Stage 0, consistent with the master's queue-retry semantics (§6.3); `RecordAsync`'s conflict-tolerant insert makes the recovery path safe. Exactly-once delivery is a non-goal.
- Deterministic clock: the recorded timestamp comes from the worker's injected `TimeProvider`; production code adds no `DateTime.UtcNow` call (banned API per `.claude/rules/csharp.md`).
- No HostAdapter, MailBridge, or Contracts-wire changes. Dedupe is enforced at the worker call site only; `SendMailRpcHandler`/`MailRoutes` remain dedupe-unaware in Stage 0.
- Key-shape divergence from the master (§6.3): the local key omits `{tenantId}`. Recorded explicitly so Stage 1 (multi-tenant/Graph) knows to extend the builder rather than reinterpret stored keys.

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - `src/OpenClaw.Core/Agent/SentActionKey.cs` — new pure static key builder + `ProposalReply` constant.
  - `src/OpenClaw.Core/Agent/Contracts/ISentActionStore.cs` — new interface (see API surface).
  - `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` — append `sent_actions` DDL to `CreateTablesSql`.
  - `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs` — new partial implementing `ISentActionStore` (`IsRecordedAsync` exact-key `SELECT 1 ... LIMIT 1`; `RecordAsync` `INSERT ... ON CONFLICT(dedupe_key) DO NOTHING`), plus the lazy schema-ensure guard.
  - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` — add `ISentActionStore sentActionStore` to the primary constructor.
  - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` — consult/skip/record logic inside the `SendEnabled` `else` branch of `ProposeAndActAsync` (file is 169 lines; stays well under cap).
  - `src/OpenClaw.Core/Program.cs` — register `builder.Services.AddSingleton<ISentActionStore>(sp => sp.GetRequiredService<CoreCacheRepository>());` next to the existing D5/D6 registrations (both are singletons, so the store shares the repository instance and connection string).
- New classes/functions/commands to add or update: listed above; no removals, no public wire-surface changes.
- Dependency changes (new/removed packages) and rationale: none. `Microsoft.Data.Sqlite`, Moq, FluentAssertions, CsCheck, and `Microsoft.Extensions.TimeProvider.Testing` are already in use.
- Logging/telemetry additions and locations: one new Information-level structured log line (dedupe hit) in `SchedulingWorker.Pipeline.cs`, using the file's existing message-template style. No new log line on the record step (the send itself is the observable action).
- Rollout plan (feature flags, staged deploys, fallback path): no new flag. The existing `SendEnabled` kill switch remains the outer gate; with sends disabled the feature is inert. Fallback: deleting rows from `sent_actions` (or the dev database) re-enables sending for affected keys; the additive migration requires no rollback script.

## Acceptance Criteria

- [x] A `sent_actions` table exists in the Core cache with an idempotent guarded migration (fresh-database and pre-existing-database upgrade paths both covered by tests; running the migration twice is safe).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md` (all `CoreCacheRepositorySentActionsTests` pass, including `InitializeAsync_twice_...`, `InitializeAsync_should_add_sent_actions_to_pre_existing_database`, and the lazy schema-ensure test)
- [x] A pure, deterministic dedupe-key builder produces `{mailbox}:{messageId}:{actionType}` and is unit-tested, including at least one CsCheck property test per T1 convention (determinism, component ordering, and distinctness for colon-free inputs).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md` (`SentActionKeyTests` 11 results + `SentActionKeyPropertyTests` 3 CsCheck properties, all passing; `SentActionKey.cs` 100% line/branch coverage per `evidence/qa-gates/coverage-comparison.2026-07-02T12-16.md`)
- [x] Inside the existing `SendEnabled`-gated block, `SchedulingWorker` consults the store before sending: on a hit it skips the send and logs a structured dedupe-hit message (message id and dedupe key as named template parameters), completing the message as a normal (not failed) outcome; on a miss it sends and then records the key with a timestamp from the injected `TimeProvider`.
  - Evidence: `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (skip-on-hit and record-on-success tests pass; fail-before counterpart `evidence/regression-testing/dedupe-expect-fail.2026-07-02T12-10.md`)
- [x] A repeated worker cycle for the same candidate message does not invoke `ISchedulingService.SendMailAsync` a second time, including across a simulated restart (new worker and store instances over the same in-memory shared-cache SQLite database).
  - Evidence: `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (`RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal` passes)
- [x] A failed send does NOT record the key (retry remains possible), and the failure stays isolated to that message per the existing `ProcessMessageSafelyAsync` behavior.
  - Evidence: `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (`RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate` passes; existing `SchedulingWorkerTests` unregressed per P4-T3 run recorded in the plan)
- [x] Full C# toolchain passes (format, lint, type-check, architecture, tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; MSTest + FluentAssertions + Moq; no temp files in tests; all files remain under the 500-line cap.
  - Evidence: `evidence/qa-gates/final-format.2026-07-02T12-12.md`, `evidence/qa-gates/final-build.2026-07-02T12-13.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T12-16.md`, `evidence/other/scope-and-size-verification.2026-07-02T12-18.md`

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Unit: dedupe-key builder determinism and component ordering (`tests/OpenClaw.Core.Tests/Agent/SentActionKeyTests.cs`); CsCheck property tests — same inputs yield the same key, output preserves `{mailbox}:{messageId}:{actionType}` ordering, distinct colon-free component triples yield distinct keys (`tests/OpenClaw.Core.Tests/Agent/SentActionKeyPropertyTests.cs`); `ArgumentException` for null/empty/whitespace components.
- [ ] Unit: repository record/exists round-trip (unknown key reads false; recorded key reads true; duplicate `RecordAsync` does not throw; `recorded_at_utc` round-trips the supplied timestamp), migration idempotency (`InitializeAsync` twice; pre-existing database created without `sent_actions` then upgraded), and lazy schema-ensure (store methods work without a prior `InitializeAsync` call) — `tests/OpenClaw.Core.Tests/CoreCacheRepositorySentActionsTests.cs`, in-memory shared-cache SQLite, no temp files.
- [ ] Unit: worker skip-on-hit (mocked `ISentActionStore` returns true; `SendMailAsync` never invoked; cycle completes without throwing), record-on-success (`RecordAsync` invoked once with the built key and the `FakeTimeProvider` timestamp, after the send), no-record-on-failure (`SendMailAsync` throws; `RecordAsync` never invoked; next candidate still processed), kill-switch composition (`SendEnabled=false`: store neither consulted nor written), and restart persistence (real store over one shared in-memory database, two worker/store instances, second cycle performs zero sends) — new file `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` following `SchedulingWorkerTests.cs` mock conventions.
