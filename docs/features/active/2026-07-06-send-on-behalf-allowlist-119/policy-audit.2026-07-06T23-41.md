# Policy Compliance Audit: send-on-behalf-allowlist (#119)

**Audit Date:** 2026-07-06
**Code Under Test:** C# only. 1 NEW production `.cs` file (`src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`, 101 lines) plus 3 MODIFIED production `.cs` files (`GraphAdapterOptions.cs` +16 to 97 lines, `GraphAdapterOptionsValidator.cs` +8 to 87 lines, `GraphHostAdapterClient.SendMail.cs` rewritten gate to 106 lines). 2 NEW test `.cs` files (`SendOnBehalfAuthorizerTests.cs` 132 lines, `SendOnBehalfAuthorizerPropertyTests.cs` 213 lines) plus 4 MODIFIED test `.cs` files (`GraphHostAdapterClientSendMailTests.cs` +251 to 489 lines, `GraphAdapterOptionsValidatorTests.cs` +127 to 413 lines, `GraphServiceCollectionExtensionsTests.cs` +31 to 183 lines, `CloudGraphContractParityTests.cs` fixture-only +19/-16 to 220 lines). Plus 20 feature scoping/evidence/runbook Markdown files (feature folder for issue #119) and 2 agent-memory Markdown files (task-researcher state records). No Python, PowerShell, TypeScript, or Bash files changed in the branch diff.

**Scope:** Full feature branch (epic child F15) @ `03e80e25e9ba75cd463e2b32b46548f14dc416b5` versus resolved base `epic/openclaw-vision-integration` @ merge-base `d67dea0117984b980b093f1c942c9a4762b8b25f` (merge-base timestamp 2026-07-06T22:49:06-04:00; supplied by the caller and reviewer-confirmed with `git merge-base`). Scope is feature-vs-base over the complete branch diff: 32 files, +2202/-34 (name-status: 10 `.cs`, 22 `.md`). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md`.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 1 production `.cs` new + 3 production `.cs` modified + 6 test `.cs` (2 new, 4 extended) | 1197 (solution) / 745 (Core.Tests) | 1192 pass, 0 fail, 5 env-gated skips | 93.73% line, 85.25% branch (Core.Tests report; executor baseline — see staleness note, non-gating) | 94.61% line, 86.08% branch (Core.Tests report, reviewer re-run matching the executor to the hundredth; OpenClaw.Core package deduped 99.36% line / 92.87% branch; pooled solution deduped 97.56% line / 90.90% branch) | `SendOnBehalfAuthorizer.cs` (new) 100.00% line (20/20), 87.50% branch (7/8); modified `GraphAdapterOptionsValidator.cs` 100.00%/100.00% and `GraphHostAdapterClient.SendMail.cs` 100.00%/100.00%; `GraphAdapterOptions.cs` auto-property-only, uninstrumented under the pre-existing runsettings CompilerGenerated exclusion, behaviorally covered by binding/validator tests |

**Note:** Python, PowerShell, Bash, and TypeScript rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit PASS.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/baseline/csharp-test-coverage.2026-07-06T22-45.md` (line 93.73% / branch 85.25%, Core.Tests report; captured 2026-07-06T22-45, four minutes before the merge-base commit — see Section 8 staleness note)
- C# post-change coverage artifact: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/csharp-test-coverage.2026-07-06T23-21.md` and `evidence/qa-gates/coverage-comparison.2026-07-06T23-21.md`
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head): `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/coverage-review/{8e142a0b...,a7b712a7...,a84c9873...}/coverage.cobertura.xml`; independently parsed with per-file line AND branch re-measured for all 4 changed production files, duplicate class entries deduplicated per file+line
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura at branch head. The C# coverage gate is met (Core.Tests report 94.61% line >= 85% and 86.08% branch >= 75%; OpenClaw.Core package deduped 99.36%/92.87%; pooled solution deduped 97.56%/90.90%; new file `SendOnBehalfAuthorizer.cs` at 100.00% line / 87.50% branch, above the uniform per-file gates; both modified logic files at 100.00% line and 100.00% branch with no regression on changed lines).

---

## Executive Summary

This feature branch closes issue #119 (epic child F15): a deterministic, fail-closed, per-send authorization gate for Graph send-on-behalf principal representation, closing the gap F13 design decision D7 explicitly deferred. The delivery is four additive pieces in `OpenClaw.Core.CloudGraph`: (a) `GraphAdapterOptions.AllowedPrincipalMailboxUpns`, an additive get-only string collection bound from indexed configuration keys, defaulting to an empty collection whose semantics are deny-all-on-behalf (fail-closed, D1); (b) `SendOnBehalfAuthorizer`, an internal static pure function `Authorize(principal, assistant, allowlist)` returning the three-value `SendAuthorizationDecision` enum with trimmed `OrdinalIgnoreCase` comparison (D2); (c) a gate at the top of `SendMailAsync` that, on `DeniedNotAllowlisted`, returns a synchronous failure envelope (`UNAUTHORIZED` / `SendOnBehalfDenied` / `Retryable=false`, message naming the configuration key and echoing no UPN, one warning log with request id only) before `executor.ExecuteAsync` — hence before token acquisition and any HTTP request (D5); and (d) a shape-only validator rule rejecting whitespace-only allowlist entries when `Enabled` (D3). `ComposeSendMailBody` now consumes the shared decision instead of re-deriving the principal/assistant comparison, so the from-injection predicate and the authorization decision share a single source. Tenant-side controls (`GrantSendOnBehalfTo` grant, rendered-appearance validation) are correctly drawn out of code scope into a human runbook recorded as a `human_interaction` exception in the orchestrator checkpoint (D7).

