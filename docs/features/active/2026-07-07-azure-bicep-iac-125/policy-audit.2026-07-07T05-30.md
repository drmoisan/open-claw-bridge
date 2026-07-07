# Policy Compliance Audit: azure-bicep-iac (#125)

**Audit Date:** 2026-07-07
**Code Under Test:** Infrastructure authoring only. 5 NEW Bicep files (`deploy/azure/main.bicep`, `deploy/azure/modules/{containerApp,keyVault,queue}.bicep`, `deploy/azure/parameters/main.dev.bicepparam`), 1 NEW Markdown doc (`deploy/azure/README.md`), 1 NEW reusable GitHub Actions workflow (`.github/workflows/_bicep-validate.yml`), 1 edit to `.github/workflows/ci.yml` (one added job), 1 `.gitignore` re-include line, 1 NEW PowerShell production script (`scripts/Test-OpenClawBicepParameterSecrets.ps1`) with 1 NEW mirrored Pester test file, plus the feature-folder documentation set (issue/spec/user-story/plan/research/runbook/evidence) and pre-existing agent-memory Markdown records from other sessions in this worktree. No C#, Python, or TypeScript production or test file changed in the branch diff.

**Scope:** Full feature branch `feature/azure-bicep-iac-125` @ `b3a252b` versus resolved base `epic/openclaw-vision-integration` @ merge-base `7a29286b687f00c6a10809efa41102c78f009c36` (confirmed via `git merge-base HEAD origin/epic/openclaw-vision-integration`). Scope is feature-vs-base over the complete branch diff, not any plan/task/phase subset. Diff: 42 files changed, +1697/-1 (`git diff --stat 7a29286..HEAD`). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` (Seeded Test Conditions) and `user-story.md` (Acceptance Criteria), per `acceptance-criteria-tracking`.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| PowerShell | 1 production + 1 test (both NEW) | 365 (repo Pester suite; baseline 358, +7 new feature tests) | 365 pass, 0 fail, 0 skip (reviewer re-run of the new test file: 7/7 pass, matching executor) | 89.66% command/line (1,760/1,963 instructions, 29 files) | 89.94% command/line (1,814/2,017 instructions, 30 files) | `Test-OpenClawBicepParameterSecrets.ps1`: 100% (54/54 instructions; 38/38 lines) |
| YAML (GitHub Actions) | 1 new reusable workflow (`_bicep-validate.yml`) + 1 edit (`ci.yml`, one added job) | N/A (no unit-test framework for workflow YAML) | Structural review PASS (executor P4-T3, reviewer-verified); real execution requires a runner | N/A | N/A | Structurally reviewed clean; **no green workflow run exists against branch head `b3a252b`** (see Section 8 / Rejected Scope Narrowing / Blocking finding below) |
| Bicep / Markdown | 5 new `.bicep` files + 1 new `.bicepparam` + 1 new `README.md` | N/A (declarative IaC; no unit-test framework) | Structural review PASS across Phases 1-3 and the final consolidated Phase 6 review (executor evidence, reviewer-verified); `bicep`/`az` CLI unavailable locally (confirmed independently: `Get-Command bicep`/`Get-Command az` both return nothing) | N/A | N/A | N/A — coverage percentage gates do not apply to declarative Bicep/Markdown files (no executable/test-file split exists for them) |
| C# / Python / TypeScript | 0 files changed | N/A | N/A | N/A — no changed files | N/A — no changed files | N/A — no changed files in these languages on the branch |

**Note:** C#, Python, and TypeScript coverage rows are `N/A` because the branch diff contains zero changed production or test files in those languages (verified by `git diff --name-status 7a29286..HEAD`, no `.cs`/`.py`/`.ts`/`.tsx` path present). PowerShell is the only language with an executable-code coverage gate on this branch; that gate is an explicit **PASS**. The workflow-YAML row records a distinct, mandatory rule (`modified-workflow-needs-green-run`), which is **not satisfied** as of this audit — recorded as a Blocking finding below, not folded into the PowerShell coverage verdict.

### Coverage Evidence Checklist

- PowerShell baseline coverage artifact: `docs/features/active/2026-07-07-azure-bicep-iac-125/evidence/baseline/poshqc-test.2026-07-07T01-30.md` (repo-wide 89.66% command/line, 29 pre-existing files, 358/358 tests, F11-precedent corrected-runsettings workaround)
- PowerShell post-change coverage artifact: `docs/features/active/2026-07-07-azure-bicep-iac-125/evidence/qa-gates/final-poshqc-test.2026-07-07T02-50.md` and `evidence/qa-gates/coverage-comparison.2026-07-07T02-55.md` (89.94% command/line, 30 files, 365/365 tests)
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - no changed files in this language on the branch`
- TypeScript post-change coverage artifact: `N/A - no changed files in this language on the branch`
- Python / C# / TypeScript coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language with an executable-coverage gate (PowerShell), independently re-verified by the reviewer (independent `Invoke-Pester` re-run of the new test file: 7/7 pass; independent `Invoke-ScriptAnalyzer`/`Invoke-Formatter` idempotency checks: clean). The PowerShell coverage gate is met (repo-wide 89.94% >= 85%; new-code 100% >= 85% line / >= 75% branch-command-proxy; no regression — the pre-existing 29 files' covered-instruction count is unchanged at 1,760 both before and after). The workflow-YAML and Bicep/Markdown rows have no coverage-percentage gate; their applicable gates (structural validation, green-run evidence) are evaluated on their own terms above and in Section 8.

---

## Executive Summary

This feature branch closes issue #125 (gap item F16, Epic C): declarative Bicep infrastructure-as-code under `deploy/azure/` provisioning the Stage 1 Azure footprint (Container Apps environment + Container App, RBAC-scoped Key Vault, Service Bus namespace + queue) for the already-containerized `OpenClaw.Core` host, plus a reusable CI workflow (`_bicep-validate.yml`) wired into `ci.yml`, a PowerShell parameter-file secret-scan script with a mirrored Pester test, and a human-exception runbook for the out-of-scope live deployment step. The delivery is infrastructure-authoring only; the reviewer independently confirmed no file under `OpenClaw.Core/` or `OpenClaw.Core.CloudSync/` (or any other C#/PowerShell production file outside the additive set) appears in the diff, other than the single documented `ci.yml` wiring edit.

