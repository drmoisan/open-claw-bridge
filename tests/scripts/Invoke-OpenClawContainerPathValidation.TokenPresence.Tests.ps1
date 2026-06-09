BeforeDiscovery {
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1 (GatewayTokenPresence probe)' {
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

    It 'GatewayTokenPresence probe reports IsExpected when .env has OPENCLAW_GATEWAY_TOKEN with non-empty value' -Tag 'ExpectFail-Phase5' {
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8081/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8081/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8081/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $probe = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'GatewayTokenPresence' }
        $probe | Should -Not -BeNullOrEmpty
        $probe.IsExpected | Should -BeTrue
    }

    It 'GatewayTokenPresence probe reports Unexpected when .env omits OPENCLAW_GATEWAY_TOKEN' -Tag 'ExpectFail-Phase2' {
        # Arrange: simulate an `.env` file that contains no OPENCLAW_GATEWAY_TOKEN line
        # at all. The extended script must surface a GatewayTokenPresence probe whose
        # SupportingDiagnostics names the missing key.
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Get-Content {
            return @('OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest', 'OPENCLAW_HTTP_PORT=8081')
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
                'http://127.0.0.1:8081/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8081/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8081/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
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
