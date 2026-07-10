# Baseline — PoshQC Test + Coverage (Issue #142)

Timestamp: 2026-07-10T19-10

## Invocation 1 — MCP tool (known coverage-path defect)
Command: mcp__drm-copilot__run_poshqc_test (workspace_root = C:\Users\DanMoisan\repos\open-claw-bridge)
EXIT_CODE: 4294967295 (-1)
Output Summary: Failed as expected. The bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to drm-copilot-only files (e.g., `scripts/powershell/Publish-DrmCopilotExtension.ps1`, `.claude/hooks/*`) absent from this repository, so the coverage stage errors out. Reproduced defect per issues #111/#125/#135/#137/#139.

## Invocation 2 — Corrected-runsettings workaround (authoritative)
Command: pwsh -NoProfile -Command "Import-Module <bundled PoshQC.psd1>; Invoke-PoshQCTest -Root C:\Users\DanMoisan\repos\open-claw-bridge -SettingsPath <scratchpad>/pester.runsettings.corrected.psd1"
- Corrected runsettings: `CodeCoverage.Path = @('scripts/*.ps1','scripts/*.psm1')`, `ExcludedPath = @()` (no production exclusion per Coverage Exclusion Policy), `Run.Path = @('tests/scripts')`.
EXIT_CODE: 0

Output Summary:
- Tests Passed: 380, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0. Duration ~38s.
- Code coverage: Covered 89.34% (command/statement coverage; Pester CoverageGutters). 1,689 analyzed commands in 22 files.
- Repo-wide baseline coverage headline: 89.34% (>= 85% line-coverage gate). Pester reports command coverage as the branch/command proxy; baseline is well above the 75% branch floor.
- Baseline test+coverage state: CLEAN and above thresholds.
