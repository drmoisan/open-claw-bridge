#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for New-OpenClawMailboxScope.

.DESCRIPTION
Covers DN parameter validation, the existing-scope idempotent short-circuit,
the creation path's exact wrapper arguments (including the MemberOfGroup
filter), the unconditional direct-membership warning, and -WhatIf producing
zero create invocations. Only the Invoke-OpenClaw* wrapper seams are mocked.
#>

BeforeAll {
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force

    $script:groupDistinguishedName = 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com'
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'New-OpenClawMailboxScope parameter validation' {
    It 'rejects an empty -GroupDistinguishedName at binding time' {
        { New-OpenClawMailboxScope -GroupDistinguishedName '' -WarningAction SilentlyContinue } | Should -Throw
    }

    It 'rejects an empty -Name at binding time' {
        {
            New-OpenClawMailboxScope -Name '' -GroupDistinguishedName $script:groupDistinguishedName -WarningAction SilentlyContinue
        } | Should -Throw
    }
}

Describe 'New-OpenClawMailboxScope behavior' {
    BeforeEach {
        Mock -ModuleName OpenClawRbac Invoke-OpenClawNewManagementScope {
            param([string]$Name, [string]$RecipientRestrictionFilter)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $RecipientRestrictionFilter
            [pscustomobject]@{ Created = $true; Name = $Name }
        }
    }

    It 'short-circuits as a reported no-op when the scope already exists' {
        # Arrange: the Get wrapper reports an existing scope.
        $existingScope = [pscustomobject]@{ Name = 'OpenClaw-ScopedMailboxes' }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementScope {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $existingScope
        }

        # Act
        $result = New-OpenClawMailboxScope `
            -GroupDistinguishedName $script:groupDistinguishedName `
            -InformationVariable capturedInformation `
            -WarningAction SilentlyContinue

        # Assert: existing scope returned, informational message emitted, no write.
        $result.Name | Should -Be 'OpenClaw-ScopedMailboxes'
        "$capturedInformation" | Should -BeLike '*already exists*'
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementScope -Times 0 -Exactly
    }

    It 'creates the scope with the exact Name and MemberOfGroup filter arguments' {
        # Arrange: no existing scope.
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementScope {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }
        # Act
        $result = New-OpenClawMailboxScope `
            -Name 'Custom-Scope' `
            -GroupDistinguishedName $script:groupDistinguishedName `
            -WarningAction SilentlyContinue

        # Assert: exact named arguments forwarded to the create wrapper.
        $result.Created | Should -BeTrue
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementScope -Times 1 -Exactly `
            -ParameterFilter {
            $Name -eq 'Custom-Scope' -and
            $RecipientRestrictionFilter -eq "MemberOfGroup -eq 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com'"
        }
    }

    It 'defaults the scope name to OpenClaw-ScopedMailboxes' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementScope {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        New-OpenClawMailboxScope `
            -GroupDistinguishedName $script:groupDistinguishedName `
            -WarningAction SilentlyContinue | Out-Null

        # Assert
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementScope -Times 1 -Exactly `
            -ParameterFilter { $Name -eq 'OpenClaw-ScopedMailboxes' }
    }

    It 'emits the direct-membership warning on the creation path' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementScope {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        New-OpenClawMailboxScope `
            -GroupDistinguishedName $script:groupDistinguishedName `
            -WarningVariable capturedWarnings `
            -WarningAction SilentlyContinue | Out-Null

        # Assert: warning names the direct-membership limitation.
        "$capturedWarnings" | Should -BeLike '*direct group membership*'
        "$capturedWarnings" | Should -BeLike '*NOT in scope*'
    }

    It 'emits the direct-membership warning on the idempotent no-op path' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementScope {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            [pscustomobject]@{ Name = 'OpenClaw-ScopedMailboxes' }
        }

        # Act
        New-OpenClawMailboxScope `
            -GroupDistinguishedName $script:groupDistinguishedName `
            -WarningVariable capturedWarnings `
            -WarningAction SilentlyContinue | Out-Null

        # Assert
        "$capturedWarnings" | Should -BeLike '*direct group membership*'
    }

    It 'emits the direct-membership warning and performs zero create invocations under -WhatIf' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetManagementScope {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        New-OpenClawMailboxScope `
            -GroupDistinguishedName $script:groupDistinguishedName `
            -WarningVariable capturedWarnings `
            -WarningAction SilentlyContinue `
            -WhatIf

        # Assert: warning still emitted; dry run makes no write call.
        "$capturedWarnings" | Should -BeLike '*direct group membership*'
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewManagementScope -Times 0 -Exactly
    }
}

