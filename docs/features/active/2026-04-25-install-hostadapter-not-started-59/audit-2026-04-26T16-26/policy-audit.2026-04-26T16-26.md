# Policy Compliance Audit: install-hostadapter-not-started-59

**Audit Date:** 2026-04-26  
**Code Under Test:** `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1`, `tests/scripts/Install.HostAdapterStart.Tests.ps1`

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| PowerShell | 5 files | 189 tests | ✅ 189 pass, 0 fail | 95.98% overall, 95.26% Install-layer | 95.98% overall, 95.26% Install-layer | 95.26% Install-layer |

### Coverage Evidence Checklist

- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-coverage.2026-04-26T15-51.md`
- PowerShell post-change coverage artifact: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/coverage-delta.2026-04-26T15-51.md`
- Per-language comparison summary: `## 1.2.1 Per-Language Coverage Comparison`

## Executive Summary

This reduced post-remediation audit reviewed `bug/install-hostadapter-not-started-59` relative to `development`, refreshed canonical PR-context artifacts against the supplied base branch, and rechecked the approved PowerShell quality gates for the reviewed scope. The resolved comparison baseline remains `development` with merge base `226516a7989f93893dca85186d12f09ba175de0f` at `2026-04-26T14:27:54-05:00`. The refreshed audit confirms that REM-01 is closed at `73d8fc5f038632b25b7c78d33345ecfafa90afc0`, all acceptance criteria remain delivered, and the prior stale review set at `2026-04-26T02-16` should remain superseded.

The audit remains fully compliant for the requested closure scope. REM-02 and REM-03 are still out of scope and do not block closure because the refreshed PR-context and QA evidence do not show them reopening REM-01 or invalidating acceptance delivery.

**Policy documents evaluated:**
- [✅] `general-code-change.instructions.md`
- [✅] `general-unit-test.instructions.md`

**Language-specific policies evaluated:**
- [N/A] `python-code-change.instructions.md` + `python-unit-test.instructions.md`
- [✅] `powershell-code-change.instructions.md` + `powershell-unit-test.instructions.md`
- [N/A] Bash: shfmt + shellcheck + bats
- [N/A] JSON: format_json + validate_json

PowerShell coverage remains 95.98% overall and 95.26% for the Install-layer target. The reviewer refreshed `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` against `development`, and reran `mcp_drmcopilotext2_run_poshqc_format`, `mcp_drmcopilotext2_run_poshqc_analyze`, and `mcp_drmcopilotext2_run_poshqc_test` for `scripts` and `tests/scripts`; all returned `ok: true`. Numeric test-count and coverage details remain anchored in the feature evidence set created at `2026-04-26T15-51` and `2026-04-26T20-19`.

**Temporary artifacts cleanup:**
- [✅] All temporary or one-time scripts created during development have been deleted
- [✅] Any ongoing tooling scripts are fully tested and compliant with repo policies
- No temporary scripts were created during this re-audit.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** - Tests run in any order | [✅] [PASS] | `artifacts/pester/pester-junit.xml` and `evidence/qa-gates/final-test.2026-04-26T15-51.md` record a clean 189-test pass with the split suites executing together. |
| **Isolation** - Each test targets single behavior | [✅] [PASS] | `Install.Tests.ps1`, `Install.Force.Tests.ps1`, and `Install.HostAdapterStart.Tests.ps1` isolate the main install flow, force-reinstall flow, and HostAdapter-start behavior. |
| **Fast Execution** - Tests complete quickly | [✅] [PASS] | `artifacts/pester/pester-junit.xml` reports 189 tests in 16.207 seconds. |
| **Determinism** - Consistent results | [✅] [PASS] | The reviewed Pester suites use mock-based boundaries and the same branch head validated in `evidence/other/post-remediation-validation.2026-04-26T15-51.md`. |
| **Readability & Maintainability** - Clear structure | [✅] [PASS] | The split test files are behavior-specific and the main test file remains below the 500-line policy limit. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | [✅] [PASS] | `evidence/baseline/baseline-coverage.2026-04-26T15-51.md` records 95.98% overall and 95.26% Install-layer baseline coverage. |
| **No Coverage Regression** | [✅] [PASS] | `evidence/qa-gates/coverage-delta.2026-04-26T15-51.md` records a 0.00-point delta overall and for the Install-layer target. |
| **New Code Coverage ≥90%** | [✅] [PASS] | Install-layer changed/new-code coverage remains 95.26%. |
| **Comprehensive Coverage** | [✅] [PASS] | Final evidence covers launch, already-running skip, missing-executable error, Docker guards, and force-reinstall transitions. |
| **Positive Flows** - Valid inputs | [✅] [PASS] | Positive install and HostAdapter-launch scenarios are part of the final Pester pass. |
| **Negative Flows** - Invalid inputs | [✅] [PASS] | Missing executable and invalid prior-install states are covered by the reviewed suites. |
| **Edge Cases** - Boundary conditions | [✅] [PASS] | Already-running HostAdapter and `-SkipDocker` guarded paths remain covered. |
| **Error Handling** - Error paths | [✅] [PASS] | The missing-executable and preflight error paths remain covered and validated. |
| **Concurrency** - If applicable | [N/A] [N/A] | Not applicable to this installer scope. |
| **State Transitions** - If applicable | [✅] [PASS] | Force-reinstall and Docker-gated state transitions remain covered. |