The toolchain was independently re-verified by the reviewer against branch head `b3a252b`:
- **Formatting (PowerShell):** `Invoke-Formatter -ScriptDefinition` idempotency check over both new PowerShell files — zero diffs (executor's authoritative `run_poshqc_format` MCP runs also EXIT 0).
- **Linting (PowerShell):** `Invoke-ScriptAnalyzer` (1.24.0) over both new files — zero diagnostics (executor's `run_poshqc_analyze` MCP runs, bundled settings, also EXIT 0).
- **Type checking:** not applicable for PowerShell per `.claude/rules/powershell.md`; not applicable for Bicep/YAML (no type-checker exists for these file types in this repository).
- **Tests (PowerShell):** reviewer ran `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1` directly — 7 passed, 0 failed, matching the executor's evidence exactly.
- **Coverage (PowerShell):** reviewer did not re-run the full-repo bundled-pipeline workaround (already independently re-verified by an equivalent method on prior reviews of the same workaround, e.g. #111, #113, #115, #117); the executor's numeric figures are internally consistent (pre-existing 29-file covered-instruction count unchanged at 1,760 both before and after, isolating the entire post-change delta to the new 100%-covered file) and are accepted as verified evidence.
- **Structural validation (Bicep/YAML):** neither the `bicep` CLI nor the `az` CLI is installed in this review environment (reviewer independently confirmed: `Get-Command bicep -ErrorAction SilentlyContinue` / `Get-Command az -ErrorAction SilentlyContinue` both return nothing, matching the executor's `evidence/baseline/cli-tooling-availability.2026-07-07T01-32.md`). The executor's documented structural-review fallback (brace-balance checks, resource/parameter/output declaration verification, secret-shaped-literal greps, cross-checks against the research artifact's Requirements Mapping table) was reviewed and found sound across all five phases of structural review evidence.

**One Blocking finding**, driven by the `modified-workflow-needs-green-run` policy rule (see Section 8): the branch diff modifies `.github/workflows/ci.yml` and adds `.github/workflows/_bicep-validate.yml`, both matching `.github/workflows/**`, and **no workflow run with a head SHA matching `b3a252b` exists** (`gh run list` returns zero rows for `feature/azure-bicep-iac-125` or head `b3a252b` as of this audit). This is a second, independent line of defense ahead of any later CI green gate and fires regardless of how clean the structural review is. No other Blocking findings. One Minor finding and several Info observations (Section 8 and the code review). Remediation is required for the Blocking finding only; the underlying feature delivery (Bicep templates, secret-scan script, runbook) is otherwise sound.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/powershell.md`
- `.claude/rules/ci-workflows.md` (evaluated — see Section 8; the new workflow's `run:` blocks contain no deliberately-failing nested command, so the exit-code-reset requirement does not apply)
- `.claude/rules/benchmark-baselines.md` (evaluated — not triggered; no baseline file or `scripts/benchmarks/**` path in the diff)
- `.claude/rules/orchestrator-state.md` (evaluated — the branch does not modify `orchestrator-state.json`; the feature's `human_interaction` exception is documented in-repo, per `.claude/rules/orchestrator-state.md`'s invariants, as a matter for the orchestrator to record, not this branch's files)
- `.claude/rules/tonality.md`
- `.claude/skills/human-exception-runbook/SKILL.md` (runbook contract)
- `.claude/skills/feature-review-workflow/SKILL.md` (`modified-workflow-needs-green-run` rule — **fires, Blocking**)

**Language-specific policies evaluated:**
- PowerShell: `.claude/rules/powershell.md`
- N/A C# / Python / TypeScript (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts are tracked by this feature. The executor's scratchpad runsettings files (corrected-`CodeCoverage.Path` copies used for the F11-precedent workaround) live in the session scratchpad and are not in the diff. The raw Pester outputs under `artifacts/pester/` are untracked tool outputs at the path the feature-review skill itself designates for PowerShell coverage.

---

## Rejected Scope Narrowing

None encountered from the caller prompt in this session. The caller supplied the resolved base branch, merge-base SHA, and scope facts framed explicitly as "verify independently against the diff; do not take these as a narrowing of scope" — this framing was honored: every scope fact supplied (languages touched, coverage figures, evidence paths, CLI unavailability, green-run requirement) was independently re-verified against the git diff, the working tree, and `gh run list`, rather than accepted at face value. No instruction attempted to mark PowerShell, YAML, or Bicep coverage as "plan scope only," "informational only," or "not applicable," and none is so marked here.

Observations (not narrowing instructions, recorded for completeness): (1) `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` were **absent** at review start (not merely stale), despite the caller's framing that they were "just regenerated" — consistent with the recurring quirk on #119/#120 that a caller's freshness claim should not be trusted without an `ls` check. No repo-local PR-context collector script exists in `scripts/`; both files were regenerated directly from git (`git log`/`--name-status`/`--stat` for the summary; full `git diff` for the appendix) per the accepted #120/#119 fallback. (2) `gh` is installed and authenticated in this environment (unlike several prior PowerShell-only reviews where it was unavailable), which allowed a direct, authoritative check of workflow-run history for the branch head — this strengthens, rather than weakens, the Section 8 Blocking finding, since the absence of a qualifying run is now a directly-observed fact rather than an inference.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 7a29286..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
- Result: **NONE.** No matching path is present in the diff. All feature evidence is written to the canonical `docs/features/active/2026-07-07-azure-bicep-iac-125/evidence/<kind>/` locations (`baseline/`, `qa-gates/`, `other/`, confirmed via `find evidence -type f`); the runbook lives at the canonical non-evidence path `<FEATURE>/runbooks/azure-bicep-deployment.runbook.md`.
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with prior reviews, e.g. #111, #119, #120); the scan was performed by direct diff inspection, matching the accepted fallback recorded in agent memory. The executor's own `plan-reconciliation.2026-07-07T03-15.md` records the same forbidden-path probe with zero hits.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | `Test-OpenClawBicepParameterSecrets.Tests.ps1` dot-sources the script fresh in `BeforeAll`; every filesystem cmdlet (`Test-Path`/`Get-ChildItem`/`Get-Content`) is mocked per-`It` with no shared mutable state between tests. Reviewer's isolated re-run of the file (7/7 pass) confirms no ordering dependency. |
| **Isolation** — Each test targets single behavior | PASS | One behavior per `It`: clean file, secret-shaped file, missing directory, empty directory, default `-Path`, main-entry-point exit-0, main-entry-point exit-1 — seven distinct scenarios, no test asserts more than one behavior. |
| **Fast Execution** | PASS | Reviewer's isolated run completed in 813ms for 7 tests; no I/O, no real files. |
| **Determinism** | PASS | No clock, no randomness, no sleeps, no network, no real filesystem access — every filesystem cmdlet is mocked; the main-entry-point tests invoke the script via the call operator and read `$LASTEXITCODE`, a documented, empirically-verified-safe pattern (matching this repo's existing `tests/scripts/Uninstall.Tests.ps1` precedent). |
| **Readability & Maintainability** | PASS | Descriptive `It` names stating scenario and expectation; explicit `# Arrange` / `# Act` / `# Assert` comments in every test; the file's header comment-block documents the five scenario categories covered. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline repo-wide 89.66% command/line coverage (1,760/1,963 instructions, 29 pre-existing production files), 358/358 tests. Source: `evidence/baseline/poshqc-test.2026-07-07T01-30.md`. |
| **No Coverage Regression** | PASS | Post-change 89.94% (1,814/2,017), a net improvement (+0.28 pp). The pre-existing 29 files' covered-instruction count is unchanged at 1,760 both before and after (1,814 minus the new file's 54 covered instructions = 1,760), confirming zero regression on any pre-existing file — the aggregate rose solely because the new file is 100% covered. |
| **New Code Coverage** | PASS | `scripts/Test-OpenClawBicepParameterSecrets.ps1`: 100% (54/54 instructions; 38/38 lines) — exceeds both the 85% line and 75% branch (command-proxy) thresholds with full margin. Reached after two tests were added specifically to exercise the script's main-entry-point `exit 0`/`exit 1` branches (documented in `evidence/qa-gates/bicep-secret-scan-poshqc-test.2026-07-07T02-35.md`); without them the file was 81.58% line / 79.63% command — below the line threshold. |
| **Comprehensive Coverage** | PASS | Clean parameter file (positive), secret-shaped literal in a parameter file (negative, names the offending file), missing target directory (edge case, no throw), empty existing directory (edge case, no throw), default `-Path` value (contract), main-entry-point exit-0 clean path and exit-1 dirty path (both branches of the script's only conditional exit logic). |
| **Positive Flows** | PASS | Clean-file scan reports `IsClean = $true`; default-path invocation reports the correct `ScannedPath`. |
| **Negative Flows** | PASS | Secret-shaped literal (a synthetic `AccountKey=`/`SharedAccessKey=` connection-string pattern) is detected and the offending file path is reported in the result and in the process's non-terminating error record. |
| **Edge Cases** | PASS | Missing directory and empty-but-existing directory both return a clean result without throwing, per the script's documented "missing/empty directory is clean, not an error" contract (relevant because a fresh clone will not yet have prod parameter files beyond `main.dev.bicepparam`). |
| **Error Handling** | PASS | The main-entry-point exit-1 path is asserted to write a non-terminating `Write-Error` record matching `'Secret-shaped literal found'` and to set `$LASTEXITCODE` to 1; the exit-0 path is asserted to print the clean message and set `$LASTEXITCODE` to 0. |
| **Concurrency** | N/A | The script is a single-pass synchronous file scan with no concurrent or async code path. |
| **State Transitions** | N/A | The script is stateless; each invocation is an independent scan. |

### 1.2.1 Per-Language Coverage Comparison

- PowerShell: Baseline: 89.66% -> Post-change: 89.94%, Change: +0.28%, New/changed-code coverage: 100% (54/54 instructions, 38/38 lines) for `Test-OpenClawBicepParameterSecrets.ps1` (command-coverage proxy for branch per the #58/#62/#111 precedent, since Pester v5 emits no separate branch-percentage metric for PowerShell), Disposition: PASS (repo-wide >= 85%, new-code >= 85% line and >= 75% branch-proxy, no regression — the pre-existing 29 files' covered-instruction count is unchanged at 1,760 before and after), Evidence: `evidence/baseline/poshqc-test.2026-07-07T01-30.md`, `evidence/qa-gates/final-poshqc-test.2026-07-07T02-50.md`, `evidence/qa-gates/coverage-comparison.2026-07-07T02-55.md`, reviewer independent re-run of the new test file (7/7 pass) and independent `Invoke-ScriptAnalyzer`/`Invoke-Formatter` checks (clean).

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | Assertions use `Should -Match` against specific expected substrings (e.g. `'Clean: scanned 0 parameter file'`, `'Secret-shaped literal found'`) rather than bare truthy checks; the offending-file assertion checks the exact `FilePath` value. |
| **Arrange-Act-Assert Pattern** | PASS | Explicit `# Arrange` / `# Act` / `# Assert` comments in every `It` block. |
| **Document Intent** | PASS | The file carries a `.SYNOPSIS`/`.DESCRIPTION` comment-based-help block stating exactly which scenarios are covered; the main-entry-point `Context` block carries an explanatory comment on why direct invocation (rather than a separate runspace/process) is used and why it is safe. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No network, no live executables, no real filesystem access anywhere in the test file — every filesystem cmdlet (`Test-Path`, `Get-ChildItem`, `Get-Content`) is Pester-mocked. |
| **Use Mocks/Stubs** | PASS | Filesystem cmdlets are the correct mock boundary here (the script's only I/O surface); no production logic is mocked — the `Test-OpenClawBicepParameterSecrets` function body and the script's main-entry-point block both execute for real under test. |
| **Environment Stability** | PASS | No temporary files anywhere (reviewer scan of the test file: zero temp-file API usage — `Set-Content`/`New-Item`/`[System.IO.Path]::GetTempFileName` do not appear); no reliance on machine PATH, profile, or working-directory state — the script's default `-Path` value is a literal relative string and every test either supplies `-Path` explicitly or mocks against the literal default. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items beyond the Section 8 Blocking finding. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #125, `spec.md` v0.1 (Overview, Behavior, Inputs/Outputs, API/CLI Surface, Data & State, Constraints & Risks, Implementation Strategy sections all present and internally consistent), `user-story.md` (two personas, one scenario, 6 acceptance criteria identical to the spec's Seeded Test Conditions plus 3 more), research artifact `research/2026-07-07-bicep-iac-architecture.md` (5 numbered decisions, Requirements Mapping §8, Testing Implications §9). |
| **Read existing change plans** | PASS | `evidence/other/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-07T01-02.md` present with a 7-phase, 34-task structure honoring the PowerShell change budget (1 production + 1 test file, well within the 2-file direct-mode cap). |
| **Document the plan** | PASS | Plan structures the work into Phase 0 (baseline) through Phase 6 (final QA/closure), with a per-phase structural-review gate for the CLI-unavailable fallback and a final `plan-reconciliation.2026-07-07T03-15.md` cross-checking every named evidence artifact against what exists on disk. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | `main.bicep` is a thin orchestrator over three single-resource-concern modules; the secret-scan script is a single function plus a four-pattern regex table plus a thin main-entry-point wrapper — no unnecessary abstraction, runner framework, or configuration machinery. |
| **Reusability** | PASS | The three Bicep modules (`containerApp`, `keyVault`, `queue`) are independently composable and each declares its own parameters/outputs rather than sharing hidden global state; the secret-pattern table in the PowerShell script is a single shared array consumed by one scan loop rather than duplicated per-pattern logic. |
| **Extensibility** | PASS | All `main.bicep` parameters carry sensible defaults except the required `containerImage`; the `main.<env>.bicepparam` naming convention (recorded in `spec.md`) accommodates a future `main.prod.bicepparam` without restructuring `main.bicep` or module contracts; the secret-scan script's `-Path` parameter defaults but is overridable. |
| **Separation of concerns** | PASS | Pure declarative resource definitions live in the Bicep modules; the PowerShell script separates pure scan logic (`Test-OpenClawBicepParameterSecrets` function, returns a result object) from the thin I/O-adjacent main-entry-point block (`Write-Output`/`Write-Error`/`exit`), which only runs when the script is invoked directly, not when dot-sourced for testing. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | `deploy/azure/main.bicep` + `deploy/azure/modules/{containerApp,keyVault,queue}.bicep` + `deploy/azure/parameters/main.dev.bicepparam` — one Bicep file per resource-provisioning concern, matching the plan's stated layout exactly; the PowerShell script/test pair mirrors `scripts/` -> `tests/scripts/` per the repo's test-location convention. |
| **Under 500 lines** | PASS | Reviewer `wc -l` on all new code files: `Test-OpenClawBicepParameterSecrets.ps1` 131, its test file 183, `main.bicep` 68, `containerApp.bicep` 63, `keyVault.bicep` 39, `queue.bicep` 40 — all well under the cap. |
| **Public vs internal** | PASS | The PowerShell script exports one public function (`Test-OpenClawBicepParameterSecrets`) plus a main-entry-point block gated on invocation mode; the Bicep modules declare only the parameters and outputs consumed by `main.bicep` or a future deployment operator (per `spec.md`'s Inputs/Outputs contract). |
| **No circular dependencies** | PASS | `main.bicep`'s three `module` references are one-directional (orchestrator -> leaf modules); no module references another module or `main.bicep` back. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | Approved PowerShell verb (`Test-`); Bicep parameter/output names match `spec.md`'s Inputs/Outputs section exactly (`environmentName`, `location`, `resourceNamePrefix`, `containerImage`; `containerAppFqdn`, `containerAppPrincipalId`, `keyVaultUri`, `serviceBusNamespaceEndpoint`, `serviceBusQueueName`). The plural `Test-OpenClawBicepParameterSecrets` carries a scoped, justified `SuppressMessageAttribute` for `PSUseSingularNouns` (function name mandated by the plan/spec and matches the script file name already referenced by `_bicep-validate.yml`). |
| **Docs/docstrings** | PASS | Comment-based help (`.SYNOPSIS`/`.DESCRIPTION`/`.PARAMETER`/`.EXAMPLE`) on both the script and the function; every Bicep file opens with a header comment stating its purpose and citing the issue/research artifact; every parameter and output carries a `@description(...)` decorator. |
| **Comment why, not what** | PASS | `keyVault.bicep`'s header explains *why* no secret value appears anywhere in the file; `queue.bicep`'s header explains why no connection-string output exists; the parameter file's comment explains why `containerImage` has no dev-time default. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Executor: `run_poshqc_format` MCP EXIT 0 at baseline, batch, and full-repo final scope. Reviewer: `Invoke-Formatter` idempotency over both new PowerShell files — zero diffs. Bicep/YAML/Markdown have no formatter in this environment; the structural-review fallback substitutes (documented, not fabricated). |
| **2. Linting** | PASS | Executor: `run_poshqc_analyze` MCP EXIT 0 at all scopes. Reviewer: `Invoke-ScriptAnalyzer` 1.24.0 over both new files — zero diagnostics. The new workflow YAML will be linted by the pre-existing `actionlint`/`Workflow Lint` job once `ci.yml` runs on a runner — not claimed as a local result. |
| **3. Type checking** | N/A | Not applicable for PowerShell; no type-checker exists for Bicep/YAML in this repository's toolchain. |
| **4. Architecture** | N/A | No architecture-boundary tooling applies to declarative IaC or a standalone utility script; the C# NetArchTest suite is unaffected (no C# changes). |
| **5. Testing** | PASS | Reviewer: isolated `Invoke-Pester` run of the new test file — 7 passed / 0 failed, matching `bicep-secret-scan-poshqc-test.2026-07-07T02-35.md` and `final-poshqc-test.2026-07-07T02-50.md` exactly. |
| **6. Contract/schema checks** | N/A | No host-service schema or wire contract changed; the Bicep parameter/output contract is the feature's own declared surface, verified against `spec.md`'s API/CLI Surface section by the executor's structural reviews and independently by this audit's file reads. |
| **7. Integration tests** | N/A | Deliberately none: live-tenant deployment is the recorded human-interaction exception; the runbook carries the human verification procedure. No CI job in this feature calls Azure with live credentials. |
| **Full toolchain loop** | PASS (PowerShell); documented fallback (Bicep/YAML) | Executor evidence shows format -> analyze -> test re-run with a restart after the coverage-driven test-file edit (P5-T3 restart, documented in `bicep-secret-scan-poshqc-test.2026-07-07T02-35.md`), then a final full-scope clean pass. Bicep/YAML use the CLI-unavailable structural-review fallback consistently, with real execution deferred to the CI runner and never claimed as a local pass. |
| **Explicit reporting** | PASS | Commands and results documented in every evidence artifact (`Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` on all 18 files) and in Appendix B below. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md`'s Implementation Strategy section lists exactly the files delivered; the commit message (`feat(deploy): add Azure Bicep IaC for OpenClaw.Core Stage 1 footprint`) describes the feature accurately. |
| **Design choices explained** | PASS | The research artifact records five numbered decisions (hosting primitive, Key Vault RBAC model, Service Bus SKU, parameterization pattern, secret-scan approach) with rationale; `spec.md`'s Constraints & Risks section explains the Container-Apps-over-Functions choice. |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `issue.md`, `spec.md`, and `user-story.md` (see the Feature Audit for per-criterion verification); the runbook is authored and cross-referenced from `deploy/azure/README.md`. |
| **Provide next steps** | PASS | The runbook's "Known follow-up, not a defect" note documents the deferred Key-Vault-RBAC-role-assignment Bicep wiring as a future feature's work; `user-story.md`'s Non-Goals section records the deferred `INotificationQueue` wiring and Entra app-registration scope. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3-PowerShell applies as a full rule set; the YAML and Bicep changes are evaluated against the applicable cross-cutting rules (`ci-workflows.md`, `general-code-change.md`) since no dedicated `.claude/rules/bicep.md` or `.claude/rules/yaml.md` exists in this repository. C#, Python, and TypeScript sections are omitted: no changed files in those categories on the branch.

### Section 3-PowerShell: PowerShell Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Toolchain via MCP (format -> analyze -> test)** | PASS with documented accommodation | Format and analyze ran via the MCP commands (EXIT 0 at every gate). The test stage's MCP tool `run_poshqc_test` fails at coverage RunStart in this repository due to the bundled settings' foreign `CodeCoverage.Path` allowlist (pre-existing, out-of-scope workspace defect, first documented at F11/#111 and reproduced identically here — same root cause, same fallback). The executor ran the identical bundled `Invoke-PoshQCTest` pipeline directly with repo-scoped coverage settings. Reviewer independently re-ran the new test file (7/7) and confirmed formatter/analyzer cleanliness by an independent method. |
| **PowerShell 7+ compatibility** | PASS | `#Requires -Version 7` present in both new script files; no Windows-PowerShell-only constructs; analyzer clean under PSScriptAnalyzer 1.24.0. |
| **Advanced functions, CmdletBinding, validation** | PASS | `Test-OpenClawBicepParameterSecrets` uses `[CmdletBinding()]` and `[OutputType([pscustomobject])]`; the outer script uses `[CmdletBinding()]` with a named `-Path` parameter (no positional-only design). |
| **ShouldProcess for state changes** | N/A | The script is read-only (scans and reports; makes no state change), so `SupportsShouldProcess` does not apply — correctly omitted. |
| **No global state / no Invoke-Expression / no secrets / no hardcoded paths** | PASS | Reviewer grep: no `Invoke-Expression` anywhere; the script's only "secret-shaped" content is the detection pattern table itself (regex literals, not real secrets); the default `-Path` value (`'deploy/azure/parameters'`) is a documented, overridable relative-path convention, not a hardcoded absolute path; no global mutable state outside the module-scoped, read-only `$script:OpenClawBicepSecretPatterns` constant table. |
| **Fail fast, no silent catch-alls** | PASS | `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'` at the top of the script; the one intentional `-ErrorAction SilentlyContinue` (on `Get-ChildItem`/`Get-Content`) is the documented "missing/empty directory or unreadable file is clean, not an error" contract, not error suppression of a real failure mode; the main-entry-point's `Write-Error ... -ErrorAction Continue` loop is intentional (report every finding, not just the first) before the final `exit 1`. |
| **Approved verbs / naming** | PASS | `Test-` is an approved verb; the deliberate plural noun carries a scoped, justified suppression (mandated function name matching the file name already referenced by the new workflow). |
| **Under 500 lines / cohesive** | PASS | 131 lines (script), 183 lines (test). |
| **Change budget (direct-mode cap: 2 production + tests)** | PASS | Exactly 1 new production PowerShell file + 1 mirrored test file — within the 2-production-file direct-mode cap; no routing to `powershell-orchestrator` required. |
| **Seam pattern** | N/A | The script has no external-executable dependency to wrap (`Get-ChildItem`/`Get-Content`/`Test-Path` are standard cmdlets, directly Pester-mockable without a wrapper seam); no `git`/`gh`/external-tool invocation exists in this script. |

---

## 4. Language-Specific Unit Test Policy Compliance

Only PowerShell tests changed. C#, Python, and TypeScript sections are omitted.

### Section 4-PowerShell: PowerShell Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework — Pester v5** | PASS | Pester 5.6.1; `Describe`/`Context`/`It` structure with `BeforeAll` for the dot-source import. |
| **Test file location & naming** | PASS | `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1` mirrors `scripts/Test-OpenClawBicepParameterSecrets.ps1`, matching this repo's flat `tests/scripts/` convention (e.g. the existing `tests/scripts/Uninstall.Tests.ps1`). |
| **One behavior per It** | PASS | Verified across all 7 tests. |
| **Mock sparingly / correct mock boundary** | PASS | Only filesystem cmdlets (`Test-Path`, `Get-ChildItem`, `Get-Content`) are mocked — the script's only I/O surface; no external executable is invoked by this script, so the "never mock git/gh directly" rule does not apply here. |
| **No external dependencies / deterministic** | PASS | No network, no live executables, no PATH/profile reliance, no working-directory assumptions; the main-entry-point tests' direct invocation pattern was empirically verified before authoring (documented in `bicep-secret-scan-poshqc-test.2026-07-07T02-35.md`) to terminate only the nested script invocation, not the Pester host process. |
| **No temporary files** | PASS | Zero temp-file usage (reviewer scan; no real file is ever created or read). |
| **Coverage >= 85% line / >= 75% branch** | PASS | 100% line / 100% command-proxy for the new file. |
| **Property-based tests** | N/A | Per the `.claude/rules/quality-tiers.md` gate, which keys off `quality-tiers.yml` (scoped to the C# solution's classified projects), the PowerShell script tree is not a classified project — same disposition as the F11 (#111) precedent, where the ops-tooling script tree is treated as T4-analog ("build scripts, dev tooling"), for which the property-density gate is "none." The uniform coverage gates were applied regardless and pass. The script's logic (regex-pattern matching over enumerated files) is exhaustively covered by the seven directed scenarios rather than property-sampled, consistent with prior PowerShell reviews. |
| **Determinism infrastructure (clock/RNG/fake timers)** | N/A | No time, randomness, or async code path in the script under test. |

---

## 5. Test Coverage Detail

### Test-OpenClawBicepParameterSecrets.Tests.ps1 (7 tests, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| reports a passing/clean result when no secret-shaped content is present | Positive | PASS |
| reports a failing result naming the offending file (secret-shaped literal) | Negative | PASS |
| handles a missing directory without an unhandled exception and reports a clean result | Edge case | PASS |
| handles an existing but empty directory without an unhandled exception and reports a clean result | Edge case | PASS |
| defaults `-Path` to `deploy/azure/parameters` when not supplied | Contract | PASS |
| exits 0 and reports the clean message when the target directory does not exist (main entry point) | State/branch coverage | PASS |
| exits 1 and writes an error naming the offending file when a secret-shaped literal is found (main entry point) | State/branch coverage | PASS |

**Coverage:** 54/54 instructions (100%), 38/38 lines (100%) for `scripts/Test-OpenClawBicepParameterSecrets.ps1`, reviewer-accepted from the executor's coverage-comparison evidence (internally consistent numeric delta reasoning independently verified: pre-existing-file covered-instruction count unchanged at 1,760 before/after isolates the entire +54 delta to this new file).

**Regression:** zero existing PowerShell files modified (the branch adds exactly one production script and its mirrored test); the reviewer's isolated 7/7 pass and the executor's full 365/365 repo-wide pass (358 baseline + 7 new) confirm no existing test broke.

**Bicep/YAML structural coverage (no numeric percentage applicable):** all 5 structural-review evidence artifacts (Phases 1-4 plus the final consolidated Phase 6 review) were read in full by the reviewer and found to cross-reference every added file/edit against the research artifact's Requirements Mapping table (§8) with an explicit pass/fail row per file; all rows PASS.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total PowerShell tests (repo, executor final run) | 365 passed / 365 (0 failed, 0 skipped) | PASS |
| New feature tests (reviewer isolated run) | 7 passed / 7 (0 failed, 0 skipped), ~813ms | PASS |
| Baseline tests | 358 (delta +7 = exactly the new feature suite) | PASS |
| Repo-wide coverage | 89.94% command/line (gate 85%) | PASS |
| New-code coverage | 100% line / 100% command-proxy (54/54, 38/38) | PASS |
| Bicep structural-review gates | 5/5 PASS (Phases 1, 2, 3, 4, and consolidated Phase 6) | PASS |
| Green workflow run against branch head `b3a252b` | **0 runs found** (`gh run list`) | **FAIL (Blocking, Section 8)** |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| PoshQC format (authoritative settings) | `mcp__drm-copilot__run_poshqc_format` (executor, baseline + batch + final full scope) | EXIT 0 all runs | PASS |
| Format idempotency (reviewer) | `Invoke-Formatter -ScriptDefinition` over both new PowerShell files | zero diffs | PASS |
| PoshQC analyze (authoritative settings) | `mcp__drm-copilot__run_poshqc_analyze` (executor, all scopes) | EXIT 0 all runs | PASS |
| PSScriptAnalyzer (reviewer) | `Invoke-ScriptAnalyzer -Path <script>, <test>` (1.24.0) | 0 diagnostics both files | PASS |
| Pester tests (reviewer, isolated) | `Invoke-Pester -Configuration <Run.Path = test file>` | 7 passed / 0 failed | PASS |
| CLI-tooling availability (reviewer, independent) | `Get-Command bicep -ErrorAction SilentlyContinue; Get-Command az -ErrorAction SilentlyContinue` | both empty, matching executor's baseline | PASS (confirms fallback is warranted, not fabricated) |
| Bicep structural review (executor, 5 gates) | manual brace/declaration/secret-literal review across Phases 1-4 + consolidated Phase 6 | all PASS | PASS |
| GitHub workflow-run history (reviewer, independent) | `gh run list` (all branches, `--limit 100`, grep for `azure-bicep`) and `gh run list --branch feature/azure-bicep-iac-125` | **zero rows** | **FAIL — Blocking (Section 8)** |
| Evidence-location scan (reviewer) | `git diff --name-only 7a29286..HEAD \| grep -E '^artifacts/(baselines\|baseline\|qa\|qa-gates\|evidence\|coverage\|regression-testing\|post-change)/'` | no matches | PASS |

**Notes:** The reviewer's PowerShell analyzer/formatter checks used PSScriptAnalyzer 1.24.0 with default settings (the repo-specific PoshQC bundled settings ship inside the MCP server bundle, which is available in this session as `mcp__drm-copilot__run_poshqc_format`/`run_poshqc_analyze`; the reviewer's independent method serves as a second, tool-diverse signal). Both methods agree: zero findings for the new PowerShell files.

---

## 8. Gaps and Exceptions

### Identified Gaps

**One Blocking finding**, one Minor finding, and several Info observations:

- **`modified-workflow-needs-green-run` — no qualifying green run against branch head (Blocking).** The branch diff modifies `.github/workflows/ci.yml` (adds the `bicep-validate` job) and adds `.github/workflows/_bicep-validate.yml`, both matching the rule's trigger glob `.github/workflows/**`. Per `.claude/skills/feature-review-workflow/SKILL.md`, this rule requires "evidence of a green workflow run against the branch head" — a workflow run whose head SHA matches the current branch head (`b3a252b`) with a `success` conclusion — before the finding can be closed. `gh run list --branch feature/azure-bicep-iac-125` returns zero rows; a broader `gh run list --limit 100 | grep azure-bicep` also returns zero rows. **No such run exists as of this audit.** This is independent of the structural-review evidence's cleanliness (which is sound) and independent of the CI-workflow file's own correctness (no deliberately-failing nested command exists in the new workflow, so the `ci-workflows.md` exit-code-reset requirement does not separately apply — see below). The rule explicitly permits a `workflow_dispatch` run against the branch head to satisfy it (not only a PR-context run), which mitigates the chicken-and-egg case where a feature must land its own CI gate before that gate can run in PR context. **Recommendation:** dispatch `ci.yml` (or, at minimum, `_bicep-validate.yml`) via `workflow_dispatch` against head `b3a252b` (or the final PR head, if the branch advances before merge) and capture the resulting run URL/conclusion as remediation-inputs evidence before this finding can close. Routed to `remediation-inputs.2026-07-07T05-30.md`.
- **`ci-workflows.md` deliberately-failing-nested-command rule: not triggered (Info, not a gap).** Reviewer read `.github/workflows/_bicep-validate.yml` in full: neither step (`bicep build ...` nor the `pwsh`-shell secret-scan invocation) intentionally invokes a command expected to fail as a negative-path self-validation. Both steps are expected to succeed on the happy path against the real, valid templates and the clean parameter file. The exit-code-reset requirement therefore does not apply to this workflow file — confirmed independently, matching the executor's own applicability note in `evidence/qa-gates/phase4-workflow-structural-review.2026-07-07T02-10.md`.
- **`benchmark-baselines.md`: not triggered (Info, not a gap).** No file under any benchmark-baseline path, and no `HostEnvironmentInfo`/baseline JSON of any kind, is added or modified by this branch (`git diff --name-only 7a29286..HEAD` contains no `scripts/benchmarks/**` or `*baseline*.json` path). The rule does not apply.
- **Service Bus API version is a `-preview` tag (Minor, non-blocking).** `deploy/azure/modules/queue.bicep` pins `Microsoft.ServiceBus/namespaces@2022-10-01-preview` for both the namespace and the queue resource. This is a valid, commonly-used Bicep API version for Service Bus (no non-preview namespace/queue API version postdates it as of this review), but preview API versions carry a documented Azure Resource Manager risk of behavior changes or retirement ahead of a stable release. Non-blocking because: (a) it is the correct, current version for this resource type per Microsoft's own Bicep documentation at the time of authoring, and (b) no live deployment occurs from this branch (deferred to the human-executed runbook), so there is no immediate production exposure. Recommendation (code review CR-1): track this as a follow-up to bump to a stable API version once Microsoft ships one for `Microsoft.ServiceBus/namespaces`/`queues`.
- **`run_poshqc_test` bundled-settings defect (Info, pre-existing workspace defect, not a finding against this branch).** The MCP tool's bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` entries for the `drm-copilot` source repository and fails at Pester coverage `RunStart` in this repository (first documented at F11/#111, reproduced identically here). Executor fallback: direct `Invoke-PoshQCTest` (the same bundled pipeline) with repo-scoped coverage settings, corrected `CodeCoverage.Path` listing all 30 current `scripts/**` production files with no `ExcludedPath` entries (per `.claude/rules/general-unit-test.md`'s Coverage Exclusion Policy — no production file excluded). Reviewer independently confirmed the CLI-availability fact underlying the parallel Bicep fallback and independently re-ran the new test file.
- **PowerShell script tree not classified in `quality-tiers.yml` (Info, pre-existing).** Same disposition as the F11 (#111) precedent: the tier map is scoped to the C# solution's projects; this script is administrator/CI-tooling (T4-analog). The uniform coverage gates were applied regardless and pass. No tier-dependent gate (property density, mutation) attaches.
- **MCP template/validator tools unavailable in this session (Info, documented accommodation).** The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set for a PowerShell-scoped branch (issue #111 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). This accommodation was previously accepted on the #80, #19, #18, #99-#111, #113-#120 series of reviews.

### Approved Exceptions

- **`bicep`/`az` CLI unavailable locally:** confirmed independently by the reviewer (`Get-Command bicep`/`Get-Command az` both empty). The executor's documented structural-review fallback substitutes for local `bicep build`; real `bicep build`/`actionlint` execution occurs only on the `windows-latest` GitHub Actions runner, which is exactly the mechanism the unresolved Blocking finding above requires evidence from.
- **PoshQC bundled settings not directly runnable by the reviewer via the failing MCP path:** the reviewer used PSScriptAnalyzer/`Invoke-Formatter` defaults as an independent second signal for the new files and relied on the executor's bundled-pipeline-workaround evidence (internally cross-checked for numeric consistency) for the coverage gate, per the same accommodation accepted on prior PowerShell-touching reviews.
- **Live tenant deployment (`az deployment group create`) out of scope for automated execution:** documented as a `human_interaction` exception with runbook `docs/features/active/2026-07-07-azure-bicep-iac-125/runbooks/azure-bicep-deployment.runbook.md` (five required sections present in order, per-step dated Microsoft Learn citations), consistent with the F11/F14/F15/F17 precedent. Recording the exception in `artifacts/orchestration/orchestrator-state.json` remains the orchestrator's responsibility, not this branch's files; this review confirms the branch-side documentation half of that contract is complete.

### Removed/Skipped Tests

- **None removed.** No existing test file was modified, deleted, or weakened (the branch adds files only). Reviewer's isolated run had zero skips.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `epic/openclaw-vision-integration`)

Branch `feature/azure-bicep-iac-125`, head `b3a252b` (single commit `feat(deploy): add Azure Bicep IaC for OpenClaw.Core Stage 1 footprint`). Range: `7a29286b687f00c6a10809efa41102c78f009c36..b3a252b` (42 files, +1697/-1).

### Files Modified (categories)

1. **`deploy/azure/`** (NEW, 7 files) — `main.bicep`, three resource modules, one `.bicepparam` binding, `README.md`.
2. **`.github/workflows/_bicep-validate.yml`** (NEW) — reusable workflow (`workflow_call` + `workflow_dispatch`), `bicep build` + parameter-file secret-scan steps.
3. **`.github/workflows/ci.yml`** (edit) — one added job referencing the reusable workflow above; the three pre-existing jobs are byte-identical.
4. **`.gitignore`** (edit) — one added re-include line for the new workflow file.
5. **`scripts/Test-OpenClawBicepParameterSecrets.ps1` + `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1`** (NEW) — parameter-file secret-scan script and its mirrored Pester test.
6. **`docs/features/active/2026-07-07-azure-bicep-iac-125/`** (NEW, ~29 files) — issue/spec/user-story/plan, research artifact, the human-exception runbook, and canonical evidence (`baseline/`, `qa-gates/`, `other/`).
7. **`.claude/agent-memory/`** (5 Markdown files) — human-exception-runbook and task-researcher memory updates from prior sessions in this worktree, not code.

---

## 10. Compliance Verdict

### Overall Status: NOT YET COMPLIANT — one Blocking finding (workflow green-run evidence)

The Bicep IaC delivery, the PowerShell secret-scan script and its test, the human-exception runbook, and every acceptance-criterion-bearing artifact in the feature folder are complete, independently re-verified by the reviewer, and pass every applicable gate: formatting, linting, testing, coverage (repo-wide and new-code), structural Bicep/YAML review, evidence-location compliance, and tonality. The single outstanding item is procedural, not a defect in the delivered code: the `modified-workflow-needs-green-run` rule requires a green workflow run against the exact branch head before a workflow-file change can be considered ready to merge, and no such run has yet been dispatched or recorded for `b3a252b`. This is expected to be resolved by a `workflow_dispatch` of `ci.yml` against the (possibly-advancing) PR head, per the rule's explicit `workflow_dispatch` allowance — this audit does not perform or authorize that dispatch itself; it records the gap and routes it to remediation inputs.

**Fail-closed reminder:** the Blocking finding above is recorded precisely because a required gate's evidence is absent, not fabricated as passing. All other required baseline and post-change metrics are present and independently re-verified.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS
- Design Principles: PASS
- Module & File Structure: PASS (all new files under 500 lines, max 183)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (PowerShell); documented CLI-unavailable fallback (Bicep/YAML)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — PowerShell
- Toolchain & Compatibility: PASS (with the documented `run_poshqc_test` workspace-defect accommodation)
- Function Design & Validation: PASS
- Error Handling: PASS (fail-fast, no silent catch-alls, one documented intentional `SilentlyContinue` for the missing/empty-directory contract)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (89.94% repo-wide; 100% new-code)
- Test Structure: PASS
- External Dependencies: PASS
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — PowerShell
- Framework & Location: PASS
- Mocking Rules: PASS (filesystem-cmdlet mocking is the correct boundary; no wrapper-seam requirement applies)
- Determinism: PASS

#### CI Workflow Authoring (`ci-workflows.md`)
- Deliberately-failing-nested-command pattern: N/A (not present in the new workflow)
- **`modified-workflow-needs-green-run` (feature-review-workflow rule): FAIL — Blocking**

#### Benchmark Baseline Provenance (`benchmark-baselines.md`)
- Not triggered — no baseline file or `scripts/benchmarks/**` path in the diff.

---

### Metrics Summary

- 365/365 repo PowerShell tests passing (executor final run; reviewer isolated re-run of the 7 new tests: 7/7)
- Repo-wide coverage 89.94% command/line (gate 85%); +0.28 pp vs. baseline
- New-code coverage 100% line / 100% command-proxy (54/54 instructions, 38/38 lines; gate 85% line / 75% branch)
- Zero analyzer diagnostics (two independent methods); zero format diffs
- All new files under the 500-line cap (max 183)
- Zero existing PowerShell/C# production files modified (additions only, plus the single documented `ci.yml` wiring edit)
- 5/5 Bicep/YAML structural-review gates PASS
- **0 qualifying green workflow runs against branch head `b3a252b`** — Blocking

---

### Recommendation

**No-Go for PR until the Blocking finding closes.** Dispatch `ci.yml` via `workflow_dispatch` against the branch head (or the final PR head) and capture a `success` conclusion as remediation-inputs evidence; once that run exists, re-audit Section 8 only (all other sections require no rework) and the verdict is expected to move to Go. See `remediation-inputs.2026-07-07T05-30.md` for the explicit remediation-required finding and required verification command.

---

## Appendix A: Test Inventory

PowerShell test changes in this feature (all NEW):

1. `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1` (183 lines, 7 tests) — clean-file pass, secret-shaped-file fail (offending path named), missing-directory no-crash, empty-directory no-crash, default `-Path` value, main-entry-point exit-0 clean path, main-entry-point exit-1 dirty path with captured error record.

Reviewer run: 7 passed / 0 failed / 0 skipped (isolated file run, ~813ms). Executor's full-repo run: 365 passed / 0 failed / 0 skipped (358 baseline + 7 new).

Bicep/YAML structural-review evidence inventory (no unit-test framework applies; declarative structural checks):

1. `evidence/qa-gates/phase1-bicep-structural-review.2026-07-07T01-40.md` — `keyVault.bicep`, `queue.bicep`.
2. `evidence/qa-gates/phase2-bicep-structural-review.2026-07-07T01-50.md` — `containerApp.bicep`, `main.bicep`.
3. `evidence/qa-gates/phase3-bicep-structural-review.2026-07-07T02-00.md` — `main.dev.bicepparam`, `README.md`.
4. `evidence/qa-gates/phase4-workflow-structural-review.2026-07-07T02-10.md` — `_bicep-validate.yml`, `ci.yml` edit.
5. `evidence/qa-gates/final-bicep-yaml-structural-review.2026-07-07T03-00.md` — consolidated, all 8 files/edits against the research artifact's Requirements Mapping table (§8).

---

## Appendix B: Toolchain Commands Reference

```powershell
# Formatting (executor, authoritative bundled settings)
mcp__drm-copilot__run_poshqc_format   # baseline + batch + full repo, EXIT 0

# Formatting idempotency (reviewer, defaults)
Invoke-Formatter -ScriptDefinition (Get-Content scripts/Test-OpenClawBicepParameterSecrets.ps1 -Raw)
Invoke-Formatter -ScriptDefinition (Get-Content tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1 -Raw)
# both IDEMPOTENT, zero diffs

# Linting (executor, authoritative bundled settings)
mcp__drm-copilot__run_poshqc_analyze  # baseline + batch + full repo, EXIT 0

# Linting (reviewer, PSScriptAnalyzer 1.24.0 defaults)
Invoke-ScriptAnalyzer -Path scripts/Test-OpenClawBicepParameterSecrets.ps1
Invoke-ScriptAnalyzer -Path tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1
# both CLEAN: 0 diagnostics

# Tests (executor: bundled Invoke-PoshQCTest pipeline with repo-scoped coverage settings
# after run_poshqc_test failed on the pre-existing bundled-settings defect)
Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.openclaw.runsettings.psd1

# Tests (reviewer, check-only, isolated to the new file)
$config = New-PesterConfiguration
$config.Run.Path = 'tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1'
Invoke-Pester -Configuration $config   # 7 passed / 0 failed

# CLI-tooling availability (reviewer, independent confirmation)
Get-Command bicep -ErrorAction SilentlyContinue
Get-Command az -ErrorAction SilentlyContinue
# both empty

# Evidence-location scan
git diff --name-only 7a29286b687f00c6a10809efa41102c78f009c36..HEAD | grep -E '^artifacts/'

# Workflow-rule trigger scan (modified-workflow-needs-green-run)
git diff --name-only 7a29286b687f00c6a10809efa41102c78f009c36..HEAD | grep -E '^(\.github/workflows/|scripts/benchmarks/|\.github/actions/)'

# Green-run evidence check (reviewer, independent, GitHub CLI)
gh run list --branch feature/azure-bicep-iac-125 --limit 10
gh run list --limit 100   # broader scan, grep for the feature branch/head
# both: zero rows for this branch/head as of this audit
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-07
**Policy Version:** Current (as of audit date)
