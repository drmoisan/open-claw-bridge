Timestamp: 2026-04-07T13:58:00Z
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root . -CoveragePaths @('./scripts/*.ps1') -TestResultOutputPath 'TestResults/qa-powershell/testResults.xml' -CoverageOutputPath 'TestResults/qa-powershell/coverage.json'"
EXIT_CODE: 0
Output Summary:
OverallLineCoverage: 75
TestResultPath: TestResults/qa-powershell/testResults.xml
CoverageArtifactPath: TestResults/qa-powershell/coverage.json