The mandatory toolchain was independently re-run by the reviewer against branch head `03e80e2` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 325 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** `CloudGraphArchitectureBoundaryTests` re-run in isolation by the reviewer — 3/3 pass (the new `SendOnBehalfAuthorizer` type falls under the existing `OpenClaw.Core.CloudGraph` namespace-prefix rules automatically); the full boundary suites also pass inside the 745/745 Core.Tests run.
- **Tests + coverage:** full solution `dotnet test` with XPlat Code Coverage — 1192 passed, 0 failed, 5 environment-gated skips (same skips as baseline); Core.Tests report 94.61% line / 86.08% branch, above the uniform gates; T1 property-test obligation satisfied with five genuine CsCheck properties (case-invariance, deny-completeness, membership soundness, self-send dominance on `Authorize`; whitespace-presence-drives-the-violation on the validator rule).
- **Regression evidence:** all pre-existing send-mail, D5 error-mapping, throttling, contract-parity, and architecture tests pass; the executor's diff-scope record proves no existing test method body was modified (helper/fixture-only extensions); fail-before/pass-after evidence for the deny gate (`send-on-behalf-gate-fail-before.2026-07-06T23-13.md`, EXIT 1 with exactly the three new deny contract tests failing, then EXIT 0).

Notably, no async-body instrumentation masking occurs on this branch: `SendMailAsync` is a synchronous task-returning method, and the reviewer confirmed the instrumented line set spans the entire gate body (lines 34-105), so the deny path, envelope synthesis, and from-injection arms are all measured directly.

