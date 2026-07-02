#Requires -Version 7
<#
.SYNOPSIS
Configures the assistant mailbox for Send on Behalf of a principal mailbox
(master checklist section 12 Step 5). Never configures Send As.
#>

<#
.SYNOPSIS
Grants the assistant mailbox FullAccess plus Send on Behalf for a principal
mailbox. Safe to re-run; never configures Send As.

.DESCRIPTION
Implements master checklist section 12 Step 5. For each principal mailbox
(pipeline input supported) the function calls
Invoke-OpenClawAddMailboxPermission (AccessRights FullAccess, InheritanceType
All, AutoMapping $false) and Invoke-OpenClawSetMailbox
(-GrantSendOnBehalfTo @{ Add = <assistant> }), each inside a ShouldProcess
gate so -WhatIf performs zero write calls. A re-run against an existing access
control entry is caught by a targeted match on the documented
existing-permission error and reported as a no-op; any other error re-throws.
Send As is never configured (administrative rule in the master checklist).

.PARAMETER PrincipalMailbox
SMTP address of the principal mailbox the assistant represents. Accepts
pipeline input for multiple principals.

.PARAMETER AssistantMailbox
SMTP address of the assistant mailbox receiving FullAccess and Send on Behalf.

.EXAMPLE
Set-OpenClawSendOnBehalf -PrincipalMailbox 'executive@contoso.com' -AssistantMailbox 'assistant@contoso.com'
#>
function Set-OpenClawSendOnBehalf {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
        [string]$PrincipalMailbox,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
        [string]$AssistantMailbox
    )

    process {
        $permissionTarget = "FullAccess on '$PrincipalMailbox' for '$AssistantMailbox' (InheritanceType All, AutoMapping off)"
        if ($PSCmdlet.ShouldProcess($permissionTarget, 'Grant mailbox permission')) {
            try {
                Invoke-OpenClawAddMailboxPermission `
                    -Identity $PrincipalMailbox `
                    -User $AssistantMailbox `
                    -AccessRights 'FullAccess' `
                    -InheritanceType 'All' `
                    -AutoMapping $false | Out-Null
            }
            catch {
                # Targeted idempotency catch: the documented Add-MailboxPermission
                # failure for an already-present access control entry reads
                # "An existing permission entry was found for user: <user>".
                # Anything else is a real failure and re-throws.
                if ($_.Exception.Message -like '*existing permission entry was found*') {
                    $message = "Mailbox permission for '$AssistantMailbox' on '$PrincipalMailbox' already exists; no changes made."
                    Write-Information -MessageData $message
                    Write-Verbose -Message $message
                }
                else {
                    throw
                }
            }
        }

        $sendOnBehalfTarget = "Send on Behalf on '$PrincipalMailbox' for '$AssistantMailbox'"
        if ($PSCmdlet.ShouldProcess($sendOnBehalfTarget, 'Grant Send on Behalf')) {
            # Set-Mailbox -GrantSendOnBehalfTo @{ Add = ... } is additive and
            # safe to re-run. Send As is never configured for this workflow.
            Invoke-OpenClawSetMailbox `
                -Identity $PrincipalMailbox `
                -GrantSendOnBehalfTo @{ Add = $AssistantMailbox } | Out-Null
        }
    }
}
