BeforeDiscovery {
    $modulePath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'Invoke-OpenClawContainerPathValidation.ps1 (HostAdapterInContainer probe)' {
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
}
