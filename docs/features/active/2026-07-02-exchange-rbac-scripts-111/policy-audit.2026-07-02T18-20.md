# Policy Compliance Audit: exchange-rbac-scripts (#111)

**Audit Date:** 2026-07-02
**Code Under Test:** PowerShell only. 9 NEW production files — the `OpenClawRbac` module at `scripts/powershell/modules/OpenClawRbac/` (`.psd1` manifest 29 lines, `.psm1` root module 27 lines, `OpenClawRbac.Seams.ps1` 197 lines with nine runtime-resolved wrapper functions, five public-function files 68-121 lines each) plus the thin entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1` (156 lines). 8 NEW Pester test files under `tests/scripts/` mirroring the production layout (70-215 lines each). Plus the feature-folder documentation set (issue/spec/user-story/plan, human-exception runbook, canonical evidence artifacts) and three agent-memory Markdown records. No C#, Python, TypeScript, or Bash production files changed in the branch diff.

**Scope:** Full feature branch `feature/exchange-rbac-scripts-111` @ `0c1104a85b4b520f17a1eaab7cbb8006eb2b14aa` versus resolved base `main` @ merge-base `8d389819d50174be9610ae69c1c4b5c9da05f829` (origin/main; the local `main` ref is stale per the caller inputs — reviewer confirmed `git merge-base HEAD origin/main` returns the same SHA and the PR-context artifacts resolve the same range). Scope is feature-vs-base over the complete branch diff. Diff file breakdown (name-status): 17 PowerShell files (9 production + 8 test, all added), 33 Markdown files (50 files, +3201/-3). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md` (mirrored in `issue.md`).

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| PowerShell | 9 production + 8 test (all NEW) | 358 (repo Pester suite; baseline 281, +77 new feature tests) | 358 pass, 0 fail, 0 skip (reviewer re-run at head) | 88.47% command (1,752 commands, 21 files) | 89.66% command (1,963 commands, 29 files); per-file LINE counters aggregate to 90.22% (1393/1544) — reviewer-parsed from `artifacts/pester/powershell-coverage.xml` | New-code aggregate 168/169 = 99.41% line (command 99.53%); per-file: psm1 5/5, Seams 59/60 (98.33%), Register 9/9, Scope 11/11, Grant 31/31, SendOnBehalf 12/12, Boundary 25/25, entry script 16/16 — all 100% except Seams; the `.psd1` manifest is data-only with no executable code |
| C# | 0 production files changed | 358+ (executor final `dotnet build`/`dotnet test` EXIT 0) | pass (executor cross-language regression guard) | n/a — no changed files | n/a — no changed files | N/A - no changed files in this language on the branch |

**Note:** C#, Python, Bash, and TypeScript coverage rows are N/A because the branch diff contains no changed production or test files in those languages (the only non-PowerShell changes are Markdown documents). Coverage verdicts are therefore PowerShell-only; the PowerShell coverage verdict is an explicit **PASS**.

