# Final QA — PoshQC Test + Coverage (Issue #142, P5-T3)

Timestamp: 2026-07-10T19-10

## Invocation 1 — MCP tool (known coverage-path defect)
Command: mcp__drm-copilot__run_poshqc_test (workspace_root = repo root)
EXIT_CODE: 4294967295 (-1)
Output Summary: Fails on the bundled runsettings' hardcoded drm-copilot CodeCoverage.Path (reproduced defect #111/#125/#135/#137/#139).

## Invocation 2 — Corrected-runsettings workaround (authoritative)
Command: pwsh -NoProfile "Import-Module <bundled PoshQC.psd1>; Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.runsettings.corrected.psd1"
- Corrected runsettings: CodeCoverage.Path = @('scripts/*.ps1','scripts/*.psm1'); ExcludedPath = @(); Run.Path = @('tests/scripts').
EXIT_CODE: 0

Output Summary:
- Tests Passed: 406, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0.
- Repo-wide code coverage (command/statement proxy): 89.91% across 1,844 analyzed commands in 24 files (the 2 new modules are included; baseline measured 22 files at 89.34%).
- All new/updated suites (Publish.Docker, Install.Docker, Install.DockerStage, Publish.Tests, Publish.Helpers.Tests, Install.Tests, Install.Force.Tests) pass in the single full run.
