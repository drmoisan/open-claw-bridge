# Baseline — PoshQC Test, 4 existing Invoke-OpenClawContainerPathValidation*.Tests.ps1 files (Issue #144, P0-T9)

- Timestamp: 2026-07-10T20-30

## Attempt 1 — MCP tool, scoped to the four test files (as scoped in the plan text)

- Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root=`C:\Users\DanMoisan\repos\open-claw-bridge`, scan_folders=`["tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1", "tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1", "tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1", "tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1"]`)
- EXIT_CODE: 4294967295 (tool returned `"ok":false`)
- Output Summary: The MCP tool fails when `scan_folders` points at `*.Tests.ps1` paths directly (also reproduced when scoped to the `tests/scripts` directory, and when invoked with no `scan_folders` at all). Reproduces the established bundled-runsettings defect referenced in the plan Conventions (`#111`, `#125`, `#135`, `#137`, `#139`, `#142`): the bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` entries that exist only in the `drm-copilot` source repository (confirmed by reading `pester.runsettings.psd1` directly: it lists `.claude/hooks/check-python-test-purity.ps1` and similar paths absent from this repo). The MCP tool always runs with coverage enabled internally; this repo's missing coverage-path entries make the wrapped process return a non-zero/failure exit even though the underlying test run itself passes (confirmed in Attempt 2 below).

## Attempt 2 — Direct `PoshQC.psd1` module invocation (workaround), scoped to the four test files

- Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\repos\drm-copilot\packages\mcp-server\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root 'C:\Users\DanMoisan\repos\open-claw-bridge' -ScanFolders @('tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1','tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1','tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1','tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1')"`
- EXIT_CODE: 0
- Output Summary: Discovery found 14 tests in 4 files. **Tests Passed: 14, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0.** A non-fatal `Write-Error` was emitted for one unresolved default coverage path (`.claude/hooks/check-python-test-purity.ps1`, confirming the known defect) but did not affect the test run or exit code. This confirms, against the pre-change production code: the `HostAdapterInContainer` probe's `v1/status`-referencing assertions in `Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` pass, and the `SupportingDiagnostics` count assertion (`Should -Be 14`) in `Invoke-OpenClawContainerPathValidation.Tests.ps1` passes.
