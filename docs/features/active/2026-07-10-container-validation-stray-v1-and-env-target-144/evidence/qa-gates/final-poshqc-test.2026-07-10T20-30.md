# Final QC — PoshQC Test, full tests/scripts suite (Issue #144, P2-T3, AC4)

- Timestamp: 2026-07-10T20-30

## Attempt 1 — MCP tool, full suite

- Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root=`C:\Users\DanMoisan\repos\open-claw-bridge`, scan_folders=`["tests/scripts"]`)
- EXIT_CODE: 4294967295 (tool returned `"ok":false`)
- Output Summary: Reproduces the established bundled-runsettings coverage-path defect (`#111`, `#125`, `#135`, `#137`, `#139`, `#142`): the bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` entries that exist only in the `drm-copilot` source repository, so the MCP wrapper returns a failure exit even though the tests themselves pass. Pass/fail is captured authoritatively in Attempt 2.

## Attempt 2 — Direct Invoke-PoshQCTest, full suite (authoritative pass/fail)

- Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\repos\drm-copilot\packages\mcp-server\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root 'C:\Users\DanMoisan\repos\open-claw-bridge' -ScanFolders @('tests/scripts')"`
- EXIT_CODE: 0
- Output Summary: **Tests Passed: 416, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0.** Baseline (P0-T10) was 406 passing; the +10 delta is the 10 new tests added in Phase 1 (5 in `OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1`, 2 in `Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1`, 3 in `Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1`). The updated HostAdapter `/status`/`ExpectedCondition` assertions (P1-T10/P1-T11) and the updated aggregation-count assertions in `Invoke-OpenClawContainerPathValidation.Tests.ps1` (P1-T19: `SupportingDiagnostics` count 14 -> 15, `GatewayTokenInContainer` assertion, `EndpointDiagnostics` unchanged at 6) all pass. AC4 test half satisfied: full suite green in a single pass alongside clean format + analyze (P2-T1/P2-T2).
