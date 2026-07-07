# Final Repo-Wide PoshQC Test-and-Coverage (P6-T3)

- Timestamp: 2026-07-07T02-50 (original MCP attempt); remediation capture 2026-07-07T04-40
- Command (MCP, still fails): `mcp__drm-copilot__run_poshqc_test` (workspace_root = repo root, full repo-default scope)
- EXIT_CODE (MCP): 4294967295 (unsigned representation of process exit code -1)
- Command (remediation, bundled-pipeline workaround): `pwsh -NoProfile -File <scratchpad>/run-poshqc-test-openclaw.ps1`, importing the bundled `PoshQC.psd1` directly and calling `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.openclaw.runsettings.psd1` (repo-default `Run.Path`; corrected `CodeCoverage.Path` listing all 30 current `scripts/**` production `.ps1`/`.psm1` files).
- EXIT_CODE (remediation): 0
- Type checking: not applicable for PowerShell per `.claude/rules/powershell.md` (skipped).

## Root Cause (MCP tool failure, unchanged from P0-T5/P5-T5)

The bundled Pester settings file's `CodeCoverage.Path` is hardcoded to a fixed allowlist of `drm-copilot` repo files that do not exist in `open-claw-bridge`; `Resolve-CoverageInfo` throws before any coverage result is produced. See `evidence/baseline/poshqc-test.2026-07-07T01-30.md` (P0-T5) for the full root-cause writeup, not repeated here.

## Remediation (F11 precedent, reproduced per P0-T5/P5-T5)

The same corrected-runsettings bundled-pipeline workaround was used, with `CodeCoverage.Path` corrected to this repository's 30 `scripts/**` production files (the 29 pre-existing files plus this feature's `scripts/Test-OpenClawBicepParameterSecrets.ps1`), and no `ExcludedPath` entries, per `.claude/rules/general-unit-test.md`'s Coverage Exclusion Policy.

## Test Result

`Invoke-PoshQCTest` (bundled pipeline, corrected settings) against the repo-default `Run.Path` (`scripts`, `tests/powershell`, `tests/scripts`):
- **Tests Passed: 365, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0** — the P0-T5 baseline's 358 tests, plus this feature's 5 original tests for `Test-OpenClawBicepParameterSecrets.ps1`, plus the 2 additional main-entry-point tests added at P5-T5 to reach the coverage threshold (358 + 5 + 2 = 365).

## Coverage Values

- Repo-wide post-change command/line coverage: **89.94%** (2,017 analyzed Commands in 30 Files; INSTRUCTION missed=203, covered=1814; LINE missed=151, covered=1431).
- Repo-wide post-change branch coverage: Pester v5 emits command-level coverage only, no separate branch-percentage metric (matches F11 precedent and `.claude/rules/powershell.md`'s Pester v5.x convention); the 89.94% command figure is the branch-sensitive post-change signal.
- `scripts/Test-OpenClawBicepParameterSecrets.ps1` command/line coverage: **100%** (INSTRUCTION missed=0, covered=54; LINE missed=0, covered=38) — see `evidence/qa-gates/bicep-secret-scan-poshqc-test.2026-07-07T02-35.md` (P5-T5) for the test additions that reached this figure.
- No production PowerShell file was excluded from measurement: the corrected `CodeCoverage.Path` lists all 30 `scripts/**` `.ps1`/`.psm1` files present in the repository at this commit (verified by direct enumeration: `Get-ChildItem -Path scripts -Recurse -Include *.ps1,*.psm1 -File` returns exactly 30 files, matching the settings list one-for-one); no `ExcludedPath` entries are present.

## Disposition

This task is **complete**, not remediation-required. Repo-wide post-change command coverage is 89.94% (up from the 89.66% baseline computed over 29 files — see the delta/threshold verification at P6-T4 for the like-for-like comparison), and the new script's own coverage is 100%, exceeding both the 85% line and 75% branch (command-proxy) thresholds.
