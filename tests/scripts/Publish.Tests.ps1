#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Publish.ps1.

.DESCRIPTION
    Invokes the orchestrator via '& $ScriptPath' inside each relevant Context
    with a full set of helper mocks, then asserts the observed call pattern.

.NOTES
    This file intentionally uses a $global: call log ($global:PublishTestCalls)
    because Pester Mock script blocks execute in the scope of the Mock'd
    function's caller (here, the orchestrator script). The orchestrator's
    $script: scope is distinct from the test file's $script: scope, so a
    $global: variable is the only reliable cross-scope store. The
    PSAvoidGlobalVars rule is suppressed below.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share a call log across scopes.'
)]
param()

Describe 'scripts/Publish.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Publish.ps1'
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Helpers.psm1'
        Import-Module $script:HelpersPath -Force
        # Shared call log. Use a mutable collection so Mock script blocks can
        # append via the same object reference without rebinding a local.
        $global:PublishTestCalls = [System.Collections.ArrayList]::new()
    }

    BeforeEach {
        # Clear prior calls in-place (keeps the same ArrayList reference).
        $global:PublishTestCalls.Clear()

        # Mock the module-exported functions at the caller's scope (the test
        # file's scope, which is also where the script's main body runs because
        # we invoke via '& $ScriptPath'). Mocks without -ModuleName intercept
        # calls made from outside the module, which is what the orchestrator
        # does.
        Mock Invoke-DotnetPublish {
            $entry = [pscustomobject]@{
                Name = 'Invoke-DotnetPublish'
                Args = [pscustomobject]@{
                    ProjectPath   = $ProjectPath
                    OutputDir     = $OutputDir
                    Configuration = $Configuration
                    ExtraArgs     = $ExtraArgs
                }
            }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Copy-DockerArtifact {
            $entry = [pscustomobject]@{ Name = 'Copy-DockerArtifact'; Args = [pscustomobject]@{ RepoRoot = $RepoRoot; DockerBundleDir = $DockerBundleDir } }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Invoke-VersionStamp {
            $entry = [pscustomobject]@{ Name = 'Invoke-VersionStamp'; Args = [pscustomobject]@{ Version = $Version } }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Invoke-LayoutAssembly {
            $entry = [pscustomobject]@{ Name = 'Invoke-LayoutAssembly'; Args = [pscustomobject]@{ Bridge = $BridgePublishDir; Client = $ClientPublishDir } }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Invoke-MakePri {
            $entry = [pscustomobject]@{ Name = 'Invoke-MakePri'; Args = [pscustomobject]@{} }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Invoke-MakeAppx {
            $entry = [pscustomobject]@{ Name = 'Invoke-MakeAppx'; Args = [pscustomobject]@{ OutputMsixPath = $OutputMsixPath } }
            [void]$global:PublishTestCalls.Add($entry)
            return $OutputMsixPath
        }
        Mock Invoke-SignTool {
            $entry = [pscustomobject]@{ Name = 'Invoke-SignTool'; Args = [pscustomobject]@{ MsixPath = $MsixPath; CertThumbprint = $CertThumbprint } }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Write-PublishManifest {
            $entry = [pscustomobject]@{ Name = 'Write-PublishManifest'; Args = [pscustomobject]@{ BundleRoot = $BundleRoot; Version = $Version } }
            [void]$global:PublishTestCalls.Add($entry)
        }

        # Keep filesystem side-effects out of the way inside the script body.
        Mock New-Item { }
        Mock Remove-Item { }
        Mock Test-Path { $false }
    }

    Context 'parameter validation' {
        It 'throws when neither -SkipSign nor -CertThumbprint is provided' {
            { & $script:ScriptPath -Version '1.2.3.0' } |
                Should -Throw -ExpectedMessage '*Either -SkipSign or a non-empty -CertThumbprint*'
        }
        It 'accepts -SkipSign alone' {
            { & $script:ScriptPath -Version '1.2.3.0' -SkipSign } | Should -Not -Throw
        }
        It 'accepts -CertThumbprint alone' {
            { & $script:ScriptPath -Version '1.2.3.0' -CertThumbprint 'ABCDEF0123' } | Should -Not -Throw
        }
        It 'rejects a 3-part version (Q1 strict validation)' {
            { & $script:ScriptPath -Version '1.2.3' -SkipSign } |
                Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
        }
    }

    Context 'stage ordering' {
        It 'calls helpers in the correct order with -SkipSign' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign | Out-Null

            $names = @($global:PublishTestCalls | ForEach-Object { $_.Name })
            $expectedPrefix = @(
                'Invoke-DotnetPublish', 'Invoke-DotnetPublish', 'Invoke-DotnetPublish', 'Invoke-DotnetPublish',
                'Copy-DockerArtifact',
                'Invoke-LayoutAssembly', 'Invoke-VersionStamp', 'Invoke-MakePri', 'Invoke-MakeAppx',
                'Write-PublishManifest'
            )
            ($names -join ',') | Should -Be ($expectedPrefix -join ',')
        }
        It 'stamps AppxManifest.xml after staging layout assembly so the manifest is not deleted before makeappx' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign | Out-Null

            $names = @($global:PublishTestCalls | ForEach-Object { $_.Name })
            $idxLayout = [array]::IndexOf($names, 'Invoke-LayoutAssembly')
            $idxVersionStamp = [array]::IndexOf($names, 'Invoke-VersionStamp')
            $idxMakeAppx = [array]::IndexOf($names, 'Invoke-MakeAppx')

            $idxVersionStamp | Should -BeGreaterThan $idxLayout
            $idxVersionStamp | Should -BeLessThan $idxMakeAppx
        }
        It 'inserts Invoke-SignTool between Invoke-MakeAppx and Write-PublishManifest when signed' {
            & $script:ScriptPath -Version '1.2.3.0' -CertThumbprint 'ABCDEF0123' | Out-Null

            $names = @($global:PublishTestCalls | ForEach-Object { $_.Name })
            $idxMakeAppx = [array]::IndexOf($names, 'Invoke-MakeAppx')
            $idxSign = [array]::IndexOf($names, 'Invoke-SignTool')
            $idxManifest = [array]::IndexOf($names, 'Write-PublishManifest')
            $idxSign | Should -BeGreaterThan $idxMakeAppx
            $idxManifest | Should -BeGreaterThan $idxSign
        }
    }

    Context 'per-project publish flags' {
        It 'passes --self-contained true -r win-x64 for Core and HostAdapter' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign | Out-Null

            $publishCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-DotnetPublish' })
            $coreCall = $publishCalls | Where-Object { $_.Args.ProjectPath -like '*OpenClaw.Core*' } | Select-Object -First 1
            $hostCall = $publishCalls | Where-Object { $_.Args.ProjectPath -like '*OpenClaw.HostAdapter*' -and $_.Args.ProjectPath -notlike '*Contracts*' } | Select-Object -First 1

            ($coreCall.Args.ExtraArgs -join ' ') | Should -Match '--self-contained true -r win-x64'
            ($hostCall.Args.ExtraArgs -join ' ') | Should -Match '--self-contained true -r win-x64'
        }
        It 'passes /p:PublishProfile=msix for MailBridge and MailBridge.Client' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign | Out-Null

            $publishCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-DotnetPublish' })
            $bridge = $publishCalls | Where-Object { $_.Args.ProjectPath -like '*OpenClaw.MailBridge\*' -or $_.Args.ProjectPath -like '*OpenClaw.MailBridge.csproj*' } | Select-Object -First 1
            $client = $publishCalls | Where-Object { $_.Args.ProjectPath -like '*OpenClaw.MailBridge.Client*' } | Select-Object -First 1

            ($bridge.Args.ExtraArgs -join ' ') | Should -Match '/p:PublishProfile=msix'
            ($client.Args.ExtraArgs -join ' ') | Should -Match '/p:PublishProfile=msix'
        }
    }

    Context 'skip-sign path' {
        It '-SkipSign does NOT invoke Invoke-SignTool' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign | Out-Null
            (@($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-SignTool' }).Count) | Should -Be 0
        }
        It '-CertThumbprint invokes Invoke-SignTool with the supplied thumbprint' {
            & $script:ScriptPath -Version '1.2.3.0' -CertThumbprint 'FEEDFACE' | Out-Null
            $signCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-SignTool' })
            $signCalls.Count | Should -Be 1
            $signCalls[0].Args.CertThumbprint | Should -Be 'FEEDFACE'
        }
    }

    Context 'output paths' {
        It 'writes the MSIX to <OutputDir>/<Version>/msix/OpenClaw.MailBridge_<Version>_x64.msix' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign -OutputDir 'D:\out' | Out-Null
            $makeAppx = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-MakeAppx' })[0]
            $expected = Join-Path (Join-Path 'D:\out' '1.2.3.0') (Join-Path 'msix' 'OpenClaw.MailBridge_1.2.3.0_x64.msix')
            $makeAppx.Args.OutputMsixPath | Should -Be $expected
        }
        It 'writes manifest.json under <OutputDir>/<Version>/' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign -OutputDir 'D:\out' | Out-Null
            $manifestCall = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Write-PublishManifest' })[0]
            $manifestCall.Args.BundleRoot | Should -Be (Join-Path 'D:\out' '1.2.3.0')
            $manifestCall.Args.Version | Should -Be '1.2.3.0'
        }
    }
}
