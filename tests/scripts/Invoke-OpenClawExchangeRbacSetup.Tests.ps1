#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for the Invoke-OpenClawExchangeRbacSetup.ps1 entry script.

.DESCRIPTION
Covers the sequencing order of the five OpenClawRbac function calls, the
scope-creation skip when -AdministrativeUnitId is supplied, -WhatIf forwarding
to every state-changing call, and exit-code mapping (Succeeded true -> 0,
false -> 1). The script is invoked in-process with the call operator (no
external processes, no temp files); its `exit` terminates only the script and
surfaces as $LASTEXITCODE. The five public module functions are mocked per the
plan's entry-script test contract.
#>

BeforeAll {
    $script:scriptPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\scripts\Invoke-OpenClawExchangeRbacSetup.ps1'
    )
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force

    $script:commonArguments = @{
        AppId                         = '00000000-0000-0000-0000-000000000001'
        EnterpriseApplicationObjectId = '00000000-0000-0000-0000-000000000002'
        PrincipalMailbox              = @('executive@contoso.com')
        AssistantMailbox              = 'assistant@contoso.com'
        InScopeMailbox                = 'in-scope-user@contoso.com'
        OutOfScopeMailbox             = 'out-of-scope-user@contoso.com'
    }
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'Invoke-OpenClawExchangeRbacSetup' {
    BeforeEach {
        $invocationOrder = [System.Collections.Generic.List[string]]::new()

        Mock Register-OpenClawServicePrincipal {
            $invocationOrder.Add('Register')
            [pscustomobject]@{ AppId = '00000000-0000-0000-0000-000000000001' }
        }
        Mock New-OpenClawMailboxScope {
            $invocationOrder.Add('Scope')
            [pscustomobject]@{ Name = 'OpenClaw-ScopedMailboxes' }
        }
        Mock Grant-OpenClawRbacRoles {
            $invocationOrder.Add('Grant')
        }
        Mock Set-OpenClawSendOnBehalf {
            $invocationOrder.Add('SendOnBehalf')
        }
        Mock Test-OpenClawScopeBoundary {
            $invocationOrder.Add('Boundary')
            [pscustomobject]@{ Succeeded = $true; FailureReason = $null }
        }
    }

    It 'sequences the five function calls in the documented order and exits 0 on success' {
        # Act
        & $script:scriptPath @script:commonArguments -GroupDistinguishedName 'CN=G,DC=contoso,DC=com' | Out-Null

        # Assert: Register -> Scope -> Grant -> SendOnBehalf -> Boundary, exit 0.
        $invocationOrder | Should -Be @('Register', 'Scope', 'Grant', 'SendOnBehalf', 'Boundary')
        $LASTEXITCODE | Should -Be 0
    }

    It 'skips scope creation and grants against the Administrative Unit when -AdministrativeUnitId is supplied' {
        # Act
        & $script:scriptPath @script:commonArguments -AdministrativeUnitId '00000000-0000-0000-0000-000000000003' | Out-Null

        # Assert: no New-OpenClawMailboxScope call; Grant received the AU id.
        $invocationOrder | Should -Be @('Register', 'Grant', 'SendOnBehalf', 'Boundary')
        Should -Invoke New-OpenClawMailboxScope -Times 0 -Exactly
        Should -Invoke Grant-OpenClawRbacRoles -Times 1 -Exactly `
            -ParameterFilter { $AdministrativeUnitId -eq '00000000-0000-0000-0000-000000000003' }
    }

    It 'applies Set-OpenClawSendOnBehalf once per principal mailbox' {
        # Act
        $arguments = $script:commonArguments.Clone()
        $arguments.PrincipalMailbox = @('executive@contoso.com', 'director@contoso.com')
        & $script:scriptPath @arguments -GroupDistinguishedName 'CN=G,DC=contoso,DC=com' | Out-Null

        # Assert
        Should -Invoke Set-OpenClawSendOnBehalf -Times 1 -Exactly `
            -ParameterFilter { $PrincipalMailbox -eq 'executive@contoso.com' }
        Should -Invoke Set-OpenClawSendOnBehalf -Times 1 -Exactly `
            -ParameterFilter { $PrincipalMailbox -eq 'director@contoso.com' }
    }

    It 'forwards -WhatIf to every state-changing call' {
        # Act
        & $script:scriptPath @script:commonArguments -GroupDistinguishedName 'CN=G,DC=contoso,DC=com' -WhatIf | Out-Null

        # Assert: all four state-changing functions received WhatIf = true.
        Should -Invoke Register-OpenClawServicePrincipal -Times 1 -Exactly -ParameterFilter { $WhatIf -eq $true }
        Should -Invoke New-OpenClawMailboxScope -Times 1 -Exactly -ParameterFilter { $WhatIf -eq $true }
        Should -Invoke Grant-OpenClawRbacRoles -Times 1 -Exactly -ParameterFilter { $WhatIf -eq $true }
        Should -Invoke Set-OpenClawSendOnBehalf -Times 1 -Exactly -ParameterFilter { $WhatIf -eq $true }
    }

    It 'exits 1 when the boundary verification fails' {
        # Arrange: the boundary check reports failure.
        Mock Test-OpenClawScopeBoundary {
            $invocationOrder.Add('Boundary')
            [pscustomobject]@{ Succeeded = $false; FailureReason = 'out-of-scope mailbox is unexpectedly in scope' }
        }

        # Act
        & $script:scriptPath @script:commonArguments -GroupDistinguishedName 'CN=G,DC=contoso,DC=com' | Out-Null

        # Assert
        $LASTEXITCODE | Should -Be 1
    }

    It 'passes the boundary mailboxes and app object id to Test-OpenClawScopeBoundary' {
        # Act
        & $script:scriptPath @script:commonArguments -GroupDistinguishedName 'CN=G,DC=contoso,DC=com' | Out-Null

        # Assert
        Should -Invoke Test-OpenClawScopeBoundary -Times 1 -Exactly `
            -ParameterFilter {
            $EnterpriseApplicationObjectId -eq '00000000-0000-0000-0000-000000000002' -and
            $InScopeMailbox -eq 'in-scope-user@contoso.com' -and
            $OutOfScopeMailbox -eq 'out-of-scope-user@contoso.com'
        }
    }
}
