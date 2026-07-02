#Requires -Version 7
<#
.SYNOPSIS
Assigns the minimum Exchange Application RBAC roles to the OpenClaw assistant
app (master checklist section 12 Step 4). Idempotent per role assignment.
#>

<#
.SYNOPSIS
Assigns the four minimum Exchange Application RBAC roles (plus the optional
calendar-write role) to the assistant app, one idempotent assignment per role.

.DESCRIPTION
Implements master checklist section 12 Step 4. Iterates the four minimum
roles - Application Mail.Read, Application Calendars.Read,
Application MailboxSettings.Read, Application Mail.Send - and additionally
Application Calendars.ReadWrite only when -IncludeCalendarWrite is set. Each
assignment is checked by name through Invoke-OpenClawGetManagementRoleAssignment
before creation through Invoke-OpenClawNewManagementRoleAssignment inside a
ShouldProcess gate. Returns one summary object per role with RoleName,
AssignmentName, and Status (Created, AlreadyExists, or WhatIf).

.PARAMETER EnterpriseApplicationObjectId
The Enterprise Application service principal Object ID from Entra ID (NOT the
App Registration object ID).

.PARAMETER ScopeName
Name of the Exchange management scope (maps to -CustomResourceScope). Part of
the ByScopeName parameter set; mutually exclusive with -AdministrativeUnitId.

.PARAMETER AdministrativeUnitId
GUID of the Microsoft Entra Administrative Unit (maps to
-RecipientAdministrativeUnitScope). Part of the ByAdministrativeUnit parameter
set; mutually exclusive with -ScopeName.

.PARAMETER RoleAssignmentPrefix
Prefix for the role-assignment names. Defaults to 'OpenClaw' (producing e.g.
'OpenClaw-MailRead').

.PARAMETER IncludeCalendarWrite
When set, additionally assigns 'Application Calendars.ReadWrite' as
'<prefix>-CalendarsReadWrite'. Off by default per the master checklist.

.OUTPUTS
One [pscustomobject] per role: RoleName, AssignmentName, Status.

.EXAMPLE
Grant-OpenClawRbacRoles -EnterpriseApplicationObjectId $spObjectId -ScopeName 'OpenClaw-ScopedMailboxes'
#>
function Grant-OpenClawRbacRoles {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '',
        Justification = 'Function name is mandated by the issue #111 spec (D1); it grants multiple RBAC roles in one call.')]
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [guid]$EnterpriseApplicationObjectId,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByScopeName')]
        [ValidateNotNullOrEmpty()]
        [string]$ScopeName,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByAdministrativeUnit')]
        [guid]$AdministrativeUnitId,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$RoleAssignmentPrefix = 'OpenClaw',

        [Parameter()]
        [switch]$IncludeCalendarWrite
    )

    $roleDefinitions = @(
        @{ RoleName = 'Application Mail.Read'; AssignmentSuffix = 'MailRead' }
        @{ RoleName = 'Application Calendars.Read'; AssignmentSuffix = 'CalendarsRead' }
        @{ RoleName = 'Application MailboxSettings.Read'; AssignmentSuffix = 'MailboxSettingsRead' }
        @{ RoleName = 'Application Mail.Send'; AssignmentSuffix = 'MailSend' }
    )
    if ($IncludeCalendarWrite) {
        $roleDefinitions += @{ RoleName = 'Application Calendars.ReadWrite'; AssignmentSuffix = 'CalendarsReadWrite' }
    }

    foreach ($roleDefinition in $roleDefinitions) {
        $assignmentName = '{0}-{1}' -f $RoleAssignmentPrefix, $roleDefinition.AssignmentSuffix
        $existingAssignment = Invoke-OpenClawGetManagementRoleAssignment -Identity $assignmentName

        if ($existingAssignment) {
            $message = "Role assignment '$assignmentName' already exists; no changes made."
            Write-Information -MessageData $message
            Write-Verbose -Message $message
            $status = 'AlreadyExists'
        }
        else {
            $target = "Exchange role assignment '$assignmentName' ($($roleDefinition.RoleName)) for app $EnterpriseApplicationObjectId"
            if ($PSCmdlet.ShouldProcess($target, 'Create')) {
                $assignmentArguments = @{
                    Name = $assignmentName
                    App  = $EnterpriseApplicationObjectId.ToString()
                    Role = $roleDefinition.RoleName
                }
                if ($PSCmdlet.ParameterSetName -eq 'ByScopeName') {
                    $assignmentArguments.CustomResourceScope = $ScopeName
                }
                else {
                    $assignmentArguments.RecipientAdministrativeUnitScope = $AdministrativeUnitId.ToString()
                }
                Invoke-OpenClawNewManagementRoleAssignment @assignmentArguments | Out-Null
                $status = 'Created'
            }
            else {
                $status = 'WhatIf'
            }
        }

        [pscustomobject]@{
            RoleName       = $roleDefinition.RoleName
            AssignmentName = $assignmentName
            Status         = $status
        }
    }
}