No Blocking findings. Two Minor findings (the untested null-entry defensive branch arm in the authorizer, and the baseline evidence captured four minutes before the merge-base — both non-gating, Section 8) and three informational observations. Remediation is not required. The feature is recommended Go for PR.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/csharp.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/orchestrator-state.md` (evaluated — the `human_interaction` block in the checkpoint satisfies all three invariants: `requirements` list present, `response` is the enum member `exception`, non-empty `runbook_path`)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is one new production file, three modified production files, six test files, the feature folder documentation/evidence/runbook set, and two agent-memory records. The executor's raw cobertura intermediates under `artifacts/csharp/baseline-2026-07-06T22-45/` and `artifacts/csharp/core-coverage.post-change.2026-07-06T23-21.cobertura.xml` are untracked (gitignored) and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`epic/openclaw-vision-integration`), merge-base SHA (`d67dea0`), and branch head (`03e80e2`), and stated "Determine review scope yourself from the branch diff against the merge-base; evaluate every changed language's full toolchain and coverage obligations." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only. The caller's framing of live-tenant Exchange configuration as runbook-covered matches the spec's own recorded scope boundary (D7) and the orchestrator checkpoint's `human_interaction` exception; it is a legitimate code/tenant boundary, not a scope narrowing, and this audit evaluated the boundary itself (Section 8).

Observation (not a narrowing, recorded for completeness): the canonical PR-context artifacts at `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` were absent from the worktree at review start despite the caller describing them as refreshed. The reviewer regenerated both directly from git against the supplied base and merge-base per the `pr-context-artifacts` refresh rule before proceeding (commands in Appendix B). Because the reviewer generated the summary from the raw authoritative diff, the recurring summary-misclassification quirk from prior reviews does not apply to this artifact pair.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only d67dea0..03e80e2 | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/<kind>/` locations (baseline, qa-gates, regression-testing, other).
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own coverage evidence was written to the canonical `evidence/qa-gates/coverage-review` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70 through #117 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Every test constructs its own options, handler double, `FakeTimeProvider`, Moq token provider, recording logger, and (binding tests) its own `ServiceCollection` with in-memory configuration; no shared state, no static mutation. 745/745 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | One decision-table row per authorizer test; one deny-contract aspect per send-mail test (envelope fields, zero-I/O, message content, log discipline each asserted in dedicated methods a-f); one validator scenario per method/DataRow; one binding path per DI test. |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 745 tests in ~2 s (reviewer run); all new tests are in-memory computation, mocked handlers, and in-memory configuration binding — no I/O, no live Graph calls. |
| **Determinism** | PASS | `FakeTimeProvider` for all time; CsCheck uses the suite's seeded `Gen`/`Sample` convention (`iter: 1000`, failing seed printed); fixed UPN constants; no sleeps, timers, network, or filesystem (reviewer grep over the five touched test files: zero matches for banned APIs — Section 7). |
| **Readability & Maintainability** | PASS | Descriptive scenario names (`Authorize_EmptyAllowlistWithDifferingPrincipal_ReturnsDeniedNotAllowlisted`, `SendMail_NonAllowlistedPrincipal_DeniesBeforeAnyIo`), FluentAssertions because-messages, Arrange/Act/Assert with decision-table row comments, XML docs on test classes citing the spec rows and properties covered. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS (with a Minor staleness note) | Baseline Core.Tests report: 93.73% line (2588/2761), 85.25% branch (682/800). Source: `evidence/baseline/csharp-test-coverage.2026-07-06T22-45.md`. The baseline was captured four minutes before the merge-base commit and omits the #121 CloudSync merge; reviewer-verified that the #121 merge touched no CloudGraph path, so the per-file baselines for every touched file are exact (Section 8, CR-119-02). |
| **No Coverage Regression** | PASS | Post-change Core.Tests report: 94.61% line (3035/3208), 86.08% branch (742/862) — reviewer re-run and re-parsed, identical to the executor's committed figures to the hundredth. Both modified logic files (`GraphAdapterOptionsValidator.cs`, `GraphHostAdapterClient.SendMail.cs`) are at 100.00% line and 100.00% branch at baseline and at head — no regression on changed lines is possible. |
| **New Code Coverage** | PASS | `SendOnBehalfAuthorizer.cs` (new): 100.00% line (20/20), 87.50% branch (7/8), reviewer-parsed from fresh deduped cobertura. The single partial arm is the defensive `entry is not null` null-guard (line 90, 3/4 conditions) — Minor CR-119-01 with a concrete test recommendation. Test files are excluded from measurement per policy. |
| **Comprehensive Coverage** | PASS | All seven spec decision-table rows as directed authorizer tests; four CsCheck properties over the pure function; deny contract proven at the client seam (envelope fields, meta, zero handler invocations, strict never-called token provider, message/log content); allow paths (exact, case-differing entry) with body-shape assertions; self-send with empty allowlist and no `from`; validator whitespace matrix (empty/space/tab), empty-list validity, disabled-mode pass-through, plus a CsCheck property; indexed-key binding and `ValidateOnStart` failure. |
| **Positive Flows** | PASS | Allowlisted on-behalf send posts to `users/{a}/sendMail` with `from = {p}` and 202 maps to ok/null; self-send succeeds without `from`; clean allowlists validate; indexed keys bind in order. |
| **Negative Flows** | PASS | Non-allowlisted and empty-allowlist denies; whitespace-entry validator violation (exactly one, names the key, echoes no value); `ValidateOnStart` throw; authorizer null-argument guards implicitly exercised via `ArgumentNullException.ThrowIfNull` (guard lines instrumented and covered). |
| **Edge Cases** | PASS | Case-only and whitespace-padding-only differences on both the principal and allowlist entries; duplicates; self-send dominance with the principal present in the allowlist; whitespace-padded principal trimming. |
| **Error Handling** | PASS | Deny envelope carries `UNAUTHORIZED` / `SendOnBehalfDenied` / `Retryable=false` with correct `Meta.RequestId` and `AdapterVersion "cloudgraph"`; exactly one warning log with the request id and no UPN. |
| **Concurrency** | N/A | The authorizer is a pure static function; the gate adds no shared mutable state. |
| **State Transitions** | N/A | No stateful component introduced; the decision is a pure function of configuration inputs. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 93.73% line, 85.25% branch (Core.Tests report; executor baseline captured four minutes before the merge-base, non-gating staleness recorded as Minor CR-119-02 because the intervening #121 merge touched no CloudGraph path) -> Post-change: 94.61% line, 86.08% branch (same report shape, reviewer re-run at branch head matching the executor to the hundredth; OpenClaw.Core package deduped 99.36% line and 92.87% branch; pooled solution deduped 97.56% line and 90.90% branch). Change: +0.88% line, +0.83% branch. New/changed-code coverage: SendOnBehalfAuthorizer.cs (new) 100.00% line (20/20) and 87.50% branch (7/8, single partial arm is the line-90 defensive null-entry guard, Minor CR-119-01); GraphAdapterOptionsValidator.cs 100.00% line (54/54) and 100.00% branch (36/36); GraphHostAdapterClient.SendMail.cs 100.00% line (56/56) and 100.00% branch (4/4) with the entire synchronous gate body instrumented (no async-exclusion masking on this branch); GraphAdapterOptions.cs auto-property-only and uninstrumented under the pre-existing runsettings CompilerGenerated attribute exclusion with direct behavioral coverage by the indexed-key binding, defaults, and validator tests. Disposition: PASS (line >= 85%, branch >= 75%, no regression on changed lines). Evidence: evidence/baseline/csharp-test-coverage.2026-07-06T22-45.md, evidence/qa-gates/csharp-test-coverage.2026-07-06T23-21.md, evidence/qa-gates/coverage-comparison.2026-07-06T23-21.md, reviewer cobertura under evidence/qa-gates/coverage-review/.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions because-clauses on every assertion ("the deny occurs before token acquisition", "self-send dominates every allowlist regardless of contents", "a whitespace-only allowlist entry is the only violation"); CsCheck prints the failing seed. |
| **Arrange-Act-Assert Pattern** | PASS | All directed tests follow Arrange/Act/Assert; property tests use the suite's generator + `Sample` structure. |
| **Document Intent** | PASS | XML docs on the test classes state the spec decision-table rows and the four properties covered; each method carries a row-citing comment. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No live Graph calls: all HTTP flows through the mocked `FakeHttpHandler` with `BaseAddress = https://graph.example.test/v1.0/`; the deny tests additionally prove zero handler invocations. The three `graph.microsoft.com` strings in the touched tests are validation inputs and a default-value assertion, not network calls (executor `test-hygiene` gate, reviewer-confirmed). |
| **Use Mocks/Stubs** | PASS | `Mock<IAppTokenProvider>(MockBehavior.Strict)` — deliberately no setups on the deny path so any call throws; handler doubles; `RecordingLogger` for log-discipline assertions; `FakeTimeProvider`. |
| **Environment Stability** | PASS | No temporary files (reviewer grep: zero matches for GetTempPath/GetTempFileName/File.Write across the touched test files); no environment variables read; no mutable global state. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #119, `spec.md` v1.0 (seven recorded design decisions D1-D7 with the seven-row decision table, error-surface JSON, and config-key table), user-story scenarios 1-5, and the master-spec §5.2/Step 8 references define the change precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-06T22-16.md` present. |
| **Document the plan** | PASS | `plan.2026-07-06T22-16.md` with per-phase evidence under `evidence/`; the diff-scope verification record reconciles the delivered diff against the plan's allowed file list. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | One pure static function, one enum, one gate, one validator rule; no new abstractions, interfaces, or dependencies; the deny envelope reuses the existing `ApiError`/`ApiMeta` composition and `ResolveRequestId` helper. |
| **Reusability** | PASS | The authorization decision lives once (`SendOnBehalfAuthorizer.Authorize`) and is consumed by both the gate and `ComposeSendMailBody` — the previous inline `principalIsAssistant` re-derivation was removed, eliminating the divergence risk. |
| **Extensibility** | PASS | Additive options key with a fail-closed default; no public contract changes (`IHostAdapterClient`, `ApiError`, `ApiEnvelope`, wire DTOs untouched — reviewer-verified empty diffs); `BridgeErrorCode "SendOnBehalfDenied"` is an additive discriminator inside the existing closed code vocabulary (D4 caller audit recorded in spec). |
| **Separation of concerns** | PASS | Pure decision logic (authorizer, no I/O/clock/logging) is separate from the client's envelope synthesis and logging; the validator rule is shape-only; tenant state is explicitly out of code scope (D7). |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | All production changes under `src/OpenClaw.Core/CloudGraph/` in namespace `OpenClaw.Core.CloudGraph`; tests mirror under `tests/OpenClaw.Core.Tests/CloudGraph/`. |
| **Under 500 lines** | PASS | Reviewer-verified via `wc -l`: production max 106 (`GraphHostAdapterClient.SendMail.cs`), test max 489 (`GraphHostAdapterClientSendMailTests.cs`) — all 10 touched `.cs` files under 500. Executor evidence: `evidence/qa-gates/file-size-cap.2026-07-06T23-21.md`. |
| **Public vs internal** | PASS | `SendAuthorizationDecision` and `SendOnBehalfAuthorizer` are `internal` (tests reach them via the existing `InternalsVisibleTo`); the only public-surface change is the additive `AllowedPrincipalMailboxUpns` property on the already-public options bag. |
| **No circular dependencies** | PASS | No project-reference or package changes; the NetArchTest CloudGraph boundary suite passes (reviewer re-run 3/3, plus inside the full run). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `SendOnBehalfAuthorizer`, `SendAuthorizationDecision.DeniedNotAllowlisted`, `AllowedPrincipalMailboxUpns`, `AllowlistKey` — PascalCase, self-describing; `Async` suffix retained on `SendMailAsync`. |
| **Docs/docstrings** | PASS | XML docs on the enum members (decision totality, fail-closed-empty semantics), the authorizer (purity contract, single-source rationale), the options property (env-binding form, binder-friendly get-only collection rationale), and the gate (deny-before-I/O contract). |
| **Comment why, not what** | PASS | Comments explain the default-allowlist test-helper rationale, the fixture-only parity-test change, and self-send dominance — not line-by-line narration. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 325 files, EXIT 0. Executor: `evidence/qa-gates/csharp-format.2026-07-06T23-21.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. The authorizer's null-entry guard handles the theoretically-null collection element honestly rather than suppressing. |
| **4. Architecture** | PASS | `CloudGraphArchitectureBoundaryTests` 3/3 (reviewer re-run in isolation, plus inside the full 745/745 run); the new type falls under the existing namespace-prefix rules automatically. |
| **5. Testing** | PASS | Reviewer: full solution test run — 1192 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the five T1 CsCheck property tests. |
| **6. Contract/schema checks** | PASS | No wire contract, HTTP surface, or schema changed (`IHostAdapterClient`, `ApiError`, `ApiEnvelope`, wire DTOs untouched — reviewer-verified empty diffs for `src/OpenClaw.HostAdapter.Contracts` and `src/OpenClaw.MailBridge`); `CloudGraphContractParityTests` pass with the fixture-only allowlist addition and unchanged assertions. |
| **7. Integration tests** | N/A | Live-tenant on-behalf submission is tenant-dependent and explicitly out of automated scope (spec D7, Constraints & Risks); covered by the human runbook and the checkpoint `human_interaction` exception (Section 8). The handler-seam contract tests are the deepest integration available without a tenant. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass final QA set at 2026-07-06T23-21. |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in the reviewer cobertura under `evidence/qa-gates/coverage-review/`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy matches the delivered diff exactly (one new file, three changed files, one runbook, five test files named — plus the documented fixture-only parity-test touch recorded in the executor's diff-scope note). |
| **Design choices explained** | PASS | Seven recorded design decisions (D1 indexed-collection binding vs rejected delimited scalar; D2 pure internal authorizer; D3 runtime gate vs rejected startup membership validation with the read-routes rationale; D4 error-code reuse with the caller audit; D5 gate layer; D6 configuration-validated; D7 tenant-side out of code scope). |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `spec.md` and `user-story.md`; the behavior change for misconfigured deployments is documented in Versioning for the PR description. |
| **Provide next steps** | PASS | Spec records rollout as inert until `Enabled`, configuration-only fallback, the runbook handoff, and the F17 follow-on for live validation. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, and TypeScript sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70 through #117 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; no null-forgiving operators added by this diff outside the pre-existing serializer pattern in `ComposeSendMailBody` (unchanged shape from baseline). |
| **Null-safety** | PASS | `ArgumentNullException.ThrowIfNull` on all three authorizer parameters and on `request` in `SendMailAsync`; the allowlist loop guards individual entries against null. |
| **Async / resource safety** | PASS | The deny path returns `Task.FromResult` synchronously — no async state machine added, no blocking waits; the allowed path is the unchanged executor pipeline. |
| **Exceptions fail-fast** | PASS | Whitespace allowlist entries fail at startup via the existing `ValidateOnStart` path (full-violation-list style preserved); the runtime deny is an envelope, not an exception, matching the adapter's established error surface. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces in all touched files; PascalCase publics; repository field convention followed. |
| **No new suppressions and no banned APIs** | PASS | Reviewer grep of the four production files for pragma, SuppressMessage, nullable directives, and dynamic returned zero matches; no wall-clock or sleep APIs introduced. |
| **Dependency policy** | PASS | Zero new dependencies. No csproj changes anywhere in the diff. |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq + CsCheck. The new tests follow the established repo convention, consistent with the prior validated audits (#70 through #117); spec.md Constraints & Risks records the MSTest convention explicitly. Pre-existing repo-wide divergence, not a finding against this branch.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq + CsCheck)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataTestMethod]`+`[DataRow]`; FluentAssertions matchers; Moq strict-mode `IAppTokenProvider`; `FakeHttpHandler` pattern; CsCheck `Gen`/`Sample` per suite convention. |
| **Test file location** | PASS | All six touched test files under `tests/OpenClaw.Core.Tests/CloudGraph/` mirroring `src/OpenClaw.Core/CloudGraph/`. No colocation in the production tree. |
| **Coverage expectation** | PASS | Core.Tests report 94.61% line / 86.08% branch; new file 100.00%/87.50%; both modified logic files 100.00%/100.00%; no regression. |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); CsCheck 4.7.0 is referenced by the test project. Five genuine CsCheck properties: case-invariance, deny-completeness, membership soundness, and self-send dominance over the pure `Authorize` function (`SendOnBehalfAuthorizerPropertyTests`, `iter: 1000` each), plus the whitespace-presence-drives-the-violation property over the validator rule (`GraphAdapterOptionsValidatorTests`). One property test per pure function added is satisfied with margin. |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80 through #117 T1 audits). No Stryker configuration exists in the repository's workflows; this is a pre-existing repo-wide pipeline gap, not a finding against this branch. |
| **Determinism (no sleeps, no wall clock)** | PASS | `FakeTimeProvider` in every client construction; seeded CsCheck with failing-seed printing; zero `Thread.Sleep`/`Task.Delay`/wall-clock reads in the touched test files (reviewer grep: zero matches; executor gate `test-hygiene.2026-07-06T23-21.md` EXIT 0). |
| **No temporary files** | PASS | Zero filesystem access in any touched test (reviewer grep). |
| **No live network** | PASS | Zero `HttpClientHandler` constructions; every client is built over a mocked handler with a `graph.example.test` base address; the deny tests prove zero handler invocations. |
| **Focused / isolated** | PASS | Fresh client/handler/options/logger per test; the `DenyClient` helper deliberately uses a strict no-setup token-provider mock so any token call fails the test. |

