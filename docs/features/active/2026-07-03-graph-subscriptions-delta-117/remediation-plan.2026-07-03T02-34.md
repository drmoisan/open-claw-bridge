# graph-subscriptions-delta — Remediation Plan (Cycle 1)

- **Issue:** #117
- **Owner:** drmoisan
- **Last Updated:** 2026-07-03T02-34
- **Status:** Ready for preflight
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata)
- **Cycle Entry:** 2026-07-03T02-34
- **Finding Remediated:** B-117-01 (Blocking) — two new files below the 75% per-file branch gate
- **Primary Input:** `docs/features/active/2026-07-03-graph-subscriptions-delta-117/remediation-inputs.2026-07-03T02-34.md`
- **Source Audits:** `policy-audit.2026-07-03T02-34.md`, `code-review.2026-07-03T02-34.md`, `feature-audit.2026-07-03T02-34.md` (same folder)

## Required References

- Policy compliance order: `CLAUDE.md` auto-loaded rules, then `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`
- Remediation requirements source: `remediation-inputs.2026-07-03T02-34.md` (enumerated fix list items 1–5, exit verification commands, Do Not Do list)
- AC source files (work mode `full-feature`): `<FEATURE>/spec.md` and `<FEATURE>/user-story.md` (`issue.md` mirrors the same list); AC protocol per `.claude/skills/acceptance-criteria-tracking/SKILL.md`

**All work must comply with these policies; do not duplicate their content here.**

## Scope and Justification

- **Required (Blocking B-117-01):** fix items 1–3 of the remediation inputs. Item 3 is executed as option (a), the fail-fast refactor, which is the reviewer-preferred resolution (CR-117-04) and is explicitly permitted by the inputs' Do Not Do list ("the optional item-3(a)/item-5 changes named above"). Justification for the production change: `expiration_utc` is `NOT NULL` and always written via `RenderUtc`, so an unparseable stored value is data corruption; the repository fail-fast policy (`general-code-change.md`) requires an explicit error, not a silent `DateTimeOffset.MinValue` sentinel. The change is confined to the `ReadSubscription` helper.
- **Same-cycle Minor findings (reviewer-recommended):** fix item 4 (CR-117-02, test-only) and fix item 5 (CR-117-03, one-line catch-filter change per worker file plus directed tests). Both are named as in-scope by the inputs; item 5's production change is limited to the single `when` filter expression in each of three worker files. One directed `TaskCanceledException` test is added per worker so the changed line in every file carries coverage (uniform "no regression on changed lines" gate).
- **Out of scope (per Do Not Do):** `GraphRequestExecutor`, the webhook processor, the queue, the schema, runsettings, coverage exclusions, suppressions, any relaxation of existing tests or assertions, and AC text edits. The executor-side timeout mapping is a follow-up issue.

## Global Conventions for This Plan

