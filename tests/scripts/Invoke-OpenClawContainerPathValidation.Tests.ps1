BeforeDiscovery {
    # Import the helper module so Pester mocks scoped to `-ModuleName OpenClawContainerValidation`
    # can locate it during discovery and test runs.
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot 'fixtures/OpenClawContainerValidation.Fixtures.psm1') -Force -ErrorAction Stop
        Import-OpenClawContainerValidationModule -TestsRoot $PSScriptRoot
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
        @($result.EndpointDiagnostics).Count | Should -Be 6
        $result.Live.IsExpected | Should -BeTrue
        $result.Ready.IsExpected | Should -BeTrue
        $result.CoreStatus.IsExpected | Should -BeTrue
        $result.AgentDashboard.IsExpected | Should -BeTrue
        $result.AgentReadyz.IsExpected | Should -BeTrue
        $result.HostAdapterInContainer.IsExpected | Should -BeTrue
        $result.GatewayTokenPresence.IsExpected | Should -BeTrue
        @($result.SupportingDiagnostics).Count | Should -Be 14
        @($script:DockerRequests) | Should -Contain 'container inspect openclaw-core'
        @($script:DockerRequests) | Should -Contain 'container inspect openclaw-agent'
    }

    It 'derives the default CoreBaseUrl from OPENCLAW_HTTP_PORT in the selected env file' {
        $requestedUris = $script:RequestedUris
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_HTTP_PORT=8081', 'OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
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
            $content = switch ($Uri.AbsolutePath) {
                '/health/live' { '{"status":"live"}' }
                '/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                '/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                '/' { '<html><body>OpenClaw Gateway Dashboard</body></html>' }
                '/readyz' { 'ready' }
                default { throw "Unexpected URI: $Uri" }
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

        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -EnvFilePath 'C:\operator\.env' -PassThru

        $result.CoreBaseUrl | Should -Be 'http://127.0.0.1:8081/'
        @($requestedUris) | Should -Contain 'http://127.0.0.1:8081/health/live'
        @($requestedUris) | Should -Contain 'http://127.0.0.1:8081/health/ready'
        @($requestedUris) | Should -Contain 'http://127.0.0.1:8081/api/status'
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
        # Now 5 endpoint-backed probes will fail: Live, Ready, CoreStatus, AgentDashboard, AgentReadyz.
        # GatewayTokenPresence is still expected because Get-Content is mocked to return a token.
        @($result.SupportingDiagnostics | Where-Object { -not $_.IsExpected }).Count | Should -BeGreaterOrEqual 5
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
        $result.SupportingDiagnostics.Count | Should -Be 14
    }
}
