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
        $script:EnvModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Env.psm1'
        Import-Module $script:HelpersPath -Force
        Import-Module $script:EnvModulePath -Force
        # Shared call log. Use a mutable collection so Mock script blocks can
        # append via the same object reference without rebinding a local.
        $global:PublishTestCalls = [System.Collections.ArrayList]::new()
        # Shared in-memory .env content the script will "read". Tests set this
        # before invoking the script so no real .env is read from disk.
        $global:PublishTestEnvContent = @('OPENCLAW_PACKAGE_VERSION=1.0.2.0')
        # Captures what the script would persist back to .env (no disk write).
        $global:PublishTestEnvWrites = [System.Collections.ArrayList]::new()
    }

    BeforeEach {
        # Clear prior calls in-place (keeps the same ArrayList reference).
        $global:PublishTestCalls.Clear()
        $global:PublishTestEnvWrites.Clear()
        # Reset the in-memory .env content to the default for each test.
        $global:PublishTestEnvContent = @('OPENCLAW_PACKAGE_VERSION=1.0.2.0')

        # Mock the .env file seam (imported from Publish.Env.psm1) at the
        # caller's scope so no disk .env is read or written. The pure helpers
        # (Get-EnvFileMap, Set-EnvFileValue, Step-PackageVersion) run for real.
        Mock Read-EnvFileContent {
            param([string]$Path)
            $null = $Path
            # Production-parity return shape: Publish.Env.psm1's real
            # Read-EnvFileContent returns via the unary-comma idiom
            # (return , ([string[]]@($lines))), which yields a single,
            # pipeline-safe string[] rather than letting PowerShell unroll it.
            return , ([string[]]@($global:PublishTestEnvContent))
        }
        Mock Write-EnvFileContent {
            param([string]$Path, [string[]]$Content)
            [void]$global:PublishTestEnvWrites.Add(
                [pscustomobject]@{ Path = $Path; Content = @($Content) }
            )
        }

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
        Mock Copy-InstallScriptsIntoBundle {
            $entry = [pscustomobject]@{ Name = 'Copy-InstallScriptsIntoBundle'; Args = [pscustomobject]@{ RepoRoot = $RepoRoot; BundleRoot = $BundleRoot } }
            [void]$global:PublishTestCalls.Add($entry)
        }
        Mock Write-PublishManifest {
            $entry = [pscustomobject]@{ Name = 'Write-PublishManifest'; Args = [pscustomobject]@{ BundleRoot = $BundleRoot; Version = $Version } }
            [void]$global:PublishTestCalls.Add($entry)
        }
        # Deterministically neutralize thumbprint resolution so the orchestrator's
        # signing gate is exercised independently of this machine's dotnet user
        # secret or OPENCLAW_CERT_THUMBPRINT env var (determinism rule: tests must
        # not depend on mutable machine/profile state). The default mock returns an
        # empty string, mirroring "no source supplied a thumbprint"; individual
        # tests can override it. The mock signature matches the production
        # named parameters exactly.
        Mock Resolve-CertThumbprint {
            param(
                [string]$ExplicitThumbprint = '',
                [string]$DotEnvThumbprint = '',
                [string]$ProjectPath = '',
                [string]$EnvThumbprint = ''
            )
            $null = $ProjectPath
            $null = $EnvThumbprint
            $entry = [pscustomobject]@{ Name = 'Resolve-CertThumbprint'; Args = [pscustomobject]@{ ExplicitThumbprint = $ExplicitThumbprint; DotEnvThumbprint = $DotEnvThumbprint; ProjectPath = $ProjectPath; EnvThumbprint = $EnvThumbprint } }
            [void]$global:PublishTestCalls.Add($entry)
            return ''
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
        It 'passes the gate and signs with the resolved thumbprint when -CertThumbprint is empty but resolution yields a value' {
            # Override the default empty-returning resolver mock for this case.
            Mock Resolve-CertThumbprint { return 'RESOLVEDABC123' }
            { & $script:ScriptPath -Version '1.2.3.0' } | Should -Not -Throw
            $signCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-SignTool' })
            $signCalls.Count | Should -Be 1
            $signCalls[0].Args.CertThumbprint | Should -Be 'RESOLVEDABC123'
        }
        It 'does not call Resolve-CertThumbprint when an explicit -CertThumbprint is supplied' {
            & $script:ScriptPath -Version '1.2.3.0' -CertThumbprint 'EXPLICITONLY' | Out-Null
            (@($global:PublishTestCalls | Where-Object { $_.Name -eq 'Resolve-CertThumbprint' })).Count | Should -Be 0
            $signCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-SignTool' })
            $signCalls[0].Args.CertThumbprint | Should -Be 'EXPLICITONLY'
        }
        It 'does not call Resolve-CertThumbprint when -SkipSign is supplied' {
            & $script:ScriptPath -Version '1.2.3.0' -SkipSign | Out-Null
            (@($global:PublishTestCalls | Where-Object { $_.Name -eq 'Resolve-CertThumbprint' })).Count | Should -Be 0
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
                'Copy-InstallScriptsIntoBundle',
                'Write-PublishManifest'
            )
            ($names -join ',') | Should -Be ($expectedPrefix -join ',')
        }
        It 'invokes Copy-InstallScriptsIntoBundle exactly once between Invoke-MakeAppx/Invoke-SignTool and Write-PublishManifest' {
            & $script:ScriptPath -Version '1.2.3.0' -CertThumbprint 'ABCDEF0123' | Out-Null

            $stageCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Copy-InstallScriptsIntoBundle' })
            $stageCalls.Count | Should -Be 1

            $names = @($global:PublishTestCalls | ForEach-Object { $_.Name })
            $idxMakeAppx = [array]::IndexOf($names, 'Invoke-MakeAppx')
            $idxSign = [array]::IndexOf($names, 'Invoke-SignTool')
            $idxStage = [array]::IndexOf($names, 'Copy-InstallScriptsIntoBundle')
            $idxManifest = [array]::IndexOf($names, 'Write-PublishManifest')

            $idxStage | Should -BeGreaterThan $idxMakeAppx
            $idxStage | Should -BeGreaterThan $idxSign
            $idxManifest | Should -BeGreaterThan $idxStage
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
            $idxStage = [array]::IndexOf($names, 'Copy-InstallScriptsIntoBundle')
            $idxManifest = [array]::IndexOf($names, 'Write-PublishManifest')
            $idxSign | Should -BeGreaterThan $idxMakeAppx
            $idxStage | Should -BeGreaterThan $idxSign
            $idxManifest | Should -BeGreaterThan $idxStage
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

    Context 'env-driven version (AC-1, AC-2, AC-3)' {
        It 'AC-1: with no -Version reads OPENCLAW_PACKAGE_VERSION, publishes the next revision, and persists it' {
            $global:PublishTestEnvContent = @('# header', 'OPENCLAW_PACKAGE_VERSION=1.0.2.0', 'OTHER=keep')
            & $script:ScriptPath -SkipSign -OutputDir 'D:\out' | Out-Null

            # Published version is the incremented revision (1.0.2.0 -> 1.0.2.1).
            $manifestCall = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Write-PublishManifest' })[0]
            $manifestCall.Args.Version | Should -Be '1.0.2.1'

            # The incremented value is persisted back to .env (update in place,
            # unrelated keys/comments preserved).
            $global:PublishTestEnvWrites.Count | Should -Be 1
            $written = @($global:PublishTestEnvWrites[0].Content)
            ($written -contains 'OPENCLAW_PACKAGE_VERSION=1.0.2.1') | Should -BeTrue
            ($written -contains 'OTHER=keep') | Should -BeTrue
            ($written -contains '# header') | Should -BeTrue
            (@($written | Where-Object { $_ -like 'OPENCLAW_PACKAGE_VERSION=*' })).Count | Should -Be 1
        }
        It 'AC-2: with -Version supplied uses it verbatim and persists it' {
            $global:PublishTestEnvContent = @('OPENCLAW_PACKAGE_VERSION=1.0.2.0')
            & $script:ScriptPath -Version '5.6.7.8' -SkipSign -OutputDir 'D:\out' | Out-Null

            $manifestCall = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Write-PublishManifest' })[0]
            $manifestCall.Args.Version | Should -Be '5.6.7.8'

            $global:PublishTestEnvWrites.Count | Should -Be 1
            $written = @($global:PublishTestEnvWrites[0].Content)
            ($written -contains 'OPENCLAW_PACKAGE_VERSION=5.6.7.8') | Should -BeTrue
        }
        It 'AC-3: with no -Version and a missing OPENCLAW_PACKAGE_VERSION throws before any state change' {
            $global:PublishTestEnvContent = @('OTHER=x')
            { & $script:ScriptPath -SkipSign -OutputDir 'D:\out' } |
                Should -Throw -ExpectedMessage '*OPENCLAW_PACKAGE_VERSION is missing or blank*'
            # No state-changing stage ran and nothing was persisted to .env.
            $global:PublishTestEnvWrites.Count | Should -Be 0
            (@($global:PublishTestCalls | Where-Object { $_.Name -eq 'Write-PublishManifest' })).Count | Should -Be 0
        }
        It 'AC-3: with no -Version and a blank OPENCLAW_PACKAGE_VERSION throws before any state change' {
            $global:PublishTestEnvContent = @('OPENCLAW_PACKAGE_VERSION=   ')
            { & $script:ScriptPath -SkipSign -OutputDir 'D:\out' } |
                Should -Throw -ExpectedMessage '*OPENCLAW_PACKAGE_VERSION is missing or blank*'
            $global:PublishTestEnvWrites.Count | Should -Be 0
        }
        It 'does not persist the version when the signing gate fails (no -SkipSign, no thumbprint)' {
            $global:PublishTestEnvContent = @('OPENCLAW_PACKAGE_VERSION=1.0.2.0')
            { & $script:ScriptPath -Version '1.2.3.0' } |
                Should -Throw -ExpectedMessage '*Either -SkipSign or a non-empty -CertThumbprint*'
            $global:PublishTestEnvWrites.Count | Should -Be 0
        }
        It 'preserves a multi-line .env verbatim and updates only OPENCLAW_PACKAGE_VERSION in place (regression: redundant @() wrap)' {
            # Regression fixture: a comment line plus two KEY=value lines. A
            # redundant @(Read-EnvFileContent ...) wrap at the call site nests
            # the mocked return in a one-element array; binding that to a
            # [string[]] parameter joins every element with $OFS (space),
            # collapsing all lines into one and appending the target key as a
            # duplicate second line. This test fails under that regression and
            # passes once the call site assigns the seam's return value directly.
            $global:PublishTestEnvContent = @(
                '# leading comment',
                'OPENCLAW_PACKAGE_VERSION=1.0.2.0',
                'OTHER_KEY=unchanged'
            )
            & $script:ScriptPath -SkipSign -OutputDir 'D:\out' | Out-Null

            $global:PublishTestEnvWrites.Count | Should -Be 1
            $written = @($global:PublishTestEnvWrites[0].Content)

            # Not collapsed into a single space-joined line: line count matches
            # the original fixture's line count.
            $written.Count | Should -Be 3

            # Every original line is preserved verbatim except the updated key.
            ($written -contains '# leading comment') | Should -BeTrue
            ($written -contains 'OTHER_KEY=unchanged') | Should -BeTrue

            # The target key is updated in place with no duplicate.
            $versionLines = @($written | Where-Object { $_ -like 'OPENCLAW_PACKAGE_VERSION=*' })
            $versionLines.Count | Should -Be 1
            $versionLines[0] | Should -Be 'OPENCLAW_PACKAGE_VERSION=1.0.2.1'
        }
    }

    Context 'Stage 0a .env thumbprint resolution (AC-4, D7)' {
        It 'passes the .env OPENCLAW_CERT_THUMBPRINT to the new -DotEnvThumbprint precedence parameter' {
            $global:PublishTestEnvContent = @(
                'OPENCLAW_PACKAGE_VERSION=1.0.2.0',
                'OPENCLAW_CERT_THUMBPRINT=DOTENVTHUMB'
            )
            # Return a resolved value so the signing gate passes; capture the args.
            Mock Resolve-CertThumbprint {
                param(
                    [string]$ExplicitThumbprint = '',
                    [string]$DotEnvThumbprint = '',
                    [string]$ProjectPath = '',
                    [string]$EnvThumbprint = ''
                )
                $entry = [pscustomobject]@{ Name = 'Resolve-CertThumbprint'; Args = [pscustomobject]@{ ExplicitThumbprint = $ExplicitThumbprint; DotEnvThumbprint = $DotEnvThumbprint; ProjectPath = $ProjectPath; EnvThumbprint = $EnvThumbprint } }
                [void]$global:PublishTestCalls.Add($entry)
                return 'DOTENVTHUMB'
            }
            & $script:ScriptPath -Version '1.2.3.0' -OutputDir 'D:\out' | Out-Null
            $resolveCall = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Resolve-CertThumbprint' })[0]
            $resolveCall.Args.DotEnvThumbprint | Should -Be 'DOTENVTHUMB'
            $resolveCall.Args.EnvThumbprint | Should -Be ([string]$env:OPENCLAW_CERT_THUMBPRINT)
        }
        It 'AC-4: at the call site, .env OPENCLAW_CERT_THUMBPRINT beats both the dotnet user secret and the process-env value' {
            # Drive the REAL Resolve-CertThumbprint (from Publish.Helpers) by
            # mocking only the Invoke-DotnetExe wrapper (user secret) and setting
            # a distinct process-env value; assert .env wins.
            $global:PublishTestEnvContent = @(
                'OPENCLAW_PACKAGE_VERSION=1.0.2.0',
                'OPENCLAW_CERT_THUMBPRINT=FROMDOTENV'
            )
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = FROMUSERSECRET'
            }
            # Use the real resolver (remove the default empty-returning mock for
            # this It) and inject a process-env value via the seam.
            Mock Resolve-CertThumbprint {
                param(
                    [string]$ExplicitThumbprint = '',
                    [string]$DotEnvThumbprint = '',
                    [string]$ProjectPath = '',
                    [string]$EnvThumbprint = ''
                )
                # Delegate to the real module function to exercise precedence.
                $resolved = & (Get-Module Publish.Helpers) {
                    param($e, $d, $p, $v)
                    Resolve-CertThumbprint -ExplicitThumbprint $e -DotEnvThumbprint $d -ProjectPath $p -EnvThumbprint $v
                } $ExplicitThumbprint $DotEnvThumbprint $ProjectPath $EnvThumbprint
                $entry = [pscustomobject]@{ Name = 'Invoke-SignTool-precedence'; Args = [pscustomobject]@{ Resolved = $resolved } }
                [void]$global:PublishTestCalls.Add($entry)
                return $resolved
            }
            & $script:ScriptPath -Version '1.2.3.0' -OutputDir 'D:\out' | Out-Null
            $signCalls = @($global:PublishTestCalls | Where-Object { $_.Name -eq 'Invoke-SignTool' })
            $signCalls.Count | Should -Be 1
            $signCalls[0].Args.CertThumbprint | Should -Be 'FROMDOTENV'
        }
    }
}
