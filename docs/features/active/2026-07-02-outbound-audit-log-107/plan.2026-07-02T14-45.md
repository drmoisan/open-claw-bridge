# outbound-audit-log - Plan

- **Issue:** #107
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T14-45
- **Status:** Ready for preflight
- **Version:** 1.0
- **Work Mode:** full-feature

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- C# Standards: `.claude/rules/csharp.md`
- Quality Tiers: `.claude/rules/quality-tiers.md` (`OpenClaw.Core` is T1)
- Architecture Boundaries: `.claude/rules/architecture-boundaries.md`
- Authoritative requirements: `docs/features/active/2026-07-02-outbound-audit-log-107/spec.md` (6 acceptance criteria, design decisions D1-D7); `issue.md`; `user-story.md`

**All work must comply with these policies; do not duplicate their content here.**

## Conventions Used in This Plan

- `<FEATURE>` = `docs/features/active/2026-07-02-outbound-audit-log-107`
- `<ts>` = ISO-8601 timestamp `yyyy-MM-ddTHH-mm` captured at task execution time.
- All evidence artifacts go to `<FEATURE>/evidence/<kind>/` (canonical scheme; non-overridable). Raw toolchain intermediates (TRX files, Cobertura XML, build logs) go to `artifacts/csharp/`; evidence artifacts summarize them with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` fields.
- CSharpier is the global tool (1.3.0): use `csharpier format .` / `csharpier check .` from the repo root. Do not use `dotnet csharpier` (no local tool manifest).
- Toolchain commands: `dotnet build OpenClaw.MailBridge.sln` (lint + nullable type-check via `TreatWarningsAsErrors`); `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (includes architecture-boundary tests in `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs`).
- Test stack: MSTest + FluentAssertions + Moq + CsCheck; in-memory shared-cache SQLite (`Mode=Memory;Cache=Shared`) per `tests/OpenClaw.Core.Tests/CoreCacheRepositorySentActionsTests.cs`; no temp files anywhere.
- Diff scope is Core-only: production changes are confined to `src/OpenClaw.Core/**`; tests to `tests/OpenClaw.Core.Tests/**`. No changes under `src/OpenClaw.HostAdapter*/**`, `src/OpenClaw.MailBridge*/**`, or any wire contract.

### Create-then-test vs. `[expect-fail]` rationale

- Phases 1-2 (contracts, repository partial) use create-then-test ordering: the test files reference `ActionAuditRecord`, `ActionAuditResultCode`, `IActionAuditLog`, and the `CoreCacheRepository` audit members, none of which exist in the baseline, so a fail-before test run is structurally impossible (the tests cannot compile). The repository tests are written and executed in the same phase as the implementation.
- Phase 4 worker emission behaviors get a true red-green cycle: after the contracts (Phase 1), the seam change (Phase 3), and the worker constructor/DI wiring (P4-T1..P4-T4) are in place, `SchedulingWorkerAuditTests.cs` compiles against existing types while the four emission calls are still absent from `SchedulingWorker.Pipeline.cs`. Those tests are run once as `[expect-fail]` with recorded evidence, then the emissions are implemented and the tests are re-run green.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture and Policy Compliance

- [x] [P0-T1] Read the repository policy files in the required order — `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/tonality.md` — and write `<FEATURE>/evidence/baseline/phase0-instructions-read.md`
  - Acceptance: artifact exists and contains `Timestamp:`, `Policy Order:` (the ordered list above), and the explicit list of files read
- [x] [P0-T2] Capture the baseline format state by running `csharpier check .` from the repo root and writing `<FEATURE>/evidence/baseline/csharpier-check.<ts>.md`
  - Acceptance: artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; expected baseline result is exit code 0 (clean tree); a non-zero baseline is recorded as-is, not fixed in Phase 0
- [x] [P0-T3] Capture the baseline lint/type-check state by running `dotnet build OpenClaw.MailBridge.sln` and writing `<FEATURE>/evidence/baseline/dotnet-build.<ts>.md`
  - Acceptance: artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (error/warning counts); expected exit code 0
