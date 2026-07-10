#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Deploy.ps1.

.DESCRIPTION
    Invokes the wrapper via '& $script:DeployScriptPath' with global-scoped
    overrides for Invoke-PublishScript / Invoke-InstallScript pre-registered
    (the Get-Command guard in Deploy.ps1 then skips defining its own local
    functions, so these overrides intercept every child invocation).

.NOTES
    This file intentionally uses $global: variables ($global:DeployTestCalls,
    $global:DeployTestBundleRoot) because the global stub functions and the
    script invoked via '&' both need a call log that survives across PowerShell
    scopes. The PSAvoidGlobalVars rule is suppressed below.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Global stub functions and the script-under-test run in different scopes; $global: is required to share a call log across scopes.'
)]
param()

Describe 'scripts/Deploy.ps1' {

    BeforeAll {
        $script:DeployScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Deploy.ps1'
    }

    BeforeEach {
        $global:DeployTestCalls = [System.Collections.ArrayList]::new()
        $global:DeployTestBundleRoot = 'D:\out\1.2.3.0'

        function global:Invoke-PublishScript {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(Mandatory = $true)]
                [string]$PublishScriptPath,

                [Parameter(Mandatory = $true)]
                [hashtable]$PublishParams
            )
            # Deploy.ps1 does not forward -WhatIf explicitly to
            # Invoke-PublishScript (see scripts/Deploy.ps1 comment), so this
            # stub does not need SupportsShouldProcess: it always records the
            # call and returns the fixture bundle root so downstream
            # Join-Path/guard logic in Deploy.ps1 has a valid, non-null value
            # in every test, including the -WhatIf propagation test (P2-T18),
            # which asserts the -WhatIf no-op behavior via the
            # Invoke-InstallScript stub below instead.
            $entry = [pscustomobject]@{
                Name = 'Invoke-PublishScript'
                Args = [pscustomobject]@{
                    PublishScriptPath = $PublishScriptPath
                    PublishParams     = $PublishParams
                }
            }
            [void]$global:DeployTestCalls.Add($entry)
            return $global:DeployTestBundleRoot
        }

        function global:Invoke-InstallScript {
            [CmdletBinding(SupportsShouldProcess = $true)]
            param(
                [Parameter(Mandatory = $true)]
                [string]$InstallScriptPath,

                [Parameter(Mandatory = $true)]
                [hashtable]$InstallParams
            )
            if ($PSCmdlet.ShouldProcess($InstallScriptPath, 'Invoke Install.ps1 (stub)')) {
                $entry = [pscustomobject]@{
                    Name = 'Invoke-InstallScript'
                    Args = [pscustomobject]@{
                        InstallScriptPath = $InstallScriptPath
                        InstallParams     = $InstallParams
                    }
                }
                [void]$global:DeployTestCalls.Add($entry)
            }
        }
    }

    Context 'parameter forwarding' {
        It 'forwards -Version, -Configuration, -CertThumbprint to Invoke-PublishScript (AC-3)' {
            & $script:DeployScriptPath -Version '1.2.3.0' -Configuration 'Debug' -CertThumbprint 'ABC123' | Out-Null

            $publishCall = @($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-PublishScript' })[0]
            $publishCall.Args.PublishParams['Version'] | Should -Be '1.2.3.0'
            $publishCall.Args.PublishParams['Configuration'] | Should -Be 'Debug'
            $publishCall.Args.PublishParams['CertThumbprint'] | Should -Be 'ABC123'
        }

        It 'forwards -SkipDocker, -DockerEnvFilePath, -AnthropicEnvFilePath, -Force to Invoke-InstallScript (AC-3)' {
            & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign -SkipDocker `
                -DockerEnvFilePath 'C:\fake\docker.env' -AnthropicEnvFilePath 'C:\fake\anthropic.env' -Force | Out-Null

            $installCall = @($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-InstallScript' })[0]
            $installCall.Args.InstallParams['SkipDocker'] | Should -BeTrue
            $installCall.Args.InstallParams['DockerEnvFilePath'] | Should -Be 'C:\fake\docker.env'
            $installCall.Args.InstallParams['AnthropicEnvFilePath'] | Should -Be 'C:\fake\anthropic.env'
            $installCall.Args.InstallParams['Force'] | Should -BeTrue
        }
    }

    Context '-SkipSign to -AllowUnsigned mapping' {
        It 'maps -SkipSign to AllowUnsigned = $true in -InstallParams (AC-3)' {
            & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign | Out-Null

            $installCall = @($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-InstallScript' })[0]
            $installCall.Args.InstallParams['AllowUnsigned'] | Should -BeTrue
        }

        It 'does not set a truthy AllowUnsigned key when -SkipSign is not supplied (AC-3)' {
            & $script:DeployScriptPath -Version '1.2.3.0' -CertThumbprint 'ABC123' | Out-Null

            $installCall = @($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-InstallScript' })[0]
            $hasKey = $installCall.Args.InstallParams.ContainsKey('AllowUnsigned')
            if ($hasKey) {
                $installCall.Args.InstallParams['AllowUnsigned'] | Should -Not -BeTrue
            }
            else {
                $hasKey | Should -BeFalse
            }
        }
    }

    Context 'publish-failure short-circuit' {
        It 'propagates a publish throw and does not invoke Install.ps1 (AC-5)' {
            function global:Invoke-PublishScript {
                [CmdletBinding()]
                param(
                    [Parameter(Mandatory = $true)][string]$PublishScriptPath,
                    [Parameter(Mandatory = $true)][hashtable]$PublishParams
                )
                $null = $PublishScriptPath
                $null = $PublishParams
                throw 'simulated publish failure'
            }

            { & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign } | Should -Throw -ExpectedMessage '*simulated publish failure*'

            (@($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-InstallScript' })).Count | Should -Be 0
        }

        It 'throws and does not invoke Install.ps1 when publish returns no bundle root (AC-5)' {
            function global:Invoke-PublishScript {
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(Mandatory = $true)][string]$PublishScriptPath,
                    [Parameter(Mandatory = $true)][hashtable]$PublishParams
                )
                $null = $PublishScriptPath
                $null = $PublishParams
                return [string]::Empty
            }

            { & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign } | Should -Throw

            (@($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-InstallScript' })).Count | Should -Be 0
        }
    }

    Context '-WhatIf propagation' {
        It 'does not throw and records no Invoke-InstallScript call under -WhatIf (AC-4)' {
            { & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign -WhatIf } | Should -Not -Throw

            (@($global:DeployTestCalls | Where-Object { $_.Name -eq 'Invoke-InstallScript' })).Count | Should -Be 0
        }
    }

    Context 'return value' {
        It 'returns exactly the bundle root on success (AC-5)' {
            $result = & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign
            $result | Should -Be $global:DeployTestBundleRoot
        }

        It 'does not change the caller working directory (AC-2)' {
            $before = Get-Location
            & $script:DeployScriptPath -Version '1.2.3.0' -SkipSign | Out-Null
            $after = Get-Location
            $after.Path | Should -Be $before.Path
        }
    }
}
