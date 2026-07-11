# Baseline — PoshQC Test-and-Coverage (Issue #144, P0-T10)

- Timestamp: 2026-07-10T20-30

## Attempt 1 — MCP tool, coverage mode, scoped to the two production files

- Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root=`C:\Users\DanMoisan\repos\open-claw-bridge`, scan_folders=`["scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1", "scripts/Invoke-OpenClawContainerPathValidation.ps1"]`)
- EXIT_CODE: 0 (tool returned `"ok":true`)
- Output Summary: This particular scoping (production-file paths passed to `scan_folders`) returned success. Separately, the same MCP tool reproducibly fails (`EXIT_CODE: 4294967295`, `"ok":false`) when `scan_folders` is scoped to `*.Tests.ps1` paths, the `tests/scripts` directory, or omitted entirely (see `poshqc-test.2026-07-10T20-30.md`), reproducing the established bundled-runsettings defect (`#111`, `#125`, `#135`, `#137`, `#139`, `#142`). Because the MCP tool does not surface per-file numeric coverage percentages in its response in either case, the numeric coverage values below are captured via the corrected-runsettings workaround (Attempt 2), per the plan's established convention.

## Attempt 2 — Corrected-runsettings `Invoke-PoshQCTest` workaround (numeric coverage source)

- Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\repos\drm-copilot\packages\mcp-server\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root 'C:\Users\DanMoisan\repos\open-claw-bridge' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'"` where `<scratchpad>\pester.runsettings.corrected.psd1` is a SCRATCHPAD-only copy of the bundled runsettings with `CodeCoverage.Path` rewritten to exactly the two in-scope production files (`scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, `scripts/Invoke-OpenClawContainerPathValidation.ps1`) and an empty `ExcludedPath` (no production file excluded from measurement, per the Coverage Exclusion Policy).
- EXIT_CODE: 0
- Output Summary: Full `tests/scripts` suite run (`Run.Path = @('tests/scripts')` in the corrected settings). **Tests Passed: 406, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0.** Coverage result parsed from `artifacts/pester/powershell-coverage.xml` (JaCoCo format; Pester's built-in PowerShell coverage tool does not produce a branch-coverage counter — only `INSTRUCTION`, `LINE`, `METHOD`, `CLASS`):
  - Aggregate `<report>`-level counters across both production files: `LINE missed="23" covered="255"` -> **line coverage = 255/278 = 91.73%**.
  - Aggregate `<report>`-level counters: `INSTRUCTION missed="34" covered="347"` -> **command/instruction coverage (used as the branch-coverage proxy per established repository precedent, e.g. `docs/features/active/2026-07-07-env-array-wrap-corruption-135/evidence/qa-gates/coverage-comparison.2026-07-09T19-13.md`) = 347/381 = 91.08%**.
  - Both numeric values are baseline (pre-Phase-1-change) measurements for the two production files.

## Baseline Coverage Summary (numeric, no placeholders)

| Metric | Value |
|---|---|
| Line coverage (2 production files) | 91.73% (255/278) |
| Command/instruction coverage (branch-coverage proxy) | 91.08% (347/381) |
| Full `tests/scripts` suite pass/fail | 406 passed / 0 failed |
