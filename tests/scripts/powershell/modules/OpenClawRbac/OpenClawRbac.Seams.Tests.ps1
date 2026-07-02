#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for the nine OpenClawRbac wrapper seams.

.DESCRIPTION
Covers, for every wrapper: (a) the missing-cmdlet path throws a specific error
naming the missing Exchange cmdlet and directing the operator to
ExchangeOnlineManagement / Connect-ExchangeOnline; (b) the resolved path
invokes the resolved command with the exact named arguments passed; (c) Get-*
wrappers return $null when the underlying lookup reports not-found.

No Exchange cmdlet is mocked with Pester's Mock anywhere in this file. The
resolved path is exercised by injecting a fake resolvable command into the
module scope (the injection pattern explicitly permitted by the plan); the
missing path is exercised by mocking Get-Command inside the module scope.
#>

BeforeDiscovery {
    $script:wrapperContractCases = @(
        @{
            WrapperName        = 'Invoke-OpenClawNewServicePrincipal'
            CmdletName         = 'New-ServicePrincipal'
            FakeParameterNames = @('AppId', 'ObjectId', 'DisplayName')
            WrapperArguments   = @{
                AppId       = '00000000-0000-0000-0000-000000000001'
                ObjectId    = '00000000-0000-0000-0000-000000000002'
                DisplayName = 'OpenClaw Assistant'
            }
            IsGetWrapper       = $false
        }
        @{
            WrapperName        = 'Invoke-OpenClawGetServicePrincipal'
            CmdletName         = 'Get-ServicePrincipal'
            FakeParameterNames = @('Identity')
            WrapperArguments   = @{ Identity = '00000000-0000-0000-0000-000000000001' }
            IsGetWrapper       = $true
        }
        @{
            WrapperName        = 'Invoke-OpenClawNewManagementScope'
            CmdletName         = 'New-ManagementScope'
            FakeParameterNames = @('Name', 'RecipientRestrictionFilter')
            WrapperArguments   = @{
                Name                       = 'OpenClaw-ScopedMailboxes'
                RecipientRestrictionFilter = "MemberOfGroup -eq 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com'"
            }
            IsGetWrapper       = $false
        }
        @{
            WrapperName        = 'Invoke-OpenClawGetManagementScope'
            CmdletName         = 'Get-ManagementScope'
            FakeParameterNames = @('Identity')
            WrapperArguments   = @{ Identity = 'OpenClaw-ScopedMailboxes' }
            IsGetWrapper       = $true
        }
        @{
            WrapperName        = 'Invoke-OpenClawNewManagementRoleAssignment'
            CmdletName         = 'New-ManagementRoleAssignment'
            FakeParameterNames = @('Name', 'App', 'Role', 'CustomResourceScope', 'RecipientAdministrativeUnitScope')
            WrapperArguments   = @{
                Name                = 'OpenClaw-MailRead'
                App                 = '00000000-0000-0000-0000-000000000002'
                Role                = 'Application Mail.Read'
                CustomResourceScope = 'OpenClaw-ScopedMailboxes'
            }
            IsGetWrapper       = $false
        }
        @{
            WrapperName        = 'Invoke-OpenClawGetManagementRoleAssignment'
            CmdletName         = 'Get-ManagementRoleAssignment'
            FakeParameterNames = @('Identity')
            WrapperArguments   = @{ Identity = 'OpenClaw-MailRead' }
            IsGetWrapper       = $true
        }
        @{
            WrapperName        = 'Invoke-OpenClawAddMailboxPermission'
            CmdletName         = 'Add-MailboxPermission'
            FakeParameterNames = @('Identity', 'User', 'AccessRights', 'InheritanceType', 'AutoMapping')
            WrapperArguments   = @{
                Identity        = 'executive@contoso.com'
                User            = 'assistant@contoso.com'
                AccessRights    = 'FullAccess'
                InheritanceType = 'All'
                AutoMapping     = $false
            }
            IsGetWrapper       = $false
        }
        @{
            WrapperName        = 'Invoke-OpenClawSetMailbox'
            CmdletName         = 'Set-Mailbox'
            FakeParameterNames = @('Identity', 'GrantSendOnBehalfTo')
            WrapperArguments   = @{
                Identity            = 'executive@contoso.com'
                GrantSendOnBehalfTo = @{ Add = 'assistant@contoso.com' }
            }
            IsGetWrapper       = $false
        }
        @{
            WrapperName        = 'Invoke-OpenClawTestServicePrincipalAuthorization'
            CmdletName         = 'Test-ServicePrincipalAuthorization'
            FakeParameterNames = @('Identity', 'Resource')
            WrapperArguments   = @{
                Identity = '00000000-0000-0000-0000-000000000002'
                Resource = 'in-scope-user@contoso.com'
            }
            IsGetWrapper       = $false
        }
    )
    $script:getWrapperContractCases = @($script:wrapperContractCases | Where-Object { $_.IsGetWrapper })
}

