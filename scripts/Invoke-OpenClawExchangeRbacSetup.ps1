#Requires -Version 7
<#
.SYNOPSIS
Thin entry script sequencing the OpenClawRbac Exchange Application RBAC setup
(master checklist section 12 Steps 2-5 and 7).

.DESCRIPTION
Imports the OpenClawRbac module by relative path and sequences:
Register-OpenClawServicePrincipal -> New-OpenClawMailboxScope (Option A only;
skipped when -AdministrativeUnitId is supplied) -> Grant-OpenClawRbacRoles ->
Set-OpenClawSendOnBehalf (repeated per principal mailbox) ->
Test-OpenClawScopeBoundary. Forwards -WhatIf to every state-changing call and
maps the boundary result to the process exit code (Succeeded true -> 0,
otherwise 1). Contains no logic beyond sequencing, parameter forwarding, and
exit-code mapping; no tenant values are hardcoded.

.PARAMETER AppId
Client / Application ID (GUID) of the app registration.

.PARAMETER EnterpriseApplicationObjectId
Enterprise Application service principal Object ID (GUID) from Entra ID (NOT
the App Registration object ID).

.PARAMETER DisplayName
Display name for the service-principal pointer. Defaults to 'OpenClaw Assistant'.

.PARAMETER ScopeName
Management scope name (ByScopeName set). Defaults to 'OpenClaw-ScopedMailboxes'.

.PARAMETER GroupDistinguishedName
DN of the scoping group (ByScopeName set; only direct members are in scope).

.PARAMETER AdministrativeUnitId
Administrative Unit GUID (ByAdministrativeUnit set); skips scope creation.

.PARAMETER RoleAssignmentPrefix
Prefix for role-assignment names. Defaults to 'OpenClaw'.

.PARAMETER IncludeCalendarWrite
Additionally grants 'Application Calendars.ReadWrite'. Off by default.

.PARAMETER PrincipalMailbox
One or more principal mailbox SMTP addresses the assistant represents.

.PARAMETER AssistantMailbox
Assistant mailbox SMTP address.

.PARAMETER InScopeMailbox
Known in-scope mailbox for positive boundary testing.

.PARAMETER OutOfScopeMailbox
Known out-of-scope mailbox for negative boundary testing.

.EXAMPLE
./scripts/Invoke-OpenClawExchangeRbacSetup.ps1 -AppId $appId -EnterpriseApplicationObjectId $spObjectId -GroupDistinguishedName $dn -PrincipalMailbox 'executive@contoso.com' -AssistantMailbox 'assistant@contoso.com' -InScopeMailbox 'in@contoso.com' -OutOfScopeMailbox 'out@contoso.com' -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [guid]$AppId,

    [Parameter(Mandatory = $true)]
    [guid]$EnterpriseApplicationObjectId,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DisplayName = 'OpenClaw Assistant',

    [Parameter(ParameterSetName = 'ByScopeName')]
    [ValidateNotNullOrEmpty()]
    [string]$ScopeName = 'OpenClaw-ScopedMailboxes',

    [Parameter(Mandatory = $true, ParameterSetName = 'ByScopeName')]
    [ValidateNotNullOrEmpty()]
    [string]$GroupDistinguishedName,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByAdministrativeUnit')]
    [guid]$AdministrativeUnitId,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$RoleAssignmentPrefix = 'OpenClaw',

    [Parameter()]
    [switch]$IncludeCalendarWrite,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string[]]$PrincipalMailbox,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
    [string]$AssistantMailbox,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
    [string]$InScopeMailbox,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
    [string]$OutOfScopeMailbox
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path -Path $PSScriptRoot -ChildPath 'powershell/modules/OpenClawRbac/OpenClawRbac.psd1')

Register-OpenClawServicePrincipal `
    -AppId $AppId `
    -EnterpriseApplicationObjectId $EnterpriseApplicationObjectId `
    -DisplayName $DisplayName `
    -WhatIf:$WhatIfPreference | Out-Null

if ($PSCmdlet.ParameterSetName -eq 'ByScopeName') {
    New-OpenClawMailboxScope `
        -Name $ScopeName `
        -GroupDistinguishedName $GroupDistinguishedName `
        -WhatIf:$WhatIfPreference | Out-Null

    Grant-OpenClawRbacRoles `
        -EnterpriseApplicationObjectId $EnterpriseApplicationObjectId `
        -ScopeName $ScopeName `
        -RoleAssignmentPrefix $RoleAssignmentPrefix `
        -IncludeCalendarWrite:$IncludeCalendarWrite `
        -WhatIf:$WhatIfPreference
}
else {
    Grant-OpenClawRbacRoles `
        -EnterpriseApplicationObjectId $EnterpriseApplicationObjectId `
        -AdministrativeUnitId $AdministrativeUnitId `
        -RoleAssignmentPrefix $RoleAssignmentPrefix `
        -IncludeCalendarWrite:$IncludeCalendarWrite `
        -WhatIf:$WhatIfPreference
}

foreach ($currentPrincipalMailbox in $PrincipalMailbox) {
    Set-OpenClawSendOnBehalf `
        -PrincipalMailbox $currentPrincipalMailbox `
        -AssistantMailbox $AssistantMailbox `
        -WhatIf:$WhatIfPreference
}

$boundaryResult = Test-OpenClawScopeBoundary `
    -EnterpriseApplicationObjectId $EnterpriseApplicationObjectId `
    -InScopeMailbox $InScopeMailbox `
    -OutOfScopeMailbox $OutOfScopeMailbox

$boundaryResult

if ($boundaryResult.Succeeded -eq $true) {
    exit 0
}
exit 1
