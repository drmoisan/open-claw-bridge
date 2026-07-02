#Requires -Version 7
<#
.SYNOPSIS
Wrapper seams for the nine Exchange Online cmdlets used by OpenClawRbac.

.DESCRIPTION
Each wrapper resolves its Exchange Online cmdlet at runtime via Get-Command so
machines without the ExchangeOnlineManagement module can parse, analyze, and
run the mocked Pester suite. There is no parse-time Import-Module of
ExchangeOnlineManagement anywhere in this module. Get-* wrappers return $null
when the object is not found (not-found is a data value, not an error). Unit
tests mock these wrappers only, never the Exchange cmdlets themselves.
#>

<#
.SYNOPSIS
Wraps New-ServicePrincipal (resolved at runtime).
#>
function Invoke-OpenClawNewServicePrincipal {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$AppId,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$ObjectId,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$DisplayName
    )
    $command = Get-Command -Name 'New-ServicePrincipal' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'New-ServicePrincipal'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ AppId = $AppId; ObjectId = $ObjectId; DisplayName = $DisplayName }
    return & $command @arguments
}

<#
.SYNOPSIS
Wraps Get-ServicePrincipal (resolved at runtime); returns $null when not found.
#>
function Invoke-OpenClawGetServicePrincipal {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Identity
    )
    $command = Get-Command -Name 'Get-ServicePrincipal' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'Get-ServicePrincipal'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Identity = $Identity; ErrorAction = 'SilentlyContinue' }
    $result = & $command @arguments
    if ($result) { return $result }
    return $null
}

<#
.SYNOPSIS
Wraps New-ManagementScope (resolved at runtime).
#>
function Invoke-OpenClawNewManagementScope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$RecipientRestrictionFilter
    )
    $command = Get-Command -Name 'New-ManagementScope' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'New-ManagementScope'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Name = $Name; RecipientRestrictionFilter = $RecipientRestrictionFilter }
    return & $command @arguments
}

<#
.SYNOPSIS
Wraps Get-ManagementScope (resolved at runtime); returns $null when not found.
#>
function Invoke-OpenClawGetManagementScope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Identity
    )
    $command = Get-Command -Name 'Get-ManagementScope' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'Get-ManagementScope'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Identity = $Identity; ErrorAction = 'SilentlyContinue' }
    $result = & $command @arguments
    if ($result) { return $result }
    return $null
}

<#
.SYNOPSIS
Wraps New-ManagementRoleAssignment (resolved at runtime).
#>
function Invoke-OpenClawNewManagementRoleAssignment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$App,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Role,
        [Parameter()][ValidateNotNullOrEmpty()][string]$CustomResourceScope,
        [Parameter()][ValidateNotNullOrEmpty()][string]$RecipientAdministrativeUnitScope
    )
    $command = Get-Command -Name 'New-ManagementRoleAssignment' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'New-ManagementRoleAssignment'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Name = $Name; App = $App; Role = $Role }
    if ($PSBoundParameters.ContainsKey('CustomResourceScope')) {
        $arguments.CustomResourceScope = $CustomResourceScope
    }
    if ($PSBoundParameters.ContainsKey('RecipientAdministrativeUnitScope')) {
        $arguments.RecipientAdministrativeUnitScope = $RecipientAdministrativeUnitScope
    }
    return & $command @arguments
}

<#
.SYNOPSIS
Wraps Get-ManagementRoleAssignment (resolved at runtime); returns $null when not found.
#>
function Invoke-OpenClawGetManagementRoleAssignment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Identity
    )
    $command = Get-Command -Name 'Get-ManagementRoleAssignment' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'Get-ManagementRoleAssignment'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Identity = $Identity; ErrorAction = 'SilentlyContinue' }
    $result = & $command @arguments
    if ($result) { return $result }
    return $null
}

<#
.SYNOPSIS
Wraps Add-MailboxPermission (resolved at runtime).
#>
function Invoke-OpenClawAddMailboxPermission {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Identity,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$User,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$AccessRights,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$InheritanceType,
        [Parameter(Mandatory = $true)][bool]$AutoMapping
    )
    $command = Get-Command -Name 'Add-MailboxPermission' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'Add-MailboxPermission'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{
        Identity        = $Identity
        User            = $User
        AccessRights    = $AccessRights
        InheritanceType = $InheritanceType
        AutoMapping     = $AutoMapping
    }
    return & $command @arguments
}

<#
.SYNOPSIS
Wraps Set-Mailbox (resolved at runtime).
#>
function Invoke-OpenClawSetMailbox {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Identity,
        [Parameter(Mandatory = $true)][ValidateNotNull()][hashtable]$GrantSendOnBehalfTo
    )
    $command = Get-Command -Name 'Set-Mailbox' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'Set-Mailbox'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Identity = $Identity; GrantSendOnBehalfTo = $GrantSendOnBehalfTo }
    return & $command @arguments
}

<#
.SYNOPSIS
Wraps Test-ServicePrincipalAuthorization (resolved at runtime).
#>
function Invoke-OpenClawTestServicePrincipalAuthorization {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Identity,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Resource
    )
    $command = Get-Command -Name 'Test-ServicePrincipalAuthorization' -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Cannot resolve the Exchange Online cmdlet 'Test-ServicePrincipalAuthorization'. Install the ExchangeOnlineManagement module and run Connect-ExchangeOnline before invoking this function."
    }
    $arguments = @{ Identity = $Identity; Resource = $Resource }
    return & $command @arguments
}