- [x] [P0-T4] Capture the baseline test and coverage state by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, directing raw TRX/Cobertura intermediates under `artifacts/csharp/`, and writing `<FEATURE>/evidence/baseline/dotnet-test-coverage.<ts>.md`
  - Acceptance: artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` with pass/fail test counts plus numeric baseline line-coverage and branch-coverage percentages (overall and for `OpenClaw.Core`); placeholders such as `UNVERIFIED` are invalid

### Phase 1 — Audit Contracts in the Agent Layer

Create-then-test phase: these are new type declarations; executable behavior is tested in Phase 2 (see rationale section above).

- [x] [P1-T1] Create `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` — static class with `const string` members `Sent = "sent"`, `SendFailed = "send_failed"`, `DedupeSkipped = "dedupe_skipped"`, `SendDisabled = "send_disabled"` (spec D2), namespace `OpenClaw.Core.Agent`, file-scoped namespace, XML docs
  - Acceptance: file exists with exactly the four constants and values above
- [x] [P1-T2] Create `src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs` — positional `sealed record` with the 13 fields and exact ordering from the spec API section (`Mailbox`, `MessageId`, `EventId?`, `ActionType`, `ActingFlags`, `CorrelationId`, `ResultCode`, `ErrorDetail?`, `OriginalStartUtc?`, `OriginalEndUtc?`, `NewStartUtc?`, `NewEndUtc?`, `RecordedAtUtc`), namespace `OpenClaw.Core.Agent`, XML docs mapping fields to master §13 step 12 (spec D1)
  - Acceptance: file exists; record shape matches the spec signature field-for-field; no store-side logic in the record
- [x] [P1-T3] Create `src/OpenClaw.Core/Agent/Contracts/IActionAuditLog.cs` — interface with `Task RecordAsync(ActionAuditRecord record, CancellationToken ct)` and `Task<IReadOnlyList<ActionAuditRecord>> GetByMessageIdAsync(string messageId, CancellationToken ct)`, namespace `OpenClaw.Core.Agent`, XML docs including the D4 resilience-boundary note
  - Acceptance: file exists with exactly the two members from the spec API section
- [x] [P1-T4] Verify the contracts compile by running `dotnet build OpenClaw.MailBridge.sln`
  - Acceptance: exit code 0, zero errors and zero warnings

### Phase 2 — Persistence: `audit_log` Table and Repository Partial

Create-then-test phase (see rationale section); all tests use in-memory shared-cache SQLite, no temp files.

- [x] [P2-T1] Append the `audit_log` DDL and `idx_audit_log_message_id` index (exact SQL from the spec Data & State section) to `CreateTablesSql` in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, after the `series_moves` statement
  - Acceptance: `CreateTablesSql` contains `CREATE TABLE IF NOT EXISTS audit_log(...)` with all 14 columns and `CREATE INDEX IF NOT EXISTS idx_audit_log_message_id ON audit_log(message_id);`; file remains <= 500 lines
- [x] [P2-T2] Create `src/OpenClaw.Core/CoreCacheRepository.AuditLog.cs` — partial declaring `CoreCacheRepository : IActionAuditLog`, mirroring `CoreCacheRepository.SentActions.cs`: per-call connection via `Open()`, lazy once-per-instance `auditLogSchemaEnsured` guard whose DDL includes both the table and the index, `RecordAsync` with fail-fast `ArgumentException` guards for empty/whitespace `Mailbox`, `MessageId`, `ActionType`, `ActingFlags`, `CorrelationId`, `ResultCode` (no `ResultCode` closed-set validation, per spec), timestamps written as `value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)`, `GetByMessageIdAsync` reading back with `DateTimeStyles.RoundtripKind` and `ORDER BY recorded_at_utc DESC, id DESC`; no clock dependency anywhere in the partial
  - Acceptance: file exists, <= 500 lines, implements both interface members, contains no `TimeProvider`/`DateTime.Now`/`DateTime.UtcNow` usage
- [x] [P2-T3] Create `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogTests.cs` with round-trip and query tests: write records then read via `GetByMessageIdAsync` with all fields intact; ordering is most-recent-first with `id DESC` tie-break under identical timestamps; a record written with a non-UTC offset reads back as the equivalent UTC instant; restart survival — a second `CoreCacheRepository` instance on the same shared-cache connection string reads records written by the first
  - Acceptance: tests compile and pass; file <= 500 lines; Arrange-Act-Assert structure; no temp files
- [x] [P2-T4] Add migration and guard tests to `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogTests.cs`: double `InitializeAsync` is idempotent; pre-existing database without `audit_log` upgrades through `InitializeAsync`; lazy schema-ensure works without any `InitializeAsync` call; `RecordAsync` throws `ArgumentException` for each of the six required fields when empty or whitespace
  - Acceptance: tests compile and pass; file remains <= 500 lines (split a `CoreCacheRepositoryAuditLogMigrationTests.cs` sibling only if the cap would otherwise be exceeded)
- [x] [P2-T5] Create `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogPropertyTests.cs` — CsCheck property test: generated `ActionAuditRecord` values (including non-UTC offsets on every `DateTimeOffset` field and null/non-null combinations of all optional fields) survive the persistence round-trip unchanged after UTC normalization to round-trip (O) form (AC5)
  - Acceptance: property test compiles and passes; file <= 500 lines; generator covers null and non-null optionals and non-zero offsets
- [x] [P2-T6] Run the Phase 2 test classes via `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositoryAuditLog"` and write `<FEATURE>/evidence/regression-testing/repository-audit-tests.<ts>.md`
  - Acceptance: exit code 0, all filtered tests pass; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with pass counts

