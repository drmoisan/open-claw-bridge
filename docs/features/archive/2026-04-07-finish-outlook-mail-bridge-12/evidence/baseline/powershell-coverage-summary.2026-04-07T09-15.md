Timestamp: 2026-04-07T09-15
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$base = git merge-base HEAD main; $coverage = Get-Content 'TestResults/baseline-powershell/coverage.json' -Raw | ConvertFrom-Json; $overall = [math]::Round([double]$coverage.OverallLineCoverage, 2); $changedScripts = @(git diff --name-only $base -- 'scripts/*.ps1' | Where-Object { $_ }); $changedTotal = 0; $changedHit = 0; foreach ($path in $changedScripts) { $file = $coverage.Files | Where-Object { $_.Path -eq $path -or $_.Path -like ('*' + $path) } | Select-Object -First 1; if ($file) { foreach ($line in $file.Lines) { $changedTotal++; if ([bool]$line.Hit) { $changedHit++ } } } }; $changedPct = if ($changedTotal -gt 0) { [math]::Round(($changedHit / $changedTotal) * 100, 2) } else { 100.0 }; Write-Output \"BaselineOverallLineCoverage: $overall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"CoverageArtifactPath: TestResults/baseline-powershell/coverage.json\""
EXIT_CODE: 0
Output Summary:
- BaselineOverallLineCoverage: 100
- ChangedOrNewLineCoverage: 100
- CoverageArtifactPath: TestResults/baseline-powershell/coverage.json
BaselineOverallLineCoverage: 100
ChangedOrNewLineCoverage: 100
CoverageArtifactPath: TestResults/baseline-powershell/coverage.json
