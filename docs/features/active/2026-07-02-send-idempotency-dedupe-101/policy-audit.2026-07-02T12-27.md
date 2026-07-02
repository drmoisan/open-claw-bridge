# Policy Compliance Audit: send-idempotency-dedupe (#101)

**Audit Date:** 2026-07-02
**Code Under Test:** C# only. 3 new production `.cs` files in `src/OpenClaw.Core` (`Agent/SentActionKey.cs` pure static dedupe-key builder, 57 lines; `Agent/Contracts/ISentActionStore.cs` interface-only store contract, 29 lines; `CoreCacheRepository.SentActions.cs` store implementation partial with lazy schema-ensure, 106 lines), 4 modified production `.cs` files (`Agent/Runtime/SchedulingWorker.Pipeline.cs` consult/skip/record logic inside the `SendEnabled` else-branch; `Agent/Runtime/SchedulingWorker.cs` one constructor parameter; `CoreCacheRepository.Schema.cs` one appended `CREATE TABLE IF NOT EXISTS sent_actions` DDL statement; `Program.cs` one DI registration), 4 new test `.cs` files and 1 modified test `.cs` file. Plus feature scoping/evidence Markdown (feature folder for issue #101). No Python, PowerShell, TypeScript, Bash, or governed JSON files changed in the branch diff.

**Scope:** Full feature branch `feature/send-idempotency-dedupe-101` @ `a3521833996bbf66e6d0e0ddedaebfaf8dcc85ec` versus resolved base `main` @ merge-base `d90681c766d8a9b9cff93fd59bc1989c80632d1f` (origin/main; the local `main` ref is stale per the caller inputs). Scope is feature-vs-base over the complete branch diff. Diff file breakdown (name-only): 12 `.cs`, 15 `.md` (27 files, +1494/-3). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md`.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 7 production `.cs` + 5 test `.cs` | 706 (solution) / 254 (Core.Tests) | 701 pass, 0 fail, 5 env-gated skips | 90.56% line, 80.05% branch (pooled solution) | 90.63% line, 80.25% branch (pooled solution) | SentActionKey.cs 100% line / 100% branch; CoreCacheRepository.SentActions.cs 100% line / 100% branch (instrumented lines; async bodies behaviorally covered by 8 repository tests); ISentActionStore.cs interface-only (legitimately excluded per policy); all 4 modified files 100% line with no changed-line regression |

**Note:** Python, PowerShell, Bash, TypeScript, and governed-JSON rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit PASS.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-02-send-idempotency-dedupe-101/evidence/baseline/baseline-test-coverage.2026-07-02T11-59.md` (pooled 90.56% line / 80.05% branch; `OpenClaw.Core` package 98.63% line / 91.82% branch); reviewer independently re-parsed the executor baseline cobertura under `artifacts/csharp/baseline-101/` and confirmed the figures exactly
- C# post-change coverage artifact: `docs/features/active/2026-07-02-send-idempotency-dedupe-101/evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md` and `evidence/qa-gates/coverage-comparison.2026-07-02T12-16.md` (pooled 90.63% line / 80.25% branch; `OpenClaw.Core` package 98.66% line / 92.07% branch)
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head): `docs/features/active/2026-07-02-send-idempotency-dedupe-101/evidence/qa-gates/coverage-review/{0a055268...,46036f31...,fed5a0ea...}/coverage.cobertura.xml`; independently parsed pooled 90.63% line (4207/4642) / 80.25% branch (947/1180), identical to executor evidence. Reviewer evidence: `docs/features/active/2026-07-02-send-idempotency-dedupe-101/evidence/qa-gates/coverage-review.2026-07-02T12-27.md`
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura. The C# coverage gate is met (pooled line 90.63% >= 85%, branch 80.25% >= 75%; both instrumented new files at 100% line / 100% branch; every instrumented changed line is covered; no regression — pooled coverage improved by +0.07pp line / +0.20pp branch).

---

## Executive Summary

This feature branch closes issue #101 (gap F6, the accepted interim duplicate-send risk from #99): the scheduling worker now enforces send idempotency. A pure static builder (`SentActionKey.Build`) produces the deterministic dedupe key `{mailbox}:{messageId}:{actionType}` with fail-fast `ArgumentException` guards on each component; a new `sent_actions` table joins the Core SQLite cache via the established `CREATE TABLE IF NOT EXISTS` guarded-migration pattern plus a lazy once-per-instance schema-ensure guard that removes the hosted-service initialization-ordering dependency; `CoreCacheRepository` implements the new clock-free `ISentActionStore` (`IsRecordedAsync` exact-key lookup; `RecordAsync` conflict-tolerant `INSERT ... ON CONFLICT(dedupe_key) DO NOTHING` with caller-supplied timestamp stored in ISO-8601 round-trip form); and `SchedulingWorker.ProposeAndActAsync` consults the store before `SendMailAsync` and records after a successful send, entirely inside the existing `SendEnabled`-gated else-branch so the kill switch remains the outer boundary. A failed send records nothing, preserving retry via the unchanged `ProcessMessageSafelyAsync` isolation. No HostAdapter, MailBridge, contract, route, or schema-wire surface changed — verified against the full diff.