### 1.2.1 Per-Language Coverage Comparison

- PowerShell: Baseline: 95.98% overall, 95.26% Install-layer -> Post-change: 95.98% overall, 95.26% Install-layer. Change: +0.00 overall / +0.00 Install-layer. New/changed-code coverage: 95.26%. Disposition: PASS. Evidence: `evidence/baseline/baseline-coverage.2026-04-26T15-51.md`, `evidence/qa-gates/coverage-delta.2026-04-26T15-51.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | [✅] [PASS] | The Pester suites use descriptive `It` names that identify the failing installer behavior directly. |
| **Arrange-Act-Assert Pattern** | [✅] [PASS] | The reviewed PowerShell tests retain clear setup, invocation, and assertion phases. |
| **Document Intent** | [✅] [PASS] | The split suite names and behavior-based grouping communicate purpose without ambiguity. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | [✅] [PASS] | The reviewed test scope relies on mocks rather than live Docker, process, or network execution. |
| **Use Mocks/Stubs** | [✅] [PASS] | External process, port, and runtime boundaries are mocked where required. |
| **Environment Stability** | [✅] [PASS] | No prohibited temporary-file creation is required by the reviewed suites. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | [✅] [PASS] | This artifact, together with `code-review.2026-04-26T16-26.md` and `feature-audit.2026-04-26T16-26.md`, is the refreshed review set for the current closure state. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | [✅] [PASS] | The authoritative scope remains issue `#59` and the approved closure target is REM-01 only. |
| **Read existing change plans** | [✅] [PASS] | `plan.2026-04-25T00-00.md`, `remediation-inputs.2026-04-26T02-16.md`, and `remediation-plan.2026-04-26T15-51.md` remain the governing plan artifacts. |
| **Document the plan** | [✅] [PASS] | The feature folder contains the original plan and remediation plan, both referenced by the refreshed review set. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | [✅] [PASS] | The validated implementation and this reduced audit stayed scoped to the installer/test remediation without widening behavior. |
| **Reusability** | [✅] [PASS] | The split test layout preserves reusable, focused test scopes rather than a single oversized file. |
| **Extensibility** | [✅] [PASS] | No new breaking public API changes were introduced by the reviewed remediation scope. |
| **Separation of concerns** | [✅] [PASS] | The reviewed diff remains concentrated in installer scripts, tests, and feature evidence artifacts. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | [✅] [PASS] | The feature remains scoped to installer behavior, related tests, and audit artifacts. |
| **Under 500 lines** | [✅] [PASS] | `evidence/baseline/remediation-state.2026-04-26T15-51.md` records `Install.Tests.ps1=456`, `Install.Force.Tests.ps1=163`, and `Install.HostAdapterStart.Tests.ps1=64`. |
| **Public vs internal** | [✅] [PASS] | No new public surface was added in the closure scope. |
| **No circular dependencies** | [✅] [PASS] | The reviewed scope does not introduce dependency-cycle evidence. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | [✅] [PASS] | The reviewed script and test names remain specific to installer and HostAdapter-start behavior. |
| **Docs/docstrings** | [✅] [PASS] | `issue.md`, the plan artifacts, and `remediation-inputs.2026-04-26T02-16.md` document scope and closure state explicitly. |
| **Comment why, not what** | [✅] [PASS] | No non-compliant commentary was introduced by the reviewed closure artifacts. |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | [✅] [PASS] | **Command:** `mcp_drmcopilotext2_run_poshqc_format(workspace_root, scan_folders=['scripts','tests/scripts'])`<br>**Result:** Current recheck returned `ok: true`; numeric final receipt exists at `evidence/qa-gates/final-format.2026-04-26T15-51.md`. |
| **2. Linting** | [✅] [PASS] | **Command:** `mcp_drmcopilotext2_run_poshqc_analyze(workspace_root, scan_folders=['scripts','tests/scripts'])`<br>**Result:** Current recheck returned `ok: true`; numeric final receipt exists at `evidence/qa-gates/final-analyze.2026-04-26T15-51.md`. |
| **3. Type checking** | [N/A] [N/A] | PowerShell type checking is not applicable in the repository contract. |
| **4. Testing** | [✅] [PASS] | **Command:** `mcp_drmcopilotext2_run_poshqc_test(workspace_root, scan_folders=['scripts','tests/scripts'])`<br>**Result:** Current recheck returned `ok: true`; numeric final receipt exists at `evidence/qa-gates/final-test.2026-04-26T15-51.md` with 189 passing tests. |
| **Full toolchain loop** | [✅] [PASS] | The final evidence set records a clean format -> analyze -> test pass, and the reviewer recheck did not reopen any failure. |
| **Explicit reporting** | [✅] [PASS] | Commands and evidence paths are documented in this audit and Appendix B. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | [✅] [PASS] | The feature plan, remediation plan, and refreshed review artifacts document the delivered installer/test remediation. |
| **Design choices explained** | [✅] [PASS] | `remediation-inputs.2026-04-26T02-16.md` preserves the scope boundary between closed REM-01 and out-of-scope REM-02/REM-03. |
| **Update supporting documents** | [✅] [PASS] | The feature folder contains updated policy, code, and feature audits plus supporting evidence. |
| **Provide next steps** | [✅] [PASS] | The refreshed review set limits follow-up to non-blocking REM-02 and REM-03 context only. |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3B: PowerShell Code Change Policy Compliance

