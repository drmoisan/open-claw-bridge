#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for Set-OpenClawSendOnBehalf.

.DESCRIPTION
Covers SMTP parameter validation, the happy path's exact wrapper arguments,
pipeline input (one application per principal mailbox), the targeted
existing-permission no-op versus re-thrown unexpected errors, -WhatIf
producing zero wrapper write invocations, and the no-Send-As rule. Only the
Invoke-OpenClaw* wrapper seams are mocked.
#>

BeforeAll {
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force

    $script:principal = 'executive@contoso.com'
    $script:assistant = 'assistant@contoso.com'
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'Set-OpenClawSendOnBehalf parameter validation' {
    It 'rejects an empty -PrincipalMailbox at binding time' {
        { Set-OpenClawSendOnBehalf -PrincipalMailbox '' -AssistantMailbox $script:assistant } | Should -Throw
    }

    It 'rejects a non-SMTP -PrincipalMailbox at binding time' {
        { Set-OpenClawSendOnBehalf -PrincipalMailbox 'not-an-address' -AssistantMailbox $script:assistant } | Should -Throw
    }

    It 'rejects an empty -AssistantMailbox at binding time' {
        { Set-OpenClawSendOnBehalf -PrincipalMailbox $script:principal -AssistantMailbox '' } | Should -Throw
    }

    It 'rejects a non-SMTP -AssistantMailbox at binding time' {
        { Set-OpenClawSendOnBehalf -PrincipalMailbox $script:principal -AssistantMailbox 'nope' } | Should -Throw
    }
}

Describe 'Set-OpenClawSendOnBehalf behavior' {
    BeforeEach {
        Mock -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission {
            param([string]$Identity, [string]$User, [string]$AccessRights, [string]$InheritanceType, [bool]$AutoMapping)
            # Reference parameters to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null = $User
            $null = $AccessRights
            $null = $InheritanceType
            $null = $AutoMapping
            [pscustomobject]@{ Granted = $true }
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawSetMailbox {
            param([string]$Identity, [hashtable]$GrantSendOnBehalfTo)
            # Reference parameters to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null = $GrantSendOnBehalfTo
            [pscustomobject]@{ Updated = $true }
        }
    }

    It 'passes the exact FullAccess permission arguments and the additive Send on Behalf payload' {
        # Act
        Set-OpenClawSendOnBehalf -PrincipalMailbox $script:principal -AssistantMailbox $script:assistant

        # Assert: exact named arguments on both wrappers.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission -Times 1 -Exactly `
            -ParameterFilter {
            $Identity -eq 'executive@contoso.com' -and
            $User -eq 'assistant@contoso.com' -and
            $AccessRights -eq 'FullAccess' -and
            $InheritanceType -eq 'All' -and
            $AutoMapping -eq $false
        }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawSetMailbox -Times 1 -Exactly `
            -ParameterFilter {
            $Identity -eq 'executive@contoso.com' -and
            $GrantSendOnBehalfTo['Add'] -eq 'assistant@contoso.com'
        }
    }

    It 'applies once per principal mailbox when principals arrive via the pipeline' {
        # Act: two principals through the pipeline.
        'executive@contoso.com', 'director@contoso.com' |
            Set-OpenClawSendOnBehalf -AssistantMailbox $script:assistant

        # Assert: each principal received exactly one permission + one Send on Behalf call.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission -Times 1 -Exactly `
            -ParameterFilter { $Identity -eq 'executive@contoso.com' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission -Times 1 -Exactly `
            -ParameterFilter { $Identity -eq 'director@contoso.com' }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawSetMailbox -Times 2 -Exactly
    }

    It 'reports the documented existing-permission error as a no-op and still grants Send on Behalf' {
        # Arrange: the documented Add-MailboxPermission existing-ACE failure.
        Mock -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission {
            param([string]$Identity, [string]$User, [string]$AccessRights, [string]$InheritanceType, [bool]$AutoMapping)
            $null = $Identity; $null = $User; $null = $AccessRights; $null = $InheritanceType; $null = $AutoMapping
            throw 'An existing permission entry was found for user: assistant@contoso.com.'
        }

        # Act / Assert: no throw; informational no-op; Send on Behalf still applied.
        {
            Set-OpenClawSendOnBehalf `
                -PrincipalMailbox $script:principal `
                -AssistantMailbox $script:assistant `
                -InformationVariable capturedInformation
            "$capturedInformation" | Should -BeLike '*already exists*'
        } | Should -Not -Throw
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawSetMailbox -Times 1 -Exactly
    }

    It 're-throws any error that is not the documented existing-permission error' {
        # Arrange: an unrelated failure from the permission wrapper.
        Mock -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission {
            param([string]$Identity, [string]$User, [string]$AccessRights, [string]$InheritanceType, [bool]$AutoMapping)
            $null = $Identity; $null = $User; $null = $AccessRights; $null = $InheritanceType; $null = $AutoMapping
            throw 'Access is denied.'
        }

        # Act / Assert
        {
            Set-OpenClawSendOnBehalf -PrincipalMailbox $script:principal -AssistantMailbox $script:assistant
        } | Should -Throw '*Access is denied*'
    }

    It 'performs zero wrapper write invocations under -WhatIf' {
        # Act
        Set-OpenClawSendOnBehalf -PrincipalMailbox $script:principal -AssistantMailbox $script:assistant -WhatIf

        # Assert: complete dry run.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission -Times 0 -Exactly
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawSetMailbox -Times 0 -Exactly
    }

    It 'never passes any Send As configuration' {
        # Act
        Set-OpenClawSendOnBehalf -PrincipalMailbox $script:principal -AssistantMailbox $script:assistant

        # Assert: the access right granted is FullAccess (never SendAs), and the
        # production function contains no Send As configuration at all.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawAddMailboxPermission -Times 0 -Exactly `
            -ParameterFilter { $AccessRights -like '*SendAs*' }
        $productionPath = Resolve-Path -Path (
            Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\Set-OpenClawSendOnBehalf.ps1'
        )
        (Get-Content -Path $productionPath -Raw) | Should -Not -Match 'SendAs|GrantSendAs'
    }
}
