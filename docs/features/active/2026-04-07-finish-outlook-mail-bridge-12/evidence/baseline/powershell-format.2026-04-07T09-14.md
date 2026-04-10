Timestamp: 2026-04-07T09-14
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCFormat -Root ."
EXIT_CODE: 0
Output Summary:
- The baseline formatter run rewrote 6 PowerShell files.
- Wrapper diagnostic exposed and then informed a follow-up fix to the repo-local PoshQC exclusion helper for `.git`, `bin`, and `obj` path filtering.
- Changed: scripts/install-mailbridge.ps1
- Changed: scripts/powershell/PoshQC/PoshQC.psd1
- Changed: scripts/powershell/PoshQC/PoshQC.psm1
- Changed: scripts/register-mailbridge-task.ps1
- Changed: scripts/test-mailbridge.ps1
- Changed: scripts/uninstall-mailbridge.ps1
