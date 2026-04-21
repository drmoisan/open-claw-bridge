# PSScriptAnalyzer suppressions for this test file:
#  - PSAvoidUsingConvertToSecureStringWithPlainText: the test inputs are
#    synthetic placeholder strings that stand in for an operator-supplied API
#    key; no real secret is ever processed here.
#  - PSAvoidGlobalVars: the Global:__OpenClawOnboardingTestWrittenContent
#    reference is the only reliable way to share a capture list between the
#    Pester test scope and a Mock scriptblock running in Pester's scope.
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '', Justification = 'Test inputs are synthetic placeholder strings; no real secret is involved.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidGlobalVars', '', Justification = 'Global variable is the intentional bridge between test scope and Pester Mock scope for capturing writes.')]
param()

# Pester tests for scripts/Invoke-OpenClawAgentOnboarding.ps1.
#
# The onboarding script orchestrates an upstream `openclaw onboard` run in a
# throwaway container, parses `OPENCLAW_GATEWAY_TOKEN=<value>` from stdout, and
# writes the token to the repository-root `.env` file. These tests exercise
# every failure mode and the idempotency contract without touching the real
# docker CLI or the real filesystem: docker is injected as a function on the
# global scope (`Invoke-FakeDocker`) and filesystem reads/writes are covered
# by Pester mocks.