### Phase 3 — Correlation-Id Seam (`ISchedulingService.SendMailAsync` Signature Change)

Build-order gate (F6 lesson): the solution build runs only after every implementing and mocking file is updated. Moq setup expression trees cannot omit optional arguments (compiler error CS0854), so every existing two-argument `SendMailAsync` setup/verify breaks and must be updated in this phase before any build.

- [x] [P3-T1] Update `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`: change `SendMailAsync` to `Task SendMailAsync(SendMailRequest request, string? correlationId, CancellationToken ct)` with a `null` default on `correlationId` so existing production call sites compile unchanged (spec D5); update XML docs. Do not run a solution build yet
  - Acceptance: interface signature matches; `correlationId` documented as the worker-generated GUID forwarded as the HostAdapter request id
- [x] [P3-T2] Update `SendMailAsync` in `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` to accept `string? correlationId = null` and forward it as the `requestId` argument of `hostAdapterClient.SendMailAsync`; no other behavior change (failure-envelope handling unchanged). Do not run a solution build yet
  - Acceptance: `correlationId` is passed as `requestId`; a null `correlationId` preserves today's behavior (client self-generates the id); no HostAdapter/MailBridge/wire files touched
- [x] [P3-T3] Update every `SendMailAsync` call site, Moq `Setup`, and `Verify` in `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` to the three-parameter signature, and add a test asserting that a supplied `correlationId` is forwarded verbatim as the `requestId` argument to `IHostAdapterClient.SendMailAsync` and that a null `correlationId` forwards null
  - Acceptance: file compiles against the new signature; new forwarding test present; file <= 500 lines
- [x] [P3-T4] Update every Moq `Setup`/`Verify`/`Capture` of `ISchedulingService.SendMailAsync` in `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs`, `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs`, and `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerFallbackTests.cs` to the three-parameter signature using `It.IsAny<string?>()`; no assertion weakening
  - Acceptance: all three files reference only the three-parameter signature; test intent unchanged
- [x] [P3-T5] Gate: run `dotnet build OpenClaw.MailBridge.sln` only after P3-T1 through P3-T4 are complete
  - Acceptance: exit code 0, zero errors and zero warnings
- [x] [P3-T6] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~HostAdapterSchedulingServiceTests|FullyQualifiedName~SchedulingWorker"` to confirm the seam change did not regress existing worker/service behavior
  - Acceptance: exit code 0, all filtered tests pass

### Phase 4 — Worker Audit Emissions (Red-Green)

- [x] [P4-T1] Add an `IActionAuditLog actionAuditLog` parameter to the `SchedulingWorker` primary constructor in `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` (after `ISentActionStore sentActionStore`); update the class XML doc to mention the audit sink. Do not run a solution build yet
  - Acceptance: constructor parameter present; file <= 500 lines
- [x] [P4-T2] Update the private `Worker(...)` factory helpers in `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs`, `SchedulingWorkerDedupeTests.cs`, and `SchedulingWorkerFallbackTests.cs` to pass a `Mock<IActionAuditLog>` (loose) object for the new constructor parameter. Do not run a solution build yet
  - Acceptance: all three factories construct `SchedulingWorker` with the new parameter; existing test assertions unchanged
- [x] [P4-T3] Register the DI forward in `src/OpenClaw.Core/Program.cs`: `builder.Services.AddSingleton<IActionAuditLog>(sp => sp.GetRequiredService<CoreCacheRepository>());` adjacent to the existing `ISentActionStore`/`ISeriesMoveHistory` forwards (currently lines 65-68)
  - Acceptance: registration present beside the sibling forwards
- [x] [P4-T4] Gate: run `dotnet build OpenClaw.MailBridge.sln` only after P4-T1 through P4-T3 are complete, then run the existing worker test classes (`--filter "FullyQualifiedName~SchedulingWorker"`)
  - Acceptance: build exit code 0 with zero warnings; all existing worker tests pass
