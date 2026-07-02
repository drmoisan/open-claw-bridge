# Policy Compliance Audit: one-on-one-move-history (#105)

**Audit Date:** 2026-07-02
**Code Under Test:** C# only. 5 production `.cs` files in `src/OpenClaw.Core` (`Agent/Contracts/ISeriesMoveHistory.cs` NEW, 44 lines, interface-only contract; `Agent/OneOnOneMoveGuard.cs` NEW, 161 lines, pure static guard + `SeriesMoveHistoryAnswers` record; `CoreCacheRepository.SeriesMoves.cs` NEW, 110 lines, `ISeriesMoveHistory` repository partial; `CoreCacheRepository.Schema.cs` MODIFIED, one-line `series_moves` DDL append to `CreateTablesSql`; `Program.cs` MODIFIED, one DI registration), 3 new test `.cs` files (`CoreCacheRepositorySeriesMovesTests.cs`, `Agent/OneOnOneMoveGuardTests.cs`, `Agent/OneOnOneMoveGuardPropertyTests.cs`). Plus feature scoping/evidence Markdown (feature folder for issue #105). No Python, PowerShell, TypeScript, Bash, or governed JSON files changed in the branch diff.

**Scope:** Full feature branch `feature/one-on-one-move-history-105` @ `be385342eb4f5b0ccc999e15730f7450d59a19b9` versus resolved base `main` @ merge-base `ee387dc8c8498148a0220bd8107b22161c5d444a` (origin/main; the local `main` ref is stale per the caller inputs). Scope is feature-vs-base over the complete branch diff. Diff file breakdown (name-only): 8 `.cs`, 14 `.md` (22 files, +1834/-1). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md` (mirrored in `issue.md`).

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 5 production `.cs` + 3 test `.cs` | 779 (solution) / 327 (Core.Tests) | 774 pass, 0 fail, 5 env-gated skips | 90.81% line, 80.62% branch (pooled solution) | 90.91% line, 80.74% branch (pooled solution, reviewer-parsed; counts identical to executor evidence) | OneOnOneMoveGuard.cs (new) 100.00% line / 100.00% branch reviewer-parsed; CoreCacheRepository.Schema.cs 100.00% line / 100.00% branch; Program.cs 100.00% line, no branch points; CoreCacheRepository.SeriesMoves.cs uninstrumented (all-async file under the pre-existing runsettings CompilerGenerated attribute exclusion, behaviorally covered by 12 dedicated tests); ISeriesMoveHistory.cs interface-only, legitimately reports no executable coverage per policy |

**Note:** Python, PowerShell, Bash, TypeScript, and governed-JSON rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit PASS.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/baseline/baseline-test-coverage.2026-07-02T14-04.md` (pooled 90.81% line / 80.62% branch; raw cobertura at `artifacts/csharp/baseline-105/`)
- C# post-change coverage artifact: `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` and `evidence/qa-gates/coverage-comparison.2026-07-02T14-20.md` (pooled 90.92% line / 80.74% branch as rendered by the executor; same raw counts as the reviewer parse)
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head): `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/coverage-review/{6915552b...,6c6a1c44...,f9acace2...}/coverage.cobertura.xml`; independently parsed pooled 90.91% line (4353/4788) / 80.74% branch (998/1236), counts identical to executor evidence. Reviewer evidence: `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/coverage-review.2026-07-02T14-35.md`
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura. The C# coverage gate is met (pooled line 90.91% >= 85%, branch 80.74% >= 75%; the only instrumented new file is at 100.00% line / 100.00% branch; both modified files are at 100.00% line; no regression — pooled coverage improved by +0.10pp line / +0.12pp branch and the HostAdapter/MailBridge report counts are unchanged vs baseline).

---

## Executive Summary

This feature branch closes issue #105 (gap F8): persisted per-series move history plus a pure 1:1 move guard implementing master Section 10.3. Three additions mirroring the issue-#101 `ISentActionStore` pattern: (a) `ISeriesMoveHistory` contract in `Agent/Contracts` (record one accepted move idempotently; query distinct moved occurrence starts most-recent-first; caller-supplied timestamps, no clock dependency); (b) a `CoreCacheRepository.SeriesMoves` partial persisting to a new `series_moves` SQLite table (`CREATE TABLE IF NOT EXISTS` in both `CreateTablesSql` and a lazy once-per-instance schema-ensure guard; `ON CONFLICT ... DO NOTHING` insert; round-trip "O" UTC strings; parameterized SQL); (c) a pure static `OneOnOneMoveGuard` (`ComputeAnswers` window arithmetic per spec D1/D2, `CanMove` allowing a `ONE_ON_ONE` move only when moves-in-last-six-occurrences < 2 AND NOT moved-previous-week and delegating every other kind to `MovePolicy.CanMove` unchanged, `ResolveSeriesKey` per D3). One DI registration in `Program.cs`. There is deliberately no production call site: the guard + store are the seam consumed by Stage 2 organizer reschedule (F18), documented as in-scope-by-design in spec.md D5.

