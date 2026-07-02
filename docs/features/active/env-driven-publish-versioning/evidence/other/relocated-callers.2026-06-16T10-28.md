# Relocated-callers verification (Phase 3, P3-T6)

Timestamp: 2026-06-16T16-40
Command: `rg -n "Find-WindowsSdkTool|Get-StampedAppxManifestXml|Invoke-VersionStamp|Invoke-LayoutAssembly|Invoke-MakePri|Invoke-MakeAppx|Invoke-SignTool" scripts/ tests/ --glob '*.ps1' --glob '*.psm1'`
EXIT_CODE: 0

## Output Summary

Enumeration of every call/import site of the seven relocated Windows SDK / MSIX
functions (`Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`,
`Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`,
`Invoke-MakeAppx`, `Invoke-SignTool`) across `scripts/` and `tests/`. Doc/archive
and `testResults.xml` matches are excluded as they are not executable callers.

Executable call/import sites:

1. `scripts/Publish.Msix.psm1` — DEFINITIONS of all seven functions plus the
   internal `Find-WindowsSdkTool` calls within `Invoke-MakePri`,
   `Invoke-MakeAppx`, `Invoke-SignTool`. Exports all seven via
   `Export-ModuleMember`. (new module, P3-T1/P3-T4)

2. `scripts/Publish.ps1` — call sites for the relocated functions in the Main
   block. Resolves via the added `Import-Module ... Publish.Msix.psm1`
   (P3-T5). RESOLVED, non-orphaned.

3. `tests/scripts/Publish.Tests.ps1` — references the relocated function names
   only through caller-scope mocks (no `-ModuleName`): `Mock Invoke-VersionStamp`,
   `Mock Invoke-LayoutAssembly`, `Mock Invoke-MakePri`, `Mock Invoke-MakeAppx`,
   `Mock Invoke-SignTool` (lines 87-105), plus call-order assertions referencing
   the same names. These caller-scope mocks intercept the functions as invoked
   by `Publish.ps1` (which is dot-sourced / invoked via `& $ScriptPath`), and
   resolve through `Publish.ps1`'s `Publish.Msix.psm1` import added in P3-T5.
   EXPECTED RESOLVED caller-scope-mock site. Needs NO edit; not required to
   import `Publish.Msix.psm1`. (per plan P3-T6 delta)

4. `tests/scripts/Publish.Helpers.Tests.ps1` — currently still holds the
   relocated-function `Describe`/`Context` blocks and their shims. These are
   MOVED to `tests/scripts/Publish.Msix.Tests.ps1` in P3-T7. After the move
   this file retains only the `global:dotnet` shim for `Invoke-DotnetPublish`.

5. `tests/scripts/Publish.Msix.Tests.ps1` — NEW file (P3-T7) that imports
   `scripts/Publish.Msix.psm1` and contains the moved tests with the three
   `Find-WindowsSdkTool` mocks rebound to `-ModuleName Publish.Msix`.

## Verdict

No orphaned caller remains after the `Publish.Msix.psm1` import was added to
`scripts/Publish.ps1`. `tests/scripts/Publish.Tests.ps1` is explicitly recorded
as a resolved caller-scope-mock site that needs no edit.
