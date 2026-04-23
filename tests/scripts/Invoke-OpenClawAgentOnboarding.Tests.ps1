#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Invoke-OpenClawAgentOnboarding.ps1.

.DESCRIPTION
    The script generates a cryptographically strong OPENCLAW_GATEWAY_TOKEN
    and writes it to the target `.env`. Tests exercise the generation and
    persistence behavior using in-memory pseudo-files so no real filesystem
    side effects occur.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Pester BeforeEach/It scopes require global state for file mocks.'
)]
param()

Describe 'scripts/Invoke-OpenClawAgentOnboarding.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Invoke-OpenClawAgentOnboarding.ps1'
        $global:OnboardTestFiles = @{}
    }

    AfterAll {
        Remove-Variable -Name OnboardTestFiles -Scope Global -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $global:OnboardTestFiles = @{}

        Mock -CommandName Test-Path -MockWith {
            param($LiteralPath)
            return $global:OnboardTestFiles.ContainsKey([string]$LiteralPath)
        }

        Mock -CommandName Get-Content -MockWith {
            param($LiteralPath)
            $key = [string]$LiteralPath
            if (-not $global:OnboardTestFiles.ContainsKey($key)) {
                throw "Test attempted to read unmocked path: $key"
            }
            return $global:OnboardTestFiles[$key]
        }

        Mock -CommandName Set-Content -MockWith {
            param($LiteralPath, $Value)
            $global:OnboardTestFiles[[string]$LiteralPath] = @($Value)
        }
    }

    Context 'token generation' {
        It 'writes a non-empty OPENCLAW_GATEWAY_TOKEN to a fresh .env' {
            & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            $written = $global:OnboardTestFiles['C:\test\.env']
            $written | Should -Not -BeNullOrEmpty
            $tokenLine = $written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $tokenLine | Should -Not -BeNullOrEmpty
            $tokenValue = ($tokenLine -split '=', 2)[1]
            $tokenValue.Length | Should -BeGreaterOrEqual 32
        }

        It 'emits only base64url-safe characters (no +, /, =)' {
            & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            $written = $global:OnboardTestFiles['C:\test\.env']
            $tokenLine = $written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $tokenValue = ($tokenLine -split '=', 2)[1]
            $tokenValue | Should -Not -Match '[+/=]'
            $tokenValue | Should -Match '^[A-Za-z0-9_-]+$'
        }

        It 'produces a distinct token on each invocation (uses system RNG)' {
            $global:OnboardTestFiles = @{}
            & $script:ScriptPath -EnvFilePath 'C:\a\.env'
            & $script:ScriptPath -EnvFilePath 'C:\b\.env'

            $aLine = $global:OnboardTestFiles['C:\a\.env'] | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $bLine = $global:OnboardTestFiles['C:\b\.env'] | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $aValue = ($aLine -split '=', 2)[1]
            $bValue = ($bLine -split '=', 2)[1]
            $aValue | Should -Not -Be $bValue
        }
    }

    Context 'idempotency' {
        It 'does not rewrite when OPENCLAW_GATEWAY_TOKEN already has a non-empty value and -Force is absent' {
            $global:OnboardTestFiles['C:\test\.env'] = @('OPENCLAW_GATEWAY_TOKEN=pre-existing-token', 'OTHER=keep')
            & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            $written = $global:OnboardTestFiles['C:\test\.env']
            $tokenLine = $written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $tokenLine | Should -Be 'OPENCLAW_GATEWAY_TOKEN=pre-existing-token'
            Should -Invoke -CommandName Set-Content -Times 0
        }

        It 'overwrites an existing non-empty token when -Force is supplied' {
            $global:OnboardTestFiles['C:\test\.env'] = @('OPENCLAW_GATEWAY_TOKEN=pre-existing-token', 'OTHER=keep')
            & $script:ScriptPath -EnvFilePath 'C:\test\.env' -Force

            $written = $global:OnboardTestFiles['C:\test\.env']
            $tokenLine = $written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $tokenLine | Should -Not -Be 'OPENCLAW_GATEWAY_TOKEN=pre-existing-token'
            $tokenLine | Should -Match '^OPENCLAW_GATEWAY_TOKEN=.+'
        }

        It 'writes a token when the existing value is empty, without requiring -Force' {
            $global:OnboardTestFiles['C:\test\.env'] = @('OPENCLAW_GATEWAY_TOKEN=', 'OTHER=keep')
            & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            $written = $global:OnboardTestFiles['C:\test\.env']
            $tokenLine = $written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $tokenLine | Should -Match '^OPENCLAW_GATEWAY_TOKEN=.+'
        }
    }

    Context 'preservation of existing content' {
        It 'preserves other key-value pairs and comments when overwriting' {
            $global:OnboardTestFiles['C:\test\.env'] = @(
                '# operator config',
                'OPENCLAW_AGENT_PORT=18789',
                'OPENCLAW_GATEWAY_TOKEN=',
                'ANTHROPIC_API_KEY='
            )
            & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            $written = $global:OnboardTestFiles['C:\test\.env']
            $written | Should -Contain '# operator config'
            $written | Should -Contain 'OPENCLAW_AGENT_PORT=18789'
            $written | Should -Contain 'ANTHROPIC_API_KEY='
            ($written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }).Count | Should -Be 1
        }

        It 'appends the token when the key is absent from the file' {
            $global:OnboardTestFiles['C:\test\.env'] = @('OPENCLAW_AGENT_PORT=18789')
            & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            $written = $global:OnboardTestFiles['C:\test\.env']
            $written | Should -Contain 'OPENCLAW_AGENT_PORT=18789'
            $tokenLine = $written | Where-Object { $_ -match '^OPENCLAW_GATEWAY_TOKEN=' }
            $tokenLine | Should -Match '^OPENCLAW_GATEWAY_TOKEN=.+'
        }
    }

    Context 'parameter validation' {
        It 'rejects a TokenByteLength below the validation range' {
            { & $script:ScriptPath -EnvFilePath 'C:\test\.env' -TokenByteLength 16 } |
                Should -Throw
        }

        It 'rejects a TokenByteLength above the validation range' {
            { & $script:ScriptPath -EnvFilePath 'C:\test\.env' -TokenByteLength 512 } |
                Should -Throw
        }
    }
}
