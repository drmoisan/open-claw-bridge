# Baseline — PoshQC Test-and-Coverage (Cycle 2, Pre-Fix)

- Timestamp: 2026-07-09T19-13

## Command 1 — MCP invocation (expected to fail on the known coverage-path defect)

- Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root = repo root)
- EXIT_CODE: 4294967295 (unsigned representation of process exit code -1)
- Output: `{"ok":false,"tool":"run_poshqc_test","summary":"Command exited with code 4294967295."}`. Confirms the established defect (F11 #111, F16 #125): the bundled `pester.runsettings.psd1`'s `CodeCoverage.Path` hardcodes files from the `drm-copilot` source repository that do not exist in `open-claw-bridge`, so Pester's coverage plugin fails at `RunStart`.

## Command 2 — corrected-runsettings workaround

- Command: `pwsh -NoProfile -ExecutionPolicy Bypass -File <scratchpad>/run-poshqc-test-135.ps1`, which imports `C:\Users\DanMoisan\.vscode-insiders\extensions\danmoisan.drm-copilot-1.0.12\resources\powershell\PoshQC\PoshQC.psd1` directly and calls `Invoke-PoshQCTest -Root C:\Users\DanMoisan\repos\open-claw-bridge -SettingsPath <scratchpad>/pester.runsettings.corrected.psd1`. The corrected settings file is a scratchpad-only copy of the bundled runsettings (`Run.Path` unchanged: `scripts`, `tests/powershell`, `tests/scripts`) with `CodeCoverage.Path` rewritten to this repository's 30 actual production `.ps1`/`.psm1` files under `scripts/**` (full glob, no subset) and no `ExcludedPath` entry, per the Coverage Exclusion Policy in `.claude/rules/general-unit-test.md`. Not written into the repo tree.
- EXIT_CODE: 0

## Command 3 — isolated repro (fail-before evidence for AC-7/AC-8)

- Command: `pwsh -NoProfile -Command "Import-Module './scripts/Publish.Env.psm1' -Force; Write-EnvFileContent -Path 'C:\fake\.env' -Content @('A=1','','B=2') -WhatIf"`
- EXIT_CODE: (terminating error caught) — confirmed to throw: `Cannot bind argument to parameter 'Content' because it is an empty string.`

## Output Summary

- Tests (pre-fix baseline, repo-wide): **Passed: 367, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0** (duration 31.15s). Matches this cycle's expected 367-passing baseline.
- Repo-wide baseline command/line coverage: **89.93%** (2,015 analyzed Commands in 30 Files; all `scripts/**` production `.ps1`/`.psm1` files, no `ExcludedPath` entries applied). Pester v5 emits command-level coverage only; the branch-coverage slot reports 0% (unused metric), so the 89.93% command-coverage figure is used as the branch-sensitive baseline signal, consistent with prior repository precedent (F11, F16, and cycle 1 of this same feature).
- Isolated repro confirms the pre-fix defect: `Write-EnvFileContent -Content @('A=1','','B=2') -WhatIf` throws `Cannot bind argument to parameter 'Content' because it is an empty string.` on the current (pre-fix) module, because `-Content` carries only `[AllowEmptyCollection()]`, not `[AllowEmptyString()]`. This is the fail-before evidence for AC-7/AC-8.

## Disposition

This baseline is complete: repo-wide command/line coverage is 89.93% across 30 production PowerShell files, with 367/367 tests passing, and the isolated repro confirms the pre-fix parameter-binding defect. This baseline is compared against the Phase 2 post-change figures (P2-T3/P2-T4).