---

## 5. Test Coverage Detail

Reviewer-parsed per-file coverage at branch head (fresh cobertura, line AND branch, duplicate class entries deduplicated per file+line):

| File | Status | Line | Branch | Notes |
|------|--------|------|--------|-------|
| SendOnBehalfAuthorizer.cs | NEW | 20/20 = 100.00% | 7/8 = 87.50% | Instrumented lines 75-100 span the entire function body (pure sync function — no instrumentation masking). The single partial arm is line 90 (3/4): the defensive `entry is not null` guard's null-entry case has no test — Minor CR-119-01. |
| GraphAdapterOptionsValidator.cs | MODIFIED | 54/54 = 100.00% | 36/36 = 100.00% | The new whitespace rule is fully covered both ways, at baseline parity (100/100 at baseline too). |
| GraphHostAdapterClient.SendMail.cs | MODIFIED | 56/56 = 100.00% | 4/4 = 100.00% | Instrumented lines 34-105 include the full deny gate, envelope synthesis, warning log, and both `ComposeSendMailBody` arms. `SendMailAsync` is a synchronous task-returning method, so the CompilerGenerated exclusion does not mask any of this file's changed lines. |
| GraphAdapterOptions.cs | MODIFIED | not instrumented | not instrumented | Auto-property options bag; compiler-generated accessors excluded by the pre-existing runsettings filter (disclosed openly in the executor's coverage evidence — same accepted disposition as the #109/#113 audits). Behaviorally covered by the indexed-key binding test, the `ValidateOnStart` test, and every validator/authorizer path reading the property. |

Test suites delivered or extended (6 files, ~29 net new runtime cases): `SendOnBehalfAuthorizerTests` (10 directed tests, all seven decision-table rows plus dominance/padding variants), `SendOnBehalfAuthorizerPropertyTests` (4 CsCheck properties), `GraphHostAdapterClientSendMailTests` (+6 gate contract tests a-f: two zero-I/O denies, message/log discipline, allowlisted send with body assertion, case-differing entry, self-send with empty allowlist), `GraphAdapterOptionsValidatorTests` (+7 cases: whitespace DataRow matrix, empty-list validity, clean-list validity, disabled-mode pass-through, 1 CsCheck property), `GraphServiceCollectionExtensionsTests` (+2: indexed-key binding, `ValidateOnStart` failure), `CloudGraphContractParityTests` (fixture-only allowlist addition; assertions unchanged).

**Coverage:** every changed executable line in the three logic files is covered; the new file exceeds both uniform per-file gates. **Gap:** none Blocking; the single partial branch arm is recorded as Minor CR-119-01 with a concrete test recommendation in the code review.

**Regression:** the executor's regression-surface record (reviewer-confirmed against the diff hunks) shows no existing test method body was modified — the changes to existing test files are confined to using-directives, shared construction helpers (which now seed the allowlist with the configured principal so pre-existing on-behalf tests still reach the send path under the fail-closed gate), new private helpers, and appended tests. All pre-existing send-mail, D5 error-mapping, throttling, parity, and architecture tests pass at head.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 1197 (1192 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 745 passed / 745 | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~2 s | PASS |
| Core.Tests report coverage | 94.61% line, 86.08% branch | PASS |
| OpenClaw.Core package (deduped) | 99.36% line, 92.87% branch | PASS |
| Pooled solution (deduped) | 97.56% line, 90.90% branch | PASS |
| New file (SendOnBehalfAuthorizer.cs, T1) | 100.00% line, 87.50% branch | PASS |
| Net new tests vs merge-base | ~29 runtime cases (23 methods incl. 5 CsCheck properties) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 325 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest) | `dotnet test ... --filter "FullyQualifiedName~CloudGraphArchitectureBoundary"` | 3/3 pass (reviewer isolation run) | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/coverage-review"` | 1192 passed, 0 failed, 5 skipped | PASS |
| Untouched-surface diff | `git diff --stat d67dea0..03e80e2 -- src/OpenClaw.Core/Agent src/OpenClaw.HostAdapter.Contracts src/OpenClaw.MailBridge src/OpenClaw.Core/HostAdapterHttpClient.cs "docker-compose*" ".github/workflows" ".github/actions" "scripts/benchmarks" quality-tiers.yml mailbridge.runsettings` | Empty — contracts, MailBridge, local client, workflows, runsettings, and tier map all unchanged | PASS |
| File-size cap | `wc -l` over all 10 touched `.cs` files | Production max 106; test max 489 — all under 500 | PASS |
| Evidence-location scan | `git diff --name-only d67dea0..03e80e2` piped to a grep for the forbidden artifacts sub-paths | No matches | PASS |
| Banned-API / suppression scan | grep over the four production files for pragma / SuppressMessage / nullable directives / dynamic | Zero matches | PASS |
| Test-hygiene scan | grep over the touched test files for Task.Delay / Thread.Sleep / wall clock / temp files / Random.Shared | Zero matches | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `03e80e2` on 2026-07-06. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change. The `modified-workflow-needs-green-run` policy rule does not fire: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths appear in the diff.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** Two Minor findings and three informational observations, recorded (not policy violations blocking this branch):

