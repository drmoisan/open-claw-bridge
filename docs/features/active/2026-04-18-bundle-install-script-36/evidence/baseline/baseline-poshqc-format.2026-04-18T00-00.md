# Baseline — PoshQC Format Check

Timestamp: 2026-04-18T00-00
Command: `Invoke-PoshQCFormat -Root <repo> -ScanFolders @('scripts','tests')`
EXIT_CODE: 0
Output Summary: All existing PowerShell files under `scripts/` and `tests/` report "Already formatted" (20 files total: 12 scripts, 8 tests). No files flagged for formatting changes. Baseline clean.

## Full Output

```
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\Build.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\dev-tools\run-actionlint.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\install-mailbridge.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\New-MsixDevCert.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\Publish.Helpers.psm1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\Publish.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\register-mailbridge-task.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\Run-Bridge.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\Run-Client.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\Test.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\scripts\uninstall-mailbridge.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\install-mailbridge.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\New-MsixDevCert.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\Publish.Helpers.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\Publish.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\register-mailbridge-task.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\runner-scripts.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\test-mailbridge.Tests.ps1
Already formatted: C:\Users\DanMoisan\repos\open-claw-bridge\tests\scripts\uninstall-mailbridge.Tests.ps1
```
