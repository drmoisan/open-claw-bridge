# Policy Audit — install-hostadapter-not-started-59

- Artifact type: policy-audit
- Timestamp: 2026-04-26T02-16
- Branch: bug/install-hostadapter-not-started-59
- Merge base: d2d13b3853538f697d0daadb75b67260019f0abe
- Base branch: development
- Work mode: minor-audit
- Reviewer: feature-review-agent

---

## Scope

Full branch diff against merge base `d2d13b3853538f697d0daadb75b67260019f0abe`. Changed production and test files:

| File | Status |
|---|---|
| `scripts/Install.ps1` | Modified (+57 lines) |
| `scripts/Install.Helpers.psm1` | Modified (+1/-1 line, indentation fix only) |
| `tests/scripts/Install.HostAdapterStart.Tests.ps1` | Added (64 lines) |
| `tests/scripts/Install.Tests.ps1` | Modified (+19/-10 lines) |

Feature-folder documentation files (issue.md, plan, evidence) are also present in the diff; these are excluded from policy checks as documentation.

Languages with changed files: **PowerShell only**.

No TypeScript, Python, or C# files changed. TypeScript, Python, and C# coverage verdicts are not applicable (zero changed files in those languages).

---

## Rejected Scope Narrowing

None detected. The caller prompt did not attempt to narrow scope.

---

## Policy Reading Order

Per the `policy-compliance-order` skill and confirmed by `evidence/baseline/phase0-instructions-read.md`:

1. `CLAUDE.md` — not present in repository; no standing instructions.
2. `.claude/rules/general-code-change.md` — applicable; read and confirmed.
3. `.claude/rules/general-unit-test.md` — applicable; read and confirmed.
4. `.claude/rules/powershell.md` — applicable (PowerShell files changed); read and confirmed.

---

## Policy Findings

### 1. General Code Change Policy

| Check | Verdict | Evidence |
|---|---|---|
| Simplicity first | PASS | Implementation uses straightforward linear logic. No unnecessary abstraction introduced. |
| File size limit (500 lines) | PARTIAL | `scripts/Install.ps1` is exactly 500 lines (blank final line counted by `wc -l` as 501 with trailing newline, but 500 substantive). `tests/scripts/Install.Tests.ps1` is 506 lines, which is 6 lines over the 500-line limit. `scripts/Install.Helpers.psm1` is 466 lines (PASS). `tests/scripts/Install.HostAdapterStart.Tests.ps1` is 64 lines (PASS). |
| Separation of concerns | PARTIAL | The three new functions (`Test-TcpPortOpen`, `Invoke-HostAdapterProcess`, `Invoke-HostAdapterStart`) are defined inside `scripts/Install.ps1` behind `if (-not (Get-Command ...))` guards rather than in `scripts/Install.Helpers.psm1` as the plan specified (P1-T1 through P1-T4). The helpers module is the designated location for reusable helper functions. The functions work correctly but are misplaced relative to the established module boundary. |
| Reusability | PARTIAL | The new helper functions are only accessible when `Install.ps1` is dot-sourced or executed. Because they are not in `Install.Helpers.psm1`, they are not exported and not available to other consumers such as `Uninstall.ps1`. |
| Error handling (fail fast, explicit) | PASS | `Invoke-HostAdapterStart` throws a clear, specific message on exe-not-found before attempting the preflight. No silent error suppression detected. |
| SupportsShouldProcess on state-changing functions | PASS | `Invoke-HostAdapterProcess` and `Invoke-HostAdapterStart` both declare `CmdletBinding(SupportsShouldProcess = $true)`. `Test-TcpPortOpen` is read-only and uses `CmdletBinding()` only (correct). |
| Mandatory toolchain loop (format → analyze → test) | PASS | Evidence in `qa-gates/`: formatter exit 0 (all CLEAN), analyzer exit 0 (zero findings), Pester exit 0 (189 passed, 0 failed). |
| No breaking public API changes | PASS | No existing exported function signatures changed. The `Export-ModuleMember` list in `Install.Helpers.psm1` is unchanged (consistent with functions being in `Install.ps1` rather than the module). |
| Naming conventions | PASS | `Test-TcpPortOpen`, `Invoke-HostAdapterProcess`, `Invoke-HostAdapterStart` all use approved PowerShell verb-noun format and descriptive names. |
| No unapproved dependencies | PASS | Only .NET BCL types (`System.Net.Sockets.TcpClient`, `System.Diagnostics.Process`, `UriBuilder`) are used; no new package dependencies. |
| I/O boundaries isolated | PASS | External process launch is isolated behind `Invoke-HostAdapterProcess` (wrapper seam). TCP probe is isolated behind `Test-TcpPortOpen`. Core logic in `Invoke-HostAdapterStart` is testable via mocks. |

### 2. General Unit Test Policy

| Check | Verdict | Evidence |
|---|---|---|
| Independence and isolation | PASS | Each `It` block in `Install.HostAdapterStart.Tests.ps1` registers its own mocks. No shared mutable state between tests. |
| Fast execution | PASS | Tests are fully mocked; no network calls, no process spawning. |
| Determinism | PASS | All external calls are mocked. No ambient environment dependencies. |
| Readability and maintainability | PASS | Test names are descriptive. Arrange-Act-Assert structure is clear in all three new tests. |
| Positive, negative, and edge-case coverage | PASS | Three paths covered: not-running (launches), already-running (skips), exe-not-found (throws). |
| No temporary files in tests | PASS | No temporary file creation detected in any test. |
| No external dependencies | PASS | `Invoke-HostAdapterProcess`, `Test-TcpPortOpen`, and `Test-Path` are all mocked in unit tests. |
| No mutable global state reliance | PASS | `Install.Tests.ps1` resets `$global:InstallTestCalls` in `BeforeEach`. `AfterEach` removes global function overrides. |

