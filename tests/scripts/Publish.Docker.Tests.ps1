#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Publish.Docker.psm1 (issue #142).

.DESCRIPTION
    Covers the pure compose transform (positive, real-file drift guard, and
    negative paths), the exact docker build/save argument vectors and
    non-zero-exit throws via the mocked Invoke-DockerExe seam, agent base-image
    resolution, and the Invoke-PublishDockerStage facade ordering plus -WhatIf.
    Mocks only the Invoke-DockerExe seam (never the docker executable); no temp
    files; no global:docker shim.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the module scope; $global: is required to share a capture log across scopes.'
)]
param()

Describe 'Publish.Docker.psm1' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Docker.psm1'
        Import-Module $script:ModulePath -Force
        $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

        $script:ComposeFixture = @(
            'name: openclaw'
            ''
            'services:'
            '  openclaw-core:'
            '    build:'
            '      context: .'
            '      dockerfile: deploy/docker/openclaw-core.Dockerfile'
            '      target: runtime'
            '      args:'
            '        BUILD_CONFIGURATION: Release'
            '    image: openclaw/core:pre-mvp'
            '    container_name: openclaw-core'
            '    restart: unless-stopped'
            ''
            '  openclaw-agent:'
            '    build:'
            '      context: .'
            '      dockerfile: deploy/docker/openclaw-agent.Dockerfile'
            '      args:'
            '        OPENCLAW_AGENT_IMAGE: ${OPENCLAW_AGENT_IMAGE}'
            '    image: openclaw/agent:pre-mvp'
            '    container_name: openclaw-agent'
            '    restart: unless-stopped'
            ''
            'volumes:'
            '  openclaw_data:'
        )
    }

    AfterAll {
        Remove-Module Publish.Docker -Force -ErrorAction SilentlyContinue
    }

    Context 'Convert-ComposeToBundleCompose' {
        It 'removes both build blocks, rewrites images, inserts pull_policy, preserves other lines' {
            $expected = @(
                'name: openclaw'
                ''
                'services:'
                '  openclaw-core:'
                '    image: openclaw/core:1.2.3.0'
                '    pull_policy: never'
                '    container_name: openclaw-core'
                '    restart: unless-stopped'
                ''
                '  openclaw-agent:'
                '    image: openclaw/agent:1.2.3.0'
                '    pull_policy: never'
                '    container_name: openclaw-agent'
                '    restart: unless-stopped'
                ''
                'volumes:'
                '  openclaw_data:'
            )

            $actual = Convert-ComposeToBundleCompose -ComposeContent $script:ComposeFixture -Version '1.2.3.0'

            $actual.Count | Should -Be $expected.Count
            for ($i = 0; $i -lt $expected.Count; $i++) {
                $actual[$i] | Should -BeExactly $expected[$i]
            }
            (@($actual | Where-Object { $_ -match '^\s*build:' })).Count | Should -Be 0
            (@($actual | Where-Object { $_ -match '^\s*pull_policy: never$' })).Count | Should -Be 2
        }
    }

    Context 'Convert-ComposeToBundleCompose over the real tracked compose (drift guard)' {
        It 'transforms the tracked docker-compose.yml without throwing' {
            $trackedLines = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\docker-compose.yml')
            { Convert-ComposeToBundleCompose -ComposeContent $trackedLines -Version '1.2.3.0' } |
                Should -Not -Throw
            $out = Convert-ComposeToBundleCompose -ComposeContent $trackedLines -Version '1.2.3.0'

            (@($out | Where-Object { $_ -match '^\s*build:' })).Count | Should -Be 0
            (@($out | Where-Object { $_ -match '^\s*image: openclaw/core:1\.2\.3\.0$' })).Count | Should -Be 1
            (@($out | Where-Object { $_ -match '^\s*image: openclaw/agent:1\.2\.3\.0$' })).Count | Should -Be 1
            (@($out | Where-Object { $_ -match '^\s*pull_policy: never$' })).Count | Should -Be 2
        }
    }

    Context 'Convert-ComposeToBundleCompose drift failures' {
        It 'throws naming openclaw-core when the core build key is absent' {
            $fixture = @(
                'services:'
                '  openclaw-core:'
                '    image: openclaw/core:pre-mvp'
                '    container_name: openclaw-core'
                '  openclaw-agent:'
                '    build:'
                '      context: .'
                '    image: openclaw/agent:pre-mvp'
            )
            { Convert-ComposeToBundleCompose -ComposeContent $fixture -Version '1.2.3.0' } |
                Should -Throw -ExpectedMessage '*openclaw-core*'
        }

        It 'throws naming openclaw-agent when the agent image key is absent' {
            $fixture = @(
                'services:'
                '  openclaw-core:'
                '    build:'
                '      context: .'
                '    image: openclaw/core:pre-mvp'
                '  openclaw-agent:'
                '    build:'
                '      context: .'
                '    container_name: openclaw-agent'
            )
            { Convert-ComposeToBundleCompose -ComposeContent $fixture -Version '1.2.3.0' } |
                Should -Throw -ExpectedMessage '*openclaw-agent*'
        }
    }

    Context 'docker argument vectors' {
        BeforeEach {
            $global:PublishDockerCapturedVectors = [System.Collections.ArrayList]::new()
            Mock -ModuleName Publish.Docker Invoke-DockerExe {
                param([string[]]$DockerArgs)
                [void]$global:PublishDockerCapturedVectors.Add([string[]]$DockerArgs)
                [pscustomobject]@{ ExitCode = 0; Output = @() }
            }
        }
        AfterEach {
            Remove-Variable -Name PublishDockerCapturedVectors -Scope Global -ErrorAction SilentlyContinue
        }

        It 'composes the exact core build vector' {
            Build-OpenClawDockerImage -RepoRoot 'C:\repo' -Version '1.2.3.0' -Kind 'core' -Configuration 'Release'
            $expected = @(
                'build'
                '-f', (Join-Path 'C:\repo' 'deploy/docker/openclaw-core.Dockerfile')
                '--target', 'runtime'
                '--build-arg', 'BUILD_CONFIGURATION=Release'
                '-t', 'openclaw/core:1.2.3.0'
                '-t', 'openclaw/core:pre-mvp'
                'C:\repo'
            )
            ($global:PublishDockerCapturedVectors[0] -join '|') | Should -Be ($expected -join '|')
        }

        It 'composes the exact agent build vector with a resolved base image' {
            Build-OpenClawDockerImage -RepoRoot 'C:\repo' -Version '1.2.3.0' -Kind 'agent' -Configuration 'Release' -AgentBaseImage 'ghcr.io/openclaw/openclaw:latest'
            $expected = @(
                'build'
                '-f', (Join-Path 'C:\repo' 'deploy/docker/openclaw-agent.Dockerfile')
                '--build-arg', 'OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest'
                '-t', 'openclaw/agent:1.2.3.0'
                '-t', 'openclaw/agent:pre-mvp'
                'C:\repo'
            )
            ($global:PublishDockerCapturedVectors[0] -join '|') | Should -Be ($expected -join '|')
        }

        It 'composes the exact save vector naming all four refs in order' {
            Save-OpenClawDockerImage -Version '1.2.3.0' -OutputTarPath 'C:\bundle\docker\openclaw-images.tar'
            $expected = @(
                'save'
                '-o', 'C:\bundle\docker\openclaw-images.tar'
                'openclaw/core:1.2.3.0'
                'openclaw/core:pre-mvp'
                'openclaw/agent:1.2.3.0'
                'openclaw/agent:pre-mvp'
            )
            ($global:PublishDockerCapturedVectors[0] -join '|') | Should -Be ($expected -join '|')
        }
    }

    Context 'docker seam non-zero exit throws' {
        BeforeEach {
            Mock -ModuleName Publish.Docker Invoke-DockerExe {
                param([string[]]$DockerArgs)
                $null = $DockerArgs
                [pscustomobject]@{ ExitCode = 1; Output = @('boom') }
            }
        }

        It 'Build-OpenClawDockerImage throws on non-zero exit' {
            { Build-OpenClawDockerImage -RepoRoot 'C:\repo' -Version '1.2.3.0' -Kind 'core' -Configuration 'Release' } |
                Should -Throw
        }

        It 'Save-OpenClawDockerImage throws on non-zero exit' {
            { Save-OpenClawDockerImage -Version '1.2.3.0' -OutputTarPath 'C:\bundle\docker\openclaw-images.tar' } |
                Should -Throw
        }
    }

    Context 'Resolve-OpenClawAgentBaseImage' {
        It 'returns the map value when OPENCLAW_AGENT_IMAGE is present' {
            Resolve-OpenClawAgentBaseImage -EnvMap @{ OPENCLAW_AGENT_IMAGE = 'ghcr.io/custom/agent:9.9' } |
                Should -Be 'ghcr.io/custom/agent:9.9'
        }
        It 'returns the Dockerfile ARG default when the key is absent' {
            Resolve-OpenClawAgentBaseImage -EnvMap @{} |
                Should -Be 'ghcr.io/openclaw/openclaw:latest'
        }
        It 'returns the default when the value is blank/whitespace' {
            Resolve-OpenClawAgentBaseImage -EnvMap @{ OPENCLAW_AGENT_IMAGE = '   ' } |
                Should -Be 'ghcr.io/openclaw/openclaw:latest'
        }
    }

    Context 'Invoke-PublishDockerStage' {
        BeforeEach {
            $global:PublishDockerCalls = [System.Collections.ArrayList]::new()
            Mock -ModuleName Publish.Docker Invoke-DockerExe {
                param([string[]]$DockerArgs)
                [void]$global:PublishDockerCalls.Add([string]$DockerArgs[0])
                [pscustomobject]@{ ExitCode = 0; Output = @() }
            }
            Mock -ModuleName Publish.Docker Set-Content { [void]$global:PublishDockerCalls.Add('set-content') }
        }
        AfterEach {
            Remove-Variable -Name PublishDockerCalls -Scope Global -ErrorAction SilentlyContinue
        }

        It 'runs core-build, agent-build, save, then writes the bundle compose' {
            Invoke-PublishDockerStage -RepoRoot $script:RepoRoot -BundleDockerDir 'C:\bundle\docker' -Version '1.2.3.0' -Configuration 'Release' -EnvMap @{}

            $seamCalls = @($global:PublishDockerCalls | Where-Object { $_ -in @('build', 'save') })
            ($seamCalls -join ',') | Should -Be 'build,build,save'
            $global:PublishDockerCalls -contains 'set-content' | Should -BeTrue
        }

        It '-WhatIf performs no docker invocations and no compose write' {
            Invoke-PublishDockerStage -RepoRoot $script:RepoRoot -BundleDockerDir 'C:\bundle\docker' -Version '1.2.3.0' -Configuration 'Release' -EnvMap @{} -WhatIf

            Should -Invoke -ModuleName Publish.Docker Invoke-DockerExe -Times 0
            Should -Invoke -ModuleName Publish.Docker Set-Content -Times 0
        }
    }

    Context 'ConvertTo-DockerExeResult' {
        It 'shapes the exit code and coerces output to string[]' {
            InModuleScope Publish.Docker {
                $r = ConvertTo-DockerExeResult -ExitCode 2 -RawOutput @('a', 7, 'b')
                $r.ExitCode | Should -Be 2
                $r.ExitCode | Should -BeOfType [int]
                ($r.Output -is [string[]]) | Should -BeTrue
                ($r.Output -join '|') | Should -Be 'a|7|b'
            }
        }
        It 'yields an empty string[] for null output' {
            InModuleScope Publish.Docker {
                $r = ConvertTo-DockerExeResult -ExitCode 0 -RawOutput $null
                $r.ExitCode | Should -Be 0
                @($r.Output).Count | Should -Be 0
            }
        }
    }
}
