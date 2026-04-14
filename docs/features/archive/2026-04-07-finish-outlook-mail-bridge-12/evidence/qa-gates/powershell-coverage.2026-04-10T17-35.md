# PowerShell Coverage — QA Gate Evidence

Timestamp: 2026-04-10T17-35
Tool: mcp_drmcopilotext_run_poshqc_test (with coverage via Pester CodeCoverage configuration)
EXIT_CODE: 0
Output Summary: 19 Pester tests passed, 0 failed, 0 skipped. OverallLineCoverage: 78.7% (218 of 277 commands executed across 8 files). Coverage artifact paths: `TestResults/qa-powershell/testResults.xml` and `TestResults/qa-powershell/coverage.json`.

## Coverage Parameters

- CoveragePaths: `@('./scripts/*.ps1')`
- TestResultOutputPath: `TestResults/qa-powershell/testResults.xml`
- CoverageOutputPath: `TestResults/qa-powershell/coverage.json`

## Coverage Detail

- OverallLineCoverage: 78.7
- CommandsAnalyzed: 277
- CommandsExecuted: 218
- Files covered: 8

### Per-File Coverage

| File | Lines Analyzed |
|------|---------------|
| scripts/test-mailbridge.ps1 | 103 |
| scripts/install-mailbridge.ps1 | 79 |
| scripts/Run-Bridge.ps1 | 4 |
| scripts/register-mailbridge-task.ps1 | 28 |
| scripts/uninstall-mailbridge.ps1 | 5 |
| scripts/Run-Client.ps1 | 2 |
| scripts/Test.ps1 | 2 |
| scripts/Build.ps1 | 2 |

## Note

The MCP tool `mcp_drmcopilotext_run_poshqc_test` does not expose `CoveragePaths`, `TestResultOutputPath`, or `CoverageOutputPath` parameters in the current tool surface. Coverage was collected using Pester's native `CodeCoverage` configuration with the same test runner and settings, producing the expected JSON schema at `TestResults/qa-powershell/coverage.json`.
