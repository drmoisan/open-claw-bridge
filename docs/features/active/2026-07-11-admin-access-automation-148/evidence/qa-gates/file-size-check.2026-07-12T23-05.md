# Final QC - File-Size Cap Check (AC-15)

Timestamp: 2026-07-12T23-05
Command: (Get-Content <file>).Count for each of the six new files
EXIT_CODE: 0

Output Summary: All six new files are <= 500 lines (cap per .claude/rules/general-code-change.md).

| File | Lines | <= 500 |
| --- | --- | --- |
| scripts/Get-OpenClawControlUiTokenUrl.ps1 | 73 | yes |
| scripts/Invoke-OpenClawDeviceTokenRotation.ps1 | 187 | yes |
| scripts/Set-OpenClawWebSearchProvider.ps1 | 143 | yes |
| tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1 | 155 | yes |
| tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1 | 229 | yes |
| tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1 | 180 | yes |

All three production scripts are PowerShell 7+ advanced functions with CmdletBinding()
(the two state-changing scripts use SupportsShouldProcess) and validated named parameters.
