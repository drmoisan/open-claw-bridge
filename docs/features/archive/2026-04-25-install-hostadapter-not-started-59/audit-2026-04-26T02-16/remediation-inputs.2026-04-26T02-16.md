# Remediation Inputs — install-hostadapter-not-started-59

- Artifact type: remediation-inputs
- Timestamp: 2026-04-26T02-16
- Branch: bug/install-hostadapter-not-started-59
- Merge base: d2d13b3853538f697d0daadb75b67260019f0abe

---

## Remediation-Required Findings

### REM-01 — BLOCKING: Install.Tests.ps1 exceeds 500-line limit

- Source artifact: `code-review.2026-04-26T02-16.md` § CR-02
- Policy: `.claude/rules/general-code-change.md` — "No production code, test code, or reusable script file may exceed 500 lines."
- File: `tests/scripts/Install.Tests.ps1`
- Current line count: 506
- Overage: 6 lines

**Required action:** Extract content from `Install.Tests.ps1` to bring it to or below 500 lines. The recommended extraction target is the `-Force prior-install handling` `Context` block (lines 358–434, approximately 77 lines), which can move to a new file `tests/scripts/Install.Force.Tests.ps1`. Alternatively, the `Docker runtime input preflight` context (lines 273–357) could be extracted to `tests/scripts/Install.DockerPreflight.Tests.ps1`. Either approach must preserve all existing tests and produce a clean toolchain pass.

The new file must include a `BeforeAll` that imports `Install.Helpers.psm1` and sets up the same `$global:InstallTestCalls` tracking and helper mocks as `Install.Tests.ps1`, since the extracted tests depend on the same mock infrastructure.

After the split, re-run the full PoshQC toolchain (format → analyze → test) and confirm all test counts match (189 total currently).

---

### REM-02 — RECOMMENDED: New functions placed in Install.ps1 rather than Install.Helpers.psm1

- Source artifact: `code-review.2026-04-26T02-16.md` § CR-01, CR-03, CR-09
- Policy: `.claude/rules/general-code-change.md` — Separation of concerns; reusability.
- Files affected: `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `tests/scripts/Install.HostAdapterStart.Tests.ps1`, `tests/scripts/Install.Tests.ps1`

**Context:** `Test-TcpPortOpen`, `Invoke-HostAdapterProcess`, and `Invoke-HostAdapterStart` are defined in `Install.ps1` behind `if (-not (Get-Command ...))` guards. The plan specified these functions should be in `Install.Helpers.psm1` with `Export-ModuleMember` entries. The current placement prevents use from other scripts and uses a non-standard mockability mechanism.

**Required action (recommended, not blocking for merge):** Move the three functions to `Install.Helpers.psm1` (before the `Export-ModuleMember` call) and add them to the `Export-ModuleMember` list. Remove the `if (-not (Get-Command ...))` wrapper blocks from `Install.ps1`. Update `Install.HostAdapterStart.Tests.ps1` to remove the dot-source of `Install.ps1` and rely only on the module import; update the `Describe` label to reference `Install.Helpers.psm1`. Replace the `function global:Invoke-HostAdapterStart { ... }` override in `Install.Tests.ps1` `BeforeEach` with a standard `Mock Invoke-HostAdapterStart { ... }`. Verify line counts remain within limits after the move: `Install.Helpers.psm1` will grow by approximately 55 lines (from 466 to ~521) — this would also breach the 500-line limit, so a secondary extraction from `Install.Helpers.psm1` may be required concurrently.

**Note:** Because resolving REM-02 may cause `Install.Helpers.psm1` to exceed 500 lines, the remediation executor must plan both concerns together. The executor should verify line counts at preflight before writing any code.

---

### REM-03 — INFORMATIONAL: Primary coverage artifact does not contain Install file data

- Source artifact: `policy-audit.2026-04-26T02-16.md` § Coverage Verification
- Policy: Coverage agent policy — primary artifact path `artifacts/pester/powershell-coverage.xml`.
- File: `artifacts/pester/powershell-coverage.xml`

**Context:** The primary coverage artifact `artifacts/pester/powershell-coverage.xml` was generated from a hooks-only Pester run and does not include coverage for `scripts/Install.ps1` or `scripts/Install.Helpers.psm1`. Coverage was verified via `artifacts/pester/coverage-final.refinement.xml` (a supplementary artifact from the executor's Phase 2 QC loop). Thresholds are met; this is a procedural gap, not a threshold failure.

**Required action (informational):** No immediate code change required. For future runs, the Pester configuration should target `./tests` and include `scripts/Install.ps1, scripts/Install.Helpers.psm1` in the `CodeCoverage.Path` so that `powershell-coverage.xml` captures Install-layer data. This may require an update to `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` or the MCP command defaults.

---

## Remediation Priority

| ID | Severity | Blocking | Action |
|---|---|---|---|
| REM-01 | High | Yes — must be resolved before merge | Extract context block(s) from `Install.Tests.ps1` to reach <= 500 lines |
| REM-02 | Medium | No — recommended before next change to these files | Move three functions to `Install.Helpers.psm1`; update tests |
| REM-03 | Informational | No | Ensure `powershell-coverage.xml` captures Install files in future runs |

## Remediation Closure Status

- REM-01 — Closed at HEAD 73d8fc5f038632b25b7c78d33345ecfafa90afc0; validated by evidence/regression-testing/rem-01-targeted-verification and evidence/other/post-remediation-validation artifacts.
- REM-02 — Remains open as a context item and was not executed under this closure plan.
- REM-03 — Remains open as a context item and was not executed under this closure plan.
