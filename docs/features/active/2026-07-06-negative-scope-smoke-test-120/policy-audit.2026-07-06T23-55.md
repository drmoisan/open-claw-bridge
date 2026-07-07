# Policy Compliance Audit: negative-scope-smoke-test (#120)

**Audit Date:** 2026-07-06
**Code Under Test:** C# only. 10 NEW production `.cs` files (9 under `src/OpenClaw.Core/ScopeValidation/` — MailboxProbeOutcome 18 lines, IMailboxScopeProbe 28, ScopeBoundaryValidationResult 28, ScopeValidationOptions 35, ScopeBoundaryValidator 60, ScopeValidationOptionsValidator 61, ScopeValidationServiceCollectionExtensions 78, ScopeBoundaryStartupValidator 104, ScopeBoundaryEvaluator 124 — plus `src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs` 78; 614 lines total) and 2 MODIFIED production files (`src/OpenClaw.Core/Program.cs` 364 lines — one registration call added at lines 69-74 — and `src/OpenClaw.Core/OpenClaw.Core.csproj` — one `InternalsVisibleTo("DynamicProxyGenAssembly2")` entry). 9 NEW test `.cs` files (8 under `tests/OpenClaw.Core.Tests/ScopeValidation/`, 1 under `tests/OpenClaw.Core.Tests/CloudGraph/`; 1457 lines, 55 test methods expanding to 65 runtime cases including 4 CsCheck properties across 2 property classes). Plus 20 Markdown files (18 feature scoping/evidence docs for issue #120 and 2 task-researcher agent-memory records). No Python, PowerShell, Bash, or TypeScript files changed in the branch diff.

