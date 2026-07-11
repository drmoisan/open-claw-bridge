BeforeDiscovery {
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1 (GatewayTokenInContainer probe)' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot 'fixtures/OpenClawContainerValidation.Fixtures.psm1') -Force -ErrorAction Stop
        Import-OpenClawContainerValidationModule -TestsRoot $PSScriptRoot

        # Shared healthy HTTP + docker seam. $TokenInContainerResult selects whether the
        # in-container gateway-token probe reports the token as present or absent. Defined
        # in BeforeAll so it is available inside It blocks at run time.
        function Install-GatewayTokenFakeSeam {
            [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'TokenInContainerResult', Justification = 'Captured by GetNewClosure into the fake-docker scriptblock; the analyzer cannot see closure capture.')]
            [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'DockerRequests', Justification = 'Captured by GetNewClosure into the fake-docker scriptblock; the analyzer cannot see closure capture.')]
            param(
                [Parameter(Mandatory = $true)][string]$TokenInContainerResult,
                # ValidateNotNull + AllowEmptyCollection (rather than Mandatory alone) because
                # the default Mandatory binder rejects an empty generic list; the list is
                # intentionally empty at call time and the fake docker fills it.
                [Parameter(Mandatory = $true)]
                [ValidateNotNull()]
                [AllowEmptyCollection()]
                [System.Collections.Generic.List[string]]$DockerRequests
            )
            Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
            Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
            Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
                param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
                $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
                $content = switch ($Uri.AbsolutePath) {
                    '/health/live' { '{"status":"live"}' }
                    '/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                    '/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                    '/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                    '/readyz' { 'ready' }
                    default { '{}' }
                }
                return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
            }
            Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                    [CmdletBinding()]
                    param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                    $commandLine = $Arguments -join ' '
                    $DockerRequests.Add($commandLine)
                    $global:LASTEXITCODE = 0
                    switch -Regex ($commandLine) {
                        '^version --format' { '25.0.0'; return }
                        'container inspect openclaw-core' { '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'; return }
                        'container inspect openclaw-agent' { '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'; return }
                        'OPENCLAW_GATEWAY_TOKEN' { $TokenInContainerResult; return }
                        'compose exec' { '200'; return }
                        default { $global:LASTEXITCODE = 1; "Unexpected: $commandLine" }
                    }
                }.GetNewClosure())
        }
    }

    BeforeEach {
        $script:RequestedUris = New-RequestedUriList
        $script:DockerRequests = New-DockerRequestList
        $script:ScriptPath = Get-ContainerValidationScriptPath -TestsRoot $PSScriptRoot
        Install-DefaultInvokeFakeDocker -DockerRequests $script:DockerRequests
    }

    AfterEach {
        Reset-ContainerPathValidationTestState
        Remove-Variable -Name 'RequestedUris' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'DockerRequests' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScriptPath' -Scope Script -ErrorAction SilentlyContinue
    }

    It 'reports GatewayTokenInContainer as expected when the in-container token is present' {
        # Arrange
        Install-GatewayTokenFakeSeam -TokenInContainerResult 'present' -DockerRequests $script:DockerRequests

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $result.GatewayTokenInContainer.IsExpected | Should -BeTrue
    }

    It 'reports GatewayTokenInContainer as unexpected when the in-container token is absent' {
        # Arrange
        Install-GatewayTokenFakeSeam -TokenInContainerResult 'absent' -DockerRequests $script:DockerRequests

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $result.GatewayTokenInContainer.IsExpected | Should -BeFalse
        $result.GatewayTokenInContainer.Summary | Should -Match 'missing or empty inside the agent container'
        $result.GatewayTokenInContainer.Details.probeResult | Should -Be 'absent'
    }

    It 'reports the AgentDashboard expectation without implying operator authentication' {
        # Arrange
        Install-GatewayTokenFakeSeam -TokenInContainerResult 'present' -DockerRequests $script:DockerRequests

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $result.AgentDashboard.ExpectedCondition | Should -Not -Match 'authenticat'
        $result.AgentDashboard.ExpectedCondition | Should -Match 'Control UI'
    }
}