- `<FEATURE>` = `docs/features/active/2026-07-03-graph-subscriptions-delta-117`. All evidence artifacts go under `<FEATURE>/evidence/<kind>/` (kinds used here: `remediation-baseline`, `regression-testing`, `qa-gates`, `other`). Raw command intermediates (TRX, Cobertura XML, build logs) go under `artifacts/csharp/`; evidence markdown files summarize them. Evidence paths outside `<FEATURE>/evidence/` are prohibited (non-overridable; `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, `artifacts/evidence/` are forbidden).
- `<ts>` = actual run timestamp in `yyyy-MM-ddTHH-mm` format, substituted at execution time.
- Every command-step evidence artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- Test stack: MSTest + FluentAssertions + Moq (+ CsCheck only where a genuine property applies) + `FakeTimeProvider`. No live Graph calls (recorded in-code payload constants only), no temp files (SQLite in-memory shared-cache connection strings per existing tests), no `Task.Delay`/`Thread.Sleep`/wall-clock reads, no file over 500 lines.
- The C# toolchain loop is, in order: (1) `csharpier format .` then `csharpier check .` (global tool 1.3.0; do NOT use `dotnet csharpier`), (2) `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as errors), (3) `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. If any step fails or changes files, restart the loop from step 1 until all steps pass in a single pass.
- Coverage parsing convention (matches the reviewer's method): parse the fresh Cobertura reports from the test run; dedupe duplicate class entries per file+line before pooling; per-file branch coverage is computed from instrumented condition arms.
- Diff-scope confinement: production changes are limited to `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` (the `ReadSubscription` helper only), and the single `catch ... when` filter line in `src/OpenClaw.Core/CloudSync/NotificationDispatchWorker.cs`, `src/OpenClaw.Core/CloudSync/SubscriptionRenewalWorker.cs`, and `src/OpenClaw.Core/CloudSync/DeltaReconciliationWorker.cs`. Test changes are limited to `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerTests.cs`, `tests/OpenClaw.Core.Tests/CoreCacheRepositorySubscriptionsTests.cs`, `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerTests.cs` (and/or `GraphDeltaReconcilerRecoveryTests.cs` if needed for the 500-line cap), `tests/OpenClaw.Core.Tests/CloudSync/NotificationDispatchWorkerTests.cs`, `tests/OpenClaw.Core.Tests/CloudSync/SubscriptionRenewalWorkerTests.cs`, and `tests/OpenClaw.Core.Tests/CloudSync/DeltaReconciliationWorkerTests.cs`. No new packages, no new production files.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Remediation Baseline Capture & Policy Compliance

- [x] [P0-T1] Read the repository policy files in the required order (`CLAUDE.md`-loaded rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`), then `<FEATURE>/remediation-inputs.2026-07-03T02-34.md` and `<FEATURE>/policy-audit.2026-07-03T02-34.md`
  - Acceptance: `<FEATURE>/evidence/remediation-baseline/phase0-instructions-read.md` exists containing `Timestamp:`, `Policy Order:`, and the explicit list of files read
- [x] [P0-T2] Capture the formatting baseline by running `csharpier check .` from the repository root
  - Acceptance: `<FEATURE>/evidence/remediation-baseline/csharpier-check.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
- [x] [P0-T3] Capture the build/analyzer baseline by running `dotnet build OpenClaw.MailBridge.sln`
  - Acceptance: `<FEATURE>/evidence/remediation-baseline/dotnet-build.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts)
