# Install.Preflight.psm1
# Stage 7 / Stage 8.5 preflight helpers for scripts/Install.ps1.
# Split out from Install.Helpers.psm1 to keep both files under the 500-line
# policy in .claude/rules/general-code-change.md. PowerShell 7+.
# Calls into Install.Helpers wrapper seams (Invoke-HostAdapterStatusRequest,
# Get-InstallEnvFileMap, Get-HostAdapterPreflightUri) so unit tests can mock
# the seams instead of real cmdlets.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'Install.Helpers.psm1') -Force -Global -ErrorAction Stop

function Get-InstallEnvFileMap {
    <#
    .SYNOPSIS
        Reads a Docker .env file at $EnvFilePath and returns a hashtable of key/value
        pairs. Empty lines, comment lines (#), and lines without `=` are skipped.
        Quoted values have surrounding " or ' stripped.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$EnvFilePath
    )
    $map = @{}
    if (-not (Test-Path -LiteralPath $EnvFilePath)) { return $map }
    $lines = @(Get-Content -LiteralPath $EnvFilePath)
    foreach ($line in $lines) {
        if ($null -eq $line) { continue }
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }
        $equalsIndex = $trimmed.IndexOf('=')
        if ($equalsIndex -lt 1) { continue }
        $key = $trimmed.Substring(0, $equalsIndex).Trim()
        $value = $trimmed.Substring($equalsIndex + 1).Trim().Trim([char[]]@('"', "'"))
        $map[$key] = $value
    }
    return $map
}

function Get-InstallEndpointUri {
    <#
    .SYNOPSIS
        Builds a child URI under $BaseUri by appending $Path to the base path,
        preserving scheme, host, and port. Used to derive the /v1/status URI.
    #>
    [CmdletBinding()]
    [OutputType([uri])]
    param(
        [Parameter(Mandatory = $true)][uri]$BaseUri,
        [Parameter(Mandatory = $true)][string]$Path
    )
    $builder = [UriBuilder]::new($BaseUri)
    $basePath = $builder.Path.TrimEnd('/')
    $relativePath = $Path.TrimStart('/')
    $builder.Path = if ([string]::IsNullOrWhiteSpace($basePath)) { $relativePath } else { '{0}/{1}' -f $basePath, $relativePath }
    return $builder.Uri
}

