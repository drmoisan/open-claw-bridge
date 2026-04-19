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
        Mock Remove-Item { [void]$global:InstallTestCalls.Add('Remove-Item') }
        Mock Test-Path {
            param($LiteralPath)
            if ($LiteralPath -like '*install-record.json') { return $false }
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
        Remove-Item -Path 'Function:\global:Test-IsElevatedAdmin' -ErrorAction SilentlyContinue
    }

    Context 'parameter binding' {
        It 'accepts the refinement parameter set with defaults' {
            { & $script:ScriptPath -WhatIf } | Should -Not -Throw
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
                'Invoke-MsixInstall',
                'Invoke-MsixCapture',
                'Invoke-ComposeUp',
                'Wait-ComposeHealthy',
                'Write-InstallRecord'
            )
            ($helperOrder -join ',') | Should -Be ($expected -join ',')
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

        It '-Force with destination-only (no record file) removes the destination' {
            Mock Test-Path {
                param($LiteralPath)
                if ($LiteralPath -like '*install-record.json') { return $false }
                $true
            }
            & $script:ScriptPath -Force | Out-Null
            $global:InstallTestCalls -contains 'Remove-Item' | Should -BeTrue
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