#### 3B.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with Invoke-Formatter** | [✅] [PASS] | Baseline and final receipts exist, and the current reviewer recheck returned `ok: true`. |
| **Linting with PSScriptAnalyzer** | [✅] [PASS] | Baseline and final receipts exist, and the current reviewer recheck returned `ok: true`. |
| **Fix all findings** | [✅] [PASS] | No current blocking analyzer findings were surfaced by the approved evidence set or the recheck. |
| **PowerShell 7+ compatible** | [✅] [PASS] | The reviewed scope passed the repository PowerShell quality gates. |

#### 3B.2 PowerShell Design & Safety

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Advanced functions** | [✅] [PASS] | The validated implementation remains covered by the reviewed test suite and did not regress in the closure evidence. |
| **Parameter validation** | [✅] [PASS] | No evidence indicates degraded parameter handling in the reviewed branch state. |
| **Avoid global state** | [✅] [PASS] | No new global-state dependence was introduced by the closure scope. |
| **Error handling** | [✅] [PASS] | The missing-executable and guarded preflight paths remain explicitly tested and passing. |

#### 3B.3 Structure, Naming, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive and under 500 lines** | [✅] [PASS] | The split install test-file counts remain compliant per `evidence/baseline/remediation-state.2026-04-26T15-51.md`. |
| **Approved verbs** | [✅] [PASS] | The reviewed PowerShell command names remain consistent with approved verb-noun patterns. |
| **Comment why** | [✅] [PASS] | Closure artifacts did not introduce comment-policy regressions. |

#### 3B.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Step 1: Format** | [✅] [PASS] | `mcp_drmcopilotext2_run_poshqc_format(...)` returned `ok: true`. |
| **Step 2: Analyze** | [✅] [PASS] | `mcp_drmcopilotext2_run_poshqc_analyze(...)` returned `ok: true`. |
| **Step 3: Type check** | N/A | Not applicable for PowerShell. |
| **Step 4: Test** | [✅] [PASS] | `mcp_drmcopilotext2_run_poshqc_test(...)` returned `ok: true`. |
| **Rerun loop if needed** | [✅] [PASS] | No restart was required by the current review recheck. |

---

## 4. Language-Specific Unit Test Policy Compliance

### Section 4B: PowerShell Unit Test Policy Compliance

#### 4B.1 Framework and Scope

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use Pester v5.x** | [✅] [PASS] | `artifacts/pester/pester-junit.xml` and the final test receipt confirm a Pester-based run. |
| **Use PoshQC Configuration** | [✅] [PASS] | The final evidence and current recheck both use the repository PoshQC test command. |
| **PowerShell 7+ Compatible** | [✅] [PASS] | The approved PowerShell quality gate completed successfully for the reviewed scope. |

