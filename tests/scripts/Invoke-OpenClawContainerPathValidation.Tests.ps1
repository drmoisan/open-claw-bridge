BeforeDiscovery {
    # Import the helper module so Pester mocks scoped to `-ModuleName OpenClawContainerValidation`
    # can locate it during discovery and test runs.
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1' {
    BeforeAll {
        $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
        Import-Module -Name $modulePath -Force -ErrorAction Stop
    }
    BeforeEach {
        $script:RequestedUris = [System.Collections.Generic.List[string]]::new()
        $script:DockerRequests = [System.Collections.Generic.List[string]]::new()
        $script:ScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\Invoke-OpenClawContainerPathValidation.ps1'
        $dockerRequests = $script:DockerRequests

        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                switch ($commandLine) {
                    'version --format {{.Server.Version}}' { '25.0.0' }
                    'container inspect openclaw-core' { '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]' }
                    'container inspect openclaw-agent' { '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]' }
                    default {
                        $global:LASTEXITCODE = 1
                        "Unexpected docker command: $commandLine"
                    }
                }
            }.GetNewClosure())
    }

    AfterEach {
        Remove-Variable -Name 'RequestedUris' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'DockerRequests' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScriptPath' -Scope Script -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-FakeDocker -ErrorAction SilentlyContinue
    }

    It 'returns expected when all container endpoints match their validation contracts' {
        $requestedUris = $script:RequestedUris
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck,
                $Headers,
                $Body
            )

            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $requestedUris.Add([string]$Uri)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true,"cacheStale":false,"lastSuccessfulPollUtc":"2026-04-20T12:00:00Z"}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"lastSuccessfulPollUtc":"2026-04-20T12:00:00Z","cacheItemCounts":{"messages":1,"meetingRequests":0,"events":2},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { throw "Unexpected URI: $Uri" }
            }

            return [pscustomobject]@{
                StatusCode = 200
                Headers    = @{ 'Content-Type' = 'application/json' }
                Content    = $content
            }
        }
        # Extend the fake docker with an exec handler for the HostAdapter probe.
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                switch -Regex ($commandLine) {
                    '^version --format' { '25.0.0'; return }
                    'container inspect openclaw-core' {
                        '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'container inspect openclaw-agent' {
                        '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'compose exec' { '200'; return }
                    default {
                        $global:LASTEXITCODE = 1
                        "Unexpected docker command: $commandLine"
                    }
                }
            }.GetNewClosure())

        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        $result.OverallResult | Should -Be 'Expected'
        $result.IsExpected | Should -BeTrue
        $result.DockerEngine.IsExpected | Should -BeTrue
        @($result.ContainerDiagnostics).Count | Should -Be 6
        @($result.EndpointDiagnostics).Count | Should -Be 7
        $result.Live.IsExpected | Should -BeTrue
        $result.Ready.IsExpected | Should -BeTrue
        $result.CoreStatus.IsExpected | Should -BeTrue
        $result.AgentDashboard.IsExpected | Should -BeTrue
        $result.AgentReadyz.IsExpected | Should -BeTrue
        $result.HostAdapterInContainer.IsExpected | Should -BeTrue
        $result.GatewayTokenPresence.IsExpected | Should -BeTrue
        $result.DashboardAuth.IsExpected | Should -BeTrue
        @($result.SupportingDiagnostics).Count | Should -Be 15
        @($script:DockerRequests) | Should -Contain 'container inspect openclaw-core'
        @($script:DockerRequests) | Should -Contain 'container inspect openclaw-agent'
    }

    It 'returns unexpected when readiness diagnostics report a degraded dependency' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck,
                $Headers,
                $Body
            )

            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $statusCode = 200
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' {
                    $statusCode = 503
                    '{"status":"degraded","sqliteReady":true,"hostAdapterReachable":false,"cacheStale":true}'
                }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":false,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":true,"staleReason":"HostAdapter unreachable"}}' }
                'http://127.0.0.1:18789/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { throw "Unexpected URI: $Uri" }
            }

            return [pscustomobject]@{
                StatusCode = $statusCode
                Headers    = @{ 'Content-Type' = 'application/json' }
                Content    = $content
            }
        }
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                switch -Regex ($commandLine) {
                    '^version --format' { '25.0.0'; return }
                    'container inspect openclaw-core' { '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'; return }
                    'container inspect openclaw-agent' { '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'; return }
                    'compose exec' { '200'; return }
                    default { $global:LASTEXITCODE = 1; "Unexpected: $commandLine" }
                }
            }.GetNewClosure())

        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        $result.OverallResult | Should -Be 'Unexpected'
        $result.IsExpected | Should -BeFalse
        $result.Ready.IsExpected | Should -BeFalse
        $result.Ready.HttpStatusCode | Should -Be 503
        $result.Ready.Details.hostAdapterReachable | Should -BeFalse
        $result.CoreStatus.IsExpected | Should -BeFalse
        $result.CoreStatus.Details.bridgeFreshness.staleReason | Should -Be 'HostAdapter unreachable'
    }

    It 'identifies the container aspect when an expected container is missing' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $commandLine = $Arguments -join ' '
                $global:LASTEXITCODE = if ($commandLine -eq 'container inspect openclaw-agent') { 1 } else { 0 }
                switch -Regex ($commandLine) {
                    '^version --format' { '25.0.0' }
                    'container inspect openclaw-core' { '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]' }
                    'container inspect openclaw-agent' { 'Error: No such container: openclaw-agent' }
                    'compose exec' { '200' }
                    default { "Unexpected docker command: $commandLine" }
                }
            })
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck,
                $Headers,
                $Body
            )

            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { throw "Unexpected URI: $Uri" }
            }

            return [pscustomobject]@{
                StatusCode = 200
                Headers    = @{ 'Content-Type' = 'application/json' }
                Content    = $content
            }
        }

        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $agentExists = $result.ContainerDiagnostics | Where-Object { $_.Name -eq 'AgentContainerExists' }

        $result.OverallResult | Should -Be 'Unexpected'
        $agentExists.IsExpected | Should -BeFalse
        $agentExists.Summary | Should -Be 'Unexpected: Agent container does not exist or could not be inspected.'
    }

    It 'captures request failures as endpoint diagnostics without throwing' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck,
                $Headers,
                $Body
            )

            $null = @($Uri, $Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            throw 'Connection refused'
        }

        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        $result.OverallResult | Should -Be 'Unexpected'
        $result.IsExpected | Should -BeFalse
        $result.Live.IsExpected | Should -BeFalse
        $result.Live.ErrorMessage | Should -Be 'Connection refused'
        # Now 6 endpoint-backed probes will fail: Live, Ready, CoreStatus, AgentDashboard, AgentReadyz, DashboardAuth.
        # GatewayTokenPresence is still expected because Get-Content is mocked to return a token.
        @($result.SupportingDiagnostics | Where-Object { -not $_.IsExpected }).Count | Should -BeGreaterOrEqual 6
    }

    It 'emits JSON when requested' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck,
                $Headers,
                $Body
            )

            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = if ([string]$Uri -eq 'http://127.0.0.1:18789/') {
                '<html><body>OpenClaw Gateway Dashboard</body></html>'
            } elseif ([string]$Uri -like '*/health/live') {
                '{"status":"live"}'
            } elseif ([string]$Uri -like '*/health/ready') {
                '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}'
            } elseif ([string]$Uri -like '*/readyz') {
                'ready'
            } elseif ([string]$Uri -like '*/auth/verify') {
                '{"ok":true}'
            } else {
                '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}'
            }

            return [pscustomobject]@{
                StatusCode = 200
                Headers    = @{ 'Content-Type' = 'application/json' }
                Content    = $content
            }
        }
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                switch -Regex ($commandLine) {
                    '^version --format' { '25.0.0'; return }
                    'container inspect openclaw-core' { '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'; return }
                    'container inspect openclaw-agent' { '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'; return }
                    'compose exec' { '200'; return }
                    default { $global:LASTEXITCODE = 1; "Unexpected: $commandLine" }
                }
            }.GetNewClosure())

        $json = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -AsJson
        $result = $json | ConvertFrom-Json

        $result.OverallResult | Should -Be 'Expected'
        $result.SupportingDiagnostics.Count | Should -Be 15
    }

    It 'DashboardAuth probe reports Unexpected when OPENCLAW_GATEWAY_TOKEN is empty' -Tag 'ExpectFail-Phase2' {
        # Arrange: the extended script reads the operator's `.env`, detects an empty
        # OPENCLAW_GATEWAY_TOKEN, and surfaces a DashboardAuth probe that refuses to
        # attempt an authenticated call. Mock Get-Content so the script sees an
        # empty token without touching disk.
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Get-Content {
            return @('OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest', 'OPENCLAW_GATEWAY_TOKEN=')
        } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }

        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck
            )
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                default { '{}' }
            }
            return [pscustomobject]@{
                StatusCode = 200
                Headers    = @{ 'Content-Type' = 'application/json' }
                Content    = $content
            }
        }

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $result.OverallResult | Should -Be 'Unexpected'
        $dashboardAuth = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'DashboardAuth' }
        $dashboardAuth | Should -Not -BeNullOrEmpty
        $dashboardAuth.IsExpected | Should -BeFalse
    }

    # ---- Phase 5 probe-level tests (expect-fail before probes are implemented) ----

    It 'AgentReadyz probe reports IsExpected when /readyz returns HTTP 200' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $readyz = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'AgentReadyz' }
        $readyz | Should -Not -BeNullOrEmpty
        $readyz.IsExpected | Should -BeTrue
    }

    It 'AgentReadyz probe reports Unexpected when /readyz returns HTTP 503' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $statusCode = 200
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { $statusCode = 503; 'not ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = $statusCode; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $readyz = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'AgentReadyz' }
        $readyz | Should -Not -BeNullOrEmpty
        $readyz.IsExpected | Should -BeFalse
    }

    It 'AgentReadyz probe reports Unexpected when /readyz is unreachable' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            if ([string]$Uri -eq 'http://127.0.0.1:18789/readyz') {
                throw 'Connection refused'
            }
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $readyz = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'AgentReadyz' }
        $readyz | Should -Not -BeNullOrEmpty
        $readyz.IsExpected | Should -BeFalse
    }

    It 'HostAdapterInContainer probe reports IsExpected when docker exec returns 200' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        # Extend the fake docker so `exec ... curl ... /v1/status` returns HTTP 200 + body.
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                switch -Regex ($commandLine) {
                    '^version --format' { '25.0.0'; return }
                    'container inspect openclaw-core' {
                        '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'container inspect openclaw-agent' {
                        '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'exec.*openclaw-agent.*v1/status' {
                        '200'
                        return
                    }
                    default {
                        $global:LASTEXITCODE = 1
                        "Unexpected docker command: $commandLine"
                    }
                }
            }.GetNewClosure())
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'HostAdapterInContainer' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeTrue
    }

    It 'HostAdapterInContainer probe reports Unexpected when docker exec returns non-200' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                $global:LASTEXITCODE = 0
                switch -Regex ($commandLine) {
                    '^version --format' { '25.0.0'; return }
                    'container inspect openclaw-core' {
                        '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'container inspect openclaw-agent' {
                        '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'exec.*openclaw-agent.*v1/status' { '500'; return }
                    default {
                        $global:LASTEXITCODE = 1
                        "Unexpected docker command: $commandLine"
                    }
                }
            }.GetNewClosure())
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'HostAdapterInContainer' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeFalse
    }

    It 'HostAdapterInContainer probe reports Unexpected when docker exec exits non-zero' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $dockerRequests = $script:DockerRequests
        Set-Item -Path Function:\Global:Invoke-FakeDocker -Value ({
                [CmdletBinding()]
                param([Parameter(ValueFromRemainingArguments = $true)][object[]]$Arguments)
                $commandLine = $Arguments -join ' '
                $dockerRequests.Add($commandLine)
                switch -Regex ($commandLine) {
                    '^version --format' { $global:LASTEXITCODE = 0; '25.0.0'; return }
                    'container inspect openclaw-core' {
                        $global:LASTEXITCODE = 0
                        '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'container inspect openclaw-agent' {
                        $global:LASTEXITCODE = 0
                        '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]'
                        return
                    }
                    'exec.*openclaw-agent' { $global:LASTEXITCODE = 2; 'container not found'; return }
                    default {
                        $global:LASTEXITCODE = 1
                        "Unexpected docker command: $commandLine"
                    }
                }
            }.GetNewClosure())
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'HostAdapterInContainer' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeFalse
    }

    It 'GatewayTokenPresence probe reports IsExpected when .env has OPENCLAW_GATEWAY_TOKEN with non-empty value' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'GatewayTokenPresence' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeTrue
    }

    It 'DashboardAuth probe reports IsExpected when POST with stored token returns 200' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $statusCode = 200
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' {
                    if ($Method -eq 'Post') { '{"ok":true}' } else { $statusCode = 405; 'method not allowed' }
                }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = $statusCode; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'DashboardAuth' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeTrue
    }

    It 'DashboardAuth probe reports Unexpected when POST returns 401' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $statusCode = 200
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' {
                    $statusCode = 401; '{"error":"unauthorized"}'
                }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = $statusCode; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'DashboardAuth' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeFalse
    }

    It 'DashboardAuth probe reports Unexpected when response body is non-JSON / malformed' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $statusCode = 200
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/auth/verify' {
                    # HTTP 200 but body is not JSON.
                    '<html>server error page</html>'
                }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = $statusCode; Headers = @{ 'Content-Type' = 'text/html' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'DashboardAuth' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeFalse
    }

    It 'GatewayTokenPresence probe reports Unexpected when .env omits OPENCLAW_GATEWAY_TOKEN' -Tag 'ExpectFail-Phase2' {
        # Arrange: simulate an `.env` file that contains no OPENCLAW_GATEWAY_TOKEN line
        # at all. The extended script must surface a GatewayTokenPresence probe whose
        # SupportingDiagnostics names the missing key.
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Get-Content {
            return @('OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest', 'OPENCLAW_HTTP_PORT=8080')
        } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }

        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param(
                [uri]$Uri,
                [string]$Method,
                [int]$TimeoutSec,
                [switch]$UseBasicParsing,
                [switch]$SkipHttpErrorCheck
            )
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                default { '{}' }
            }
            return [pscustomobject]@{
                StatusCode = 200
                Headers    = @{ 'Content-Type' = 'application/json' }
                Content    = $content
            }
        }

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $tokenProbe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'GatewayTokenPresence' }
        $tokenProbe | Should -Not -BeNullOrEmpty
        $tokenProbe.IsExpected | Should -BeFalse
        # Supporting diagnostics string (Summary or Details) must reference the missing key.
        ($tokenProbe.Summary + ' ' + ($tokenProbe.Details | ConvertTo-Json -Compress)) | Should -Match 'OPENCLAW_GATEWAY_TOKEN'
    }
}
