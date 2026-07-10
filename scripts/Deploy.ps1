#Requires -Version 7.0
<#
.SYNOPSIS
    Unified deploy entry point for OpenClaw: runs scripts/Publish.ps1 to
    produce a versioned bundle, then runs the staged Install.ps1 from inside
    that bundle to install it, without ever changing the caller's working
    directory.

.DESCRIPTION
    Thin wrapper-function orchestrator. Forwards publish-specific parameters
    to Publish.ps1, captures the bundle root it returns, fails fast if no
    bundle root is produced, forwards install-specific parameters (including
    the -SkipSign -> -AllowUnsigned mapping) to the staged copy of
    Install.ps1 inside the bundle, and returns the bundle root on success.

    Both child invocations are routed through script-scope wrapper functions
    (Invoke-PublishScript, Invoke-InstallScript), each guarded with the
    Get-Command override pattern already used for Test-IsElevatedAdmin in
    scripts/Install.ps1, so Pester tests can pre-register global-scoped
    overrides before this script is invoked via '&'.

.PARAMETER Version
    Forwarded verbatim to Publish.ps1's -Version parameter when supplied.

.PARAMETER Configuration
    Forwarded to Publish.ps1's -Configuration parameter when supplied.
    Values: Debug or Release. Default: Release.

.PARAMETER CertThumbprint
    Forwarded to Publish.ps1's -CertThumbprint parameter when supplied.

.PARAMETER SkipSign
    Forwarded to Publish.ps1's -SkipSign switch when supplied, and also
    mapped to Install.ps1's -AllowUnsigned switch (an unsigned bundle
    requires -AllowUnsigned at install time).

.PARAMETER SkipDocker
    Forwarded to Install.ps1's -SkipDocker switch when supplied.

.PARAMETER DockerEnvFilePath
    Forwarded to Install.ps1's -DockerEnvFilePath parameter when supplied.
    This script forwards the path string only; it never reads, copies, or
    stages the referenced file itself.

.PARAMETER AnthropicEnvFilePath
    Forwarded to Install.ps1's -AnthropicEnvFilePath parameter when supplied.
    This script forwards the path string only; it never reads, copies, or
    stages the referenced file itself.

.PARAMETER Force
    Forwarded to Install.ps1's -Force switch when supplied.

