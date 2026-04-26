Timestamp: 2026-04-25T00:00Z
Command: mcp_drmcopilotext_run_poshqc_test
EXIT_CODE: 1
Failure: calls helpers in the correct order — Expected sequence Get-ManifestVersion,Test-ManifestIntegrity,Test-DockerAvailable,Copy-BundleContents,Initialize-DotEnv,Invoke-MsixInstall,Invoke-MsixCapture,Invoke-ComposeUp,Wait-ComposeHealthy,Write-InstallRecord but Invoke-WebRequest appears after Invoke-MsixInstall in the current call log. Correctly fails against pre-fix production code confirming preflight runs after MSIX install.
