# send-idempotency-dedupe - Plan

- **Issue:** #101
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T11-40
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature

## Required References

- General Coding Standards: [`.claude/rules/general-code-change.md`](../../../../.claude/rules/general-code-change.md)
- General Unit Test Policy: [`.claude/rules/general-unit-test.md`](../../../../.claude/rules/general-unit-test.md)
- C# Standards: [`.claude/rules/csharp.md`](../../../../.claude/rules/csharp.md)
- Architecture Boundaries: [`.claude/rules/architecture-boundaries.md`](../../../../.claude/rules/architecture-boundaries.md)
- Quality Tiers: [`.claude/rules/quality-tiers.md`](../../../../.claude/rules/quality-tiers.md)
- Authoritative requirements: `docs/features/active/2026-07-02-send-idempotency-dedupe-101/spec.md` (6 acceptance criteria), with `issue.md` and `user-story.md` as supporting context

**All work must comply with these policies; do not duplicate their content here.**

## Conventions Used Throughout This Plan

- `<FEATURE>` = `docs/features/active/2026-07-02-send-idempotency-dedupe-101`
- `<TS>` = execution-time ISO-8601 timestamp in `yyyy-MM-ddTHH-mm` form (fill in at execution)
- All evidence artifacts go under `<FEATURE>/evidence/<kind>/` per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Raw tool intermediates (TRX, Cobertura XML, TestResults directories) go under `artifacts/csharp/`; evidence artifacts summarize them and record `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- CSharpier is the global tool (1.3.0): use `csharpier format .` / `csharpier check .` (not `dotnet csharpier`; the repo has no local tool manifest).
- Test stack: MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`). No temp files in tests; use the in-memory shared-cache SQLite pattern from `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs`.
- Diff-scope confinement (production code): only `src/OpenClaw.Core/Agent/SentActionKey.cs` (new), `src/OpenClaw.Core/Agent/Contracts/ISentActionStore.cs` (new), `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs` (new), `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs`, `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`, `src/OpenClaw.Core/Program.cs`. No changes under `src/OpenClaw.HostAdapter/`, `src/OpenClaw.MailBridge/`, or any `*.Contracts` wire surface.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Policy Compliance

- [x] [P0-T1] Read repository policies in the required order: `CLAUDE.md`-loaded rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`
  - Acceptance: `<FEATURE>/evidence/baseline/phase0-instructions-read.md` exists with `Timestamp:`, `Policy Order:`, and the explicit list of files read
- [x] [P0-T2] Capture the formatting baseline by running `csharpier check .` from the repo root
  - Acceptance: `<FEATURE>/evidence/baseline/baseline-format.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail and any unformatted-file count)
- [x] [P0-T3] Capture the build/lint/type-check baseline by running `dotnet build OpenClaw.MailBridge.sln` from the repo root
  - Acceptance: `<FEATURE>/evidence/baseline/baseline-build.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts; analyzers and nullable analysis run in this step)
- [x] [P0-T4] Capture the test-and-coverage baseline by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-101` from the repo root
  - Acceptance: `<FEATURE>/evidence/baseline/baseline-test-coverage.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` containing pass/fail counts and the numeric baseline line-coverage and branch-coverage percentages read from the generated Cobertura report under `artifacts/csharp/baseline-101/` (no placeholders such as UNVERIFIED)

### Phase 1 — Pure Dedupe-Key Builder (SentActionKey)

- [x] [P1-T1] Create `src/OpenClaw.Core/Agent/SentActionKey.cs`: a pure static class with `public const string ProposalReply = "proposal-reply";` and `public static string Build(string mailbox, string messageId, string actionType)` returning `{mailbox}:{messageId}:{actionType}` joined with `:` in that fixed order; throw `ArgumentException` (naming the offending parameter) for any null, empty, or whitespace-only component; document in XML docs that components are not escaped and key distinctness is guaranteed only for colon-free components
  - Acceptance: file exists, compiles under `dotnet build OpenClaw.MailBridge.sln`, contains no clock or I/O dependency, and is under 500 lines
