#Requires -Version 7
<#
.SYNOPSIS
Provision (or validate) a `web_search` provider entry in the baked OpenClaw agent
configuration seed, referencing the provider API key via a SecretRef-style env
interpolation.

.DESCRIPTION
The `web_search` tool is enabled by the `coding` tools profile, but the underlying
search provider entry and credential reference are not configured in the baked seed
`deploy/docker/openclaw-assistant/openclaw.json`. This script adds a provider entry
at `plugins.entries.<ProviderName>.config.webSearch.apiKey` whose value is the
SecretRef interpolation `${<ApiKeyEnvVar>}` (mirroring how `gateway.auth.token`
references `${OPENCLAW_GATEWAY_TOKEN}`). The provider API key itself is human-held
(external SaaS-issued) and is supplied by the operator into `.env`/secrets; it is
never hard-coded in the config.

The change must be made in the baked seed because the agent entrypoint re-seeds
`/.openclaw/openclaw.json` from the seed on every start; persistence therefore
requires editing the seed and rebuilding the image.

Behavior:
  - Idempotent: when the provider entry already references the SecretRef, the script
    makes no change.
  - Fails explicitly (throws) on invalid input JSON and when the referenced provider
    API-key env var is not set in the environment.
  - Validates that the serialized result re-parses via ConvertFrom-Json before writing.
  - The seed write is gated by ShouldProcess.

.PARAMETER ConfigPath
Path to the baked agent config seed. Default: the repository seed
`deploy/docker/openclaw-assistant/openclaw.json` resolved relative to this script.

.PARAMETER ProviderName
The web_search provider name to provision. Default: `firecrawl` (pinned per the
feature schema-pin; see the feature evidence).

.PARAMETER ApiKeyEnvVar
The environment variable name backing the provider API key SecretRef. Default:
`WEB_SEARCH_API_KEY`.

.NOTES
Supplying the provider API key value is a human-interaction step covered by the
runbook. This script only wires the provider entry and SecretRef reference.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$ConfigPath = (Join-Path -Path $PSScriptRoot -ChildPath '../deploy/docker/openclaw-assistant/openclaw.json'),

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$ProviderName = 'firecrawl',

    [Parameter(Mandatory = $false)]
    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')]
    [string]$ApiKeyEnvVar = 'WEB_SEARCH_API_KEY'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Agent config seed not found at '$ConfigPath'."
}

$raw = (@(Get-Content -LiteralPath $ConfigPath) -join "`n")

try {
    $config = $raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
    throw "Agent config seed '$ConfigPath' is not valid JSON: $($_.Exception.Message)"
}

$providerKeyValue = [System.Environment]::GetEnvironmentVariable($ApiKeyEnvVar)
if ([string]::IsNullOrWhiteSpace($providerKeyValue)) {
    throw "The referenced provider-key environment variable '$ApiKeyEnvVar' is not set. Supply it in .env/secrets (see the admin-access runbook in docs/mailbridge-runbook.md) before provisioning the web_search provider."
}

$secretRef = '${' + $ApiKeyEnvVar + '}'

function Get-OpenClawChildProperty {
    [CmdletBinding()]
    param([object]$InputObject, [Parameter(Mandatory = $true)][string]$Name)
    if ($null -eq $InputObject) { return $null }
    $prop = $InputObject.PSObject.Properties[$Name]
    if ($prop) { return $prop.Value }
    return $null
}

# --- Idempotency: already provisioned with the SecretRef? -----------------

$plugins = Get-OpenClawChildProperty -InputObject $config -Name 'plugins'
$entries = Get-OpenClawChildProperty -InputObject $plugins -Name 'entries'
$providerEntry = Get-OpenClawChildProperty -InputObject $entries -Name $ProviderName
$providerConfig = Get-OpenClawChildProperty -InputObject $providerEntry -Name 'config'
$webSearch = Get-OpenClawChildProperty -InputObject $providerConfig -Name 'webSearch'
$existingApiKey = Get-OpenClawChildProperty -InputObject $webSearch -Name 'apiKey'

if ($existingApiKey -eq $secretRef) {
    Write-Verbose "web_search provider '$ProviderName' is already provisioned with the SecretRef in '$ConfigPath'; no change."
    return
}

# --- Build/attach the provider entry --------------------------------------

if (-not $config.PSObject.Properties['plugins']) {
    $config | Add-Member -NotePropertyName 'plugins' -NotePropertyValue ([pscustomobject]@{ entries = [pscustomobject]@{} })
}
if (-not $config.plugins.PSObject.Properties['entries']) {
    $config.plugins | Add-Member -NotePropertyName 'entries' -NotePropertyValue ([pscustomobject]@{})
}

$newProviderEntry = [pscustomobject]@{
    config = [pscustomobject]@{
        webSearch = [pscustomobject]@{
            apiKey = $secretRef
        }
    }
}

if ($config.plugins.entries.PSObject.Properties[$ProviderName]) {
    $config.plugins.entries.PSObject.Properties.Remove($ProviderName)
}
$config.plugins.entries | Add-Member -NotePropertyName $ProviderName -NotePropertyValue $newProviderEntry

# --- Serialize + validate re-parse ----------------------------------------

$serialized = $config | ConvertTo-Json -Depth 20
try {
    $null = $serialized | ConvertFrom-Json -ErrorAction Stop
}
catch {
    throw "Serialized web_search provisioning produced invalid JSON for '$ConfigPath': $($_.Exception.Message)"
}

# --- Write (gated) ---------------------------------------------------------

if ($PSCmdlet.ShouldProcess($ConfigPath, "Provision web_search provider '$ProviderName'")) {
    Set-Content -LiteralPath $ConfigPath -Value $serialized -Encoding UTF8
    Write-Verbose "Provisioned web_search provider '$ProviderName' with SecretRef in '$ConfigPath'."
}