The mandatory toolchain was independently re-run by the reviewer against the branch head `be38534` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 223 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** NetArchTest suite runs inside `OpenClaw.Core.Tests` — included in the 327/327 pass.
- **Tests + coverage:** full solution `dotnet test` with `--collect:"XPlat Code Coverage"` — 774 passed, 0 failed, 5 environment-gated skips (same skips as baseline); pooled coverage 90.91% line / 80.74% branch, above the uniform gates; T1 property-test obligation satisfied by `OneOnOneMoveGuardPropertyTests` (CsCheck, 4 properties, 1000 iterations each: monotonicity, delegation equivalence, window invariance, previous-week half-open-interval oracle).
- **Regression evidence:** `MovePolicy.cs`, `RecurringMeetingClassifier.cs`, and `NormalizedMeetingContext.cs` are absent from the diff (reviewer-verified); the 17 existing MovePolicy/classifier tests pass unmodified (`evidence/regression-testing/movepolicy-regression.2026-07-02T14-17.md`, reconfirmed inside the reviewer's 327/327 Core.Tests run).

No Blocking findings. No material PARTIAL findings. Two informational observations (async-body instrumentation scope for the new repository partial — pre-existing runsettings behavior, same disposition as the accepted #99/#103 audits; and `ResolveSeriesKey` covered by exhaustive directed partition tests rather than a CsCheck property — see Section 8 and the code review). Remediation is not required. The feature is recommended Go for PR.

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
- No temporary or throwaway scripts were introduced by this feature; the diff is five production files, three test files, and documentation/evidence Markdown. The executor's raw cobertura intermediates under `artifacts/csharp/baseline-105/` and `artifacts/csharp/final-105/` are untracked (gitignored) and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`ee387dc`), the checked-out feature branch, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observation (not a narrowing instruction, recorded for completeness): the PR-context summary's "Changed files overview" reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only (14 files). That categorization is inaccurate; the authoritative `git diff ee387dc..be38534` contains 5 production C# files and 3 test C# files (22 files total, +1834/-1). This is the fourth consecutive review (#99, #101, #103, #105) where the summary miscategorizes a C# branch as docs-only. The audit used the authoritative git diff file list, not the summary categorization. Related parsing noise: the summary's author-asserted autoclose list contains non-issue tokens (`#AC-1`..`#AC-5`, `#ISO-8601`) lifted from acceptance-criteria labels; only #105 is the closing issue. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only ee387dc..HEAD | grep -E '^artifacts/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/<kind>/` locations (baseline, qa-gates, regression-testing, other).
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own evidence was written to the canonical `evidence/qa-gates/` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, #18, #99, #101, and #103 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/baseline-105/` and `artifacts/csharp/final-105/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Each repository test creates a uniquely named in-memory shared-cache SQLite database (`core-sm-{label}-{Guid:N};Mode=Memory;Cache=Shared`); guard tests are pure with per-test inputs; no shared mutable state. 327/327 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | 12 repository cases each pin one store behavior (round-trip, duplicate idempotency, normalization, ordering, isolation, init idempotency, upgrade, lazy ensure, blank-key x3, restart); 27 guard unit cases pin one rule each (six truth-table cells via DataRows, one boundary or guard per method). |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 327 tests in ~1 s (reviewer run); property tests are in-memory CsCheck sampling; SQLite tests use in-memory shared cache. |
| **Determinism** | PASS | All timestamps are fixed literals (candidate anchored at 2026-07-06T15:00Z); the store is clock-free by design (caller-supplied timestamps, reviewer-verified zero clock reads in the new files); CsCheck properties use the suite's seeded `Gen`/`Sample` convention (failing seed printed); no sleeps, timers, network, or filesystem. |
| **Readability & Maintainability** | PASS | Descriptive scenario names (`RecordMoveAsync_duplicate_pair_should_not_throw_and_leave_one_row`, `ComputeAnswers_SeventhMostRecentOccurrence_IsOutsideWindow`), FluentAssertions with because-messages, XML doc summaries on all three new test classes citing the AC each covers, scenario letters (a)-(j) mapped to the plan. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline pooled: 90.81% line (4298/4733), 80.62% branch (990/1228). Source: `evidence/baseline/baseline-test-coverage.2026-07-02T14-04.md`. |
| **No Coverage Regression** | PASS | Post-change pooled: 90.91% line, 80.74% branch (+0.10pp / +0.12pp, reviewer-parsed). HostAdapter and MailBridge report counts identical to baseline; the delta is entirely in `OpenClaw.Core` (98.74% -> 98.78% line, 91.79% -> 91.95% branch). Modified files `Schema.cs` and `Program.cs` at 100.00% line post-change. |
| **New Code Coverage** | PASS | `OneOnOneMoveGuard.cs` (new): 100.00% line (52/52) / 100.00% branch (8/8) — reviewer-parsed, line AND branch, zero partial conditions. `CoreCacheRepository.SeriesMoves.cs` is uninstrumented (all-async file under the pre-existing runsettings CompilerGenerated exclusion) and behaviorally verified by 12 dedicated tests covering both public methods and every branch (see Section 8). `ISeriesMoveHistory.cs` is interface-only and legitimately reports no executable coverage per the general-unit-test policy clarification. |
| **Comprehensive Coverage** | PASS | Store round-trip/idempotency/normalization/ordering/isolation/migration/upgrade/lazy-ensure/validation/restart; guard truth table, window boundaries (exactly six, fewer than six, future anchors), previous-week boundaries (-7/-8/-1/0 days), same-anchor double move, key fallback chain, null guards; four CsCheck invariants. |
| **Positive Flows** | PASS | Record/query round-trip; allowed truth-table cells (0 and 1 moves, no previous-week move); `ResolveSeriesKey` returning `SeriesMasterId`. |
| **Negative Flows** | PASS | Blank series key throws `ArgumentException` with parameter name (null/empty/whitespace DataRows); blocked truth-table cells; double-null identity throws; eight null-argument guard tests. |
| **Edge Cases** | PASS | Exactly-six-occurrence window boundary (seventh-most-recent excluded); fewer-than-six young-series window; -7-day closed lower bound vs -8-day exclusion; candidate's own anchor not previous-week; two moves on one UTC anchor date counted per row; future occurrence anchors excluded; non-UTC offset normalized to "O"-format UTC. |
| **Error Handling** | PASS | Fail-fast `ArgumentException`/`ArgumentNullException` guards pinned by tests; duplicate insert is a designed no-op (`ON CONFLICT DO NOTHING`) proven with a direct row-count query; no catch blocks in the new production code. |
| **Concurrency** | N/A | No new concurrency surface; the lazy schema-ensure flag races only to a harmless duplicate `CREATE TABLE IF NOT EXISTS` (documented in the XML doc). |
| **State Transitions** | PASS | Restart persistence proven with a second repository instance over the same shared in-memory database; pre-existing-database upgrade proven by seeding a pre-#105 schema and running `InitializeAsync`. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 90.81% line, 80.62% branch (pooled solution) -> Post-change: 90.91% line, 80.74% branch. Change: +0.10% line, +0.12% branch. New/changed-code coverage: OneOnOneMoveGuard.cs (new) 100.00% line / 100.00% branch reviewer-parsed; CoreCacheRepository.Schema.cs 100.00% line / 100.00% branch; Program.cs 100.00% line with no branch points; CoreCacheRepository.SeriesMoves.cs uninstrumented per the pre-existing runsettings attribute exclusion and behaviorally covered by 12 dedicated tests; ISeriesMoveHistory.cs interface-only per policy; new test files excluded from measurement per policy. Disposition: PASS (line >= 85%, branch >= 75%, no regression on changed lines). Evidence: `evidence/baseline/baseline-test-coverage.2026-07-02T14-04.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T14-20.md`, reviewer re-run `evidence/qa-gates/coverage-review.2026-07-02T14-35.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions because-clauses on every material assertion ("a duplicate record must not add a second row", "the previous-week interval is [candidate anchor - 7 days, candidate anchor)"); CsCheck prints the failing seed per suite convention; `WithParameterName` pins the throwing parameter. |
| **Arrange-Act-Assert Pattern** | PASS | All new tests carry explicit `// Arrange` / `// Act` / `// Assert` comments (pure one-liner assertions in the ResolveSeriesKey group remain clearly structured). |
| **Document Intent** | PASS | XML docs on all three new test classes state the AC(s) covered, the no-temp-files SQLite strategy, and the seed-printing determinism note; inline comments map scenarios (a)-(j) to the plan. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No network, COM, or external process; repository tests use in-memory shared-cache SQLite per the established `CoreCacheRepositorySentActionsTests` convention; the guard is pure and needs no doubles. |
| **Use Mocks/Stubs** | PASS | No mocks needed (real in-memory repository per spec; Moq deliberately unused as the spec anticipated); direct anchor-connection SQL used only to verify stored state. |
| **Environment Stability** | PASS | No temporary files (in-memory SQLite only; executor grep scan `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` section (c), zero matches); no mutable global state; no environment variables read. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #105, `spec.md` v0.2 (five design decisions D1-D5 with verified code anchors), user-story scenarios, and the F8 gap-analysis reference define the change precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-02T13-48.md` present. |
| **Document the plan** | PASS | `plan.2026-07-02T13-48.md` with per-phase evidence under `evidence/**`; completed tasks recorded in the PR-context summary. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | One interface, one repository partial, one pure static class, one DI line; window arithmetic is a single LINQ pipeline; no new dependencies, no new abstractions beyond the spec-sanctioned contract. |
| **Reusability** | PASS | Mirrors the established `ISentActionStore` partial-per-store pattern exactly (per-call `Open()`, lazy schema-ensure, "O"-format UTC strings, `ON CONFLICT DO NOTHING`); reuses `MeetingContextNormalizer.NormalizeEmail` and `RecurringMeetingClassifier.Classify` rather than duplicating normalization/classification. |
| **Extensibility** | PASS | `ISeriesMoveHistory` is a narrow two-method contract shaped for the Section 10.3 predicates; `SeriesMoveHistoryAnswers` decouples the guard from the store so F18 can supply answers from any source; occurrence lists are caller-supplied per D2 with the conservative-degradation contract documented. |
| **Separation of concerns** | PASS | Pure logic (`OneOnOneMoveGuard`) in Agent with no I/O/clock/store reference; persistence isolated in the repository partial; the only composition point is DI registration. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Contract in `Agent/Contracts/`, pure guard in `Agent/`, persistence partial at repository root — matching the existing layout; tests mirror production paths. |
| **Under 500 lines** | PASS | `wc -l` (reviewer): ISeriesMoveHistory.cs 44, OneOnOneMoveGuard.cs 161, CoreCacheRepository.SeriesMoves.cs 110, CoreCacheRepository.Schema.cs 254, Program.cs 332, CoreCacheRepositorySeriesMovesTests.cs 321, OneOnOneMoveGuardTests.cs 352, OneOnOneMoveGuardPropertyTests.cs 265. All under the 500-line cap (max 352); matches executor evidence `evidence/other/scope-and-size-verification.2026-07-02T14-25.md`. |
| **Public vs internal** | PASS | The public additions are the spec-sanctioned contract, guard, and answers record; the repository partial stays `internal sealed partial`; the DDL constant and schema-ensure guard are `private`. |
| **No circular dependencies** | PASS | No project-reference changes; NetArchTest boundary suite passes inside the 327/327 Core.Tests run. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `ISeriesMoveHistory`, `RecordMoveAsync`/`GetMovedOccurrenceStartsAsync` (Async suffix), `SeriesMoveHistoryAnswers`, `MovesInLastSixOccurrences`, `MovedPreviousWeek`, `ResolveSeriesKey`, `Anchor`; SQL columns `series_key`/`occurrence_start_utc`/`moved_at_utc`. |
| **Docs/docstrings** | PASS | XML docs on every public member citing issue #105 and master Section 10.3; `ComputeAnswers` documents the D1/D2 anchor and half-open-interval semantics and the conservative caller-supplied-list contract; the schema-ensure guard documents the benign concurrency race. |
| **Comment why, not what** | PASS | Comments explain the window-membership-per-anchor vs count-per-row distinction, the idempotency rationale, and the restart-persistence anchor-connection technique in tests. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 223 files, EXIT 0. Executor: `evidence/qa-gates/final-format.2026-07-02T14-20.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. |
| **4. Architecture** | PASS | NetArchTest boundary tests pass within the full Core.Tests run (327/327). No COM, VSTO, or interop references added; persistence stays behind the `ISeriesMoveHistory` contract. |
| **5. Testing** | PASS | Reviewer: full solution test run — 774 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the T1 property-based tests. |
| **6. Contract/schema checks** | N/A | No governed wire contract, DTO, route, or schema surface changed; the SQLite DDL is additive (`CREATE TABLE IF NOT EXISTS` only, no ALTER, no backfill), with fresh-database and upgrade paths both tested. |
| **7. Integration tests** | N/A | No adapter/external-system boundary changed; the repository-level behavior is covered against a real in-memory SQLite `CoreCacheRepository` per spec. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass Phase loop (qa-gates set at 2026-07-02T14-20 after one formatting restart documented in `final-format`). |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in `evidence/qa-gates/coverage-review.2026-07-02T14-35.md`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy matches the delivered diff exactly (three new production files, two modified, three new test files); the commit message describes the feature. |
| **Design choices explained** | PASS | D1 (UTC-day anchor + half-open 7-day window instead of ISO weeks, with fail-safe rationale), D2 (caller-supplied occurrence dates with the move-records-only deadlock explained), D3 (series-key fallback chain), D4 (pure static guard leaving `MovePolicy` untouched), D5 (no production consumer yet — in-scope-by-design F18 seam) all documented in spec.md. |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `spec.md`, `user-story.md`, and the `issue.md` mirror. |
| **Provide next steps** | PASS | Spec D5 records the F18 obligations (consume the guard, record moves when and only when a move is taken); Constraints & Risks records the conservative-degradation and partial-accumulation accepted risks. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, TypeScript, and governed-JSON sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70, #80, #19, #18, #99, #101, and #103 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; new code uses `ArgumentNullException.ThrowIfNull`, `string.IsNullOrEmpty`/`IsNullOrWhiteSpace` guards, and non-nullable signatures throughout. |
| **Null-safety** | PASS | Every reference argument on the public guard surface is guarded; `ResolveSeriesKey` fails fast on double-null identity rather than producing an unkeyed row; store rows parse with `DateTimeStyles.RoundtripKind`. |
| **Async / resource safety** | PASS | `await using` for connections and readers; cancellation token forwarded to every ADO call; `Async` suffix on all async members; no fire-and-forget, no blocking waits, no `async void`. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces in all new files; PascalCase publics; interface `I` prefix; parameterized SQL with `$`-prefixed named parameters per repo convention. |
| **Exceptions fail-fast** | PASS | `ArgumentException` with parameter name for blank series key (before opening a connection); `ArgumentNullException.ThrowIfNull` at the pure entry points; no catch blocks added anywhere in production. |
| **No new suppressions and no banned APIs** | PASS | Reviewer grep of the added C# diff lines for pragma, SuppressMessage, DateTime.Now, DateTime.UtcNow, Random.Shared, Thread.Sleep, Task.Delay, GetTempPath, GetTempFileName returned zero matches. The store and guard are clock-free by design (caller-supplied timestamps; the executor's clock-read scan in `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` section (d) confirms zero clock reads in the new files). |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions (+ CsCheck; Moq available but correctly unused here). The new tests follow the established repo convention, consistent with the prior validated #70, #80, #19, #18, #99, #101, and #103 audits. Pre-existing repo-wide divergence, not a finding against this branch.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataRow]` with DisplayName; FluentAssertions matchers incl. `WithParameterName`; CsCheck `Gen`/`Sample` per suite convention. |
| **Test file location** | PASS | `tests/OpenClaw.Core.Tests/CoreCacheRepositorySeriesMovesTests.cs` mirrors `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs`; `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuard*Tests.cs` mirror `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs`. No colocation in the production tree. |
| **Coverage expectation** | PASS | Pooled 90.91% line / 80.74% branch; instrumented new/changed files at 100.00% line and branch; uninstrumented repository partial behaviorally covered (Section 8); no regression. |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); the new algorithmic pure functions (`ComputeAnswers`, `CanMove`) carry four genuine CsCheck properties at 1000 iterations each (monotonicity — adding a recorded move never flips blocked to allowed; delegation equivalence to `MovePolicy.CanMove` over generated non-1:1 contexts; window invariance under strictly-older moves; previous-week membership vs an independent half-open-interval oracle). CsCheck 4.7.0 was already referenced by `OpenClaw.Core.Tests`; no new dependency. `ResolveSeriesKey` (a three-branch total selector) is covered by exhaustive directed partition tests rather than a property; see Section 8 (Info) — consistent with the #103 grading where the density gate applied to the algorithmic pure logic. |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80/#99/#103 T1 audits). |
| **Determinism (no sleeps, no wall clock)** | PASS | Fixed literal timestamps throughout; store is clock-free; CsCheck seeded with failing-seed printing; no `Thread.Sleep`/`Task.Delay`/timers in the diff; no `FakeTimeProvider` needed because no component under test reads a clock (spec-documented). |
| **No temporary files** | PASS | In-memory shared-cache SQLite (`Data Source=core-sm-{label}-{Guid:N};Mode=Memory;Cache=Shared`); anchor connections keep databases alive; zero filesystem artifacts. |
| **Focused / isolated** | PASS | Fresh uniquely named database per test; direct anchor-connection SQL used only for state verification; each guard test builds its own inputs. |

---

## 5. Test Coverage Detail

### CoreCacheRepositorySeriesMovesTests (10 methods / 12 cases, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `RecordMoveAsync_then_query_should_round_trip_occurrence_starts` | Positive (round-trip, AC-1) | PASS |
| `RecordMoveAsync_duplicate_pair_should_not_throw_and_leave_one_row` | Idempotency (ON CONFLICT DO NOTHING, direct row-count verification) | PASS |
| `RecordMoveAsync_should_normalize_non_utc_offset_to_utc_o_form` | Normalization (stored string asserted byte-exact) | PASS |
| `GetMovedOccurrenceStartsAsync_should_return_most_recent_first` | Ordering (recorded out of order) | PASS |
| `GetMovedOccurrenceStartsAsync_should_isolate_series_keys` | Isolation (key A invisible to key B) | PASS |
| `InitializeAsync_twice_should_not_throw_and_series_moves_should_exist` | Migration idempotency (fresh path) | PASS |
| `InitializeAsync_should_add_series_moves_to_pre_existing_database` | Upgrade (seeded pre-#105 schema) | PASS |
| `Store_methods_should_work_on_fresh_database_without_InitializeAsync` | Lazy schema-ensure (both store methods) | PASS |
| `RecordMoveAsync_blank_key_should_throw_ArgumentException` (3 DataRows) | Negative (null/empty/whitespace key) | PASS |
| `Second_repository_instance_should_see_rows_recorded_by_first_instance` | Restart persistence (two instances, one shared database) | PASS |

### OneOnOneMoveGuardTests (17 methods / 27 cases, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `CanMove_OneOnOne_TruthTable` (6 DataRows) | Truth table (moves 0/1/2 x previous-week yes/no) | PASS |
| `ComputeAnswers_SeventhMostRecentOccurrence_IsOutsideWindow` | Boundary (exactly six anchors; seventh excluded) | PASS |
| `ComputeAnswers_FewerThanSixOccurrences_AllAnchorsFormWindow` | Edge (young series) | PASS |
| `ComputeAnswers_PreviousWeekBoundaries` (4 DataRows) | Boundary (-7 blocked / -8 allowed / -1 blocked / same-anchor not previous-week) | PASS |
| `ComputeAnswers_TwoMovesOnSameAnchorDate_CountAsTwo` | Semantics (count per move row, membership per anchor) | PASS |
| `ComputeAnswers_OccurrenceAnchorsAfterCandidate_AreExcludedFromWindow` | Edge (future anchors) | PASS |
| `ResolveSeriesKey_ReturnsSeriesMasterId_WhenPresent` / `_FallsBackToEventId_WhenSeriesMasterIdIsNull` / `_WhenSeriesMasterIdIsEmpty` | Fallback chain (D3) | PASS |
| `ResolveSeriesKey_Throws_WhenBothIdsAreNullOrEmpty` (2 DataRows) | Negative (double-null identity) | PASS |
| 8 null-argument guard tests (`ComputeAnswers` x2, `CanMove` x5, `ResolveSeriesKey` x1) | Negative (fail-fast contracts) | PASS |

### OneOnOneMoveGuardPropertyTests (4 CsCheck properties, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `CanMove_OneOnOne_IsMonotonicallyBlockingInMoveHistory` | Property (monotonicity, 1000 iters — AC-2) | PASS |
| `CanMove_NonOneOnOneKinds_DelegateToMovePolicyUnchanged` | Property (delegation equivalence over generated contexts — AC-3) | PASS |
| `ComputeAnswers_MovesOlderThanWindow_DoNotChangeWindowCount` | Property (window invariance) | PASS |
| `ComputeAnswers_MovedPreviousWeek_MatchesHalfOpenIntervalOracle` | Property (independent interval oracle) | PASS |

**Coverage:** `OneOnOneMoveGuard.cs` 100.00% line / 100.00% branch (new file, reviewer-parsed); `Schema.cs` and `Program.cs` 100.00% line; `SeriesMoves.cs` uninstrumented per the pre-existing runsettings attribute exclusion, behaviorally covered as above. **Gap:** none attributable to this branch.

**Regression:** `MovePolicy.cs`, `RecurringMeetingClassifier.cs`, and `NormalizedMeetingContext.cs` are absent from the diff (reviewer grep); the 17 existing MovePolicy/classifier tests pass unmodified (`evidence/regression-testing/movepolicy-regression.2026-07-02T14-17.md`, filter run EXIT 0), reconfirmed inside the reviewer's 327/327 Core.Tests run. The delegation-equivalence property provides continuous regression pressure beyond the fixed suites.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 779 (774 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 327 passed / 327 | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~1 s | PASS |
| Pooled Code Coverage | 90.91% line, 80.74% branch | PASS |
| New guard file (T1, new code) | 100.00% line, 100.00% branch | PASS |
| Net new tests vs baseline | +43 (12 repository cases + 27 guard unit cases + 4 CsCheck properties) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 223 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest) | Included in `dotnet test` Core.Tests run | 327/327 pass (boundary tests included) | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/coverage-review"` | 774 passed, 0 failed, 5 skipped | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `be38534` on 2026-07-02. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** Two observations, recorded (not policy violations on this branch):

- **Async-body instrumentation exclusion (Informational).** The pre-existing `mailbridge.runsettings` coverlet setting `ExcludeByAttribute=CompilerGeneratedAttribute` excludes async state-machine bodies from instrumentation solution-wide. Every member of the new `CoreCacheRepository.SeriesMoves.cs` is async, so the file contributes zero instrumented lines; per-line cobertura cannot attest it. Per the disposition accepted on the #99 and #103 reviews, the reviewer verified the file behaviorally instead: the 12 dedicated repository tests exercise both public methods and every branch — blank-key `ArgumentException` (all three DataRow arms), the `ON CONFLICT DO NOTHING` duplicate path (verified with a direct row-count query), non-UTC normalization (stored string asserted byte-exact), descending order, series isolation, both flag states of the lazy schema-ensure guard (fresh database without `InitializeAsync`, then repeated calls), migration idempotency, pre-existing-database upgrade, and restart persistence across two repository instances. The runsettings file is byte-identical to base on this branch; the setting is an attribute-level filter, not a production-path `exclude` entry of the kind the coverage-exclusion policy prohibits. The recommended runsettings follow-up remains open (also recorded on #99 and #103). The async members of the pre-existing `CoreCacheRepository.SentActions.cs` are measured identically, confirming this is the established measurement configuration, not something introduced here.
- **`ResolveSeriesKey` covered by directed partition tests, not a CsCheck property (Informational).** The T1 property-density gate is satisfied for the algorithmic pure functions (`ComputeAnswers`, `CanMove`) with four genuine properties. `ResolveSeriesKey` is a three-branch total selector whose complete input partition (non-empty `SeriesMasterId`; null/empty master with non-empty `EventId`; both null; both empty) is exhaustively enumerated by five deterministic directed tests — stronger than random sampling over the same partition. Consistent with the #103 grading, where the density gate applied to the new algorithmic pure function. Optional hardening recorded in the code review: a small property (any non-empty `SeriesMasterId` is returned verbatim) would make the rule uniformity-proof. Not a gate failure.

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("command csharpier ... package contains dotnet-csharpier"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70, #80, #19, #18, #99, #101, and #103 audits. The format check ran to EXIT 0 over all 223 files.
- **MCP template/validator tools unavailable:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set (issue #103 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **GitHub CLI unavailable:** `gh` is not installed, so issue cross-verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None removed.** No existing test was deleted, modified, or weakened; the diff touches no existing test file. The 5 solution skips are pre-existing environment-gated COM/publish tests, unchanged.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `ee387dc`)

Branch `feature/one-on-one-move-history-105`, head `be385342eb4f5b0ccc999e15730f7450d59a19b9` (single commit: "feat(core): recurring 1:1 move-history persistence and move guard"). Range: `ee387dc8c8498148a0220bd8107b22161c5d444a..be385342eb4f5b0ccc999e15730f7450d59a19b9`.

### Files Modified (categories)

1. **`src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs`** (NEW, 44 lines) — two-method durable move-history contract; idempotent record with caller-supplied timestamps; distinct occurrence starts most-recent-first.
2. **`src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs`** (NEW, 161 lines) — `SeriesMoveHistoryAnswers` record; pure static `ComputeAnswers` (D1/D2 window arithmetic), `CanMove` (Section 10.3 rule for `ONE_ON_ONE`, delegation to `MovePolicy.CanMove` otherwise), `ResolveSeriesKey` (D3 fallback chain).
3. **`src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs`** (NEW, 110 lines) — `ISeriesMoveHistory` partial mirroring the `SentActions` pattern: lazy schema-ensure, parameterized `ON CONFLICT DO NOTHING` insert, round-trip "O" UTC storage, `RoundtripKind` parsing.
4. **`src/OpenClaw.Core/CoreCacheRepository.Schema.cs`** (MODIFIED) — `series_moves` DDL appended to `CreateTablesSql` (fresh-database path).
5. **`src/OpenClaw.Core/Program.cs`** (MODIFIED) — `ISeriesMoveHistory` singleton registration resolving the existing `CoreCacheRepository`, adjacent to the `ISentActionStore` registration.
6. **`tests/OpenClaw.Core.Tests/**`** — 3 new test files (repository suite, guard unit suite, guard property suite; 43 new cases).
7. **`docs/features/active/2026-07-02-one-on-one-move-history-105/**`** (NEW, 14 files) — issue/spec/user-story/plan and canonical evidence (baseline, qa-gates, regression-testing, other).

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The C# change passes formatting, linting, nullable type-checking, architecture-boundary enforcement, the full unit-test suite, and the uniform coverage gates, all independently re-run by the reviewer at branch head. The T1 property-test obligation is satisfied with four genuine CsCheck properties over the new algorithmic pure functions, including the AC-mandated monotonicity property and a delegation-equivalence property that continuously proves non-1:1 behavior is unchanged. Regression is proven both by diff absence (`MovePolicy.cs`/classifier untouched) and by the unmodified passing suites. No evidence-location or file-size violations. No new suppressions or banned-API additions; the new persistence and guard code is clock-free by design. The `modified-workflow-needs-green-run` rule does not fire (verified: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (one contract, one partial, one pure class, one DI line; established pattern mirrored)
- Module & File Structure: PASS (all files under 500 lines, max 352)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-fast guards; no new catch blocks; designed idempotent no-op documented and tested)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (90.91%/80.74% pooled; guard 100%/100%; changed lines covered or behaviorally verified)
- Test Structure: PASS
- External Dependencies: PASS (in-memory SQLite, no temp files, no mocks needed)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions repo convention; tests/ mirror)
- Determinism: PASS (fixed literal timestamps; clock-free store; seeded CsCheck)
- T1 obligations: PASS (four CsCheck properties incl. monotonicity and delegation equivalence; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 774/774 runnable solution tests passing (5 pre-existing environment-gated skips)
- 90.91% pooled line coverage, 80.74% pooled branch coverage (gates: 85%/75%)
- New T1 guard file: 100.00% line / 100.00% branch (reviewer-parsed, line and branch)
- No regression: pooled +0.10pp line / +0.12pp branch; HostAdapter and MailBridge report counts unchanged vs baseline
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- All 8 touched `.cs` files under the 500-line cap (max 352)

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, regression evidence, and policy requirements pass against branch head `be38534`. No remediation inputs are required. Operational note (from spec, not a gate): the guard and store have no production call site by design; Stage 2 organizer reschedule (F18) is the consumer and carries the obligations to consult the guard before displacing a 1:1 and to record a move when (and only when) a move action is actually taken.

---

## Appendix A: Test Inventory

C# test changes in this feature (all in `tests/OpenClaw.Core.Tests/` and `tests/OpenClaw.Core.Tests/Agent/`):

1. `CoreCacheRepositorySeriesMovesTests.cs` (NEW, 321 lines) — 10 methods / 12 cases: record/query round-trip, duplicate-pair idempotency with direct row-count verification, non-UTC-offset normalization with byte-exact stored-string assertion, descending order, series-key isolation, `InitializeAsync` idempotency, pre-existing-database upgrade (seeded pre-#105 schema), lazy schema-ensure without `InitializeAsync`, blank-key `ArgumentException` (null/empty/whitespace DataRows), restart persistence across two repository instances over one shared in-memory database.
2. `Agent/OneOnOneMoveGuardTests.cs` (NEW, 352 lines) — 17 methods / 27 cases: six-cell `CanMove` truth table (DataRows), exactly-six-occurrence window boundary, fewer-than-six window, previous-week boundaries (-7/-8/-1/0 days, DataRows), same-UTC-day double move, future-anchor exclusion, `ResolveSeriesKey` fallback chain (3 cases) and double-null throw (2 DataRows), eight null-argument guards.
3. `Agent/OneOnOneMoveGuardPropertyTests.cs` (NEW, 265 lines) — 4 CsCheck properties (1000 iterations each): monotonicity (appending a moved occurrence never flips blocked to allowed), delegation equivalence (guard equals `MovePolicy.CanMove` for all generated non-1:1 contexts regardless of history), window invariance (moves anchored strictly older than the six-anchor window are inert), previous-week membership vs an independent half-open-interval oracle.

Reviewer run: `OpenClaw.Core.Tests` 327 passed, 0 failed; solution total 774 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/coverage-review"

# Regression subset (executor evidence, AC-3)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~MovePolicyTests|FullyQualifiedName~RecurringMeetingClassifier"

# Evidence-location scan
git diff --name-only ee387dc8c8498148a0220bd8107b22161c5d444a..HEAD | grep -E '^artifacts/'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-02
**Policy Version:** Current (as of audit date)
