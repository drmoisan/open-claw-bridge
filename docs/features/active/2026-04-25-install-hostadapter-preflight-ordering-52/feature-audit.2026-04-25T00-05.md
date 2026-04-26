# Feature Audit: install-hostadapter-preflight-ordering (Issue #52)

**Audit Date:** 2026-04-25
**Feature Folder:** `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52`
**Base Branch:** `main`
**Head Branch:** `bug/install-hostadapter-preflight-ordering-52`
**Work Mode:** `minor-audit`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `48065c691e74c6b40b019a837eb0cdd294ad8993` per `artifacts/pr_context.summary.txt`)
- **Head branch/commit:** `bug/install-hostadapter-preflight-ordering-52`
- **Merge base:** `48065c691e74c6b40b019a837eb0cdd294ad8993`
- **Evidence sources:**
  - Baseline evidence: `evidence/baseline/` (phase0-instructions-read.md, baseline-format.md, baseline-analyze.md, baseline-test.md)
  - Regression-testing evidence: `evidence/regression-testing/` (fail-before-p1t1.2026-04-25T00-00.md, fail-before-p1t2.2026-04-25T00-00.md, fail-before-p1t3.2026-04-25T00-00.md)
  - QA gate evidence: `evidence/qa-gates/` (final-qc-format.md, final-qc-analyze.md, final-qc-test.md, coverage-delta.md)
  - Code inspection: `scripts/Install.ps1` (lines 403–440), `tests/scripts/Install.Tests.ps1` (lines 82–305)
- **Feature folder used:** `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52`
- **Requirements source:** `issue.md` (sole source — `minor-audit` work mode)
- **Work mode resolution note:** `- Work Mode: minor-audit` is explicitly present in `issue.md` and confirmed in `plan.2026-04-25T00-00.md`. AC source is `## Acceptance Criteria` in `issue.md` only.
- **Scope note:** Baseline is the `main` branch state. The change involves two files: `scripts/Install.ps1` and `tests/scripts/Install.Tests.ps1`.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/issue.md` — only source (minor-audit)

### Acceptance Criteria (from `issue.md` § Acceptance Criteria)

1. When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-MsixInstall` must NOT have been called (MSIX left clean).
2. When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-ComposeUp` must NOT be called.
3. When `Assert-HostAdapterRuntimePreflight` fails, `Wait-ComposeHealthy` must NOT be called.
4. The happy-path stage ordering test passes with `Assert-HostAdapterRuntimePreflight` (or its underlying `Invoke-WebRequest`) confirmed to execute before `Invoke-MsixInstall`.
5. All existing Install.ps1 tests pass (no regressions).
6. The full PoshQC toolchain (format → analyze → test) passes without errors.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| 1 | `Invoke-MsixInstall` NOT called when preflight fails | PASS | `Install.ps1` line 409: `Assert-HostAdapterRuntimePreflight` executes in Stage 7 before `Invoke-MsixInstall` at Stage 8. Test `'throws before compose up when the HostAdapter status probe is not ready'` (line 288): `$global:InstallTestCalls -contains 'Invoke-MsixInstall' \| Should -BeFalse` passes. Test `'does not install MSIX when the HostAdapter status probe throws on unreachable endpoint'` (line 299): same assertion passes. Fail-before: `fail-before-p1t1.2026-04-25T00-00.md` (EXIT_CODE 1 against pre-fix code). | `mcp_drmcopilotext_run_poshqc_test` | Covered by two independent tests (HTTP 503 and WebException). |
| 2 | `Invoke-ComposeUp` NOT called when preflight fails | PASS | Test `'throws before compose up when the HostAdapter status probe is not ready'` (line 295): `$global:InstallTestCalls -contains 'Invoke-ComposeUp' \| Should -BeFalse` passes. Script exits before reaching Stage 9 when Stage 7 throws. | `mcp_drmcopilotext_run_poshqc_test` | Covered by the same HTTP 503 test that covers AC-1. |
| 3 | `Wait-ComposeHealthy` NOT called when preflight fails | PASS | Test `'throws before compose up when the HostAdapter status probe is not ready'` (line 296): `$global:InstallTestCalls -contains 'Wait-ComposeHealthy' \| Should -BeFalse` passes. Script exits before reaching Stage 9 when Stage 7 throws. | `mcp_drmcopilotext_run_poshqc_test` | Covered by the same HTTP 503 test that covers AC-1 and AC-2. |
| 4 | Happy-path: `Invoke-WebRequest` executes before `Invoke-MsixInstall` | PASS | Test `'calls helpers in the correct order'` (line ~194): `$expected` array includes `'Invoke-WebRequest'` immediately before `'Invoke-MsixInstall'`; assertion passes. `BeforeEach Mock Invoke-WebRequest` (line 83–87) appends `'Invoke-WebRequest'` to `$global:InstallTestCalls`. Fail-before: `fail-before-p1t2.2026-04-25T00-00.md` (EXIT_CODE 1 — `Invoke-WebRequest` appeared after `Invoke-MsixInstall` in pre-fix code). | `mcp_drmcopilotext_run_poshqc_test` | The plan used `Invoke-WebRequest` as a proxy for the preflight (it is the HTTP probe inside `Assert-HostAdapterRuntimePreflight`). |
| 5 | All existing Install.ps1 tests pass (no regressions) | PASS | Baseline: 185 tests passed. Final QC: 186 tests passed (1 new test added). Zero failures. Evidence: `evidence/qa-gates/final-qc-test.md` (EXIT_CODE 0, 186 passed / 0 failed / 0 skipped). All previously passing tests continue to pass. | `mcp_drmcopilotext_run_poshqc_test` | Net +1 test. No pre-existing test was removed or broken. |
| 6 | Full PoshQC toolchain passes without errors | PASS | Format: `evidence/qa-gates/final-qc-format.md` (EXIT_CODE 0, no files changed). Analyze: `evidence/qa-gates/final-qc-analyze.md` (EXIT_CODE 0, zero diagnostics). Test: `evidence/qa-gates/final-qc-test.md` (EXIT_CODE 0, 186 passed). All three steps passed in a single clean pass. | `mcp_drmcopilotext_run_poshqc_format`, `mcp_drmcopilotext_run_poshqc_analyze`, `mcp_drmcopilotext_run_poshqc_test` | Baseline QA also confirmed clean (EXIT_CODE 0 for all three steps at baseline). |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 6 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. None. All six acceptance criteria are verified as PASS.

**Recommended follow-up verification steps:**

1. Open a follow-up task to extract one test context from `tests/scripts/Install.Tests.ps1` to bring the file under the 500-line policy limit (currently 503 lines). This is not an AC item but is a policy compliance gap documented in `policy-audit.2026-04-25T00-05.md` §8.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules, all six AC items evaluated as PASS are checked off in `issue.md`. The source file uses standard markdown checkbox format (`- [ ]`), so check-off is applied directly.

### AC Status Summary

- Source: `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/issue.md`
- Total AC items: 6
- Checked off (delivered): 6
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `issue.md` | 6 | 6 | 0 | Checkbox-backed (`- [ ]` → `- [x]`); all items verified via toolchain evidence and code inspection |

All six AC checkboxes in `issue.md` are updated to `- [x]` as part of this audit.
