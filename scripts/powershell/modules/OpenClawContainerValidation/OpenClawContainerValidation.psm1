#Requires -Version 7
<#
.SYNOPSIS
Shared helpers for scripts/Invoke-OpenClawContainerPathValidation.ps1.

.DESCRIPTION
Holds the docker CLI wrapper, HTTP probe wrapper, URL composition helper,
JSON parsing helper, and `.env` parsing helper previously inlined in the
validation script. Extracted here to keep the validation script under the
500-line cap per `.claude/rules/general-code-change.md` while the script
grows new probes.

Functions are exported individually so the script can dot-source them
through `Import-Module`.
#>

$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Combine a base URI and a relative path into a new URI.
#>
function Get-OpenClawEndpointUri {
    [CmdletBinding()]
    [OutputType([uri])]
    param(
        [Parameter(Mandatory = $true)][uri]$BaseUri,
        [Parameter(Mandatory = $true)][string]$Path
    )
    $builder = [UriBuilder]::new($BaseUri)
    $basePath = $builder.Path.TrimEnd('/')
    $relativePath = $Path.TrimStart('/')
    $builder.Path = if ([string]::IsNullOrWhiteSpace($basePath)) {
        $relativePath
    }
    else {
        '{0}/{1}' -f $basePath, $relativePath
    }
    return $builder.Uri
}

<#
.SYNOPSIS
Return the named property of an object, or `$null` if the object or property is missing.
#>
function Get-OpenClawPropertyValue {
    [CmdletBinding()]
    param(
        [object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )
    if ($null -eq $InputObject) { return $null }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($property) { return $property.Value }
    return $null
}

<#
.SYNOPSIS
Produce a single-line preview of response content, truncated to MaxLength characters.
#>
function Get-OpenClawContentPreview {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$Content,
        [int]$MaxLength = 500
    )
    if ([string]::IsNullOrWhiteSpace($Content)) { return '' }
    $normalized = $Content -replace '\s+', ' '
    if ($normalized.Length -le $MaxLength) { return $normalized }
    return $normalized.Substring(0, $MaxLength)
}

<#
.SYNOPSIS
Parse JSON content into a PowerShell object, returning `$null` on any parse error.
#>
function ConvertFrom-OpenClawJsonContent {
    [CmdletBinding()]
    param([string]$Content)
    if ([string]::IsNullOrWhiteSpace($Content)) { return $null }
    try {
        return $Content | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return $null
    }
}

<#
.SYNOPSIS
Invoke an HTTP request against the given URI and normalize the response into a
PSCustomObject with `RequestSucceeded`, `HttpStatusCode`, `ContentType`, `Json`,
`BodyPreview`, and `ErrorMessage` fields.
#>
function Invoke-OpenClawEndpointRequest {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][uri]$Uri,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds,
        [string]$Method = 'Get',
        [hashtable]$Headers,
        [string]$Body
    )
    try {
        $splat = @{
            Uri                = $Uri
            Method             = $Method
            TimeoutSec         = $TimeoutSeconds
            UseBasicParsing    = $true
            SkipHttpErrorCheck = $true
        }
        if ($Headers) { $splat['Headers'] = $Headers }
        if ($Body) { $splat['Body'] = $Body }
        $response = Invoke-WebRequest @splat
        $content = [string]$response.Content
        $contentType = if ($response.Headers.ContainsKey('Content-Type')) {
            [string]($response.Headers['Content-Type'] -join '; ')
        }
        else { '' }
        return [pscustomobject]@{
            RequestSucceeded = $true
            HttpStatusCode   = [int]$response.StatusCode
            ContentType      = $contentType
            Json             = ConvertFrom-OpenClawJsonContent -Content $content
            BodyPreview      = Get-OpenClawContentPreview -Content $content
            ErrorMessage     = $null
        }
    }
    catch {
        return [pscustomobject]@{
            RequestSucceeded = $false
            HttpStatusCode   = $null
            ContentType      = ''
            Json             = $null
            BodyPreview      = ''
            ErrorMessage     = $_.Exception.Message
        }
    }
}

