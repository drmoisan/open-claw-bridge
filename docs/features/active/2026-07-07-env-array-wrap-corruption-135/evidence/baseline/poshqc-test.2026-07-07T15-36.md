# PowerShell Test-and-Coverage Baseline — Issue #135

Timestamp: 2026-07-07T15-36

Command (MCP, expected to fail on the known coverage-path defect): `mcp__drm-copilot__run_poshqc_test` (workspace_root = `C:\Users\DanMoisan\repos\open-claw-bridge`)

EXIT_CODE (MCP): 4294967295 (unsigned representation of process exit code -1)

Command (corrected-runsettings workaround): `pwsh -NoProfile -ExecutionPolicy Bypass -File <scratchpad>/run-poshqc-test-135.ps1`, which imports the bundled module `C:\Users\DanMoisan\.vscode-insiders\extensions\danmoisan.drm-copilot-1.0.11\resources\powershell\PoshQC\PoshQC.psd1` directly and calls `Invoke-PoshQCTest -Root C:\Users\DanMoisan\repos\open-claw-bridge -SettingsPath <scratchpad>/pester.runsettings.corrected.psd1`. The corrected settings file is a scratchpad-only copy of the bundled `pester.runsettings.psd1` (`Run.Path` unchanged: `scripts`, `tests/powershell`, `tests/scripts`) with `CodeCoverage.Path` rewritten to this repository's 30 actual production `.ps1`/`.psm1` files under `scripts/**` (full glob, no subset) and with no `ExcludedPath` entry, per `.claude/rules/general-unit-test.md`'s Coverage Exclusion Policy. The corrected settings file was not written into the repo tree.

EXIT_CODE (corrected-runsettings workaround): 0

## Root Cause of the MCP Failure (established, not re-diagnosed)

The MCP tool's bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to an allowlist of files that exist only in the `drm-copilot` source repository (e.g. `.claude/hooks/check-python-test-purity.ps1`, `scripts/dev-tools/Invoke-FullRelease.ps1`), none of which exist in `open-claw-bridge`. Pester 5's coverage plugin fails at `RunStart` ("Could not resolve coverage path") and the tool exits non-zero even though all tests pass. This is the same reproduced defect documented at F11 (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/baseline/poshqc-test.2026-07-02T17-25.md`) and F16 (`docs/features/active/2026-07-07-azure-bicep-iac-125/evidence/baseline/poshqc-test.2026-07-07T01-30.md`).

## Output Summary

- MCP tool result: `ok=false`, `"summary":"Command exited with code 4294967295."` — confirms the known coverage-path defect on this pre-fix baseline attempt.
- Corrected-runsettings workaround run (same `Invoke-PoshQCTest` code path, corrected coverage scope): `LASTEXITCODE=0`.
- Tests (pre-fix baseline, repo-wide): **Passed: 365, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0** (duration 30.32s). This confirms the plan's expectation that, at this pre-fix baseline, the existing tests PASS — the redundant `@()`-wrap bug in `scripts/Publish.ps1` and `scripts/New-MsixDevCert.ps1` is latent because the current test mocks (which return already-collapsed/space-joined content, not an array split across multiple `KEY=value` lines) mask it.
- Repo-wide baseline command/line coverage: **89.94%** (2,017 analyzed Commands in 30 Files; all `scripts/**` production `.ps1`/`.psm1` files, no `ExcludedPath` entries applied).
- Repo-wide baseline branch coverage: Pester v5 emits command-level coverage only and reports no separate branch-percentage metric for PowerShell (tool reported "89.94% / 0%" where the second value is the unused branch slot). Command coverage counts commands inside every branch arm, so untaken branch arms register as uncovered commands; the 89.94% command figure is used as the branch-sensitive baseline signal, consistent with prior repository precedent (F11, F16).
- Raw tool output (scratchpad-relative, ephemeral, written under the repo's gitignored `artifacts/pester/` per established precedent): `pester-junit.xml`, `powershell-coverage.xml` (CoverageGutters format), `powershell-coverage.koverage.xml`.

## Disposition

This baseline is complete: repo-wide command/line coverage is 89.94% across 30 production PowerShell files, with 365/365 tests passing. This baseline is compared against the Phase 2 post-change figures (P2-T3/P2-T4).
