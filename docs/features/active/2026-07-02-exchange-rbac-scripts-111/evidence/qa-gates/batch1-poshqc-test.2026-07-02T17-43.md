# QA Gate — Batch 1 PoshQC Test with Coverage

Timestamp: 2026-07-02T17-43
Command: mcp__drm-copilot__run_poshqc_test (scan_folders: ["tests/scripts/powershell/modules/OpenClawRbac"]) — failed with exit 4294967295 due to the known bundled-settings defect (foreign `CodeCoverage.Path` entries; see baseline `poshqc-test.2026-07-02T17-25.md`); then the same bundled pipeline executed directly: `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.batch1.runsettings.psd1` (Run.Path scoped to `tests/scripts/powershell/modules/OpenClawRbac`; coverage on `OpenClawRbac.psm1` + `OpenClawRbac.Seams.ps1`).
EXIT_CODE: 0
Output Summary:
- Tests Passed: 24, Failed: 0, Skipped: 0 (Batch 1: OpenClawRbac.Module.Tests.ps1 + OpenClawRbac.Seams.Tests.ps1).
- Line/command coverage over the Batch 1 module files: **98.85%** (87 analyzed Commands in 2 Files, 1 missed command in OpenClawRbac.Seams.ps1). Branch metric: not emitted by Pester v5 (command coverage is the branch-sensitive signal; see baseline artifact).
- Batch loop status: format (clean, 0 changes) -> analyze (0 diagnostics) -> test (pass) completed in a single uninterrupted pass; no restart required.
- Raw tool output: `artifacts/pester/pester-junit.openclawrbac.xml`, `artifacts/pester/openclawrbac-coverage.xml`.
