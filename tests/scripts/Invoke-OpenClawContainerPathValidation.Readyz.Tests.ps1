BeforeDiscovery {
    # Import the helper module so Pester mocks scoped to `-ModuleName OpenClawContainerValidation`
    # can locate it during discovery and test runs.
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1 (AgentReadyz probe)' {
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
                default { '{}' }
            }
            return [pscustomobject]@{ StatusCode = 200; Headers = @{ 'Content-Type' = 'application/json' }; Content = $content }
        }
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru
        $readyz = $result.SupportingDiagnostics | Where-Object { $_.Name -eq 'AgentReadyz' }
        $readyz | Should -Not -BeNullOrEmpty
        $readyz.IsExpected | Should -BeFalse
    }
}
