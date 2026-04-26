# Baseline — New-File Non-Collision Check

Timestamp: 2026-04-18T00-00
Command: `ls scripts/Install.ps1 scripts/Uninstall.ps1 scripts/Install.Helpers.psm1 tests/scripts/Install.Tests.ps1 tests/scripts/Uninstall.Tests.ps1 tests/scripts/Install.Helpers.Tests.ps1`
EXIT_CODE: 2 (all six paths absent as required)
Output Summary: All six target paths are absent before work begins. No collision with pre-existing files. The retained scheduled-task scripts (`scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`) use different casing and hyphenated names and are unaffected.

## Raw ls output

```
ls: cannot access 'scripts/Install.ps1': No such file or directory
ls: cannot access 'scripts/Uninstall.ps1': No such file or directory
ls: cannot access 'scripts/Install.Helpers.psm1': No such file or directory
ls: cannot access 'tests/scripts/Install.Tests.ps1': No such file or directory
ls: cannot access 'tests/scripts/Uninstall.Tests.ps1': No such file or directory
ls: cannot access 'tests/scripts/Install.Helpers.Tests.ps1': No such file or directory
```

## Pre-existing scripts surface (verified unchanged by the plan)

`scripts/` at baseline: `Build.ps1`, `New-MsixDevCert.ps1`, `Publish.Helpers.psm1`, `Publish.ps1`, `Run-Bridge.ps1`, `Run-Client.ps1`, `Test.ps1`, `dev-tools/`, `install-mailbridge.ps1`, `register-mailbridge-task.ps1`, `test-mailbridge.ps1`, `uninstall-mailbridge.ps1`.

Zero matches for each target path. Baseline clean.
