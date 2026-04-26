#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Install.ps1.

.DESCRIPTION
    Invokes the orchestrator via '& $ScriptPath' with a full set of helper
    mocks (Install.Helpers.psm1 pre-imported), then asserts parameter
    binding, stage ordering, and per-switch branching.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share a call log across scopes.'
)]
param()

Describe 'scripts/Install.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Install.ps1'
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        Import-Module $script:HelpersPath -Force
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
        Mock Invoke-MsixRemove { [void]$global:InstallTestCalls.Add('Invoke-MsixRemove') }
        Mock Invoke-ComposeUp { [void]$global:InstallTestCalls.Add('Invoke-ComposeUp') }
        Mock Wait-ComposeHealthy { [void]$global:InstallTestCalls.Add('Wait-ComposeHealthy') }
        Mock Invoke-ComposeDown { [void]$global:InstallTestCalls.Add('Invoke-ComposeDown') }
        Mock Write-InstallRecord { [void]$global:InstallTestCalls.Add('Write-InstallRecord') }
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
        Mock Get-Content {
            param($LiteralPath)
            if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
            }
            return 'test-hostadapter-token'
        }
        Mock Invoke-WebRequest {
            [void]$global:InstallTestCalls.Add('Invoke-WebRequest')
            [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{}' }
        }
        Mock Test-Path {
            param($LiteralPath)
            if ($LiteralPath -like '*install-record.json') { return $false }
            if ($LiteralPath -like '*TestAppData*OpenClaw*docker*secrets*.env.anthropic') { return $true }
            if ($LiteralPath -like '*TestAppData*OpenClaw*docker*.env') { return $true }
            if ($LiteralPath -like '*TestAppData*OpenClaw*docker/.env') { return $true }
            # Any destination under LOCALAPPDATA\OpenClaw\<leaf> must default to absent so
            # tests that supply -SourcePath or -Version do not trip the prior-install guard.
            if ($LiteralPath -like '*TestAppData*OpenClaw\*') { return $false }
            if ($LiteralPath -like '*TestAppData*OpenClaw/*') { return $false }
            $true
        }

        # Elevation-probe default: treat test host as elevated so -AllowUnsigned paths
        # proceed through the precheck. Individual It blocks can override.
        function global:Test-IsElevatedAdmin { $true }
    }

    AfterEach {
        Remove-Item -Path 'Function:\Test-IsElevatedAdmin' -Force -ErrorAction SilentlyContinue
        Remove-Item -Path 'Function:\Invoke-HostAdapterStart' -Force -ErrorAction SilentlyContinue
        Remove-Variable -Name CapturedHostAdapterPreflightUri -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name CapturedHostAdapterPreflightAuthorization -Scope Global -ErrorAction SilentlyContinue
    }

    Context 'parameter binding' {
        It 'accepts the refinement parameter set with defaults' {
            { & $script:ScriptPath -WhatIf } | Should -Not -Throw
        }

        It 'accepts operator Docker env file parameters' {
            {
                & $script:ScriptPath `
                    -DockerEnvFilePath 'C:\operator\.env' `
                    -AnthropicEnvFilePath 'C:\operator\secrets\.env.anthropic' `
                    -WhatIf
            } | Should -Not -Throw
        }

        It '-SourcePath overrides the default $PSScriptRoot' {
            & $script:ScriptPath -SourcePath 'C:\custom\bundle' | Out-Null
            # Find-NewestPublishVersion has been retired and must never be called.
            ($global:InstallTestCalls -contains 'Find-NewestPublishVersion') | Should -BeFalse
            $global:InstallTestCalls[0] | Should -Be 'Get-ManifestVersion'
            $global:LastGetManifestVersionBundleRoot | Should -Be 'C:\custom\bundle'
        }

        It 'defaults -SourcePath to $PSScriptRoot when not supplied' {
            & $script:ScriptPath | Out-Null
            # The script resolves $PSScriptRoot to the directory scripts/Install.ps1
            # lives in (the repo scripts/ folder during tests). The orchestrator's
            # $PSScriptRoot is always the fully resolved path.
            $expected = (Resolve-Path (Split-Path -Path $script:ScriptPath -Parent)).Path
            $global:LastGetManifestVersionBundleRoot | Should -Be $expected
        }
    }

    Context 'administrator precheck on -AllowUnsigned' {
        It 'throws before any helper runs when -AllowUnsigned is set and the probe returns false' {
            function global:Test-IsElevatedAdmin { $false }
            { & $script:ScriptPath -AllowUnsigned } |
                Should -Throw -ExpectedMessage '*AllowUnsigned requires*administrator*Relaunch PowerShell as administrator*'
            $global:InstallTestCalls -contains 'Test-ManifestIntegrity' | Should -BeFalse
        }

        It 'proceeds through the install when -AllowUnsigned is set and the probe returns true' {
            function global:Test-IsElevatedAdmin { $true }
            & $script:ScriptPath -AllowUnsigned | Out-Null
            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeTrue
        }

        It 'does not attempt the admin probe when -AllowUnsigned is NOT supplied' {
            # Force a false probe; without -AllowUnsigned the precheck path must be skipped.
            function global:Test-IsElevatedAdmin { $false }
            & $script:ScriptPath | Out-Null
            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeTrue
        }
    }

    Context 'stage ordering (happy path)' {
        It 'calls helpers in the correct order' {
            & $script:ScriptPath | Out-Null
            $helperOrder = $global:InstallTestCalls | Where-Object { $_ -ne 'New-Item' -and $_ -ne 'Remove-Item' }
            $expected = @(
                'Get-ManifestVersion',
                'Test-ManifestIntegrity',
                'Test-DockerAvailable',
                'Copy-BundleContents',
                'Initialize-DotEnv',
                'Invoke-HostAdapterStart',
                'Invoke-WebRequest',
                'Invoke-MsixInstall',
                'Invoke-MsixCapture',
                'Invoke-ComposeUp',
                'Wait-ComposeHealthy',
                'Write-InstallRecord'
            )
            ($helperOrder -join ',') | Should -Be ($expected -join ',')
        }
    }

    Context 'operator Docker env file staging' {
        It 'copies supplied operator env files after .env initialization and before MSIX install' {
            & $script:ScriptPath `
                -DockerEnvFilePath 'C:\operator\.env' `
                -AnthropicEnvFilePath 'C:\operator\secrets\.env.anthropic' | Out-Null

            $idxInitialize = $global:InstallTestCalls.IndexOf('Initialize-DotEnv')
            $idxCopy = $global:InstallTestCalls.IndexOf('Copy-Item')
            $idxMsix = $global:InstallTestCalls.IndexOf('Invoke-MsixInstall')

            $idxInitialize | Should -BeGreaterOrEqual 0
            $idxCopy | Should -BeGreaterThan $idxInitialize
            $idxMsix | Should -BeGreaterThan $idxCopy
            Should -Invoke -CommandName Copy-Item -Times 1 -Exactly -ParameterFilter { $LiteralPath -eq 'C:\operator\.env' -and $Destination -like '*\docker\.env' -and $Force }
            Should -Invoke -CommandName Copy-Item -Times 1 -Exactly -ParameterFilter { $LiteralPath -eq 'C:\operator\secrets\.env.anthropic' -and $Destination -like '*\docker\secrets\.env.anthropic' -and $Force }
        }

        It 'throws before manifest validation when a supplied operator env file is absent' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -eq 'C:\missing\.env') { return $false }
                if ($LiteralPath -like '*install-record.json') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw*docker*secrets*.env.anthropic') { return $true }
                if ($LiteralPath -like '*TestAppData*OpenClaw\*') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw/*') { return $false }
                $true
            }

            { & $script:ScriptPath -DockerEnvFilePath 'C:\missing\.env' } |
                Should -Throw -ExpectedMessage '*-DockerEnvFilePath*not found*outside artifacts/publish*'

            $global:InstallTestCalls -contains 'Get-ManifestVersion' | Should -BeFalse
            $global:InstallTestCalls -contains 'Test-ManifestIntegrity' | Should -BeFalse
        }
    }

    Context 'OPENCLAW_GATEWAY_TOKEN guard' {
        It 'throws before MSIX install when the staged .env has an empty OPENCLAW_GATEWAY_TOKEN' {
            Mock Get-Content {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env') {
                    return @('OPENCLAW_GATEWAY_TOKEN=')
                }
                return 'test-hostadapter-token'
            }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*OPENCLAW_GATEWAY_TOKEN*Invoke-OpenClawAgentOnboarding.ps1*SkipDocker*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
            $global:InstallTestCalls -contains 'Wait-ComposeHealthy' | Should -BeFalse
        }

        It 'throws before MSIX install when the staged .env is missing OPENCLAW_GATEWAY_TOKEN entirely' {
            Mock Get-Content {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env') {
                    return @('OPENCLAW_AGENT_PORT=18789')
                }
                return 'test-hostadapter-token'
            }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*OPENCLAW_GATEWAY_TOKEN*Invoke-OpenClawAgentOnboarding.ps1*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
        }

        It 'does not run the gateway token guard when -SkipDocker is supplied' {
            Mock Get-Content {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env') {
                    return @('OPENCLAW_GATEWAY_TOKEN=')
                }
                return 'test-hostadapter-token'
            }

            { & $script:ScriptPath -SkipDocker } | Should -Not -Throw
            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeTrue
        }
    }

    Context 'Docker runtime input preflight' {
        It 'throws before MSIX install when Docker is enabled and the Anthropic env file is absent' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw*docker*secrets*.env.anthropic') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw\*') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw/*') { return $false }
                $true
            }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*Required Docker secret file not found*-AnthropicEnvFilePath*-SkipDocker*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
        }

        It 'throws before compose up when the HostAdapter status probe is not ready' {
            Mock Invoke-WebRequest { [pscustomobject]@{ StatusCode = 503; Headers = @{}; Content = '{}' } }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*HostAdapter preflight failed before starting Docker*HTTP 503*OpenClaw.MailBridge*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
            $global:InstallTestCalls -contains 'Wait-ComposeHealthy' | Should -BeFalse
        }

        It 'does not install MSIX when the HostAdapter status probe throws on unreachable endpoint' {
            Mock Invoke-WebRequest { throw [System.Net.WebException] 'Connection refused' }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*HostAdapter preflight failed before starting Docker*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
        }

        It 'probes the host-loopback HostAdapter status URI before compose up' {
            $global:CapturedHostAdapterPreflightUri = $null
            Mock Invoke-WebRequest {
                param([uri]$Uri)
                $global:CapturedHostAdapterPreflightUri = [string]$Uri
                [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{}' }
            }

            & $script:ScriptPath | Out-Null

            $global:CapturedHostAdapterPreflightUri | Should -Be 'http://127.0.0.1:4319/v1/status'
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeTrue
        }

        It 'uses installed docker env HostAdapter settings for the preflight' {
            $global:CapturedHostAdapterPreflightUri = $null
            $global:CapturedHostAdapterPreflightAuthorization = $null
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw*docker*.env') { return $true }
                if ($LiteralPath -like '*TestAppData*OpenClaw*docker*secrets*.env.anthropic') { return $true }
                if ($LiteralPath -eq 'C:\tokens\adapter.token') { return $true }
                if ($LiteralPath -like '*TestAppData*OpenClaw\*') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw/*') { return $false }
                $true
            }
            Mock Get-Content {
                param($LiteralPath)
                if ($LiteralPath -like '*docker*.env') {
                    return @(
                        'OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:5319/v1',
                        'HOSTADAPTER_TOKEN_FILE=C:\tokens\adapter.token',
                        'OPENCLAW_GATEWAY_TOKEN=custom-gateway-token'
                    )
                }
                return 'custom-token'
            }
            Mock Invoke-WebRequest {
                param([uri]$Uri, [hashtable]$Headers)
                $global:CapturedHostAdapterPreflightUri = [string]$Uri
                $global:CapturedHostAdapterPreflightAuthorization = [string]$Headers.Authorization
                [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{}' }
            }

            & $script:ScriptPath | Out-Null

            $global:CapturedHostAdapterPreflightUri | Should -Be 'http://127.0.0.1:5319/v1/status'
            $global:CapturedHostAdapterPreflightAuthorization | Should -Be 'Bearer custom-token'
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
    }

    Context 'manifest integrity failure' {
        It 'throws and never invokes helpers after Test-ManifestIntegrity' {
            Mock Test-ManifestIntegrity {
                [void]$global:InstallTestCalls.Add('Test-ManifestIntegrity')
                throw 'Manifest integrity check failed for bundle X. Discrepancies: SHA-256 mismatch: foo.exe'
            }
            { & $script:ScriptPath } | Should -Throw -ExpectedMessage '*Manifest integrity check failed*'
            $global:InstallTestCalls -contains 'New-Item' | Should -BeFalse
            $global:InstallTestCalls -contains 'Copy-BundleContents' | Should -BeFalse
        }
    }

    Context 'docker not running' {
        It 'throws with remediation containing -SkipDocker' {
            Mock Test-DockerAvailable {
                [void]$global:InstallTestCalls.Add('Test-DockerAvailable')
                throw 'Docker Desktop is not running or not installed. Start Docker Desktop and retry, or pass -SkipDocker to skip the container stage.'
            }
            { & $script:ScriptPath } | Should -Throw -ExpectedMessage '*-SkipDocker*'
            $global:InstallTestCalls -contains 'Copy-BundleContents' | Should -BeFalse
        }
    }

    Context 'bundle-root self-location' {
        It 'computes $BundleRoot = $PSScriptRoot when -SourcePath is not supplied and passes that value to Get-ManifestVersion and Test-ManifestIntegrity' {
            $global:LastTestManifestBundleRoot = $null
            Mock Test-ManifestIntegrity {
                param($BundleRoot)
                [void]$global:InstallTestCalls.Add('Test-ManifestIntegrity')
                $global:LastTestManifestBundleRoot = $BundleRoot
            }
            & $script:ScriptPath | Out-Null
            $expected = (Resolve-Path (Split-Path -Path $script:ScriptPath -Parent)).Path
            $global:LastGetManifestVersionBundleRoot | Should -Be $expected
            $global:LastTestManifestBundleRoot | Should -Be $expected
        }

        It 'throws with the bundle root path in the message when manifest.json is absent' {
            # Override the BeforeEach Test-Path mock so the bundle-selection guard
            # fires on the missing manifest.json. The orchestrator throws BEFORE
            # any helper runs.
            $missingBundle = 'C:\nowhere\empty-bundle'
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -eq (Join-Path $missingBundle 'manifest.json')) { return $false }
                if ($LiteralPath -like '*install-record.json') { return $false }
                $true
            }
            { & $script:ScriptPath -SourcePath $missingBundle } |
                Should -Throw -ExpectedMessage "*empty-bundle*manifest.json*"
            # The early abort runs before Get-ManifestVersion / Test-ManifestIntegrity.
            $global:InstallTestCalls -contains 'Get-ManifestVersion' | Should -BeFalse
            $global:InstallTestCalls -contains 'Test-ManifestIntegrity' | Should -BeFalse
        }
    }

    Context 'MSIX missing' {
        It 'throws with the expected path when the MSIX is absent' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $false }
                if ($LiteralPath -like '*TestAppData*OpenClaw*1.2.3.0') { return $false }
                if ($LiteralPath -like '*OpenClaw.MailBridge_*_x64.msix') { return $false }
                $true
            }
            { & $script:ScriptPath } | Should -Throw -ExpectedMessage '*OpenClaw.MailBridge_1.2.3.0_x64.msix*'
            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
        }
    }
}
