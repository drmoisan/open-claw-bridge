# Targeted Verification — P1-T2 through P1-T8 — Issue #135

Timestamp: 2026-07-07T15-42

## Commands

Command (MCP, expected to fail on the known coverage-path defect): `mcp__drm-copilot__run_poshqc_test` (workspace_root = `C:\Users\DanMoisan\repos\open-claw-bridge`, scan_folders = `["tests/scripts/Publish.Tests.ps1", "tests/scripts/New-MsixDevCert.Tests.ps1"]`)

EXIT_CODE (MCP): 4294967295 (unsigned representation of process exit code -1) — confirms the known coverage-path defect (F11 #111, F16 #125) reproduces identically in targeted-scope mode.

Command (corrected-runsettings workaround): `pwsh -NoProfile -ExecutionPolicy Bypass -File <scratchpad>/run-poshqc-test-targeted-135.ps1`, which imports the bundled `PoshQC.psd1` module directly and calls `Invoke-PoshQCTest -Root C:\Users\DanMoisan\repos\open-claw-bridge -SettingsPath <scratchpad>/pester.runsettings.targeted-135.psd1`.

The targeted settings file's `Run.Path` is `tests/scripts/Publish.Msix.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1` — the two edited test files plus `Publish.Msix.Tests.ps1`. `Publish.Msix.Tests.ps1` was included because narrowing `Run.Path` to only the two edited files reproduces an unrelated cross-file test-order dependency: `Publish.Tests.ps1`'s `Mock Invoke-VersionStamp` requires `Invoke-VersionStamp` (defined in `scripts/Publish.Msix.psm1`) to already be resolvable in the session, which the full repo-wide run satisfies incidentally via discovery order (`Publish.Msix.Tests.ps1` sorts before `Publish.Tests.ps1` alphabetically) but a narrow 2-file scope does not. This is a pre-existing test-file coupling unrelated to the P1-T2 through P1-T8 changes; including `Publish.Msix.Tests.ps1` in scope reproduces the same session state the full Phase 2 repo-wide run uses. `CodeCoverage.Path` lists the 5 production files these three test files exercise (`scripts/New-MsixDevCert.ps1`, `scripts/Publish.Env.psm1`, `scripts/Publish.Helpers.psm1`, `scripts/Publish.Msix.psm1`, `scripts/Publish.ps1`), no `ExcludedPath` entry.

EXIT_CODE (corrected-runsettings workaround): 0

## Output Summary

- MCP tool result (2-file scope): `ok=false`, `"summary":"Command exited with code 4294967295."` — confirms the known coverage-path defect reproduces in targeted mode as well.
- Corrected-runsettings workaround (3-file scope, `Publish.Msix.Tests.ps1` + the two edited files): `LASTEXITCODE=0`.
- Tests: **Passed: 54, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0** (duration 3.72s). This includes all pre-existing `It` blocks in `Publish.Tests.ps1` and `New-MsixDevCert.Tests.ps1` (unchanged behavior after the mock-parity fix) plus the two new regression `It` blocks added in P1-T7 (`Publish.Tests.ps1`) and P1-T8 (`New-MsixDevCert.Tests.ps1`), all passing.
- Targeted command/line coverage across the 5 in-scope production files: 63.3% (406 analyzed Commands in 5 Files) — expected to be lower than the repo-wide 89.94% baseline because this run measures coverage against only 5 files exercised by 3 test files, not all 30 production files exercised by the full 365+ test suite. Full repo-wide coverage is captured in Phase 2 (P2-T3/P2-T4).

## Disposition

Confirms the P1-T2 through P1-T8 edits (production call-site fixes, mock-parity fixes, new regression tests) are internally consistent: all tests in both edited files pass, including the two new multi-line `.env` regression tests. Proceeding to the full-repo Phase 2 QC loop.