- **CR-119-01 (Minor) — untested null-entry defensive arm.** `SendOnBehalfAuthorizer.cs` line 90: the `entry is not null` guard inside the membership loop reports condition coverage 3/4; the null-entry arm (a list containing a null element) has no test anywhere. This is a measured-and-uncovered arm, not an instrumentation exclusion, but it does not pull the file below any gate (87.50% branch >= 75%) and the scenario is unreachable from the configuration binder in practice. Concrete recommendation in the code review (one directed test passing a list containing a null element).
- **CR-119-02 (Minor) — baseline evidence predates the merge-base.** The committed baseline (`evidence/baseline/csharp-test-coverage.2026-07-06T22-45.md`, Core.Tests 616 tests, 93.73%/85.25%) was captured at 22:45, four minutes before the merge-base commit `d67dea0` (22:49, the #121 CloudSync merge); the baseline cobertura contains no CloudSync classes, and the post-change delta (+129 Core.Tests cases) therefore bundles #117's tests with this feature's ~29. Reviewer-verified this is non-gating: `git diff d67dea0^1..d67dea0 -- src/OpenClaw.Core/CloudGraph tests/OpenClaw.Core.Tests/CloudGraph` is empty, so the per-file baselines for every file this feature touches are exact, both modified logic files are at 100.00% line and branch at baseline and head, and every gate verdict in this audit rests on the reviewer's fresh head measurements. Recommendation: capture future baselines at the exact merge-base SHA recorded in the checkpoint.
- **Auto-property instrumentation exclusion (Informational).** `GraphAdapterOptions.cs` consists solely of auto-properties whose accessors are compiler-generated, so the file is absent from cobertura at baseline and head under the pre-existing `mailbridge.runsettings` CompilerGenerated attribute filter (unchanged on this branch; an attribute-level filter, not a production-path `exclude` entry of the kind the coverage-exclusion policy prohibits). The executor disclosed this openly in both coverage evidence records. Behavioral coverage is direct: the indexed-key binding test and `ValidateOnStart` test read and populate the new property, and every validator/authorizer test path consumes it. Same accepted disposition as the #109 and #113 audits. Unlike several prior branches, no async-body masking exists here — `SendMailAsync` remains synchronous and its gate body is fully instrumented.
- **Untrimmed from-injection value (Informational, pre-existing).** `ComposeSendMailBody` injects the raw `options.PrincipalMailboxUpn` as the `from` address; a whitespace-padded configured principal would be sent padded. This is byte-identical to the baseline D7 behavior (reviewer-verified against `git show d67dea0:...SendMail.cs`) and is not introduced by this branch; the authorization comparison itself trims. Optional hardening noted in the code review.
- **Trimmed comparison strengthens the D7 predicate (Informational, intentional).** The baseline self-send comparison was untrimmed `OrdinalIgnoreCase`; the authorizer trims per the spec's explicit decision semantics ("on Trim()ed values"). A whitespace-padded principal equal to the assistant now self-sends (no `from`) instead of injecting a padded `from` — a deterministic-normalization hardening in the fail-safe direction, covered by `Authorize_WhitespacePaddedPrincipal_ReturnsAllowedOnBehalf` and the self-send tests.

### Code/Tenant Boundary Evaluation (caller-requested)

The boundary is correctly drawn. Everything decidable from configuration and code is automated and tested: the allowlist decision, the fail-closed default, the deny-before-I/O contract (proven with a zero-invocation handler and a strict never-called token provider), the envelope shape, and the no-UPN-leak message/log discipline. What is deferred to the runbook is exclusively tenant state that this environment cannot reach without credentials: the Exchange `GrantSendOnBehalfTo` grant, allowlist/grant reconciliation, live Graph acceptance, rendered-appearance checks in Outlook/OWA, and Send As absence. The runbook at `runbooks/send-on-behalf-validation.runbook.md` covers all four spec items, cross-references the F11 RBAC runbook rather than duplicating it, documents the Send As precedence hazard with cited Microsoft Learn sources, and includes the two-controls reconciliation step. The orchestrator checkpoint records the requirement (`HI-119-01`) with `response: "exception"` and the correct non-empty `runbook_path`, satisfying all three `human_interaction` invariants in `.claude/rules/orchestrator-state.md`. The spec and user-story explicitly do not claim automated verification for these items.

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment; the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70 through #117 audits. The format check ran to EXIT 0 over all 325 files.
- **MCP template/validator tools:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing C# artifact set (issue #115 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **PR-context collector:** no repo PR-context collector script exists and the canonical artifacts were absent at review start; the reviewer generated `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` directly from git against the supplied base and merge-base (Appendix B).
- **GitHub CLI:** not used for this review; issue cross-verification is based on the in-repo issue.md/spec.md records. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None removed.** No existing test method body was modified, deleted, or weakened (reviewer-verified from the diff hunks; the executor's `regression-surface.2026-07-06T23-19.md` documents the same). The shared construction helpers in two existing test files gained a seeded allowlist — a mechanically necessary fixture consequence of the intended fail-closed behavior change, with all assertions unchanged. The 5 solution skips are pre-existing environment-gated COM/publish tests, unchanged.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `d67dea0`)

Single commit `03e80e2` ("feat(core): gate send-on-behalf with principal-mailbox allowlist"). Range: `d67dea0117984b980b093f1c942c9a4762b8b25f..03e80e25e9ba75cd463e2b32b46548f14dc416b5` (32 files, +2202/-34).

### Files Modified (categories)

1. **`src/OpenClaw.Core/CloudGraph/`** (1 new + 3 modified, 391 lines total post-change) — the F15 gate: `SendOnBehalfAuthorizer.cs` (new pure decision function + enum), `GraphAdapterOptions.cs` (additive allowlist property, fail-closed empty default), `GraphAdapterOptionsValidator.cs` (shape-only whitespace rule), `GraphHostAdapterClient.SendMail.cs` (deny-before-I/O gate; `ComposeSendMailBody` consumes the shared decision).
2. **`tests/OpenClaw.Core.Tests/CloudGraph/`** (2 new + 4 extended, ~29 net new runtime cases) — decision-table units, 5 CsCheck properties, deny-contract seam tests (zero-I/O proof), allow-path body assertions, validator matrix, binding/`ValidateOnStart` tests, fixture-only parity update.
3. **`docs/features/active/2026-07-06-send-on-behalf-allowlist-119/`** (20 files) — issue/spec/user-story/plan/research, the tenant-validation runbook, and canonical evidence (baseline, qa-gates, regression-testing incl. fail-before/pass-after, other incl. the human-interaction record and diff-scope verification).
4. **`.claude/agent-memory/task-researcher/`** (2 files) — project-state memory refresh (metadata, not code).

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The C# change passes formatting, linting, nullable type-checking, the NetArchTest CloudGraph boundary suite, the full unit-test suite, and the uniform coverage gates, all independently re-run by the reviewer at branch head. The T1 property-test obligation is satisfied directly: five genuine CsCheck properties over the two pure functions this branch adds. The fail-closed contract is proven at the seam (zero handler invocations, strict never-called token provider) with fail-before/pass-after evidence, the from-injection predicate and the authorization decision share one verified source, the additive options default is fail-closed, and the code/tenant boundary is correctly drawn with a complete runbook and a structurally valid checkpoint exception. No existing test was weakened, no dependencies were added, no public contract changed, and no evidence-location or file-size violations exist. The `modified-workflow-needs-green-run` rule does not fire (no workflow, benchmark, or action paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing. The baseline staleness (CR-119-02) is recorded and reviewer-neutralized by fresh head measurements.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (single decision source; pure function; zero new dependencies)
- Module & File Structure: PASS (all files under 500 lines, production max 106)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-closed startup validation; runtime deny envelope per the established error surface)
- Dependency Policy: PASS (zero new dependencies)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (94.61%/86.08% report-level; new file 100.00%/87.50%; changed lines fully covered)
- Test Structure: PASS
- External Dependencies: PASS (mocked handlers, strict token-provider mocks, no temp files, no live Graph calls)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq + CsCheck repo convention; tests mirror)
- Determinism: PASS (FakeTimeProvider; seeded CsCheck)
- T1 obligations: PASS (five genuine properties; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 1192/1192 runnable solution tests passing (5 pre-existing environment-gated skips)
- Core.Tests report: 94.61% line / 86.08% branch (gates: 85%/75%); OpenClaw.Core package deduped 99.36%/92.87%; pooled solution deduped 97.56%/90.90%
- New file SendOnBehalfAuthorizer.cs: 100.00% line, 87.50% branch; both modified logic files 100.00%/100.00%
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- All 10 touched `.cs` files under the 500-line cap (production max 106)

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, regression evidence, and policy requirements pass against branch head `03e80e2`. No remediation inputs are required. Operational notes (from spec, not gates): deployments with `{p} != {a}` and no allowlist change behavior from silent representation to denial — the intended fail-closed hardening — and this must be called out in the PR description per spec Versioning; the tenant-side grant and rendered-appearance validation remain a human runbook item (`HI-119-01`) before rollout.

---

## Appendix A: Test Inventory

C# test changes in this feature (all in `tests/OpenClaw.Core.Tests/CloudGraph/`):

1. `SendOnBehalfAuthorizerTests.cs` (NEW, 132 lines, 10 tests) — all seven spec decision-table rows: self-send equal, self-send case-differing, self-send dominance with the principal allowlisted, exact member, case-differing member, whitespace-padded member, whitespace-padded principal, empty allowlist deny, non-member deny, duplicate entries.
2. `SendOnBehalfAuthorizerPropertyTests.cs` (NEW, 213 lines, 4 CsCheck properties at `iter: 1000`) — case-invariance (random recasing of principal and every entry never changes the decision), deny-completeness (any allowlist with the principal filtered out always denies when `{p} != {a}`), membership soundness (inserting the principal in any casing/padding at any position always allows), self-send dominance (recased-equal assistant always yields `AllowedSelf` for every generated allowlist).
3. `GraphHostAdapterClientSendMailTests.cs` (EXTENDED, +6 tests a-f) — decisive deny with non-empty allowlist (envelope fields, `Meta.RequestId`, `AdapterVersion`, zero handler invocations, strict token provider `Times.Never`); empty-allowlist deny (same proofs); message-names-key with no UPN plus exactly-one-warning log discipline via `RecordingLogger`; allowlisted send with full body-shape assertion (`from = {p}` at `users/{a}/sendMail`); case-differing entry permits; self-send with empty allowlist omits `from`. New helpers: `DenyClient` (strict no-setup token provider), `NeverInvokedHandler`, `RecordingLogger`. Existing test method bodies unmodified.
4. `GraphAdapterOptionsValidatorTests.cs` (EXTENDED, +7 cases) — whitespace-entry DataRow matrix (empty/space/tab, exactly one violation naming the key, echoing no value), empty-list validity, non-whitespace-list validity, disabled-mode pass-through, and 1 CsCheck property (inserting a whitespace entry at any position always yields the violation; clean lists never do).
5. `GraphServiceCollectionExtensionsTests.cs` (EXTENDED, +2 tests) — indexed keys `AllowedPrincipalMailboxUpns:0`/`:1` bind in order; a whitespace entry fails the `ValidateOnStart` path with `OptionsValidationException`.
6. `CloudGraphContractParityTests.cs` (FIXTURE-ONLY) — the shared `Service` helper seeds the principal into the allowlist so the on-behalf parity flow still reaches the Graph 400 -> INVALID_REQUEST path under the fail-closed gate; assertions unchanged.

Reviewer run: `OpenClaw.Core.Tests` 745 passed, 0 failed; solution total 1192 passed, 0 failed, 5 env-gated skipped. Fail-before/pass-after: `evidence/regression-testing/send-on-behalf-gate-fail-before.2026-07-06T23-13.md` (EXIT 1 with exactly the three deny contract tests failing pre-gate; EXIT 0 post-gate).

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# PR-context artifacts (regenerated by the reviewer; collector script absent)
git log --oneline d67dea0117984b980b093f1c942c9a4762b8b25f..03e80e25e9ba75cd463e2b32b46548f14dc416b5
git diff --name-status d67dea0117984b980b093f1c942c9a4762b8b25f..03e80e25e9ba75cd463e2b32b46548f14dc416b5   # summary
git diff d67dea0117984b980b093f1c942c9a4762b8b25f..03e80e25e9ba75cd463e2b32b46548f14dc416b5                  # appendix

# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Architecture-boundary subset (reviewer isolation run)
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --no-build --filter "FullyQualifiedName~CloudGraphArchitectureBoundary"

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --no-build --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/coverage-review"

# Untouched-surface verification (empty diffs expected)
git diff --stat d67dea0..03e80e2 -- src/OpenClaw.Core/Agent src/OpenClaw.HostAdapter.Contracts src/OpenClaw.MailBridge src/OpenClaw.Core/HostAdapterHttpClient.cs "docker-compose*" ".github/workflows" ".github/actions" "scripts/benchmarks" quality-tiers.yml mailbridge.runsettings

# Baseline-staleness verification (empty diff proves the stale baseline is non-gating for touched files)
git diff --stat d67dea0^1..d67dea0 -- src/OpenClaw.Core/CloudGraph tests/OpenClaw.Core.Tests/CloudGraph

# File-size cap
wc -l src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphContractParityTests.cs tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerPropertyTests.cs tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerTests.cs

# Evidence-location scan
git diff --name-only d67dea0..03e80e2 | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'

# Banned-API / suppression / hygiene scans of the touched files
grep -rnE '#pragma|SuppressMessage|#nullable|\bdynamic\b' src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs
grep -rnE 'Thread\.Sleep|Task\.Delay\(|DateTime\.UtcNow|DateTime\.Now|Random\.Shared|GetTempPath|GetTempFileName' tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerTests.cs tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerPropertyTests.cs tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-06
**Policy Version:** Current (as of audit date)
