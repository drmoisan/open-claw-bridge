#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for Grant-OpenClawRbacRoles.

.DESCRIPTION
Covers the default four-role grant with exact role and assignment names, the
-IncludeCalendarWrite switch, parameter-set routing between -ScopeName
(-CustomResourceScope) and -AdministrativeUnitId
(-RecipientAdministrativeUnitScope), per-role idempotency, and -WhatIf
producing zero create invocations with Status WhatIf rows. Only the
Invoke-OpenClaw* wrapper seams are mocked.
#>

BeforeAll {
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force

    $script:appObjectId = '00000000-0000-0000-0000-000000000002'
    $script:adminUnitId = '00000000-0000-0000-0000-000000000003'
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'Grant-OpenClawRbacRoles parameter validation' {
    It 'rejects a malformed -EnterpriseApplicationObjectId at binding time' {
        { Grant-OpenClawRbacRoles -EnterpriseApplicationObjectId 'not-a-guid' -ScopeName 'S' } | Should -Throw
    }

    It 'rejects supplying both -ScopeName and -AdministrativeUnitId (mutually exclusive parameter sets)' {
        {
            Grant-OpenClawRbacRoles `
                -EnterpriseApplicationObjectId $script:appObjectId `
                -ScopeName 'S' `
                -AdministrativeUnitId $script:adminUnitId
        } | Should -Throw
    }

    It 'rejects supplying neither -ScopeName nor -AdministrativeUnitId' {
        { Grant-OpenClawRbacRoles -EnterpriseApplicationObjectId $script:appObjectId } | Should -Throw
    }
}

Describe 'Grant-OpenClawRbacRoles behavior' {
    BeforeEach {
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementRoleAssignment {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment {
            param(
                [string]$Name,
                [string]$App,
                [string]$Role,
                [string]$CustomResourceScope,
                [string]$RecipientAdministrativeUnitScope
            )
            # Reference parameters to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $App
            $null = $Role
            $null = $CustomResourceScope
            $null = $RecipientAdministrativeUnitScope
            [pscustomobject]@{ Name = $Name }
        }
    }

    It 'grants exactly the four minimum roles by default with exact role and assignment names' {
        # Act
        $rows = @(
            Grant-OpenClawRbacRoles `
                -EnterpriseApplicationObjectId $script:appObjectId `
                -ScopeName 'OpenClaw-ScopedMailboxes'
        )

        # Assert: exactly four rows in role order with prefixed assignment names.
        $rows.Count | Should -Be 4
        $rows[0].RoleName | Should -Be 'Application Mail.Read'
        $rows[0].AssignmentName | Should -Be 'OpenClaw-MailRead'
        $rows[1].RoleName | Should -Be 'Application Calendars.Read'
        $rows[1].AssignmentName | Should -Be 'OpenClaw-CalendarsRead'
        $rows[2].RoleName | Should -Be 'Application MailboxSettings.Read'
        $rows[2].AssignmentName | Should -Be 'OpenClaw-MailboxSettingsRead'
        $rows[3].RoleName | Should -Be 'Application Mail.Send'
        $rows[3].AssignmentName | Should -Be 'OpenClaw-MailSend'
        $rows | ForEach-Object { $_.Status | Should -Be 'Created' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 4 -Exactly
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 0 -Exactly `
            -ParameterFilter { $Role -eq 'Application Calendars.ReadWrite' }
    }

    It 'adds exactly one Calendars.ReadWrite grant when -IncludeCalendarWrite is set' {
        # Act
        $rows = @(
            Grant-OpenClawRbacRoles `
                -EnterpriseApplicationObjectId $script:appObjectId `
                -ScopeName 'OpenClaw-ScopedMailboxes' `
                -IncludeCalendarWrite
        )

        # Assert: five rows; the extra row is the calendar-write role.
        $rows.Count | Should -Be 5
        $rows[4].RoleName | Should -Be 'Application Calendars.ReadWrite'
        $rows[4].AssignmentName | Should -Be 'OpenClaw-CalendarsReadWrite'
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 1 -Exactly `
            -ParameterFilter { $Role -eq 'Application Calendars.ReadWrite' -and $Name -eq 'OpenClaw-CalendarsReadWrite' }
    }

    It 'honors a custom -RoleAssignmentPrefix' {
        # Act
        $rows = @(
            Grant-OpenClawRbacRoles `
                -EnterpriseApplicationObjectId $script:appObjectId `
                -ScopeName 'S' `
                -RoleAssignmentPrefix 'Contoso'
        )

        # Assert
        $rows[0].AssignmentName | Should -Be 'Contoso-MailRead'
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 1 -Exactly `
            -ParameterFilter { $Name -eq 'Contoso-MailRead' }
    }

    It 'routes -ScopeName to the CustomResourceScope argument on every create call' {
        # Act
        Grant-OpenClawRbacRoles `
            -EnterpriseApplicationObjectId $script:appObjectId `
            -ScopeName 'OpenClaw-ScopedMailboxes' | Out-Null

        # Assert: every create call used CustomResourceScope and never the AU scope.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 4 -Exactly `
            -ParameterFilter {
            $CustomResourceScope -eq 'OpenClaw-ScopedMailboxes' -and
            [string]::IsNullOrEmpty($RecipientAdministrativeUnitScope)
        }
    }

    It 'routes -AdministrativeUnitId to the RecipientAdministrativeUnitScope argument on every create call' {
        # Act
        Grant-OpenClawRbacRoles `
            -EnterpriseApplicationObjectId $script:appObjectId `
            -AdministrativeUnitId $script:adminUnitId | Out-Null

        # Assert
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 4 -Exactly `
            -ParameterFilter {
            $RecipientAdministrativeUnitScope -eq '00000000-0000-0000-0000-000000000003' -and
            [string]::IsNullOrEmpty($CustomResourceScope)
        }
    }

    It 'passes the app object id and checks each assignment by name before creating' {
        # Act
        Grant-OpenClawRbacRoles `
            -EnterpriseApplicationObjectId $script:appObjectId `
            -ScopeName 'S' | Out-Null

        # Assert: creates carry the app id; idempotency checks are name-scoped.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 4 -Exactly `
            -ParameterFilter { $App -eq '00000000-0000-0000-0000-000000000002' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawGetManagementRoleAssignment -Times 1 -Exactly `
            -ParameterFilter { $Identity -eq 'OpenClaw-MailRead' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawGetManagementRoleAssignment -Times 1 -Exactly `
            -ParameterFilter { $Identity -eq 'OpenClaw-MailSend' }
    }

    It 'reports AlreadyExists for an existing assignment and still creates the others' {
        # Arrange: only the MailRead assignment already exists.
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementRoleAssignment {
            param([string]$Identity)
            if ($Identity -eq 'OpenClaw-MailRead') {
                return [pscustomobject]@{ Name = 'OpenClaw-MailRead' }
            }
            $null
        }

        # Act
        $rows = @(
            Grant-OpenClawRbacRoles `
                -EnterpriseApplicationObjectId $script:appObjectId `
                -ScopeName 'S'
        )

        # Assert: one AlreadyExists row, three Created rows, no create for MailRead.
        ($rows | Where-Object { $_.Status -eq 'AlreadyExists' }).AssignmentName | Should -Be 'OpenClaw-MailRead'
        @($rows | Where-Object { $_.Status -eq 'Created' }).Count | Should -Be 3
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 0 -Exactly `
            -ParameterFilter { $Name -eq 'OpenClaw-MailRead' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 3 -Exactly
    }

    It 'performs zero create invocations and returns Status WhatIf rows under -WhatIf' {
        # Act
        $rows = @(
            Grant-OpenClawRbacRoles `
                -EnterpriseApplicationObjectId $script:appObjectId `
                -ScopeName 'S' `
                -WhatIf
        )

        # Assert: four dry-run rows and no write calls.
        $rows.Count | Should -Be 4
        $rows | ForEach-Object { $_.Status | Should -Be 'WhatIf' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementRoleAssignment -Times 0 -Exactly
    }
}

