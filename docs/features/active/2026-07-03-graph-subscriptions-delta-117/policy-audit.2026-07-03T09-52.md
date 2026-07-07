# Policy Compliance Audit: graph-subscriptions-delta (#117) — Remediation Cycle 1 Exit Re-audit

**Audit Date:** 2026-07-03
**Code Under Test:** C# only. Full feature branch at post-remediation head: 20 NEW production `.cs` files (18 under `src/OpenClaw.Core/CloudSync/` plus the 2 repository store partials `CoreCacheRepository.Subscriptions.cs` — now 190 lines after the cycle's fail-fast refactor — and `CoreCacheRepository.DeltaLinks.cs`) and 2 MODIFIED production `.cs` files (`Program.cs` 358 lines, `CoreCacheRepository.Schema.cs` 275 lines). 21 NEW test `.cs` files (19 under `tests/OpenClaw.Core.Tests/CloudSync/`, 2 at the test-project root) now carrying 100 net-new runtime cases (Core.Tests 616 baseline -> 716: 91 from the feature commit + 9 from the remediation commit), including 7 CsCheck properties. Remediation commit `bc8d50a` changed 4 production files already in the diff (the `ReadSubscription` fail-fast refactor and one catch-filter line in each of the three workers) and 7 test files (9 new directed tests plus one additive test-helper parameter). No Python, PowerShell, Bash, or TypeScript files changed in the branch diff.

**Scope:** Full feature branch `feature/graph-subscriptions-delta-117` @ `bc8d50a324a7e5127908c6a9204f36c8dc50850b` (two commits: `f6aea4d` feature + `bc8d50a` remediation cycle 1) versus resolved base `main` @ merge-base `402703c6c18d0131128696147443ef9f41110f3c` (origin/main; the local `main` ref is stale per the caller inputs — reviewer-confirmed `git merge-base origin/main HEAD` resolves the same SHA and the refreshed PR-context artifacts record the same range). Scope is feature-vs-base over the complete branch diff: 89 files, +7766/-5 (43 `.cs`, the remainder Markdown scoping/evidence/audit/memory files). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md` (mirrored in `issue.md`). This is the remediation cycle 1 exit re-audit for Blocking finding B-117-01 (`remediation-inputs.2026-07-03T02-34.md`, `remediation-plan.2026-07-03T02-34.md`).

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 20 production `.cs` (all new) + 2 production `.cs` modified + 21 test `.cs` (all new) | 1168 (solution) / 716 (Core.Tests) | 1163 pass, 0 fail, 5 env-gated skips | 92.34% line, 83.16% branch (pooled solution) | 92.84% line, 83.63% branch (pooled solution, reviewer re-run and re-parsed) | Every instrumented new file at 100.00% line; per-file branch gate now PASSES on every instrumented changed file: GraphSubscriptionManager.cs 4/4 = 100.00% (was 50.00% — B-117-01), CoreCacheRepository.Subscriptions.cs 2/2 = 100.00% (was 50.00% — B-117-01), GraphDeltaReconciler.cs 15/16 = 93.75% (was 75.00% zero-margin — CR-117-02); the sole residual partial arm (reconciler line 243) is structurally unreachable dead code (new Major finding CR-117-05, Section 8); async bodies, auto-property and record-only files remain uninstrumented under the pre-existing runsettings CompilerGenerated exclusion and were verified behaviorally (Section 8); modified Program.cs 254/254 line and 6/6 branch, modified Schema partial 37/37 and 6/6 |

**Note:** Python, PowerShell, Bash, and TypeScript rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit **PASS**: pooled, package, per-file line, and per-file branch dimensions all meet the uniform gates at branch head, and Blocking finding B-117-01 is closed.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/baseline/dotnet-test-coverage.2026-07-03T01-26.md` (line 92.34% / branch 83.16% pooled) and remediation-cycle baseline `evidence/remediation-baseline/dotnet-test-coverage.2026-07-03T09-06.md`
- C# post-change coverage artifact: `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/dotnet-test-coverage.2026-07-03T09-40.md` and `evidence/qa-gates/coverage-verification.2026-07-03T09-40.md`
- Reviewer-regenerated cobertura (this re-audit, fresh `dotnet test` at branch head `bc8d50a`): `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review-r4/{1f4e8dff...,7b9b29cc...,c30667cb...}/coverage.cobertura.xml`; independently parsed pooled 92.84% line (5631/6065) / 83.63% branch (1318/1576) — identical to the executor's committed figures, with per-file line AND branch re-measured for all 22 changed production files
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura at branch head. All uniform gates are met (92.84% line >= 85%, 83.63% branch >= 75%, both improved over baseline; every instrumented new file at 100.00% line; every instrumented changed file at or above 75% branch). No FAIL verdict remains.

---

## Executive Summary

This is the exit re-audit for remediation cycle 1 of issue #117 (gap F14: Graph change-notification subscriptions, thin webhook, `messages/delta` reconciliation in the new `OpenClaw.Core.CloudSync` namespace). The original review (`policy-audit.2026-07-03T02-34.md`) found one Blocking finding, B-117-01: two new production files measured 50.00% instrumented branch coverage, below the uniform 75% per-file gate, with the untaken arms being genuinely untested fail-fast/fallback guards. Remediation commit `bc8d50a` delivered the enumerated fix list from `remediation-inputs.2026-07-03T02-34.md` in full:

- **Fix items 1-2 (B-117-01, manager arms):** two directed tests drive `CreateAsync` with a mocked handler returning body `{}` (missing `id` -> `GraphMappingException` -> `INTERNAL_ERROR` envelope, nothing persisted) and body `null` (JSON-null deserialization -> `JsonException` -> `TRANSPORT_FAILURE` envelope, nothing persisted). Reviewer-verified: `GraphSubscriptionManager.cs` instrumented branch is now 4/4 = 100.00%.
- **Fix item 3, option (a) (B-117-01 + CR-117-04, store arm):** the `?? DateTimeOffset.MinValue` sentinel in `ReadSubscription` was replaced with a fail-fast `InvalidOperationException` naming the subscription id and the `expiration_utc` column (the column is NOT NULL and always written via `RenderUtc`, so an unparseable value is data corruption), plus a directed corrupt-row test through a second shared-cache connection. Reviewer-verified: `CoreCacheRepository.Subscriptions.cs` instrumented branch is now 2/2 = 100.00% and the `MinValue` sentinel no longer appears in the file.
- **Fix item 4 (CR-117-02, reconciler exact-gate arms):** three directed tests pin the `ParseDeltaPage` defensive arms — a page without a `value` property (zero upserts, walk completes), `@removed` entries without an `id` (two Debug `"(unknown)"` skips: property absent and JSON-null id), and an unparseable entry inside `value` (`TRANSPORT_FAILURE` envelope + failed `delta_reconcile` ingest run). Reviewer-verified: `GraphDeltaReconciler.cs` is now 15/16 = 93.75% branch, off the zero-margin gate. The plan's literal-null-entry payload was adjusted to the nearest reachable `JsonException` payload with a recorded deviation, because the literal-null case is structurally unreachable — this surfaces one NEW Major finding (CR-117-05, Section 8): the line-243 `?? throw new JsonException` arm is dead code, and a literal `null` entry escapes as an unhandled `InvalidOperationException` instead of the documented envelope mapping. The failure mode is bounded (Section 8); the production fix was outside this cycle's permitted diff scope and is routed to a follow-up issue, not another remediation cycle.
- **Fix item 5 (CR-117-03, worker cancellation filters):** the three worker loop filters changed from `when (ex is not OperationCanceledException)` to `when (!stoppingToken.IsCancellationRequested)` (exactly one line per file), with one directed `TaskCanceledException`-continues-with-Warning test per worker. All 43 worker tests pass, including the pre-existing shutdown/cancellation tests, demonstrating the stop path is unchanged.