function Get-HostAdapterPreflightUri {
    <#
    .SYNOPSIS
        Returns the /v1/status URI for the HostAdapter, derived from
        OpenClaw__HostAdapter__BaseUrl in the supplied env map (defaulting to
        http://host.docker.internal:4319/v1). The host.docker.internal alias is
        rewritten to 127.0.0.1 because the preflight runs on the host.
    #>
    [CmdletBinding()]
    [OutputType([uri])]
    param([hashtable]$EnvMap)
    $baseUrl = 'http://host.docker.internal:4319/v1'
    if ($EnvMap -and $EnvMap.ContainsKey('OpenClaw__HostAdapter__BaseUrl') -and -not [string]::IsNullOrWhiteSpace([string]$EnvMap['OpenClaw__HostAdapter__BaseUrl'])) {
        $baseUrl = [string]$EnvMap['OpenClaw__HostAdapter__BaseUrl']
    }
    try {
        $builder = [UriBuilder]::new([uri]$baseUrl)
    }
    catch {
        throw "OpenClaw__HostAdapter__BaseUrl in the installed docker .env is not a valid URI: '$baseUrl'."
    }
    if ($builder.Host -eq 'host.docker.internal') { $builder.Host = '127.0.0.1' }
    return Get-InstallEndpointUri -BaseUri $builder.Uri -Path '/status'
}

function Format-HostAdapterPreflightFailure {
    <#
    .SYNOPSIS
        Formats the operator-facing preflight failure message. When the body
        parses as JSON with a non-null `error` block whose `code` or `message`
        is a non-empty string, the returned message includes both verbatim.
        Otherwise the function falls back to the legacy HTTP-status-only
        message preserving AC-04.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][uri]$StatusUri,
        [Parameter(Mandatory = $true)][int]$StatusCode,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Body
    )

    $remediation = 'Confirm OpenClaw.HostAdapter is running, the token is valid, and OpenClaw.MailBridge is running, then retry; or pass -SkipDocker to skip the container stage.'
    $errorCode = $null
    $errorMessage = $null

    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        try {
            $parsed = $Body | ConvertFrom-Json -ErrorAction Stop
            if ($null -ne $parsed -and $parsed.PSObject.Properties.Name -contains 'error' -and $null -ne $parsed.error) {
                $errBlock = $parsed.error
                if ($errBlock.PSObject.Properties.Name -contains 'code') {
                    $candidate = [string]$errBlock.code
                    if (-not [string]::IsNullOrWhiteSpace($candidate)) { $errorCode = $candidate }
                }
                if ($errBlock.PSObject.Properties.Name -contains 'message') {
                    $candidate = [string]$errBlock.message
                    if (-not [string]::IsNullOrWhiteSpace($candidate)) { $errorMessage = $candidate }
                }
            }
        }
        catch {
            $errorCode = $null
            $errorMessage = $null
        }
    }

    if ($null -ne $errorCode -or $null -ne $errorMessage) {
        $codePart = if ($null -ne $errorCode) { $errorCode } else { '(missing)' }
        $messagePart = if ($null -ne $errorMessage) { $errorMessage } else { '(missing)' }
        return "HostAdapter preflight failed before starting Docker. GET $StatusUri returned HTTP $StatusCode. error.code=$codePart; error.message=$messagePart. $remediation"
    }

    return "HostAdapter preflight failed before starting Docker. GET $StatusUri returned HTTP $StatusCode. $remediation"
}

function Get-PreflightTokenAndUri {
    # Internal helper: resolves the HostAdapter token file path and the /v1/status URI
    # from the staged docker .env. Throws operator-facing messages identical to the
    # legacy Assert-HostAdapterRuntimePreflight prelude on missing/empty token.
    [CmdletBinding()]
    [OutputType([hashtable])]
    param([Parameter(Mandatory = $true)][string]$DestDockerDir)

    $envFilePath = Join-Path $DestDockerDir '.env'
    $envMap = Get-InstallEnvFileMap -EnvFilePath $envFilePath

    $tokenFilePath = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
    if ($envMap.ContainsKey('HOSTADAPTER_TOKEN_FILE') -and -not [string]::IsNullOrWhiteSpace([string]$envMap['HOSTADAPTER_TOKEN_FILE'])) {
        $tokenFilePath = [Environment]::ExpandEnvironmentVariables([string]$envMap['HOSTADAPTER_TOKEN_FILE'])
    }

    if (-not (Test-Path -LiteralPath $tokenFilePath)) {
        throw "HostAdapter preflight failed before starting Docker. Token file not found at '$tokenFilePath'. Provision the HostAdapter token file, start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
    }

    $token = (Get-Content -LiteralPath $tokenFilePath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "HostAdapter preflight failed before starting Docker. Token file '$tokenFilePath' is empty. Provision a non-empty HostAdapter token, start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
    }

    $statusUri = Get-HostAdapterPreflightUri -EnvMap $envMap
    return @{ Token = $token; StatusUri = $statusUri }
}