<#
.SYNOPSIS
Construct a uniform probe-result PSCustomObject for aggregation into
`SupportingDiagnostics` by the validation script.
#>
function Get-OpenClawValidationResult {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Category = 'Endpoint',
        [uri]$Uri,
        [string]$Target,
        [Parameter(Mandatory = $true)][string]$ExpectedCondition,
        [pscustomobject]$Request,
        [Parameter(Mandatory = $true)][bool]$IsExpected,
        [Parameter(Mandatory = $true)][string]$Summary,
        [hashtable]$Details = @{}
    )
    return [pscustomobject]@{
        Category          = $Category
        Name              = $Name
        Target            = if (-not [string]::IsNullOrWhiteSpace($Target)) { $Target } elseif ($Uri) { [string]$Uri } else { '' }
        Uri               = [string]$Uri
        IsExpected        = $IsExpected
        HttpStatusCode    = if ($Request) { $Request.HttpStatusCode } else { $null }
        ExpectedCondition = $ExpectedCondition
        Summary           = $Summary
        ContentType       = if ($Request) { $Request.ContentType } else { '' }
        Details           = [pscustomobject]$Details
        ErrorMessage      = if ($Request) { $Request.ErrorMessage } else { $null }
        BodyPreview       = if ($Request) { $Request.BodyPreview } else { '' }
    }
}

<#
.SYNOPSIS
Invoke an external docker CLI command with the given arguments, capturing
stdout + stderr, exit code, and any launch error into a PSCustomObject.
#>
function Invoke-OpenClawDockerCommand {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string[]]$CommandArguments
    )
    try {
        $output = @(& $ExecutablePath @CommandArguments 2>&1)
        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
        return [pscustomobject]@{
            Succeeded    = $exitCode -eq 0
            ExitCode     = $exitCode
            Output       = $output
            ErrorMessage = $null
        }
    }
    catch {
        return [pscustomobject]@{
            Succeeded    = $false
            ExitCode     = $null
            Output       = @()
            ErrorMessage = $_.Exception.Message
        }
    }
}

<#
.SYNOPSIS
Parse a `.env` file into a hashtable of KEY->VALUE entries. Blank lines and
comment lines are ignored. Missing files produce an empty hashtable.
#>
function Get-OpenClawEnvFileMap {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param([Parameter(Mandatory = $true)][string]$EnvFilePath)
    $map = @{}
    if (-not (Test-Path -LiteralPath $EnvFilePath)) { return $map }
    $lines = @(Get-Content -LiteralPath $EnvFilePath)
    foreach ($line in $lines) {
        if ($null -eq $line) { continue }
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0) { continue }
        if ($trimmed.StartsWith('#')) { continue }
        $equalsIndex = $trimmed.IndexOf('=')
        if ($equalsIndex -lt 1) { continue }
        $key = $trimmed.Substring(0, $equalsIndex).Trim()
        $value = $trimmed.Substring($equalsIndex + 1).Trim()
        $map[$key] = $value
    }
    return $map
}

<#
.SYNOPSIS
Compose the version-neutral deployed operator `.env` path from a LocalAppData base (pure; returns `$null` for a null/whitespace base).
#>
function Get-OpenClawOperatorEnvFilePath {
    [CmdletBinding()]
    [OutputType([string])]
    param([string]$LocalAppDataPath = $env:LOCALAPPDATA)
    if ([string]::IsNullOrWhiteSpace($LocalAppDataPath)) { return $null }
    return Join-Path $LocalAppDataPath 'OpenClaw/operator-config/.env'
}

<#
.SYNOPSIS
Resolve the default `.env` path: fallback without probing when `-OperatorEnvFilePath` is null/whitespace, else the operator path if it exists on disk, else the fallback.
#>
function Resolve-OpenClawDefaultEnvFilePath {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$OperatorEnvFilePath,
        [Parameter(Mandatory = $true)][string]$FallbackEnvFilePath
    )
    if ([string]::IsNullOrWhiteSpace($OperatorEnvFilePath)) { return $FallbackEnvFilePath }
    if (Test-Path -LiteralPath $OperatorEnvFilePath) { return $OperatorEnvFilePath }
    return $FallbackEnvFilePath
}

