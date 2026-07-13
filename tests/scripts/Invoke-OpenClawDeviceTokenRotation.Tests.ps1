#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Invoke-OpenClawDeviceTokenRotation.ps1.

.DESCRIPTION
    The script rotates the HostAdapter device (bearer) token by writing a new
    cryptographically-generated base64url secret to the host token file and then
    restarting the consuming containers (openclaw-core, openclaw-agent) through the
    Invoke-OpenClawDockerCommand wrapper seam. Tests drive the host token file with
    in-memory pseudo-files (Test-Path / Get-Content / Set-Content mocks over a global
    hashtable) and mock the docker wrapper seam (never docker directly). No real
    filesystem, process, or network side effects occur. The device-token value is
    never written to any log/verbose/debug stream.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Pester BeforeEach/It scopes require global state for file and docker-call mocks.'
)]
param()

Describe 'scripts/Invoke-OpenClawDeviceTokenRotation.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Invoke-OpenClawDeviceTokenRotation.ps1'
        $moduleManifest = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
        Import-Module -Name $moduleManifest -Force -Global -ErrorAction Stop
        $global:RotationTestFiles = @{}
        $global:RotationDockerCalls = [System.Collections.Generic.List[object]]::new()
        $global:RotationCallOrder = [System.Collections.Generic.List[string]]::new()
    }

    AfterAll {
        Remove-Variable -Name RotationTestFiles -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name RotationDockerCalls -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name RotationCallOrder -Scope Global -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $global:RotationTestFiles = @{}
        $global:RotationDockerCalls = [System.Collections.Generic.List[object]]::new()
        $global:RotationCallOrder = [System.Collections.Generic.List[string]]::new()

        # Host token file operations happen in the script's own scope.
        Mock -CommandName Test-Path -MockWith {
            param($LiteralPath)
            return $global:RotationTestFiles.ContainsKey([string]$LiteralPath)
        }

        Mock -CommandName Get-Content -MockWith {
            param($LiteralPath)
            $key = [string]$LiteralPath
            if (-not $global:RotationTestFiles.ContainsKey($key)) {
                throw "Test attempted to read unmocked path: $key"
            }
            return $global:RotationTestFiles[$key]
        }

        Mock -CommandName Set-Content -MockWith {
            param($LiteralPath, $Value)
            $global:RotationTestFiles[[string]$LiteralPath] = @($Value)
            $global:RotationCallOrder.Add('write')
        }

        # Docker restarts go through the wrapper seam. Mock the seam, never docker.
        Mock -CommandName Invoke-OpenClawDockerCommand -MockWith {
            param([string]$ExecutablePath, [string[]]$CommandArguments)
            $global:RotationDockerCalls.Add([pscustomobject]@{
                    ExecutablePath   = $ExecutablePath
                    CommandArguments = $CommandArguments
                })
            $global:RotationCallOrder.Add('restart:' + ($CommandArguments -join ' '))
            return [pscustomobject]@{ Succeeded = $true; ExitCode = 0; Output = @(); ErrorMessage = $null }
        }
    }

    Context 'rotation happy path (write-first ordering + consumer restarts)' {
        It 'writes a new non-empty base64url secret to the host token file before any docker restart (with -Force on an existing file)' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')

            # Act
            & $script:ScriptPath -TokenFilePath $tokenFile -Force

            # Assert - secret written and non-empty
            $written = ($global:RotationTestFiles[$tokenFile] -join '').Trim()
            $written | Should -Not -BeNullOrEmpty
            $written | Should -Not -Be 'old-token-value'
            # Assert - write occurred strictly before the first restart
            $writeIndex = $global:RotationCallOrder.IndexOf('write')
            $writeIndex | Should -BeGreaterOrEqual 0
            $restartOrderIndex = [array]::FindIndex([string[]]$global:RotationCallOrder, [Predicate[string]] { param($s) $s -like 'restart:*' })
            $writeIndex | Should -BeLessThan $restartOrderIndex
        }

        It 'restarts openclaw-core and openclaw-agent via Invoke-OpenClawDockerCommand with restart arguments' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')

            # Act
            & $script:ScriptPath -TokenFilePath $tokenFile -Force

            # Assert
            $argLists = $global:RotationDockerCalls | ForEach-Object { , $_.CommandArguments }
            $argLists.Count | Should -Be 2
            ($argLists[0] -join ' ') | Should -Be 'restart openclaw-core'
            ($argLists[1] -join ' ') | Should -Be 'restart openclaw-agent'
        }
    }

    Context 'ShouldProcess gating' {
        It 'performs no file write and no restart under -WhatIf' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')

            # Act
            & $script:ScriptPath -TokenFilePath $tokenFile -Force -WhatIf

            # Assert
            Should -Invoke -CommandName Set-Content -Times 0
            Should -Invoke -CommandName Invoke-OpenClawDockerCommand -Times 0
            ($global:RotationTestFiles[$tokenFile] -join '') | Should -Be 'old-token-value'
        }
    }

    Context 'idempotency' {
        It 'does not rotate an already-valid token file and performs no restart when -Force is absent' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('existing-valid-token')

            # Act
            & $script:ScriptPath -TokenFilePath $tokenFile

            # Assert
            Should -Invoke -CommandName Set-Content -Times 0
            Should -Invoke -CommandName Invoke-OpenClawDockerCommand -Times 0
            ($global:RotationTestFiles[$tokenFile] -join '') | Should -Be 'existing-valid-token'
        }
    }

    Context 'explicit failure paths' {
        It 'throws explicitly when the token file write fails (unwritable file)' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')
            Mock -CommandName Set-Content -MockWith { throw 'Access to the path is denied.' }

            # Act / Assert
            { & $script:ScriptPath -TokenFilePath $tokenFile -Force } |
                Should -Throw -ExpectedMessage '*device token*'
        }

        It 'throws explicitly when a docker restart fails (Succeeded = $false)' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')
            Mock -CommandName Invoke-OpenClawDockerCommand -MockWith {
                param([string]$ExecutablePath, [string[]]$CommandArguments)
                $null = @($ExecutablePath, $CommandArguments)
                return [pscustomobject]@{ Succeeded = $false; ExitCode = 1; Output = @('boom'); ErrorMessage = 'restart failed' }
            }

            # Act / Assert
            { & $script:ScriptPath -TokenFilePath $tokenFile -Force } |
                Should -Throw -ExpectedMessage '*restart*'
        }

        It 'throws an error directing to the runbook and creates no placeholder when the host token file is absent' {
            # Arrange - token file not present in the pseudo-filesystem
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'

            # Act
            $threw = $false
            $message = ''
            try { & $script:ScriptPath -TokenFilePath $tokenFile -Force }
            catch { $threw = $true; $message = $_.Exception.Message }

            # Assert
            $threw | Should -BeTrue
            $message | Should -BeLike '*runbook*'
            Should -Invoke -CommandName Set-Content -Times 0
            $global:RotationTestFiles.ContainsKey($tokenFile) | Should -BeFalse
        }
    }

    Context 'token-file resolution from .env' {
        It 'resolves the host token file from HOSTADAPTER_TOKEN_FILE in the .env when -TokenFilePath is not supplied' {
            # Arrange - .env parsing happens inside the module scope
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')
            Mock -ModuleName OpenClawContainerValidation -CommandName Test-Path -MockWith { return $true } -ParameterFilter { $LiteralPath -match '\.env$' }
            Mock -ModuleName OpenClawContainerValidation -CommandName Get-Content -MockWith {
                return @("HOSTADAPTER_TOKEN_FILE=$tokenFile")
            } -ParameterFilter { $LiteralPath -match '\.env$' }

            # Act
            & $script:ScriptPath -EnvFilePath 'C:\test\.env' -Force

            # Assert - the resolved token file was rotated and both consumers restarted
            ($global:RotationTestFiles[$tokenFile] -join '').Trim() | Should -Not -Be 'old-token-value'
            $global:RotationDockerCalls.Count | Should -Be 2
        }

        It 'throws when HOSTADAPTER_TOKEN_FILE is absent from the .env and no -TokenFilePath is supplied' {
            # Arrange - module-scope .env parse returns no HOSTADAPTER_TOKEN_FILE
            Mock -ModuleName OpenClawContainerValidation -CommandName Test-Path -MockWith { return $true } -ParameterFilter { $LiteralPath -match '\.env$' }
            Mock -ModuleName OpenClawContainerValidation -CommandName Get-Content -MockWith {
                return @('OPENCLAW_AGENT_PORT=18789')
            } -ParameterFilter { $LiteralPath -match '\.env$' }

            # Act / Assert
            { & $script:ScriptPath -EnvFilePath 'C:\test\.env' -Force } |
                Should -Throw -ExpectedMessage '*HOSTADAPTER_TOKEN_FILE*'
        }
    }

    Context 'secret hygiene and shape' {
        It 'never writes the device-token value to the verbose/debug/warning/information streams' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')

            # Act
            $VerbosePreference = 'Continue'
            $DebugPreference = 'Continue'
            $InformationPreference = 'Continue'
            $all = & { & $script:ScriptPath -TokenFilePath $tokenFile -Force } *>&1
            $secret = ($global:RotationTestFiles[$tokenFile] -join '').Trim()
            $logRecords = $all | Where-Object {
                $_ -is [System.Management.Automation.VerboseRecord] -or
                $_ -is [System.Management.Automation.DebugRecord] -or
                $_ -is [System.Management.Automation.WarningRecord] -or
                $_ -is [System.Management.Automation.InformationRecord] -or
                $_ -is [System.Management.Automation.ErrorRecord]
            }

            # Assert
            $secret | Should -Not -BeNullOrEmpty
            ($logRecords | Out-String) | Should -Not -Match ([regex]::Escape($secret))
        }

        It 'generates a secret that matches the base64url charset (shape only, not exact value)' {
            # Arrange
            $tokenFile = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
            $global:RotationTestFiles[$tokenFile] = @('old-token-value')

            # Act
            & $script:ScriptPath -TokenFilePath $tokenFile -Force

            # Assert
            $secret = ($global:RotationTestFiles[$tokenFile] -join '').Trim()
            $secret | Should -Match '^[A-Za-z0-9_-]+$'
            $secret | Should -Not -Match '[+/=]'
        }
    }
}