The mandatory toolchain was independently re-run by the reviewer against the branch head `a352183` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 212 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** NetArchTest suite in `OpenClaw.Core.Tests` — 2 passed, 0 failed.
- **Tests + coverage:** full solution `dotnet test` with `--collect:"XPlat Code Coverage"` — 701 passed, 0 failed, 5 environment-gated skips (same skips as baseline); pooled coverage 90.63% line / 80.25% branch, above the uniform gates; T1 property-test obligation for the new pure function satisfied by `SentActionKeyPropertyTests` (3 CsCheck properties, 1000 iterations each, seed printed on failure per suite convention).
- **Regression evidence:** fail-before artifact (EXIT 1; the five new dedupe tests failing before the worker seam existed) and pass-after artifact (EXIT 0) are present under `evidence/regression-testing/`.

No Blocking findings. No material PARTIAL findings. One Minor finding (dedupe-hit log line verified by code inspection, not content-asserted in a test) and three informational observations (Section 8 and the code review). Remediation is not required. The feature is recommended Go for PR.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/csharp.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript / governed JSON (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is seven production files, five test files, and documentation/evidence Markdown. The executor's raw cobertura intermediates under `artifacts/csharp/baseline-101/` and `artifacts/csharp/final-101/` are untracked (gitignored) and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`d90681c`), the checked-out feature branch, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observation (not a narrowing instruction, recorded for completeness): the PR-context summary's "Changed files overview" reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only. That categorization is inaccurate; the authoritative `git diff d90681c..a352183` contains 7 production C# files and 5 test C# files. The audit used the authoritative git diff file list, not the summary categorization. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only d90681c..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
- Result: **NONE.** All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-02-send-idempotency-dedupe-101/evidence/<kind>/` locations (baseline, qa-gates, regression-testing, other). No files under `artifacts/` are tracked in the diff at all.
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own evidence was written to the canonical `evidence/qa-gates/` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, #18, and #99 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/baseline-101/` and `artifacts/csharp/final-101/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Every repository test builds its own uniquely-named in-memory shared-cache database (`Data Source=core-sa-<label>-<guid>` / `worker-dedupe-<guid>`); every worker test builds a fresh mock graph via `ServiceReturningContext()`/`CandidateSource()`/`Store()` helpers; property tests are pure. No shared mutable state. 254/254 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | Each dedupe test pins one seam behavior (hit-skip, miss-send-record ordering, failure no-record + isolation, kill-switch composition, restart persistence); each repository test pins one store behavior (round-trip, duplicate idempotency, timestamp form, migration idempotency, upgrade, malformed key, lazy ensure); each builder test pins format, constant, or one guard. |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 254 tests in ~1 s (reviewer run); all new tests are in-memory (mocks or in-memory SQLite). |
| **Determinism** | PASS | No wall-clock reads, sleeps, timers, network, or filesystem in any new test. `FakeTimeProvider(Now)` supplies the clock; the recorded timestamp is asserted against the fake value. CsCheck property tests follow the suite's seeded-sample convention (failing seed printed on `Sample` failure, documented in the class XML doc). |
| **Readability & Maintainability** | PASS | Descriptive names (`RunCycle_StoreMiss_SendsThenRecordsKeyWithInjectedClockTimestamp`, `InitializeAsync_should_add_sent_actions_to_pre_existing_database`), FluentAssertions with reason strings throughout, XML doc summaries on all four new test classes citing the AC anchors, Arrange/Act/Assert comments. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline pooled: 90.56% line, 80.05% branch; `OpenClaw.Core` package 98.63% line / 91.82% branch. Source: `evidence/baseline/baseline-test-coverage.2026-07-02T11-59.md`; independently re-parsed by the reviewer from `artifacts/csharp/baseline-101/` cobertura with identical results. |
| **No Coverage Regression** | PASS | Post-change pooled: 90.63% line, 80.25% branch (+0.07pp / +0.20pp); Core package 98.66% / 92.07% (+0.03pp / +0.25pp). Independently confirmed from reviewer cobertura (this audit). |
| **New Code Coverage** | PASS | Per-changed-file (reviewer cobertura, line AND branch): `SentActionKey.cs` (new) 100% line (21/21) / 100% branch (6/6). `CoreCacheRepository.SentActions.cs` (new) 100% line (10/10) / 100% branch (6/6) on instrumented lines; async method bodies are uninstrumented per the pre-existing runsettings attribute exclusion and are behaviorally covered by 8 repository tests including the 4-row malformed-key negative test (Section 8 informational note). `ISentActionStore.cs` (new) is interface-only with no executable lines — legitimately excluded per the type-only-module policy clarification. Modified files: `SchedulingWorker.Pipeline.cs` 100% line (20/20) / 50% branch (2/4) — both partial conditions are on pre-existing lines 170 and 177 untouched by this branch (diff hunks add lines 129-151 and 155-157 only) and the file was identically 20/20 line, 2/4 branch at baseline, so there is no changed-line regression; `SchedulingWorker.cs`, `CoreCacheRepository.Schema.cs`, and `Program.cs` all 100% line. New test files are excluded from coverage measurement per policy (`[*.Tests]*` exclude in `mailbridge.runsettings`). |
| **Comprehensive Coverage** | PASS | Builder format/constant/guards + 3 properties; store round-trip, duplicate idempotency, UTC-normalized ISO-8601 timestamp form, migration idempotency, pre-existing-database upgrade, malformed-key rejection, lazy schema-ensure; worker hit-skip, miss-send-record ordering, failure no-record with next-candidate processing, kill-switch composition, restart persistence over one shared database. Existing `SchedulingWorkerTests` (13-line helper change only) unregressed. |
| **Positive Flows** | PASS | Build returns the colon-joined key; record-then-read returns true; miss path sends then records `Msg1Key` with the `FakeTimeProvider` timestamp; restart test sends exactly once in total. |
| **Negative Flows** | PASS | `ArgumentException` per builder component (null/empty/whitespace x 3 via `[DataRow]`, parameter name asserted); malformed dedupe keys rejected by `RecordAsync` (4 data rows, `WithParameterName("dedupeKey")`); send failure records nothing. |
| **Edge Cases** | PASS | Duplicate `RecordAsync` leaves exactly one row; non-UTC offset timestamp normalized to UTC `O` form; fresh database without `InitializeAsync` served by the lazy ensure guard; pre-existing database upgraded in place; distinctness property constrained to colon-free inputs matching the documented builder contract. |
| **Error Handling** | PASS | `RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate` proves the throw propagates to `ProcessMessageSafelyAsync`, nothing is recorded, and the second candidate is still processed; builder and store guards fail fast with named parameters. |
| **Concurrency** | PASS (addressed) | The lazy schema-ensure guard's benign race (non-synchronized `bool` over idempotent `CREATE TABLE IF NOT EXISTS` DDL) is documented in the production XML doc; restart test exercises two repository instances over one shared database. No other new concurrency surface. |
| **State Transitions** | PASS | Store state transition (unrecorded -> recorded -> skip) covered across cycles and across simulated restart; kill-switch-off leaves state untouched (`IsRecordedAsync`/`RecordAsync` both `Times.Never`). |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 90.56% line, 80.05% branch (pooled solution) -> Post-change: 90.63% line, 80.25% branch. Change: +0.07% line, +0.20% branch. New/changed-code coverage: SentActionKey.cs 100% line / 100% branch and CoreCacheRepository.SentActions.cs 100% line / 100% branch on instrumented lines with async bodies behaviorally covered; ISentActionStore.cs interface-only per policy; all four modified files 100% line with the only partial branches on two pre-existing untouched lines identical at baseline; new test files excluded from measurement per policy. Disposition: PASS (line >= 85%, branch >= 75%, no regression on changed lines). Evidence: `evidence/baseline/baseline-test-coverage.2026-07-02T11-59.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T12-16.md`, reviewer re-run `evidence/qa-gates/coverage-review.2026-07-02T12-27.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions reason strings on every assertion ("the record must happen only after a successful send", "a duplicate record must not add a second row"); call-order assertion compares full sequences (`callOrder.Should().Equal(["send", "record"], ...)`); CsCheck prints the failing seed. |
| **Arrange-Act-Assert Pattern** | PASS | All new tests carry explicit `// Arrange` / `// Act` / `// Assert` sections with scenario comments tied to the ACs. |
| **Document Intent** | PASS | XML docs on all four new test classes state the AC mapping (AC-1, AC-2, AC-3/4/5) and, for the property class, the T1 obligation and seed-print behavior. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No network, filesystem, COM, or external process; SQLite is in-memory shared-cache only; all worker seams mocked with Moq. |
| **Use Mocks/Stubs** | PASS | Moq mocks for `ISchedulingService`/`ISchedulingCandidateSource`/`ISentActionStore` per suite convention; the restart test deliberately uses the real store over in-memory SQLite to prove persistence, matching the established `CoreCacheRepositoryResponseStatusTests` pattern. |
| **Environment Stability** | PASS | No temporary files (verified by executor evidence `scope-and-size-verification.2026-07-02T12-18.md` section (c) and reviewer inspection: every connection string is `Mode=Memory;Cache=Shared` with a GUID name); no mutable global state; no environment variables read. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #101, `spec.md` v0.2 (behavior, dedupe-key contract, migration and lazy-ensure design, at-least-once trade-off), user-story scenarios, and the F6 gap-analysis reference define the change precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-02T11-40.md` present. |
| **Document the plan** | PASS | `plan.2026-07-02T11-40.md` with per-phase evidence under `evidence/**`; completed tasks recorded in the PR-context summary. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | One pure static builder, one two-method interface, one repository partial following the existing per-call connection pattern, ~26 lines of worker logic, one DDL statement, one DI line. No new dependencies, no new abstractions beyond the single store seam. |
| **Reusability** | PASS | Dedupe key construction centralized in `SentActionKey` (constant + builder) rather than inline string interpolation; store behavior behind `ISentActionStore` so the worker tests mock it and the repository implements it alongside its existing roles. |
| **Extensibility** | PASS | The Stage 1 `{tenantId}` extension path is documented on the spec and the builder; `actionType` is parameterized with a constant for the only Stage 0 action; the store interface is caller-clocked so future callers keep determinism. |
| **Separation of concerns** | PASS | Pure key construction (no I/O) separate from persistence (repository partial) separate from orchestration (worker pipeline); the store has no clock dependency — the worker supplies `timeProvider.GetUtcNow()`. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | New table logic isolated in `CoreCacheRepository.SentActions.cs` (dedicated partial, per the file-size constraint strategy in spec); worker change confined to the `SendEnabled` else-branch; tests mirror the production layout. |
| **Under 500 lines** | PASS | Reviewer-verified `wc -l`: SentActionKey.cs 57, ISentActionStore.cs 29, CoreCacheRepository.SentActions.cs 106, CoreCacheRepository.Schema.cs 253, SchedulingWorker.cs 104, SchedulingWorker.Pipeline.cs 195, Program.cs 329, SentActionKeyTests.cs 78, SentActionKeyPropertyTests.cs 81, CoreCacheRepositorySentActionsTests.cs 212, SchedulingWorkerTests.cs 319, SchedulingWorkerDedupeTests.cs 296. All under the 500-line cap; matches executor evidence `evidence/other/scope-and-size-verification.2026-07-02T12-18.md`. |
| **Public vs internal** | PASS | `SentActionKey` and `ISentActionStore` are public within `OpenClaw.Core` (consumed by the worker and DI); `CoreCacheRepository` remains `internal sealed`; `ParseSentActionKey` and `EnsureSentActionsSchemaAsync` are private. |
| **No circular dependencies** | PASS | No project-reference changes; NetArchTest boundary suite passes (2/2, reviewer run). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `SentActionKey.Build`, `ProposalReply`, `ISentActionStore.IsRecordedAsync`/`RecordAsync`, `EnsureSentActionsSchemaAsync`, `sentActionsSchemaEnsured`; test names state scenario and expectation. |
| **Docs/docstrings** | PASS | Builder XML doc documents the no-escaping limitation and colon-free distinctness guarantee (spec requirement); interface doc states caller-supplied timestamps and `RecordAsync` idempotency; partial doc explains the lazy schema-ensure rationale; worker comment cites issue #101 and the isolation boundary. |
| **Comment why, not what** | PASS | The worker comment explains why record-after-send preserves retry semantics; the ensure-guard doc explains why the benign race is harmless (idempotent DDL, flag only avoids round-trips). |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 212 files, EXIT 0. Executor: `evidence/qa-gates/final-format.2026-07-02T12-12.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). Executor additionally verified with `--no-incremental`. |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. |
| **4. Architecture** | PASS | NetArchTest boundary tests: 2 passed, 0 failed (reviewer run). No COM, VSTO, or interop references added; the new code references only `Microsoft.Data.Sqlite` (already in use). |
| **5. Testing** | PASS | Reviewer: full solution test run — 701 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the T1 property-based tests for the new pure function. |
| **6. Contract/schema checks** | N/A | No governed wire contract, DTO, route, or schema surface changed; the `sent_actions` table is internal Core-cache storage with an additive `CREATE TABLE IF NOT EXISTS` migration; `OpenClaw.HostAdapter.Contracts` and `OpenClaw.MailBridge.Contracts` are untouched in the diff. |
| **7. Integration tests** | N/A | No adapter/external-system boundary changed; the integration-style condition (restart persistence over a real store) is covered in-memory per spec. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the closing single-pass loop after the P5-T3 coverage fix (`final-format.2026-07-02T12-12.md` loop notes, `final-build.2026-07-02T12-13.md`, `final-test-coverage.2026-07-02T12-16.md`). |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in `evidence/qa-gates/coverage-review.2026-07-02T12-27.md`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy enumerates exactly the seven production files delivered; the diff matches with zero extras. |
| **Design choices explained** | PASS | Record-after-send at-least-once window, key-shape divergence from the master (`{tenantId}` deferred), redundant component columns for audit queries, and the lazy schema-ensure startup-ordering rationale are all documented in spec.md. |
| **Update supporting documents** | PASS | Acceptance criteria checked off with per-item evidence in `spec.md`, `user-story.md`, and the `issue.md` mirror. |
| **Provide next steps** | PASS | Spec Rollout: no new flag; `SendEnabled` remains the outer gate; fallback documented (delete `sent_actions` rows); Stage 1 key extension recorded. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, TypeScript, and governed-JSON sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70, #80, #19, #18, and #99 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; new code has no nullable warnings; `ExecuteScalarAsync` result checked with `is not null`; the tests' `null!` usages are the deliberate null-guard probes. |
| **Null-safety** | PASS | `string.IsNullOrWhiteSpace` guards on every builder component; `ParseSentActionKey` rejects null/whitespace keys and whitespace components before persistence. |
| **Async / resource safety** | PASS | `await using` on connections; `.ConfigureAwait(false)` on the new worker awaits per the file's existing pattern; caller token forwarded end-to-end (`OpenAsync(ct)`, `ExecuteScalarAsync(ct)`, `ExecuteNonQueryAsync(ct)`); no fire-and-forget, no blocking waits, no `async void`. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces throughout; `Async` suffix; PascalCase publics; parameterized SQL with named `$` parameters matching repository convention. |
| **Exceptions fail-fast** | PASS | `ArgumentException` with named parameters at the builder and at `ParseSentActionKey`; no catch blocks added anywhere in production; the send failure intentionally propagates to the existing isolation boundary. |
| **Deterministic clock** | PASS | The recorded timestamp is `timeProvider.GetUtcNow()` from the worker's injected `TimeProvider`; the store is clock-free by design; no `DateTime.UtcNow` in the diff (grep clean). |
| **No new suppressions** | PASS | The added C# diff lines contain no pragma or SuppressMessage entries and no banned-API usages (grep of all added lines for DateTime.Now/UtcNow, Random.Shared, Thread.Sleep, Task.Delay, pragma, SuppressMessage returned nothing). |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq (all existing suites; divergence recorded in `.claude/agent-memory/prd-feature/project_test_framework_discrepancy.md`). The new and modified tests follow the established repo convention, consistent with the prior validated #70, #80, #19, #18, and #99 audits. Pre-existing repo-wide divergence, not a finding against this branch.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataRow]`; FluentAssertions matchers with reason strings; Moq `Setup`/`Verify`/`Callback` per suite convention. |
| **Test file location** | PASS | `tests/OpenClaw.Core.Tests/Agent/SentActionKey{,Property}Tests.cs` mirror `src/OpenClaw.Core/Agent/SentActionKey.cs`; `tests/OpenClaw.Core.Tests/CoreCacheRepositorySentActionsTests.cs` mirrors `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs`; `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` mirrors `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`. No colocation in the production tree. |
| **Coverage expectation** | PASS | Pooled 90.63% line / 80.25% branch; both instrumented new files 100%/100%; every instrumented changed line covered; no regression. |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); the one new pure function (`SentActionKey.Build`) has three CsCheck properties (determinism, fixed component ordering on split, distinctness for colon-free triples; 1000 iterations each; generators exclude colons and whitespace per the documented builder contract). CsCheck 4.7.0 was already referenced by `OpenClaw.Core.Tests`; no new dependency. |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80 and #99 T1 audits). |
| **Determinism (no sleeps, no wall clock)** | PASS | No `Thread.Sleep`/`Task.Delay`/`DateTime.Now`/timers in the diff; `FakeTimeProvider(Now)` everywhere a clock is needed; the recorded timestamp asserted equal to the fake `Now`; CsCheck prints the failing seed for reproducibility. |
| **No temporary files** | PASS | In-memory shared-cache SQLite exclusively (GUID-named `Mode=Memory;Cache=Shared` sources); zero filesystem artifacts; verified by reviewer inspection and executor evidence. |
| **Focused / isolated** | PASS | Fresh mock graph or fresh database per test; helper factories (`Options`, `Message`, `ServiceReturningContext`, `CandidateSource`, `Store`, `Worker`, `NewConnectionString`) build per-test state only. |

