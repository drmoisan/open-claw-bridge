BeforeDiscovery {
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1 (default -EnvFilePath resolution)' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot 'fixtures/OpenClawContainerValidation.Fixtures.psm1') -Force -ErrorAction Stop
        Import-OpenClawContainerValidationModule -TestsRoot $PSScriptRoot
        $script:FakeOperatorEnvPath = 'C:\FakeAppData\OpenClaw\operator-config\.env'
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

    It 'resolves the default EnvFilePath to the operator env file when it exists' {
        # Arrange
        $fakeOperatorEnvPath = $script:FakeOperatorEnvPath
        Mock Get-OpenClawOperatorEnvFilePath { return $fakeOperatorEnvPath }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true } -ParameterFilter { $LiteralPath -eq $fakeOperatorEnvPath }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_HTTP_PORT=8081', 'OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
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

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $result.EnvFilePath | Should -Be $fakeOperatorEnvPath
    }

    It 'falls back to ./.env when the operator env file does not exist' {
        # Arrange
        $fakeOperatorEnvPath = $script:FakeOperatorEnvPath
        Mock Get-OpenClawOperatorEnvFilePath { return $fakeOperatorEnvPath }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $false } -ParameterFilter { $LiteralPath -eq $fakeOperatorEnvPath }
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }
        Mock -ModuleName OpenClawContainerValidation Get-Content { return @('OPENCLAW_HTTP_PORT=8081', 'OPENCLAW_GATEWAY_TOKEN=valid-token-abc') } -ParameterFilter { (($Path -and $Path -match '\.env$') -or ($LiteralPath -and $LiteralPath -match '\.env$')) }
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

        # Act
        $result = & $script:ScriptPath -DockerPath 'Invoke-FakeDocker' -PassThru

        # Assert
        $result.EnvFilePath | Should -Be './.env'
    }
}
