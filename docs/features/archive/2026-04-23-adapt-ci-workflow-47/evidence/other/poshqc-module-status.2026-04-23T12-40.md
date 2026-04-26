---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command 'if (Test-Path -LiteralPath "scripts/powershell/PoshQC/PoshQC.psm1") { Import-Module -Force -Name (Resolve-Path "scripts/powershell/PoshQC/PoshQC.psm1"); Get-Command -Module PoshQC | Select-Object -ExpandProperty Name } else { "MODULE_ABSENT" }'
EXIT_CODE: 0
---

# PoshQC Module Status

Output:
```
MODULE_ABSENT
```

Output Summary:
- `scripts/powershell/PoshQC/PoshQC.psm1` does not exist on this branch.
- Confirmed by directory listing: `scripts/powershell/` contains only `modules/` (OpenClawContainerValidation), no PoshQC subfolder.
- AC-4 has been amended (issue.md) to require direct `Invoke-ScriptAnalyzer` + `Invoke-Pester` calls. Phase 2 PoshQC job will be written per the amended AC-4; the plan's `hashFiles()` guard variant is superseded by the amendment.
- This status will drive [P4-T5] to use the authorized skip branch (module absent).