### 3. PowerShell Policy

| Check | Verdict | Evidence |
|---|---|---|
| PowerShell 7+ compatibility | PASS | `#Requires -Version 7.0` present in `Install.ps1` and test files. `.NET` APIs used are available in PS7+. |
| Advanced functions with CmdletBinding | PASS | All three new functions use `[CmdletBinding()]` or `[CmdletBinding(SupportsShouldProcess = $true)]`. |
| Mandatory parameters declared | PASS | All parameters marked `[Parameter(Mandatory = $true)]`. |
| Wrapper function seam for external process calls | PASS | `[System.Diagnostics.Process]::Start()` extracted into `Invoke-HostAdapterProcess`. Tests mock the wrapper rather than the .NET type. |
| Mock signature parity | PASS | Test mocks for `Invoke-HostAdapterProcess` and `Test-TcpPortOpen` do not require parameter declarations beyond what is exercised (consistent with Pester v5 behavior and existing test pattern). |
| No `Invoke-Expression`, no hardcoded credentials | PASS | Neither found in the diff. |
| Toolchain: format → analyze → test | PASS | All three steps passed. See qa-gates evidence. |
| Per-batch file cap (3 production, 3 test) | PASS | 2 production files changed (`Install.ps1`, `Install.Helpers.psm1`), 2 test files changed/added (`Install.Tests.ps1`, `Install.HostAdapterStart.Tests.ps1`). Within cap. |

---

## Coverage Verification — PowerShell

Coverage artifact for PowerShell: `artifacts/pester/powershell-coverage.xml`

**Finding:** The primary coverage artifact (`artifacts/pester/powershell-coverage.xml`) does not contain entries for `scripts/Install.ps1` or `scripts/Install.Helpers.psm1`. The artifact covers only `.claude/hooks/` scripts. This indicates the primary artifact was generated from a hooks-only test run and does not represent the Install toolchain run.

A supplementary artifact, `artifacts/pester/coverage-final.refinement.xml`, was produced by the executor's Phase 2 QC loop and contains Install-specific coverage data:

| File | Lines Covered | Lines Total | Coverage |
|---|---|---|---|
| `scripts/Install.ps1` | 70 | 75 | 93.3% |
| `scripts/Install.Helpers.psm1` | 131 | 136 | 96.3% |
| Repo-wide (5 files) | 430 | 448 | 96.0% |

The executor's narrative evidence in `qa-gates/final-test.2026-04-25T00-00.md` states: 189 tests passed, 0 failed, overall coverage 92.6% on 419 analyzed commands in 2 files (Install.ps1 + Install.Helpers.psm1), exceeding the 80% overall threshold and the 90% new-code threshold.

**Coverage verdict for new functions:** All three new functions are present in `Install.ps1`. `coverage-final.refinement.xml` shows `scripts/Install` at 93.3%. The three new test scenarios (not-running, already-running, exe-not-found) are confirmed passing. The new-code threshold of 90% is met.

**Coverage verdict for modified files:** `Install.ps1` at 93.3% and `Install.Helpers.psm1` at 96.3%; both exceed the 80% modified-file threshold and represent increases from the baseline (51.33% on the pre-implementation scope per baseline-test artifact). No regression.

**Primary artifact gap:** `artifacts/pester/powershell-coverage.xml` does not contain `Install.ps1` or `Install.Helpers.psm1` entries. This is a gap in the standard artifact location. Coverage is verifiable via `coverage-final.refinement.xml`, but the primary artifact path per policy is not populated.

| Threshold | Verdict | Basis |
|---|---|---|
| Repo-wide >= 80% | PASS | 96.0% per `coverage-final.refinement.xml` |
| New code >= 90% | PASS | 93.3% for `Install.ps1` which hosts all new functions |
| Modified file no regression | PASS | Both files increased from baseline |
| Primary artifact populated | FAIL | `artifacts/pester/powershell-coverage.xml` lacks Install file entries |

**Overall PowerShell coverage verdict: PARTIAL** — thresholds are met per supplementary artifact, but the primary artifact path (`artifacts/pester/powershell-coverage.xml`) is not populated with Install file data.

---

## Summary

| Domain | Verdict |
|---|---|
| General code change policy | PARTIAL |
| General unit test policy | PASS |
| PowerShell policy | PASS |
| PowerShell coverage thresholds | PASS (via supplementary artifact) |
| Primary PowerShell coverage artifact population | FAIL |

**Overall policy verdict: PARTIAL**

Blocking finding: `tests/scripts/Install.Tests.ps1` is 506 lines, 6 lines over the 500-line limit.

Non-blocking findings (noted for remediation consideration):
- New functions placed in `Install.ps1` rather than `Install.Helpers.psm1` as planned; reduces reusability and deviates from established module boundary.
- Primary coverage artifact `artifacts/pester/powershell-coverage.xml` does not include Install file data; coverage is verified only via supplementary artifact.