---

## 5. Test Coverage Detail

### SentActionKeyTests (11 results: 2 named + 3 parameterized x 3 rows) and SentActionKeyPropertyTests (3 CsCheck properties, new files)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `Build_WithValidComponents_ReturnsColonJoinedKeyInFixedOrder` | Positive (format) | PASS |
| `ProposalReply_Constant_IsProposalReplyLiteral` | Positive (constant) | PASS |
| `Build_WithInvalidMailbox_ThrowsArgumentExceptionNamingMailbox` (3 rows) | Negative (null/empty/whitespace, parameter name) | PASS |
| `Build_WithInvalidMessageId_ThrowsArgumentExceptionNamingMessageId` (3 rows) | Negative | PASS |
| `Build_WithInvalidActionType_ThrowsArgumentExceptionNamingActionType` (3 rows) | Negative | PASS |
| `Build_IsDeterministic_SameTripleAlwaysYieldsSameKey` | Property (1000 iterations) | PASS |
| `Build_ColonFreeComponents_SplitYieldsComponentsInFixedOrder` | Property (ordering) | PASS |
| `Build_DistinctColonFreeTriples_YieldDistinctKeys` | Property (distinctness) | PASS |

### CoreCacheRepositorySentActionsTests (11 results: 7 named + 1 negative x 4 rows, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `IsRecordedAsync_should_return_false_for_unknown_key` | Positive (miss) | PASS |
| `RecordAsync_then_IsRecordedAsync_should_return_true` | Positive (round-trip) | PASS |
| `RecordAsync_duplicate_key_should_not_throw_and_leave_one_row` | Edge (idempotent insert, row-count verified) | PASS |
| `RecordAsync_should_round_trip_caller_supplied_timestamp_in_iso8601_o_form` | Edge (non-UTC offset normalized to UTC `O` form) | PASS |
| `InitializeAsync_twice_should_not_throw_and_sent_actions_should_exist` | Migration idempotency (AC-1 fresh path) | PASS |
| `InitializeAsync_should_add_sent_actions_to_pre_existing_database` | Migration upgrade path (AC-1) | PASS |
| `RecordAsync_malformed_key_should_throw_ArgumentException` (4 rows) | Negative (fail-fast key-shape guard) | PASS |
| `Store_methods_should_work_on_fresh_database_without_InitializeAsync` | Lazy schema-ensure guard | PASS |

