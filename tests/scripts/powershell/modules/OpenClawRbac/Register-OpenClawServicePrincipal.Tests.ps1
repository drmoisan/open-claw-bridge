#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for Register-OpenClawServicePrincipal.

.DESCRIPTION
Covers GUID binding validation, the existing-service-principal idempotent
short-circuit, the creation path's exact wrapper arguments, and -WhatIf
producing zero write-wrapper invocations. Only the Invoke-OpenClaw* wrapper
seams are mocked; mock signatures match the production wrapper parameters.
#>

BeforeAll {
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force

    $script:validAppId = '00000000-0000-0000-0000-000000000001'
    $script:validObjectId = '00000000-0000-0000-0000-000000000002'
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'Register-OpenClawServicePrincipal parameter validation' {
    It 'rejects a malformed -AppId at binding time' {
        # Arrange / Act / Assert: GUID typing fails the bind before any logic runs.
        {
            Register-OpenClawServicePrincipal `
                -AppId 'not-a-guid' `
                -EnterpriseApplicationObjectId $script:validObjectId
        } | Should -Throw
    }

    It 'rejects a malformed -EnterpriseApplicationObjectId at binding time' {
        {
            Register-OpenClawServicePrincipal `
                -AppId $script:validAppId `
                -EnterpriseApplicationObjectId 'not-a-guid'
        } | Should -Throw
    }

    It 'rejects an empty -DisplayName at binding time' {
        {
            Register-OpenClawServicePrincipal `
                -AppId $script:validAppId `
                -EnterpriseApplicationObjectId $script:validObjectId `
                -DisplayName ''
        } | Should -Throw
    }
}

Describe 'Register-OpenClawServicePrincipal behavior' {
    BeforeEach {
        Mock -ModuleName OpenClawRbac Invoke-OpenClawNewServicePrincipal {
            param([string]$AppId, [string]$ObjectId, [string]$DisplayName)
            # Reference parameters to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $ObjectId
            $null = $DisplayName
            [pscustomobject]@{ Created = $true; AppId = $AppId }
        }
    }

    It 'short-circuits as a reported no-op when the service principal already exists' {
        # Arrange: the Get wrapper reports an existing service principal.
        $existingObject = [pscustomobject]@{ AppId = $script:validAppId; DisplayName = 'OpenClaw Assistant' }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetServicePrincipal {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $existingObject
        }

        # Act: register with an already-registered AppId.
        $result = Register-OpenClawServicePrincipal `
            -AppId $script:validAppId `
            -EnterpriseApplicationObjectId $script:validObjectId `
            -InformationVariable capturedInformation

        # Assert: existing object returned, informational message emitted, no write.
        $result.AppId | Should -Be $script:validAppId
        $capturedInformation | Should -Not -BeNullOrEmpty
        "$capturedInformation" | Should -BeLike '*already exists*'
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewServicePrincipal -Times 0 -Exactly
    }

    It 'looks up the service principal by AppId' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetServicePrincipal {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        Register-OpenClawServicePrincipal `
            -AppId $script:validAppId `
            -EnterpriseApplicationObjectId $script:validObjectId | Out-Null

        # Assert: the Get wrapper received the AppId as its Identity.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawGetServicePrincipal -Times 1 -Exactly `
            -ParameterFilter { $Identity -eq '00000000-0000-0000-0000-000000000001' }
    }

    It 'creates the service principal with the exact AppId, ObjectId, and DisplayName arguments' {
        # Arrange: no existing service principal.
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetServicePrincipal {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        $result = Register-OpenClawServicePrincipal `
            -AppId $script:validAppId `
            -EnterpriseApplicationObjectId $script:validObjectId `
            -DisplayName 'Custom Assistant'

        # Assert: the New wrapper received exactly the expected named arguments.
        $result.Created | Should -BeTrue
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewServicePrincipal -Times 1 -Exactly `
            -ParameterFilter {
            $AppId -eq '00000000-0000-0000-0000-000000000001' -and
            $ObjectId -eq '00000000-0000-0000-0000-000000000002' -and
            $DisplayName -eq 'Custom Assistant'
        }
    }

    It 'defaults DisplayName to OpenClaw Assistant on the creation path' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetServicePrincipal {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        Register-OpenClawServicePrincipal `
            -AppId $script:validAppId `
            -EnterpriseApplicationObjectId $script:validObjectId | Out-Null

        # Assert
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewServicePrincipal -Times 1 -Exactly `
            -ParameterFilter { $DisplayName -eq 'OpenClaw Assistant' }
    }

    It 'performs zero write-wrapper invocations under -WhatIf' {
        # Arrange
        Mock -ModuleName OpenClawRbac Invoke-OpenClawGetServicePrincipal {
            param([string]$Identity)
            # Reference the parameter to satisfy PSReviewUnusedParameter (mock signature parity).
            $null = $Identity
            $null
        }

        # Act
        Register-OpenClawServicePrincipal `
            -AppId $script:validAppId `
            -EnterpriseApplicationObjectId $script:validObjectId `
            -WhatIf

        # Assert: dry run makes no write call.
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawNewServicePrincipal -Times 0 -Exactly
    }
}