**Branch-coverage metric note (PowerShell):** Pester v5 emits command-level coverage only and produces no branch-percentage metric for PowerShell. Command coverage counts every command inside every branch arm, so untaken branch arms surface as uncovered commands; the command figure is the branch-sensitive signal. This disposition follows repository precedent (features #58 and #62, cited in the executor's baseline and final artifacts). The single uncovered new-code command (Seams.ps1 line 112, the `RecipientAdministrativeUnitScope` pass-through arm of `Invoke-OpenClawNewManagementRoleAssignment`) is the only untaken branch arm in the new code and is named as a Minor finding in the code review; 168/169 = 99.41% line and 99.53% command both clear the 85%/75% uniform gates by a wide margin.

### Coverage Evidence Checklist

- PowerShell baseline coverage artifact: `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/baseline/poshqc-test.2026-07-02T17-25.md` (repo-wide 88.47% command, 281 tests, EXIT 0)
- PowerShell post-change coverage artifact: `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/qa-gates/final-poshqc-test.2026-07-02T18-55.md` and `evidence/qa-gates/coverage-comparison.2026-07-02T18-57.md` (raw XML at `artifacts/pester/powershell-coverage.xml`, reviewer independently re-parsed per file: line and command counters reproduce the executor's figures exactly)
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / C# / Bash coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (PowerShell), with per-new-file line coverage re-measured by the reviewer from the on-disk Pester coverage XML. The PowerShell coverage gate is met (repo-wide 89.66% command / 90.22% line-counter >= 85%; new-code 99.41% line >= 85%; branch-sensitive command metric 99.53% >= 75%; no regression — the 21 pre-existing measured files are unchanged by this branch and the repo-wide figure rose +1.19 pp).

---

## Executive Summary

This feature branch closes issue #111 (gap item F11, Epic C item 11): the Exchange Online Application RBAC administrator checklist (master doc §12 Steps 2-5 and 7, §13 Step 3) delivered as an idempotent, parameterized PowerShell module. The delivery is: (a) the `OpenClawRbac` module — nine runtime-resolved wrapper seams (one per Exchange cmdlet, resolved via `Get-Command` with a specific actionable error when unresolved; `Get-*` wrappers return `$null` on not-found) plus five public advanced functions (`Register-OpenClawServicePrincipal`, `New-OpenClawMailboxScope`, `Grant-OpenClawRbacRoles`, `Set-OpenClawSendOnBehalf`, `Test-OpenClawScopeBoundary`), all `CmdletBinding()`, state-changing functions `SupportsShouldProcess`, check-before-create idempotency, no parse-time ExchangeOnlineManagement dependency; (b) the thin entry script sequencing Register → Scope → Grant → SendOnBehalf → Boundary with `-WhatIf` forwarding and exit-code mapping (0/1); (c) a fully mocked Pester suite (77 tests) mirroring the production layout, mocking only the wrapper seams; and (d) a contract-conformant human-exception runbook covering master §12 Steps 1-8 and §13 Step 3 with dated Microsoft Learn citations, because live-tenant execution is a recorded human exception (HI-1 in orchestrator state, orchestrator-verified per AC-4).

The toolchain was independently re-verified by the reviewer against branch head `0c1104a`:
- **Formatting:** `Invoke-Formatter` idempotency check over all 16 script files — zero diffs (executor's authoritative `run_poshqc_format` MCP runs at batch and final scope all EXIT 0).
- **Linting:** `Invoke-ScriptAnalyzer` (1.24.0) over the module, entry script, and all test files — zero diagnostics at any severity (executor's `run_poshqc_analyze` MCP runs, which use the bundled repo settings, all EXIT 0).
- **Type checking:** not applicable for PowerShell per `.claude/rules/powershell.md`.
- **Tests:** reviewer ran the full repo Pester suite (`tests/scripts` + `tests/powershell`) — 358 passed, 0 failed, 0 skipped, matching the executor's final run exactly.
- **Coverage:** reviewer independently parsed `artifacts/pester/powershell-coverage.xml` (produced by the executor's final full-scope run at 2026-07-02T18-55) — per-file and aggregate figures reproduce the executor's evidence to the hundredth.

One pre-existing workspace tooling defect is documented (not a finding against the branch): the bundled PoshQC `run_poshqc_test` MCP tool fails at Pester coverage RunStart in this repository because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` entries for the drm-copilot repository, six of which do not exist here. The executor ran the identical bundled `Invoke-PoshQCTest` pipeline directly with repo-scoped coverage settings (precedent from features #58/#62; diagnosis and deviation recorded in `evidence/baseline/poshqc-test.2026-07-02T17-25.md` and every subsequent test artifact). The reviewer accepts this accommodation: the same Pester pipeline executed with a corrected coverage path list, and the reviewer's own independent test run confirms the results.

No Blocking findings. One Minor finding (seam-level AU-scope forwarding arm untested — the single uncovered new-code command) and several Info observations (Section 8 and the code review). Remediation is not required. The feature is recommended Go for PR.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/powershell.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/orchestrator-state.md` (evaluated — the branch does not modify `orchestrator-state.json`; the pre-existing `human_interaction` block was read-verified against the rule's three invariants: requirements list present, `response: "exception"` in the enum, non-empty `runbook_path`)
- `.claude/rules/tonality.md`
- `.claude/skills/human-exception-runbook/SKILL.md` (runbook contract)

**Language-specific policies evaluated:**
- PowerShell: `.claude/rules/powershell.md`
- N/A C# / Python / Bash / TypeScript (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts are tracked by this feature; the diff is the module, entry script, tests, and documentation/evidence Markdown. The executor's scratchpad runsettings files (`pester.batch*.runsettings.psd1` etc.) lived in the session scratchpad and are not in the diff. The raw Pester outputs under `artifacts/pester/` are untracked tool outputs at the path the feature-review skill itself designates for PowerShell coverage.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`8d38981`), the checked-out feature branch, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." The caller's context notes (PoshQC bundled-settings defect, AC-4 orchestrator ownership) were framed as facts, not scope constraints, and were independently re-verified rather than accepted. No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observations (not narrowing instructions, recorded for completeness): (1) unlike the six prior reviews (#99-#109), the PR-context summary's "Changed files overview" correctly categorizes this branch (15 core-logic files, PowerShell recognized); the authoritative `git diff 8d38981..0c1104a` was still used as the scope source. (2) The summary's author-asserted autoclose list contains the non-issue tokens `#AC-1`, `#AC-3`, `#AC-4`, `#HI-1`, and `#ISO-8601` parsed from AC labels and evidence prose — noise per the established pattern; only #111 is the closing issue. GitHub CLI is unavailable, so autoclose verification is author-asserted only.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 8d38981..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/<kind>/` locations (`baseline/`, `qa-gates/`, `other/`); the runbook lives at the canonical non-evidence path `<FEATURE>/runbooks/exchange-rbac-setup.runbook.md`.
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, #18, and #99-#109 audits); the scan was performed by direct diff inspection. The executor's own `plan-reconciliation.2026-07-02T19-01.md` records the same forbidden-path probe with zero hits.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Every test file imports the module fresh in `BeforeAll` (`Import-Module ... -Force`) and removes it in `AfterAll`; mocks are registered per-`Describe`/`BeforeEach` or inline per-`It`; the seam tests remove injected fake commands in `AfterEach`. 358/358 pass in the reviewer's single run. |
| **Isolation** — Each test targets single behavior | PASS | One behavior per `It` throughout: per-parameter binding rejections, one idempotency short-circuit per function, one ShouldProcess dry-run per function, one boundary-matrix cell per test, one seam contract clause per test (missing-cmdlet / argument-forwarding / null-on-not-found). |
| **Fast Execution** | PASS | Full repo suite completes in ~21 s (reviewer run); the feature suite is pure in-process mocking with no I/O. |
| **Determinism** | PASS | No clock, no randomness, no sleeps, no network, no filesystem writes; all inputs are fixed placeholder GUIDs/addresses; the entry-script tests invoke the script in-process with the call operator and read `$LASTEXITCODE`. |
| **Readability & Maintainability** | PASS | Descriptive `It` names stating scenario and expectation; explicit Arrange/Act/Assert comments; `-Because` clauses on the seam forwarding assertions; data-driven `-ForEach` contract cases named per wrapper. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline repo-wide 88.47% command (1,752 commands, 21 files), 281 tests. Source: `evidence/baseline/poshqc-test.2026-07-02T17-25.md`. |
| **No Coverage Regression** | PASS | Post-change 89.66% command (+1.19 pp) on the identical metric; the 21 baseline production files are unchanged by this branch (all PowerShell changes are additions), so changed-line regression is structurally impossible; the aggregate rose because the 8 new measurable files land at 99.41% line. |
| **New Code Coverage** | PASS | 168/169 = 99.41% line across the module + entry script, reviewer-parsed per file from the coverage XML; lowest file `OpenClawRbac.Seams.ps1` at 98.33% (single uncovered command at line 112, named in the code review). Test files are excluded from measurement per policy. |
| **Comprehensive Coverage** | PASS | Parameter validation (GUID binding, empty/non-SMTP rejection), idempotency short-circuits for all three Get-checked functions plus per-role for Grant, ShouldProcess/WhatIf dry-runs for all four state-changing functions and the entry script, exact wrapper argument forwarding, all nine missing-cmdlet errors, all three null-on-not-found contracts, the full four-cell boundary matrix, entry-script sequencing/skip/exit-code paths. |
| **Positive Flows** | PASS | Creation paths with exact arguments for all functions; (allowed, denied) boundary success; entry-script happy path exit 0. |
| **Negative Flows** | PASS | Binding rejections; mutually-exclusive parameter sets rejected; unexpected `Add-MailboxPermission` errors re-thrown; three failing boundary-matrix cells with precise `FailureReason` strings; entry-script exit 1. |
| **Edge Cases** | PASS | Existing-ACE targeted error match vs. any-other-error re-throw; both-sides-fail reason joining; pipeline input (multiple principals); AU parameter-set route skipping scope creation; default parameter values pinned. |
| **Error Handling** | PASS | All nine wrappers' missing-cmdlet errors asserted to name the cmdlet, the module, and `Connect-ExchangeOnline`; the targeted idempotency catch re-throw path pinned. |
| **Concurrency** | N/A | Sequential administrative functions; no concurrency surface. |
| **State Transitions** | N/A | All state lives in the Exchange tenant (mocked); functions are stateless. |

### 1.2.1 Per-Language Coverage Comparison

- PowerShell: Baseline: 88.47% -> Post-change: 89.66%, Change: +1.19% (command metric; per-file LINE counters aggregate to 90.22% post-change), New/changed-code coverage: 168/169 = 99.41% line and 99.53% command across the 8 new measurable production files (per-file: 100% for seven files, Seams.ps1 98.33%; branch-sensitive command proxy per #58/#62 precedent because Pester v5 emits no branch percentage), Disposition: PASS (repo-wide >= 85%, new-code >= 85% line and >= 75% branch-proxy, no regression — all pre-existing measured files unchanged), Evidence: `evidence/baseline/poshqc-test.2026-07-02T17-25.md`, `evidence/qa-gates/final-poshqc-test.2026-07-02T18-55.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T18-57.md`, reviewer re-parse of `artifacts/pester/powershell-coverage.xml` and independent 358/358 test re-run.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | `-Because` clauses on the seam forwarding/count assertions; exact-string assertions on `FailureReason` values; `Should -Invoke ... -ParameterFilter` failures identify the mismatched argument set. |
| **Arrange-Act-Assert Pattern** | PASS | Explicit `# Arrange` / `# Act` / `# Assert` comments in every behavior test across all eight files. |
| **Document Intent** | PASS | Every test file carries a `.SYNOPSIS`/`.DESCRIPTION` block stating the covered contract clauses; individual `It` names are self-describing. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No tenant connectivity anywhere; ExchangeOnlineManagement is never imported (pinned by a dedicated test scanning the module for `Import-Module ExchangeOnlineManagement` and `#Requires -Modules`); no network, no external processes. |
| **Use Mocks/Stubs** | PASS | Only the `Invoke-OpenClaw*` wrapper seams are mocked (`Mock -ModuleName OpenClawRbac`), matching the mocking rule; Exchange cmdlets are never mocked directly — the seam tests inject fake resolvable commands into module scope to exercise the real wrapper bodies. Mock signatures mirror production wrapper parameters (signature parity). |
| **Environment Stability** | PASS | No temporary files (reviewer scan of the test diff for temp-file APIs: zero matches; the executor's `ac1-module-surface` gate scanned the same); no reliance on machine PATH, profile, or working-directory state — all paths resolve relative to `$PSScriptRoot`. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #111, `spec.md` v1.0 (five recorded design decisions D1-D5 with layout, seam contract, idempotency model, runbook contract, and result semantics), user-story personas/scenarios, master §12/§13 references. |
| **Read existing change plans** | PASS | `evidence/other/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-02T16-53.md` present with batch structure honoring the PowerShell change budget. |
| **Document the plan** | PASS | Plan with three code batches (3+3, 3+3, 3+2 files) under the per-batch cap plus a docs-only batch; per-batch gate evidence under `evidence/qa-gates/`; `evidence/other/plan-reconciliation.2026-07-02T19-01.md` reconciles delivered files against the plan. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | One wrapper per cmdlet, one file per public function, a dot-sourcing root module, and a sequencing-only entry script; no runner frameworks, no configuration machinery, no abstraction beyond the mandated seam pattern. |
| **Reusability** | PASS | The nine seams are the single choke point for all Exchange calls (reviewer grep: no production function invokes an Exchange cmdlet directly); the missing-cmdlet error message is the only repeated fragment, a reasonable trade against indirection for a 9-function seam file. |
| **Extensibility** | PASS | Keyword parameters with defaults throughout; two parameter sets on `Grant-OpenClawRbacRoles` route scope vs. Administrative Unit; the deferred `Get-MailboxPermission` wrapper follow-up is documented in spec D3 rather than speculatively built. |
| **Separation of concerns** | PASS | Pure decision logic (boundary evaluation, role iteration, idempotency checks) sits in the public functions; all external I/O is confined to the seams; exit-code mapping is confined to the entry script (pinned by a static-scan test that the boundary function contains no `exit`). |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Module layout follows the observed repo convention (`scripts/powershell/modules/<Name>/` with `.psd1` + `.psm1`, per the `OpenClawContainerValidation` model); tests mirror production paths under `tests/scripts/`. |
| **Under 500 lines** | PASS | Reviewer `wc -l` on all 17 files: max 215 (`Test-OpenClawScopeBoundary.Tests.ps1`); production max 197 (`OpenClawRbac.Seams.ps1`). |
| **Public vs internal** | PASS | Manifest exports the five public functions plus the nine wrappers; wrapper export is a recorded spec decision (D1) enabling `Mock -ModuleName` and argument verification. Nothing else is exported (`CmdletsToExport`/`AliasesToExport`/`VariablesToExport` all empty). |
| **No circular dependencies** | PASS | The module depends on nothing in-repo; the entry script imports the module by relative path; tests import the manifest only. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | Approved verbs throughout (`Register-`, `New-`, `Grant-`, `Set-`, `Test-`, `Invoke-`); nouns are prefixed and self-describing; the deliberate plural in `Grant-OpenClawRbacRoles` carries a scoped, justified `SuppressMessageAttribute` (spec-mandated name). |
| **Docs/docstrings** | PASS | Comment-based help on every public function and every wrapper (`.SYNOPSIS`, `.DESCRIPTION`, `.PARAMETER`, `.OUTPUTS`, `.EXAMPLE`); the wrong-ID warning (Enterprise Application Object ID vs App Registration object ID) is in the help text where the spec demanded it. |
| **Comment why, not what** | PASS | The targeted idempotency catch carries a comment explaining the documented error match and re-throw rule; the `.psm1` documents why it contains only dot-sourcing. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Executor: `run_poshqc_format` MCP EXIT 0 at batch scopes and full-repo final scope (`final-poshqc-format.2026-07-02T18-50.md`). Reviewer: `Invoke-Formatter` idempotency over all 16 script files — zero diffs. |
| **2. Linting** | PASS | Executor: `run_poshqc_analyze` MCP EXIT 0 at batch and full scopes (`final-poshqc-analyze.2026-07-02T18-51.md`). Reviewer: `Invoke-ScriptAnalyzer` 1.24.0 over module + tests + entry script — zero diagnostics. |
| **3. Type checking** | N/A | Not applicable for PowerShell per `.claude/rules/powershell.md`. |
| **4. Architecture** | N/A | No architecture-boundary tooling exists for the PowerShell script tree; the module has no in-repo dependencies. The C# NetArchTest suite is unaffected (no C# changes; executor final `dotnet test` EXIT 0). |
| **5. Testing** | PASS | Reviewer: full repo Pester run — 358 passed / 0 failed / 0 skipped, matching `final-poshqc-test.2026-07-02T18-55.md` exactly. |
| **6. Contract/schema checks** | N/A | No host-service schema or wire contract changed; the wrapper parameter subsets are the module's outward contract and are pinned by the seam forwarding tests. |
| **7. Integration tests** | N/A | Deliberately none: live-tenant execution is the recorded human exception (HI-1); the spec and non-goals prohibit any CI tenant call. The runbook carries the human verification procedure. |
| **Full toolchain loop** | PASS | Executor evidence shows format → analyze → test re-run per batch and a final full-scope clean pass; the batch-2 format artifact records a restart after an auto-fix, satisfying the restart rule. Reviewer's independent pass was clean first time. |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in the executor's evidence set (every artifact carries timestamp, command, EXIT_CODE). |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy matches the delivered diff exactly (9 production files, 8 test files, runbook, evidence); the commit message describes the feature. |
| **Design choices explained** | PASS | Spec D1-D5 record layout, seam contract, idempotency (including the `Set-OpenClawSendOnBehalf` targeted-catch trade-off and its documented follow-up), runbook sourcing, and result/exit semantics. |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `spec.md`, `user-story.md`, and the `issue.md` mirror; the runbook is authored and cross-referenced from orchestrator state. |
| **Provide next steps** | PASS | Non-goals record the deferred `Get-MailboxPermission` wrapper, the deferred calendar-write grant, and the human-only §12 Step 6 review; the runbook's Step 7 checklist defines the engineering handoff. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 PowerShell applies. C#, Python, Bash, and TypeScript sections are omitted: no changed files in those categories on the branch.

### Section 3-PowerShell: PowerShell Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Toolchain via MCP (format → analyze → test)** | PASS with documented accommodation | Format and analyze ran via the MCP commands (EXIT 0 at every gate). The test stage's MCP tool `run_poshqc_test` fails at coverage RunStart in this repo due to the bundled settings' foreign `CodeCoverage.Path` entries (pre-existing workspace defect, diagnosed in the baseline artifact); the executor ran the identical bundled `Invoke-PoshQCTest` pipeline directly with repo-scoped coverage paths (features #58/#62 precedent). Reviewer independently re-ran the tests (358/358) and re-parsed the coverage XML. |
| **PowerShell 7+ compatibility** | PASS | `#Requires -Version 7` in every script file; manifest `PowerShellVersion = '7.0'`; no Windows-PowerShell-only constructs; analyzer clean. |
| **Advanced functions, CmdletBinding, validation** | PASS | All 14 functions use `[CmdletBinding()]`; mandatory parameters marked; `[guid]` typing, `ValidateNotNullOrEmpty`, and SMTP `ValidatePattern` attributes throughout; executor's `ac1-module-surface` gate verified per-function CmdletBinding and per-parameter validation programmatically. |
| **ShouldProcess for state changes** | PASS | All four state-changing functions and the entry script declare `SupportsShouldProcess`; every write call site is inside a `ShouldProcess` gate; `-WhatIf` → zero write-wrapper invocations pinned by tests for each function and the entry script. |
| **No global state / no Invoke-Expression / no secrets / no hardcoded paths** | PASS | Reviewer grep: no `Invoke-Expression`, no credential or secret handling (spec non-goal: ambient `Connect-ExchangeOnline` session), no tenant values (executor gate scanned for real GUIDs/org names), paths resolve via `$PSScriptRoot`. |
| **Fail fast, no silent catch-alls** | PASS | Wrappers throw specific missing-cmdlet errors; the single `catch` block is targeted (documented existing-ACE message match) and re-throws everything else; `$ErrorActionPreference = 'Stop'` in the entry script and root module. `Get-*` wrappers' `ErrorAction SilentlyContinue` is the deliberate not-found-is-data contract (spec D2), not error suppression, and the forwarding tests pin it. |
| **Approved verbs / naming** | PASS | Analyzer clean including `PSUseApprovedVerbs`; the one suppression (`PSUseSingularNouns` on `Grant-OpenClawRbacRoles`) is scoped, justified, and spec-mandated. |
| **Under 500 lines / cohesive** | PASS | Max production file 197 lines. |
| **Change budget (per-batch cap 3+3)** | PASS | Plan batches: 3+2, 3+3, 3+2 — all within the cap; work routed through the orchestrated mode as the 9-production-file scope requires. |
| **Seam pattern** | PASS | One wrapper per external cmdlet with explicit named parameters covering exactly the used subset; no parameter named `Args`; runtime resolution; tests mock wrappers only. |

---

## 4. Language-Specific Unit Test Policy Compliance

Only PowerShell tests changed. C#, Python, and TypeScript sections are omitted.

### Section 4-PowerShell: PowerShell Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework — Pester v5** | PASS | Pester 5.6.1; `Describe`/`Context`-free flat `Describe`/`It` structure with `BeforeAll`/`BeforeEach`/`AfterEach`/`AfterAll`; data-driven `-ForEach` contract cases for the nine seams. |
| **Test file location & naming** | PASS | `tests/scripts/powershell/modules/OpenClawRbac/*.Tests.ps1` mirrors `scripts/powershell/modules/OpenClawRbac/`; `tests/scripts/Invoke-OpenClawExchangeRbacSetup.Tests.ps1` mirrors `scripts/`. (Pre-existing tests under `tests/scripts/` are flat; the new files follow the written mirrored-layout rule — recorded in spec D1.) |
| **One behavior per It** | PASS | Verified across all eight files. |
| **Mock sparingly / wrapper-only mocking** | PASS | Only `Invoke-OpenClaw*` seams are Pester-mocked; real wrapper bodies are exercised by injecting fake resolvable commands (no Exchange cmdlet is Pester-mocked anywhere); mock signature parity holds (mock `param()` blocks mirror production wrapper parameters, with `PSReviewUnusedParameter` satisfied by explicit references). |
| **No external dependencies / deterministic** | PASS | No network, no live executables, no PATH/profile reliance, no working-directory assumptions (`$PSScriptRoot`-relative resolution); in-process entry-script invocation. |
| **No temporary files** | PASS | Zero temp-file usage (reviewer scan; executor gate scan). |
| **Coverage >= 85% line / >= 75% branch** | PASS | 99.41% line new-code; 99.53% command as the branch-sensitive proxy (Pester emits no branch metric — documented toolchain limitation per #58/#62 precedent); repo-wide 89.66%/90.22%. |
| **Property-based tests** | N/A | The T1/T2 property-test gate keys off `quality-tiers.yml`, which classifies the solution's C# projects; the PowerShell script tree is not a classified project. By the tier definitions these administrator setup scripts are ops tooling (T4-analog: "build scripts, dev tooling"), for which the property-density gate is "none". The uniform coverage gates were applied regardless and pass. The near-pure decision logic (boundary matrix) is exhaustively enumerated (all four cells) rather than property-sampled. |
| **Determinism infrastructure (clock/RNG/fake timers)** | N/A | No time, randomness, or async in the code under test. |

---

## 5. Test Coverage Detail

### OpenClawRbac.Module.Tests.ps1 (3 tests, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| imports without error and without importing ExchangeOnlineManagement | Positive (AC-1/AC-2 parse-time independence) | PASS |
| exports exactly the function set declared in FunctionsToExport | Contract (manifest surface, both directions) | PASS |
| contains no parse-time Import-Module of ExchangeOnlineManagement and no #Requires -Modules directive | Negative (static scan of every module file) | PASS |

### OpenClawRbac.Seams.Tests.ps1 (21 tests: 9 missing-cmdlet + 9 forwarding + 3 not-found, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| throws a specific error naming `<CmdletName>` when the cmdlet cannot be resolved (x9, all wrappers) | Negative (runtime-resolution failure, actionable message) | PASS |
| invokes the resolved command with the exact named arguments passed (x9, all wrappers; exact-count assertion incl. the Get-* `ErrorAction` contract) | Contract (argument forwarding through real wrapper bodies) | PASS |
| returns $null when the underlying lookup reports not-found (x3, Get-* wrappers) | Contract (not-found-is-data) | PASS |

### Register-OpenClawServicePrincipal.Tests.ps1 (8 tests) / New-OpenClawMailboxScope.Tests.ps1 (8 tests)

| Coverage | Scenario Type | Status |
|-----------|--------------|--------|
| GUID/empty binding rejections (x3 / x2) | Negative | PASS |
| Existing-object idempotent short-circuit with informational message and zero writes | Idempotency | PASS |
| Creation path exact wrapper arguments (incl. MemberOfGroup filter string); default values pinned | Positive | PASS |
| `-WhatIf` → zero write invocations; unconditional direct-membership warning on all three paths (Scope only) | Dry-run / warning contract | PASS |

### Grant-OpenClawRbacRoles.Tests.ps1 (11 tests)

| Coverage | Scenario Type | Status |
|-----------|--------------|--------|
| Binding rejection; mutually-exclusive sets rejected; neither-set rejected | Negative | PASS |
| Default four roles with exact names/order; no Calendars.ReadWrite by default; +1 with the switch; custom prefix | Positive | PASS |
| ScopeName → CustomResourceScope on every call; AU id → RecipientAdministrativeUnitScope on every call (mocked wrapper level) | Parameter-set routing | PASS |
| Per-role idempotency (AlreadyExists row + 3 creates); `-WhatIf` → 0 creates, Status WhatIf rows | Idempotency / dry-run | PASS |

### Set-OpenClawSendOnBehalf.Tests.ps1 (10 tests) / Test-OpenClawScopeBoundary.Tests.ps1 (10 tests) / Invoke-OpenClawExchangeRbacSetup.Tests.ps1 (6 tests)

| Coverage | Scenario Type | Status |
|-----------|--------------|--------|
| SMTP/empty binding rejections (x4 / x3) | Negative | PASS |
| Exact FullAccess + additive Send-on-Behalf payloads; pipeline per-principal application; existing-ACE no-op vs re-throw; `-WhatIf` → 0 writes; no-Send-As (mock filter + static scan) | Contract / idempotency | PASS |
| Full boundary matrix (all four cells with exact FailureReason strings incl. joined); exactly-two-calls with correct Identity/Resource; raw-row surfacing; no-`exit` static scan | Matrix / contract | PASS |
| Entry script: sequencing order; AU skip route; per-principal loop; `-WhatIf` forwarded to all four state-changing calls; exit 0/1 mapping; boundary argument passthrough | Sequencing / exit semantics | PASS |

**Coverage:** new-code 168/169 = 99.41% line, reviewer-parsed per file (seven files 100%, Seams.ps1 59/60). **Gap:** the single uncovered command is Seams.ps1 line 112 — the `RecipientAdministrativeUnitScope` pass-through arm inside the real `Invoke-OpenClawNewManagementRoleAssignment` body; the AU route is verified only against the mocked wrapper (Grant tests) and the mocked public function (entry-script test), never through the real wrapper. Named as a Minor finding in the code review with a concrete recommendation.

**Regression:** zero existing PowerShell files modified (all 17 code files are additions); baseline 281 tests all still pass inside the reviewer's 358/358 run; executor's final `dotnet build`/`dotnet test` EXIT 0 confirm the C# solution is unaffected.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total PowerShell tests (repo, reviewer run) | 358 passed / 358 (0 failed, 0 skipped) | PASS |
| Baseline tests | 281 (delta +77 = exactly the new feature suite) | PASS |
| Execution time | ~21 s (full repo suite) | PASS |
| Repo-wide coverage | 89.66% command / 90.22% line-counter (gates 85%) | PASS |
| New-code coverage | 99.41% line / 99.53% command (168/169, 211 commands) | PASS |
| C# solution (executor final gate) | build + test EXIT 0 | PASS |
| Net new tests vs baseline | +77 across 8 mirrored test files | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| PoshQC format (authoritative settings) | `mcp__drm-copilot__run_poshqc_format` (executor, batch + full scope) | EXIT 0 all runs | PASS |
| Format idempotency (reviewer) | `Invoke-Formatter -ScriptDefinition` over all 16 script files | zero diffs | PASS |
| PoshQC analyze (authoritative settings) | `mcp__drm-copilot__run_poshqc_analyze` (executor, batch + full scope) | EXIT 0 all runs | PASS |
| PSScriptAnalyzer (reviewer) | `Invoke-ScriptAnalyzer -Path <module, tests, entry script> -Recurse` (1.24.0) | NO_DIAGNOSTICS | PASS |
| Pester tests (reviewer) | `Invoke-Pester` over `tests/scripts` + `tests/powershell` | 358 passed / 0 failed | PASS |
| Coverage re-parse (reviewer) | python parse of `artifacts/pester/powershell-coverage.xml` per file | matches executor figures exactly | PASS |
| Module surface gate (executor) | `ac1-module-surface.2026-07-02T18-38.md` — export set, CmdletBinding, ShouldProcess, validation attributes, tenant-value scan, line caps | EXIT 0 | PASS |
| Runbook conformance (executor) | `runbook-conformance.2026-07-02T18-45.md` — section order + 12 dated citations | EXIT 0; reviewer re-read confirms five sections in order | PASS |
| C# solution unaffected | `dotnet build` / `dotnet test OpenClaw.MailBridge.sln` (executor final gate) | EXIT 0 | PASS |

**Notes:** The reviewer's analyzer/formatter runs used PSScriptAnalyzer defaults because the repo-specific PoshQC settings ship inside the MCP server bundle, which is not available in this review environment; the executor's MCP runs (which do use the bundled settings) are the authoritative gate evidence and all passed. The two independent methods agree: zero findings.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** One Minor finding and three Info observations:

- **Seam-level AU-scope forwarding arm untested (Minor).** `Invoke-OpenClawNewManagementRoleAssignment`'s `RecipientAdministrativeUnitScope` branch (Seams.ps1 line 112) is the single uncovered new-code command. The seam contract case exercises only the `CustomResourceScope` route through the real wrapper body; the AU route is tested only against mocks. If the pass-through line regressed, the mocked tests would still pass. Recommendation (code review CR-1): add one seam `-ForEach` case supplying `RecipientAdministrativeUnitScope`. Non-blocking: the arm is symmetric with the covered arm and the thresholds pass with margin.
- **Pester v5 emits no branch-coverage percentage for PowerShell (Info).** The 75% branch gate was graded on the command-coverage proxy (commands in untaken branch arms register as uncovered), consistent with the #58/#62 precedent the executor cited. This is a toolchain limitation, not a measurement waiver; the proxy value is 99.53% and every conditional arm except the one named above is exercised.
- **`run_poshqc_test` bundled-settings defect (Info, pre-existing workspace defect).** The MCP tool's bundled `pester.runsettings.psd1` hardcodes drm-copilot coverage paths and fails at Pester RunStart in this repository (exit 4294967295 on every attempt, diagnosed in the baseline artifact). Executor fallback: direct `Invoke-PoshQCTest` (the same bundled pipeline) with repo-scoped coverage settings. Reviewer verified the fallback's outputs independently. Follow-up belongs to the tooling workspace, not this repo's code.
- **PowerShell script tree not classified in `quality-tiers.yml` (Info, pre-existing).** The tier map is scoped to the C# solution's projects. The new module is administrator ops tooling (T4-analog under the tier definitions); the uniform coverage gates were applied regardless and pass. No tier-dependent gate (property density, mutation) attaches. Pre-existing repo-wide condition, not a finding against this branch.

### Approved Exceptions

- **MCP template/validator tools unavailable:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set (issue #109 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance; same accommodation accepted on the #80, #19, #18, and #99-#109 reviews.
- **PoshQC bundled settings not directly runnable by the reviewer:** repo PSScriptAnalyzer settings live in the MCP server bundle. The reviewer used analyzer/formatter defaults as an independent second signal and relied on the executor's MCP EXIT-0 evidence for the authoritative-settings gate (two methods, both clean).
- **GitHub CLI unavailable:** `gh` is not installed, so autoclose verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None removed.** No existing test file was modified, deleted, or weakened (the branch adds files only). Reviewer run had zero skips.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `8d38981`)

Branch `feature/exchange-rbac-scripts-111`, head `0c1104a85b4b520f17a1eaab7cbb8006eb2b14aa` (single commit). Range: `8d389819d50174be9610ae69c1c4b5c9da05f829..0c1104a85b4b520f17a1eaab7cbb8006eb2b14aa` (50 files, +3201/-3).

### Files Modified (categories)

1. **`scripts/powershell/modules/OpenClawRbac/`** (NEW, 8 files) — manifest, dot-sourcing root module, nine wrapper seams (`OpenClawRbac.Seams.ps1`), and five public-function files implementing master §12 Steps 2-5 and 7 with check-before-create idempotency, ShouldProcess gates, and runtime cmdlet resolution.
2. **`scripts/Invoke-OpenClawExchangeRbacSetup.ps1`** (NEW) — thin entry script: import by relative path, Register → Scope (skipped on the AU parameter set) → Grant → SendOnBehalf (per principal) → Boundary, `-WhatIf` forwarding, exit 0/1 mapping.
3. **`tests/scripts/powershell/modules/OpenClawRbac/` + `tests/scripts/Invoke-OpenClawExchangeRbacSetup.Tests.ps1`** (NEW, 8 files, 77 tests) — fully mocked at the wrapper seams; module-surface, seam-contract, per-function behavior, boundary matrix, and entry-script sequencing suites.
4. **`docs/features/active/2026-07-02-exchange-rbac-scripts-111/`** (NEW, 30 files) — issue/spec/user-story/plan, the human-exception runbook (five required sections, 12 dated Microsoft Learn citations), and canonical evidence (`baseline/`, `qa-gates/`, `other/`).
5. **`.claude/agent-memory/`** (3 Markdown files) — orchestrator status update and one prd-feature memory record + index line (harness metadata, not code).

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The PowerShell change passes formatting, linting, and the full mocked Pester suite, independently re-verified by the reviewer at branch head; the uniform coverage gates pass with new-code line coverage at 99.41% and no possibility of changed-line regression (additions only). The module honors every PowerShell-specific policy: advanced functions with validation, ShouldProcess on all state changes, the wrapper-seam pattern with runtime resolution and wrapper-only mocking, no parse-time external dependency, no temp files, all files under the 500-line cap, and the per-batch change budget respected. The human-exception runbook is contract-conformant (five sections in order, dated citations, canonical path) and matches the orchestrator-state HI-1 record. The `modified-workflow-needs-green-run` rule does not fire (verified: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered; the `orchestrator-state` human-interaction invariants were read-verified and hold.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (seam pattern, single choke point, sequencing-only entry script)
- Module & File Structure: PASS (all files under 500 lines, max 215)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (executor MCP gates EXIT 0; reviewer independent pass clean)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — PowerShell
- Toolchain & Compatibility: PASS (with the documented `run_poshqc_test` workspace-defect accommodation)
- Function Design & Validation: PASS
- Error Handling: PASS (specific throws; one targeted, re-throwing catch)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (89.66%/99.41%; command proxy for branch; one named uncovered arm, Minor)
- Test Structure: PASS
- External Dependencies: PASS (wrapper-only mocking, no temp files, no tenant contact)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — PowerShell
- Framework & Location: PASS (Pester v5; mirrored `tests/scripts/` layout)
- Mocking Rules: PASS (seams only; signature parity; fake-command injection for real wrapper bodies)
- Determinism: PASS

---

### Metrics Summary

- 358/358 repo PowerShell tests passing (reviewer re-run; baseline 281 + 77 new)
- Repo-wide coverage 89.66% command / 90.22% line-counter (gate 85%); +1.19 pp vs baseline
- New-code coverage 168/169 = 99.41% line, 99.53% command (branch-sensitive proxy; gate 75%)
- Zero analyzer diagnostics (two independent methods); zero format diffs
- All 17 code files under the 500-line cap (max 215)
- Zero existing files modified in `scripts/` or `tests/` (additions only)

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, and policy requirements pass against branch head `0c1104a`, independently re-verified. One Minor, non-blocking finding (seam-level AU-scope forwarding case) is recorded in the code review as an optional hardening. No remediation inputs are required. Operational note (from spec, not a gate): the module is inert until a human administrator executes it in a connected session per the runbook; AC-4's orchestrator-owned verification is recorded in orchestrator state and was read-verified by this review.

---

## Appendix A: Test Inventory

PowerShell test changes in this feature (all NEW):

1. `tests/scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Module.Tests.ps1` (70 lines, 3 tests) — manifest import without ExchangeOnlineManagement, exact export-set match in both directions, static scan for forbidden parse-time dependency markers.
2. `tests/scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Seams.Tests.ps1` (213 lines, 21 tests) — data-driven contract over all nine wrappers: missing-cmdlet error (names cmdlet + module + Connect-ExchangeOnline), exact argument forwarding through real wrapper bodies via injected fake commands with exact-count assertions (incl. the Get-* `ErrorAction SilentlyContinue` contract), `$null` on not-found for the three Get-* wrappers.
3. `tests/scripts/powershell/modules/OpenClawRbac/Register-OpenClawServicePrincipal.Tests.ps1` (171 lines, 8 tests) — binding rejections, existing-SP no-op, AppId lookup, exact creation arguments, DisplayName default, WhatIf dry-run.
4. `tests/scripts/powershell/modules/OpenClawRbac/New-OpenClawMailboxScope.Tests.ps1` (172 lines, 8 tests) — binding rejections, existing-scope no-op, exact Name + MemberOfGroup filter arguments, name default, unconditional direct-membership warning on create/no-op/WhatIf paths, WhatIf dry-run.
5. `tests/scripts/powershell/modules/OpenClawRbac/Grant-OpenClawRbacRoles.Tests.ps1` (212 lines, 11 tests) — binding and parameter-set rejections, exact four-role default set with names/order, calendar-write switch, custom prefix, scope vs AU routing, per-role idempotency, WhatIf rows.
6. `tests/scripts/powershell/modules/OpenClawRbac/Set-OpenClawSendOnBehalf.Tests.ps1` (155 lines, 10 tests) — SMTP binding rejections, exact FullAccess/AutoMapping-off and additive `@{Add=}` payloads, pipeline per-principal, existing-ACE no-op vs re-throw, WhatIf dry-run, no-Send-As (behavioral + static scan).
7. `tests/scripts/powershell/modules/OpenClawRbac/Test-OpenClawScopeBoundary.Tests.ps1` (215 lines, 10 tests) — binding rejections, all four boundary-matrix cells with exact FailureReason strings, exactly-two-calls contract with Identity/Resource verification, raw-row surfacing, no-`exit` static scan.
8. `tests/scripts/Invoke-OpenClawExchangeRbacSetup.Tests.ps1` (133 lines, 6 tests) — sequencing order, AU skip route, per-principal loop, WhatIf forwarding to all four state-changing calls, exit 0/1 mapping, boundary argument passthrough.

Reviewer run: 358 passed / 0 failed / 0 skipped (full repo suite, ~21 s).

---

## Appendix B: Toolchain Commands Reference (PowerShell)

```powershell
# Formatting (executor, authoritative bundled settings)
mcp__drm-copilot__run_poshqc_format   # batch scopes + full repo, EXIT 0

# Formatting idempotency (reviewer, defaults)
Invoke-Formatter -ScriptDefinition (Get-Content <file> -Raw)   # 16 files, zero diffs

# Linting (executor, authoritative bundled settings)
mcp__drm-copilot__run_poshqc_analyze  # batch scopes + full repo, EXIT 0

# Linting (reviewer, PSScriptAnalyzer 1.24.0 defaults)
Invoke-ScriptAnalyzer -Path scripts/powershell/modules/OpenClawRbac -Recurse
Invoke-ScriptAnalyzer -Path tests/scripts/powershell/modules/OpenClawRbac -Recurse
Invoke-ScriptAnalyzer -Path scripts/Invoke-OpenClawExchangeRbacSetup.ps1
Invoke-ScriptAnalyzer -Path tests/scripts/Invoke-OpenClawExchangeRbacSetup.Tests.ps1

# Tests (executor: bundled Invoke-PoshQCTest pipeline with repo-scoped coverage settings
# after run_poshqc_test failed on the pre-existing bundled-settings defect)
Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.final.runsettings.psd1

# Tests (reviewer, check-only, no coverage regeneration)
$config = New-PesterConfiguration
$config.Run.Path = @('tests/scripts','tests/powershell')
Invoke-Pester -Configuration $config   # 358 passed / 0 failed

# Coverage re-parse (reviewer)
python <parse artifacts/pester/powershell-coverage.xml per-file LINE counters>

# C# solution unaffected (executor final gate)
dotnet build OpenClaw.MailBridge.sln
dotnet test OpenClaw.MailBridge.sln

# Evidence-location scan
git diff --name-only 8d389819d50174be9610ae69c1c4b5c9da05f829..HEAD | grep -E '^artifacts/'

# Workflow-rule trigger scan (modified-workflow-needs-green-run)
git diff --name-only 8d389819d50174be9610ae69c1c4b5c9da05f829..HEAD | grep -E '^(\.github/workflows/|scripts/benchmarks/|\.github/actions/)'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-02
**Policy Version:** Current (as of audit date)
