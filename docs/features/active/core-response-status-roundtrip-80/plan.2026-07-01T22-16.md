# core-response-status-roundtrip (Plan)

- **Issue:** #80
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-01T22-16
- **Status:** Draft (pending preflight)
- **Version:** 1.0
- **Work Mode:** full-bug (persisted marker in `docs/features/active/core-response-status-roundtrip-80/issue.md`)
- **AC Source:** `docs/features/active/core-response-status-roundtrip-80/spec.md` `## Acceptance Criteria` (identical to `issue.md` `## Acceptance Criteria`; AC-1 through AC-5 referenced below in list order)

**Fail-closed evidence rule:** Include explicit baseline artifact tasks, final-QA artifact tasks, and coverage-comparison tasks for each in-scope language when policy requires coverage. If any required baseline artifact, QA artifact, or coverage-comparison artifact is missing, the audit verdict must be BLOCKED or INCOMPLETE, never PASS.

**Evidence accounting rule:** Record the expected artifact path or location in each evidence-producing task. Do not mark evidence-backed work complete without the artifact.

## Scope and Constraints

- Bugfix workflow: regression test first (must fail before the fix), then minimal targeted fix. No opportunistic refactors.
- In-scope language: C# only.
- Production changes limited to `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` and `src/OpenClaw.Core/CoreCacheRepository.Events.cs`. New test file: `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs`. No other production or test file may change.
- Reference implementation to mirror: `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` (guarded `response_status` ALTER, lines 43-46) and `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs` (round-trip regression test shape).
- The Core upsert has exactly five write/read touchpoints for the new column; Phase 2 enumerates each as its own task so none is missed: (1) INSERT column list, (2) VALUES list, (3) `ON CONFLICT ... DO UPDATE SET` clause, (4) `AddEventParameters` binding, (5) `ReadEvent` materialization.
- Migration must be idempotent: fresh-database DDL path (`CreateTablesSql`) and existing-database guarded `ALTER TABLE events ADD COLUMN response_status INTEGER NULL` upgrade path are both covered by tasks and tests.
- Tests use the in-memory shared-cache SQLite pattern from `tests/OpenClaw.Core.Tests/CoreCacheRepositoryGraphFieldsTests.cs` (`Data Source=<unique>;Mode=Memory;Cache=Shared`); temporary files in tests are prohibited.
- Every touched file stays under the 500-line cap (`CoreCacheRepository.Schema.cs` currently 241 lines; `CoreCacheRepository.Events.cs` currently 259 lines).
- Test framework: MSTest + FluentAssertions, matching the existing `tests/OpenClaw.Core.Tests/` suite convention.

## Toolchain Command Forms (repo-verified)

- No `.config/dotnet-tools.json` (local tool manifest) exists in this repository, so `dotnet tool restore` has nothing to restore and `dotnet csharpier` is not available. CSharpier is invoked via the global subcommand form `csharpier check .` / `csharpier format .` (CSharpier 1.x). The delegation prompt's `csharpier .` and the issue's `dotnet csharpier check .` both resolve to these commands.
- Build (lint via Roslyn analyzers + nullable type checking, warnings as errors per `Directory.Build.props`): `dotnet build OpenClaw.MailBridge.sln`.
- Test with coverage (coverlet cobertura, `[*.Tests]*` excluded per `mailbridge.runsettings`): `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Architecture-boundary tests, where present in the solution, run as part of this test invocation.
- Raw cobertura outputs land under `tests/<project>/TestResults/<guid>/coverage.cobertura.xml`; intermediate raw copies may be placed under `artifacts/csharp/`. Evidence artifacts (the durable record) go only to `docs/features/active/core-response-status-roundtrip-80/evidence/<kind>/` per `evidence-and-timestamp-conventions`.

## Evidence Locations (canonical, non-overridable)

- Baseline: `docs/features/active/core-response-status-roundtrip-80/evidence/baseline/`
- Regression testing (fail-before / pass-after): `docs/features/active/core-response-status-roundtrip-80/evidence/regression-testing/`
- Final QA gates: `docs/features/active/core-response-status-roundtrip-80/evidence/qa-gates/`
- Coverage baseline/comparison: `docs/features/active/core-response-status-roundtrip-80/evidence/coverage/`
- Issue update mirrors: `docs/features/active/core-response-status-roundtrip-80/evidence/issue-updates/`

Every command-step artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`. Artifact filenames use the plan-cycle timestamp `2026-07-01T22-16` for deterministic reconciliation.

### Phase 0 — Policy Reading and Baseline Capture

