# PoshQC Test-and-Coverage — Bicep Secret-Scan Script + Test (P5-T5)

- Timestamp: 2026-07-07T02-35 (original MCP attempt); remediation capture 2026-07-07T04-25
- Command (MCP, still fails): `mcp__drm-copilot__run_poshqc_test` (scan_folders: `["tests/scripts"]`)
- EXIT_CODE (MCP): 4294967295 (unsigned representation of process exit code -1)
- Command (remediation, bundled-pipeline workaround): `pwsh -NoProfile -File <scratchpad>/run-poshqc-test-openclaw.ps1`, importing the bundled `PoshQC.psd1` directly and calling `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.openclaw.runsettings.psd1` (same corrected-`CodeCoverage.Path` settings file used at P0-T5/P6-T3, listing all 30 current `scripts/**` production files including `scripts/Test-OpenClawBicepParameterSecrets.ps1`).
- EXIT_CODE (remediation): 0

## Root Cause (MCP tool failure, unchanged from P0-T5)

The bundled Pester settings file's `CodeCoverage.Path` is hardcoded to a fixed allowlist of `drm-copilot` repo files (e.g. `.claude/hooks/check-python-test-purity.ps1`), none of which exist in `open-claw-bridge`, so `Resolve-CoverageInfo` throws before any coverage result is produced. The `scan_folders` parameter only affects `Run.Path` (which tests execute), not `CodeCoverage.Path` (which lines are measured), so scoping to `tests/scripts` does not bypass the defect. This is not a defect in `Test-OpenClawBicepParameterSecrets.ps1` or its test.

## Remediation (F11 precedent, reproduced per P0-T5)

The same corrected-runsettings bundled-pipeline workaround documented at `evidence/baseline/poshqc-test.2026-07-07T01-30.md` was used. Full details (module path, corrected `CodeCoverage.Path` scope, scratchpad-only runsettings location) are recorded there and not repeated here.

## Test Additions Made to Reach the Coverage Threshold

The script's main entry-point block (`if ($MyInvocation.InvocationName -ne '.') { ... exit 0 / exit 1 }`) only executes when the script is invoked directly, not when dot-sourced. The original test file (5 tests, all dot-sourcing the script and calling the `Test-OpenClawBicepParameterSecrets` function directly) never exercised this block, leaving 7 of 38 lines (11 of 54 commands) uncovered — 81.58% line / 79.63% command coverage, below the 85% threshold.

Two tests were added to `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1` under a new `Context 'main entry point (script invoked directly, not dot-sourced)'`, invoking the script via `& $script:ScriptPath` (same pattern as this repo's existing `tests/scripts/Uninstall.Tests.ps1`) with `Test-Path`/`Get-ChildItem`/`Get-Content` mocked via Pester's `Mock` cmdlet — no real or temporary file is created:

1. **Clean/exit-0 branch**: mocks `Test-Path` to return `$false` for the default `deploy/azure/parameters` path, asserts `$LASTEXITCODE -eq 0` and the "Clean: scanned 0 parameter file(s)..." message.
2. **Dirty/exit-1 branch**: mocks `Test-Path`/`Get-ChildItem`/`Get-Content` to simulate one `.bicepparam` file containing a secret-shaped connection string, asserts `$LASTEXITCODE -eq 1` and a captured error record matching "Secret-shaped literal found".

Before authoring these tests, it was empirically verified (via scratch scripts, not committed) that in PowerShell 7, a script's top-level `exit` statement inside a call-operator (`&`) invocation terminates only that nested script invocation, not the calling process or Pester host — confirmed by running the equivalent pattern standalone and observing the caller continue executing afterward. This is why `& $script:ScriptPath` (same runspace as the rest of the test file, and the runspace Pester's coverage collector instruments) was used, rather than isolating the call in a separate runspace/process (which was tried first and found NOT to register with Pester's coverage instrumentation, since coverage tracing is scoped to the runspace it is attached to).

## Test Result

`Invoke-Pester` (via the corrected-runsettings bundled pipeline) against `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1`:
- **Tests Passed: 7, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0**, covering the original 5 scenarios (clean file, secret-shaped-value file, missing directory, empty directory, default parameter value) plus the 2 new main-entry-point scenarios (exit 0 / exit 1).

## Coverage Values

- Command/line coverage for `scripts/Test-OpenClawBicepParameterSecrets.ps1`: **100%** (INSTRUCTION: missed=0, covered=54; LINE: missed=0, covered=38) — measured via the repo-wide corrected-runsettings run recorded at P6-T3, in which this file is one of the 30 `CodeCoverage.Path` entries.
- Branch coverage: Pester v5 emits command-level coverage only, no separate branch-percentage metric (matches F11 precedent and `.claude/rules/powershell.md`'s Pester v5.x convention). The 100% command-coverage figure is the branch-sensitive signal; every branch arm in the script's `if`/`foreach`/pattern-match logic executed under the 7 tests.

## Disposition

Coverage for `scripts/Test-OpenClawBicepParameterSecrets.ps1` is **100%** line/command coverage, exceeding the 85% line and 75% branch (command-proxy) thresholds. Toolchain restarted from format after the test-file edit: format and analyze both passed clean (0 files changed, 0 diagnostics) before this coverage capture was taken. This task is **complete**, not remediation-required.
