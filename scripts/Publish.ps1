#Requires -Version 7.0
<#
.SYNOPSIS
    Unified publish entry point for OpenClaw. Produces a versioned local
    artifact bundle under artifacts/publish/<version>/ containing executables,
    docker artifacts, and a signed or unsigned MSIX installer, plus a
    manifest.json enumerating every file in the bundle.

.DESCRIPTION
    Unified release publish entry point for OpenClaw. Orchestrates:
      1. Parameter validation (fails fast when neither -SkipSign nor
         -CertThumbprint is supplied).
      2. Clean-and-create of the per-version bundle directory.
      3. Per-project dotnet publish (four runnable src/ projects).
      4. Docker artifact copy (compose files, deploy/docker/**, .env.example),
         with the secrets/ exclusion enforced.
      5. MSIX assembly (version stamp, layout, PRI, makeappx pack, optional
         signtool sign).
      6. manifest.json generation (SHA-256 per file, sorted by path).

    Delegates the bulk of stage-internal logic to scripts/Publish.Helpers.psm1
    so this orchestrator stays under 500 lines per repo policy.

.PARAMETER Version
    Mandatory 4-part version (for example '1.2.3.0'). Validated strictly via
    ValidatePattern '^\d+\.\d+\.\d+\.\d+$'; 3-part inputs are rejected at
    parameter binding time. Planner resolution Q1 (strict validation, no
    normalization). The value is stamped verbatim into AppxManifest.xml.

.PARAMETER OutputDir
    Root directory for the versioned bundle. The script writes under
    <OutputDir>/<Version>/. Default: 'artifacts/publish'.

.PARAMETER Configuration
    Build configuration passed to every dotnet publish invocation. Values:
    Debug or Release. Default: Release.

.PARAMETER CertThumbprint
    SHA-1 thumbprint of the code-signing certificate in Cert:\CurrentUser\My.
    Required unless -SkipSign is supplied.

.PARAMETER SkipSign
    When supplied, the MSIX is packed but not signed.

.NOTES
    Planner resolutions captured in this header:
      Q1 strict version validation via ValidatePattern (reject 3-part input).
      Q2 MSIX publish profile retention: OpenClaw.MailBridge and
         OpenClaw.MailBridge.Client are published via /p:PublishProfile=msix
         to preserve the feature #17 regression surface.
      Q3 structural stability only: manifest.json hashes are asserted to be
         64-character lowercase hex strings with non-negative sizes; not
         byte-identical across runs (R2R retains residual non-determinism).

.EXAMPLE
    .\scripts\Publish.ps1 -Version '1.2.3.0' -SkipSign
    Dev build: bundle without signing.

.EXAMPLE
    .\scripts\Publish.ps1 -Version '1.2.3.0' -CertThumbprint 'ABCDEF1234...'
    Signed release build.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$OutputDir = 'artifacts/publish',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$CertThumbprint = '',

    [switch]$SkipSign
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Import without -Force so Pester mocks applied to the already-loaded module
# are preserved across repeated script invocations within the same session.
Import-Module (Join-Path $PSScriptRoot 'Publish.Helpers.psm1')

# --- Main (only runs when executed directly, not when dot-sourced for tests) ---
if ($MyInvocation.InvocationName -ne '.') {

    # Stage 0: parameter validation. Fail fast before any state-changing stage.
    if (-not $SkipSign -and [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        throw 'Either -SkipSign or a non-empty -CertThumbprint must be supplied. Refusing to proceed with an ambiguous signing configuration.'
    }

    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $BundleRoot = Join-Path $OutputDir $Version

    # Stage 1: clean and recreate the bundle root.
    Write-Information "[publish] Preparing bundle root $BundleRoot" -InformationAction Continue
    if (Test-Path $BundleRoot) {
        if ($PSCmdlet.ShouldProcess($BundleRoot, 'Remove prior bundle root')) {
            Remove-Item -Recurse -Force -Path $BundleRoot
        }
    }
    if ($PSCmdlet.ShouldProcess($BundleRoot, 'Create bundle root')) {
        $null = New-Item -ItemType Directory -Force -Path $BundleRoot
    }

    # Stage 2: per-project dotnet publish.
    $ExecutablesRoot = Join-Path $BundleRoot 'executables'
    $PublishMatrix = @(
        [pscustomobject]@{
            Name      = 'OpenClaw.Core'
            Project   = Join-Path $RepoRoot 'src/OpenClaw.Core/OpenClaw.Core.csproj'
            ExtraArgs = @('--self-contained', 'true', '-r', 'win-x64')
        },
        [pscustomobject]@{
            Name      = 'OpenClaw.HostAdapter'
            Project   = Join-Path $RepoRoot 'src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj'
            ExtraArgs = @('--self-contained', 'true', '-r', 'win-x64')
        },
        [pscustomobject]@{
            Name      = 'OpenClaw.MailBridge'
            Project   = Join-Path $RepoRoot 'src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj'
            ExtraArgs = @('/p:PublishProfile=msix')
        },
        [pscustomobject]@{
            Name      = 'OpenClaw.MailBridge.Client'
            Project   = Join-Path $RepoRoot 'src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj'
            ExtraArgs = @('/p:PublishProfile=msix')
        }
    )

    foreach ($entry in $PublishMatrix) {
        $projectOut = Join-Path $ExecutablesRoot $entry.Name
        Write-Information "[publish] $($entry.Name) -> $projectOut" -InformationAction Continue
        Invoke-DotnetPublish `
            -ProjectPath $entry.Project `
            -OutputDir $projectOut `
            -Configuration $Configuration `
            -ExtraArgs $entry.ExtraArgs
    }

    # Stage 3: docker artifact copy.
    $DockerBundleDir = Join-Path $BundleRoot 'docker'
    Write-Information "[docker] Copying docker artifacts to $DockerBundleDir" -InformationAction Continue
    Copy-DockerArtifact -RepoRoot $RepoRoot -DockerBundleDir $DockerBundleDir

    # Stage 4: MSIX pipeline.
    $StagingDir = Join-Path $RepoRoot 'installer/staging'
    $ManifestSource = Join-Path $RepoRoot 'installer/Package.appxmanifest'
    $AssetsDir = Join-Path $RepoRoot 'installer/Assets'
    $BridgeDir = Join-Path $ExecutablesRoot 'OpenClaw.MailBridge'
    $ClientDir = Join-Path $ExecutablesRoot 'OpenClaw.MailBridge.Client'
    $MsixDir = Join-Path $BundleRoot 'msix'
    $MsixPath = Join-Path $MsixDir ("OpenClaw.MailBridge_{0}_x64.msix" -f $Version)

    Write-Information '[msix] Stamping AppxManifest.xml' -InformationAction Continue
    Invoke-VersionStamp -ManifestSourcePath $ManifestSource -StagingDir $StagingDir -Version $Version

    Write-Information '[msix] Assembling staging layout' -InformationAction Continue
    Invoke-LayoutAssembly -BridgePublishDir $BridgeDir -ClientPublishDir $ClientDir -AssetsDir $AssetsDir -StagingDir $StagingDir

    Write-Information '[msix] Generating PRI resource index' -InformationAction Continue
    Invoke-MakePri -StagingDir $StagingDir

    Write-Information "[msix] Packing $MsixPath" -InformationAction Continue
    Invoke-MakeAppx -StagingDir $StagingDir -OutputMsixPath $MsixPath

    if (-not $SkipSign) {
        Write-Information '[msix] Signing MSIX' -InformationAction Continue
        Invoke-SignTool -MsixPath $MsixPath -CertThumbprint $CertThumbprint
    }
    else {
        Write-Information '[msix] Skipping sign (-SkipSign)' -InformationAction Continue
    }

    # Stage 5: manifest.
    Write-Information "[manifest] Writing manifest.json under $BundleRoot" -InformationAction Continue
    Write-PublishManifest -BundleRoot $BundleRoot -Version $Version

    Write-Information "[publish] Bundle written to $BundleRoot" -InformationAction Continue
    return $BundleRoot
}