- [x] [P0-T4] Capture the test-and-coverage baseline by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and extracting numeric pooled line and branch coverage from the produced Cobertura reports (dedupe per the coverage parsing convention; raw reports retained under `artifacts/csharp/`)
  - Acceptance: `<FEATURE>/evidence/remediation-baseline/dotnet-test-coverage.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing test pass/fail counts and numeric pooled line-coverage and branch-coverage percentages (no placeholders); expected reference point is the reviewer's pooled 92.83% line / 83.25% branch
- [x] [P0-T5] From the same Cobertura reports, record the per-file baseline for the finding: line and branch coverage for `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs` (expected 100.00% line, 2/4 = 50.00% branch), `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` (expected 100.00% line, 1/2 = 50.00% branch), and `src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs` (expected 75.00% branch, zero margin), confirming B-117-01 reproduces at cycle start
  - Acceptance: `<FEATURE>/evidence/remediation-baseline/per-file-branch-baseline.<ts>.md` exists with `Timestamp:`, `Command:` (the parse command), `EXIT_CODE:`, and `Output Summary:` listing numeric per-file line and branch percentages for all three files
- [x] [P0-T6] Record the AC-5 cycle-start determination: the remediation inputs direct that AC-5's existing `[x]` check-off is re-confirmed at re-audit and NOT re-edited during execution (inputs, Do Not Do, final bullet), so no uncheck is performed; document the contested state (feature-audit AC-5 = PARTIAL; executor `[x]` at `<FEATURE>/spec.md` line 145, `<FEATURE>/user-story.md` line 60, and the `issue.md` mirror) without modifying any AC source file
  - Acceptance: `<FEATURE>/evidence/other/ac5-determination.<ts>.md` exists with `Timestamp:`, the cited inputs directive, and confirmation that `git status` shows no modification to `spec.md`, `user-story.md`, or `issue.md` from this task

### Phase 1 — Cover ParseSubscription Fail-Fast Arms (fix items 1–2)

- [x] [P1-T1] Add test `CreateAsync_missing_id_in_the_response_fails_fast_and_persists_nothing` to `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerTests.cs` (currently 247 lines; stays under 500): mocked handler returns HTTP 200 with body `{}` (valid JSON object, no `id`); drive `GraphSubscriptionManager.CreateAsync`; assert the failure envelope carries code `INTERNAL_ERROR` (the executor's `GraphMappingException` mapping for the line-320 throw arm) and the `FakeSubscriptionStore` remains empty
  - Acceptance: test present, follows Arrange–Act–Assert, and passes; no other test in the file modified
- [x] [P1-T2] Add test `CreateAsync_body_deserializing_to_json_null_fails_fast_and_persists_nothing` to the same file: mocked handler returns HTTP 200 with body `null` (the four-character JSON literal); drive `CreateAsync`; assert the failure envelope carries code `TRANSPORT_FAILURE` (the executor's `JsonException` mapping for the line-315 `?? throw` arm) and nothing is persisted to the store
  - Acceptance: test present, follows Arrange–Act–Assert, and passes; no other test in the file modified
- [x] [P1-T3] Run the directed suite `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~GraphSubscriptionManagerTests"` and record the result
  - Acceptance: `<FEATURE>/evidence/regression-testing/parse-subscription-arms.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the two new tests passing alongside the existing four

### Phase 2 — ReadSubscription Expiration Fail-Fast Refactor (fix item 3, option (a))

- [x] [P2-T1] In `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` `ReadSubscription` (line 157), replace `ReadDateTimeOffset(reader, "expiration_utc") ?? DateTimeOffset.MinValue` with a fail-fast arm that throws `InvalidOperationException` naming the subscription id and the `expiration_utc` column when the stored value is unparseable (e.g., read `subscription_id` and the raw expiration first, then `?? throw new InvalidOperationException(...)`); change nothing else in the file. Minimal-change justification: the column is `NOT NULL` and always written via `RenderUtc`, so an unparseable value is data corruption and must fail fast per `general-code-change.md`, per CR-117-04, and per remediation-inputs item 3(a) (preferred option)
  - Acceptance: the `DateTimeOffset.MinValue` sentinel no longer appears in the file; the exception message contains both the subscription id and the literal column name `expiration_utc`; `dotnet build OpenClaw.MailBridge.sln` exits 0
- [x] [P2-T2] Add test `GetSubscriptionAsync_with_unparseable_expiration_throws_naming_the_id_and_column` to `tests/OpenClaw.Core.Tests/CoreCacheRepositorySubscriptionsTests.cs` (currently 197 lines; stays under 500): upsert a valid record through the repository on a `NewConnectionString(...)` shared-cache database, then open a second `SqliteConnection` on the same connection string (existing file pattern) and `UPDATE`/`INSERT` a row with `expiration_utc = 'not-a-timestamp'`; call `GetSubscriptionAsync`; assert `InvalidOperationException` whose message contains the subscription id and `expiration_utc`
  - Acceptance: test present, follows Arrange–Act–Assert, and passes; existing round-trip tests in the file are unmodified and still pass
- [x] [P2-T3] Run the directed suite `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositorySubscriptionsTests"` and record the result
  - Acceptance: `<FEATURE>/evidence/regression-testing/read-subscription-failfast.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the new test passing alongside all existing subscription-store tests

### Phase 3 — Pin GraphDeltaReconciler.ParseDeltaPage Exact-Gate Arms (fix item 4, CR-117-02)

- [x] [P3-T1] Add test `Reconcile_page_without_a_value_property_upserts_nothing_and_completes_the_walk` to `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerTests.cs` (currently 181 lines): recorded page body with no `value` property followed by a terminal deltaLink page; assert zero upserts through the sink and a successful walk that persists the deltaLink
  - Acceptance: test present and passes; file remains under 500 lines
- [x] [P3-T2] Add test `Reconcile_removed_entry_without_an_id_is_skipped_with_debug_log_and_no_upsert` to the same file (or `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerRecoveryTests.cs` if the 500-line cap requires; currently 311 lines): recorded page whose `value` contains an `@removed` entry with no `id`; assert the entry is skipped via the Debug log path using `"(unknown)"` and no upsert occurs for it
  - Acceptance: test present and passes; target file remains under 500 lines
- [x] [P3-T3] Add test `Reconcile_null_entry_inside_value_fails_with_transport_failure_and_a_failed_ingest_run` (delivered as `Reconcile_unparseable_entry_inside_value_fails_with_transport_failure_and_a_failed_ingest_run`; literal-null variant structurally unreachable — deviation recorded in `evidence/regression-testing/delta-reconciler-arms.2026-07-03T09-19.md`) to the same target file as P3-T2: recorded page whose `value` array contains the literal `null`; assert the failure envelope via the `JsonException` mapping and a failed `delta_reconcile` ingest run record
  - Acceptance: test present and passes; target file remains under 500 lines
- [x] [P3-T4] Run the directed suite `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~GraphDeltaReconciler"` and record the result
  - Acceptance: `<FEATURE>/evidence/regression-testing/delta-reconciler-arms.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the three new tests passing alongside all existing reconciler tests

### Phase 4 — Broaden Worker Loop Catch Filters (fix item 5, CR-117-03)

- [x] [P4-T1] In `src/OpenClaw.Core/CloudSync/NotificationDispatchWorker.cs` line 59, change the loop catch filter from `catch (Exception ex) when (ex is not OperationCanceledException)` to `catch (Exception ex) when (!stoppingToken.IsCancellationRequested)`; change nothing else in the file
  - Acceptance: exactly one line changed in the file; `dotnet build OpenClaw.MailBridge.sln` exits 0
- [x] [P4-T2] Add test `Loop_continues_with_warning_when_the_inner_call_throws_TaskCanceledException_without_stop_requested` to `tests/OpenClaw.Core.Tests/CloudSync/NotificationDispatchWorkerTests.cs` (currently 228 lines): the inner dispatch call throws `TaskCanceledException` while the stop token is not cancelled; assert a Warning log and that the loop continues (a subsequent iteration executes); stop the worker cleanly afterward
  - Acceptance: test present, deterministic (no real waits), and passes; file remains under 500 lines
- [x] [P4-T3] In `src/OpenClaw.Core/CloudSync/SubscriptionRenewalWorker.cs` line 44, apply the identical one-line catch-filter change
  - Acceptance: exactly one line changed in the file; `dotnet build OpenClaw.MailBridge.sln` exits 0
- [x] [P4-T4] Add the analogous `TaskCanceledException`-continues-with-Warning test to `tests/OpenClaw.Core.Tests/CloudSync/SubscriptionRenewalWorkerTests.cs` (currently 236 lines), same shape as P4-T2 against the renewal sweep call
  - Acceptance: test present, deterministic, and passes; file remains under 500 lines
- [x] [P4-T5] In `src/OpenClaw.Core/CloudSync/DeltaReconciliationWorker.cs` line 46, apply the identical one-line catch-filter change
  - Acceptance: exactly one line changed in the file; `dotnet build OpenClaw.MailBridge.sln` exits 0
- [x] [P4-T6] Add the analogous `TaskCanceledException`-continues-with-Warning test to `tests/OpenClaw.Core.Tests/CloudSync/DeltaReconciliationWorkerTests.cs` (currently 197 lines), same shape as P4-T2 against the reconcile call
  - Acceptance: test present, deterministic, and passes; file remains under 500 lines
- [x] [P4-T7] Run the directed suite `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~WorkerTests"` and record the result
  - Acceptance: `<FEATURE>/evidence/regression-testing/worker-catch-filters.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the three new tests passing alongside all existing worker tests (shutdown/cancellation tests included, proving stop-path behavior is unchanged)

### Phase 5 — Final QA Loop, Coverage Verification, and AC-5 Re-affirmation

- [x] [P5-T1] Run `csharpier format .` then `csharpier check .` from the repository root (loop step 1; if this or any later step in P5-T1..P5-T3 fails or changes files, restart from this task until all three pass in a single pass)
  - Acceptance: `<FEATURE>/evidence/qa-gates/csharpier.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` from the final clean pass
- [x] [P5-T2] Run `dotnet build OpenClaw.MailBridge.sln` (loop step 2)
  - Acceptance: `<FEATURE>/evidence/qa-gates/dotnet-build.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (0 warnings / 0 errors) from the final clean pass