- [x] [P0-T1] Read the repository policy files in the required order: `CLAUDE.md`-loaded standing rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`. Write `docs/features/active/core-response-status-roundtrip-80/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read.
  - Acceptance: artifact exists with all three fields and lists every file above.
- [x] [P0-T2] Record the git baseline: run `git rev-parse --abbrev-ref HEAD` and `git rev-parse HEAD`, and confirm a clean working tree with `git status --porcelain`. Write `docs/features/active/core-response-status-roundtrip-80/evidence/baseline/baseline-git.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (branch name + commit SHA + clean/dirty state).
  - Acceptance: artifact exists with all four fields and a concrete SHA.
- [x] [P0-T3] Capture the baseline format state: run `csharpier check .` from the repo root. Write `docs/features/active/core-response-status-roundtrip-80/evidence/baseline/baseline-csharpier.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (file count checked, unformatted count).
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0` expected on a clean baseline.
- [x] [P0-T4] Capture the baseline build/lint/type-check state: run `dotnet build OpenClaw.MailBridge.sln`. Write `docs/features/active/core-response-status-roundtrip-80/evidence/baseline/baseline-build.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (warning/error counts; analyzers and nullable run as errors).
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0` expected on a clean baseline.
- [x] [P0-T5] Capture the baseline test + coverage state: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Write `docs/features/active/core-response-status-roundtrip-80/evidence/baseline/baseline-test-coverage.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including test pass/fail counts and numeric baseline coverage: pooled line/branch percent across the produced `coverage.cobertura.xml` reports and the `OpenClaw.Core` package line-rate/branch-rate (the targeted module). Placeholders such as `UNVERIFIED` are invalid.
  - Acceptance: artifact exists with all four fields and numeric line and branch percentages for both pooled and `OpenClaw.Core` scopes.
- [x] [P0-T6] Write the baseline coverage evidence artifact `docs/features/active/core-response-status-roundtrip-80/evidence/coverage/coverage-baseline.2026-07-01T22-16.md`: record the `OpenClaw.Core` package line-rate and branch-rate, the pooled solution line/branch percent, and the source `coverage.cobertura.xml` path(s) from P0-T5 (raw copies may additionally be placed under `artifacts/csharp/`). Include `Timestamp:` and the P0-T5 `Command:`/`EXIT_CODE:` for traceability.
  - Acceptance: artifact exists with numeric baseline values and at least one concrete cobertura source path.

### Phase 1 — Regression Test (must fail before fix)

- [x] [P1-T1] Create `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs` (MSTest + FluentAssertions, namespace `OpenClaw.Core.Tests`), mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs` but exercising `CoreCacheRepository.UpsertEventsAsync(...)` / `GetEventAsync(...)` with a `BridgeStatusDto`, request id, and `observedAtUtc` per the `CoreCacheRepositoryGraphFieldsTests` pattern (`ReadyBridge` static, `BuildEvent` helper, fixed `DateTimeOffset` values, unique `Data Source=<name>-{Guid.NewGuid():N};Mode=Memory;Cache=Shared` connection strings, no temporary files). Include exactly three tests:
  1. `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_declined` — write `ResponseStatus: 4`, assert `loaded.ResponseStatus == 4`.
  2. `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_null` — write `ResponseStatus: null`, assert `loaded.ResponseStatus` is null (not 0).
  3. `InitializeAsync_should_add_response_status_column_to_existing_database` — before calling `InitializeAsync`, open a separate `SqliteConnection` on the same connection string and execute the current (pre-fix) `events` DDL without the `response_status` column; then call `InitializeAsync` (guarded ALTER upgrade path), call `InitializeAsync` a second time (idempotency, no "duplicate column" error), then upsert `ResponseStatus: 4` and assert it reads back as 4.
  - Acceptance: file exists at the stated path, `dotnet build OpenClaw.MailBridge.sln` compiles it with exit 0, file is under 500 lines, and no test writes to disk.
- [x] [P1-T2] [expect-fail] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositoryResponseStatusTests"` against the pre-fix production code. Expected outcome: tests 1 and 3 FAIL (read returns null instead of 4) and test 2 passes pre-fix only because `ReadEvent` forces every read to null — record this explanation in the artifact. Write `docs/features/active/core-response-status-roundtrip-80/evidence/regression-testing/regression-fail-before.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), and `Output Summary:` naming each failing test and the observed actual-vs-expected values.
  - Acceptance: artifact exists with all four fields, `EXIT_CODE` is non-zero, and at least tests 1 and 3 are recorded as failing. Satisfies the fail-before half of AC-4.

### Phase 2 — Minimal Targeted Fix

All Phase 2 production edits are limited to `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` and `src/OpenClaw.Core/CoreCacheRepository.Events.cs`.

- [x] [P2-T1] In `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, add `response_status INTEGER NULL` as a column in the `events` `CREATE TABLE` statement inside `CreateTablesSql` (fresh-database path, AC-1).
  - Acceptance: the `events` DDL in `CreateTablesSql` contains exactly one `response_status INTEGER NULL` column definition.