### SchedulingWorkerDedupeTests (5 tests, new file) and SchedulingWorkerTests (helper extended only)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `RunCycle_StoreHit_SkipsSendAndCompletesWithoutThrowing` | Dedupe hit skip, normal outcome (AC-3) | PASS |
| `RunCycle_StoreMiss_SendsThenRecordsKeyWithInjectedClockTimestamp` | Miss path; send-before-record order proven via callbacks; `FakeTimeProvider` timestamp asserted (AC-3) | PASS |
| `RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate` | Failure no-record + per-message isolation (AC-5) | PASS |
| `RunCycle_SendDisabled_NeverTouchesStoreAndNeverSends` | Kill-switch composition (store untouched) | PASS |
| `RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal` | Restart persistence, real store, exactly-once total (AC-4) | PASS |
| `SchedulingWorkerTests` helper `Worker(...)` extended with a default not-recorded store mock | Pre-existing suite unregressed (all pass) | PASS |

**Coverage:** `SentActionKey.cs` 100% line / 100% branch; `CoreCacheRepository.SentActions.cs` 100% line / 100% branch (instrumented); all modified files 100% line. **Gap:** none attributable to this branch (the only partial branches, Pipeline.cs lines 170/177, are pre-existing untouched ternaries, identical at baseline).

