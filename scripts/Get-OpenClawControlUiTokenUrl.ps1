#Requires -Version 7
<#
.SYNOPSIS
Compose the OpenClaw Control UI authentication URL that delivers the gateway
token to the operator via the `#token=` URL fragment.

.DESCRIPTION
Reads a target `.env` and returns the Control UI authentication URL of the shape

    http://127.0.0.1:<OPENCLAW_AGENT_PORT>/#token=<OPENCLAW_GATEWAY_TOKEN>

where `<OPENCLAW_AGENT_PORT>` resolves from `OPENCLAW_AGENT_PORT` (default 18789)
and `<OPENCLAW_GATEWAY_TOKEN>` is read verbatim from the same `.env`. The gateway
token is already produced by `scripts/Invoke-OpenClawAgentOnboarding.ps1`; this
script performs delivery only and does not generate any token.

The base64url gateway token (RFC 4648 Section 5, characters `A-Z a-z 0-9 - _`) is
fragment-safe and is embedded verbatim; it is NOT percent-encoded or otherwise
mutated. The returned URL is the delivery artifact and is handed off to the
operator, who opens it in a browser to complete Control UI authentication (a
human-interaction step documented in the runbook).

`.env` parsing reuses the shared `Get-OpenClawEnvFileMap` seam in
`OpenClawContainerValidation.psm1` rather than re-implementing parsing.

.PARAMETER EnvFilePath
Target `.env` path. Default: `./.env` in the current working directory.

.OUTPUTS
System.String. The constructed Control UI `#token=` URL.

.NOTES
Read-only: this function makes no state changes and does not implement
ShouldProcess. The gateway token value is never written to any output, verbose,
debug, warning, or information stream; the only object returned is the URL that
must, by design, carry the token in its fragment.
#>
[CmdletBinding()]
[OutputType([string])]
param(
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$EnvFilePath = './.env'
)

$ErrorActionPreference = 'Stop'

$moduleManifest = Join-Path $PSScriptRoot 'powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1'
# Import the helper module only if not already loaded. In Pester test runs the
# module is pre-imported in BeforeAll so Mock -ModuleName can intercept cmdlets
# inside the module's scope; re-importing with -Force would invalidate those mocks.
if (-not (Get-Module -Name OpenClawContainerValidation)) {
    Import-Module -Name $moduleManifest -ErrorAction Stop
}

$envMap = Get-OpenClawEnvFileMap -EnvFilePath $EnvFilePath

$port = if ($envMap.ContainsKey('OPENCLAW_AGENT_PORT') -and -not [string]::IsNullOrWhiteSpace($envMap['OPENCLAW_AGENT_PORT'])) {
    $envMap['OPENCLAW_AGENT_PORT']
}
else {
    '18789'
}

$hasToken = $envMap.ContainsKey('OPENCLAW_GATEWAY_TOKEN')
$token = if ($hasToken) { [string]$envMap['OPENCLAW_GATEWAY_TOKEN'] } else { '' }

if (-not $hasToken -or [string]::IsNullOrWhiteSpace($token)) {
    throw "OPENCLAW_GATEWAY_TOKEN is missing or empty in '$EnvFilePath'. Generate it first by running scripts/Invoke-OpenClawAgentOnboarding.ps1, then retry."
}

# The base64url token is fragment-safe; embed it verbatim (no percent-encoding).
return "http://127.0.0.1:$port/#token=$token"
