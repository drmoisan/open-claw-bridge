#Requires -Version 7
<#
.SYNOPSIS
Registers the Exchange Online service-principal pointer for the OpenClaw
assistant app (master checklist section 12 Step 2). Idempotent.
#>

<#
.SYNOPSIS
Creates the Exchange Online service-principal pointer for the assistant app,
or reports a no-op when it already exists.

.DESCRIPTION
Implements master checklist section 12 Step 2. Looks up the service principal
by AppId through Invoke-OpenClawGetServicePrincipal; when it already exists
the function emits an informational message, performs no write, and returns
the existing object. Otherwise it creates the pointer through
Invoke-OpenClawNewServicePrincipal inside a ShouldProcess gate, so -WhatIf
performs zero write calls.

.PARAMETER AppId
The Client / Application ID (GUID) of the app registration.

.PARAMETER EnterpriseApplicationObjectId
The Enterprise Application service principal Object ID from Entra ID. WARNING:
this is the Object ID of the Enterprise Application (service principal) object
in the tenant, NOT the App Registration object ID. Confusing the two silently
mis-registers the service principal.

.PARAMETER DisplayName
Display name for the Exchange Online service-principal pointer. Defaults to
'OpenClaw Assistant'.

.OUTPUTS
The existing or newly created service-principal object; nothing under -WhatIf.

.EXAMPLE
Register-OpenClawServicePrincipal -AppId $appId -EnterpriseApplicationObjectId $spObjectId -WhatIf
#>
function Register-OpenClawServicePrincipal {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [guid]$AppId,

        [Parameter(Mandatory = $true)]
        [guid]$EnterpriseApplicationObjectId,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$DisplayName = 'OpenClaw Assistant'
    )

    $existingServicePrincipal = Invoke-OpenClawGetServicePrincipal -Identity $AppId.ToString()
    if ($existingServicePrincipal) {
        $message = "Service principal for AppId '$AppId' already exists; no changes made."
        Write-Information -MessageData $message
        Write-Verbose -Message $message
        return $existingServicePrincipal
    }

    $target = "Exchange Online service principal '$DisplayName' (AppId $AppId)"
    if ($PSCmdlet.ShouldProcess($target, 'Create')) {
        return Invoke-OpenClawNewServicePrincipal `
            -AppId $AppId.ToString() `
            -ObjectId $EnterpriseApplicationObjectId.ToString() `
            -DisplayName $DisplayName
    }
}
