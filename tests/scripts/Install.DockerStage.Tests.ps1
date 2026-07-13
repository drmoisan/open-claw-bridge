#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 stage-sequence tests for the scripts/Install.ps1 Docker image-load
    stage (issue #142).

.DESCRIPTION
    Uses the Install-orchestrator harness (mirrors tests/scripts/Install.Force.Tests.ps1)
    to invoke Install.ps1 via '& $ScriptPath' with a full helper-mock set, plus a
    recording mock for Invoke-DockerImageLoad. Asserts that Stage 9 loads the
    bundled image tar before compose up, the tar path is correct, the load runs on
    a -Force reinstall, and the load is skipped under -SkipDocker.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share a call log across scopes.'
)]
param()

Describe 'scripts/Install.ps1 - Docker image-load stage' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Install.ps1'
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        $script:PreflightPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1'
        $script:DockerModulePath = Join-Path $PSScriptRoot '..\..\scripts\Install.Docker.psm1'
        Import-Module $script:HelpersPath -Force
        Import-Module $script:PreflightPath -Force
        Import-Module $script:DockerModulePath -Force
        $global:InstallTestCalls = [System.Collections.ArrayList]::new()

        # Override LOCALAPPDATA so the script resolves stable test paths.
        $global:OriginalLOCALAPPDATA = $env:LOCALAPPDATA
        $env:LOCALAPPDATA = 'C:\TestAppData\Local'
    }

    AfterAll {
        if ($null -ne $global:OriginalLOCALAPPDATA) {
            $env:LOCALAPPDATA = $global:OriginalLOCALAPPDATA
        }
        Remove-Variable -Name InstallTestCalls -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name OriginalLOCALAPPDATA -Scope Global -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $global:InstallTestCalls.Clear()
        $global:LastGetManifestVersionBundleRoot = $null
        $global:LastImageTarPath = $null

        # Helper mocks (module-exported functions intercepted at the caller's scope).
        Mock Get-ManifestVersion {
            param($BundleRoot)
            [void]$global:InstallTestCalls.Add('Get-ManifestVersion')
            $global:LastGetManifestVersionBundleRoot = $BundleRoot
            '1.2.3.0'
        }
        Mock Test-ManifestIntegrity { [void]$global:InstallTestCalls.Add('Test-ManifestIntegrity') }
        Mock Test-DockerAvailable { [void]$global:InstallTestCalls.Add('Test-DockerAvailable'); $true }
        Mock Copy-BundleContents { [void]$global:InstallTestCalls.Add('Copy-BundleContents') }
        Mock Initialize-DotEnv { [void]$global:InstallTestCalls.Add('Initialize-DotEnv') }
        Mock Invoke-MsixInstall { [void]$global:InstallTestCalls.Add('Invoke-MsixInstall') }
        Mock Invoke-MsixCapture { [void]$global:InstallTestCalls.Add('Invoke-MsixCapture'); 'OpenClaw.MailBridge_1.2.3.0_x64__abc' }
        Mock Invoke-MsixAppActivate { [void]$global:InstallTestCalls.Add('Invoke-MsixAppActivate') }
        Mock Invoke-MsixRemove { [void]$global:InstallTestCalls.Add('Invoke-MsixRemove') }
        Mock Invoke-ComposeUp { [void]$global:InstallTestCalls.Add('Invoke-ComposeUp') }
        Mock Wait-ComposeHealthy { [void]$global:InstallTestCalls.Add('Wait-ComposeHealthy') }
        Mock Invoke-ComposeDown { [void]$global:InstallTestCalls.Add('Invoke-ComposeDown') }
        Mock Write-InstallRecord { [void]$global:InstallTestCalls.Add('Write-InstallRecord') }
        Mock Invoke-DockerImageLoad {
            param($ImageTarPath)
            $global:LastImageTarPath = $ImageTarPath
            [void]$global:InstallTestCalls.Add('Invoke-DockerImageLoad')
        }
        function global:Invoke-HostAdapterStart { [void]$global:InstallTestCalls.Add('Invoke-HostAdapterStart') }
        Mock Read-InstallRecord {
            [void]$global:InstallTestCalls.Add('Read-InstallRecord')
            [pscustomobject]@{
                version            = '1.2.3.0'
                destinationPath    = 'C:\TestAppData\Local\OpenClaw\1.2.3.0'
                packageFullName    = 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
                composeProjectName = 'openclaw'
                composeFilePath    = 'C:\TestAppData\Local\OpenClaw\1.2.3.0\docker\docker-compose.yml'
                skipDocker         = $false
                allowUnsigned      = $false
            }
        }

        # Filesystem shims. Test-Path default: MSIX exists; prior-install does NOT.
        Mock New-Item { [void]$global:InstallTestCalls.Add('New-Item') }
        Mock Copy-Item { [void]$global:InstallTestCalls.Add('Copy-Item') }
        Mock Remove-Item { [void]$global:InstallTestCalls.Add('Remove-Item') } -ParameterFilter { ($Path -notlike 'Function:\*') -and ($LiteralPath -notlike 'Function:\*') }
        $script:GetContentMock = {
            param($LiteralPath)
            if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
            }
            if ($LiteralPath -like '*docker-compose.yml') {
                return @(
                    'services:'
                    '  openclaw-core:'
                    '    image: openclaw/core:1.2.3.0'
                    '  openclaw-agent:'
                    '    image: openclaw/agent:1.2.3.0'
                )
            }
            return 'test-hostadapter-token'
        }
        Mock Get-Content $script:GetContentMock
        Mock Get-Content $script:GetContentMock -ModuleName Install.Preflight
        $script:EnvMapMock = {
            param($EnvFilePath)
            $null = $EnvFilePath
            @{ 'OPENCLAW_GATEWAY_TOKEN' = 'test-gateway-token' }
        }
        Mock Get-InstallEnvFileMap $script:EnvMapMock
        Mock Get-InstallEnvFileMap $script:EnvMapMock -ModuleName Install.Preflight
        $script:DefaultStatusResponse = {
            [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{"ok":true,"data":{"state":"running","mode":"x"},"meta":{"requestId":"r","adapterVersion":"1.0.0.0","bridge":null},"error":null}' }
        }
        Mock Invoke-WebRequest $script:DefaultStatusResponse
        Mock Invoke-WebRequest $script:DefaultStatusResponse -ModuleName Install.Helpers
        Mock Invoke-HostAdapterStatusRequest {
            [void]$global:InstallTestCalls.Add('Invoke-HostAdapterStatusRequest')
            [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{"ok":true,"data":{"state":"running","mode":"x"},"meta":{"requestId":"r","adapterVersion":"1.0.0.0","bridge":null},"error":null}' }
        } -ModuleName Install.Preflight
        Mock Assert-HostAdapterRespondingPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterRespondingPreflight') }
        Mock Assert-HostAdapterBridgeReadyPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterBridgeReadyPreflight') }
        $script:TestPathMock = {
            param($LiteralPath)
            if ($LiteralPath -like '*install-record.json') { return $false }
            if ($LiteralPath -like '*TestAppData*OpenClaw*docker*secrets*.env.anthropic') { return $true }
            if ($LiteralPath -like '*TestAppData*OpenClaw*docker*.env') { return $true }
            if ($LiteralPath -like '*TestAppData*OpenClaw*docker/.env') { return $true }
            if ($LiteralPath -like '*TestAppData*OpenClaw\*') { return $false }
            if ($LiteralPath -like '*TestAppData*OpenClaw/*') { return $false }
            $true
        }
        Mock Test-Path $script:TestPathMock
        Mock Test-Path $script:TestPathMock -ModuleName Install.Preflight

        function global:Test-IsElevatedAdmin { $true }
    }

    AfterEach {
        Remove-Item -Path 'Function:\Test-IsElevatedAdmin' -Force -ErrorAction SilentlyContinue
        Remove-Item -Path 'Function:\Invoke-HostAdapterStart' -Force -ErrorAction SilentlyContinue
        Remove-Variable -Name LastImageTarPath -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name capturedRecord -Scope Global -ErrorAction SilentlyContinue
    }

    Context 'image load stage' {
        It 'loads the tar at <DestDockerDir>\openclaw-images.tar' {
            & $script:ScriptPath | Out-Null
            $global:LastImageTarPath | Should -Be 'C:\TestAppData\Local\OpenClaw\1.2.3.0\docker\openclaw-images.tar'
        }

        It 'loads the image after copying bundle contents and before compose up' {
            & $script:ScriptPath | Out-Null
            $idxCopy = $global:InstallTestCalls.IndexOf('Copy-BundleContents')
            $idxLoad = $global:InstallTestCalls.IndexOf('Invoke-DockerImageLoad')
            $idxUp = $global:InstallTestCalls.IndexOf('Invoke-ComposeUp')
            $idxCopy | Should -BeGreaterOrEqual 0
            $idxLoad | Should -BeGreaterThan $idxCopy
            $idxUp | Should -BeGreaterThan $idxLoad
        }

        It 'still loads the image before compose up on a -Force reinstall' {
            $forceTestPath = {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $true }
                $true
            }
            Mock Test-Path $forceTestPath
            Mock Test-Path $forceTestPath -ModuleName Install.Preflight
            & $script:ScriptPath -Force | Out-Null
            $idxLoad = $global:InstallTestCalls.IndexOf('Invoke-DockerImageLoad')
            $idxUp = $global:InstallTestCalls.IndexOf('Invoke-ComposeUp')
            $idxLoad | Should -BeGreaterOrEqual 0
            $idxUp | Should -BeGreaterThan $idxLoad
        }
    }

    Context '-SkipDocker path' {
        It 'does NOT invoke Test-DockerAvailable' {
            & $script:ScriptPath -SkipDocker | Out-Null
            $global:InstallTestCalls -contains 'Test-DockerAvailable' | Should -BeFalse
        }

        It 'does NOT invoke Invoke-ComposeUp or Wait-ComposeHealthy' {
            & $script:ScriptPath -SkipDocker | Out-Null
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
            $global:InstallTestCalls -contains 'Wait-ComposeHealthy' | Should -BeFalse
        }

        It 'records skipDocker = $true in the install record' {
            $global:capturedRecord = $null
            Mock Write-InstallRecord {
                param($Record)
                $global:capturedRecord = $Record
                [void]$global:InstallTestCalls.Add('Write-InstallRecord')
            }
            & $script:ScriptPath -SkipDocker | Out-Null
            $global:capturedRecord.skipDocker | Should -BeTrue
        }

        It 'does NOT invoke Invoke-DockerImageLoad under -SkipDocker' {
            & $script:ScriptPath -SkipDocker | Out-Null
            $global:InstallTestCalls -contains 'Invoke-DockerImageLoad' | Should -BeFalse
        }
    }

    Context 'image version alignment guard' {
        It 'proceeds to Invoke-DockerImageLoad then Invoke-ComposeUp when both compose tags match ResolvedVersion' {
            & $script:ScriptPath | Out-Null
            $idxLoad = $global:InstallTestCalls.IndexOf('Invoke-DockerImageLoad')
            $idxUp = $global:InstallTestCalls.IndexOf('Invoke-ComposeUp')
            $idxLoad | Should -BeGreaterOrEqual 0
            $idxUp | Should -BeGreaterThan $idxLoad
        }

        It 'throws before Invoke-DockerImageLoad when the core and agent compose tags disagree with each other' {
            $mismatchedContentMock = {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                    return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
                }
                if ($LiteralPath -like '*docker-compose.yml') {
                    return @(
                        '    image: openclaw/core:1.2.3.0'
                        '    image: openclaw/agent:1.2.4.0'
                    )
                }
                return 'test-hostadapter-token'
            }
            Mock Get-Content $mismatchedContentMock

            { & $script:ScriptPath | Out-Null } | Should -Throw

            $global:InstallTestCalls -notcontains 'Invoke-DockerImageLoad' | Should -BeTrue
        }

        It 'throws when both compose tags agree with each other but disagree with ResolvedVersion' {
            $sameWrongContentMock = {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                    return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
                }
                if ($LiteralPath -like '*docker-compose.yml') {
                    return @(
                        '    image: openclaw/core:1.2.2.0'
                        '    image: openclaw/agent:1.2.2.0'
                    )
                }
                return 'test-hostadapter-token'
            }
            Mock Get-Content $sameWrongContentMock

            { & $script:ScriptPath | Out-Null } | Should -Throw

            $global:InstallTestCalls -notcontains 'Invoke-DockerImageLoad' | Should -BeTrue
        }

        It 'throws a distinct error when the compose file is missing an image: line for a service' {
            $missingImageContentMock = {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                    return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
                }
                if ($LiteralPath -like '*docker-compose.yml') {
                    return @(
                        '    image: openclaw/core:1.2.3.0'
                    )
                }
                return 'test-hostadapter-token'
            }
            Mock Get-Content $missingImageContentMock

            { & $script:ScriptPath | Out-Null } | Should -Throw '*openclaw/agent*'

            $global:InstallTestCalls -notcontains 'Invoke-DockerImageLoad' | Should -BeTrue
        }

        It 'skips the guard entirely under -SkipDocker even with mismatched compose tags' {
            $mismatchedContentMock = {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                    return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
                }
                if ($LiteralPath -like '*docker-compose.yml') {
                    return @(
                        '    image: openclaw/core:1.2.3.0'
                        '    image: openclaw/agent:1.2.4.0'
                    )
                }
                return 'test-hostadapter-token'
            }
            Mock Get-Content $mismatchedContentMock

            { & $script:ScriptPath -SkipDocker | Out-Null } | Should -Not -Throw

            $global:InstallTestCalls -contains 'Invoke-DockerImageLoad' | Should -BeFalse
        }
    }
}
