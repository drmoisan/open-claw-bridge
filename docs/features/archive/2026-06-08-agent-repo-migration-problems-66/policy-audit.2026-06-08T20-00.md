# Policy Compliance Audit: Issue #66 — Agent Harness Migration Correction

**Audit Date:** 2026-06-08
**Code Under Test:** Documentation/policy/configuration change to the agent harness. Branch diff (merge-base `72d11879918bab20652abf2965eea42f17ab67d1` -> head `3ed46efaa28c43fefc946413bb3ba64866ca8d29`) comprises 198 changed paths: 179 Markdown, 15 PowerShell (`.ps1`), 2 JSON, 1 YAML (`quality-tiers.yml`), and `.gitignore`. No product source (`src/`), test source (`tests/`), build config (`*.csproj`, `*.sln`, `global.json`, `mailbridge.runsettings`), CI workflow (`.github/workflows/**`), benchmark (`scripts/benchmarks/**`), or action (`.github/actions/**`) file is in the diff.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| PowerShell | 15 `.ps1` (additions; harness hooks newly tracked) | No harness Pester tests for `.claude/hooks/` | ❌ no hook test suite present | N/A (untracked before this branch) | 0% lines (`artifacts/pester/powershell-coverage.xml`, 284 missed / 0 covered) | **FAIL** — 0% on 5 measured hooks; 10 hooks have no coverage entry |
| C# | 0 files | n/a | n/a | n/a | n/a | N/A - zero changed files in branch diff |
| TypeScript | 0 files | n/a | n/a | n/a | n/a | N/A - zero changed files in branch diff |
| Python | 0 files | n/a | n/a | n/a | n/a | N/A - zero changed files in branch diff |

**Note:** C#, TypeScript, and Python carry the `N/A` verdict legitimately because each has zero changed files in the branch diff. PowerShell has 15 changed `.ps1` files in the branch diff and therefore carries an explicit `FAIL` verdict, not `N/A`.

### Coverage Evidence Checklist

- TypeScript baseline coverage artifact: `N/A - zero changed TypeScript files in branch diff`
- TypeScript post-change coverage artifact: `N/A - zero changed TypeScript files in branch diff`
- PowerShell baseline coverage artifact: `N/A - hooks untracked before this branch; no committed baseline`
- PowerShell post-change coverage artifact: `artifacts/pester/powershell-coverage.xml` (JaCoCo, dated 06/06/2026; report total LINE missed=284 covered=0)
- Per-language comparison summary: Section 1.2.1 below

**Verdict rule observed:** PowerShell is in scope (changed files present) and its coverage verdict is FAIL, so this audit does NOT report an overall PASS. C#/TS/Python are out of scope (zero changed files) and their N/A verdicts are permitted under the Scope Invariant.

---

## Executive Summary

Issue #66 corrects the agent harness (`.claude/`, `.github/{agents,instructions,prompts,skills}`, `AGENTS.md`) that was copied from the "drm-copilot" / "TaskMaster" repository without per-file adaptation, and brings the previously-gitignored harness under version control (Scope Extension, Option 1A). The change is documentation/policy/configuration only; no product runtime, test source, build config, or CI workflow is touched. A `dotnet build` / `dotnet test` command-validity smoke was recorded by the executor (`evidence/qa-gates/ac10-command-smoke.md`).

The substantive corrections (residual marker removal, tool-name reconciliation to MSTest/Moq/FluentAssertions, COM-confinement, path qualification, `quality-tiers.yml` / `docs/ci.research.md` creation, Python/TypeScript ecosystem removal) are verified against the branch diff and the live tree. All 15 acceptance criteria (AC-01..AC-15) verify PASS on independent re-scan.

One blocking finding stands: PowerShell has 15 changed `.ps1` files in the branch diff (the `.claude/hooks/*` scripts that became tracked when `.gitignore` was edited to un-ignore `.claude/`), and the only PowerShell coverage artifact present reports 0% line coverage on the 5 hooks it measures, with no coverage data at all for the other 10 changed hooks. Under the SKILL coverage-verification mandate and the Scope Invariant, a language with changed files in the branch diff must carry an explicit PASS or FAIL verdict and cannot be excused as out-of-scope. The verdict is FAIL and a remediation trigger.

