# AC11 — No Import-Module Reference to OpenClawContainerValidation in Install.ps1

Timestamp: 2026-07-12T10-30

Command: `Select-String -Path scripts/Install.ps1 -Pattern 'OpenClawContainerValidation'` (equivalently run via `grep -n "OpenClawContainerValidation" scripts/Install.ps1`)

EXIT_CODE: 1 (grep convention: exit 1 means zero matches found)

Output Summary: Zero matches. `scripts/Install.ps1` contains no reference to `OpenClawContainerValidation.psm1` or its `.psd1` anywhere in the file, confirming the ratified design decision (AC11): the Stage 9 guard (`Get-ComposeServiceImageTag`/`Assert-ComposeImageVersionAligned`) is implemented as a small, self-contained script-scope duplication rather than importing the module, because the module is not part of the bundle-staged file set produced by `Copy-InstallScriptsIntoBundle`.