- [x] [P5-T3] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (loop step 3; includes the NetArchTest architecture suites) and extract numeric pooled line and branch coverage from the fresh Cobertura reports (raw reports retained under `artifacts/csharp/`)
  - Acceptance: `<FEATURE>/evidence/qa-gates/dotnet-test-coverage.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` containing test pass/fail counts and numeric post-change pooled line and branch coverage (no placeholders) from the final clean pass
- [x] [P5-T4] Coverage verification: parse the fresh Cobertura reports from P5-T3 (dedupe duplicate class entries per file+line before pooling) and report numeric per-file LINE and BRANCH coverage for `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs` and `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` (each must be >= 75% instrumented branch), `src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs` (must be > 75.00% branch), and the three worker files `NotificationDispatchWorker.cs`, `SubscriptionRenewalWorker.cs`, `DeltaReconciliationWorker.cs` (each >= 75% branch; changed catch-filter line covered); plus pooled coverage, which must satisfy the uniform gates (>= 85% line / >= 75% branch) AND no regression versus the reviewer reference 92.83% line / 83.25% branch and versus the P0-T4 remediation baseline
  - Acceptance: `<FEATURE>/evidence/qa-gates/coverage-verification.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` listing every named per-file line and branch percentage, both pooled percentages, and the baseline deltas, all numeric, all thresholds met; if any threshold is not met the task fails and the plan is remediation-required (no PASS reporting)
