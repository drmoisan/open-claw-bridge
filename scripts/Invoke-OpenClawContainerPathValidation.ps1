#Requires -Version 7
<#
.SYNOPSIS
Single pass/fail diagnostic for the OpenClaw container path.

.DESCRIPTION
Probes container health, `/health` endpoints on `openclaw-core`, readiness
(`/readyz`) on `openclaw-agent`, in-container HostAdapter
reachability via `docker compose exec`, and the presence of
`OPENCLAW_GATEWAY_TOKEN` in the operator's `.env`. Aggregates the results
into a single `OverallResult` value (`Expected` or `Unexpected`).

Helpers (URL composition, docker wrapper, HTTP wrapper, `.env` parsing,
and the four new probes introduced by issue #38) live in the
`OpenClawContainerValidation` module under
`scripts/powershell/modules/OpenClawContainerValidation/`.

.PARAMETER CoreBaseUrl
Base URL for OpenClaw.Core endpoint probes. When omitted, the script reads
`OPENCLAW_HTTP_PORT` from `-EnvFilePath` and uses `http://127.0.0.1:<port>`.
If the env file or port setting is absent, the fallback is
`http://127.0.0.1:8080`.
#>
[CmdletBinding()]
param(
    [uri]$CoreBaseUrl,
    [uri]$AgentBaseUrl = 'http://127.0.0.1:18789',
    [string]$CoreContainerName = 'openclaw-core',
    [string]$AgentContainerName = 'openclaw-agent',
    [string]$DockerPath = 'docker',
    [ValidateRange(1, 300)]
    [int]$TimeoutSeconds = 10,
    [string]$EnvFilePath = './.env',
    [switch]$PassThru,
    [switch]$AsJson
)
$ErrorActionPreference = 'Stop'

$moduleManifest = Join-Path $PSScriptRoot 'powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1'
# Import the helper module only if not already loaded. In Pester test runs the
# module is pre-imported in BeforeAll so Mock -ModuleName can intercept cmdlets
# inside the module's scope; re-importing with -Force would invalidate those mocks.
if (-not (Get-Module -Name OpenClawContainerValidation)) {
    Import-Module -Name $moduleManifest -ErrorAction Stop
}

function Get-OpenClawCoreBaseUrl {
    [CmdletBinding()]
    [OutputType([uri])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$EnvFilePath
    )

    $envMap = Get-OpenClawEnvFileMap -EnvFilePath $EnvFilePath
    $portValue = '8080'
    if ($envMap.ContainsKey('OPENCLAW_HTTP_PORT') -and -not [string]::IsNullOrWhiteSpace([string]$envMap['OPENCLAW_HTTP_PORT'])) {
        $portValue = ([string]$envMap['OPENCLAW_HTTP_PORT']).Trim().Trim([char[]]@('"', "'"))
    }

    $port = 0
    if (-not [int]::TryParse($portValue, [ref]$port) -or $port -lt 1 -or $port -gt 65535) {
        throw "OPENCLAW_HTTP_PORT in '$EnvFilePath' must be an integer from 1 through 65535. Actual value: '$portValue'."
    }

    return [uri]("http://127.0.0.1:{0}" -f $port)
}

function Invoke-OpenClawDockerEngineValidation {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ExecutablePath)
    $command = Invoke-OpenClawDockerCommand -ExecutablePath $ExecutablePath -CommandArguments @('version', '--format', '{{.Server.Version}}')
    $version = [string](@($command.Output) -join ' ').Trim()
    $isExpected = $command.Succeeded -and -not [string]::IsNullOrWhiteSpace($version)
    $summary = if ($isExpected) {
        'Expected: Docker engine responded to the version probe.'
    }
    else {
        'Unexpected: Docker engine did not respond to the version probe.'
    }
    return Get-OpenClawValidationResult `
        -Category 'Docker' `
        -Name 'DockerEngine' `
        -Target $ExecutablePath `
        -ExpectedCondition 'Docker command is available and the Docker engine returns a server version' `
        -IsExpected $isExpected `
        -Summary $summary `
        -Details @{
        dockerPath = $ExecutablePath
        version    = $version
        exitCode   = $command.ExitCode
        output     = @($command.Output)
    }
}

function Get-OpenClawContainerInspection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$DockerExecutablePath,
        [Parameter(Mandatory = $true)][string]$ContainerName
    )
    $command = Invoke-OpenClawDockerCommand -ExecutablePath $DockerExecutablePath -CommandArguments @('container', 'inspect', $ContainerName)
    if (-not $command.Succeeded) {
        return [pscustomobject]@{
            Exists       = $false
            Container    = $null
            ExitCode     = $command.ExitCode
            Output       = @($command.Output)
            ErrorMessage = $command.ErrorMessage
        }
    }
    try {
        $containers = @((@($command.Output) -join [Environment]::NewLine) | ConvertFrom-Json -ErrorAction Stop)
        return [pscustomobject]@{
            Exists       = $containers.Count -gt 0
            Container    = if ($containers.Count -gt 0) { $containers[0] } else { $null }
            ExitCode     = $command.ExitCode
            Output       = @($command.Output)
            ErrorMessage = $null
        }
    }
    catch {
        return [pscustomobject]@{
            Exists       = $false
            Container    = $null
            ExitCode     = $command.ExitCode
            Output       = @($command.Output)
            ErrorMessage = "Docker inspect returned invalid JSON for container '$ContainerName': $($_.Exception.Message)"
        }
    }
}

function Invoke-OpenClawContainerValidation {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory = $true)][string]$DockerExecutablePath,
        [Parameter(Mandatory = $true)][string]$ContainerName,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )
    $inspection = Get-OpenClawContainerInspection -DockerExecutablePath $DockerExecutablePath -ContainerName $ContainerName
    $container = $inspection.Container
    $state = Get-OpenClawPropertyValue -InputObject $container -Name 'State'
    $config = Get-OpenClawPropertyValue -InputObject $container -Name 'Config'
    $health = Get-OpenClawPropertyValue -InputObject $state -Name 'Health'
    $status = [string](Get-OpenClawPropertyValue -InputObject $state -Name 'Status')
    $healthStatus = [string](Get-OpenClawPropertyValue -InputObject $health -Name 'Status')
    $image = [string](Get-OpenClawPropertyValue -InputObject $config -Name 'Image')
    $running = Get-OpenClawPropertyValue -InputObject $state -Name 'Running'
    $existsSummary = if ($inspection.Exists) { "Expected: $DisplayName container exists." } else { "Unexpected: $DisplayName container does not exist or could not be inspected." }
    $runningExpected = $inspection.Exists -and $running -eq $true -and $status -eq 'running'
    $runningSummary = if ($runningExpected) { "Expected: $DisplayName container is running." } else { "Unexpected: $DisplayName container is not running." }
    $healthExpected = $inspection.Exists -and $healthStatus -eq 'healthy'
    $healthSummary = if ($healthExpected) { "Expected: $DisplayName container health is healthy." } else { "Unexpected: $DisplayName container health is not healthy." }
    return @(
        Get-OpenClawValidationResult -Category 'Container' -Name "$DisplayName`ContainerExists" -Target $ContainerName `
            -ExpectedCondition 'Docker inspect succeeds for the expected container name' `
            -IsExpected $inspection.Exists -Summary $existsSummary `
            -Details @{ containerName = $ContainerName; image = $image; exitCode = $inspection.ExitCode; output = @($inspection.Output) }
        Get-OpenClawValidationResult -Category 'Container' -Name "$DisplayName`ContainerRunning" -Target $ContainerName `
            -ExpectedCondition 'Container state is running' `
            -IsExpected $runningExpected -Summary $runningSummary `
            -Details @{ containerName = $ContainerName; image = $image; status = $status; running = $running }
        Get-OpenClawValidationResult -Category 'Container' -Name "$DisplayName`ContainerHealthy" -Target $ContainerName `
            -ExpectedCondition 'Container health status is healthy' `
            -IsExpected $healthExpected -Summary $healthSummary `
            -Details @{ containerName = $ContainerName; image = $image; status = $status; healthStatus = $healthStatus; errorMessage = $inspection.ErrorMessage }
    )
}

