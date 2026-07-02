#Requires -Version 7
<#
.SYNOPSIS
Root module for OpenClawRbac.

.DESCRIPTION
Dot-sources the sibling .ps1 files that implement the Exchange Online
Application RBAC setup for the OpenClaw assistant (issue #111): the nine
runtime-resolved wrapper seams plus one file per public function. Contains no
logic other than dot-sourcing; the manifest (OpenClawRbac.psd1) controls the
exported surface.
#>

$ErrorActionPreference = 'Stop'

$moduleFileNames = @(
    'OpenClawRbac.Seams.ps1',
    'Register-OpenClawServicePrincipal.ps1',
    'New-OpenClawMailboxScope.ps1',
    'Grant-OpenClawRbacRoles.ps1',
    'Set-OpenClawSendOnBehalf.ps1',
    'Test-OpenClawScopeBoundary.ps1'
)

foreach ($moduleFileName in $moduleFileNames) {
    . (Join-Path -Path $PSScriptRoot -ChildPath $moduleFileName)
}
