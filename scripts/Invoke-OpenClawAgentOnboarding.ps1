#Requires -Version 7
<#
.SYNOPSIS
Generates a cryptographically strong OPENCLAW_GATEWAY_TOKEN and writes it to
the target `.env` file.

.DESCRIPTION
The baked `openclaw.json` seed in the `openclaw-agent` container image already
references `${OPENCLAW_GATEWAY_TOKEN}` as the gateway auth token via SecretRef
(see `deploy/docker/openclaw-assistant/openclaw.json`). Compose passes the
value from the project `.env` into the container at runtime. This script's
only job is to produce that secret on the operator host and persist it in the
target `.env`.

Idempotent: if the target `.env` already contains a non-empty
`OPENCLAW_GATEWAY_TOKEN`, the script returns without changes unless `-Force`
is supplied.

.PARAMETER EnvFilePath
Target `.env` path. Default: `./.env` in the current working directory.

.PARAMETER TokenByteLength
Number of random bytes sampled from the system RNG. The resulting token is
base64url-encoded (RFC 4648 Section 5) so it is safe to embed verbatim in a
shell-quoted `.env` value. Default: 48 bytes (~64 characters of output).

.PARAMETER Force
When set, overwrites an existing non-empty `OPENCLAW_GATEWAY_TOKEN` in the
target `.env`. Without `-Force`, an already-populated token is preserved and
the script exits `0`.

.NOTES
The `ANTHROPIC_API_KEY` is NOT the gateway token. Operators supply the
Anthropic key separately in `secrets/.env.anthropic` (see the runbook).
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [string]$EnvFilePath = './.env',

    [Parameter(Mandatory = $false)]
    [ValidateRange(24, 128)]
    [int]$TokenByteLength = 48,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Get-OpenClawEnvEntryMap {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param([Parameter(Mandatory = $true)][string]$EnvFilePath)

    if (-not (Test-Path -LiteralPath $EnvFilePath)) {
        return @{}
    }

    $lines = @(Get-Content -LiteralPath $EnvFilePath)
    $map = @{}
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

function New-OpenClawGatewayToken {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Pure function: samples random bytes from the system RNG with no filesystem, network, or process side effects.'
    )]
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateRange(24, 128)]
        [int]$ByteLength
    )

    $bytes = [byte[]]::new($ByteLength)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    # Base64url (RFC 4648 Section 5): replace '+' and '/', strip padding.
    $base64 = [Convert]::ToBase64String($bytes)
    return ($base64 -replace '\+', '-' -replace '/', '_' -replace '=', '')
}

function Set-OpenClawEnvEntry {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)][string]$EnvFilePath,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $lines = @()
    if (Test-Path -LiteralPath $EnvFilePath) {
        $lines = @(Get-Content -LiteralPath $EnvFilePath)
    }

    $pattern = "^\s*$([regex]::Escape($Key))\s*="
    $replacement = "$Key=$Value"
    $replaced = $false
    $updated = foreach ($line in $lines) {
        if ($line -match $pattern) {
            $replaced = $true
            $replacement
        }
        else {
            $line
        }
    }

    if (-not $replaced) {
        $updated = @($updated) + @($replacement)
    }

    if ($PSCmdlet.ShouldProcess($EnvFilePath, "Write $Key")) {
        Set-Content -LiteralPath $EnvFilePath -Value $updated -Encoding UTF8
    }
}

# --- Main flow -------------------------------------------------------------

$existing = Get-OpenClawEnvEntryMap -EnvFilePath $EnvFilePath
if ($existing.ContainsKey('OPENCLAW_GATEWAY_TOKEN') -and -not [string]::IsNullOrWhiteSpace($existing['OPENCLAW_GATEWAY_TOKEN']) -and -not $Force) {
    Write-Verbose "OPENCLAW_GATEWAY_TOKEN already present in '$EnvFilePath'. Use -Force to overwrite."
    return
}

$token = New-OpenClawGatewayToken -ByteLength $TokenByteLength
Set-OpenClawEnvEntry -EnvFilePath $EnvFilePath -Key 'OPENCLAW_GATEWAY_TOKEN' -Value $token
Write-Verbose "Wrote OPENCLAW_GATEWAY_TOKEN to '$EnvFilePath'."
