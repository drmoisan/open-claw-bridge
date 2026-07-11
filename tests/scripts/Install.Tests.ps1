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
        Mock Invoke-DockerImageLoad { [void]$global:InstallTestCalls.Add('Invoke-DockerImageLoad') }
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
        $script:GetContentMock = {
            param($LiteralPath)
            if ($LiteralPath -like '*docker*.env' -or $LiteralPath -like '*docker/.env') {
                return @('OPENCLAW_GATEWAY_TOKEN=test-gateway-token')
            }
            return 'test-hostadapter-token'
        }
        Mock Get-Content $script:GetContentMock
        Mock Get-Content $script:GetContentMock -ModuleName Install.Preflight
        # Default env-map mock so Assert-StagedGatewayTokenPresent (called from Install.ps1
        # script scope) and the Stage 7/8.5 preflight prelude both see the test token map
        # without depending on in-module Get-Content interception.
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
        # Default preflight stubs that no-op, so script-level happy-path tests run without
        # relying on Invoke-WebRequest mock interception inside imported modules.
        Mock Assert-HostAdapterRespondingPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterRespondingPreflight') }
        Mock Assert-HostAdapterBridgeReadyPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterBridgeReadyPreflight') }
        $script:TestPathMock = {
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
        Mock Test-Path $script:TestPathMock
        Mock Test-Path $script:TestPathMock -ModuleName Install.Preflight

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
                'Assert-HostAdapterRespondingPreflight',
                'Invoke-MsixInstall',
                'Invoke-MsixCapture',
                'Invoke-MsixAppActivate',
                'Assert-HostAdapterBridgeReadyPreflight',
                'Invoke-DockerImageLoad',
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
            $emptyTokenMap = { @{ 'OPENCLAW_GATEWAY_TOKEN' = '' } }
            Mock Get-InstallEnvFileMap $emptyTokenMap
            Mock Get-InstallEnvFileMap $emptyTokenMap -ModuleName Install.Preflight

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*OPENCLAW_GATEWAY_TOKEN*Invoke-OpenClawAgentOnboarding.ps1*SkipDocker*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
            $global:InstallTestCalls -contains 'Wait-ComposeHealthy' | Should -BeFalse
        }

        It 'throws before MSIX install when the staged .env is missing OPENCLAW_GATEWAY_TOKEN entirely' {
            $missingTokenMap = { @{ 'OPENCLAW_AGENT_PORT' = '18789' } }
            Mock Get-InstallEnvFileMap $missingTokenMap
            Mock Get-InstallEnvFileMap $missingTokenMap -ModuleName Install.Preflight

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*OPENCLAW_GATEWAY_TOKEN*Invoke-OpenClawAgentOnboarding.ps1*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
        }

        It 'does not run the gateway token guard when -SkipDocker is supplied' {
            $emptyTokenMap = { @{ 'OPENCLAW_GATEWAY_TOKEN' = '' } }
            Mock Get-InstallEnvFileMap $emptyTokenMap
            Mock Get-InstallEnvFileMap $emptyTokenMap -ModuleName Install.Preflight

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
            $statusUri = [uri]'http://127.0.0.1:4319/v1/status'
            Mock Assert-HostAdapterRespondingPreflight {
                throw "HostAdapter preflight failed before starting Docker. GET $statusUri returned HTTP 503. Confirm OpenClaw.HostAdapter is running, the token is valid, and OpenClaw.MailBridge is running, then retry; or pass -SkipDocker to skip the container stage."
            }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*HostAdapter preflight failed before starting Docker*HTTP 503*OpenClaw.MailBridge*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
            $global:InstallTestCalls -contains 'Wait-ComposeHealthy' | Should -BeFalse
        }

        It 'does not install MSIX when the HostAdapter status probe throws on unreachable endpoint' {
            Mock Assert-HostAdapterRespondingPreflight {
                throw 'HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status was unreachable: Connection refused. Start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage.'
            }

            { & $script:ScriptPath } |
                Should -Throw -ExpectedMessage '*HostAdapter preflight failed before starting Docker*'

            $global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse
        }

        It 'probes the host-loopback HostAdapter status URI before compose up' {
            $global:CapturedHostAdapterPreflightDir = $null
            Mock Assert-HostAdapterRespondingPreflight {
                param($DestDockerDir)
                $global:CapturedHostAdapterPreflightDir = [string]$DestDockerDir
            }

            & $script:ScriptPath | Out-Null

            $global:CapturedHostAdapterPreflightDir | Should -Match 'docker$'
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeTrue
        }

        It 'invokes the HostAdapter responding preflight with the staged docker dir' {
            Mock Assert-HostAdapterRespondingPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterRespondingPreflight') }

            & $script:ScriptPath | Out-Null

            Should -Invoke Assert-HostAdapterRespondingPreflight -Times 1
            $global:InstallTestCalls -contains 'Assert-HostAdapterRespondingPreflight' | Should -BeTrue
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

    Context '-SkipDocker regression for new preflights' {
        It 'does not invoke Assert-HostAdapterRespondingPreflight when -SkipDocker is supplied' {
            Mock Assert-HostAdapterRespondingPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterRespondingPreflight') }
            Mock Assert-HostAdapterBridgeReadyPreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterBridgeReadyPreflight') }
            & $script:ScriptPath -SkipDocker | Out-Null
            Should -Invoke Assert-HostAdapterRespondingPreflight -Times 0
            Should -Invoke Assert-HostAdapterBridgeReadyPreflight -Times 0
        }
    }

    Context 'Stage 8 protocol-activation launch' {
        It 'invokes Invoke-MsixAppActivate with openclaw-mailbridge:firstrun after Invoke-MsixCapture' {
            & $script:ScriptPath | Out-Null

            Should -Invoke Invoke-MsixAppActivate -Times 1 -Exactly -ParameterFilter {
                $ActivationUri -eq 'openclaw-mailbridge:firstrun'
            }

            $idxCapture = $global:InstallTestCalls.IndexOf('Invoke-MsixCapture')
            $idxActivate = $global:InstallTestCalls.IndexOf('Invoke-MsixAppActivate')
            $idxBridge = $global:InstallTestCalls.IndexOf('Assert-HostAdapterBridgeReadyPreflight')
            $idxActivate | Should -BeGreaterThan $idxCapture
            $idxBridge | Should -BeGreaterThan $idxActivate
        }

        It 'skips Invoke-MsixAppActivate when -SkipDocker is supplied' {
            & $script:ScriptPath -SkipDocker | Out-Null
            Should -Invoke Invoke-MsixAppActivate -Times 0 -Exactly
            $global:InstallTestCalls -contains 'Invoke-MsixAppActivate' | Should -BeFalse
        }

        It 'calls Invoke-MsixRemove with the captured PackageFullName when Invoke-MsixAppActivate throws' {
            Mock Invoke-MsixAppActivate {
                [void]$global:InstallTestCalls.Add('Invoke-MsixAppActivate')
                throw 'boom'
            }

            { & $script:ScriptPath } | Should -Throw -ExpectedMessage '*boom*'

            Should -Invoke Invoke-MsixRemove -Times 1 -Exactly -ParameterFilter {
                $PackageFullName -eq 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
            }
            # The no-orphan invariant (issue #52) is preserved: the readiness gate and
            # compose stage never run once activation fails.
            $global:InstallTestCalls -contains 'Assert-HostAdapterBridgeReadyPreflight' | Should -BeFalse
            $global:InstallTestCalls -contains 'Invoke-ComposeUp' | Should -BeFalse
        }

        It 'does not call Invoke-MsixAppActivate before Invoke-MsixCapture' {
            & $script:ScriptPath | Out-Null
            $idxCapture = $global:InstallTestCalls.IndexOf('Invoke-MsixCapture')
            $idxActivate = $global:InstallTestCalls.IndexOf('Invoke-MsixAppActivate')
            $idxCapture | Should -BeGreaterOrEqual 0
            $idxActivate | Should -BeGreaterThan $idxCapture
        }
    }
}