<#
.SYNOPSIS
Probe GET `{AgentBaseUrl}/readyz` and return a probe result named `AgentReadyz`.
#>
function Invoke-OpenClawReadyzProbe {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][uri]$AgentBaseUrl,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )
    $uri = Get-OpenClawEndpointUri -BaseUri $AgentBaseUrl -Path '/readyz'
    $request = Invoke-OpenClawEndpointRequest -Uri $uri -TimeoutSeconds $TimeoutSeconds
    $isExpected = $request.RequestSucceeded -and $request.HttpStatusCode -eq 200
    $summary = if ($isExpected) {
        'Expected: agent readyz returned HTTP 200.'
    }
    elseif (-not $request.RequestSucceeded) {
        "Unexpected: agent readyz unreachable ($($request.ErrorMessage))."
    }
    else {
        "Unexpected: agent readyz returned HTTP $($request.HttpStatusCode)."
    }
    return Get-OpenClawValidationResult `
        -Name 'AgentReadyz' `
        -Uri $uri `
        -ExpectedCondition 'Agent /readyz returns HTTP 200' `
        -Request $request `
        -IsExpected $isExpected `
        -Summary $summary `
        -Details @{ statusCode = $request.HttpStatusCode }
}

<#
.SYNOPSIS
Probe the in-container HostAdapter reachability by running `docker compose exec`
against the agent container. Returns a probe result named `HostAdapterInContainer`.
#>
function Invoke-OpenClawHostAdapterInContainerProbe {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][string]$DockerExecutablePath,
        [Parameter(Mandatory = $true)][string]$AgentContainerName
    )
    $shellCommand = 'TOKEN=$(tr -d "\r\n" < /run/openclaw/hostadapter.token); curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" http://host.docker.internal:4319/status'
    $command = Invoke-OpenClawDockerCommand -ExecutablePath $DockerExecutablePath -CommandArguments @(
        'compose', 'exec', '-T', $AgentContainerName, 'sh', '-c', $shellCommand
    )
    $output = ([string](@($command.Output) -join ' ')).Trim()
    $code = 0
    if (-not [int]::TryParse($output, [ref]$code)) { $code = 0 }
    $isExpected = $command.Succeeded -and $code -eq 200
    $summary = if ($isExpected) {
        'Expected: in-container HostAdapter probe returned HTTP 200.'
    }
    elseif (-not $command.Succeeded) {
        "Unexpected: docker exec failed with exit code $($command.ExitCode)."
    }
    else {
        "Unexpected: in-container HostAdapter returned HTTP $code."
    }
    return Get-OpenClawValidationResult `
        -Category 'Container' `
        -Name 'HostAdapterInContainer' `
        -Target $AgentContainerName `
        -ExpectedCondition 'docker compose exec against the agent container reports HTTP 200 for /status with bearer token' `
        -IsExpected $isExpected `
        -Summary $summary `
        -Details @{
        dockerExitCode = $command.ExitCode
        httpStatusCode = $code
        rawOutput      = $output
    }
}

<#
.SYNOPSIS
Read the operator `.env` and confirm `OPENCLAW_GATEWAY_TOKEN` is present and
non-empty. Returns a probe result named `GatewayTokenPresence`.
#>
function Test-OpenClawGatewayTokenPresence {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param([Parameter(Mandatory = $true)][string]$EnvFilePath)
    $map = Get-OpenClawEnvFileMap -EnvFilePath $EnvFilePath
    $hasKey = $map.ContainsKey('OPENCLAW_GATEWAY_TOKEN')
    $value = if ($hasKey) { [string]$map['OPENCLAW_GATEWAY_TOKEN'] } else { '' }
    $isExpected = $hasKey -and -not [string]::IsNullOrWhiteSpace($value)
    $summary = if ($isExpected) {
        "Expected: OPENCLAW_GATEWAY_TOKEN is present in '$EnvFilePath'."
    }
    elseif (-not $hasKey) {
        "Unexpected: OPENCLAW_GATEWAY_TOKEN is missing from '$EnvFilePath'. Run scripts/Invoke-OpenClawAgentOnboarding.ps1."
    }
    else {
        "Unexpected: OPENCLAW_GATEWAY_TOKEN is present but empty in '$EnvFilePath'."
    }
    return Get-OpenClawValidationResult `
        -Category 'Configuration' `
        -Name 'GatewayTokenPresence' `
        -Target $EnvFilePath `
        -ExpectedCondition 'OPENCLAW_GATEWAY_TOKEN is present and non-empty in the target .env' `
        -IsExpected $isExpected `
        -Summary $summary `
        -Details @{
        envFilePath    = $EnvFilePath
        tokenKeyExists = $hasKey
        tokenIsEmpty   = ($hasKey -and [string]::IsNullOrWhiteSpace($value))
    }
}

<#
.SYNOPSIS
Confirm OPENCLAW_GATEWAY_TOKEN is present/non-empty inside the running agent container via `docker compose exec` (distinct from the `.env`-file presence check); returns a `GatewayTokenInContainer` probe result.
#>
function Test-OpenClawGatewayTokenInContainer {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][string]$DockerExecutablePath,
        [Parameter(Mandatory = $true)][string]$AgentContainerName
    )
    $shellCommand = 'if [ -n "$OPENCLAW_GATEWAY_TOKEN" ]; then echo present; else echo absent; fi'
    $command = Invoke-OpenClawDockerCommand -ExecutablePath $DockerExecutablePath -CommandArguments @(
        'compose', 'exec', '-T', $AgentContainerName, 'sh', '-c', $shellCommand
    )
    $output = ([string](@($command.Output) -join ' ')).Trim()
    $isExpected = $command.Succeeded -and $output -eq 'present'
    $summary = if ($isExpected) {
        'Expected: OPENCLAW_GATEWAY_TOKEN is present and non-empty inside the agent container.'
    }
    elseif (-not $command.Succeeded) {
        "Unexpected: docker exec failed with exit code $($command.ExitCode) while probing the in-container gateway token."
    }
    else {
        'Unexpected: OPENCLAW_GATEWAY_TOKEN is missing or empty inside the agent container.'
    }
    return Get-OpenClawValidationResult `
        -Category 'Container' `
        -Name 'GatewayTokenInContainer' `
        -Target $AgentContainerName `
        -ExpectedCondition 'OPENCLAW_GATEWAY_TOKEN is present and non-empty inside the running agent container (checked via docker exec), distinct from the .env-file presence check' `
        -IsExpected $isExpected `
        -Summary $summary `
        -Details @{
        dockerExitCode = $command.ExitCode
        probeResult    = $output
    }
}

