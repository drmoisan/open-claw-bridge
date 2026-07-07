# Final PowerShell Test-and-Coverage Check — Issue #135

Timestamp: 2026-07-07T15-49

Command (MCP, expected to fail on the known coverage-path defect): `mcp__drm-copilot__run_poshqc_test` (workspace_root = `C:\Users\DanMoisan\repos\open-claw-bridge`)

EXIT_CODE (MCP): 4294967295 (unsigned representation of process exit code -1)

Command (corrected-runsettings workaround): `pwsh -NoProfile -ExecutionPolicy Bypass -File <scratchpad>/run-poshqc-test-135.ps1`, which imports the bundled module `C:\Users\DanMoisan\.vscode-insiders\extensions\danmoisan.drm-copilot-1.0.11\resources\powershell\PoshQC\PoshQC.psd1` directly and calls `Invoke-PoshQCTest -Root C:\Users\DanMoisan\repos\open-claw-bridge -SettingsPath <scratchpad>/pester.runsettings.corrected.psd1` — the identical corrected settings file used for the P0-T8 baseline (`Run.Path` unchanged: `scripts`, `tests/powershell`, `tests/scripts`; `CodeCoverage.Path` covering all 30 actual production `.ps1`/`.psm1` files under `scripts/**`, no `ExcludedPath` entry).

EXIT_CODE (corrected-runsettings workaround): 0

## Output Summary

- MCP tool result: `ok=false`, `"summary":"Command exited with code 4294967295."` — confirms the known coverage-path defect (F11 #111, F16 #125) reproduces identically post-change.
- Corrected-runsettings workaround run (same `Invoke-PoshQCTest` code path, same coverage scope as the P0-T8 baseline): `LASTEXITCODE=0`.
- Tests (post-change, repo-wide): **Passed: 367, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0** (duration 22.8s). This is the baseline's 365 tests plus the two new multi-line `.env` regression tests added in P1-T7 (`Publish.Tests.ps1`) and P1-T8 (`New-MsixDevCert.Tests.ps1`), all passing.
- Repo-wide post-change command/line coverage: **89.93%** (2,015 analyzed Commands in 30 Files; same 30 production `scripts/**` `.ps1`/`.psm1` files as the baseline, no `ExcludedPath` entries applied).
- Repo-wide post-change branch coverage: Pester v5 emits command-level coverage only, consistent with the baseline methodology (F11, F16 precedent); 89.93% command coverage is used as the branch-sensitive proxy.
- Raw tool output (scratchpad-relative, ephemeral, written under the repo's gitignored `artifacts/pester/`): `pester-junit.xml`, `powershell-coverage.xml` (CoverageGutters format), `powershell-coverage.koverage.xml`.

## Disposition

All tests pass repo-wide including the two new regression tests. No restart from P2-T1 was required (no test failures). Coverage delta vs. the P0-T8 baseline is analyzed in `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md` (P2-T4).
