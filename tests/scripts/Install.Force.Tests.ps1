#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Install.ps1 — Force reinstall scenarios.

.DESCRIPTION
    Covers the -Force parameter paths: reinstall over existing install,
    guard throwing when -Force is absent and a prior install exists,
    tolerance of uninstall-sequence failures, and destination-only cleanup.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share a call log across scopes.'
)]
param()

Describe 'scripts/Install.ps1 — Force reinstall' {

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

    Context '-Force over existing install' {
        It 'runs uninstall sequence before install when -Force and prior install exist' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $true }
                $true
            }
            & $script:ScriptPath -Force | Out-Null
            # Uninstall sequence runs before the install sequence.
            $idxComposeDown = $global:InstallTestCalls.IndexOf('Invoke-ComposeDown')
            $idxMsixRemove = $global:InstallTestCalls.IndexOf('Invoke-MsixRemove')
            $idxCopy = $global:InstallTestCalls.IndexOf('Copy-BundleContents')
            $idxComposeDown | Should -BeGreaterOrEqual 0
            $idxMsixRemove | Should -BeGreaterOrEqual 0
            $idxCopy | Should -BeGreaterThan $idxMsixRemove
        }

        It 'throws when prior install exists and -Force is NOT supplied' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $true }
                $true
            }
            { & $script:ScriptPath } | Should -Throw -ExpectedMessage '*-Force*Uninstall.ps1*'
        }

        It '-Force tolerates compose-down and msix-remove failures in the prior-install uninstall sequence' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $true }
                $true
            }
            Mock Invoke-ComposeDown { [void]$global:InstallTestCalls.Add('Invoke-ComposeDown'); throw 'compose down boom' }
            Mock Invoke-MsixRemove { [void]$global:InstallTestCalls.Add('Invoke-MsixRemove'); throw 'msix remove boom' }
            & $script:ScriptPath -Force *>&1 | Out-Null
            $global:InstallTestCalls -contains 'Copy-BundleContents' | Should -BeTrue
        }

        It '-Force with destination-only (no record file) removes MSIX and destination' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $false }
                $true
            }
            & $script:ScriptPath -Force | Out-Null
            $global:InstallTestCalls -contains 'Invoke-MsixRemove' | Should -BeTrue
            $global:InstallTestCalls -contains 'Remove-Item' | Should -BeTrue
        }
    }
}