BeforeAll {
    $manifestPath = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac\OpenClawRbac.psd1'
    )
    Import-Module $manifestPath -Force
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'OpenClawRbac seam contract for <WrapperName>' -ForEach $script:wrapperContractCases {
    AfterEach {
        # Remove any fake command injected into the module scope by this test.
        InModuleScope OpenClawRbac -Parameters @{ CmdletName = $CmdletName } {
            Remove-Item -Path ('function:script:' + $CmdletName) -ErrorAction SilentlyContinue
        }
    }

    It 'throws a specific error naming <CmdletName> when the cmdlet cannot be resolved' {
        # Arrange: force runtime resolution to fail for this cmdlet name only.
        Mock -ModuleName OpenClawRbac Get-Command { $null } `
            -ParameterFilter ([scriptblock]::Create("`$Name -eq '$CmdletName'"))

        # Act: invoke the wrapper and capture the terminating error.
        $thrownError = $null
        try {
            & $WrapperName @WrapperArguments
        }
        catch {
            $thrownError = $_
        }

        # Assert: specific, actionable error naming the cmdlet and remediation.
        $thrownError | Should -Not -BeNullOrEmpty
        $thrownError.Exception.Message | Should -BeLike "*$CmdletName*"
        $thrownError.Exception.Message | Should -BeLike '*ExchangeOnlineManagement*'
        $thrownError.Exception.Message | Should -BeLike '*Connect-ExchangeOnline*'
    }

    It 'invokes the resolved command with the exact named arguments passed' {
        # Arrange: inject a fake resolvable command into the module scope that
        # echoes back every bound parameter (no Pester mock of the cmdlet).
        InModuleScope OpenClawRbac -Parameters @{
            CmdletName         = $CmdletName
            FakeParameterNames = $FakeParameterNames
        } {
            $parameterList = ($FakeParameterNames | ForEach-Object { "`$$_" }) -join ', '
            $fakeBody = @"
[CmdletBinding()]
param($parameterList)
`$captured = @{}
foreach (`$parameterName in `$PSBoundParameters.Keys) { `$captured[`$parameterName] = `$PSBoundParameters[`$parameterName] }
[pscustomobject]@{ FakeCmdlet = '$CmdletName'; BoundParameters = `$captured }
"@
            Set-Item -Path ('function:script:' + $CmdletName) -Value ([scriptblock]::Create($fakeBody))
        }

        # Act: invoke the wrapper with the case's named arguments.
        $result = & $WrapperName @WrapperArguments

        # Assert: the fake was invoked and received exactly the named
        # arguments passed to the wrapper (Get-* wrappers additionally pass
        # ErrorAction=SilentlyContinue as part of their not-found contract).
        $result.FakeCmdlet | Should -Be $CmdletName
        foreach ($argumentName in $WrapperArguments.Keys) {
            $result.BoundParameters.ContainsKey($argumentName) |
                Should -BeTrue -Because "argument '$argumentName' must be forwarded"
            $result.BoundParameters[$argumentName] |
                Should -Be $WrapperArguments[$argumentName] -Because "argument '$argumentName' must be forwarded unchanged"
        }
        $expectedArgumentCount = $WrapperArguments.Count
        if ($IsGetWrapper) {
            $result.BoundParameters['ErrorAction'] | Should -Be 'SilentlyContinue'
            $expectedArgumentCount += 1
        }
        $result.BoundParameters.Count | Should -Be $expectedArgumentCount -Because 'no extra arguments may be forwarded'
    }
}

Describe 'OpenClawRbac Get-* not-found contract for <WrapperName>' -ForEach $script:getWrapperContractCases {
    AfterEach {
        InModuleScope OpenClawRbac -Parameters @{ CmdletName = $CmdletName } {
            Remove-Item -Path ('function:script:' + $CmdletName) -ErrorAction SilentlyContinue
        }
    }

    It 'returns $null when the underlying lookup reports not-found' {
        # Arrange: inject a fake resolvable command that returns nothing,
        # modelling the not-found outcome of the underlying lookup.
        InModuleScope OpenClawRbac -Parameters @{ CmdletName = $CmdletName } {
            $emptyFakeBody = "[CmdletBinding()]`nparam(`$Identity)"
            Set-Item -Path ('function:script:' + $CmdletName) -Value ([scriptblock]::Create($emptyFakeBody))
        }

        # Act: invoke the Get-* wrapper.
        $result = & $WrapperName @WrapperArguments

        # Assert: not-found is surfaced as $null, not as an error.
        $result | Should -BeNullOrEmpty
    }
}