#### 4B.2 Test Style and Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Focused Unit Tests** | [✅] [PASS] | The split suites target distinct install and HostAdapter behaviors. |
| **Test Behavior Over Implementation** | [✅] [PASS] | The reviewed tests validate installer outcomes rather than internal implementation detail. |
| **Mocking Used Sparingly** | [✅] [PASS] | Only the external boundaries required for deterministic installer tests are mocked. |
| **Organization** | [✅] [PASS] | The PowerShell tests remain under `tests/scripts/` and mirror the installer script domain. |

#### 4B.3 Naming and Readability

| Requirement | Status | Evidence |
|------------|--------|----------|
| **File Naming** - *.Tests.ps1 | [✅] [PASS] | All reviewed test files end with `.Tests.ps1`. |
| **Describe/Context/It Structure** | [✅] [PASS] | Standard Pester organization remains intact throughout the reviewed suites. |
| **Logical Grouping** | [✅] [PASS] | Main install flow, force reinstall flow, and HostAdapter-start behavior remain grouped logically. |
| **Docstrings/Comments** | [✅] [PASS] | The test names are self-documenting for the reviewed scenarios. |

#### 4B.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use PoshQCTest Command** | [✅] [PASS] | `mcp_drmcopilotext2_run_poshqc_test(...)` returned `ok: true`, and the final numeric receipt records 189 passing tests. |
| **No Alternative Test Runners** | [✅] [PASS] | Only the approved bundled PowerShell quality tools were used. |

---

## 5. Test Coverage Detail

### Installer PowerShell scope (189 tests)

| Test Name | Scenario Type | Lines Covered | Status |
|-----------|--------------|---------------|--------|
| `Install.Tests.ps1` suite | Positive / Negative / Edge / Error Handling | `scripts/Install.ps1` main install flow | ✅ |
| `Install.Force.Tests.ps1` suite | State Transition / Error Handling | force-reinstall branches | ✅ |
| `Install.HostAdapterStart.Tests.ps1` suite | Positive / Negative / Edge | HostAdapter start behavior | ✅ |
| `Install.Helpers.Tests.ps1` suite | Positive / Negative / Error Handling | helper-module boundaries | ✅ |

**Coverage:** 95.98% overall PowerShell line coverage; 95.26% Install-layer line coverage.

**Not covered:** None identified that block REM-01 closure.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | 189 | ✅ |
| Tests Passed | 189 (100%) | ✅ |
| Tests Failed | 0 | ✅ |
| Execution Time | 16.207s total | ✅ Fast |
| Average Time per Test | 85.75ms | ✅ Fast |
| Discovery Time | Included in total Pester execution | ✅ |
| Functions/Classes Tested | Install-layer scope covered by evidence set | ✅ |
| Test File Size | `456 / 163 / 64` lines for the split install suites | ✅ Maintainable |
| Code Coverage (if applicable) | 95.98% overall, 95.26% Install-layer | ✅ |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Invoke-Formatter | `mcp_drmcopilotext2_run_poshqc_format(workspace_root, scan_folders=['scripts','tests/scripts'])` | `ok: true`; final receipt exit 0 | ✅ |
| PSScriptAnalyzer | `mcp_drmcopilotext2_run_poshqc_analyze(workspace_root, scan_folders=['scripts','tests/scripts'])` | `ok: true`; final receipt exit 0 | ✅ |
| Pester Tests | `mcp_drmcopilotext2_run_poshqc_test(workspace_root, scan_folders=['scripts','tests/scripts'])` | `ok: true`; final receipt 189 pass / 0 fail | ✅ |

**Notes:** `artifacts/pester/powershell-coverage.xml` still lacks Install-layer entries, so `artifacts/pester/coverage-final.refinement.xml` remains the supplementary numeric source for the audited coverage values.

---

## 8. Gaps and Exceptions

### Identified Gaps
- `artifacts/pester/powershell-coverage.xml` remains insufficient for Install-layer coverage detail, but the required supplementary coverage artifact is present and recorded. This does not block REM-01 closure.

### Approved Exceptions
- None. No policy exception is required for the requested review outcome.

### Removed/Skipped Tests
- None. The refreshed audit did not identify any removed or skipped test required for closure.

---

## 9. Summary of Changes

### Commits in This PR/Branch

1. **`44659d5`** - `fix(install): start HostAdapter from bundle before Stage 7 preflight`
2. **`73d8fc5`** - `fix(install): remediate 500-line violation and test cleanup bugs`

