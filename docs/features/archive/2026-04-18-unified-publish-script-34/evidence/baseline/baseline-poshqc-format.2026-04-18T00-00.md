# Baseline PoshQC Format

- Timestamp: 2026-04-18T00-00
- Command: `Invoke-PoshQCFormat -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')` (equivalent of `mcp__drmCopilotExtension__run_poshqc_format`; MCP tool resolves to this module invocation)
- EXIT_CODE: 0
- Output Summary: PASS. 18 PowerShell files scanned under `scripts/` and `tests/scripts/`. Every file reported "Already formatted"; no file was modified by the formatter. Scope includes `scripts/build-msix.ps1` and `tests/scripts/build-msix.Tests.ps1` (both within this scan).

## Files reported as already formatted

- `scripts/build-msix.ps1`
- `scripts/Build.ps1`
- `scripts/dev-tools/run-actionlint.ps1`
- `scripts/install-mailbridge.ps1`
- `scripts/New-MsixDevCert.ps1`
- `scripts/register-mailbridge-task.ps1`
- `scripts/Run-Bridge.ps1`
- `scripts/Run-Client.ps1`
- `scripts/test-mailbridge.ps1`
- `scripts/Test.ps1`
- `scripts/uninstall-mailbridge.ps1`
- `tests/scripts/build-msix.Tests.ps1`
- `tests/scripts/install-mailbridge.Tests.ps1`
- `tests/scripts/New-MsixDevCert.Tests.ps1`
- `tests/scripts/register-mailbridge-task.Tests.ps1`
- `tests/scripts/runner-scripts.Tests.ps1`
- `tests/scripts/test-mailbridge.Tests.ps1`
- `tests/scripts/uninstall-mailbridge.Tests.ps1`