<#
.SYNOPSIS
Split an `<repo>:<tag>` image reference on the last `:`, returning `Tag = ''` (not a throw) when no `:` is present.
#>
function ConvertFrom-OpenClawImageReference {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param([Parameter(Mandatory = $true)][string]$ImageReference)
    $splitIndex = $ImageReference.LastIndexOf(':')
    if ($splitIndex -lt 0) {
        return [pscustomobject]@{ Repository = $ImageReference; Tag = '' }
    }
    return [pscustomobject]@{
        Repository = $ImageReference.Substring(0, $splitIndex)
        Tag        = $ImageReference.Substring($splitIndex + 1)
    }
}

<#
.SYNOPSIS
Compare the Control UI (`openclaw/core`) and gateway (`openclaw/agent`) image tags against `-ExpectedVersion`; returns a `Get-OpenClawValidationResult`-shaped object.
#>
function Test-OpenClawImageVersionAligned {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][string]$CoreImageReference,
        [Parameter(Mandatory = $true)][string]$AgentImageReference,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )
    $core = ConvertFrom-OpenClawImageReference -ImageReference $CoreImageReference
    $agent = ConvertFrom-OpenClawImageReference -ImageReference $AgentImageReference
    $parsedVersion = $null
    $coreWellFormed = [System.Version]::TryParse($core.Tag, [ref]$parsedVersion)
    $agentWellFormed = [System.Version]::TryParse($agent.Tag, [ref]$parsedVersion)
    $coreMatches = $coreWellFormed -and ($core.Tag -eq $ExpectedVersion)
    $agentMatches = $agentWellFormed -and ($agent.Tag -eq $ExpectedVersion)
    $isExpected = $coreMatches -and $agentMatches
    $summary = if ($isExpected) {
        "Expected: both '$CoreImageReference' and '$AgentImageReference' are pinned to version '$ExpectedVersion'."
    }
    elseif (-not $coreWellFormed -or -not $agentWellFormed) {
        "Unexpected: malformed image tag(s) - core tag '$($core.Tag)' well-formed=$coreWellFormed, agent tag '$($agent.Tag)' well-formed=$agentWellFormed; expected 4-part version '$ExpectedVersion'."
    }
    else {
        "Unexpected: version mismatch - core tag '$($core.Tag)', agent tag '$($agent.Tag)', expected version '$ExpectedVersion'."
    }
    return Get-OpenClawValidationResult `
        -Category 'Container' `
        -Name 'ImageVersionAlignment' `
        -ExpectedCondition "Both core and agent image tags equal '$ExpectedVersion' and are well-formed 4-part versions" `
        -IsExpected $isExpected `
        -Summary $summary `
        -Details @{
        coreImageReference  = $CoreImageReference
        agentImageReference = $AgentImageReference
        coreTag             = $core.Tag
        agentTag            = $agent.Tag
        expectedVersion     = $ExpectedVersion
    }
}

Export-ModuleMember -Function @(
    'Get-OpenClawEndpointUri',
    'Get-OpenClawPropertyValue',
    'Get-OpenClawContentPreview',
    'ConvertFrom-OpenClawJsonContent',
    'Invoke-OpenClawEndpointRequest',
    'Get-OpenClawValidationResult',
    'Invoke-OpenClawDockerCommand',
    'Get-OpenClawEnvFileMap',
    'Get-OpenClawOperatorEnvFilePath',
    'Resolve-OpenClawDefaultEnvFilePath',
    'Invoke-OpenClawReadyzProbe',
    'Invoke-OpenClawHostAdapterInContainerProbe',
    'Test-OpenClawGatewayTokenPresence',
    'Test-OpenClawGatewayTokenInContainer',
    'ConvertFrom-OpenClawImageReference',
    'Test-OpenClawImageVersionAligned'
)
