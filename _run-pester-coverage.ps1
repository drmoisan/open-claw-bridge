$config = New-PesterConfiguration
$config.Run.Path = 'tests/scripts/'
$config.Output.Verbosity = 'Detailed'
$config.CodeCoverage.Enabled = $true
$config.CodeCoverage.Path = @('./scripts/*.ps1')
$config.CodeCoverage.OutputPath = 'TestResults/qa-powershell/coverage.xml'
$config.TestResult.Enabled = $true
$config.TestResult.OutputPath = 'TestResults/qa-powershell/testResults.xml'
$config.Run.PassThru = $true
New-Item -ItemType Directory -Path 'TestResults/qa-powershell' -Force | Out-Null
$result = Invoke-Pester -Configuration $config
$covered = $result.CodeCoverage.CommandsExecutedCount
$total = $result.CodeCoverage.CommandsAnalyzedCount
$pct = if ($total -gt 0) { [math]::Round(($covered / $total) * 100, 2) } else { 100.0 }
Write-Output "OverallLineCoverage: $pct"
Write-Output "TestResultPath: TestResults/qa-powershell/testResults.xml"
Write-Output "CoverageOutputPath: TestResults/qa-powershell/coverage.xml"
