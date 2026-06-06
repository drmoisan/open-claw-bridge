#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for the Compose-related helpers exported by scripts/Install.Helpers.psm1.

.DESCRIPTION
    Covers the four Compose helpers: Test-DockerAvailable, Invoke-ComposeUp,
    Wait-ComposeHealthy, and Invoke-ComposeDown. Tests every helper using mocks
    and a function shim so no real docker interaction is required. Inter-test
    state for the docker shim is held in $global: variables because function
    shims defined with `function global:docker { ... }` execute in global scope
    and cannot reach script-scoped variables. No temporary files are created.
    This file was extracted verbatim from Install.Helpers.Tests.ps1 to keep the
    parent file under the 500-line cap.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Global docker shim functions run in global scope and must share state with the test via $global: variables.'
)]
param()

Describe 'Install.Helpers.psm1 (Compose helpers)' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        Import-Module $script:ModulePath -Force
    }

    AfterAll {
        Remove-Module Install.Helpers -ErrorAction SilentlyContinue
    }

    Context 'Test-DockerAvailable' {
        BeforeEach {
            # Define a docker shim in the global scope; individual It blocks
            # override it to set the desired exit code.
            function global:docker { $global:LASTEXITCODE = 0 }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
        }

        It 'returns $true when the docker shim sets $LASTEXITCODE = 0' {
            function global:docker { $global:LASTEXITCODE = 0 }
            Test-DockerAvailable | Should -BeTrue
        }

        It 'throws with a remediation message containing -SkipDocker when the shim sets $LASTEXITCODE = 1' {
            function global:docker { $global:LASTEXITCODE = 1 }
            $thrown = { Test-DockerAvailable } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match '-SkipDocker'
        }
    }

    Context 'Invoke-ComposeUp' {
        BeforeEach {
            $global:lastDockerArgs = $null
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 0 }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
            Remove-Variable -Name lastDockerArgs -Scope Global -ErrorAction SilentlyContinue
        }

        It 'docker shim receives compose up -d with explicit flags verbatim' {
            Invoke-ComposeUp -DestDockerDir 'C:\dest\docker' -ComposeFilePath 'C:\dest\docker\docker-compose.yml'
            $expected = @('compose', '--project-name', 'openclaw', '--project-directory', 'C:\dest\docker', '-f', 'C:\dest\docker\docker-compose.yml', 'up', '-d', 'openclaw-core', 'openclaw-agent')
            ($global:lastDockerArgs -join '|') | Should -Be ($expected -join '|')
        }

        It 'throws on non-zero exit' {
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 1 }
            { Invoke-ComposeUp -DestDockerDir 'C:\dest\docker' -ComposeFilePath 'C:\dest\docker\dc.yml' } |
                Should -Throw -ExpectedMessage '*docker compose up failed*'
        }

        It '-WhatIf does not invoke the shim' {
            Invoke-ComposeUp -DestDockerDir 'C:\d' -ComposeFilePath 'C:\d\dc.yml' -WhatIf
            $global:lastDockerArgs | Should -BeNullOrEmpty
        }
    }

    Context 'Wait-ComposeHealthy' {
        BeforeEach {
            $global:dockerCallCount = 0
            $global:dockerResponses = @()
            function global:docker {
                $global:dockerCallCount++
                $global:LASTEXITCODE = 0
                $idx = [math]::Min($global:dockerCallCount - 1, $global:dockerResponses.Count - 1)
                $global:dockerResponses[$idx]
            }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
            Remove-Variable -Name dockerCallCount -Scope Global -ErrorAction SilentlyContinue
            Remove-Variable -Name dockerResponses -Scope Global -ErrorAction SilentlyContinue
        }

        It 'returns when both services report running + healthy on the first poll' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' },
                        [pscustomobject]@{ Service = 'openclaw-agent'; State = 'running'; Health = 'healthy' }
                    ) | ConvertTo-Json -Compress))
            { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 10 -PollIntervalSeconds 1 } | Should -Not -Throw
        }

        It 'throws with failing service name on timeout when a service never reports healthy' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' },
                        [pscustomobject]@{ Service = 'openclaw-agent'; State = 'starting'; Health = 'starting' }
                    ) | ConvertTo-Json -Compress))
            $thrown = { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 2 -PollIntervalSeconds 1 } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'openclaw-agent'
        }

        It 'accepts Health as null/empty when no healthcheck is defined' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = '' },
                        [pscustomobject]@{ Service = 'openclaw-agent'; State = 'running'; Health = $null }
                    ) | ConvertTo-Json -Compress))
            { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 10 -PollIntervalSeconds 1 } | Should -Not -Throw
        }

        It 'exposes default values -TimeoutSeconds 90 and -PollIntervalSeconds 3' {
            $ast = (Get-Command -Module Install.Helpers -Name Wait-ComposeHealthy).ScriptBlock.Ast
            $paramAst = $ast.Body.ParamBlock.Parameters
            ($paramAst | Where-Object { $_.Name.VariablePath.UserPath -eq 'TimeoutSeconds' }).DefaultValue.Extent.Text | Should -Be '90'
            ($paramAst | Where-Object { $_.Name.VariablePath.UserPath -eq 'PollIntervalSeconds' }).DefaultValue.Extent.Text | Should -Be '3'
        }

        It 'parses a stream of one JSON object per line' {
            $core = [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' } | ConvertTo-Json -Compress
            $agent = [pscustomobject]@{ Service = 'openclaw-agent'; State = 'running'; Health = 'healthy' } | ConvertTo-Json -Compress
            $global:dockerResponses = @($core + "`n" + $agent)
            { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 10 -PollIntervalSeconds 1 } | Should -Not -Throw
        }

        It 'treats malformed JSON as transient and retries until timeout' {
            $global:dockerResponses = @('NOT VALID JSON')
            $thrown = { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 2 -PollIntervalSeconds 1 } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'Timed out'
        }

        It 'reports absent-service when JSON omits a required service' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' }
                    ) | ConvertTo-Json -Compress -AsArray))
            $thrown = { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 2 -PollIntervalSeconds 1 } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'openclaw-agent'
        }
    }

    Context 'Invoke-ComposeDown' {
        BeforeEach {
            $global:lastDockerArgs = $null
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 0 }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
            Remove-Variable -Name lastDockerArgs -Scope Global -ErrorAction SilentlyContinue
        }

        It 'docker shim receives compose --project-name <name> -f <file> down verbatim' {
            Invoke-ComposeDown -ComposeFilePath 'C:\x\dc.yml' -ProjectName 'openclaw'
            ($global:lastDockerArgs -join '|') | Should -Be (@('compose', '--project-name', 'openclaw', '-f', 'C:\x\dc.yml', 'down') -join '|')
        }

        It 'throws on non-zero exit' {
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 2 }
            { Invoke-ComposeDown -ComposeFilePath 'C:\x\dc.yml' } | Should -Throw -ExpectedMessage '*docker compose down failed*'
        }

        It '-WhatIf does not invoke the shim' {
            Invoke-ComposeDown -ComposeFilePath 'C:\x\dc.yml' -WhatIf
            $global:lastDockerArgs | Should -BeNullOrEmpty
        }
    }
}
