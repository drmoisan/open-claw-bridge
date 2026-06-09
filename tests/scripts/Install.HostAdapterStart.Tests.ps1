#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 unit tests for Invoke-HostAdapterStart, Test-TcpPortOpen, and
    Invoke-HostAdapterProcess defined in scripts/Install.ps1.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share call state across scopes.'
)]
param()

Describe 'Install.ps1 — Invoke-HostAdapterStart' {

    BeforeAll {
        $script:InstallScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Install.ps1'
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        Import-Module $script:HelpersPath -Force

        # Dot-source Install.ps1 to bring its functions into scope without
        # running the main block (guarded by InvocationName -ne '.').
        . $script:InstallScriptPath
    }

    Context 'exe not found' {
        It 'throws with message containing HostAdapter executable not found at' {
            Mock Test-Path { return $false } -ParameterFilter { $LiteralPath -like '*OpenClaw.HostAdapter.exe' }

            {
                Invoke-HostAdapterStart `
                    -HostAdapterExePath 'C:\missing\OpenClaw.HostAdapter.exe' `
                    -AspNetCoreUrls 'http://127.0.0.1:4319'
            } | Should -Throw -ExpectedMessage '*HostAdapter executable not found at*'
        }
    }

    Context 'already running' {
        It 'does NOT call Invoke-HostAdapterProcess when port is already listening' {
            Mock Test-Path { return $true } -ParameterFilter { $LiteralPath -like '*OpenClaw.HostAdapter.exe' }
            Mock Test-TcpPortOpen { return $true }
            # Simulate the stale-detection seams returning a matching PID and path so the
            # legitimate-already-running branch is taken rather than the stale-detection throw.
            Mock Get-ListeningProcessId { return 9999 }
            Mock Get-ProcessMainModulePath { return 'C:\fake\OpenClaw.HostAdapter.exe' }
            Mock Invoke-HostAdapterProcess { }

            Invoke-HostAdapterStart `
                -HostAdapterExePath 'C:\fake\OpenClaw.HostAdapter.exe' `
                -AspNetCoreUrls 'http://127.0.0.1:4319'

            Should -Invoke -CommandName Invoke-HostAdapterProcess -Times 0 -Exactly
        }
    }

    Context 'not running — launches process' {
        It 'calls Invoke-HostAdapterProcess exactly once when port is not listening' {
            Mock Test-Path { return $true } -ParameterFilter { $LiteralPath -like '*OpenClaw.HostAdapter.exe' }
            Mock Test-TcpPortOpen { return $false }
            Mock Invoke-HostAdapterProcess { }

            Invoke-HostAdapterStart `
                -HostAdapterExePath 'C:\fake\OpenClaw.HostAdapter.exe' `
                -AspNetCoreUrls 'http://127.0.0.1:4319'

            Should -Invoke -CommandName Invoke-HostAdapterProcess -Times 1 -Exactly
        }
    }
}
