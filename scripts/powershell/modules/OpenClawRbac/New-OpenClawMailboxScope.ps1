#Requires -Version 7
<#
.SYNOPSIS
Creates the group-based Exchange Online management scope for the OpenClaw
assistant (master checklist section 12 Step 3, Option A). Idempotent.
#>

<#
.SYNOPSIS
Creates the mailbox management scope based on direct membership in the
scoping group, or reports a no-op when the scope already exists.

.DESCRIPTION
Implements master checklist section 12 Step 3, Option A. Always warns that
only DIRECT group membership counts (nested members are not in scope) - the
warning is emitted on every invocation, including under -WhatIf. Looks up the
scope by name through Invoke-OpenClawGetManagementScope; when it exists the
function emits an informational message, performs no write, and returns the
existing scope. Otherwise it creates the scope through
Invoke-OpenClawNewManagementScope with the filter
"MemberOfGroup -eq '<GroupDistinguishedName>'" inside a ShouldProcess gate.

Option B (Administrative Unit) needs no scope object; callers skip this
function and use Grant-OpenClawRbacRoles -AdministrativeUnitId instead.

.PARAMETER Name
Name of the management scope. Defaults to 'OpenClaw-ScopedMailboxes'.

.PARAMETER GroupDistinguishedName
DistinguishedName (DN) of the scoping group. Only direct members of this
group are in scope.

.OUTPUTS
The existing or newly created management-scope object; nothing under -WhatIf.

.EXAMPLE
New-OpenClawMailboxScope -GroupDistinguishedName 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com'
#>
function New-OpenClawMailboxScope {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$Name = 'OpenClaw-ScopedMailboxes',

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$GroupDistinguishedName
    )

    Write-Warning -Message 'Only direct group membership counts for this management scope; nested group members are NOT in scope.'

    $existingScope = Invoke-OpenClawGetManagementScope -Identity $Name
    if ($existingScope) {
        $message = "Management scope '$Name' already exists; no changes made."
        Write-Information -MessageData $message
        Write-Verbose -Message $message
        return $existingScope
    }

    $recipientRestrictionFilter = "MemberOfGroup -eq '$GroupDistinguishedName'"
    $target = "Exchange Online management scope '$Name' (filter: $recipientRestrictionFilter)"
    if ($PSCmdlet.ShouldProcess($target, 'Create')) {
        return Invoke-OpenClawNewManagementScope `
            -Name $Name `
            -RecipientRestrictionFilter $recipientRestrictionFilter
    }
}
