Timestamp: 2026-04-07T13:51:19.9638804Z
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCAnalyze -Root ."
EXIT_CODE: 1
Output Summary:
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\install-mailbridge.ps1:10:10 [Warning] Function 'New-DefaultBridgeSettings' has verb that could change system state. Therefore, the function has to support 'ShouldProcess'.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\install-mailbridge.ps1:10:10 [Warning] The cmdlet 'New-DefaultBridgeSettings' uses a plural noun. A singular noun should be used instead.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\install-mailbridge.ps1:14:5 [Information] The cmdlet 'New-DefaultBridgeSettings' returns an object of type 'System.Collections.Specialized.OrderedDictionary' but this type is not declared in the OutputType attribute.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\install-mailbridge.ps1:29:10 [Warning] The cmdlet 'Test-OutlookProfilePrerequisites' uses a plural noun. A singular noun should be used instead.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:6:10 [Warning] The cmdlet 'Get-PoshQCDiscoveredFiles' uses a plural noun. A singular noun should be used instead.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:37:5 [Information] The cmdlet 'Resolve-PoshQCOutputPath' returns an object of type 'System.String' but this type is not declared in the OutputType attribute.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:40:10 [Warning] The cmdlet 'Import-PoshQCSettings' uses a plural noun. A singular noun should be used instead.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:52:10 [Information] The cmdlet 'Install-PoshQCTools' does not have a help comment.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:52:10 [Warning] The cmdlet 'Install-PoshQCTools' uses a plural noun. A singular noun should be used instead.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:73:10 [Information] The cmdlet 'Invoke-PoshQCFormat' does not have a help comment.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:106:10 [Information] The cmdlet 'Invoke-PoshQCAnalyze' does not have a help comment.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:198:5 [Information] The cmdlet 'Convert-CodeCoverageToSummary' returns an object of type 'System.Collections.Specialized.OrderedDictionary' but this type is not declared in the OutputType attribute.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\powershell\PoshQC\PoshQC.psm1:205:10 [Information] The cmdlet 'Invoke-PoshQCTest' does not have a help comment.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1:3:13 [Warning] The parameter 'TaskName' has been declared but not used.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1:4:13 [Warning] The parameter 'ClientPath' has been declared but not used.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1:5:10 [Warning] The parameter 'ReadyTimeoutSeconds' has been declared but not used.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1:9:11 [Warning] The parameter 'OpenClawSvcPipeConnect' has been declared but not used.
C:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1:10:11 [Warning] The parameter 'NetworkDenyVerified' has been declared but not used.
Exception: PowerShell analysis failed with 18 finding(s).