**Fail-before / pass-after:** `evidence/regression-testing/dedupe-expect-fail.2026-07-02T12-10.md` (EXIT 1; the five dedupe tests failing before the worker seam) and `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (EXIT 0).

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 706 (701 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 254 passed / 254 | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~1 s | PASS |
| Pooled Code Coverage | 90.63% line, 80.25% branch | PASS |
| `OpenClaw.Core` package coverage (T1) | 98.66% line, 92.07% branch | PASS |
| Net new test results vs baseline | +30 (11 SentActionKey unit + 3 property + 11 repository + 5 worker dedupe) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 212 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest) | `dotnet test tests/OpenClaw.Core.Tests/... --filter "FullyQualifiedName~ArchitectureBoundary"` | 2 passed, 0 failed | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` | 701 passed, 0 failed, 5 skipped | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `a352183` on 2026-07-02. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** One Minor finding and two informational observations, recorded (none is a policy violation on this branch):

- **Dedupe-hit log line not content-asserted (Minor).** AC-3 requires a structured dedupe-hit log with `{MessageId}` and `{DedupeKey}` named template parameters. The production code emits exactly that (`SchedulingWorker.Pipeline.cs` lines 144-148, verified by inspection), but `RunCycle_StoreHit_SkipsSendAndCompletesWithoutThrowing` uses `NullLogger` and asserts only the skip and normal completion. The behavioral core of the criterion (skip, normal outcome) is test-verified; the log content is inspection-verified. Recommended follow-up (non-blocking): assert the log event via a capturing logger. Detailed in the code review findings table.
- **Async-body instrumentation exclusion (Informational).** The pre-existing `mailbridge.runsettings` coverlet setting `ExcludeByAttribute=...CompilerGeneratedAttribute...` excludes async state-machine bodies from instrumentation solution-wide. The new worker consult/record logic (Pipeline.cs lines 129-157) and the store's async method bodies therefore contribute no instrumented lines. This is an instrumentation-scope limitation, not a coverage regression; the runsettings file is byte-identical to base on this branch. The behavior is fully covered by the five dedupe tests and eight repository tests with fail-before/pass-after evidence. Same disposition as the accepted #99 audit; the recommended runsettings follow-up remains open.
- **Pre-existing partial branches in Pipeline.cs (Informational).** File-level branch coverage is 50% (2/4) because of two pre-existing ternaries on untouched lines 170 (`MailboxUpn()` internal-domain fallback) and 177 (`BuildProposalReply` empty-slots message), identical at baseline. Not attributable to this branch; a follow-up test of the two uncovered arms would close it.

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("command csharpier ... package contains dotnet-csharpier"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70, #80, #19, #18, and #99 audits. The format check ran to EXIT 0 over all 212 files.
- **MCP template/validator tools unavailable:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set (issue #99 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **GitHub CLI unavailable:** `gh` is not installed, so issue cross-verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None.** No tests were removed, skipped, or weakened. `SchedulingWorkerTests.cs` changed only its private `Worker(...)` factory (adds a default not-recorded store mock so the 13 pre-existing worker tests keep their pre-dedupe behavior); all pre-existing assertions are intact and passing.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `d90681c`)

Branch `feature/send-idempotency-dedupe-101`, head `a3521833996bbf66e6d0e0ddedaebfaf8dcc85ec`. Range: `d90681c766d8a9b9cff93fd59bc1989c80632d1f..a3521833996bbf66e6d0e0ddedaebfaf8dcc85ec` (27 files, +1494/-3).

### Files Modified (categories)

1. **`src/OpenClaw.Core/Agent/SentActionKey.cs`** (NEW) — pure static dedupe-key builder with `ProposalReply` constant, per-component fail-fast guards, and the documented no-escaping/colon-free-distinctness contract.
2. **`src/OpenClaw.Core/Agent/Contracts/ISentActionStore.cs`** (NEW) — two-method clock-free store interface (`IsRecordedAsync`, idempotent `RecordAsync` with caller-supplied timestamp).
3. **`src/OpenClaw.Core/CoreCacheRepository.SentActions.cs`** (NEW) — `ISentActionStore` implementation partial: exact-key `SELECT 1 ... LIMIT 1`, `INSERT ... ON CONFLICT(dedupe_key) DO NOTHING` with parsed component columns and ISO-8601 `O` timestamp, lazy once-per-instance schema-ensure guard, malformed-key `ArgumentException` guard.
4. **`src/OpenClaw.Core/CoreCacheRepository.Schema.cs`** (MODIFIED) — `sent_actions` DDL appended to `CreateTablesSql` (additive `CREATE TABLE IF NOT EXISTS`).
5. **`src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs`** (MODIFIED) — `ISentActionStore sentActionStore` added to the primary constructor.
6. **`src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`** (MODIFIED) — consult/skip-with-structured-log/record logic inside the `SendEnabled` else-branch; record uses `timeProvider.GetUtcNow()` after a successful send.
7. **`src/OpenClaw.Core/Program.cs`** (MODIFIED) — singleton `ISentActionStore` registration resolving to the existing `CoreCacheRepository` instance.
8. **`tests/OpenClaw.Core.Tests/`** — `Agent/SentActionKeyTests.cs` (NEW), `Agent/SentActionKeyPropertyTests.cs` (NEW, CsCheck), `CoreCacheRepositorySentActionsTests.cs` (NEW), `Agent/Runtime/SchedulingWorkerDedupeTests.cs` (NEW), `Agent/Runtime/SchedulingWorkerTests.cs` (helper factory extended only).
9. **`docs/features/active/2026-07-02-send-idempotency-dedupe-101/**`** (NEW, 15 files) — issue/spec/user-story/plan and canonical evidence (baseline, qa-gates, regression-testing, other).

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The C# change passes formatting, linting, nullable type-checking, architecture-boundary enforcement, the full unit-test suite, and the uniform coverage gates, all independently re-run by the reviewer at branch head. The T1 property-test obligation for the new pure function is satisfied with three CsCheck properties. Fail-before/pass-after regression evidence is present and discriminates the new dedupe seam exactly. No evidence-location or file-size violations. No new suppressions or banned APIs; the deterministic clock seam is used for the recorded timestamp. The `modified-workflow-needs-green-run` rule does not fire (verified: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (one pure builder + one two-method seam + one repository partial; no new dependencies)
- Module & File Structure: PASS (all files under 500 lines)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-fast guards; no new catch blocks; failure propagates to the existing isolation boundary)
- Deterministic Clock: PASS (injected `TimeProvider`; clock-free store)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (90.63%/80.25% pooled; new files 100%/100%; changed lines covered; no regression)
- Test Structure: PASS
- External Dependencies: PASS (Moq seams + in-memory SQLite, no temp files)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq repo convention; tests/ mirror)
- Determinism: PASS (FakeTimeProvider; seeded property sampling with seed printing; no sleeps/wall clock)
- T1 obligations: PASS (three property tests for the new pure function; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 701/701 runnable solution tests passing (5 pre-existing environment-gated skips)
- 90.63% pooled line coverage, 80.25% pooled branch coverage (gates: 85%/75%)
- T1 `OpenClaw.Core` package: 98.66% line / 92.07% branch
- New files: SentActionKey.cs 100%/100%, CoreCacheRepository.SentActions.cs 100%/100% (instrumented)
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- Architecture boundary: 2/2 NetArchTest tests pass

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, regression evidence, and policy requirements pass against branch head `a352183`. No remediation inputs are required. This feature closes the accepted duplicate-send interim risk carried from #99; the at-least-once window (crash between send and record) is explicitly documented and accepted for Stage 0.

---

## Appendix A: Test Inventory

C# test changes in this feature (all in `tests/OpenClaw.Core.Tests/`):

1. `Agent/SentActionKeyTests.cs` (NEW, 78 lines) — `Build_WithValidComponents_ReturnsColonJoinedKeyInFixedOrder`, `ProposalReply_Constant_IsProposalReplyLiteral`, and three parameterized guard tests (`Build_WithInvalidMailbox_...`, `Build_WithInvalidMessageId_...`, `Build_WithInvalidActionType_...`; 3 `[DataRow]` rows each asserting the named parameter).
2. `Agent/SentActionKeyPropertyTests.cs` (NEW, 81 lines) — three CsCheck properties (`Build_IsDeterministic_SameTripleAlwaysYieldsSameKey`, `Build_ColonFreeComponents_SplitYieldsComponentsInFixedOrder`, `Build_DistinctColonFreeTriples_YieldDistinctKeys`; 1000 iterations each; colon-free non-whitespace generators; failing seed printed per suite convention).
3. `CoreCacheRepositorySentActionsTests.cs` (NEW, 212 lines) — round-trip, duplicate idempotency with row-count verification, ISO-8601 `O` UTC timestamp round-trip, `InitializeAsync` idempotency, pre-existing-database upgrade, 4-row malformed-key negative test, lazy schema-ensure. In-memory shared-cache SQLite exclusively.
4. `Agent/Runtime/SchedulingWorkerDedupeTests.cs` (NEW, 296 lines) — hit-skip, miss-send-record with proven call order and `FakeTimeProvider` timestamp, failure no-record with next-candidate isolation, kill-switch store-untouched composition, restart persistence over one shared in-memory database with a real `CoreCacheRepository` store.
5. `Agent/Runtime/SchedulingWorkerTests.cs` (MODIFIED, helper only) — private `Worker(...)` factory now supplies a default not-recorded `ISentActionStore` mock; all 13 pre-existing tests unchanged and passing.

Reviewer run: `OpenClaw.Core.Tests` 254 passed, 0 failed; solution total 701 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Architecture-boundary tests (subset)
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --no-build --filter "FullyQualifiedName~ArchitectureBoundary"

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-send-idempotency-dedupe-101/evidence/qa-gates/coverage-review"

# Regression subsets (executor fail-before / pass-after evidence)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerDedupeTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SentActionKey"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositorySentActions"

# Evidence-location scan
git diff --name-only d90681c766d8a9b9cff93fd59bc1989c80632d1f..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'

# Banned-API / suppression scan over added C# lines
git diff d90681c766d8a9b9cff93fd59bc1989c80632d1f..HEAD -- 'src/*.cs' 'tests/*.cs' | grep -E '^\+' | grep -E 'DateTime\.(Now|UtcNow)|Random\.Shared|Thread\.Sleep|Task\.Delay|pragma|SuppressMessage'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-02
**Policy Version:** Current (as of audit date)
