# Split Plan — Install.Helpers.Tests.ps1 size remediation

- Timestamp: 2026-04-27T09-30
- Feature folder: `docs/features/active/2026-04-26-install-stale-hostadapter-detection/`
- Canonical issue number: N/A
- Source file: `tests/scripts/Install.Helpers.Tests.ps1` (522 lines including trailing newline; 521 content lines, 21 over the 500-line cap)
- Target sibling file: `tests/scripts/Install.Helpers.Compose.Tests.ps1`
- Work Mode: minor-audit
- Mandate: file-size policy compliance only. No production-file edits. No test logic changes beyond the mechanical split.

## Inventory (verified by read)

Confirmed structure of `tests/scripts/Install.Helpers.Tests.ps1`:

- Header block (lines 1-17): file-level comment, `SuppressMessageAttribute`, `param()`.
- Outer `Describe 'Install.Helpers.psm1'` (lines 19-497) containing:
  - `BeforeAll` / `AfterAll` (lines 21-28): imports `scripts\Install.Helpers.psm1`, removes module on teardown.
  - `Context 'scripts/Install.Helpers.psm1 - export surface'` (lines 30-53). Retain in original (it asserts the full export surface including the four Compose helpers).
  - Non-Compose `Context` blocks to retain in original:
    - `Context 'Get-ManifestVersion'` (lines 55-84)
    - `Context 'Test-ManifestIntegrity'` (lines 86-159)
    - `Context 'Copy-BundleContents'` (lines 161-182)
    - `Context 'Initialize-DotEnv'` (lines 184-204)
    - `Context 'Invoke-MsixInstall'` (lines 206-225)
    - `Context 'Invoke-MsixCapture'` (lines 227-240)
    - `Context 'Invoke-MsixRemove'` (lines 242-268)
    - `Context 'Write-InstallRecord'` (lines 417-451)
    - `Context 'Read-InstallRecord'` (lines 453-496)
  - Compose-related `Context` blocks to extract (the four targeted by this plan):
    - `Context 'Test-DockerAvailable'` (lines 270-290)
    - `Context 'Invoke-ComposeUp'` (lines 292-318)
    - `Context 'Wait-ComposeHealthy'` (lines 320-389)
    - `Context 'Invoke-ComposeDown'` (lines 391-415)
- AC-14 remediation `Describe` blocks to retain in original (top-level `Describe`, not nested):
  - `Describe 'Get-ProcessMainModulePath defensive branch'` (lines 499-509)
  - `Describe 'Get-ListeningProcessId no-listener path'` (lines 511-521)

Note: the four Compose blocks are `Context` blocks nested inside the outer `Describe 'Install.Helpers.psm1'`. The new file must wrap them in an equivalent outer `Describe` so the `BeforeAll`/`AfterAll` (Import-Module / Remove-Module) preamble at lines 21-28 is reused with parameter and behaviour parity.

Compose-block extraction span (inclusive): lines 270-415 (146 lines). Removing this span from the original yields 521 - 146 = 375 content lines, well under 500.

The new sibling file must contain:
- the file-level header comment, the `SuppressMessageAttribute`, and `param()` from lines 1-17 (verbatim, scoped to the Compose subset),
- a single outer `Describe 'Install.Helpers.psm1 (Compose helpers)'` block,
- the same `BeforeAll` (Import-Module Install.Helpers.psm1 -Force) and `AfterAll` (Remove-Module) pair,
- the four extracted `Context` blocks verbatim (lines 270-415).

Estimated size of new file: 146 (extracted blocks) + ~30 (header + Describe + BeforeAll/AfterAll) ~= 176 lines. Well under 500.

## Evidence Location Invariant

