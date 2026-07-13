#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Get-OpenClawControlUiTokenUrl.ps1.

.DESCRIPTION
    The script composes the OpenClaw Control UI authentication URL of the shape
    http://127.0.0.1:<OPENCLAW_AGENT_PORT>/#token=<OPENCLAW_GATEWAY_TOKEN> from a
    target `.env`, reusing the Get-OpenClawEnvFileMap parser seam in
    OpenClawContainerValidation.psm1. Tests drive the parser with in-memory
    pseudo-files (module-scoped Test-Path / Get-Content mocks over a global
    hashtable) so no real filesystem side effects occur. The gateway token value
    is never written to any log/verbose/debug/warning/information stream.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Pester BeforeEach/It scopes require global state for file mocks.'
)]
param()

Describe 'scripts/Get-OpenClawControlUiTokenUrl.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Get-OpenClawControlUiTokenUrl.ps1'
        $moduleManifest = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
        Import-Module -Name $moduleManifest -Force -Global -ErrorAction Stop
        $global:DeliveryTestFiles = @{}
    }

    AfterAll {
        Remove-Variable -Name DeliveryTestFiles -Scope Global -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $global:DeliveryTestFiles = @{}

        # The .env parser (Get-OpenClawEnvFileMap) runs inside the module scope,
        # so the file-I/O mocks must be injected into that module scope.
        Mock -ModuleName OpenClawContainerValidation -CommandName Test-Path -MockWith {
            param($LiteralPath)
            return $global:DeliveryTestFiles.ContainsKey([string]$LiteralPath)
        }

        Mock -ModuleName OpenClawContainerValidation -CommandName Get-Content -MockWith {
            param($LiteralPath)
            $key = [string]$LiteralPath
            if (-not $global:DeliveryTestFiles.ContainsKey($key)) {
                throw "Test attempted to read unmocked path: $key"
            }
            return $global:DeliveryTestFiles[$key]
        }
    }

    Context 'URL composition' {
        It 'returns http://127.0.0.1:18789/#token=<token> for a valid token and default port' {
            # Arrange
            $global:DeliveryTestFiles['C:\test\.env'] = @('OPENCLAW_GATEWAY_TOKEN=abc-DEF_123')

            # Act
            $url = & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            # Assert
            $url | Should -Be 'http://127.0.0.1:18789/#token=abc-DEF_123'
        }

        It 'uses an explicit OPENCLAW_AGENT_PORT when present in the .env' {
            # Arrange
            $global:DeliveryTestFiles['C:\test\.env'] = @(
                'OPENCLAW_AGENT_PORT=28080',
                'OPENCLAW_GATEWAY_TOKEN=abc-DEF_123'
            )

            # Act
            $url = & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            # Assert
            $url | Should -Be 'http://127.0.0.1:28080/#token=abc-DEF_123'
        }

        It 'places the base64url token in the fragment verbatim (no re-encoding or mutation)' {
            # Arrange
            $token = 'Ab1-cd2_Ef3-gh4_Ij5'
            $global:DeliveryTestFiles['C:\test\.env'] = @("OPENCLAW_GATEWAY_TOKEN=$token")

            # Act
            $url = & $script:ScriptPath -EnvFilePath 'C:\test\.env'

            # Assert
            $fragment = ($url -split '#', 2)[1]
            $fragment | Should -Be "token=$token"
            $url | Should -Match ([regex]::Escape($token))
        }
    }

    Context 'missing/empty token guard' {
        It 'throws an error naming Invoke-OpenClawAgentOnboarding.ps1 when the token key is absent, and emits no URL' {
            # Arrange
            $global:DeliveryTestFiles['C:\test\.env'] = @('OPENCLAW_AGENT_PORT=18789')

            # Act
            $captured = $null
            $threw = $false
            $message = ''
            try { $captured = & $script:ScriptPath -EnvFilePath 'C:\test\.env' }
            catch { $threw = $true; $message = $_.Exception.Message }

            # Assert
            $threw | Should -BeTrue
            $message | Should -BeLike '*Invoke-OpenClawAgentOnboarding.ps1*'
            $captured | Should -BeNullOrEmpty
        }

        It 'throws the same guided error when the token value is empty/whitespace, and emits no URL' {
            # Arrange
            $global:DeliveryTestFiles['C:\test\.env'] = @('OPENCLAW_GATEWAY_TOKEN=   ')

            # Act
            $captured = $null
            $threw = $false
            $message = ''
            try { $captured = & $script:ScriptPath -EnvFilePath 'C:\test\.env' }
            catch { $threw = $true; $message = $_.Exception.Message }

            # Assert
            $threw | Should -BeTrue
            $message | Should -BeLike '*Invoke-OpenClawAgentOnboarding.ps1*'
            $captured | Should -BeNullOrEmpty
        }
    }

    Context 'secret hygiene' {
        It 'never writes the token value to the verbose/debug/warning/information streams' {
            # Arrange
            $token = 'SECRET-token_value-987'
            $global:DeliveryTestFiles['C:\test\.env'] = @("OPENCLAW_GATEWAY_TOKEN=$token")

            # Act - merge all streams; success-stream (URL) is a [string], log
            # records carry their own record types so they can be filtered out.
            $VerbosePreference = 'Continue'
            $DebugPreference = 'Continue'
            $InformationPreference = 'Continue'
            $all = & { & $script:ScriptPath -EnvFilePath 'C:\test\.env' } *>&1
            $logRecords = $all | Where-Object {
                $_ -is [System.Management.Automation.VerboseRecord] -or
                $_ -is [System.Management.Automation.DebugRecord] -or
                $_ -is [System.Management.Automation.WarningRecord] -or
                $_ -is [System.Management.Automation.InformationRecord] -or
                $_ -is [System.Management.Automation.ErrorRecord]
            }

            # Assert - the token appears only in the returned URL, never in a log stream.
            ($logRecords | Out-String) | Should -Not -Match ([regex]::Escape($token))
        }
    }
}