- [x] [P1-T2] Create `tests/OpenClaw.Core.Tests/Agent/SentActionKeyTests.cs` with MSTest + FluentAssertions unit tests: (a) `Build("owner@contoso.com", "msg-1", SentActionKey.ProposalReply)` returns `"owner@contoso.com:msg-1:proposal-reply"`; (b) `ProposalReply` equals `"proposal-reply"`; (c) `Build` throws `ArgumentException` for null, empty, and whitespace-only values of each of the three components (parameterized via `[DataRow]`)
  - Acceptance: file exists in the `tests/` tree (not colocated), each test has Arrange–Act–Assert structure and a descriptive name, and all tests in the file pass
- [x] [P1-T3] Create `tests/OpenClaw.Core.Tests/Agent/SentActionKeyPropertyTests.cs` with CsCheck property tests: (a) determinism — the same `(mailbox, messageId, actionType)` triple always yields the same key; (b) component ordering — the produced key, split on `:` for colon-free inputs, yields the components in `{mailbox}:{messageId}:{actionType}` order; (c) distinctness — distinct colon-free non-whitespace component triples yield distinct keys; generators must exclude null/empty/whitespace and colon-containing strings where the property requires it
  - Acceptance: file exists, uses CsCheck (already referenced by `OpenClaw.Core.Tests.csproj`), and all property tests pass with reproducible seeds on failure
- [x] [P1-T4] Verify Phase 1 by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SentActionKey"` from the repo root
  - Acceptance: exit code 0 with all `SentActionKey*` tests passing

### Phase 2 — Store Contract, Schema, and Repository Partial

- [x] [P2-T1] Create `src/OpenClaw.Core/Agent/Contracts/ISentActionStore.cs` declaring `public interface ISentActionStore` with `Task<bool> IsRecordedAsync(string dedupeKey, CancellationToken ct);` and `Task RecordAsync(string dedupeKey, DateTimeOffset recordedAtUtc, CancellationToken ct);`, with XML docs stating the caller supplies the timestamp (the store has no clock dependency) and that `RecordAsync` is idempotent
  - Acceptance: file exists alongside `ISchedulingService.cs`, compiles, interface-only (no executable behavior), under 500 lines
- [x] [P2-T2] Update `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` by appending the `sent_actions` DDL to `CreateTablesSql`: `CREATE TABLE IF NOT EXISTS sent_actions(dedupe_key TEXT PRIMARY KEY, mailbox TEXT NOT NULL, message_id TEXT NOT NULL, action_type TEXT NOT NULL, recorded_at_utc TEXT NOT NULL);`
  - Acceptance: DDL appended verbatim inside `CreateTablesSql`; no existing table, column, or migration statement modified; file remains under 500 lines