All evidence artifacts produced by this plan are written to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/<kind>/`. Non-canonical evidence paths are rejected.

---

### Phase 0 — Baseline capture and policy reads

- [x] [P0-T1] Read `.claude/rules/general-code-change.md` and capture the 500-line cap clause and the mandatory toolchain order. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/baseline/phase0-instructions-read.2026-04-27T09-30.md` with fields: `Timestamp:`, `Policy Order:` (list `general-code-change.md`, `general-unit-test.md`, `powershell.md`), and explicit list of files read. Acceptance: artifact exists at the named path with all required fields.
- [x] [P0-T2] Read `.claude/rules/general-unit-test.md` and `.claude/rules/powershell.md`; append confirmation of toolchain (format -> analyze -> test) and mock parameter-name parity rule to the same `phase0-instructions-read.2026-04-27T09-30.md` artifact. Acceptance: file lists both rule paths under "Files Read".
- [x] [P0-T3] Capture exact pre-change line count of `tests/scripts/Install.Helpers.Tests.ps1`. Command: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1' | Measure-Object -Line).Lines`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/baseline/p0-source-linecount.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record the integer line count and confirm > 500). Acceptance: artifact exists and `Output Summary:` contains the exact line count.
- [x] [P0-T4] Confirm sibling target path is unused. Command: `Test-Path -LiteralPath 'tests/scripts/Install.Helpers.Compose.Tests.ps1'`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/baseline/p0-target-availability.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record `False` to confirm path is free). Acceptance: artifact records `False`.
- [x] [P0-T5] Capture baseline Pester result for `tests/scripts/Install.Helpers.Tests.ps1`. MCP command: `mcp__drmCopilotExtension__run_poshqc_test` scoped to `tests/scripts/Install.Helpers.Tests.ps1`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/baseline/p0-pester-baseline.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record passed count, failed count, and confirm pre-split test count). Acceptance: artifact exists and `Output Summary:` records numeric pass/fail counts. Coverage note: this plan re-uses the prior `p7r1-coverage-delta` artifact for the 90% gate; no fresh coverage capture is required by scope.

### Phase 1 — Create sibling file `Install.Helpers.Compose.Tests.ps1`

- [x] [P1-T1] Create `tests/scripts/Install.Helpers.Compose.Tests.ps1` with the file-level header (verbatim from `tests/scripts/Install.Helpers.Tests.ps1` lines 1-17: `#Requires -Version 7.0` directive, the multi-line `<# .SYNOPSIS / .DESCRIPTION #>` comment block updated to state the file covers the four Compose helpers only, the `[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidGlobalVars', '', Justification = '...')]`, and `param()`). Acceptance: file exists; first non-blank line is `#Requires -Version 7.0`; suppress attribute and `param()` are present.
- [x] [P1-T2] Append outer `Describe 'Install.Helpers.psm1 (Compose helpers)'` block to `tests/scripts/Install.Helpers.Compose.Tests.ps1` containing exactly the same `BeforeAll` (lines 21-24: assigns `$script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'`; calls `Import-Module $script:ModulePath -Force`) and `AfterAll` (lines 26-28: `Remove-Module Install.Helpers -ErrorAction SilentlyContinue`) pair as the source. Acceptance: `BeforeAll` and `AfterAll` blocks match source lines 21-28 byte-for-byte except for outer indentation.
- [x] [P1-T3] Copy the `Context 'Test-DockerAvailable'` block (source lines 270-290) verbatim into the new `Describe` body in `tests/scripts/Install.Helpers.Compose.Tests.ps1`. Acceptance: copied range matches the source range byte-for-byte; mock parameter names (`docker` shim signature) unchanged.
- [x] [P1-T4] Copy the `Context 'Invoke-ComposeUp'` block (source lines 292-318) verbatim into `tests/scripts/Install.Helpers.Compose.Tests.ps1` after the previous block. Acceptance: copied range matches source range byte-for-byte.
- [x] [P1-T5] Copy the `Context 'Wait-ComposeHealthy'` block (source lines 320-389) verbatim into `tests/scripts/Install.Helpers.Compose.Tests.ps1` after the previous block. Acceptance: copied range matches source range byte-for-byte.
- [x] [P1-T6] Copy the `Context 'Invoke-ComposeDown'` block (source lines 391-415) verbatim into `tests/scripts/Install.Helpers.Compose.Tests.ps1` after the previous block; close the outer `Describe`. Acceptance: copied range matches source range byte-for-byte; closing `}` for the outer `Describe` is present.
- [x] [P1-T7] Verify the new file is syntactically a single outer `Describe`. Command: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Compose.Tests.ps1' | Measure-Object -Line).Lines`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/baseline/p1-new-file-linecount.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record line count; assert <= 500). Acceptance: line count is <= 500.

### Phase 2 — Prune Compose blocks from the original file