**Policy documents evaluated:**
- ✅ `.claude/rules/general-code-change.md` (cross-language code change policy)
- ✅ `.claude/rules/general-unit-test.md` (cross-language unit test policy)
- ✅ `.claude/rules/quality-tiers.md` (tier system, uniform 85%/75% coverage)
- ✅ `.claude/rules/tonality.md` (authored-content tone)
- ✅ `.claude/rules/powershell.md` (PowerShell-specific)

**Language-specific policies evaluated:**
- N/A `python-*` policy — zero Python files changed; Python rules deleted by this change
- ✅ PowerShell rules — 15 `.ps1` files changed (harness hooks newly tracked)
- N/A C# code/test policy — zero C# source/test files changed

**Temporary artifacts cleanup:**
- ✅ No temporary scripts were created by this review.
- N/A No new ongoing tooling scripts authored by this review.

---

## Rejected Scope Narrowing

The caller prompt included the following context, quoted verbatim:

> "This change is a documentation/policy/configuration change to the agent harness. ... There is no C#/PowerShell/product source change in the diff; the only compiler interaction performed was a `dotnet build`/`dotnet test` command-validity smoke (recorded in evidence)."

> "Because the harness is newly version-controlled, the diff presents most harness files as additions. Many predate this change and are pre-existing infrastructure."

Justification for rejection: the assertion "There is no ... PowerShell ... source change in the diff" is contradicted by the branch diff, which contains 15 `.ps1` files under `.claude/hooks/` as additions. Under the SKILL Scope Invariant, a language with changed files in the branch diff (regardless of whether the files predate the change or entered the diff via version-control inclusion) must receive an explicit PASS or FAIL coverage verdict; "not applicable" / "out of scope" is not acceptable. The caller framing is treated as context only and does not narrow the audit. The full feature-vs-base audit proceeds, and PowerShell coverage is evaluated as FAIL on the available evidence. This rejection does not dispute that the change is non-runtime; it only refuses to drop PowerShell from the mandatory coverage verdict.

---

## Evidence Location Compliance