- [x] [P2-T3] Create `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs`: a new partial declaring `internal sealed partial class CoreCacheRepository : ISentActionStore` that implements `IsRecordedAsync` (exact-key `SELECT 1 FROM sent_actions WHERE dedupe_key = $key LIMIT 1`, parameterized) and `RecordAsync` (`INSERT INTO sent_actions(...) VALUES (...) ON CONFLICT(dedupe_key) DO NOTHING`, parameterized, storing the three components parsed from the key's fixed `{mailbox}:{messageId}:{actionType}` shape and `recorded_at_utc` as `recordedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)`), plus a lazy once-per-repository-instance schema-ensure guard that executes the `sent_actions` `CREATE TABLE IF NOT EXISTS` before the first store operation so the store is safe without a prior `InitializeAsync` call; follow the existing per-call `Open()`/parameterized-command pattern
  - Acceptance: file exists, compiles, contains no `DateTime.UtcNow`/`DateTime.Now`/`TimeProvider` usage (timestamp is caller-supplied), and is under 500 lines
- [x] [P2-T4] Create `tests/OpenClaw.Core.Tests/CoreCacheRepositorySentActionsTests.cs` (MSTest + FluentAssertions, in-memory shared-cache SQLite with anchor connections, no temp files) covering: (a) `IsRecordedAsync` returns false for an unknown key; (b) `RecordAsync` then `IsRecordedAsync` returns true; (c) duplicate `RecordAsync` for the same key does not throw and leaves one row; (d) `recorded_at_utc` round-trips the caller-supplied timestamp in ISO-8601 "O" form (read back via a direct query on the anchor connection); (e) migration idempotency — `InitializeAsync` twice does not throw and `sent_actions` exists; (f) pre-existing-database upgrade — seed a database with the current tables but without `sent_actions`, then `InitializeAsync` adds it; (g) lazy schema-ensure — store methods work on a fresh database with no prior `InitializeAsync` call
  - Acceptance: file exists in the `tests/` tree, all seven scenarios present as individually named tests, all pass, file under 500 lines
- [x] [P2-T5] Verify Phase 2 by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositorySentActions"` from the repo root
  - Acceptance: exit code 0 with all `CoreCacheRepositorySentActionsTests` passing

### Phase 3 — Worker Seam Wiring and Expect-Fail Dedupe Tests (Test-First)

- [x] [P3-T1] Update `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` by adding `ISentActionStore sentActionStore` to the primary constructor (after `ISchedulingService schedulingService`, before `ISchedulingCandidateSource candidateSource` or in a position consistent with existing ordering); make no pipeline behavior change in this task
  - Acceptance: constructor parameter added; no other production logic changed; file remains under 500 lines
- [x] [P3-T2] Update `src/OpenClaw.Core/Program.cs` by registering `builder.Services.AddSingleton<ISentActionStore>(sp => sp.GetRequiredService<CoreCacheRepository>());` adjacent to the existing D5/D6 singleton registrations (lines 62–66 area)
  - Acceptance: registration present; `dotnet build src/OpenClaw.Core/OpenClaw.Core.csproj` succeeds (the solution build is deferred to P3-T3, which updates the test project's `Worker(...)` helper to the new constructor shape); no other `Program.cs` change
- [x] [P3-T3] Update the `Worker(...)` helper in `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs` to construct and pass a default `Mock<ISentActionStore>` whose `IsRecordedAsync` returns `false`, so all existing worker tests compile and keep their current behavior
  - Acceptance: `dotnet build OpenClaw.MailBridge.sln` succeeds and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerTests"` passes with no existing assertion weakened; file remains under 500 lines
- [x] [P3-T4] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` following the `SchedulingWorkerTests.cs` mock conventions (Moq, `FakeTimeProvider`, `NullLogger`, `Options(sendEnabled: ...)`) with five tests: (a) skip-on-hit — mocked `ISentActionStore.IsRecordedAsync` returns true; `SendMailAsync` is never invoked and the cycle completes without throwing; (b) record-on-success — on a miss, `RecordAsync` is invoked exactly once with the key `SentActionKey.Build("owner@contoso.com", "msg-1", SentActionKey.ProposalReply)` and the `FakeTimeProvider` timestamp, and the send occurs before the record (verify via Moq callback sequencing); (c) no-record-on-failure — `IsRecordedAsync` is consulted once, `SendMailAsync` throws, `RecordAsync` is never invoked, and the next candidate is still processed; (d) kill-switch composition — with `SendEnabled=false`, neither `IsRecordedAsync` nor `RecordAsync` is invoked and the existing "SendEnabled is false" behavior is unchanged; (e) restart persistence — using a real `CoreCacheRepository` store over one shared in-memory SQLite database, two successive worker/store instance pairs process the same candidate and `SendMailAsync` is invoked exactly once in total
  - Acceptance: file exists in the `tests/` tree, compiles, contains all five tests with Arrange–Act–Assert structure and descriptive names, no temp files, file under 500 lines
- [x] [P3-T5] [expect-fail] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerDedupeTests"` before the pipeline change lands and record the fail-before evidence: tests (a) skip-on-hit, (b) record-on-success, (c) no-record-on-failure, and (e) restart persistence are expected to FAIL (the worker does not yet consult or write the store); test (d) kill-switch composition is expected to PASS (the store is untouched today)
  - Acceptance: `<FEATURE>/evidence/regression-testing/dedupe-expect-fail.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), and an `Output Summary:` listing each of the five tests with its observed outcome matching the expectations above

### Phase 4 — Pipeline Consult-Before-Send / Record-After-Success

- [x] [P4-T1] Update `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` inside the existing `SendEnabled`-gated `else` branch of `ProposeAndActAsync` (currently lines 120–132): build `var dedupeKey = SentActionKey.Build(MailboxUpn(), messageId, SentActionKey.ProposalReply);`; if `await sentActionStore.IsRecordedAsync(dedupeKey, cancellationToken)` is true, log one Information-level structured message with named template parameters `{MessageId}` and `{DedupeKey}` (matching the file's existing logging pattern) and return normally without sending; otherwise invoke `SendMailAsync` and, after it completes successfully, `await sentActionStore.RecordAsync(dedupeKey, timeProvider.GetUtcNow(), cancellationToken)`; a thrown send exception must propagate before the record step so `ProcessMessageSafelyAsync` isolation is unchanged; the `SendEnabled == false` path and its log line must be untouched
  - Acceptance: consult/record logic lives entirely inside the `SendEnabled` `else` branch; no `DateTime.UtcNow` added; file remains under 500 lines
- [x] [P4-T2] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerDedupeTests"` and record the pass-after evidence for the Phase 3 expect-fail tests
  - Acceptance: `<FEATURE>/evidence/regression-testing/dedupe-pass-after.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` showing all five dedupe tests passing
- [x] [P4-T3] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorker"` to confirm the pre-existing worker suite (kill switches, failure isolation, send composition) shows no regression alongside the new dedupe suite
  - Acceptance: exit code 0; all `SchedulingWorkerTests` and `SchedulingWorkerDedupeTests` tests pass with no assertion weakened

### Phase 5 — Final QA Loop, Coverage Comparison, and Scope Verification

Run P5-T1 through P5-T3 as the mandatory toolchain loop: if any step fails or changes files, fix and restart the loop from P5-T1 until all three complete cleanly in a single pass. `EXIT_CODE: SKIPPED` is not a valid outcome for any task in this phase.

- [x] [P5-T1] Run `csharpier format .` then `csharpier check .` from the repo root (formatting gate)
  - Acceptance: `<FEATURE>/evidence/qa-gates/final-format.<TS>.md` exists with `Timestamp:`, `Command:` (both commands), `EXIT_CODE: 0` for the check, and an `Output Summary:` stating whether `csharpier format` changed any files (a changed file restarts the loop)
- [x] [P5-T2] Run `dotnet build OpenClaw.MailBridge.sln` from the repo root (lint + nullable type-check gate via the analyzer stack with `TreatWarningsAsErrors=true`)
  - Acceptance: `<FEATURE>/evidence/qa-gates/final-build.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` (0 warnings, 0 errors)
- [x] [P5-T3] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final-101` from the repo root (full test gate including the architecture-boundary tests, unit tests, and property tests, in coverage mode)
  - Acceptance: `<FEATURE>/evidence/qa-gates/final-test-coverage.<TS>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` containing pass/fail counts and the numeric post-change line-coverage and branch-coverage percentages read from the Cobertura report under `artifacts/csharp/final-101/`
- [x] [P5-T4] Produce the coverage comparison: read the baseline Cobertura report (`artifacts/csharp/baseline-101/`) and the final report (`artifacts/csharp/final-101/`) and record (a) baseline line/branch coverage, (b) post-change line/branch coverage, (c) per-file line coverage for every changed/new production file (`SentActionKey.cs`, `CoreCacheRepository.SentActions.cs`, `SchedulingWorker.cs`, `SchedulingWorker.Pipeline.cs`, `CoreCacheRepository.Schema.cs`, `Program.cs`), and (d) a verdict that line coverage >= 85%, branch coverage >= 75%, and changed-line coverage did not regress; `ISentActionStore.cs` is interface-only and may be noted as legitimately excluded from executable coverage
  - Acceptance: `<FEATURE>/evidence/qa-gates/coverage-comparison.<TS>.md` exists with `Timestamp:`, both numeric coverage sets, per-file figures, and an explicit PASS/FAIL verdict per threshold; if any required numeric value is unavailable the task outcome is remediation-required, never PASS
- [x] [P5-T5] Verify scope and size invariants: (a) every production file in the diff is one of the seven files listed in the Conventions section (no HostAdapter, MailBridge, or Contracts-wire changes) via `git diff --name-only` against the branch base; (b) every new or modified production and test file is under 500 lines via a line count; (c) no test creates temp files (inspect the two new SQLite test files for the in-memory shared-cache pattern only)
  - Acceptance: `<FEATURE>/evidence/other/scope-and-size-verification.<TS>.md` exists with `Timestamp:`, the `git diff --name-only` file list, per-file line counts, and a PASS/FAIL verdict for each of (a)–(c)
- [x] [P5-T6] Update `<FEATURE>/spec.md`, `<FEATURE>/issue.md`, and `<FEATURE>/user-story.md` acceptance-criteria checkboxes to reflect verified outcomes, citing the evidence artifact path for each criterion
  - Acceptance: each of the six acceptance criteria is checked only where a named evidence artifact under `<FEATURE>/evidence/` supports it; no criterion is checked without cited evidence

## Test Plan

- Unit (pure): `tests/OpenClaw.Core.Tests/Agent/SentActionKeyTests.cs` (format, constant, `ArgumentException` per component) and `tests/OpenClaw.Core.Tests/Agent/SentActionKeyPropertyTests.cs` (CsCheck: determinism, component ordering, colon-free distinctness) — AC-2.
- Unit (repository): `tests/OpenClaw.Core.Tests/CoreCacheRepositorySentActionsTests.cs` (record/exists round-trip, duplicate-record idempotency, timestamp round-trip, `InitializeAsync` idempotency, pre-existing-database upgrade, lazy schema-ensure) — AC-1; in-memory shared-cache SQLite, no temp files.
- Unit (worker): `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` (skip-on-hit with structured log outcome, record-on-success with `FakeTimeProvider` timestamp and send-before-record ordering, no-record-on-failure with failure isolation, kill-switch composition, restart persistence over one shared database) — AC-3, AC-4, AC-5; existing `SchedulingWorkerTests.cs` guards against regression.
- Toolchain: `csharpier format .`/`csharpier check .` → `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable) → `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (includes `AgentArchitectureBoundaryTests`) — AC-6.
- Coverage evidence:
  - Baseline artifact: `<FEATURE>/evidence/baseline/baseline-test-coverage.<TS>.md` (raw: `artifacts/csharp/baseline-101/`)
  - Post-change artifact: `<FEATURE>/evidence/qa-gates/final-test-coverage.<TS>.md` (raw: `artifacts/csharp/final-101/`)
  - Comparison artifact: `<FEATURE>/evidence/qa-gates/coverage-comparison.<TS>.md`
- Fail-before/pass-after evidence: `<FEATURE>/evidence/regression-testing/dedupe-expect-fail.<TS>.md` (P3-T5) and `<FEATURE>/evidence/regression-testing/dedupe-pass-after.<TS>.md` (P4-T2).

## Open Questions / Notes

- `.claude/rules/csharp.md` names xUnit/NSubstitute as defaults, but the spec and the repository's actual test stack are MSTest + FluentAssertions + Moq (see `SchedulingWorkerTests.cs`, `CoreCacheRepositoryResponseStatusTests.cs`); this plan follows the established in-repo stack per the spec.
- `CoreCacheRepository` is `internal sealed`; the new partial implements the public `ISentActionStore` interface, and the DI registration in `Program.cs` resolves the same singleton instance, matching the spec's D5/D6 pattern.
- No README or configuration-doc changes are required: the feature adds no config keys, flags, routes, or CLI surface (spec "Inputs / Outputs").
- The at-least-once window (crash between send and record) is accepted for Stage 0 per the spec; `RecordAsync`'s conflict-tolerant insert covers the recovery path. No task addresses exactly-once delivery (non-goal).
- The dedupe key omits `{tenantId}` (local single-mailbox form); Stage 1 extends the builder rather than reinterpreting stored keys (spec "Constraints & Risks").
