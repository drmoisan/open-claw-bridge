Timestamp: 2026-04-07T09-15
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root . -CoveragePaths @('./scripts/*.ps1') -TestResultOutputPath 'TestResults/baseline-powershell/testResults.xml' -CoverageOutputPath 'TestResults/baseline-powershell/coverage.json'"
EXIT_CODE: 0
Output Summary:
- Test result artifact: TestResults/baseline-powershell/testResults.xml
- Coverage artifact: TestResults/baseline-powershell/coverage.json
- OverallLineCoverage: 100
- No PowerShell test files were found under `tests/` at baseline.
- Pester total: 0, passed: 0, failed: 0, skipped: 0