The branch diff was scanned for files written under non-canonical evidence paths (`artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, `artifacts/coverage/`).

- `git diff --name-only <merge-base> <head> -- 'artifacts/baselines/**' 'artifacts/qa/**' 'artifacts/evidence/**' 'artifacts/coverage/**'` returns zero paths.
- All 29 evidence files introduced by this branch are under the canonical `docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/<kind>/` location.
- The working tree contains untracked `artifacts/coverage/` and `artifacts/evidence/` directories, but `git check-ignore` confirms `artifacts/` is gitignored and none of those files appear in the branch diff. They are not introduced or modified by this branch.

Verdict: **PASS** — no Evidence Location violations introduced by this branch.

The `validate_evidence_locations.py --root .` script is absent from this repository (`scripts/validate_evidence_locations.py` does not exist); the enforcement hook `.claude/hooks/enforce-evidence-locations.ps1` is present. The diff-based scan above is the substituted verification and found no violations.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| Independence | N/A PASS | No product/test source changed; no new unit tests authored by this change. The change is documentation/policy/config. |
| Isolation | N/A | Same as above. |
| Fast execution | N/A | Same as above. |
| Determinism | N/A | Same as above. |
| Readability & maintainability | N/A | Same as above. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| Baseline coverage documented | ❌ FAIL | PowerShell `.ps1` files are in the branch diff but had no committed baseline (untracked before this branch). The post-change artifact `artifacts/pester/powershell-coverage.xml` reports 0% line coverage on the 5 hooks it measures. |
| No coverage regression | N/A | No prior committed PowerShell coverage baseline exists to regress against. |
| New code coverage >= 85% (uniform tier rule) | ❌ FAIL | The 15 changed `.ps1` hook files are new to version control. Measured coverage is 0% on 5 of them; the remaining 10 have no coverage entry. Threshold is line >= 85% / branch >= 75%. |
| Comprehensive coverage | ❌ FAIL | No Pester test suite exists for `.claude/hooks/`; the hooks are executed by Claude Code at runtime, not by an in-repo test harness. |

### 1.2.1 Per-Language Coverage Comparison

- PowerShell: Baseline: 0% lines (no committed baseline; hooks untracked before this branch). Post-change: 0% lines (`artifacts/pester/powershell-coverage.xml`, report total LINE missed=284 covered=0). Change: 0% (0 covered lines before and after; no improvement). New/changed-code coverage: 0% on the 5 measured hooks, with 10 of 15 changed hooks absent from the artifact entirely. Disposition: FAIL. Evidence: `artifacts/pester/powershell-coverage.xml`.
- C#: N/A - zero changed files in branch diff. Disposition: N/A.
- TypeScript: N/A - zero changed files in branch diff. Disposition: N/A.
- Python: N/A - zero changed files in branch diff (Python ecosystem removed by this change). Disposition: N/A.

### 1.3–1.5

| Requirement | Status | Evidence |
|------------|--------|----------|
| Clear failure messages | N/A | No new tests authored. |
| Arrange-Act-Assert | N/A | No new tests authored. |
| External dependencies avoided | N/A | No new tests authored. |
| Pre-submission review | ✅ PASS | This audit plus the executor's `evidence/qa-gates/*` artifacts constitute the review. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| Clarify objective | ✅ PASS | Objective defined in `spec.md` and `issue.md` (Issue #66); evidence-backed inventory in `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`. |
| Read existing change plans | ✅ PASS | Two plan files present: `plan.2026-06-08T09-15.md`, `plan.2026-06-08T11-00.md`. |
| Document the plan | ✅ PASS | Per-area Scope checklist in `spec.md`, each item citing a research section. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| Simplicity first | ✅ PASS | Corrections are minimal textual edits, deletions of residue, and three new supporting files. No indirection added. |
| Reusability | N/A | Documentation change. |
| Extensibility | N/A | Documentation change. |
| Separation of concerns | ✅ PASS | `.claude/rules/*` established as single source of truth; `.github/instructions/*` and `AGENTS.md` reconciled to it (spec "Single Source of Truth"). |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| Cohesive modules | ✅ PASS | Files grouped by harness area (rules, agents, skills, instructions). |
| Under 500 lines | ✅ PASS | `evidence/other/p5t7-line-counts.md` records no non-exempt file exceeds 500 lines; `AGENTS.md` (1053 lines) is a Markdown documentation file, exempt per `general-code-change.md`. Verified the only non-Markdown authored files: `quality-tiers.yml` (28 lines), `.claude/settings.json` (valid JSON). |
| Public vs internal | N/A | Documentation change. |
| No circular dependencies | ✅ PASS | `.claude/rules/architecture-boundaries.md` describes the acyclic project-reference graph; no harness cross-reference cycle introduced. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| Descriptive names | ✅ PASS | New files (`quality-tiers.yml`, `docs/ci.research.md`, orchestrator memory file) are descriptively named. |
| Docs/docstrings | ✅ PASS | `docs/ci.research.md` documents the tier system; `quality-tiers.yml` carries a header comment block. |
| Comment why, not what | ✅ PASS | `quality-tiers.yml` header explains tier source-of-truth and uniform thresholds. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| 1. Formatting | N/A PASS | No source code changed; Markdown/YAML/JSON are not subject to the C#/PowerShell formatters. JSON validity confirmed: `Get-Content .claude/settings.json -Raw | ConvertFrom-Json` succeeds. |
| 2. Linting | N/A | No source code changed. |
| 3. Type checking | N/A | No source code changed. |
| 4. Testing | ⚠️ PARTIAL | A `dotnet build` / `dotnet test` command-validity smoke ran (298 passed, 0 failed, 3 skipped; `evidence/qa-gates/ac10-command-smoke.md`). This validates the corrected command strings; it is not a coverage gate for this change. No PowerShell hook test suite exists. |
| Full toolchain loop | N/A | No source-code toolchain loop applies to a documentation/config change. |
| Explicit reporting | ✅ PASS | Commands and results recorded in `evidence/qa-gates/*`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| Summarize changes | ✅ PASS | `evidence/other/closeout-summary.md` and `closeout-summary-extension.md`. |
| Design choices explained | ✅ PASS | Spec "Single Source of Truth" and "Execution Deviations" sections. |
| Update supporting documents | ✅ PASS | `AGENTS.md`, `.github/instructions/*`, `.claude/rules/*` reconciled in the same change. |
| Provide next steps | ✅ PASS | Spec "Out of scope / follow-ups" lists the generator script, benchmark validator, and `.config/dotnet-tools.json` follow-ups. |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3B: PowerShell Code Change Policy Compliance

The 15 changed `.ps1` files are pre-existing harness hooks that entered version control via the `.gitignore` edit. The spec explicitly states harness hook files are not modified; their content is unchanged. They are nonetheless in the branch diff.

| Requirement | Status | Evidence |
|------------|--------|----------|
| Formatting (Invoke-Formatter) | UNVERIFIED | The hooks were not re-formatted by this change (content unchanged). No PoshQC format run was performed against them in this review. |
| Linting (PSScriptAnalyzer) | UNVERIFIED | No PSScriptAnalyzer run was performed against the hooks in this review; the change did not modify their content. |
| Cohesive and under 500 lines | ✅ PASS | The largest changed hook is `validate-feature-review-coverage.ps1` (459 lines per the PR-context overview); all are under 500. |
| Error handling | UNVERIFIED | Hook content unchanged by this branch; not re-audited line-by-line. |

Note: because the hooks' content is unchanged by this branch, the substantive PowerShell code-change gates are not re-triggered by an authored edit. The coverage gate, however, is triggered by the file's presence in the branch diff and is evaluated as FAIL in Section 1.2.

---

## 4. Language-Specific Unit Test Policy Compliance

### Section 4B: PowerShell Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| Use Pester v5.x | N/A | No hook test suite exists. |
| PowerShell coverage | ❌ FAIL | `artifacts/pester/powershell-coverage.xml` shows 0% line coverage on the measured `.claude/hooks/*.ps1` files; 10 of 15 changed hooks are absent from the artifact entirely. |

---

## 5. Test Coverage Detail

This change adds no product code and no new unit tests; there is no per-function test coverage table to populate. The only coverage data in scope is the PowerShell hook coverage discussed in Sections 1.2 and 1.2.1.

| Component | Tests | Lines Covered | Status |
|-----------|-------|---------------|--------|
| `.claude/hooks/*.ps1` (5 measured in artifact) | 0 (no harness Pester suite) | 0 / 284 (0%) | ❌ |
| `.claude/hooks/*.ps1` (10 of 15 changed, unmeasured) | n/a | absent from artifact | ❌ |

**Not covered:** All measured hook lines (0% line coverage). Remediation required.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (this change) | 0 new tests | N/A |
| C# smoke tests run (command-validity) | 298 passed, 0 failed, 3 skipped | ✅ |
| PowerShell hook tests | 0 (no suite) | ❌ |
| Code Coverage (PowerShell hooks) | 0% lines, branch n/a | ❌ |

The 298-test figure is the `dotnet test` command-validity smoke (`evidence/qa-gates/ac10-command-smoke.md`); it is a supporting signal for AC-10, not a coverage gate for this documentation change.

---

## 7. Code Quality Checks

**For PowerShell:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Invoke-Formatter | not run (hook content unchanged) | UNVERIFIED | ⚠️ |
| PSScriptAnalyzer | not run (hook content unchanged) | UNVERIFIED | ⚠️ |
| Pester coverage (artifact) | `artifacts/pester/powershell-coverage.xml` | 0% line coverage on measured hooks | ❌ |

**Notes:** The hooks' content is unchanged by this branch; the format/lint gates are not re-triggered by an authored edit. The coverage gate is triggered by the files' presence in the branch diff and fails.

---

## 5A. Acceptance-Criteria Policy Verification Summary

All 15 ACs independently re-verified during this review (full per-AC evaluation is in `feature-audit.2026-06-08T20-00.md`). Summary of re-scan commands and results:

| AC | Verdict | Re-scan evidence |
|----|---------|------------------|
| AC-01 | PASS | `rg` marker scan over `.claude .github AGENTS.md` — all hits are explicit prohibitions, qualified not-present statements, or agent-memory provenance. |
| AC-02 | PASS | `mailbridge.runsettings`, `quality-tiers.yml`, `docs/ci.research.md` exist; `pester.runsettings.psd1` and `Test-BaselineProvenance.ps1` qualified not-present in referencing docs. |
| AC-03 | PASS | `quality-tiers.yml` lists all 9 solution projects with valid tiers, no extras. |
| AC-04 | PASS | No stray 80%/90% coverage gates in `.claude/rules` or `AGENTS.md`; MSTest/Moq/FluentAssertions present; no xUnit/NSubstitute as the named framework. |
| AC-05 | PASS | All 18 removed `.claude/` + `.github/agents/*` files absent; no removed-worker delegation remains. |
| AC-06 | PASS | No `msbuild TaskMaster` / `vstest.console` in `AGENTS.md` or `.github/instructions`; `dotnet build`/`dotnet test`/`OpenClaw.MailBridge.sln` present in `AGENTS.md`. |
| AC-07 | PASS | `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` exists; cited at `orchestrator.md:155`. |
| AC-08 | PASS | `docs/ci.research.md` exists; `quality-tiers.md` cites it as tier source of truth. |
| AC-09 | PASS | No `dotnet csharpier check` / `dotnet tool run csharpier` / `Directory.Build.props` analyzer-config claim in the three named files; global `csharpier` form present. |
| AC-10 | PASS | `dotnet build` / `dotnet test` smoke valid; solution and runsettings paths resolve (`evidence/qa-gates/ac10-command-smoke.md`). |
| AC-11 | PASS | `.gitignore` un-ignores `.claude/` and `.github/{agents,instructions,prompts,skills}`; `artifacts/` still ignored. |
| AC-12 | PASS | Full-tree marker scan: every hit in the permitted-exception set. |
| AC-13 | PASS | Dangling-reference scan returns no active reference outside agent-memory provenance. |
| AC-14 | PASS | `.claude/settings.json` valid JSON, no Python residue; all 11 remaining hook command paths resolve. |
| AC-15 | PASS | All 7 extension deletion targets absent. |

---

## 8. Gaps and Exceptions

### Identified Gaps

- **PowerShell coverage (blocking):** 15 `.ps1` harness hook files are in the branch diff; the available coverage artifact reports 0% line coverage and omits 10 of the 15 changed files. This fails the uniform tier rule (line >= 85%) and the repo-wide < 80% FAIL trigger. Remediation is required. See `remediation-inputs.2026-06-08T20-00.md`.

### Approved Exceptions

- **None.** The non-runtime nature of the change does not exempt PowerShell from the mandatory coverage verdict per the Scope Invariant.

### Removed/Skipped Tests

- **None.** No tests were removed or skipped by this change.

---

## 9. Summary of Changes

### Commits in This Branch

- `3ed46ef` — `fix(harness): adapt migrated agent harness to this repo and track it`

### Files Modified (categories)

- **MODIFIED:** `.gitignore` (un-ignore harness; ignore `.claude/settings.local.json`), `AGENTS.md` (command-string and threshold corrections).
- **NEW (tracked):** `quality-tiers.yml`, `docs/ci.research.md`, `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`, and the full `.claude/` + `.github/{agents,instructions,prompts,skills}` harness now under version control.
- **DELETED:** TypeScript/Python residue rules, agents, skills, prompts, and hooks (per AC-05 and AC-15 lists).

---

## 10. Compliance Verdict

### Overall Status: ⚠️ PARTIALLY COMPLIANT

The substantive harness correction is delivered and all 15 acceptance criteria verify PASS. One blocking gap remains: PowerShell coverage is FAIL because 15 `.ps1` files are in the branch diff and the available coverage artifact reports 0% line coverage with 10 changed files unmeasured. This prevents an overall PASS.

**Fail-closed note:** A required coverage threshold is not met for a language with changed files; the audit is not marked PASS or ready for merge.

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Before Making Changes
- ✅ Design Principles
- ✅ Module & File Structure (500-line limit satisfied)
- ✅ Naming, Docs, Comments
- ⚠️ Toolchain Execution — command-validity smoke only; PowerShell hook tests absent
- ✅ Summarize & Document

#### Language-Specific Code Change Policy (Section 3)
- ⚠️ PowerShell — hook content unchanged; format/lint UNVERIFIED, coverage FAIL

#### General Unit Test Policy (Section 1)
- ❌ Coverage & Scenarios — PowerShell coverage FAIL
- N/A Core Principles / Test Structure / External Dependencies (no new tests)

#### Language-Specific Unit Test Policy (Section 4)
- ❌ PowerShell — 0% measured hook coverage

### Metrics Summary

- ✅ 15/15 acceptance criteria PASS
- ❌ PowerShell line coverage 0% on measured hooks (threshold 85%); repo-wide < 80% FAIL trigger met
- ✅ All referenced filesystem paths exist or are qualified not-present
- ✅ No Evidence Location violations introduced by this branch
- ✅ No product/test/build/CI/workflow/benchmark file modified

### Recommendation

**Needs revision (blocked on one item).** The harness correction itself is complete and correct. The single blocking item is the PowerShell coverage FAIL on the 15 `.ps1` hook files newly brought into version control. Remediation handoff is created at `remediation-inputs.2026-06-08T20-00.md`.

---

## Appendix A: Test Inventory

No new tests were authored by this change. The complete test inventory for this review is empty:

- No new unit tests (documentation/policy/config change).
- The `dotnet test` smoke (`evidence/qa-gates/ac10-command-smoke.md`) exercised the pre-existing C# test projects (298 passed, 0 failed, 3 skipped) solely to confirm the corrected command strings resolve; those tests are not authored by this change.
- No Pester test suite exists for `.claude/hooks/`.

---

## Appendix B: Toolchain Commands Reference

Commands executed during this review (check-only; no mutation):

```bash
# Branch scope
git diff --name-status 72d11879918bab20652abf2965eea42f17ab67d1 3ed46efaa28c43fefc946413bb3ba64866ca8d29
git diff --name-only <merge-base> <head> -- 'src/' 'tests/' '*.csproj' '*.sln' 'global.json' '.github/workflows/'

# AC-01 / AC-12 residual marker scan
rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md

# AC-13 dangling-reference scan
rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md" .claude .github

# AC-06 command scan
rg -n "msbuild TaskMaster|vstest\.console" AGENTS.md .github/instructions

# AC-09 csharpier prohibited forms
rg -n "dotnet csharpier check|dotnet tool run csharpier|Directory\.Build\.props" .claude/skills/csharp-qa-gate/SKILL.md .claude/skills/invoke-csharp-engineer/SKILL.md .github/instructions/csharp-code-change.instructions.md

# AC-14 settings.json validity
pwsh -NoProfile -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"

# Evidence-location diff scan
git diff --name-only <merge-base> <head> -- 'artifacts/baselines/**' 'artifacts/qa/**' 'artifacts/evidence/**' 'artifacts/coverage/**'

# PowerShell coverage (artifact inspection, not re-run)
# artifacts/pester/powershell-coverage.xml report total: LINE missed=284 covered=0
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-06-08
**Policy Version:** Current (as of audit date)
