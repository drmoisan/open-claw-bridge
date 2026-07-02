#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for Test-OpenClawScopeBoundary.

.DESCRIPTION
Covers the full pass/fail matrix - (allowed, denied), (denied, denied),
(allowed, allowed), (denied, allowed) - plus the exactly-two-wrapper-calls
contract with correct Identity/Resource arguments, raw-row surfacing in the
details properties, and GUID/SMTP parameter validation. Only the
Invoke-OpenClaw* wrapper seam is mocked. The mock is registered inline in each
test with It-scoped row payloads so the behavior scriptblock resolves them.
#>

BeforeAll {
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force

    $script:appObjectId = '00000000-0000-0000-0000-000000000002'
    $script:inScopeMailbox = 'in-scope-user@contoso.com'
    $script:outOfScopeMailbox = 'out-of-scope-user@contoso.com'
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'Test-OpenClawScopeBoundary parameter validation' {
    It 'rejects a malformed -EnterpriseApplicationObjectId at binding time' {
        {
            Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId 'not-a-guid' `
                -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox
        } | Should -Throw
    }

    It 'rejects a non-SMTP -InScopeMailbox at binding time' {
        {
            Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
                -InScopeMailbox 'nope' -OutOfScopeMailbox $script:outOfScopeMailbox
        } | Should -Throw
    }

    It 'rejects a non-SMTP -OutOfScopeMailbox at binding time' {
        {
            Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
                -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox ''
        } | Should -Throw
    }
}

Describe 'Test-OpenClawScopeBoundary pass/fail matrix' {
    It 'returns Succeeded = $true with no FailureReason for (allowed, denied)' {
        # Arrange: in-scope allowed, out-of-scope denied.
        $authorizationRows = @{
            InScope    = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $true })
            OutOfScope = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $false })
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization {
            param([string]$Identity, [string]$Resource)
            $null = $Identity
            if ($Resource -eq 'in-scope-user@contoso.com') { return $authorizationRows.InScope }
            return $authorizationRows.OutOfScope
        }

        # Act
        $result = Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
            -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox

        # Assert
        $result.InScopeAllowed | Should -BeTrue
        $result.OutOfScopeDenied | Should -BeTrue
        $result.Succeeded | Should -BeTrue
        $result.FailureReason | Should -BeNullOrEmpty
    }

    It 'returns Succeeded = $false with the in-scope reason for (denied, denied)' {
        # Arrange: neither mailbox is in scope.
        $authorizationRows = @{
            InScope    = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $false })
            OutOfScope = @()
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization {
            param([string]$Identity, [string]$Resource)
            $null = $Identity
            if ($Resource -eq 'in-scope-user@contoso.com') { return $authorizationRows.InScope }
            return $authorizationRows.OutOfScope
        }

        # Act
        $result = Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
            -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox

        # Assert
        $result.InScopeAllowed | Should -BeFalse
        $result.OutOfScopeDenied | Should -BeTrue
        $result.Succeeded | Should -BeFalse
        $result.FailureReason | Should -Be 'in-scope mailbox has no effective role or InScope=False'
    }

    It 'returns Succeeded = $false with the out-of-scope reason for (allowed, allowed)' {
        # Arrange: both mailboxes report in scope.
        $authorizationRows = @{
            InScope    = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $true })
            OutOfScope = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $true })
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization {
            param([string]$Identity, [string]$Resource)
            $null = $Identity
            if ($Resource -eq 'in-scope-user@contoso.com') { return $authorizationRows.InScope }
            return $authorizationRows.OutOfScope
        }

        # Act
        $result = Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
            -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox

        # Assert
        $result.InScopeAllowed | Should -BeTrue
        $result.OutOfScopeDenied | Should -BeFalse
        $result.Succeeded | Should -BeFalse
        $result.FailureReason | Should -Be 'out-of-scope mailbox is unexpectedly in scope'
    }

    It 'returns Succeeded = $false with both reasons joined for (denied, allowed)' {
        # Arrange: the boundary is inverted.
        $authorizationRows = @{
            InScope    = @()
            OutOfScope = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $true })
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization {
            param([string]$Identity, [string]$Resource)
            $null = $Identity
            if ($Resource -eq 'in-scope-user@contoso.com') { return $authorizationRows.InScope }
            return $authorizationRows.OutOfScope
        }

        # Act
        $result = Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
            -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox

        # Assert
        $result.Succeeded | Should -BeFalse
        $result.FailureReason |
            Should -Be 'in-scope mailbox has no effective role or InScope=False; out-of-scope mailbox is unexpectedly in scope'
    }
}

Describe 'Test-OpenClawScopeBoundary wrapper contract' {
    It 'makes exactly two authorization calls with the correct Identity and Resource arguments' {
        # Arrange
        $authorizationRows = @{
            InScope    = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $true })
            OutOfScope = @()
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization {
            param([string]$Identity, [string]$Resource)
            $null = $Identity
            if ($Resource -eq 'in-scope-user@contoso.com') { return $authorizationRows.InScope }
            return $authorizationRows.OutOfScope
        }

        # Act
        Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
            -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox | Out-Null

        # Assert
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization -Times 2 -Exactly
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization -Times 1 -Exactly `
            -ParameterFilter {
            $Identity -eq '00000000-0000-0000-0000-000000000002' -and $Resource -eq 'in-scope-user@contoso.com'
        }
        Should -Invoke -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization -Times 1 -Exactly `
            -ParameterFilter {
            $Identity -eq '00000000-0000-0000-0000-000000000002' -and $Resource -eq 'out-of-scope-user@contoso.com'
        }
    }

    It 'surfaces the raw authorization rows in InScopeDetails and OutOfScopeDetails' {
        # Arrange
        $authorizationRows = @{
            InScope    = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $true })
            OutOfScope = @([pscustomobject]@{ RoleName = 'Application Mail.Read'; InScope = $false })
        }
        Mock -ModuleName OpenClawRbac Invoke-OpenClawTestServicePrincipalAuthorization {
            param([string]$Identity, [string]$Resource)
            $null = $Identity
            if ($Resource -eq 'in-scope-user@contoso.com') { return $authorizationRows.InScope }
            return $authorizationRows.OutOfScope
        }

        # Act
        $result = Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $script:appObjectId `
            -InScopeMailbox $script:inScopeMailbox -OutOfScopeMailbox $script:outOfScopeMailbox

        # Assert: raw rows with their original properties are surfaced.
        @($result.InScopeDetails).Count | Should -Be 1
        $result.InScopeDetails[0].RoleName | Should -Be 'Application Mail.Read'
        $result.InScopeDetails[0].InScope | Should -BeTrue
        @($result.OutOfScopeDetails).Count | Should -Be 1
        $result.OutOfScopeDetails[0].InScope | Should -BeFalse
    }

    It 'contains no exit statement (exit-code mapping belongs to the entry script)' {
        # Arrange / Act: static scan of the production function file.
        $productionPath = Resolve-Path -Path (
            Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\Test-OpenClawScopeBoundary.ps1'
        )
        $content = Get-Content -Path $productionPath -Raw

        # Assert: no exit statement anywhere in the function file.
        $content | Should -Not -Match '(?m)^\s*exit\b'
    }
}