**Scope:** Full feature branch `feature/negative-scope-smoke-test-120` @ head `99614291f2d81693e3e36e0a302b223d2849e58e` versus resolved base `epic/openclaw-vision-integration` @ merge-base `d67dea0117984b980b093f1c942c9a4762b8b25f` (merge-base timestamp 2026-07-06T22:49:06-04:00; base branch supplied by the caller and reviewer-confirmed identical for local and origin refs). Scope is feature-vs-base over the complete branch diff: 41 files, +3355/-3 (name-status: 21 `.cs`/`.csproj`, 20 `.md`). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md`.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 10 production `.cs` (all new) + 2 production files modified (Program.cs, OpenClaw.Core.csproj) + 9 test `.cs` (all new) | 1233 (solution) / 781 (Core.Tests) | 1228 pass, 0 fail, 5 env-gated skips | OpenClaw.Core package 92.49% line, 80.90% branch (plain instrumentation, executor Phase 0) | OpenClaw.Core package 92.82% line, 81.40% branch (reviewer fresh re-run and re-parse, identical to the executor's committed figures); pooled solution 89.97% line / 78.30% branch (plain) and 93.06% / 84.00% (repo runsettings convention) | Every instrumented new production file at 100.00% line AND 100.00% branch under FULL (plain) instrumentation including all async bodies (Section 5 table); `IMailboxScopeProbe.cs` interface-only per the coverage policy clarification; modified Program.cs changed lines (69-72) covered in both runs, 258/258 line and 6/6 branch under the repo convention measurement |

**Note:** Python, PowerShell, Bash, and TypeScript rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit PASS.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md` (OpenClaw.Core package line 92.49% / branch 80.90%; solution 1163 pass / 5 skips)
- C# post-change coverage artifact: `docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/qa-gates/final-qa-04-dotnet-test-coverage.md` and `evidence/qa-gates/final-qa-05-coverage-delta.md`
- Reviewer-regenerated cobertura (this audit, two fresh `dotnet test` runs at branch head): plain-instrumentation run under `docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/qa-gates/coverage-review/{58d51541...,ce026ff6...,426b58c7...}/coverage.cobertura.xml` (OpenClaw.Core package 92.82% line / 81.40% branch — identical to the executor's committed figures; per-file line AND branch re-measured for all 11 changed production files with async bodies fully instrumented) and repo-convention run (`--settings mailbridge.runsettings`) under `evidence/qa-gates/coverage-review-settings/`
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura at branch head under two instrumentation modes. The C# coverage gate is met (OpenClaw.Core package 92.82% line >= 85% and 81.40% branch >= 75%, both improved over baseline; pooled solution 89.97%/78.30% plain and 93.06%/84.00% convention, above the uniform gates; every instrumented new file at 100.00% line and 100.00% branch including async bodies; modified Program.cs changed lines fully covered with no regression).

---

## Executive Summary

This feature branch closes issue #120 (gap F17, Epic openclaw-vision Epic C): the application-runtime negative-scope smoke test required by master §13 Steps 2-3 — an opt-in startup validation proving the Exchange Online Application RBAC boundary before business logic runs. The delivery is a host-neutral `OpenClaw.Core.ScopeValidation` namespace plus one Graph-bound probe: (a) the pure static `ScopeBoundaryEvaluator` (D3/D4) with the fail-closed denial recognizer `IsAuthorizationDenial` (exactly `!Ok` + `UNAUTHORIZED` + `ErrorAccessDenied`, all Ordinal — the only envelope-level discriminator because the F13 executor folds 401/403 into one code) and the pair evaluator producing `ScopeBoundaryValidationResult` with the invariants `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason == null` iff `Succeeded`; (b) the narrow internal port `IMailboxScopeProbe` (D2) implemented by `GraphMailboxScopeProbe`, which mirrors the `GraphHostAdapterClient` constructor seams and reuses the F13 `GraphRequestExecutor` pipeline verbatim (bearer token, `client-request-id`, retry/backoff, D5 error mapping), issuing the harmless read `GET users/{escaped-upn}/messages?$top=1&$select=id` with the success body discarded; (c) `ScopeBoundaryValidator` (D5) probing in-scope first, out-of-scope second, both always executed; (d) the one-shot `IHostedService` `ScopeBoundaryStartupValidator` logging a single structured entry (Information on pass, Critical on fail; outcome summaries limited to Ok/ErrorCode/BridgeErrorCode — never tokens or bodies) and throwing `InvalidOperationException` to hard-abort startup on any failure; (e) the `OpenClaw:ScopeValidation` options section (D6) with the full-list pure validator and `AddOptions/Bind/Validate/ValidateOnStart` binding; and (f) the three-branch DI registration `AddScopeBoundaryValidation` — disabled registers nothing, enabled-without-Graph throws at composition time, enabled-with-Graph registers the typed probe client, validator, and hosted service — called once from `Program.cs`. The F13 `IHostAdapterClient` contract, `OpenClaw.HostAdapter.Contracts`, and `MessagePollingWorker` are untouched (reviewer-verified empty diffs, D1/D8). Live-tenant verification ships as the D7 human runbook, recorded in orchestrator state as `human_interaction` requirement HI-1 with `response: exception` and the matching `runbook_path` (reviewer-verified against all three `orchestrator-state.md` invariants).

The mandatory toolchain was independently re-run by the reviewer against the branch head `9961429` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 341 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** the new `ScopeValidationArchitectureBoundaryTests` (3 NetArchTest rules pinning the D1 dependency direction: the pure core must not depend on CloudGraph, System.Net.Http, or Microsoft.Extensions.Logging) plus the pre-existing CloudGraph/CloudSync/Agent boundary suites run inside `OpenClaw.Core.Tests` — included in the 781/781 pass.
- **Tests + coverage:** full solution `dotnet test` with XPlat Code Coverage, run twice (plain instrumentation and the repo `--settings mailbridge.runsettings` convention) — 1228 passed, 0 failed, 5 environment-gated skips (same skips as baseline); OpenClaw.Core package 92.82% line / 81.40% branch, above the uniform gates and above baseline; T1 property-test obligation satisfied with four genuine CsCheck properties covering all three new pure functions (`IsAuthorizationDenial` conjunct-iff, `Evaluate` invariant pair, options-validator disabled-always-valid and determinism).
- **Regression evidence:** all 716 baseline Core.Tests cases pass unmodified inside the reviewer's full run (781 = 716 + 65 new); zero existing test files were modified (the test diff is the nine new files); the changed Program.cs lines are executed by the pre-existing `WebApplicationFactory` composition-root tests.

No Blocking findings. No Major findings. One Minor code-review finding (public accessibility of three spec-designated-internal types, pattern-consistent with the sanctioned F13 trio) and informational observations recorded in Section 8. Remediation is not required. The feature is recommended Go for PR.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/csharp.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/orchestrator-state.md` (consulted for the HI-1 `human_interaction` invariants; no checkpoint-validator code changes on the branch)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is ten new production files, two modified production files (composition root + csproj), nine test files, two agent-memory records, and documentation/evidence Markdown. The reviewer's cobertura parser was a scratchpad throwaway outside the repository.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`epic/openclaw-vision-integration`), merge-base SHA (`d67dea0`), the checked-out feature branch, and stated "Determine review scope yourself from the branch diff against the merge-base; run the full toolchain for every language with changed files." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observation (not a narrowing instruction, recorded for completeness): the PR-context artifacts named as inputs (`artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt`) did not exist at review start (the `artifacts/` directory contained only `orchestration/`). Per the `pr-context-artifacts` refresh rule the reviewer regenerated both artifacts from the authoritative git range `d67dea0..9961429` using the supplied base branch (no repo-local collector script exists and the MCP collector is not part of this session's toolset; the git-derived summary/appendix carry the same commit list, name-status file list, diff stat, and full unified diff a collector run would anchor). Because the reviewer generated the summary directly from git, the historical C#-branch-misclassified-as-docs-only summary quirk (observed on the #99-#117 reviews) does not apply to this artifact pair. Scope was determined from `git diff --stat d67dea0..HEAD` in all cases.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only d67dea0..HEAD | grep -E '^artifacts/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/<kind>/` locations (baseline, qa-gates).
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own coverage evidence was written to the canonical `evidence/qa-gates/coverage-review/` and `evidence/qa-gates/coverage-review-settings/` paths.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70 through #117 audits); the scan was performed by direct diff inspection. The reviewer-regenerated PR-context artifacts at `artifacts/pr_context.summary.txt`/`artifacts/pr_context.appendix.txt` are the skill-designated canonical locations for PR context, not evidence artifacts, and are untracked.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Every test constructs its own options (`Options.Create` or in-memory configuration), Moq probe/token doubles, `FakeHttpHandler`, `FakeTimeProvider`, capturing logger, and (where needed) its own `ServiceCollection`; no shared mutable state, no static mutation, no fixture ordering. 781/781 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | One classification-matrix row per method/DataRow; one pair-evaluation cell per method; one composition rule per validator test (both-invoked, order, no-short-circuit, verbatim composition, token flow); one hosted-service behavior per test (success log, failure log+throw, token/body absence, cancellation, StopAsync); one options rule per method/DataRow; one DI branch per test; one D1 dependency direction per architecture test; one probe contract aspect per test (path, escaping, headers, each projection row, cancellation flow). |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 781 tests in ~2 s (reviewer run); all new tests are in-memory computation, mocked probes/handlers, and in-memory configuration binding — no I/O, no live Graph calls. |
| **Determinism** | PASS | The evaluator and options validator are pure (no clock at all); the probe tests inject `FakeTimeProvider` into the executor seam; CsCheck uses the suite's seeded `Gen`/`Sample` convention with failing-seed printing; no sleeps, timers, network, or filesystem (reviewer scans: zero matches for wall-clock APIs, `Thread.Sleep`, `Task.Delay`, or live-endpoint construction in the new test folders). |
| **Readability & Maintainability** | PASS | Descriptive scenario names (`IsAuthorizationDenial_CaseVariantBridgeCode_IsFalse`, `Evaluate_DeniedAndAllowed_FailsWithBothReasonsJoined`, `ValidateAsync_WhenInScopeFails_StillProbesOutOfScope`, `AddScopeBoundaryValidation_EnabledWithoutGraphAdapter_ThrowsAtCompositionTime`), FluentAssertions because-messages, Arrange/Act/Assert structure, XML docs on every test class citing the design decisions covered (D2-D6, AC-5). |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline OpenClaw.Core package: 92.49% line, 80.90% branch (plain instrumentation). Source: `evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`. |
| **No Coverage Regression** | PASS | Post-change OpenClaw.Core package: 92.82% line, 81.40% branch — +0.33pp / +0.50pp, reviewer re-run and re-parsed, identical to the executor's committed figures. The changed Program.cs lines (69-72, the single `AddScopeBoundaryValidation` call) are covered in both instrumentation modes (12 hits via the pre-existing `WebApplicationFactory` composition-root tests); no changed-line regression is possible elsewhere because all other production changes are additions. |
| **New Code Coverage** | PASS | Every instrumented new production file at 100.00% line AND 100.00% branch under FULL (plain) instrumentation — including every async method body (`GraphMailboxScopeProbe.ProbeMailboxReadAsync`, `ScopeBoundaryValidator.ValidateAsync`, `ScopeBoundaryStartupValidator.StartAsync` — reviewer-verified from the instrumented line sets, Section 5). `IMailboxScopeProbe.cs` is interface-only with no executable behavior per the coverage policy clarification. Test files are excluded from measurement per policy. |
| **Comprehensive Coverage** | PASS | Full D3 classification matrix (the one true shape; 401 null/InvalidAuthenticationToken; all six other D5 codes; Ok==true; both case-variant constants pinning Ordinal); full D4 pair matrix including the `"; "`-joined both-sides case, the wrong-error quoting, and the null-BridgeErrorCode dash substitution; probe request shape (path, UPN escaping, `$top=1&$select=id`, bearer, `client-request-id`) and all four envelope-projection rows; hosted-service success/failure/cancellation plus token/body log-absence; composition order/no-short-circuit/verbatim/token-flow; options-validator full rule set including OrdinalIgnoreCase distinctness and multi-fault reporting; all three DI registration branches plus `ValidateOnStart` fail-closed and null guards. |
| **Positive Flows** | PASS | (allowed, denied) pass verdict with null `FailureReason`; 200-empty-value probe read; enabled-with-Graph DI resolution and per-key options binding. |
| **Negative Flows** | PASS | Every non-pass pair cell with the exact reason strings; every non-denial classification row; enabled-without-Graph composition-time throw; `OptionsValidationException` for missing/equal UPNs; null-argument guards on every constructor and entry point. |
| **Edge Cases** | PASS | Empty in-scope mailbox (200 with empty `value` is still allowed); unparseable 403 error body → null `BridgeErrorCode`; case-variant denial constants rejected (Ordinal); case-variant equal UPNs rejected (OrdinalIgnoreCase); reserved-character UPN escaping; whitespace-only options fields. |
| **Error Handling** | PASS | The probe never throws on Graph failure (every failure mapped into the outcome via the F13 D5 matrix, inherited by construction); the startup validator throws `InvalidOperationException` naming the `FailureReason` and logs Critical before throwing; transient failures (`THROTTLED`, `TRANSPORT_FAILURE`) fail the boundary with the wrong-error reason rather than passing. |
| **Concurrency** | N/A | All new components are stateless composition over injected seams; the two probes run sequentially by design (deterministic order, spec D5); no shared mutable state introduced. |
| **State Transitions** | PASS | The hosted-service one-shot lifecycle (StartAsync validate→log→return/throw; StopAsync no-op) is pinned; probe order (in-scope → out-of-scope) and the no-short-circuit invariant are asserted via recorded call order. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 92.49% line, 80.90% branch (OpenClaw.Core package, plain instrumentation) -> Post-change: 92.82% line, 81.40% branch. Change: +0.33% line, +0.50% branch. New/changed-code coverage: every instrumented new production file at 100.00% line and 100.00% branch under full plain instrumentation, reviewer-parsed per file — ScopeBoundaryEvaluator 56/56 line and 16/16 branch; ScopeBoundaryStartupValidator 54/54 and 6/6; GraphMailboxScopeProbe 37/37 and 6/6; ScopeValidationServiceCollectionExtensions 36/36 and 4/4; ScopeValidationOptionsValidator 32/32 and 12/12; ScopeBoundaryValidator 26/26 (no branch points); ScopeBoundaryValidationResult 10/10 (no branch points); MailboxProbeOutcome 6/6 (no branch points); ScopeValidationOptions 3/3 (no branch points); IMailboxScopeProbe interface-only per the coverage policy clarification; modified Program.cs changed lines 69-72 covered in both instrumentation modes with 258/258 line and 6/6 branch under the repo runsettings convention used by the prior accepted audits, while the plain-instrumentation whole-file figure (264/304 line, 37/66 branch) reflects pre-existing uncovered endpoint-lambda code from other opt-in features entirely outside this branch's changed range (every missed line verified outside lines 69-74); the executor's plain run did not apply the runsettings CompilerGenerated exclusion, so — unlike the #99-#117 reviews — all async bodies ARE instrumented and measured at 100.00% with no behavioral-verification substitution needed; pooled solution 89.97% line (9256/10288) and 78.30% branch (2190/2797) plain, 93.06%/84.00% under the convention run; new test files excluded from measurement per policy. Disposition: PASS (line >= 85%, branch >= 75%, no regression on changed lines). Evidence: `evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`, `evidence/qa-gates/final-qa-04-dotnet-test-coverage.md`, `evidence/qa-gates/final-qa-05-coverage-delta.md`, reviewer re-run cobertura under `evidence/qa-gates/coverage-review/` and `evidence/qa-gates/coverage-review-settings/`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions because-clauses on every assertion ("BridgeErrorCode is compared Ordinal; 'erroraccessdenied' must not match 'ErrorAccessDenied'", "a disabled section waives all other rules", "the thrown exception names the FailureReason"); the architecture tests join `FailingTypeNames` into the assertion message; CsCheck prints the failing seed. |
| **Arrange-Act-Assert Pattern** | PASS | All directed tests follow Arrange/Act/Assert; property tests use the suite's generator + `Sample` structure with explanatory vocabulary comments. |
| **Document Intent** | PASS | XML docs on all nine test classes state the design decisions covered (D2-D6), the mocked-Graph approach, and the invariants under test; each method has a scenario-describing name. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No live Graph calls anywhere: probe HTTP flows through the existing `FakeHttpHandler` double returning recorded Graph-shaped JSON raw strings; all other tests mock `IMailboxScopeProbe` directly. Configuration binding uses `AddInMemoryCollection` exclusively (stated in the DI test class doc). |
| **Use Mocks/Stubs** | PASS | `Mock<IMailboxScopeProbe>` and `Mock<IAppTokenProvider>` (Moq, strict where appropriate); `FakeHttpHandler`; `NullLogger`/minimal capturing logger; `FakeTimeProvider`. |
| **Environment Stability** | PASS | No temporary files (reviewer grep over the new test folders: zero filesystem access — fixtures are inline raw strings); no environment variables read; no mutable global state. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #120, `spec.md` v1.0 (eight recorded design decisions D1-D8 with the D3 classification matrix, D4 result shape, D6 config-key table, and the D7 human-exception mirror of F11 HI-1), user-story scenarios, and the master §13 Steps 2-3 references define the change precisely. Authoritative research artifact (Option 3c analysis) present under `research/`. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-06T22-18.md` present. |
| **Document the plan** | PASS | `plan.2026-07-06T22-18.md` with per-phase evidence under `evidence/baseline/` and `evidence/qa-gates/`. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | A pure static classifier + evaluator, one narrow port with one method, one thin Graph implementation delegating to the existing executor, one sequential two-call orchestrator, one hosted service, one options bag + validator pair in the established pattern. No new abstraction framework, no inheritance. |
| **Reusability** | PASS | The probe reuses the F13 `GraphRequestExecutor` pipeline by construction rather than duplicating auth/retry/error mapping (constructor seams mirror `GraphHostAdapterClient` exactly); the options binding reuses the `GraphAdapterOptions` `AddOptions/Bind/Validate/ValidateOnStart` pattern; the failure-reason composition mirrors F11 D5. |
| **Extensibility** | PASS | The evaluator consumes the port-shaped `MailboxProbeOutcome`, so any future probe implementation (e.g., a different read kind) plugs in behind `IMailboxScopeProbe` without touching the decision logic; the expected-denial values are named constants designed for a one-constant tenant-divergence correction (spec D3). |
| **Separation of concerns** | PASS | Pure classification/evaluation (`ScopeBoundaryEvaluator`, no I/O/clock/logging/DI — pinned by 3 NetArchTest rules) is separated from I/O (`GraphMailboxScopeProbe` in `CloudGraph/`), from composition (`ScopeBoundaryValidator`, no logging or verdict logic), from host lifecycle + logging (`ScopeBoundaryStartupValidator`), and from DI glue (extensions class); the dependency direction (ScopeValidation defines the port, CloudGraph implements it) is architecture-tested. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Host-neutral pieces under `src/OpenClaw.Core/ScopeValidation/` in namespace `OpenClaw.Core.ScopeValidation`; the Graph-bound probe beside the executor it reuses in `src/OpenClaw.Core/CloudGraph/` (spec D1 layout); tests mirror both paths under `tests/OpenClaw.Core.Tests/`. |
| **Under 500 lines** | PASS | Reviewer-verified via `wc -l` over all 20 changed `.cs` files: production max 124 (`ScopeBoundaryEvaluator.cs`), modified `Program.cs` 364, test max 268 (`ScopeBoundaryEvaluatorTests.cs`) — all under 500. |
| **Public vs internal** | PASS with Minor note | Seven of ten new types are `internal` (evaluator, both records, port, probe, validator, hosted service). Three are `public` (`ScopeValidationServiceCollectionExtensions`, `ScopeValidationOptions`, `ScopeValidationOptionsValidator`), mirroring the sanctioned F13 trio (`AddGraphHostAdapterClient`/`GraphAdapterOptions`/`GraphAdapterOptionsValidator`, #115 audit Section 2.3) but diverging from this spec's "API / CLI Surface" prose ("All new types are internal"). Recorded as Minor CR-120-01 in the code review; no functional risk (no external callers; same-assembly composition root). |
| **No circular dependencies** | PASS | No project-reference changes; the D1 NetArchTest suite (3 rules) passes inside the 781/781 Core.Tests run, pinning the ScopeValidation-does-not-depend-on-CloudGraph direction. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `IsAuthorizationDenial`, `BuildFailureReason`, `ProbeMailboxReadAsync`, `AddScopeBoundaryValidation`, `ExpectedDenialGraphCode` — PascalCase, self-describing; `Async` suffix on async methods; private fields follow the repository's established unprefixed camelCase convention (matching `GraphHostAdapterClient`). |
| **Docs/docstrings** | PASS | XML docs on every type and member, recording the fail-closed rationale, the 401/403 fold and why `BridgeErrorCode` is the only discriminator, the no-short-circuit rationale, the hosted-service fail-fast semantics, per-key env-binding forms, and the three DI branches. |
| **Comment why, not what** | PASS | Inline comments explain the discarded-success-body decision (no DTO mapping path in the probe), the DynamicProxyGenAssembly2 grant purpose, and the Program.cs opt-in semantics — not line-by-line narration. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 341 files, EXIT 0. Executor: `evidence/qa-gates/final-qa-01-csharpier-check.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. The outcome record's error fields are honestly nullable with the null/`"-"` substitution handled at the formatting site. |
| **4. Architecture** | PASS | New `ScopeValidationArchitectureBoundaryTests` (3 D1 rules) plus the pre-existing CloudGraph/CloudSync/Agent boundary suites pass within the full Core.Tests run. |
| **5. Testing** | PASS | Reviewer: full solution test run — 1228 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the four T1 CsCheck property tests. |
| **6. Contract/schema checks** | PASS | No wire contract, HTTP surface, or schema changed. `IHostAdapterClient` and `OpenClaw.HostAdapter.Contracts` are untouched (reviewer-verified empty diff, D1); `MessagePollingWorker` untouched (D8). The probe's Graph-boundary contract (request shape + envelope projection) is pinned by the `FakeHttpHandler` contract tests. |
| **7. Integration tests** | N/A | Live-tenant verification is tenant-dependent and explicitly out of scope (spec D7; no Azure/Exchange credentials in this environment or CI); it ships as the human runbook recorded as HI-1. The DI tests exercise the full registration graph including `ValidateOnStart`, which is the deepest integration available without a tenant. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass final QA set at 2026-07-06T23-32. |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in the reviewer cobertura under `evidence/qa-gates/coverage-review/` and `coverage-review-settings/`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy table matches the delivered diff exactly (nine ScopeValidation files, one CloudGraph probe, one Program.cs call, tests under the mirror paths, the D7 runbook). |
| **Design choices explained** | PASS | Eight recorded design decisions (D1 dedicated probe port with the rejected 3a/3b alternatives; D2 port + outcome projection; D3 fail-closed denial classification with the full matrix; D4 result record and reason composition; D5 hosted-service fail-fast with the rejected IStartupFilter/inline alternatives; D6 config section and three-branch registration; D7 human-exception runbook; D8 explicit out-of-scope for the #117 Meta.Bridge gap). |
| **Update supporting documents** | PASS | Acceptance criteria AC-1 through AC-8 checked off in `spec.md` and `user-story.md` by the executor; AC-9 verified and checked off by this review (feature audit); the runbook is authored per the human-exception-runbook skill structure. |
| **Provide next steps** | PASS | Spec records rollout as opt-in dark-by-default with configuration-only fallback; the runbook names the live-tenant pass and negative-rehearsal procedures and the denial-code confirmation step; #117 (Meta.Bridge) remains the queued follow-up. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, and TypeScript sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70 through #117 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; `MailboxProbeOutcome` error fields honestly nullable (`ErrorCode`/`BridgeErrorCode`/`ErrorMessage`); `FailureReason` nullability carries the null-iff-Succeeded invariant; zero null-forgiving operators in the new production files (reviewer grep). |
| **Null-safety** | PASS | `ArgumentNullException.ThrowIfNull` guards every constructor and both evaluator entry points; `ArgumentException.ThrowIfNullOrWhiteSpace` guards the probe's mailbox parameter; options rules enforce non-whitespace fail-closed at startup. |
| **Async / resource safety** | PASS | The probe delegates request construction/disposal to the existing executor pipeline (per-attempt factory, `using`-disposed messages — unchanged F13 code); cancellation tokens flow end-to-end (pinned by tests at the probe, validator, and hosted-service levels); no new timers or delays. |
| **Exceptions fail-fast** | PASS | Enabled-without-Graph throws `InvalidOperationException` at composition time; invalid options throw `OptionsValidationException` at startup via `ValidateOnStart`; a failed boundary throws `InvalidOperationException` naming the `FailureReason` from `StartAsync`, aborting host startup; no catch-all handlers anywhere in the new code (zero `catch` blocks — failures arrive as envelope data by design). |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces in all 19 new `.cs` files; PascalCase publics; `Async` suffix; repository field convention followed. |
| **No new suppressions and no banned APIs** | PASS | Reviewer grep of both new production folders and the new test folders for pragma, SuppressMessage, nullable directives, dynamic, wall-clock APIs, and sleeps returned zero matches. The evaluator is clock-free; probe time flows through the injected `TimeProvider`. |
| **Dependency policy** | PASS | Zero new package dependencies. The only csproj change is an `InternalsVisibleTo("DynamicProxyGenAssembly2")` assembly attribute enabling Moq proxies for the internal `IMailboxScopeProbe` (commented in the csproj; Info note CR-120-02 in the code review). |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq + CsCheck. The new tests follow the established repo convention, consistent with the prior validated #70 through #117 audits (spec.md Constraints & Risks records the MSTest convention explicitly as a known pre-existing mismatch).

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq + CsCheck)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataTestMethod]`+`[DataRow]`; FluentAssertions matchers; Moq for `IMailboxScopeProbe`/`IAppTokenProvider`; the existing `FakeHttpHandler` fixture for the Graph boundary; CsCheck `Gen`/`Sample` per suite convention; NetArchTest for the boundary suite. |
| **Test file location** | PASS | Eight test files under `tests/OpenClaw.Core.Tests/ScopeValidation/` mirroring `src/OpenClaw.Core/ScopeValidation/`; one under `tests/OpenClaw.Core.Tests/CloudGraph/` mirroring the probe's location. No colocation in the production tree. |
| **Coverage expectation** | PASS | OpenClaw.Core package 92.82% line / 81.40% branch; every instrumented new file 100.00% line and 100.00% branch under full instrumentation (async bodies included — no exclusion masking on this branch, Section 5); no regression. |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); CsCheck 4.7.0 is referenced by the test project. Four genuine CsCheck properties cover all three new pure functions: `IsAuthorizationDenial` true-iff-all-three-conjuncts over a generated outcome space that includes the accepted constants, case variants, the 401 code, other D5 codes, and null (`ScopeBoundaryEvaluatorPropertyTests`, 1); the `Evaluate` invariant pair `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason is null == Succeeded` over generated outcome pairs (1); options-validator disabled-always-valid over generated field values and determinism over generated options (`ScopeValidationOptionsValidatorPropertyTests`, 2). |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80 through #117 T1 audits). The spec schedules Stryker >= 75% for the nightly stage; the classifier's constant/conjunction tests are designed as mutant-killing rows. |
| **Determinism (no sleeps, no wall clock)** | PASS | Pure functions clock-free; `FakeTimeProvider` injected wherever the executor seam requires a clock; seeded CsCheck with failing-seed printing; zero `Thread.Sleep`/`Task.Delay`/wall-clock reads in test code (reviewer grep: zero matches). |
| **No temporary files** | PASS | Graph payloads are inline raw strings; configuration is in-memory; zero filesystem access in any new test (reviewer grep). |
| **No live network** | PASS | Zero bare `new HttpClient()`-over-real-handler or live-endpoint constructions in the new test files; the probe's `HttpClient` wraps `FakeHttpHandler` with a `graph.example.test` base address (URL-shape assertions only). |
| **Focused / isolated** | PASS | Fresh doubles/options/logger per test; recorded call order asserted per test; no shared fixtures beyond static readonly immutable records. |

---

## 5. Test Coverage Detail

Reviewer-parsed per-file coverage at branch head (fresh cobertura, line AND branch, duplicate class entries deduplicated per file+line). Primary attestation is the PLAIN instrumentation run (`coverage-review/`), in which — unlike every prior C# review on this repository — async method bodies ARE instrumented, because the plain `dotnet test --collect:"XPlat Code Coverage"` command does not apply the `mailbridge.runsettings` CompilerGenerated exclusion. The repo-convention run (`coverage-review-settings/`, `--settings mailbridge.runsettings`) is reported for comparability with prior audits.

| File | Status | Line (plain) | Branch (plain) | Notes |
|------|--------|------|--------|-------|
| ScopeBoundaryEvaluator.cs | NEW | 56/56 = 100.00% | 16/16 = 100.00% | Pure classifier + evaluator; every conjunct arm and reason-composition branch covered. |
| ScopeBoundaryStartupValidator.cs | NEW | 54/54 = 100.00% | 6/6 = 100.00% | Async `StartAsync` body fully instrumented and covered (lines 47-91), both log branches + throw. |
| GraphMailboxScopeProbe.cs | NEW | 37/37 = 100.00% | 6/6 = 100.00% | Async `ProbeMailboxReadAsync` body fully instrumented and covered (lines 56-77) incl. the envelope projection. |
| ScopeValidationServiceCollectionExtensions.cs | NEW | 36/36 = 100.00% | 4/4 = 100.00% | All three D6 registration branches covered. |
| ScopeValidationOptionsValidator.cs | NEW | 32/32 = 100.00% | 12/12 = 100.00% | Every rule and the multi-fault path covered. |
| ScopeBoundaryValidator.cs | NEW | 26/26 = 100.00% | no branches | Async `ValidateAsync` body fully instrumented and covered. |
| ScopeBoundaryValidationResult.cs | NEW | 10/10 = 100.00% | no branches | Record definition. |
| MailboxProbeOutcome.cs | NEW | 6/6 = 100.00% | no branches | Record definition. |
| ScopeValidationOptions.cs | NEW | 3/3 = 100.00% | no branches | Options bag (auto-properties instrumented in the plain run). |
| IMailboxScopeProbe.cs | NEW | interface-only | interface-only | No executable behavior; legitimately omitted from measurement per the coverage policy clarification. |
| Program.cs | MODIFIED | 264/304 = 86.84% (plain); 258/258 = 100.00% (convention) | 37/66 = 56.06% (plain); 6/6 = 100.00% (convention) | Changed lines 69-72 (the single `AddScopeBoundaryValidation` call) covered in BOTH runs (12 hits, `WebApplicationFactory` composition-root tests). Every plain-run missed line and partial branch is pre-existing endpoint-lambda/opt-in code from other features, verified entirely outside the changed range 69-74; the prior audits' convention measurement reports the file at 100.00%/100.00%. No regression on changed lines. |
| OpenClaw.Core.csproj | MODIFIED | n/a | n/a | Assembly-attribute item only (not coverage-measurable). |

Test suites delivered (9 files, 55 test methods, 65 runtime cases): D3 classification matrix + D4 pair matrix (`ScopeBoundaryEvaluatorTests`, 13 methods incl. a 6-row DataRow matrix), 2 evaluator CsCheck properties, composition suite (5), hosted-service suite (5, incl. token/body log-absence and cancellation), options-validator directed suite (8 methods incl. two 3-row DataRow matrices), 2 options CsCheck properties, DI extension suite (9 incl. a 2-row fail-closed DataRow), probe contract suite (8), and the 3-rule NetArchTest boundary suite.

**Coverage:** every instrumented new file at 100.00% line and 100.00% branch under full instrumentation. **Gap:** none.

**Regression:** zero existing test files modified (reviewer-verified from the branch diff — the only test changes are the nine new files); all 716 baseline Core.Tests cases pass inside the reviewer's 781/781 run; the untouched packages report the same pass counts as baseline (100 HostAdapter, 347+5 MailBridge).

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 1233 (1228 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 781 passed / 781 (baseline 716; +65 = the new ScopeValidation/probe cases) | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~2 s | PASS |
| OpenClaw.Core package coverage | 92.82% line, 81.40% branch (plain; baseline 92.49%/80.90%) | PASS |
| Pooled solution coverage | 89.97% line / 78.30% branch (plain); 93.06% / 84.00% (convention) | PASS |
| New-code per-file coverage | 100.00% line, 100.00% branch on every instrumented new file (async bodies included) | PASS |
| Modified file (Program.cs) changed lines | Covered in both runs; 100.00%/100.00% under the convention measurement | PASS |
| Net new tests vs baseline | +65 runtime cases (55 methods incl. 4 CsCheck properties) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 341 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest, D1 rules) | Included in `dotnet test` Core.Tests run | 781/781 pass (ScopeValidation, CloudGraph, CloudSync, and Agent boundary suites included) | PASS |
| MSTest tests + coverage (plain) | `dotnet test --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/qa-gates/coverage-review"` | 1228 passed, 0 failed, 5 skipped | PASS |
| MSTest tests + coverage (repo convention) | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/qa-gates/coverage-review-settings"` | 1228 passed, 0 failed, 5 skipped | PASS |
| Untouched-surface diff | `git diff --name-only d67dea0..HEAD -- src/OpenClaw.HostAdapter.Contracts "src/OpenClaw.Core/Agent/MessagePollingWorker*"` | Empty — the F13 contract project and `MessagePollingWorker` unchanged (D1/D8) | PASS |
| File-size cap | `wc -l` over all changed `.cs` files | Production max 124 new / 364 Program.cs; test max 268 — all under 500 | PASS |
| Evidence-location scan | `git diff --name-only d67dea0..HEAD \| grep -E '^artifacts/'` | No matches | PASS |
| Banned-API / suppression scan | grep over the new production and test folders for pragma / SuppressMessage / nullable directives / dynamic / wall-clock APIs / sleeps | Zero matches | PASS |
| HI-1 orchestrator-state record | Reviewer read of `artifacts/orchestration/orchestrator-state.json` | `human_interaction.requirements[]` contains HI-1 with `response: "exception"` and `runbook_path` exactly matching the committed runbook; all three `orchestrator-state.md` invariants satisfied | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `9961429` on 2026-07-06. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change. The `modified-workflow-needs-green-run` policy rule does not fire: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths appear in the diff.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** One Minor cross-reference and two informational observations, recorded (not policy violations that reduce any gate):

- **Public accessibility of three spec-designated-internal types (Minor, CR-120-01, cross-reference).** `ScopeValidationServiceCollectionExtensions`, `ScopeValidationOptions`, and `ScopeValidationOptionsValidator` are `public` while the spec's "API / CLI Surface" section states "All new types are `internal`". The accessibility mirrors the sanctioned F13 trio exactly (public options bag + pure validator + Add* extension, #115 audit) and there are no external callers; the divergence is textual, documented with a concrete recommendation in `code-review.2026-07-06T23-55.md`. No gate is affected.
- **Program.cs plain-instrumentation figures (Informational).** Under full (plain) instrumentation the whole-file Program.cs figures are 86.84% line / 56.06% branch because the plain run instruments the minimal-API endpoint lambdas and opt-in blocks of OTHER features that the prior audits' convention measurement excludes as compiler-generated. The reviewer verified every missed line and partial branch lies outside this branch's changed range (lines 69-74); the changed lines are covered in both runs, and the convention measurement used by all prior accepted audits reports the file at 100.00% line and branch. Pre-existing condition of the composition root, not attributable to or worsened by this branch; the standing recommendation to keep the host-bound file minimal (coverage-exclusion policy) is already honored by this feature's one-call addition.
- **Instrumentation-mode note (Informational, favorable).** The executor's coverage commands ran WITHOUT `--settings mailbridge.runsettings`, so the CompilerGenerated exclusion that masked async bodies on the #99-#117 reviews did not apply: all new async bodies are directly measured at 100.00%. The executor's per-file claims were independently re-measured and matched exactly. No behavioral-verification substitution was needed on this branch.

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("Restore failed"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70 through #117 audits. The format check ran to EXIT 0 over all 341 files.
- **MCP template/validator tools:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing C# artifact sets (issue #115 review and the #117 re-audit accepted 2026-07-06) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **PR-context collector:** the canonical PR-context artifacts were absent at review start and no repo-local collector script exists; the reviewer regenerated `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` directly from the authoritative git range using the supplied base branch (see Rejected Scope Narrowing observation). All evidence anchors in this audit cite the git range or committed files directly.
- **Live-tenant verification (HI-1):** recorded in orchestrator state as a `human_interaction` exception with the D7 runbook (`runbooks/negative-scope-startup-validation.runbook.md`), mirroring the F11 HI-1 precedent. No CI job attempts a live Graph call.

### Removed/Skipped Tests

- **None removed.** No existing test file was modified, deleted, or weakened (reviewer-verified: the branch diff contains only the nine new test files under `tests/`). The 5 solution skips are pre-existing environment-gated COM/publish tests, unchanged.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `d67dea0`)

Branch `feature/negative-scope-smoke-test-120`, head `99614291f2d81693e3e36e0a302b223d2849e58e`. Range: `d67dea0117984b980b093f1c942c9a4762b8b25f..99614291f2d81693e3e36e0a302b223d2849e58e` (41 files, +3355/-3). Commits: `9b2f09f` docs scaffold (issue, spec, user-story, research, plan, runbook), `231fef6` merge of the epic base, `9961429` feat(core) — the implementation, tests, and evidence.

### Files Modified (categories)

1. **`src/OpenClaw.Core/ScopeValidation/`** (NEW, 9 files, 536 lines total) — the host-neutral scope-validation core: outcome/result records, the probe port, the pure fail-closed evaluator (D3/D4), the options bag + pure full-list validator (D6), the two-probe orchestrator and one-shot startup hosted service (D5), and the three-branch DI registration.
2. **`src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs`** (NEW, 78 lines) — the Graph-bound probe implementation reusing the F13 `GraphRequestExecutor` pipeline (D2).
3. **`src/OpenClaw.Core/Program.cs`** (MODIFIED, +6) — the single unconditional `AddScopeBoundaryValidation` call after the backend-selection block; **`src/OpenClaw.Core/OpenClaw.Core.csproj`** (MODIFIED, +5) — the commented `InternalsVisibleTo("DynamicProxyGenAssembly2")` grant for Moq proxies of the internal port.
4. **`tests/OpenClaw.Core.Tests/`** (NEW, 9 files, 1457 lines total) — 65 runtime cases: classification/pair matrices, 4 CsCheck properties, composition, hosted-service, options, DI, probe-contract, and D1 architecture suites.
5. **`docs/features/active/2026-07-06-negative-scope-smoke-test-120/`** (NEW, 18 files) — issue/spec/user-story/research/plan, the D7 runbook, and canonical evidence (baseline, qa-gates); **`.claude/agent-memory/task-researcher/`** — 2 memory records (metadata, not code).

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The C# change passes formatting, linting, nullable type-checking, the D1 architecture-boundary suite, the full unit-test suite, and the uniform coverage gates, all independently re-run by the reviewer at branch head under two instrumentation modes. The T1 property-test obligation is satisfied directly: four genuine CsCheck properties covering all three new pure functions. The feature introduces zero new package dependencies, is inert by default (disabled registers nothing; the default configuration is byte-identical in behavior — pinned by the DI disabled/absent tests), preserves the F13 contract and `MessagePollingWorker` untouched (verified empty diffs), no existing test was modified, and test data is inline raw strings with no live network path. Every instrumented new production file measures 100.00% line and 100.00% branch including async bodies. No evidence-location or file-size violations. No new suppressions or banned-API additions. The `modified-workflow-needs-green-run` rule does not fire (verified: no workflow, benchmark, or action paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered. The HI-1 `human_interaction` record satisfies all three `orchestrator-state.md` invariants.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (pure core behind a narrow port; executor pipeline reused, not duplicated; zero new dependencies)
- Module & File Structure: PASS (all files under 500 lines, production max 124 new; Minor accessibility note CR-120-01)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-closed classifier; composition-time and startup-time fail-fast; no catch-alls)
- Dependency Policy: PASS (zero new packages; one commented InternalsVisibleTo grant)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (package 92.82%/81.40%, improved over baseline; every instrumented new file 100%/100% incl. async bodies)
- Test Structure: PASS
- External Dependencies: PASS (mocked probes/handlers, inline fixtures, no temp files, no live Graph calls)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq + CsCheck repo convention; tests/ mirror)
- Determinism: PASS (pure clock-free core; FakeTimeProvider at the executor seam; seeded CsCheck)
- T1 obligations: PASS (four genuine properties covering all three pure functions; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 1228/1228 runnable solution tests passing (5 pre-existing environment-gated skips)
- OpenClaw.Core package: 92.82% line / 81.40% branch (gates: 85%/75%), both improved over baseline (+0.33pp/+0.50pp)
- Pooled solution: 89.97% line / 78.30% branch (plain); 93.06% / 84.00% (repo convention)
- New code: 100.00% line and 100.00% branch on every instrumented new file, async bodies directly measured
- Modified Program.cs: changed lines covered in both runs; 100.00%/100.00% under the convention measurement
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- All 20 touched `.cs` files under the 500-line cap (new production max 124)

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, regression evidence, and policy requirements pass against branch head `9961429`. No remediation inputs are required. Operational note (from spec, not a gate): the validation is dark by default; enabling it requires the Graph adapter plus the two `OpenClaw__ScopeValidation__*` mailbox UPNs from the F11 handoff package, and the live-tenant pass/negative-rehearsal confirmation is the HI-1 human runbook action, including capture of the tenant-observed denial code.

---

## Appendix A: Test Inventory

C# test changes in this feature (8 files in `tests/OpenClaw.Core.Tests/ScopeValidation/`, 1 in `tests/OpenClaw.Core.Tests/CloudGraph/`):

1. `ScopeBoundaryEvaluatorTests.cs` (NEW, 268 lines, 13 methods / 18 cases) — D3 classification matrix: the real 403 RBAC denial as the only true case; 401-shaped `UNAUTHORIZED` with null and `InvalidAuthenticationToken` bridge codes; six other-code DataRows (`CONFIGURATION_ERROR`, `NOT_FOUND`, `THROTTLED`, `TRANSPORT_FAILURE`, `INVALID_REQUEST`, `INTERNAL_ERROR`); `Ok == true`; both case-variant constants pinning Ordinal. D4 pair matrix: (allowed, denied) pass with null reason; (denied, denied) in-scope-only reason without the join; (allowed, allowed) scope-leak reason; (denied, allowed) `"; "`-joined both-sides reason; (allowed, wrong-error) with the observed `{ErrorCode}/{BridgeErrorCode}` quoted; null-BridgeErrorCode dash substitution; verbatim outcome composition (`BeSameAs`).
2. `ScopeBoundaryEvaluatorPropertyTests.cs` (NEW, 95 lines, 2 CsCheck properties) — `IsAuthorizationDenial` true-iff-all-three-conjuncts over a generated vocabulary including the accepted constants, case variants, the 401 code, other codes, and null; `Evaluate` invariant pair (`Succeeded == InScopeAllowed && OutOfScopeDenied`; `FailureReason is null == Succeeded`) over generated outcome pairs.
3. `ScopeBoundaryValidatorTests.cs` (NEW, 162 lines, 5 tests) — both probes invoked with the configured mailboxes; in-scope-first order; no short-circuit when in-scope fails; verbatim outcome composition; cancellation token passed to both probes.
4. `ScopeBoundaryStartupValidatorTests.cs` (NEW, 197 lines, 5 tests) — success logs a single Information entry carrying every result field; failure logs a single Critical entry containing the `FailureReason` and throws `InvalidOperationException` naming it; log output contains no bearer token or response-body text; pre-cancelled token propagates `OperationCanceledException`; `StopAsync` completes synchronously. Uses a minimal in-file capturing logger (no I/O).
5. `ScopeValidationOptionsValidatorTests.cs` (NEW, 156 lines, 8 methods / 12 cases) — disabled always valid (arbitrary and empty fields); null/empty/whitespace DataRows per UPN with key-named violations; equal and case-variant-equal UPNs violate distinctness (OrdinalIgnoreCase); multi-fault reporting; messages never echo configured values.
6. `ScopeValidationOptionsValidatorPropertyTests.cs` (NEW, 76 lines, 2 CsCheck properties) — disabled yields zero violations for any generated field values; the validator is deterministic for any generated options.
7. `ScopeValidationServiceCollectionExtensionsTests.cs` (NEW, 193 lines, 9 methods / 10 cases) — disabled and absent-section register nothing; enabled-without-Graph throws at composition time; enabled-with-Graph resolves probe/validator/hosted service; per-key options binding; invalid options fail closed via `OptionsValidationException` (DataRows: missing in-scope UPN, equal UPNs); null-services and null-configuration guards. In-memory configuration only.
8. `ScopeValidationArchitectureBoundaryTests.cs` (NEW, 83 lines, 3 tests) — NetArchTest rules pinning D1: the four pure-core types have no dependency on `OpenClaw.Core.CloudGraph`, `System.Net.Http`, or `Microsoft.Extensions.Logging`.
9. `GraphMailboxScopeProbeTests.cs` (NEW, 227 lines, 8 tests) — request shape (GET `users/{upn}/messages` with `$top=1`/`$select=id`); reserved-character UPN percent-encoding; bearer Authorization and `client-request-id` headers; 200-empty-value → Ok; 403 `ErrorAccessDenied` → `(false, UNAUTHORIZED, ErrorAccessDenied, ...)`; 401 `InvalidAuthenticationToken` → `UNAUTHORIZED` with the 401 Graph code; unparseable error body → null `BridgeErrorCode`; cancellation token flow-through into the executor. All via `FakeHttpHandler`.

Reviewer run: `OpenClaw.Core.Tests` 781 passed, 0 failed; solution total 1228 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build

# Tests + coverage, plain instrumentation (async bodies measured; reviewer results directory is the canonical evidence path)
dotnet test --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/qa-gates/coverage-review"

# Tests + coverage, repo runsettings convention (comparability with prior audits)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/qa-gates/coverage-review-settings"

# Untouched-surface verification (empty output expected)
git diff --name-only d67dea0117984b980b093f1c942c9a4762b8b25f..HEAD -- src/OpenClaw.HostAdapter.Contracts "src/OpenClaw.Core/Agent/MessagePollingWorker*"

# File-size cap
git diff --name-only d67dea0117984b980b093f1c942c9a4762b8b25f..HEAD -- "*.cs" | xargs wc -l

# Evidence-location scan
git diff --name-only d67dea0117984b980b093f1c942c9a4762b8b25f..HEAD | grep -E '^artifacts/'

# Banned-API / suppression / hygiene scans of the changed files
grep -rnE '#pragma|SuppressMessage|#nullable|\bdynamic\b|Thread\.Sleep|Task\.Delay|DateTime\.Now|DateTime\.UtcNow|Random\.Shared' src/OpenClaw.Core/ScopeValidation src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs tests/OpenClaw.Core.Tests/ScopeValidation tests/OpenClaw.Core.Tests/CloudGraph/GraphMailboxScopeProbeTests.cs

# HI-1 record verification (read-only)
python -c "import json; d=json.load(open('artifacts/orchestration/orchestrator-state.json')); print(d['human_interaction'])"
```