function Assert-HostAdapterRespondingPreflight {
    <#
    .SYNOPSIS
        Stage 7 preflight: verifies the HostAdapter is responsive by inspecting
        for `meta.adapterVersion` in the /v1/status envelope. Accepts any well-
        formed HostAdapter envelope regardless of HTTP status, including a
        responsive-but-bridge-down 502 body. Throws via
        Format-HostAdapterPreflightFailure when the body cannot be parsed or
        adapterVersion is missing.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$DestDockerDir)

    $resolved = Get-PreflightTokenAndUri -DestDockerDir $DestDockerDir
    $statusUri = $resolved.StatusUri
    $token = $resolved.Token

    try {
        $response = Invoke-HostAdapterStatusRequest -StatusUri $statusUri -Token $token
    }
    catch {
        throw "HostAdapter preflight failed before starting Docker. GET $statusUri was unreachable: $($_.Exception.Message). Start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
    }

    $statusCode = [int]$response.StatusCode
    $body = [string]$response.Content

    $hasAdapterVersion = $false
    if (-not [string]::IsNullOrWhiteSpace($body)) {
        try {
            $parsed = $body | ConvertFrom-Json -ErrorAction Stop
            if ($null -ne $parsed -and $parsed.PSObject.Properties.Name -contains 'meta' -and $null -ne $parsed.meta) {
                $metaBlock = $parsed.meta
                if ($metaBlock.PSObject.Properties.Name -contains 'adapterVersion') {
                    $candidate = [string]$metaBlock.adapterVersion
                    if (-not [string]::IsNullOrWhiteSpace($candidate)) { $hasAdapterVersion = $true }
                }
            }
        }
        catch {
            $hasAdapterVersion = $false
        }
    }

    if ($hasAdapterVersion) {
        return
    }

    throw (Format-HostAdapterPreflightFailure -StatusUri $statusUri -StatusCode $statusCode -Body $body)
}

function Get-HostAdapterBridgeReadyClassification {
    <#
    .SYNOPSIS
        Internal classifier for the Stage 8.5 bounded-polling loop. Returns one of
        'success', 'retryable', or 'terminal' for a single /v1/status response.
    .DESCRIPTION
        - 'retryable': HTTP 502 with parsed JSON error.code == TRANSPORT_FAILURE,
          OR HTTP 200 with data.state in {starting, waiting_for_outlook}
          (case-insensitive; matches IsBridgeNotReady in
          src/OpenClaw.HostAdapter/Program.cs:444). MailBridge is not yet ready;
          the caller should wait and re-poll.
        - 'success': HTTP 200 with a non-empty data.state not in the not-ready set.
        - 'terminal': anything else (any other non-200 such as 401, or any 200 with
          a body that does not parse as JSON or lacks data.state).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][int]$StatusCode,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Body
    )

    $notReadyStates = @('starting', 'waiting_for_outlook')

    $parsed = $null
    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        try { $parsed = $Body | ConvertFrom-Json -ErrorAction Stop }
        catch { $parsed = $null }
    }

    if ($StatusCode -eq 200) {
        # Success or not-ready-retry both require a parseable data.state.
        if ($null -ne $parsed -and $parsed.PSObject.Properties.Name -contains 'data' -and $null -ne $parsed.data) {
            $dataBlock = $parsed.data
            if ($dataBlock.PSObject.Properties.Name -contains 'state') {
                $stateValue = [string]$dataBlock.state
                if (-not [string]::IsNullOrWhiteSpace($stateValue)) {
                    foreach ($candidate in $notReadyStates) {
                        if ([string]::Equals($stateValue, $candidate, [System.StringComparison]::OrdinalIgnoreCase)) {
                            return 'retryable'
                        }
                    }
                    return 'success'
                }
            }
        }
        # 200 with an unparseable body or no usable data.state is terminal.
        return 'terminal'
    }

    if ($StatusCode -eq 502 -and $null -ne $parsed -and $parsed.PSObject.Properties.Name -contains 'error' -and $null -ne $parsed.error) {
        $errBlock = $parsed.error
        if ($errBlock.PSObject.Properties.Name -contains 'code') {
            $code = [string]$errBlock.code
            if ([string]::Equals($code, 'TRANSPORT_FAILURE', [System.StringComparison]::OrdinalIgnoreCase)) {
                return 'retryable'
            }
        }
    }

    return 'terminal'
}