function Invoke-OpenClawLiveEndpointValidation {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][uri]$Uri, [Parameter(Mandatory = $true)][int]$TimeoutSeconds)
    $request = Invoke-OpenClawEndpointRequest -Uri $Uri -TimeoutSeconds $TimeoutSeconds
    $status = [string](Get-OpenClawPropertyValue -InputObject $request.Json -Name 'status')
    $isExpected = $request.RequestSucceeded -and $request.HttpStatusCode -eq 200 -and $status -eq 'live'
    $summary = if ($isExpected) { 'Expected: core live endpoint returned HTTP 200 with status live.' } else { 'Unexpected: core live endpoint did not return HTTP 200 with status live.' }
    return Get-OpenClawValidationResult -Name 'Live' -Uri $Uri `
        -ExpectedCondition 'HTTP 200 and JSON status live' -Request $request -IsExpected $isExpected -Summary $summary `
        -Details @{ status = $status }
}

function Invoke-OpenClawReadyEndpointValidation {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][uri]$Uri, [Parameter(Mandatory = $true)][int]$TimeoutSeconds)
    $request = Invoke-OpenClawEndpointRequest -Uri $Uri -TimeoutSeconds $TimeoutSeconds
    $status = [string](Get-OpenClawPropertyValue -InputObject $request.Json -Name 'status')
    $sqliteReady = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'sqliteReady'
    $hostAdapterReachable = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'hostAdapterReachable'
    $isExpected = $request.RequestSucceeded -and $request.HttpStatusCode -eq 200 -and $status -eq 'ready' -and $sqliteReady -eq $true -and $hostAdapterReachable -eq $true
    $summary = if ($isExpected) { 'Expected: core readiness returned HTTP 200 with SQLite ready and HostAdapter reachable.' } else { 'Unexpected: core readiness did not report a fully ready container path.' }
    return Get-OpenClawValidationResult -Name 'Ready' -Uri $Uri `
        -ExpectedCondition 'HTTP 200, JSON status ready, sqliteReady true, and hostAdapterReachable true' `
        -Request $request -IsExpected $isExpected -Summary $summary `
        -Details @{
        status                = $status
        sqliteReady           = $sqliteReady
        hostAdapterReachable  = $hostAdapterReachable
        cacheStale            = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'cacheStale'
        lastSuccessfulPollUtc = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'lastSuccessfulPollUtc'
    }
}

function Invoke-OpenClawStatusEndpointValidation {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][uri]$Uri, [Parameter(Mandatory = $true)][int]$TimeoutSeconds)
    $request = Invoke-OpenClawEndpointRequest -Uri $Uri -TimeoutSeconds $TimeoutSeconds
    $sqliteReady = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'sqliteReady'
    $hostAdapterReachable = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'hostAdapterReachable'
    $cacheItemCounts = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'cacheItemCounts'
    $bridgeFreshness = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'bridgeFreshness'
    $hasStatusShape = $null -ne $cacheItemCounts -and $null -ne $bridgeFreshness
    $isExpected = $request.RequestSucceeded -and $request.HttpStatusCode -eq 200 -and $sqliteReady -eq $true -and $hostAdapterReachable -eq $true -and $hasStatusShape
    $summary = if ($isExpected) { 'Expected: core status returned cache counts, bridge freshness, and reachable dependencies.' } else { 'Unexpected: core status did not report the expected ready dependency and cache diagnostic shape.' }
    return Get-OpenClawValidationResult -Name 'CoreStatus' -Uri $Uri `
        -ExpectedCondition 'HTTP 200, sqliteReady true, hostAdapterReachable true, cacheItemCounts present, and bridgeFreshness present' `
        -Request $request -IsExpected $isExpected -Summary $summary `
        -Details @{
        sqliteReady           = $sqliteReady
        hostAdapterReachable  = $hostAdapterReachable
        lastSuccessfulPollUtc = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'lastSuccessfulPollUtc'
        lastFailedPollUtc     = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'lastFailedPollUtc'
        lastFailureReason     = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'lastFailureReason'
        bridgeObservedAtUtc   = Get-OpenClawPropertyValue -InputObject $request.Json -Name 'bridgeObservedAtUtc'
        cacheItemCounts       = $cacheItemCounts
        bridgeFreshness       = $bridgeFreshness
    }
}

