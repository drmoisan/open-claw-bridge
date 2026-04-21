#Requires -Version 7
<#
.SYNOPSIS
Runs the OpenClaw upstream onboarding command once and writes the generated
`OPENCLAW_GATEWAY_TOKEN` to the repository-root `.env`.

.DESCRIPTION
Wraps `docker compose run --rm --no-deps --entrypoint node openclaw-agent
dist/index.js onboard ...` with the argument set from docs.openclaw.ai/install
/docker. Parses the `OPENCLAW_GATEWAY_TOKEN=<value>` line from stdout and
writes it to the target `.env`. Idempotent: if the `.env` already contains a
non-empty token, returns `0` without re-running onboard unless `-Force` is
supplied.

.PARAMETER AnthropicApiKey
SecureString containing the Anthropic API key. When absent, prompts the
operator via `Read-Host -AsSecureString`.

.PARAMETER EnvFilePath
Target `.env` path. Default: `./.env` in the current working directory.

.PARAMETER ComposeFiles
Compose file paths forwarded to `docker compose`. Defaults to the project
compose files.

.PARAMETER DockerPath
Path or command name for the docker CLI. Default: `docker`. Tests inject a
fake docker via this parameter.

.PARAMETER OnboardBinaryPath
Relative path inside the `openclaw-agent` image to the upstream onboarding
binary. Default: `dist/index.js`. Operators can override this when an
upstream release renames or relocates the entry-point binary.

.PARAMETER Force
When set, overwrites an existing non-empty `OPENCLAW_GATEWAY_TOKEN` in the
target `.env`. Otherwise, the script exits `0` with a verbose message.

.NOTES
Fails fast with distinct error categories for:
 - Missing Docker CLI (invariant violation before any side effect).
 - Non-zero exit from the upstream onboard command.
 - Malformed onboard output (no OPENCLAW_GATEWAY_TOKEN= line).
 - Unable to write the target `.env`.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [SecureString]$AnthropicApiKey,

    [Parameter(Mandatory = $false)]
    [string]$EnvFilePath = './.env',

    [Parameter(Mandatory = $false)]
    [string[]]$ComposeFiles = @('./docker-compose.yml', './docker-compose.dev.yml'),

    [Parameter(Mandatory = $false)]
    [string]$DockerPath = 'docker',

    [Parameter(Mandatory = $false)]
    [string]$OnboardBinaryPath = 'dist/index.js',

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Helpers are defined inline below. The file stays well under the 500-line
# cap in `.claude/rules/general-code-change.md`, so no module extraction is
# required. If a future change pushes the file over the cap, extract these
# helpers into scripts/powershell/modules/OpenClawOnboarding/.

function Assert-OpenClawDockerAvailable {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$DockerPath)

    $resolved = Get-Command -Name $DockerPath -ErrorAction SilentlyContinue
    if (-not $resolved) {
        throw "Docker CLI not found at '$DockerPath'. Install Docker Desktop or supply an explicit -DockerPath value."
    }
}

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

function ConvertFrom-OpenClawSecureString {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory = $true)][SecureString]$SecureValue)

    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Invoke-OpenClawOnboardCommand {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory = $true)][string]$DockerPath,
        [Parameter(Mandatory = $true)][string[]]$ComposeFiles,
        [Parameter(Mandatory = $true)][string]$AnthropicApiKeyPlaintext,
        [Parameter(Mandatory = $true)][string]$OnboardBinaryPath
    )

    $composeArgs = @('compose')
    foreach ($file in $ComposeFiles) {
        if (-not [string]::IsNullOrWhiteSpace($file)) {
            $composeArgs += @('--file', $file)
        }
    }
    $composeArgs += @(
        'run', '--rm', '--no-deps',
        '--entrypoint', 'node',
        'openclaw-agent', $OnboardBinaryPath,
        'onboard',
        '--mode', 'local',
        '--no-install-daemon',
        '--non-interactive',
        '--auth-choice', 'apiKey',
        '--anthropic-api-key', $AnthropicApiKeyPlaintext,
        '--secret-input-mode', 'plaintext',
        '--gateway-port', '18789',
        '--gateway-bind', 'loopback',
        '--skip-skills'
    )

    $global:LASTEXITCODE = 0
    $output = @(& $DockerPath @composeArgs 2>&1)
    $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
    if ($exitCode -ne 0) {
        $joined = ($output -join [Environment]::NewLine)
        throw "Upstream onboard command exited with code $exitCode. Output: $joined"
    }
    return $output
}

function Get-OpenClawTokenFromOnboardOutput {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][object[]]$Output)

    foreach ($line in $Output) {
        $text = [string]$line
        $match = [regex]::Match($text, 'OPENCLAW_GATEWAY_TOKEN\s*=\s*(?<token>\S+)')
        if ($match.Success) {
            return $match.Groups['token'].Value
        }
    }
    throw "Onboard output did not contain an OPENCLAW_GATEWAY_TOKEN= line. Output appears malformed; the upstream onboarding contract may have changed."
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
        } else {
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

Assert-OpenClawDockerAvailable -DockerPath $DockerPath

$existing = Get-OpenClawEnvEntryMap -EnvFilePath $EnvFilePath
if ($existing.ContainsKey('OPENCLAW_GATEWAY_TOKEN') -and -not [string]::IsNullOrWhiteSpace($existing['OPENCLAW_GATEWAY_TOKEN']) -and -not $Force) {
    Write-Verbose "OPENCLAW_GATEWAY_TOKEN already present in '$EnvFilePath'. Use -Force to overwrite."
    return
}

if (-not $AnthropicApiKey) {
    $AnthropicApiKey = Read-Host -Prompt 'Enter Anthropic API key' -AsSecureString
}
$apiKeyPlain = ConvertFrom-OpenClawSecureString -SecureValue $AnthropicApiKey
try {
    $output = Invoke-OpenClawOnboardCommand -DockerPath $DockerPath -ComposeFiles $ComposeFiles -AnthropicApiKeyPlaintext $apiKeyPlain -OnboardBinaryPath $OnboardBinaryPath
    $token = Get-OpenClawTokenFromOnboardOutput -Output $output
    Set-OpenClawEnvEntry -EnvFilePath $EnvFilePath -Key 'OPENCLAW_GATEWAY_TOKEN' -Value $token
    Write-Verbose "Wrote OPENCLAW_GATEWAY_TOKEN to '$EnvFilePath'."
} finally {
    # Overwrite the plaintext key reference eagerly even though .NET will GC it.
    $apiKeyPlain = $null
}