- [x] [P2-T2] In `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, add a guarded ALTER at the start of `MigrateEventsSchemaAsync`, mirroring `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` lines 43-46: if `!await EventsColumnExistsAsync(connection, "response_status")`, execute `ALTER TABLE events ADD COLUMN response_status INTEGER NULL;` (existing-database upgrade path, AC-1). Do not add the column to the `GraphFieldColumns` array (that array documents the issue-#72 set).
  - Acceptance: `MigrateEventsSchemaAsync` contains the guarded `response_status` ALTER before the `GraphFieldColumns` loop; `GraphFieldColumns` is unchanged.
- [x] [P2-T3] In `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, correct the now-stale doc comments: the class-level summary (currently line 10, "Per the spec Non-Goals (issue #80), no response_status column is added here."), the `GraphFieldColumns` summary (currently line 107, "The response_status column is intentionally NOT added here; that gap is deferred to issue #80..."), and the `MigrateEventsSchemaAsync` summary, so each accurately describes that `response_status` is now present (added by issue #80) while the `GraphFieldColumns` array remains the issue-#72 set.
  - Acceptance: no comment in the file describes `response_status` as deferred or absent; `rg -i "deferred" src/OpenClaw.Core/CoreCacheRepository.Schema.cs` returns no `response_status`-related match.
- [x] [P2-T4] In `src/OpenClaw.Core/CoreCacheRepository.Events.cs`, upsert touchpoint 1 of 3: add `response_status` to the `INSERT INTO events(...)` column list in the `UpsertEventsAsync` command text.
  - Acceptance: the INSERT column list contains `response_status` exactly once.
- [x] [P2-T5] In `src/OpenClaw.Core/CoreCacheRepository.Events.cs`, upsert touchpoint 2 of 3: add `$response_status` to the `VALUES(...)` list in the `UpsertEventsAsync` command text, positionally matching the column added in P2-T4.
  - Acceptance: the VALUES list contains `$response_status` exactly once, at the same ordinal position as `response_status` in the column list.
- [x] [P2-T6] In `src/OpenClaw.Core/CoreCacheRepository.Events.cs`, upsert touchpoint 3 of 3: add `response_status = excluded.response_status` to the `ON CONFLICT(bridge_id) DO UPDATE SET` clause in the `UpsertEventsAsync` command text.
  - Acceptance: the DO UPDATE SET clause contains `response_status = excluded.response_status` exactly once.
- [x] [P2-T7] In `src/OpenClaw.Core/CoreCacheRepository.Events.cs`, write-path binding: add `command.Parameters.AddWithValue("$response_status", ToDbValue(evt.ResponseStatus));` to `AddEventParameters` (null binds as `DBNull.Value` via the existing `ToDbValue(int?)` helper).
  - Acceptance: `AddEventParameters` binds `$response_status` exactly once via `ToDbValue(evt.ResponseStatus)`.
- [x] [P2-T8] In `src/OpenClaw.Core/CoreCacheRepository.Events.cs`, read-path materialization: in `ReadEvent`, replace the hardcoded `ResponseStatus: null,` (currently line 248) with `ResponseStatus: ReadNullableInt(reader, "response_status"),` so SQL NULL round-trips to `null` and never coerces to 0 (AC-2).
  - Acceptance: `ReadEvent` contains `ReadNullableInt(reader, "response_status")` and no hardcoded `ResponseStatus: null` remains.
- [x] [P2-T9] Verify file-size compliance: confirm `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, `src/OpenClaw.Core/CoreCacheRepository.Events.cs`, and `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs` are each under 500 lines, and confirm via `git status --porcelain` that no files outside these three (plus plan/evidence artifacts) were modified.
  - Acceptance: all three line counts < 500; diff scope matches the plan's file list.
- [x] [P2-T10] Regression pass-after: re-run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositoryResponseStatusTests"`. All three tests must pass. Write `docs/features/active/core-response-status-roundtrip-80/evidence/regression-testing/regression-pass-after.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` (3 passed, 0 failed).
  - Acceptance: artifact exists with all four fields and `EXIT_CODE: 0`. Completes the pass-after half of AC-4 and demonstrates AC-1/AC-2.

### Phase 3 — Full QA Loop, Coverage Comparison, and Closeout

Loop rule: if any of P3-T1 through P3-T3 fails or changes any file, fix the cause and restart the loop from P3-T1. The artifacts below record the final clean pass.

- [x] [P3-T1] Final QA — Formatting: run `csharpier format .` then `csharpier check .` from the repo root. Write `docs/features/active/core-response-status-roundtrip-80/evidence/qa-gates/final-csharpier.2026-07-01T22-16.md` with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, and `Output Summary:`. If `csharpier format .` changed any file, restart the loop from this task after the change is committed to the working tree.
  - Acceptance: `csharpier check .` reports `EXIT_CODE: 0` with zero unformatted files. Supports AC-5.
- [x] [P3-T2] Final QA — Lint + type check: run `dotnet build OpenClaw.MailBridge.sln` (Roslyn analyzers and nullable analysis as errors). Write `docs/features/active/core-response-status-roundtrip-80/evidence/qa-gates/final-build.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (0 warnings, 0 errors). Restart the loop from P3-T1 on failure.
  - Acceptance: `EXIT_CODE: 0` with zero analyzer or nullable diagnostics. Supports AC-5.
- [x] [P3-T3] Final QA — Tests with coverage: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (full solution: unit, architecture-boundary, and the new regression tests). Write `docs/features/active/core-response-status-roundtrip-80/evidence/qa-gates/final-test-coverage.2026-07-01T22-16.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including total pass/fail counts, explicit confirmation that all pre-existing `tests/OpenClaw.Core.Tests/` tests pass unchanged (AC-3), and numeric post-change coverage: pooled line/branch percent and `OpenClaw.Core` package line-rate/branch-rate, with the cobertura source path(s). Restart the loop from P3-T1 on failure. `SKIPPED` is not a valid outcome for this task.
  - Acceptance: `EXIT_CODE: 0`, zero failed tests, numeric coverage values recorded. Supports AC-3 and AC-5.
- [x] [P3-T4] Coverage comparison: write `docs/features/active/core-response-status-roundtrip-80/evidence/coverage/coverage-delta.2026-07-01T22-16.md` comparing P0-T6 baseline against P3-T3 post-change values. Must record: (a) baseline and post-change pooled line/branch percent and `OpenClaw.Core` package line-rate/branch-rate with deltas; (b) threshold verification line >= 85% and branch >= 75%; (c) changed-lines coverage — from the post-change cobertura, confirm the new/changed lines in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (guarded ALTER, both guard branches exercised by the fresh-DB and existing-DB tests) and `src/OpenClaw.Core/CoreCacheRepository.Events.cs` (`$response_status` binding and `ReadNullableInt` read) are covered; (d) a PASS or remediation-required disposition. If any required numeric value is unavailable or any threshold fails, the disposition is remediation-required, never PASS.
  - Acceptance: artifact exists with numeric baseline, post-change, and delta values, changed-lines findings for both production files, and an explicit disposition. Supports AC-5.
- [x] [P3-T5] Closeout: check off the satisfied `## Acceptance Criteria` items in `docs/features/active/core-response-status-roundtrip-80/issue.md` (and mirror the same checkbox state in `spec.md` and `user-story.md`), each with the evidence artifact path that proves it. Write the issue-update mirror `docs/features/active/core-response-status-roundtrip-80/evidence/issue-updates/issue-80.2026-07-01T22-16.md` with `Timestamp:`, the exact update text (AC-to-evidence mapping), and `PostedAs:` (`comment`/`body` with URL if posted to GitHub issue #80, or `POSTING BLOCKED` with reason if not).
  - Acceptance: all five AC checkboxes in `issue.md` are checked with evidence paths, and the mirror artifact exists with the required fields.

## Preflight

- This plan requires validation before execution: run the `mcp__drm-copilot__validate_orchestration_artifacts` MCP tool with `artifact_type: "plan"` and `artifact_path: docs/features/active/core-response-status-roundtrip-80/plan.2026-07-01T22-16.md`, and route the plan through `atomic-executor` with `DIRECTIVE: PREFLIGHT VALIDATION ONLY`.
- Required signal: `PREFLIGHT: ALL CLEAR` before any task executes. On `PREFLIGHT: REVISIONS REQUIRED`, revise this same file in place (no sibling plan files) and repeat.
