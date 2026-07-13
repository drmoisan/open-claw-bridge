#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Set-OpenClawWebSearchProvider.ps1.

.DESCRIPTION
    The script provisions a web_search provider entry in the baked agent config seed
    deploy/docker/openclaw-assistant/openclaw.json, referencing the provider API key
    via a SecretRef-style ${...} env interpolation (never a literal key). Tests drive
    the seed with in-memory JSON pseudo-files (Test-Path / Get-Content / Set-Content
    mocks over a global hashtable) so no real filesystem side effects occur. The
    referenced provider-key env var is controlled via the process environment within
    each test and cleaned up afterward.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Pester BeforeEach/It scopes require global state for JSON file mocks.'
)]
param()

Describe 'scripts/Set-OpenClawWebSearchProvider.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Set-OpenClawWebSearchProvider.ps1'
        $global:ProviderTestFiles = @{}

        $script:BaseSeedJson = @'
{
  "gateway": {
    "mode": "local",
    "port": 18789,
    "bind": "auto",
    "auth": {
      "mode": "token",
      "token": "${OPENCLAW_GATEWAY_TOKEN}"
    }
  },
  "session": {
    "dmScope": "per-channel-peer"
  },
  "tools": {
    "profile": "coding"
  },
  "agents": {
    "defaults": {
      "model": {
        "primary": "anthropic/claude-opus-4-6"
      }
    }
  }
}
'@
    }

    AfterAll {
        Remove-Variable -Name ProviderTestFiles -Scope Global -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $global:ProviderTestFiles = @{}
        # The referenced provider-key env var is present for positive paths.
        $env:WEB_SEARCH_API_KEY = 'operator-supplied-firecrawl-key'

        Mock -CommandName Test-Path -MockWith {
            param($LiteralPath, $Path)
            $key = if ($LiteralPath) { [string]$LiteralPath } else { [string]$Path }
            return $global:ProviderTestFiles.ContainsKey($key)
        }

        Mock -CommandName Get-Content -MockWith {
            param($LiteralPath, $Path)
            $key = if ($LiteralPath) { [string]$LiteralPath } else { [string]$Path }
            if (-not $global:ProviderTestFiles.ContainsKey($key)) {
                throw "Test attempted to read unmocked path: $key"
            }
            return $global:ProviderTestFiles[$key]
        }

        Mock -CommandName Set-Content -MockWith {
            param($LiteralPath, $Path, $Value)
            $key = if ($LiteralPath) { [string]$LiteralPath } else { [string]$Path }
            $global:ProviderTestFiles[$key] = ($Value -join "`n")
        }
    }

    AfterEach {
        Remove-Item -Path Env:\WEB_SEARCH_API_KEY -ErrorAction SilentlyContinue
    }

    Context 'provisioning a seed with no provider entry' {
        It 'adds a web_search provider entry whose API key is a SecretRef interpolation, not a literal key' {
            # Arrange
            $cfg = 'C:\seed\openclaw.json'
            $global:ProviderTestFiles[$cfg] = $script:BaseSeedJson

            # Act
            & $script:ScriptPath -ConfigPath $cfg

            # Assert
            $result = $global:ProviderTestFiles[$cfg] | ConvertFrom-Json
            $apiKey = $result.plugins.entries.firecrawl.config.webSearch.apiKey
            $apiKey | Should -Be '${WEB_SEARCH_API_KEY}'
            $apiKey | Should -Not -Be 'operator-supplied-firecrawl-key'
            $apiKey | Should -Match '^\$\{[A-Z_]+\}$'
        }
    }

    Context 'idempotency' {
        It 'yields identical JSON with no duplicate provider entry when re-run on an already-provisioned seed' {
            # Arrange - provision once
            $cfg = 'C:\seed\openclaw.json'
            $global:ProviderTestFiles[$cfg] = $script:BaseSeedJson
            & $script:ScriptPath -ConfigPath $cfg
            $afterFirst = $global:ProviderTestFiles[$cfg]

            # Act - re-run
            & $script:ScriptPath -ConfigPath $cfg
            $afterSecond = $global:ProviderTestFiles[$cfg]

            # Assert - the second run is a no-op (no write) and content is unchanged
            Should -Invoke -CommandName Set-Content -Times 1
            $afterSecond | Should -Be $afterFirst
            $reparsed = $afterSecond | ConvertFrom-Json
            @($reparsed.plugins.entries.firecrawl).Count | Should -Be 1
        }
    }

    Context 'ShouldProcess gating' {
        It 'writes nothing under -WhatIf' {
            # Arrange
            $cfg = 'C:\seed\openclaw.json'
            $global:ProviderTestFiles[$cfg] = $script:BaseSeedJson

            # Act
            & $script:ScriptPath -ConfigPath $cfg -WhatIf

            # Assert
            Should -Invoke -CommandName Set-Content -Times 0
            $global:ProviderTestFiles[$cfg] | Should -Be $script:BaseSeedJson
        }
    }

    Context 'explicit failure paths' {
        It 'throws explicitly on invalid input JSON' {
            # Arrange
            $cfg = 'C:\seed\openclaw.json'
            $global:ProviderTestFiles[$cfg] = 'this is { not valid json'

            # Act / Assert
            { & $script:ScriptPath -ConfigPath $cfg } | Should -Throw -ExpectedMessage '*JSON*'
        }

        It 'throws explicitly when the referenced provider-key env var is missing' {
            # Arrange
            $cfg = 'C:\seed\openclaw.json'
            $global:ProviderTestFiles[$cfg] = $script:BaseSeedJson
            Remove-Item -Path Env:\WEB_SEARCH_API_KEY -ErrorAction SilentlyContinue

            # Act / Assert
            { & $script:ScriptPath -ConfigPath $cfg } | Should -Throw -ExpectedMessage '*WEB_SEARCH_API_KEY*'
        }
    }

    Context 'validation and preservation' {
        It 'produces JSON that round-trips and preserves the pre-existing gateway token and tools profile' {
            # Arrange
            $cfg = 'C:\seed\openclaw.json'
            $global:ProviderTestFiles[$cfg] = $script:BaseSeedJson

            # Act
            & $script:ScriptPath -ConfigPath $cfg

            # Assert - round-trips through ConvertFrom-Json
            $result = $global:ProviderTestFiles[$cfg] | ConvertFrom-Json
            $result.gateway.auth.token | Should -Be '${OPENCLAW_GATEWAY_TOKEN}'
            $result.tools.profile | Should -Be 'coding'
            $result.plugins.entries.firecrawl.config.webSearch.apiKey | Should -Be '${WEB_SEARCH_API_KEY}'
        }
    }
}
