#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Install.Docker.psm1 (issue #142).

.DESCRIPTION
    Covers Invoke-DockerImageLoad: happy-path argument vector, missing-tar throw
    with re-publish remediation, non-zero docker-exit throw, and -WhatIf producing
    zero seam invocations. Mocks only the Invoke-DockerExe seam (never the docker
    executable); no temp files; no global:docker shim.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the module scope; $global: is required to share a capture log across scopes.'
)]
param()

Describe 'Install.Docker.psm1' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Install.Docker.psm1'
        Import-Module $script:ModulePath -Force
    }

    AfterAll {
        Remove-Module Install.Docker -Force -ErrorAction SilentlyContinue
    }

    Context 'Invoke-DockerImageLoad' {
        BeforeEach {
            $global:InstallDockerCapturedVectors = [System.Collections.ArrayList]::new()
            Mock -ModuleName Install.Docker Invoke-DockerExe {
                param([string[]]$DockerArgs)
                [void]$global:InstallDockerCapturedVectors.Add([string[]]$DockerArgs)
                [pscustomobject]@{ ExitCode = 0; Output = @() }
            }
        }
        AfterEach {
            Remove-Variable -Name InstallDockerCapturedVectors -Scope Global -ErrorAction SilentlyContinue
        }

        It 'loads the tar via the exact docker load vector' {
            Mock -ModuleName Install.Docker Test-Path { $true }
            Invoke-DockerImageLoad -ImageTarPath 'C:\fake\docker\openclaw-images.tar'
            $expected = @('load', '-i', 'C:\fake\docker\openclaw-images.tar')
            ($global:InstallDockerCapturedVectors[0] -join '|') | Should -Be ($expected -join '|')
        }

        It 'throws naming the tar path and the re-publish remediation when the tar is missing' {
            Mock -ModuleName Install.Docker Test-Path { $false }
            $err = $null
            try { Invoke-DockerImageLoad -ImageTarPath 'C:\fake\docker\openclaw-images.tar' }
            catch { $err = $_.Exception.Message }
            $err | Should -Not -BeNullOrEmpty
            $err | Should -BeLike '*C:\fake\docker\openclaw-images.tar*'
            $err | Should -BeLike '*Publish.ps1*'
            Should -Invoke -ModuleName Install.Docker Invoke-DockerExe -Times 0
        }

        It 'throws on a non-zero docker load exit' {
            Mock -ModuleName Install.Docker Test-Path { $true }
            Mock -ModuleName Install.Docker Invoke-DockerExe {
                param([string[]]$DockerArgs)
                $null = $DockerArgs
                [pscustomobject]@{ ExitCode = 1; Output = @('load failed') }
            }
            { Invoke-DockerImageLoad -ImageTarPath 'C:\fake\docker\openclaw-images.tar' } | Should -Throw
        }

        It '-WhatIf performs no docker invocation' {
            Mock -ModuleName Install.Docker Test-Path { $true }
            Invoke-DockerImageLoad -ImageTarPath 'C:\fake\docker\openclaw-images.tar' -WhatIf
            Should -Invoke -ModuleName Install.Docker Invoke-DockerExe -Times 0
        }
    }

    Context 'ConvertTo-DockerExeResult' {
        It 'shapes the exit code and coerces output to string[]' {
            InModuleScope Install.Docker {
                $r = ConvertTo-DockerExeResult -ExitCode 3 -RawOutput @('x', 5, 'y')
                $r.ExitCode | Should -Be 3
                $r.ExitCode | Should -BeOfType [int]
                ($r.Output -is [string[]]) | Should -BeTrue
                ($r.Output -join '|') | Should -Be 'x|5|y'
            }
        }
        It 'yields an empty string[] for null output' {
            InModuleScope Install.Docker {
                $r = ConvertTo-DockerExeResult -ExitCode 0 -RawOutput $null
                $r.ExitCode | Should -Be 0
                @($r.Output).Count | Should -Be 0
            }
        }
    }
}