Describe 'Invoke-OpenClawAgentOnboarding.ps1' {
    BeforeAll {
        $script:ScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\Invoke-OpenClawAgentOnboarding.ps1'
    }

    BeforeEach {
        $script:DockerRequests = [System.Collections.Generic.List[string]]::new()
        $script:WrittenContent = [System.Collections.Generic.List[object]]::new()
        # Publish a module-visible reference so mock scriptblocks (which run in
        # Pester's own scope) can still reach the same list instance.
        $Global:__OpenClawOnboardingTestWrittenContent = $script:WrittenContent
        $dockerRequests = $script:DockerRequests

        # Default fake docker succeeds and emits a token line for the onboard command.
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )
                $commandLine = ($Arguments -join ' ')
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                if ($commandLine -match '--version') {
                    'Docker version 25.0.0, build abc'
                    return
                }
                if ($commandLine -match 'onboard') {
                    # Emulate an upstream onboard run that prints a generated token line.
                    "Starting onboard..."
                    "OPENCLAW_GATEWAY_TOKEN=sample-generated-token-abc123"
                    "Done."
                    return
                }
                $global:LASTEXITCODE = 1
                "Unexpected docker command: $commandLine"
            }.GetNewClosure())
    }

    AfterEach {
        Remove-Item Function:\Global:Invoke-FakeDocker -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath 'Variable:Global:__OpenClawOnboardingTestWrittenContent' -ErrorAction SilentlyContinue
        Remove-Variable -Name 'DockerRequests' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'WrittenContent' -Scope Script -ErrorAction SilentlyContinue
    }

    It 'Onboarding script fails fast when docker CLI is unavailable' -Tag 'ExpectFail-Phase4' {
        # Arrange: simulate no docker CLI resolvable on PATH by pointing DockerPath at a
        # non-existent command and mocking Get-Command to return $null for it.
        Mock Get-Command {
            param([string]$Name, $ErrorAction)
            $null = $ErrorAction
            if ($Name -in @('docker', 'missing-docker-cli')) { return $null }
            return (Microsoft.PowerShell.Core\Get-Command -Name $Name -ErrorAction SilentlyContinue)
        }

        # Act + Assert: script must throw a descriptive error category.
        { & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'missing-docker-cli' -EnvFilePath 'dummy.env' } |
            Should -Throw -ExpectedMessage '*Docker*'
    }

    It 'Onboarding script propagates non-zero exit from upstream onboard command' -Tag 'ExpectFail-Phase4' {
        # Arrange: docker returns non-zero for the onboard subcommand.
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = ($Arguments -join ' ')
                $dockerRequests.Add($commandLine)
                if ($commandLine -match '--version') {
                    $global:LASTEXITCODE = 0
                    return 'Docker version 25.0.0, build abc'
                }
                $global:LASTEXITCODE = 42
                'upstream onboard failed: provider error'
            }.GetNewClosure())
        Mock Test-Path { return $true }
        Mock Get-Content { return @() } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }

        { & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env' } |
            Should -Throw -ExpectedMessage '*onboard*'
    }

    It 'Onboarding script fails fast when onboard output contains no OPENCLAW_GATEWAY_TOKEN= line' -Tag 'ExpectFail-Phase4' {
        # Arrange: docker succeeds but prints only boilerplate with no token line.
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = ($Arguments -join ' ')
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                if ($commandLine -match '--version') { return 'Docker version 25.0.0, build abc' }
                'Starting onboard...'
                'Done.'
            }.GetNewClosure())
        Mock Test-Path { return $true }
        Mock Get-Content { return @() } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }

        { & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env' } |
            Should -Throw -ExpectedMessage '*malformed*'
    }

    It 'Onboarding script is idempotent when OPENCLAW_GATEWAY_TOKEN is already present in .env and -Force is not supplied' -Tag 'ExpectFail-Phase4' {
        # Arrange: `.env` already contains a non-empty token. Docker must NOT be called
        # for onboard; only a version check is permitted.
        Mock Test-Path { return $true }
        Mock Get-Content {
            return @(
                'OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest',
                'OPENCLAW_GATEWAY_TOKEN=existing-token-xyz'
            )
        } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock Set-Content {
            $effectivePath = if ($Path) { $Path } else { $LiteralPath }
            $Global:__OpenClawOnboardingTestWrittenContent.Add(@{ Path = $effectivePath; Value = $Value })
        }

        # Act
        & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env' -Verbose

        # Assert: no onboard command ran; no .env write occurred.
        @($script:DockerRequests | Where-Object { $_ -match 'onboard' }).Count | Should -Be 0
        @($script:WrittenContent).Count | Should -Be 0
    }

    It 'Onboarding script overwrites token when OPENCLAW_GATEWAY_TOKEN is present and -Force is supplied' -Tag 'ExpectFail-Phase4' {
        # Arrange: `.env` has an existing token; -Force instructs the script to re-run
        # onboard and replace the value.
        Mock Test-Path { return $true }
        Mock Get-Content {
            return @(
                'OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest',
                'OPENCLAW_GATEWAY_TOKEN=old-token'
            )
        } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock Set-Content {
            $effectivePath = if ($Path) { $Path } else { $LiteralPath }
            $Global:__OpenClawOnboardingTestWrittenContent.Add(@{ Path = $effectivePath; Value = $Value })
        }

        # Act
        & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env' -Force

        # Assert: onboard was executed and a write occurred with the new token.
        @($script:DockerRequests | Where-Object { $_ -match 'onboard' }).Count | Should -BeGreaterOrEqual 1
        @($script:WrittenContent).Count | Should -BeGreaterOrEqual 1
        $lastWritten = ($script:WrittenContent[-1].Value -join "`n")
        $lastWritten | Should -Match 'OPENCLAW_GATEWAY_TOKEN=sample-generated-token-abc123'
        $lastWritten | Should -Not -Match 'OPENCLAW_GATEWAY_TOKEN=old-token'
    }

    It 'Onboarding script consumes -AnthropicApiKey parameter without prompting' -Tag 'ExpectFail-Phase4' {
        # Arrange: caller passes the API key explicitly; Read-Host must not be called.
        Mock Test-Path { return $true }
        Mock Get-Content { return @() } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock Set-Content {
            $effectivePath = if ($Path) { $Path } else { $LiteralPath }
            $Global:__OpenClawOnboardingTestWrittenContent.Add(@{ Path = $effectivePath; Value = $Value })
        }
        Mock Read-Host {}

        # Act
        & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env'

        # Assert
        Should -Invoke -CommandName Read-Host -Times 0
    }

    It 'Onboarding script prompts via Read-Host -AsSecureString when -AnthropicApiKey is absent' -Tag 'ExpectFail-Phase4' {
        # Arrange: no -AnthropicApiKey; the script must call Read-Host -AsSecureString exactly once.
        Mock Test-Path { return $true }
        Mock Get-Content { return @() } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock Set-Content {
            $effectivePath = if ($Path) { $Path } else { $LiteralPath }
            $Global:__OpenClawOnboardingTestWrittenContent.Add(@{ Path = $effectivePath; Value = $Value })
        }
        Mock Read-Host { return (ConvertTo-SecureString -String 'sk-prompted' -AsPlainText -Force) }

        # Act
        & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env'

        # Assert
        Should -Invoke -CommandName Read-Host -Times 1 -ParameterFilter { $AsSecureString -eq $true }
    }

    It 'Onboarding script invokes docker with the default OnboardBinaryPath when -OnboardBinaryPath is not supplied' {
        # Arrange: standard fake-docker shim records the argument list; capture it for
        # positional inspection. No temporary files; no real filesystem writes.
        Mock Test-Path { return $true }
        Mock Get-Content { return @() } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock Set-Content {
            $effectivePath = if ($Path) { $Path } else { $LiteralPath }
            $Global:__OpenClawOnboardingTestWrittenContent.Add(@{ Path = $effectivePath; Value = $Value })
        }

        # Act: invoke without -OnboardBinaryPath. The default 'dist/index.js' must land
        # immediately after 'openclaw-agent' and immediately before 'onboard' in argv.
        & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env'

        # Assert
        $onboardLine = @($script:DockerRequests | Where-Object { $_ -match 'onboard' }) | Select-Object -First 1
        $onboardLine | Should -Not -BeNullOrEmpty
        $tokens = $onboardLine -split '\s+'
        $agentIndex = [Array]::IndexOf($tokens, 'openclaw-agent')
        $agentIndex | Should -BeGreaterThan -1
        $tokens[$agentIndex + 1] | Should -Be 'dist/index.js'
        $tokens[$agentIndex + 2] | Should -Be 'onboard'
    }

    It 'Onboarding script substitutes -OnboardBinaryPath into docker arguments when explicitly supplied' {
        # Arrange: same fake-docker shim. The caller overrides the onboard binary path.
        Mock Test-Path { return $true }
        Mock Get-Content { return @() } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock Set-Content {
            $effectivePath = if ($Path) { $Path } else { $LiteralPath }
            $Global:__OpenClawOnboardingTestWrittenContent.Add(@{ Path = $effectivePath; Value = $Value })
        }

        # Act: override the binary path.
        & $script:ScriptPath -AnthropicApiKey (ConvertTo-SecureString -String 'sk-test' -AsPlainText -Force) -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'dummy.env' -OnboardBinaryPath 'openclaw.mjs'

        # Assert: the captured argument list contains the override and does not contain the default.
        $onboardLine = @($script:DockerRequests | Where-Object { $_ -match 'onboard' }) | Select-Object -First 1
        $onboardLine | Should -Not -BeNullOrEmpty
        $onboardLine | Should -Match 'openclaw\.mjs'
        $onboardLine | Should -Not -Match 'dist/index\.js'
    }
}