.EXAMPLE
    .\scripts\Deploy.ps1 -Version '1.2.3.0' -SkipSign
    Publishes an unsigned bundle, then installs it (mapping -SkipSign to
    Install.ps1's -AllowUnsigned).

.EXAMPLE
    .\scripts\Deploy.ps1 -Version '1.2.3.0' -CertThumbprint 'ABCDEF0123' -DockerEnvFilePath 'C:\ops\docker.env' -AnthropicEnvFilePath 'C:\ops\anthropic.env'
    Publishes a signed bundle, then installs it with operator-managed Docker
    env files forwarded by path.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Version,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$CertThumbprint,

    [switch]$SkipSign,

    [switch]$SkipDocker,

    [string]$DockerEnvFilePath,

    [string]$AnthropicEnvFilePath,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Wrapper-function seam for Publish.ps1. Defined at script scope so tests can
# override it with a global function (pre-registered before this script is
# invoked via '&'); the production path invokes the real Publish.ps1.
if (-not (Get-Command -Name 'Invoke-PublishScript' -ErrorAction SilentlyContinue)) {
    function Invoke-PublishScript {
        [CmdletBinding(SupportsShouldProcess = $true)]
        param(
            [Parameter(Mandatory = $true)]
            [string]$PublishScriptPath,

            [Parameter(Mandatory = $true)]
            [hashtable]$PublishParams
        )
        if ($PSCmdlet.ShouldProcess($PublishScriptPath, 'Invoke Publish.ps1')) {
            return & $PublishScriptPath @PublishParams
        }
    }
}

# Wrapper-function seam for the staged Install.ps1. Same guard pattern as
# Invoke-PublishScript above.
if (-not (Get-Command -Name 'Invoke-InstallScript' -ErrorAction SilentlyContinue)) {
    function Invoke-InstallScript {
        [CmdletBinding(SupportsShouldProcess = $true)]
        param(
            [Parameter(Mandatory = $true)]
            [string]$InstallScriptPath,

            [Parameter(Mandatory = $true)]
            [hashtable]$InstallParams
        )
        if ($PSCmdlet.ShouldProcess($InstallScriptPath, 'Invoke Install.ps1')) {
            return & $InstallScriptPath @InstallParams
        }
    }
}

# --- Main (only runs when executed directly, not when dot-sourced for tests) ---
if ($MyInvocation.InvocationName -ne '.') {

    # Stage 1: publish. Build the forwarded publish-parameter hashtable from
    # only the bound parameters, so Publish.ps1's own defaults (e.g.
    # Configuration = 'Release', the .env-driven -Version fallback) remain in
    # effect when the corresponding -Deploy.ps1 parameter was not supplied.
    $publishParams = @{}
    if ($PSBoundParameters.ContainsKey('Version')) { $publishParams['Version'] = $Version }
    if ($PSBoundParameters.ContainsKey('Configuration')) { $publishParams['Configuration'] = $Configuration }
    if ($PSBoundParameters.ContainsKey('CertThumbprint')) { $publishParams['CertThumbprint'] = $CertThumbprint }
    if ($SkipSign) { $publishParams['SkipSign'] = $true }

    # -WhatIf is not forwarded explicitly here: Invoke-PublishScript is a
    # script-scoped function (defined above, lexically nested inside this
    # script), so it inherits this script's own $WhatIfPreference through
    # normal PowerShell scope resolution when Deploy.ps1 itself is invoked
    # with -WhatIf, and its own $PSCmdlet.ShouldProcess() gate observes it.
    $publishScriptPath = Join-Path $PSScriptRoot 'Publish.ps1'
    Write-Information "[deploy:publish] Invoking $publishScriptPath" -InformationAction Continue
    $bundleRoot = Invoke-PublishScript -PublishScriptPath $publishScriptPath -PublishParams $publishParams

    # Stage 2: fail-fast guard. Refuse to proceed to Install.ps1 when publish
    # threw (surfaces via $ErrorActionPreference = 'Stop' above) or returned no
    # bundle root.
    if ([string]::IsNullOrWhiteSpace($bundleRoot)) {
        throw "Publish.ps1 did not return a bundle root; refusing to invoke the staged Install.ps1. Verify $publishScriptPath completed successfully and returned its bundle root."
    }
    Write-Information "[deploy:publish] Bundle root $bundleRoot" -InformationAction Continue

    # Stage 3: install. Build the forwarded install-parameter hashtable from
    # only the bound parameters, plus the -SkipSign -> -AllowUnsigned mapping
    # (an unsigned bundle requires -AllowUnsigned at install time).
    $installParams = @{}
    if ($PSBoundParameters.ContainsKey('SkipDocker')) { $installParams['SkipDocker'] = $true }
    if ($PSBoundParameters.ContainsKey('DockerEnvFilePath')) { $installParams['DockerEnvFilePath'] = $DockerEnvFilePath }
    if ($PSBoundParameters.ContainsKey('AnthropicEnvFilePath')) { $installParams['AnthropicEnvFilePath'] = $AnthropicEnvFilePath }
    if ($PSBoundParameters.ContainsKey('Force')) { $installParams['Force'] = $true }
    if ($SkipSign) { $installParams['AllowUnsigned'] = $true }

    $installScriptPath = Join-Path $bundleRoot 'Install.ps1'
    Write-Information "[deploy:install] Invoking $installScriptPath" -InformationAction Continue
    Invoke-InstallScript -InstallScriptPath $installScriptPath -InstallParams $installParams -WhatIf:$WhatIfPreference

    Write-Information "[deploy] Deployed bundle root $bundleRoot" -InformationAction Continue
    return $bundleRoot
}