### Files Modified

1. **`scripts/Install.ps1`** (MODIFIED)
   - Contains the installer behavior under review.
   - Remains part of the validated closure scope.

2. **`scripts/Install.Helpers.psm1`** (MODIFIED)
   - Remains part of the reviewed PowerShell scope.
   - No refreshed evidence reopens a policy blocker here.

3. **`tests/scripts/Install.Tests.ps1`** (MODIFIED)
   - Main installer test file remains below 500 lines.
   - Continues to cover the core install scenarios.

4. **`tests/scripts/Install.Force.Tests.ps1`** (NEW)
   - Carries the extracted force-reinstall scenarios.
   - Keeps the test scope compliant with the file-size rule.

5. **`tests/scripts/Install.HostAdapterStart.Tests.ps1`** (NEW)
   - Carries the HostAdapter-start scenarios required by the issue acceptance criteria.
   - Remains present and passing in the validated branch state.

---

## 10. Compliance Verdict

### Overall Status: ✅ FULLY COMPLIANT

The refreshed minor-audit review finds REM-01 closed and the reviewed branch compliant for the requested closure scope. Required baseline evidence, QA evidence, coverage-comparison evidence, and refreshed PR-context artifacts all exist. REM-02 and REM-03 remain non-blocking context items only.

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Before Making Changes: PASS
- ✅ Design Principles: PASS
- ✅ Module & File Structure: PASS
- ✅ Naming, Docs, Comments: PASS
- ✅ Toolchain Execution: PASS
- ✅ Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3)

**For PowerShell:**
- ✅ Tooling & Baseline: PASS
- ✅ PowerShell Design & Safety: PASS
- ✅ Structure & Naming: PASS
- ✅ Toolchain: PASS

#### General Unit Test Policy (Section 1)
- ✅ Core Principles: PASS
- ✅ Coverage & Scenarios: PASS
- ✅ Test Structure: PASS
- ✅ External Dependencies: PASS
- ✅ Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4)

**For PowerShell:**
- ✅ Framework & Scope: PASS
- ✅ Test Style & Structure: PASS
- ✅ Naming & Readability: PASS
- ✅ Toolchain: PASS

### Metrics Summary

- ✅ 189/189 tests passing
- ✅ 95.98% overall PowerShell coverage
- ✅ 95.26% Install-layer changed/new-code coverage
- ✅ Split install test files remain under the 500-line limit
- ✅ Reviewer recheck of format, analyze, and test returned `ok: true`

### Recommendation

**Ready for merge**

This audit supersedes the stale `policy-audit.2026-04-26T02-16.md` artifact and confirms that the reviewed branch remains ready for normal PR flow for the approved closure scope.

---

## Appendix A: Test Inventory

### Complete Test List

- `tests/scripts/Install.Tests.ps1` suite
- `tests/scripts/Install.Force.Tests.ps1` suite
- `tests/scripts/Install.HostAdapterStart.Tests.ps1` suite
- `tests/scripts/Install.Helpers.Tests.ps1` suite
- Remaining repository PowerShell suites included in the 189-test Pester run recorded at `artifacts/pester/pester-junit.xml`

---

## Appendix B: Toolchain Commands Reference

```text
mcp_drmcopilotext2_collect_pr_context(workspace_root='c:\Users\DanMoisan\repos\open-claw-bridge', base='development')
mcp_drmcopilotext2_run_poshqc_format(workspace_root='c:\Users\DanMoisan\repos\open-claw-bridge', scan_folders=['scripts','tests/scripts'])
mcp_drmcopilotext2_run_poshqc_analyze(workspace_root='c:\Users\DanMoisan\repos\open-claw-bridge', scan_folders=['scripts','tests/scripts'])
mcp_drmcopilotext2_run_poshqc_test(workspace_root='c:\Users\DanMoisan\repos\open-claw-bridge', scan_folders=['scripts','tests/scripts'])
git merge-base HEAD development
git show -s --format=%cI 226516a7989f93893dca85186d12f09ba175de0f
git rev-parse HEAD
(Get-Content 'tests/scripts/Install.Tests.ps1').Count
(Get-Content 'tests/scripts/Install.Force.Tests.ps1').Count
(Get-Content 'tests/scripts/Install.HostAdapterStart.Tests.ps1').Count
```

**Audit Completed By:** GitHub Copilot  
**Audit Date:** 2026-04-26  
**Policy Version:** Current (as of audit date)