- [x] [P5-T5] Verify diff-scope and file-size compliance: `git diff --stat` against the pre-cycle head shows changes confined to the files named in the Global Conventions diff-scope statement (plus evidence/plan markdown), and every modified `.cs` file is <= 500 lines
  - Acceptance: `<FEATURE>/evidence/qa-gates/diff-scope-and-file-size.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` listing each modified file with its line count and no out-of-scope entries
- [x] [P5-T6] Re-affirm the contested AC-5 check-off per `acceptance-criteria-tracking`: with P5-T4 passed, verify the existing `[x]` on AC-5 at `<FEATURE>/spec.md` line 145, `<FEATURE>/user-story.md` line 60, and the `issue.md` mirror is now accurate (checkbox state only; no text edits, no new criteria), and record a dated re-affirmation citing the P5-T4 coverage-verification artifact and the P5-T1..P5-T3 clean-pass artifacts, including the required AC Status Summary block (Source, Total 5, Checked off 5, Remaining 0)
  - Acceptance: `<FEATURE>/evidence/other/ac5-reaffirmation.<ts>.md` exists with `Timestamp:`, the dated citations, the AC Status Summary block, and confirmation that no AC source file text was modified by this cycle (`git diff` on the three files shows no changes, or checkbox-state-only if a correction was required)

## Exit Condition (for orchestrator re-audit)

B-117-01 closed (both named files >= 75% per-file instrumented branch), CR-117-02/03/04 addressed, toolchain clean in a single pass, pooled coverage non-regressed, `blocking_count == 0` at the exit re-audit (`policy-audit`/`code-review`/`feature-audit` at the exit timestamp).
