#Requires -Version 7
<#
.SYNOPSIS
Rotate (reissue) the HostAdapter device (bearer) token: write a new
cryptographically-generated secret to the host token file, then restart the
consuming containers so every end reads the same value.

.DESCRIPTION
The HostAdapter device token is a host file bind-mounted read-only into the
`openclaw-core` and `openclaw-agent` containers. Rotation:

  1. Resolves the host token file from `-TokenFilePath`, or from
     `HOSTADAPTER_TOKEN_FILE` in the target `.env` (parsed via the shared
     `Get-OpenClawEnvFileMap` seam).
  2. Fails with a runbook-directed error when the host token file is absent
     (provisioning the initial value is an operator step; no placeholder is
     created).
  3. Is idempotent: an already-present, non-empty token is left untouched unless
     `-Force` is supplied.
  4. Generates a new base64url secret with a cryptographic RNG
     (`System.Security.Cryptography.RandomNumberGenerator`) and writes it to the
     host token file BEFORE restarting any consumer. The old token is invalidated
     implicitly by overwrite once every consumer has restarted.
  5. Restarts `-CoreContainerName` then `-AgentContainerName` through the
     `Invoke-OpenClawDockerCommand` wrapper seam (never a direct `docker` call).

Every state-changing action (file write, each restart) is gated by
`ShouldProcess`. The device-token value is never written to any output, verbose,
debug, warning, or information stream. This script does not generate the gateway
token and does not modify `scripts/Invoke-OpenClawAgentOnboarding.ps1`.

.PARAMETER EnvFilePath
Target `.env` path used to resolve `HOSTADAPTER_TOKEN_FILE` when `-TokenFilePath`
is not supplied. Default: `./.env`.

.PARAMETER TokenFilePath
Explicit host token-file path. When supplied, the `.env` is not consulted.

.PARAMETER DockerExecutablePath
Path to the docker executable passed to the wrapper seam. Default: `docker`.

.PARAMETER TokenByteLength
Number of random bytes sampled from the system RNG for the new secret. The result
is base64url-encoded. Default: 48.

.PARAMETER CoreContainerName
Core container to restart. Default: `openclaw-core`.

.PARAMETER AgentContainerName
Agent container to restart. Default: `openclaw-agent`.

.PARAMETER Force
Rotate even when the host token file already contains a non-empty value.

.NOTES
Where HostAdapter runs interactively (`dotnet run`), restarting it is an operator
step covered by the runbook. This script restarts the container consumers only.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$EnvFilePath = './.env',

    [Parameter(Mandatory = $false)]
    [string]$TokenFilePath,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$DockerExecutablePath = 'docker',

    [Parameter(Mandatory = $false)]
    [ValidateRange(24, 128)]
    [int]$TokenByteLength = 48,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$CoreContainerName = 'openclaw-core',

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$AgentContainerName = 'openclaw-agent',

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$moduleManifest = Join-Path $PSScriptRoot 'powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1'
# Import the helper module only if not already loaded so Pester's Mock -ModuleName
# injections survive during test runs (a -Force re-import would invalidate them).
if (-not (Get-Module -Name OpenClawContainerValidation)) {
    Import-Module -Name $moduleManifest -ErrorAction Stop
}

function New-OpenClawDeviceToken {
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

function Restart-OpenClawConsumer {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)][string]$ContainerName,
        [Parameter(Mandatory = $true)][string]$DockerPath
    )

    if (-not $PSCmdlet.ShouldProcess($ContainerName, 'Restart container')) {
        return
    }

    $result = Invoke-OpenClawDockerCommand -ExecutablePath $DockerPath -CommandArguments @('restart', $ContainerName)
    if ($null -eq $result -or -not $result.Succeeded) {
        $detail = if ($result -and $result.ErrorMessage) { $result.ErrorMessage } else { "exit code $($result.ExitCode)" }
        throw "Failed to restart container '$ContainerName' via docker restart ($detail)."
    }
}

# --- Resolve the host token file ------------------------------------------

$resolvedTokenFile = if (-not [string]::IsNullOrWhiteSpace($TokenFilePath)) {
    $TokenFilePath
}
else {
    $envMap = Get-OpenClawEnvFileMap -EnvFilePath $EnvFilePath
    if (-not $envMap.ContainsKey('HOSTADAPTER_TOKEN_FILE') -or [string]::IsNullOrWhiteSpace($envMap['HOSTADAPTER_TOKEN_FILE'])) {
        throw "HOSTADAPTER_TOKEN_FILE is not set in '$EnvFilePath'. Supply -TokenFilePath or set HOSTADAPTER_TOKEN_FILE; see the admin-access runbook in docs/mailbridge-runbook.md."
    }
    [string]$envMap['HOSTADAPTER_TOKEN_FILE']
}

# --- Absent-file guard (no silent placeholder) ----------------------------

if (-not (Test-Path -LiteralPath $resolvedTokenFile)) {
    throw "Host device-token file '$resolvedTokenFile' does not exist. Provisioning the initial device-token value is an operator step; see the admin-access runbook in docs/mailbridge-runbook.md. No placeholder file was created."
}

# --- Idempotency guard -----------------------------------------------------

$existingValue = (@(Get-Content -LiteralPath $resolvedTokenFile) -join "`n").Trim()
if (-not [string]::IsNullOrWhiteSpace($existingValue) -and -not $Force) {
    Write-Verbose "Host device-token file '$resolvedTokenFile' already contains a non-empty value. Use -Force to rotate."
    return
}

# --- Generate + write the new secret (write-first ordering) ---------------

$newSecret = New-OpenClawDeviceToken -ByteLength $TokenByteLength

if ($PSCmdlet.ShouldProcess($resolvedTokenFile, 'Write device token')) {
    try {
        Set-Content -LiteralPath $resolvedTokenFile -Value $newSecret -Encoding UTF8
    }
    catch {
        throw "Failed to write the rotated device token to '$resolvedTokenFile': $($_.Exception.Message)"
    }
}

# --- Restart the consumers -------------------------------------------------

Restart-OpenClawConsumer -ContainerName $CoreContainerName -DockerPath $DockerExecutablePath
Restart-OpenClawConsumer -ContainerName $AgentContainerName -DockerPath $DockerExecutablePath

Write-Verbose "Device-token rotation complete for '$resolvedTokenFile'; restarted '$CoreContainerName' and '$AgentContainerName'."