- [x] [P4-T5] Create `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs` — new partial containing: a pure static acting-flags builder returning `SendEnabled=<bool>;CalendarWriteEnabled=<bool>` from `AgentPolicyOptions` (spec D3); a record-builder helper composing `ActionAuditRecord` from mailbox/message/context/correlation-id/result-code/error-detail with `RecordedAtUtc` from `timeProvider.GetUtcNow()` and Stage 0 time columns null; and `WriteAuditSafelyAsync` exactly per spec D4 (single narrow `catch (Exception exception) when (exception is not OperationCanceledException)` that logs at Error with message id and result code and continues), with XML docs naming this the one sanctioned resilience boundary
  - Acceptance: file exists, <= 500 lines; the only catch is the D4 boundary; `OperationCanceledException` propagates
- [x] [P4-T6] [expect-fail] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerAuditTests.cs` (new file; existing worker test files are at ~330/335 lines and would approach the 500-line cap) covering, with a Moq-captured `IActionAuditLog`: (a) `send_disabled` record when `SendEnabled=false`; (b) `dedupe_skipped` record on a dedupe hit; (c) `sent` record after a successful send, written before `sentActionStore.RecordAsync`; (d) `send_failed` record with exception type+message in `ErrorDetail` when `SendMailAsync` throws, with the original exception still propagating to `ProcessMessageSafelyAsync`; (e) correlation id parses as a GUID and equals the `correlationId` value forwarded to `SendMailAsync`; (f) audit-failure resilience — `RecordAsync` throwing does not stop processing on the success path and does not replace the original exception on the failure path, and the failure is logged at Error; (g) `RecordedAtUtc` equals the `FakeTimeProvider` value. Run the new class and record the expected failure in `<FEATURE>/evidence/regression-testing/schedulingworker-audit-expect-fail.<ts>.md`
  - Acceptance: file compiles (all referenced types exist after P4-T5); the emission-behavior tests fail because `ProposeAndActAsync` does not yet emit; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), `Output Summary:` naming the failing tests; file <= 500 lines
- [x] [P4-T7] Implement the four emission points in `ProposeAndActAsync` in `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` (spec D5/D6): generate `Guid.NewGuid().ToString()` once at the top of the gated outbound-action block (before the `SendEnabled` check); write `send_disabled` in the `!options.SendEnabled` branch; write `dedupe_skipped` in the `IsRecordedAsync` hit branch; wrap the `SendMailAsync` call (now passing the correlation id) in a try/catch filtered `when (exception is not OperationCanceledException)` that writes `send_failed` with error detail and then rethrows via `throw;`; write `sent` immediately after the send returns and before `sentActionStore.RecordAsync`; all four writes go through `WriteAuditSafelyAsync`; early exits (hydration miss, triage gate, `NotSupportedException` deferral) emit nothing
  - Acceptance: exactly four emission call sites, all via `WriteAuditSafelyAsync`; `send_failed` write precedes `throw;`; file <= 500 lines
- [x] [P4-T8] Re-run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorker"` and record the pass-after result in `<FEATURE>/evidence/regression-testing/schedulingworker-audit-pass-after.<ts>.md`
  - Acceptance: exit code 0; all `SchedulingWorkerAuditTests` and pre-existing worker tests pass; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:`
- [x] [P4-T9] Verify the diff scope and file-size caps: `git diff --name-only` against the branch base shows only the enumerated files under `src/OpenClaw.Core/**`, `tests/OpenClaw.Core.Tests/**`, and `<FEATURE>/**`, with nothing under `src/OpenClaw.HostAdapter*/**` or `src/OpenClaw.MailBridge*/**`; every new/modified `.cs` file is <= 500 lines. Record the result in `<FEATURE>/evidence/other/diff-scope-check.<ts>.md`
  - Acceptance: artifact lists the changed files, confirms Core-only scope, and lists line counts for each new/modified `.cs` file

### Phase 5 — Final QA Loop and Coverage Comparison

Loop rule: run P5-T1 through P5-T3 in order; if any step fails or changes files, fix, then restart from P5-T1. Evidence artifacts record the final clean pass; `EXIT_CODE: SKIPPED` is not a valid outcome for any task in this phase.

- [x] [P5-T1] Format: run `csharpier format .` then `csharpier check .` from the repo root; write `<FEATURE>/evidence/qa-gates/final-csharpier.<ts>.md`
  - Acceptance: `csharpier check .` exits 0 on the final pass; artifact contains `Timestamp:`, `Command:` (both commands), `EXIT_CODE:`, `Output Summary:`, and notes whether `format` changed files (which triggers a loop restart)
- [x] [P5-T2] Lint + type-check: run `dotnet build OpenClaw.MailBridge.sln`; write `<FEATURE>/evidence/qa-gates/final-dotnet-build.<ts>.md`
  - Acceptance: exit code 0, zero errors, zero warnings; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
- [x] [P5-T3] Architecture + unit + property + contract tests with coverage: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, directing raw intermediates under `artifacts/csharp/`; write `<FEATURE>/evidence/qa-gates/final-dotnet-test-coverage.<ts>.md`
  - Acceptance: exit code 0, all tests pass; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` with numeric post-change line-coverage and branch-coverage percentages (overall and for `OpenClaw.Core`); placeholders are invalid
- [x] [P5-T4] Compare coverage: from the Phase 0 baseline artifact and the P5-T3 Cobertura output, compute baseline vs. post-change line/branch coverage and the coverage of the changed/new production files (`ActionAuditRecord.cs`, `ActionAuditResultCode.cs`, `CoreCacheRepository.AuditLog.cs`, `CoreCacheRepository.Schema.cs`, `SchedulingWorker.cs`, `SchedulingWorker.Audit.cs`, `SchedulingWorker.Pipeline.cs`, `HostAdapterSchedulingService.cs`, `Program.cs`; `IActionAuditLog.cs` and `ISchedulingService.cs` are interface-only and may be omitted per policy); write `<FEATURE>/evidence/qa-gates/coverage-comparison.<ts>.md`
  - Acceptance: artifact reports baseline coverage, post-change coverage, and new/changed-code coverage numerically; line >= 85% and branch >= 75% hold; no coverage regression on changed lines; if any threshold fails, the plan outcome is remediation-required, not PASS
- [x] [P5-T5] Verify acceptance-criteria mapping: map each of the six ACs in `<FEATURE>/spec.md` to the specific passing tests and evidence artifacts (AC1 → P2-T3/P2-T4 tests; AC2 → P4-T6(a-d)/P4-T8; AC3 → P4-T6(f); AC4 → P2-T2 clock-free check + P4-T6(e,g) + P3-T3 forwarding test; AC5 → P2-T5; AC6 → P5-T1..P5-T4); write `<FEATURE>/evidence/qa-gates/ac-verification.<ts>.md`
  - Acceptance: artifact lists all six ACs with test names and evidence paths; any unmapped AC blocks completion

## Test Plan

- Unit (repository): `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogTests.cs` — round-trip, query ordering (`recorded_at_utc DESC, id DESC`), restart survival, migration idempotency (double `InitializeAsync`, pre-existing-database upgrade, lazy schema-ensure), `ArgumentException` guards. In-memory shared-cache SQLite; no temp files.
- Unit (worker): `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerAuditTests.cs` — one record per decision point, ordering relative to `sentActionStore.RecordAsync`, `send_failed`-before-propagation, correlation-id GUID + forwarding, audit-failure resilience, `FakeTimeProvider`-sourced `RecordedAtUtc`.
- Unit (seam): `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — `correlationId` forwarded verbatim as `requestId`; null preserves self-generated behavior.
- Property (CsCheck): `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogPropertyTests.cs` — persistence round-trip with UTC normalization.
- Architecture: existing `AgentArchitectureBoundaryTests` run in every `dotnet test` invocation; no new boundaries introduced.
- Coverage evidence: baseline `<FEATURE>/evidence/baseline/dotnet-test-coverage.<ts>.md`; post-change `<FEATURE>/evidence/qa-gates/final-dotnet-test-coverage.<ts>.md`; comparison `<FEATURE>/evidence/qa-gates/coverage-comparison.<ts>.md`. Raw Cobertura/TRX under `artifacts/csharp/`.

## Open Questions / Notes

- Guard placement: per the spec API section ("The store does not validate `ResultCode` membership"), the non-empty guards live in `CoreCacheRepository.AuditLog.RecordAsync` (SeriesMoves guard style), not on the record type; the result-code closed set is enforced at the worker by construction (only `ActionAuditResultCode` constants are referenced).
- `correlationId` carries a `null` default on the interface and implementation so production call sites outside the worker compile unchanged; Moq setups still break at compile time (CS0854 — expression trees cannot omit optional arguments), which is why P3-T5 gates the build behind P3-T3/P3-T4.
- Log-verification approach for the D4 Error log in P4-T6(f): use a capturing `ILogger<SchedulingWorker>` test double (Moq `Mock<ILogger<SchedulingWorker>>` verifying `LogLevel.Error`), consistent with existing worker-test conventions.
- No retention/pruning, no query API, and no HostAdapter/MailBridge/wire changes — non-goals per the user story.
