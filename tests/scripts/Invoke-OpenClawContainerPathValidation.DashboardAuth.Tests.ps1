BeforeDiscovery {
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1 (DashboardAuth probe)' {
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

    It 'DashboardAuth probe POSTs to overridden -DashboardAuthPath when supplied to the script' {
        # Arrange: capture every URI that the module's Invoke-WebRequest mock observes.
        $capturedUris = $script:RequestedUris
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Invoke-WebRequest {
            param([uri]$Uri, [string]$Method, [int]$TimeoutSec, [switch]$UseBasicParsing, [switch]$SkipHttpErrorCheck, $Headers, $Body)
            $null = @($Method, $TimeoutSec, $UseBasicParsing, $SkipHttpErrorCheck, $Headers, $Body)
            $capturedUris.Add([string]$Uri)
            $content = switch ([string]$Uri) {
                'http://127.0.0.1:8080/health/live' { '{"status":"live"}' }
                'http://127.0.0.1:8080/health/ready' { '{"status":"ready","sqliteReady":true,"hostAdapterReachable":true}' }
                'http://127.0.0.1:8080/api/status' { '{"sqliteReady":true,"hostAdapterReachable":true,"cacheItemCounts":{"messages":0,"meetingRequests":0,"events":0},"bridgeFreshness":{"cacheStale":false}}' }
                'http://127.0.0.1:18789/' { '<html>dashboard</html>' }
                'http://127.0.0.1:18789/readyz' { 'ready' }
                'http://127.0.0.1:18789/api/auth/verify' { '{"ok":true}' }
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }

        # Act: override the DashboardAuthPath.
        $null = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -DashboardAuthPath '/api/auth/verify' -PassThru

        # Assert: captured URIs include the override and not the default.
        $capturedUris | Should -Contain 'http://127.0.0.1:18789/api/auth/verify'
        $capturedUris | Should -Not -Contain 'http://127.0.0.1:18789/auth/verify'
    }
}