function Invoke-OpenClawAgentDashboardEndpointValidation {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][uri]$Uri, [Parameter(Mandatory = $true)][int]$TimeoutSeconds)
    $request = Invoke-OpenClawEndpointRequest -Uri $Uri -TimeoutSeconds $TimeoutSeconds
    $hasBody = -not [string]::IsNullOrWhiteSpace($request.BodyPreview)
    $isExpected = $request.RequestSucceeded -and $request.HttpStatusCode -eq 200 -and $hasBody
    $summary = if ($isExpected) { 'Expected: agent dashboard root returned HTTP 200 with a response body.' } else { 'Unexpected: agent dashboard root did not return HTTP 200 with a response body.' }
    return Get-OpenClawValidationResult -Name 'AgentDashboard' -Uri $Uri `
        -ExpectedCondition 'HTTP 200 with a non-empty response body' `
        -Request $request -IsExpected $isExpected -Summary $summary `
        -Details @{ hasBody = $hasBody }
}

if ($null -eq $CoreBaseUrl) {
    $CoreBaseUrl = Get-OpenClawCoreBaseUrl -EnvFilePath $EnvFilePath
}

$live = Invoke-OpenClawLiveEndpointValidation -Uri (Get-OpenClawEndpointUri -BaseUri $CoreBaseUrl -Path '/health/live') -TimeoutSeconds $TimeoutSeconds
$ready = Invoke-OpenClawReadyEndpointValidation -Uri (Get-OpenClawEndpointUri -BaseUri $CoreBaseUrl -Path '/health/ready') -TimeoutSeconds $TimeoutSeconds
$coreStatus = Invoke-OpenClawStatusEndpointValidation -Uri (Get-OpenClawEndpointUri -BaseUri $CoreBaseUrl -Path '/api/status') -TimeoutSeconds $TimeoutSeconds
$agentDashboard = Invoke-OpenClawAgentDashboardEndpointValidation -Uri (Get-OpenClawEndpointUri -BaseUri $AgentBaseUrl -Path '/') -TimeoutSeconds $TimeoutSeconds
$agentReadyz = Invoke-OpenClawReadyzProbe -AgentBaseUrl $AgentBaseUrl -TimeoutSeconds $TimeoutSeconds
$tokenPresence = Test-OpenClawGatewayTokenPresence -EnvFilePath $EnvFilePath

$dockerEngine = Invoke-OpenClawDockerEngineValidation -ExecutablePath $DockerPath
$containerDiagnostics = @(
    Invoke-OpenClawContainerValidation -DockerExecutablePath $DockerPath -ContainerName $CoreContainerName -DisplayName 'Core'
    Invoke-OpenClawContainerValidation -DockerExecutablePath $DockerPath -ContainerName $AgentContainerName -DisplayName 'Agent'
)
$hostAdapterProbe = Invoke-OpenClawHostAdapterInContainerProbe -DockerExecutablePath $DockerPath -AgentContainerName $AgentContainerName
$endpointDiagnostics = @($live, $ready, $coreStatus, $agentDashboard, $agentReadyz, $tokenPresence)
$supportingDiagnostics = @($dockerEngine) + $containerDiagnostics + @($hostAdapterProbe) + $endpointDiagnostics
$isExpected = -not [bool]($supportingDiagnostics | Where-Object { -not $_.IsExpected })
$result = [pscustomobject]@{
    OverallResult          = if ($isExpected) { 'Expected' } else { 'Unexpected' }
    IsExpected             = $isExpected
    CheckedAtUtc           = (Get-Date).ToUniversalTime().ToString('o')
    CoreBaseUrl            = [string]$CoreBaseUrl
    AgentBaseUrl           = [string]$AgentBaseUrl
    CoreContainerName      = $CoreContainerName
    AgentContainerName     = $AgentContainerName
    EnvFilePath            = $EnvFilePath
    DockerEngine           = $dockerEngine
    ContainerDiagnostics   = $containerDiagnostics
    EndpointDiagnostics    = $endpointDiagnostics
    Live                   = $live
    Ready                  = $ready
    CoreStatus             = $coreStatus
    AgentDashboard         = $agentDashboard
    AgentReadyz            = $agentReadyz
    HostAdapterInContainer = $hostAdapterProbe
    GatewayTokenPresence   = $tokenPresence
    SupportingDiagnostics  = $supportingDiagnostics
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 8
}
elseif ($PassThru) {
    $result
}
else {
    Write-Output "OverallResult: $($result.OverallResult)"
    Write-Output "IsExpected: $($result.IsExpected)"
    Write-Output "CheckedAtUtc: $($result.CheckedAtUtc)"
    $result.SupportingDiagnostics |
        Select-Object Category, Name, IsExpected, HttpStatusCode, Summary |
            Format-Table -AutoSize
}

