# QA Gate — AC-1 Module-Surface Conformance

Timestamp: 2026-07-02T18-38
Command: pwsh script — Import-Module `scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1` then: Compare-Object of exported functions against the expected 14-name set; per-function CmdletBinding check via `FunctionInfo.CmdletBinding`; SupportsShouldProcess check via presence of the WhatIf parameter on the four state-changing functions; per-parameter validation check (ValidateArgumentsAttribute present or strongly typed [guid]/[bool]/[switch]); Select-String scan for tenant-specific values (org/user names, real GUIDs — module GUID line excluded); line-count check (<= 500) for all 17 feature files.
EXIT_CODE: 0
Output Summary: All AC-1 clauses PASS.
- Clause 1 (export surface exactly 5 public + 9 wrappers = 14): PASS (Compare-Object empty).
- Clause 2 (every exported function uses CmdletBinding): PASS.
- Clause 3 (every state-changing function declares SupportsShouldProcess — Register, New-Scope, Grant, Set-SendOnBehalf): PASS.
- Clause 4 (all non-common parameters carry validation attributes or strong typing): PASS.
- Clause 5 (no tenant-specific hardcoded values in production files): PASS (0 hits; placeholder examples live only in comment-based help with contoso.com placeholders).
- Clause 6 (every new file under 500 lines): PASS (largest: Test-OpenClawScopeBoundary.Tests.ps1 at 215 lines; largest production file: OpenClawRbac.Seams.ps1 at 197 lines).