The mandatory toolchain was independently re-run by the reviewer against branch head `bc8d50a` and passes in a single pass: `csharpier check .` (322 files, EXIT 0); `dotnet build OpenClaw.MailBridge.sln` (0 warnings, 0 errors — analyzers + nullable as errors); full-solution `dotnet test` with XPlat coverage into the canonical evidence path — 1163 passed, 0 failed, 5 pre-existing environment-gated skips (Core.Tests 716/716 including the NetArchTest boundary suites); pooled coverage 92.84% line / 83.63% branch, independently parsed and identical to the executor's committed figures. The diff-scope confinement holds: the remediation cycle changed only the files named in the plan's Global Conventions statement, no runsettings/coverage-exclusion/suppression changes, no existing test relaxed or deleted (the remediation's only edit to a pre-existing-in-branch test file body is an additive optional logger parameter in a test helper).

**Blocking findings: none.** B-117-01 is closed with reviewer-independent numeric verification. One NEW Major finding (CR-117-05) and informational observations are recorded in Section 8 and the companion code review; none fails a policy gate. The feature is recommended **Go for PR**, with CR-117-05 and the executor timeout mapping captured as a follow-up issue.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/csharp.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/orchestrator-state.md` (not triggered — no checkpoint changes)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by the feature or the remediation cycle; the cycle's cobertura-parsing helper lived in the agent scratchpad outside the repository. The executor's raw cobertura intermediates under `artifacts/csharp/remediation-{baseline,final}-117/` are untracked (gitignored) tooling outputs and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract for the remediation cycle 1 re-audit, supplied the authoritative base branch (`main`), merge-base SHA (`402703c`), the checked-out feature branch at the post-remediation head, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only. The re-audit was performed over the full feature-vs-base diff, not only the remediation commit.

Observation (not a narrowing instruction, recorded for completeness): the refreshed PR-context summary's "Changed files overview" again reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only. That categorization is inaccurate; the authoritative `git diff 402703c..bc8d50a` contains 43 `.cs` files (22 production, 21 test). This is the tenth C#-branch review in which the summary miscategorizes a C# branch as docs-only (#99, #101, #103, #105, #107, #109, #113, #115, #117 original, #117 re-audit). The audit used the authoritative git diff file list. Related parsing noise: the summary's author-asserted autoclose list contains `#113`, `#74`, and the non-issue tokens `#AC-2`, `#HI-1`, and `#ISO-8601` lifted from AC labels and spec prose; only #117 is the closing issue. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 402703c..HEAD | grep -E '^artifacts/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature and remediation evidence in the diff is written to the canonical `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/<kind>/` locations (baseline, remediation-baseline, qa-gates, regression-testing, other).
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own fresh cobertura was written to the canonical `evidence/qa-gates/coverage-review-r4/` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, #18, and #99–#117 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Unchanged from the 02-34 audit for the 91 feature tests; the 9 remediation tests follow the same pattern (own mocked handler, own `FakeSubscriptionStore`/decorator, own shared-cache connection string, own `FakeTimeProvider`). 716/716 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | Each remediation test pins exactly one arm or one loop-continuation behavior (missing-id arm; null-body arm; corrupt-expiration throw; no-`value` page; id-less `@removed`; unparseable entry; one `TaskCanceledException` continuation per worker). |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 716 tests in ~2 s (reviewer run); all remediation tests are in-memory (mocked handlers, in-memory SQLite, cooperative yields — no real waits). |
| **Determinism** | PASS | The one intermittent failure observed by the executor during the cycle (pass-1 loop history in `evidence/qa-gates/dotnet-test-coverage.2026-07-03T09-40.md`) was in a test ADDED this cycle and was redesigned to a time-window-insensitive form (atomic call counter on a throw-once store, never-due record, logger asserted only after `StopAsync`) before the final clean pass; the executor recorded 8 consecutive stable coverage-instrumented runs. Reviewer's full run: 0 failures. No wall-clock APIs, sleeps, or `Random.Shared` in any changed test file (reviewer grep: zero matches; the single grep hit in `CryptoClientStateGenerator.cs` is an XML doc-comment stating `Random.Shared` is not used). |
| **Readability & Maintainability** | PASS | Remediation tests carry XML docs citing the finding IDs they close (B-117-01, CR-117-02, CR-117-03/fix item 5) and follow Arrange/Act/Assert with because-messages. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Feature baseline pooled: 92.34% line (5234/5668), 83.16% branch (1269/1526) (`evidence/baseline/dotnet-test-coverage.2026-07-03T01-26.md`); remediation-cycle baseline reproduced B-117-01 at cycle start (`evidence/remediation-baseline/per-file-branch-baseline.2026-07-03T09-06.md`). |
| **No Coverage Regression** | PASS | Post-change pooled: 92.84% line (5631/6065), 83.63% branch (1318/1576) — +0.50pp / +0.47pp vs the main baseline and +0.01pp / +0.38pp vs the pre-remediation head, reviewer re-run and re-parsed, identical to the executor's committed figures. MailBridge and HostAdapter figures unchanged; modified `Program.cs` and Schema partial at 100.00% line and branch. |
| **New Code Coverage** | PASS | Every instrumented new file at 100.00% line. Per-file branch: `GraphSubscriptionManager.cs` 4/4 = 100.00% and `CoreCacheRepository.Subscriptions.cs` 2/2 = 100.00% (B-117-01 CLOSED); `GraphDeltaReconciler.cs` 15/16 = 93.75% (CR-117-02 closed; sole residual partial is the structurally unreachable line-243 arm — CR-117-05, Section 8); all other instrumented changed files at 100.00% branch or with no instrumented branches. |
| **Comprehensive Coverage** | PASS | The 02-34 scenario matrix plus the nine remediation scenarios: both `ParseSubscription` fail-fast arms, the corrupt-expiration fail-fast, the three `ParseDeltaPage` defensive arms (reachable variants), and the three worker `TaskCanceledException` continuations. |
| **Positive Flows** | PASS | Unchanged from the 02-34 audit (create/renew persistence, full delta walks, dispatch upserts, opt-in composition). |
| **Negative Flows** | PASS | Now includes the previously untested parse-boundary failures: missing `id` -> `INTERNAL_ERROR`, JSON-null body -> `TRANSPORT_FAILURE`, corrupt stored expiration -> `InvalidOperationException` naming id and column, unparseable delta entry -> `TRANSPORT_FAILURE` + failed ingest run. |
| **Edge Cases** | PASS | The 02-34 gaps are closed (fail-fast arms, MinValue fallback eliminated, exact-gate arms pinned). The one residual unpinnable case — a literal `null` entry inside a delta `value` array — is dead-code-shadowed and recorded as CR-117-05 (Major, follow-up), not as an untested-scenario gap: the delivered test covers the nearest reachable payload of the same scenario class. |
| **Error Handling** | PASS | Every documented fail-fast guard that is reachable now has a directed test. The unreachable line-243 arm cannot be tested without the CR-117-05 production fix, which was outside this cycle's permitted diff (Section 8). |
| **Concurrency** | PASS | Unchanged from the 02-34 audit; the new throw-once store decorator uses `Interlocked`/`Volatile` for its cross-thread counter, and the redesigned renewal test removed an unsynchronized concurrent logger read. |
| **State Transitions** | PASS | Unchanged from the 02-34 audit. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 92.34% line, 83.16% branch (pooled solution) -> Post-change: 92.84% line, 83.63% branch. Change: +0.50% line, +0.47% branch. New/changed-code coverage: every instrumented new production file at 100.00% line, and the per-file branch gate passes everywhere (GraphSubscriptionManager 55/55 line and 4/4 branch, up from 2/4; CoreCacheRepository.Subscriptions 18/18 line and 2/2 branch, up from 1/2 — the instrumented line set grew 9 -> 18 with the block-bodied ReadSubscription refactor; GraphDeltaReconciler 88/88 line and 15/16 branch = 93.75%, up from 12/16; CloudSyncOptionsValidator 46/46 and 22/22; NotificationRequestProcessor 31/31 and 2/2; modified Schema partial 37/37 and 6/6; modified Program.cs 254/254 and 6/6; the three workers 100.00% line with no instrumented branches) — interface-only, record-only, and auto-property files are uninstrumented per the coverage policy clarification, and the async method bodies (including the three changed worker catch-filter lines) are uninstrumented under the pre-existing runsettings CompilerGenerated exclusion with behavioral verification recorded in Section 8. Disposition: PASS — B-117-01 closed; pooled gates, per-file line, per-file branch, and no-regression all pass. Evidence: `evidence/baseline/dotnet-test-coverage.2026-07-03T01-26.md`, `evidence/qa-gates/dotnet-test-coverage.2026-07-03T09-40.md`, `evidence/qa-gates/coverage-verification.2026-07-03T09-40.md`, reviewer re-run cobertura under `evidence/qa-gates/coverage-review-r4/`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | Because-clauses on every remediation assertion ("a subscription body without 'id' must fail fast", "the loop survives a TaskCanceledException and processes the next item"). |
| **Arrange-Act-Assert Pattern** | PASS | All nine remediation tests follow Arrange/Act/Assert with section comments. |
| **Document Intent** | PASS | XML docs cite the closed finding IDs and the structural-unreachability rationale where the payload deviates from the plan. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | Unchanged: mocked handlers, in-memory shared-cache SQLite (the corrupt-row test uses a second `SqliteConnection` on the same in-memory connection string — no filesystem), in-process factories. |
| **Use Mocks/Stubs** | PASS | Moq for `IHostAdapterClient`; a hand-rolled throw-once decorator over `FakeSubscriptionStore` where Moq cannot proxy the `internal` interface (documented in the test XML doc and evidence). |
| **Environment Stability** | PASS | No temp files, no environment variables, no mutable global state in any changed test file (reviewer grep: zero matches). |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This re-audit closes the remediation loop for B-117-01; no Blocking item remains. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Remediation objective precisely enumerated in `remediation-inputs.2026-07-03T02-34.md` (fix items 1-5 with exit verification commands and a Do Not Do list). |
| **Read existing change plans** | PASS | `evidence/remediation-baseline/phase0-instructions-read.md` records the policy-order read at cycle start. |
| **Document the plan** | PASS | `remediation-plan.2026-07-03T02-34.md` (atomic tasks P0-P5, all checked) with per-phase evidence under `evidence/{remediation-baseline,regression-testing,qa-gates,other}/`. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | The cycle's production surface is minimal: one guard refactor in `ReadSubscription` and one filter expression per worker. One latent complexity issue in pre-cycle code is newly recorded (CR-117-05: a dead `?? throw` arm shadowed by an earlier `TryGetProperty` — Section 8). |
| **Reusability** | PASS | P3 tests reuse the recovery suite's `ReadIngestRunsAsync`/`CapturingLogger` helpers instead of duplicating them (recorded plan deviation, reusability-motivated). |
| **Extensibility** | PASS | Unchanged from the 02-34 audit; no public surface changed this cycle. |
| **Separation of concerns** | PASS | Unchanged from the 02-34 audit. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Unchanged; remediation edits stayed inside the existing files. |
| **Under 500 lines** | PASS | Reviewer-verified via `wc -l` over all 43 changed `.cs` files: production max 358 (`Program.cs`), test max 407 (`GraphDeltaReconcilerRecoveryTests.cs`). Executor evidence: `evidence/qa-gates/diff-scope-and-file-size.2026-07-03T09-42.md`. |
| **Public vs internal** | PASS | Unchanged; no visibility changes this cycle. |
| **No circular dependencies** | PASS | No project-reference or package changes; the namespace-scoped NetArchTest suites pass inside the 716/716 Core.Tests run. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | Remediation test names state scenario and expectation; the new exception message names the subscription id and the literal column. |
| **Docs/docstrings** | PASS | XML docs added for the throw-once decorator and each new test. Note: `ParseDeltaPage`'s XML doc describes a JsonException fail-fast that the literal-null input cannot actually reach (part of CR-117-05, Section 8). |
| **Comment why, not what** | PASS | The corrupt-row test comments the corruption rationale; the reachability deviation is explained in the test XML doc. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 322 files, EXIT 0. Executor final pass: `evidence/qa-gates/csharpier.2026-07-03T09-28.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable analysis as errors. |
| **4. Architecture** | PASS | CloudSync/CloudGraph/Agent boundary suites pass within the full Core.Tests run (716/716). |
| **5. Testing** | PASS | Reviewer: full solution test run — 1163 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the seven T1 CsCheck property tests. |
| **6. Contract/schema checks** | PASS | No wire contract, HTTP surface, or schema changed this cycle; the branch's schema additions remain additive `CREATE TABLE IF NOT EXISTS` with idempotency tests. |
| **7. Integration tests** | N/A | Live webhook delivery/subscription creation require a tenant + public URL — explicitly out of scope (spec Constraints & Risks; F16/HI-1). `WebApplicationFactory` composition tests remain the deepest in-process integration. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass at `bc8d50a`; the executor's loop history records one restart (intermittent new-test failure, redesigned) followed by a fully clean pass — the loop-restart rule was followed and disclosed. |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in the reviewer cobertura under `evidence/qa-gates/coverage-review-r4/`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | The remediation commit message and evidence set describe the delivered changes exactly (reviewer-verified against the diff). |
| **Design choices explained** | PASS | Fail-fast justification (NOT NULL column + RenderUtc write path), filter-broadening rationale (unmapped `HttpClient.Timeout` cancellations), and the P3-T3 payload deviation are all recorded with reasons. |
| **Update supporting documents** | PASS | AC check-off state re-affirmed without edits per the remediation directive (`evidence/other/ac5-reaffirmation.2026-07-03T09-43.md`); no AC text changed. |
| **Provide next steps** | PASS | The executor's deviation record explicitly flags the literal-null escape for this re-audit (graded as CR-117-05 with a follow-up route, Section 8). |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, and TypeScript sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70–#117 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; unchanged posture this cycle. |
| **Null-safety** | PASS | The cycle strengthened null/corruption handling: `ReadSubscription` now fails fast instead of coercing to `MinValue`; both `ParseSubscription` guards are now test-pinned. |
| **Async / resource safety** | PASS | Unchanged; the three filter changes do not alter resource handling; SQLite connections remain `await using`. |
| **Exceptions fail-fast** | PASS | Improved this cycle (MinValue sentinel eliminated per CR-117-04). Residual: the reconciler's literal-null path throws `InvalidOperationException` from `TryGetProperty` rather than the designed `JsonException` mapping — fail-visible but off-contract; CR-117-05 (Major, Section 8). |
| **Naming / file-scoped namespaces** | PASS | Unchanged; all files file-scoped, conventions followed. |
| **No new suppressions and no banned APIs** | PASS | Reviewer grep over all 43 changed `.cs` files for pragma, SuppressMessage, nullable directives, wall-clock APIs, sleeps, `Random.Shared`, and temp-file APIs: zero functional matches (one XML doc-comment mention of `Random.Shared` in `CryptoClientStateGenerator.cs` documenting its non-use). |
| **Dependency policy** | PASS | Zero new dependencies; no csproj, package, or runsettings changes anywhere in the diff. |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq + CsCheck. The cycle's tests follow the established repo convention, consistent with the prior validated audits. Pre-existing repo-wide divergence, not a finding against this branch.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq + CsCheck)** | PASS | The nine remediation tests use `[TestMethod]`, FluentAssertions, Moq (where the target is proxyable), and the existing handler/store doubles. |
| **Test file location** | PASS | All edits inside the existing mirrored test files; no colocation. |
| **Coverage expectation** | PASS | Pooled 92.84% line / 83.63% branch; every instrumented new file at 100.00% line; per-file branch gate met everywhere (lowest instrumented changed file: `GraphDeltaReconciler.cs` at 93.75%). B-117-01 closed. |
| **Property-based tests (T1 density)** | PASS | Unchanged: seven genuine CsCheck properties on the branch's two algorithmic pure functions (`ClientStateMatches` x4, `IsRenewalDue` x3). The cycle added directed edge tests, which is the correct instrument for pinning specific arms. |
| **Mutation testing** | N/A | Pre-merge/nightly pipeline stage per policy, not the per-commit loop (same disposition as prior validated T1 audits). |
| **Determinism (no sleeps, no wall clock)** | PASS | The cycle's tests use `FakeTimeProvider` and bounded cooperative yields only; the executor's disclosed intermittent-test redesign removed the one scheduling-sensitive assertion introduced mid-cycle (Section 1.1 Determinism row). |
| **No temporary files** | PASS | The corrupt-row test corrupts an in-memory shared-cache row via a second connection — no filesystem touch. |
| **No live network** | PASS | All HTTP through mocked handlers; no live Graph calls. |
| **Focused / isolated** | PASS | Fresh doubles per test; the throw-once decorator is per-test-instance state. |

---

## 5. Test Coverage Detail

Reviewer-parsed per-file coverage at branch head `bc8d50a` (fresh cobertura under `evidence/qa-gates/coverage-review-r4/`, line AND branch, duplicate class entries deduplicated per file+line; pooled = sum of deduped per-report totals — same convention as the 02-34 audit and the executor's evidence):

| File | Status | Line | Branch | Notes |
|------|--------|------|--------|-------|
| CloudSync/ChannelNotificationQueue.cs | NEW | 20/20 = 100.00% | no branches | Unchanged from 02-34. |
| CloudSync/CloudSyncOptions.cs | NEW | not instrumented | not instrumented | Auto-property bag; pre-existing CompilerGenerated exclusion (Section 8); behaviorally covered by binding/defaults tests. |
| CloudSync/CloudSyncOptionsValidator.cs | NEW | 46/46 = 100.00% | 22/22 = 100.00% | Unchanged. |
| CloudSync/CloudSyncServiceCollectionExtensions.cs | NEW | 40/40 = 100.00% | no branches | Unchanged. |
| CloudSync/CryptoClientStateGenerator.cs | NEW | 5/5 = 100.00% | no branches | Unchanged. |
| CloudSync/DeltaReconciliationWorker.cs | NEW (filter line changed this cycle) | 7/7 = 100.00% | no branches | Async `ExecuteAsync` body (contains the changed filter) uninstrumented (Section 8); 4 FakeTimeProvider tests incl. the new `TaskCanceledException` continuation. |
| CloudSync/GraphDeltaReconciler.cs | NEW | 88/88 = 100.00% | 15/16 = 93.75% | **CR-117-02 closed** (was exactly 75.00%). Sole partial arm at line 243: the structurally unreachable `?? throw new JsonException` dead-code arm (CR-117-05, Section 8). Async `RunAsync` body uninstrumented (Section 8). |
| CloudSync/GraphNotificationsEndpoint.cs | NEW | 35/35 = 100.00% | no branches | Unchanged. |
| CloudSync/GraphSubscriptionManager.cs | NEW | 55/55 = 100.00% | 4/4 = 100.00% | **B-117-01 CLOSED** (was 2/4 = 50.00%). Both `ParseSubscription` fail-fast arms now test-pinned (missing-id -> `INTERNAL_ERROR`; JSON-null body -> `TRANSPORT_FAILURE`; nothing persisted in either case). Async bodies uninstrumented (Section 8). |
| CloudSync/IClientStateGenerator.cs | NEW | interface-only | n/a | Omission permitted per the coverage policy clarification. |
| CloudSync/IDeltaLinkStore.cs | NEW | interface-only | n/a | Same. |
| CloudSync/INotificationQueue.cs | NEW | interface-only | n/a | Same. |
| CloudSync/ISubscriptionStore.cs | NEW | 8/8 = 100.00% | no branches | Unchanged. |
| CloudSync/NotificationDispatchWorker.cs | NEW (filter line changed this cycle) | 17/17 = 100.00% | no branches | Async loop body (contains the changed filter) uninstrumented (Section 8); new `TaskCanceledException` continuation test added. |
| CloudSync/NotificationRequestProcessor.cs | NEW | 31/31 = 100.00% | 2/2 = 100.00% | Unchanged. |
| CloudSync/NotificationWireModels.cs | NEW | 11/11 = 100.00% | no branches | Unchanged. |
| CloudSync/NotificationWorkItem.cs | NEW | not instrumented | n/a | Records + const-only class; pre-existing exclusion. |
| CloudSync/SubscriptionRenewalWorker.cs | NEW (filter line changed this cycle) | 8/8 = 100.00% | no branches | Async sweep bodies uninstrumented (Section 8); new `TaskCanceledException` continuation test added; the intermittent-form of that test was redesigned before the final pass. |
| CoreCacheRepository.DeltaLinks.cs | NEW | not instrumented | not instrumented | All members async; pre-existing exclusion; 4 dedicated behavioral tests. |
| CoreCacheRepository.Schema.cs | MODIFIED | 37/37 = 100.00% | 6/6 = 100.00% | Unchanged. |
| CoreCacheRepository.Subscriptions.cs | NEW (ReadSubscription refactored this cycle) | 18/18 = 100.00% | 2/2 = 100.00% | **B-117-01 CLOSED** (was 1/2 = 50.00%). The `MinValue` sentinel is eliminated (CR-117-04); the fail-fast throw is pinned by the corrupt-row test. Instrumented line set grew 9 -> 18 with the block-bodied refactor. Async store bodies uninstrumented (Section 8). |
| Program.cs | MODIFIED | 254/254 = 100.00% | 6/6 = 100.00% | Unchanged; both D-6 opt-in arms tested. |

Remediation tests delivered (9 new runtime cases across 7 existing test files): `CreateAsync_missing_id_in_the_response_fails_fast_and_persists_nothing`, `CreateAsync_body_deserializing_to_json_null_fails_fast_and_persists_nothing`, `GetSubscriptionAsync_with_unparseable_expiration_throws_naming_the_id_and_column`, `Reconcile_page_without_a_value_property_upserts_nothing_and_completes_the_walk`, `Reconcile_removed_entry_without_an_id_is_skipped_with_debug_log_and_no_upsert` (covers both id-fallback arms: property absent and JSON-null id), `Reconcile_unparseable_entry_inside_value_fails_with_transport_failure_and_a_failed_ingest_run` (plan-recorded payload deviation), and one `Loop_continues_with_warning_when_the_*_throws_TaskCanceledException_without_stop_requested` per worker.

**Coverage:** pooled 92.84% line / 83.63% branch; every instrumented new file at 100.00% line; every instrumented changed file at or above 93.75% branch. **No gate failure remains.**

**Regression:** zero pre-branch test files modified (all 21 test files are new vs base; the remediation edits are inside those new files and are additive — reviewer-verified from the `f6aea4d..bc8d50a` diff hunks: no assertion weakened, no test deleted; the only non-test-addition edit is an optional `workerLogger` parameter added to a private test helper with the previous default preserved). All 616 pre-branch Core.Tests cases pass inside the reviewer's 716/716 run; MailBridge and HostAdapter coverage figures unchanged from baseline.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 1168 (1163 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 716 passed / 716 (baseline 616; +91 feature, +9 remediation) | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~2 s | PASS |
| Pooled Code Coverage | 92.84% line, 83.63% branch | PASS |
| Per-new-file line coverage | 100.00% on every instrumented new file | PASS |
| Per-new-file branch coverage | Minimum instrumented changed file: GraphDeltaReconciler.cs 93.75% (gate 75%); B-117-01 files both 100.00% | PASS |
| Net new tests vs baseline | +100 runtime cases (incl. 7 CsCheck properties) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 322 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest, namespace-scoped) | Included in `dotnet test` Core.Tests run | 716/716 pass (CloudSync, CloudGraph, and Agent boundary suites included) | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review-r4"` | 1163 passed, 0 failed, 5 skipped | PASS |
| Per-file coverage re-measure | Reviewer cobertura parse (dedupe per file+line; pooled = sum of deduped per-report totals) | All gates met; B-117-01 closed; figures identical to executor evidence | PASS |
| File-size cap | `wc -l` over all 43 changed `.cs` files | Production max 358; test max 407 — all under 500 | PASS |
| Evidence-location scan | `git diff --name-only 402703c..HEAD \| grep -E '^artifacts/'` | No matches | PASS |
| Banned-API / suppression scan | grep over all 43 changed `.cs` files for pragma / SuppressMessage / nullable directives / wall-clock APIs / sleeps / Random.Shared / temp-file APIs | Zero functional matches (one doc-comment mention) | PASS |
| Test-hygiene scan | Same grep set over the changed test files | Zero matches | PASS |
| Workflow-path scan (`modified-workflow-needs-green-run`) | `git diff --name-only 402703c..HEAD -- .github/workflows scripts/benchmarks .github/actions` | No matches — rule not triggered | PASS |
| Remediation diff-scope confinement | `git diff f6aea4d..bc8d50a --stat` vs the plan's Global Conventions statement | 4 production files (named), 7 test files (named), evidence/memory markdown only | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `bc8d50a` on 2026-07-03. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

**No Blocking findings. One NEW Major finding (follow-up routed); informational observations.**

- **B-117-01 (Blocking, 02-34 audit) — CLOSED.** Reviewer-independent verification from fresh cobertura at `bc8d50a`: `GraphSubscriptionManager.cs` instrumented branch 4/4 = 100.00% (was 2/4); `CoreCacheRepository.Subscriptions.cs` 2/2 = 100.00% (was 1/2, and the silent-fallback arm was eliminated by the fail-fast refactor rather than merely covered — the reviewer-preferred CR-117-04 resolution). The three delivered tests assert the failure envelopes (`INTERNAL_ERROR`, `TRANSPORT_FAILURE`) and the empty store, and the corrupt-row test asserts the exception names the id and column. CR-117-02 (exact-gate reconciler) and CR-117-03 (worker cancellation filters) are also closed with directed tests; all 43 worker tests including pre-existing shutdown tests pass.
- **CR-117-05 (Major, NEW) — `ParseDeltaPage` literal-null entry: dead fail-fast arm and off-contract escape.** Flagged by the executor's deviation record (`evidence/regression-testing/delta-reconciler-arms.2026-07-03T09-19.md`) and reviewer-confirmed by code read: in `src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs`, the guard at line 233 (`element.TryGetProperty("@removed", out _)`) throws `System.InvalidOperationException` for a non-Object-kind element, so a delta `value` array containing a literal JSON `null` never reaches the `?? throw new JsonException` arm at lines 244-245 — that arm is dead code (the sole residual partial branch, 1/2 at line 243, and the reason the file reports 93.75% rather than 100.00%). Behavior on that input, reviewer-traced: the `InvalidOperationException` is not caught by `GraphRequestExecutor.ParseSuccess` (which maps only `JsonException` -> `TRANSPORT_FAILURE` and `GraphMappingException` -> `INTERNAL_ERROR`), escapes `RunAsync` BEFORE `RecordRunAsync("failed", ...)` executes — so, contrary to the designed failure path, **no failed `delta_reconcile` ingest run is recorded** — and surfaces at the calling worker's loop boundary, where the cycle-hardened filter (`when (!stoppingToken.IsCancellationRequested)`) logs a Warning with the exception and continues the loop; the next scheduled tick retries from the stored delta link. Severity rationale for **Major, non-blocking**: (1) no policy gate fails — the file is at 93.75% branch, well above 75%, and the dead arm is untestable rather than untested; (2) the input is a degenerate malformed response outside the Graph delta contract (message resources or `@removed` objects), not producible by well-formed Graph traffic; (3) the failure mode is bounded, observable (Warning with full exception), and self-healing (loop continues; delta walk restarts from the stored link next tick) — verified by the worker continuation tests exercising this exact boundary; (4) the production fix was explicitly outside this remediation cycle's permitted diff scope (`remediation-inputs.2026-07-03T02-34.md` Do Not Do). It is nonetheless a real latent defect: dead code plus an XML-doc/pipeline contract the input cannot satisfy, and a missing failed-run record on that path. **Required follow-up (tracked, not merge-blocking):** file an issue to (a) guard element kind at the top of the `ParseDeltaPage` loop (`if (element.ValueKind != JsonValueKind.Object) throw new JsonException(...)`), which restores the envelope mapping and failed-ingest-run recording, removes the dead arm, and makes the scenario directly testable; and (b) bundle the previously recorded executor-side follow-up (map timeout-origin `TaskCanceledException` in `GraphRequestExecutor`). Full detail and recommendation in `code-review.2026-07-03T09-52.md`.
- **Async-body instrumentation exclusion (Informational, carried forward).** The pre-existing `mailbridge.runsettings` CompilerGenerated exclusion means async bodies contribute zero instrumented lines — including the three changed worker catch-filter lines, which therefore cannot appear in cobertura. Per the disposition accepted on the #99–#117 reviews, the changed lines were verified behaviorally: each worker's new continuation test passes only when the broadened filter catches the non-stop-token `TaskCanceledException` (Warning + loop continues), and the pre-existing shutdown tests pass only when the filter declines during stop. The runsettings file is unchanged on this branch; the recommended runsettings follow-up remains open (recorded since #99).
- **Auto-property/record instrumentation exclusion (Informational, carried forward).** `CloudSyncOptions.cs`, `NotificationWorkItem.cs`, and `CoreCacheRepository.DeltaLinks.cs` remain uninstrumented under the same pre-existing filter with direct behavioral coverage; same accepted disposition as the 02-34 audit.
- **Worker filter shutdown nuance (Informational).** With the broadened filter, a non-cancellation exception thrown while stop is already requested now escapes `ExecuteAsync` (previously it was caught by the `ex is not OperationCanceledException` filter). During shutdown the host is already stopping and `BackgroundService` captures the task fault, so the practical impact is nil; recorded for completeness. The pre-existing shutdown tests pass unchanged.
- **Mid-cycle intermittent test (Informational, disclosed).** The executor's pass-1 loop history records one intermittent failure in a test ADDED this cycle (scheduling-sensitive assertion under coverage instrumentation); it was redesigned to a time-window-insensitive form and verified stable across 8 consecutive instrumented runs, then the loop was restarted and completed cleanly. This is the toolchain loop working as designed, not a masked flake: no retry hacks, no assertion weakening (the redesigned test asserts strictly loop-survival semantics, which is the behavior under test).

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("command csharpier ... package contains dotnet-csharpier"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70–#117 audits. The format check ran to EXIT 0 over all 322 files.
- **MCP template/validator tools:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from this feature's validator-shaped 02-34 artifact set (itself shaped on the validator-passing #115 set) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **GitHub CLI unavailable:** `gh` is not installed, so issue cross-verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None removed.** No pre-branch test file was modified, deleted, or weakened; the remediation edits are additive within the branch's own new test files (reviewer-verified hunk-by-hunk). The 5 solution skips are pre-existing environment-gated COM/publish tests, unchanged.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `402703c`)

Branch `feature/graph-subscriptions-delta-117`, head `bc8d50a324a7e5127908c6a9204f36c8dc50850b`. Two commits: `f6aea4d` (feature, 63 files, +6211/-1) and `bc8d50a` (remediation cycle 1, 39 files, +1565/-14). Full range: `402703c..bc8d50a` — 89 files, +7766/-5.

### Files Modified (categories)

1. **Feature commit `f6aea4d`** — unchanged from the 02-34 audit: the 18-file CloudSync module, 2 store partials, Schema/Program modifications, 21 test files (91 cases), feature docs and evidence.
2. **Remediation commit `bc8d50a`, production (4 files):** `CoreCacheRepository.Subscriptions.cs` — `ReadSubscription` fail-fast refactor (+16/-1, eliminates the MinValue sentinel); `NotificationDispatchWorker.cs` / `SubscriptionRenewalWorker.cs` / `DeltaReconciliationWorker.cs` — one catch-filter line each (`when (ex is not OperationCanceledException)` -> `when (!stoppingToken.IsCancellationRequested)`).
3. **Remediation commit `bc8d50a`, tests (7 files, +9 runtime cases):** the two `ParseSubscription` arm tests, the corrupt-expiration test, the three `ParseDeltaPage` arm tests (one payload deviation recorded), the three worker continuation tests, one additive optional helper parameter, and one hand-rolled throw-once store decorator.
4. **Remediation commit `bc8d50a`, documentation:** the 02-34 review artifact set (first committed here), `remediation-inputs`/`remediation-plan` at 02-34, cycle evidence under `evidence/{remediation-baseline,regression-testing,qa-gates,other}/`, and agent-memory records (metadata, not code).

---

## 10. Compliance Verdict

### Overall Status: COMPLIANT — BLOCKING FINDING B-117-01 CLOSED; ZERO BLOCKING FINDINGS REMAIN

The C# change passes formatting, linting, nullable type-checking, the namespace-scoped architecture-boundary suites, the full unit-test suite (1163/1163 runnable), the pooled coverage gates (92.84% line / 83.63% branch, both improved over baseline with no regression), the per-file line AND branch gates on every instrumented changed file, the file-size cap, and every hygiene scan — all independently re-run and re-parsed by the reviewer at branch head `bc8d50a`. The remediation cycle stayed exactly within its permitted diff scope, added no suppressions or exclusions, weakened no tests, and disclosed its one mid-cycle intermittent-test redesign and its one payload deviation with reasons.

One NEW Major finding (CR-117-05: the `ParseDeltaPage` dead fail-fast arm and the off-contract `InvalidOperationException` escape for a literal-null delta entry) is recorded with a bounded, observable, self-healing failure mode and a concrete two-part follow-up; it does not fail any gate and does not block the PR.

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (remediation inputs/plan/policy-order evidence present)
- Design Principles: PASS (minimal production surface; helper reuse; one carried-latent-defect recorded as CR-117-05)
- Module & File Structure: PASS (all 43 changed files under 500 lines; production max 358, test max 407)
- Naming, Docs, Comments: PASS (one doc-accuracy item folded into CR-117-05)
- Toolchain Execution: PASS (single clean pass, reviewer re-verified; executor loop restart disclosed)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-fast strengthened this cycle: MinValue sentinel eliminated, parse guards pinned; residual off-contract escape recorded as CR-117-05, follow-up routed)
- Dependency Policy: PASS (zero new dependencies)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (pooled 92.84%/83.63%; per-file branch gate met everywhere; B-117-01 closed)
- Test Structure: PASS
- External Dependencies: PASS (mocked handlers, in-memory SQLite, no temp files, no live Graph calls)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq + CsCheck repo convention; tests/ mirror)
- Determinism: PASS (FakeTimeProvider + cooperative yields; mid-cycle flake redesigned, disclosed, and verified stable)
- T1 obligations: PASS (seven genuine CsCheck properties; per-file branch coverage expectation now met; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 1163/1163 runnable solution tests passing (5 pre-existing environment-gated skips)
- 92.84% pooled line coverage, 83.63% pooled branch coverage (gates: 85%/75%), both improved over baseline (92.34%/83.16%) and over the pre-remediation head (92.83%/83.25%)
- Every instrumented new file: 100.00% line coverage
- Per-file branch gate: PASS on all 22 changed production files (minimum instrumented: 93.75%)
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- All 43 changed `.cs` files under the 500-line cap (production max 358, test max 407)

---

### Recommendation

**Go for PR.** Blocking finding B-117-01 is closed with reviewer-independent numeric verification; CR-117-02/03/04 are closed; the toolchain is clean in a single pass at branch head; pooled and per-file gates hold with no regression; `blocking_count == 0`. Before or immediately after merge, file the follow-up issue for CR-117-05 (the `ParseDeltaPage` element-kind guard) bundled with the `GraphRequestExecutor` timeout-mapping follow-up already recorded in the 02-34 review; neither requires holding this PR.

---

## Appendix A: Test Inventory

C# test changes in this feature (21 files: 19 in `tests/OpenClaw.Core.Tests/CloudSync/`, 2 at the test-project root; entries 1-21 as inventoried in `policy-audit.2026-07-03T02-34.md` Appendix A, with the remediation cycle's additions noted here):

1. `GraphSubscriptionManagerTests.cs` (now 295 lines) — +2 remediation tests: missing-`id` body fails fast with `INTERNAL_ERROR` and persists nothing; JSON-null body fails fast with `TRANSPORT_FAILURE` and persists nothing (B-117-01 fix items 1-2).
2. `CoreCacheRepositorySubscriptionsTests.cs` (now 237 lines, test-project root) — +1 remediation test: corrupt `expiration_utc` (second shared-cache connection) throws `InvalidOperationException` naming `sub-1` and `expiration_utc` (fix item 3(a)).
3. `GraphDeltaReconcilerTests.cs` (now 219 lines) — +1 remediation test: page without a `value` property upserts nothing and completes the walk (CR-117-02 arm, line 229 false arm).
4. `GraphDeltaReconcilerRecoveryTests.cs` (now 407 lines) — +2 remediation tests: id-less `@removed` entries (absent and JSON-null id) skipped with two Debug `"(unknown)"` logs and no upsert; unparseable entry (`{"id": 123}`) maps to `TRANSPORT_FAILURE` with a failed `delta_reconcile` ingest run (payload deviation from the plan's literal-null recorded — see CR-117-05).
5. `NotificationDispatchWorkerTests.cs` (now 281 lines) — +1 remediation test: `TaskCanceledException` from the inner fetch without stop requested logs Warning and the loop processes the next item (CR-117-03).
6. `SubscriptionRenewalWorkerTests.cs` (now 354 lines) — +1 remediation test (redesigned mid-cycle to a time-window-insensitive form): `TaskCanceledException` from the sweep without stop requested logs Warning and the next sweep runs; includes the hand-rolled `ThrowOnFirstListStore` decorator (Moq cannot proxy the `internal` interface).
7. `DeltaReconciliationWorkerTests.cs` (now 243 lines) — +1 remediation test: `TaskCanceledException` from the reconcile without stop requested logs Warning and the next tick reconciles; additive optional `workerLogger` helper parameter (default preserved).
8-21. The remaining fourteen feature-commit suites (`CloudSyncOptionsValidatorTests`, `CryptoClientStateGeneratorTests`, `ChannelNotificationQueueTests`, `ClientStatePropertyTests`, `NotificationRequestProcessorTests`, `NotificationRequestProcessorEdgeTests`, `GraphNotificationsEndpointTests`, `GraphSubscriptionManagerLifecycleTests`, `RenewalDuePropertyTests`, `CloudSyncServiceCollectionExtensionsTests`, `CloudSyncSelectionTests`, `CloudSyncArchitectureBoundaryTests`, `CloudSyncTestDoubles`, `CoreCacheRepositoryDeltaLinksTests`) are unchanged from the 02-34 inventory.

Reviewer run: `OpenClaw.Core.Tests` 716 passed, 0 failed; solution total 1163 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path for this re-audit)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review-r4"

# Remediation directed subsets (executor, cycle evidence)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~GraphSubscriptionManagerTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositorySubscriptionsTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~GraphDeltaReconciler"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~WorkerTests"

# File-size cap (all changed .cs files)
git diff --name-only 402703c6c18d0131128696147443ef9f41110f3c..HEAD -- '*.cs' | xargs wc -l

# Evidence-location scan
git diff --name-only 402703c6c18d0131128696147443ef9f41110f3c..HEAD | grep -E '^artifacts/'

# Workflow-path scan (modified-workflow-needs-green-run trigger)
git diff --name-only 402703c6c18d0131128696147443ef9f41110f3c..HEAD -- .github/workflows scripts/benchmarks .github/actions

# Remediation diff-scope confinement
git diff --stat f6aea4d646dd79d3be3cc74f61f31ad5c56b52a3..bc8d50a324a7e5127908c6a9204f36c8dc50850b

# Banned-API / suppression / hygiene scans over all changed .cs files
git diff --name-only 402703c6c18d0131128696147443ef9f41110f3c..HEAD -- '*.cs' | xargs grep -lnE 'Thread\.Sleep|DateTime\.UtcNow|DateTime\.Now|Random\.Shared|GetTempPath|GetTempFileName|#pragma warning|SuppressMessage|#nullable'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-03
**Policy Version:** Current (as of audit date)