function Assert-HostAdapterBridgeReadyPreflight {
    <#
    .SYNOPSIS
        Stage 8.5 preflight: verifies the bridge is ready after MSIX install using a
        bounded polling loop. Requires HTTP 200 with a non-empty `data.state` value
        that is neither `starting` nor `waiting_for_outlook` (case-insensitive;
        matches the IsBridgeNotReady contract in
        src/OpenClaw.HostAdapter/Program.cs:444).
    .DESCRIPTION
        Polls Invoke-HostAdapterStatusRequest until the deadline computed from
        $TimeoutSec elapses. Each response is classified (see
        Get-HostAdapterBridgeReadyClassification):
          - success  -> returns.
          - terminal -> throws via Format-HostAdapterPreflightFailure immediately.
          - retryable (HTTP 502 + TRANSPORT_FAILURE, or HTTP 200 +
            state in {starting, waiting_for_outlook}) -> waits PollIntervalSec via
            $DelayProvider and re-polls.
        On timeout exhaustion the last observed status/body are formatted via
        Format-HostAdapterPreflightFailure and thrown. Clock reads go through
        $NowProvider and waits through $DelayProvider so Pester can drive the loop
        deterministically without wall-clock sleeps.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$DestDockerDir,
        [Parameter()][int]$TimeoutSec = 60,
        [Parameter()][int]$PollIntervalSec = 2,
        [Parameter()][scriptblock]$NowProvider = { [datetime]::UtcNow },
        [Parameter()][scriptblock]$DelayProvider = { param([int]$Seconds) Start-Sleep -Seconds $Seconds }
    )

    $resolved = Get-PreflightTokenAndUri -DestDockerDir $DestDockerDir
    $statusUri = $resolved.StatusUri
    $token = $resolved.Token

    $deadline = (& $NowProvider).AddSeconds($TimeoutSec)
    $lastStatusCode = 0
    $lastBody = ''

    while ($true) {
        try {
            $response = Invoke-HostAdapterStatusRequest -StatusUri $statusUri -Token $token
        }
        catch {
            throw "HostAdapter preflight failed before starting Docker. GET $statusUri was unreachable: $($_.Exception.Message). Start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
        }

        $lastStatusCode = [int]$response.StatusCode
        $lastBody = [string]$response.Content

        $classification = Get-HostAdapterBridgeReadyClassification -StatusCode $lastStatusCode -Body $lastBody

        if ($classification -eq 'success') {
            return
        }
        if ($classification -eq 'terminal') {
            throw (Format-HostAdapterPreflightFailure -StatusUri $statusUri -StatusCode $lastStatusCode -Body $lastBody)
        }

        # Retryable: wait PollIntervalSec, then re-check the deadline before polling again.
        & $DelayProvider $PollIntervalSec
        if ((& $NowProvider) -ge $deadline) {
            break
        }
    }

    throw (Format-HostAdapterPreflightFailure -StatusUri $statusUri -StatusCode $lastStatusCode -Body $lastBody)
}

function Invoke-Stage8Point5BridgeReadyOrRollback {
    <#
    .SYNOPSIS
        Stage 8.5 wrapper: invokes Assert-HostAdapterBridgeReadyPreflight and on
        failure calls Invoke-MsixRemove with the captured PackageFullName before
        re-throwing. Preserves the no-orphan invariant from issue #52.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$DestDockerDir,
        [Parameter(Mandatory = $true)][string]$PackageFullName
    )

    try {
        Assert-HostAdapterBridgeReadyPreflight -DestDockerDir $DestDockerDir
    }
    catch {
        $bridgeReadyError = $_.Exception.Message
        try { Invoke-MsixRemove -PackageFullName $PackageFullName }
        catch { Write-Information "[install:hostadapter-bridge-check] msix rollback tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
        throw $bridgeReadyError
    }
}

Export-ModuleMember -Function 'Format-HostAdapterPreflightFailure',
'Assert-HostAdapterRespondingPreflight', 'Assert-HostAdapterBridgeReadyPreflight',
'Invoke-Stage8Point5BridgeReadyOrRollback', 'Get-InstallEnvFileMap',
'Get-InstallEndpointUri', 'Get-HostAdapterPreflightUri'