- [x] [P2-T1] Delete the `Context 'Test-DockerAvailable'` block (source lines 270-290) from `tests/scripts/Install.Helpers.Tests.ps1`. Acceptance: file no longer contains the literal string `Context 'Test-DockerAvailable'`.
- [x] [P2-T2] Delete the `Context 'Invoke-ComposeUp'` block (source lines 292-318) from `tests/scripts/Install.Helpers.Tests.ps1`. Acceptance: file no longer contains the literal string `Context 'Invoke-ComposeUp'`.
- [x] [P2-T3] Delete the `Context 'Wait-ComposeHealthy'` block (source lines 320-389) from `tests/scripts/Install.Helpers.Tests.ps1`. Acceptance: file no longer contains the literal string `Context 'Wait-ComposeHealthy'`.
- [x] [P2-T4] Delete the `Context 'Invoke-ComposeDown'` block (source lines 391-415) from `tests/scripts/Install.Helpers.Tests.ps1`. Acceptance: file no longer contains the literal string `Context 'Invoke-ComposeDown'`.
- [x] [P2-T5] Verify the export-surface assertion in `Context 'scripts/Install.Helpers.psm1 - export surface'` (originally lines 30-53) is unchanged and still asserts all 16 exported helpers including the four Compose helpers. Acceptance: lines 33-50 (post-edit positions may differ) still list `'Test-DockerAvailable'`, `'Invoke-ComposeUp'`, `'Wait-ComposeHealthy'`, `'Invoke-ComposeDown'` inside the `$expected` array.
- [x] [P2-T6] Verify the AC-14 remediation `Describe` blocks are intact and unchanged (`Describe 'Get-ProcessMainModulePath defensive branch'` and `Describe 'Get-ListeningProcessId no-listener path'`). Acceptance: both `Describe` headers and their `It` blocks remain verbatim.
- [x] [P2-T7] Capture post-prune line count of `tests/scripts/Install.Helpers.Tests.ps1`. Command: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1' | Measure-Object -Line).Lines`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/baseline/p2-source-linecount.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record line count; assert <= 500). Acceptance: line count is <= 500.

### Phase 3 — Final QA loop and verification

- [x] [P3-T1] Run PoshQC formatter on both files. MCP command: `mcp__drmCopilotExtension__run_poshqc_format` over `tests/scripts/Install.Helpers.Tests.ps1` and `tests/scripts/Install.Helpers.Compose.Tests.ps1`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/qa-gates/p3-format.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record formatter outcome and whether files were modified). Acceptance: formatter exits 0; if any file is modified, restart the loop from this task.
- [x] [P3-T2] Run PoshQC analyzer on both files. MCP command: `mcp__drmCopilotExtension__run_poshqc_analyze` over `tests/scripts/Install.Helpers.Tests.ps1` and `tests/scripts/Install.Helpers.Compose.Tests.ps1`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/qa-gates/p3-analyze.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record analyzer findings; expect zero new findings). Acceptance: analyzer exits 0 with no new findings versus baseline; if findings appear, address and restart from P3-T1.
- [x] [P3-T3] Run Pester scoped to both target files. MCP command: `mcp__drmCopilotExtension__run_poshqc_test` with paths `tests/scripts/Install.Helpers.Tests.ps1` and `tests/scripts/Install.Helpers.Compose.Tests.ps1`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/qa-gates/p3-pester.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record passed/failed/skipped counts and assert total passed equals 215). Acceptance: total passed count is exactly 215, failed count is 0; if any test changes files via formatter, restart from P3-T1.
- [x] [P3-T4] Verify final line counts of both files. Commands: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1' | Measure-Object -Line).Lines` and `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Compose.Tests.ps1' | Measure-Object -Line).Lines`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/qa-gates/p3-linecounts.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record both line counts; assert each <= 500). Acceptance: both counts <= 500.
- [x] [P3-T5] Run repository-wide Pester sanity pass to confirm no other test file references the removed contexts and no double-registration occurs. MCP command: `mcp__drmCopilotExtension__run_poshqc_test` scoped to `tests/scripts/`. Write evidence to `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/qa-gates/p3-pester-repo.2026-04-27T09-30.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (record total passed/failed/skipped). Acceptance: failed count is 0; passed count is at least the prior repo baseline (no regressions introduced by the split).

---

PREFLIGHT: ALL CLEAR
