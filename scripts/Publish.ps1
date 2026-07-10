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
      5. MSIX assembly (layout, version stamp, PRI, makeappx pack, optional
         signtool sign).
      6. manifest.json generation (SHA-256 per file, sorted by path).

    Delegates the bulk of stage-internal logic to scripts/Publish.Helpers.psm1
    so this orchestrator stays under 500 lines per repo policy.

.PARAMETER Version
    Optional 4-part version (for example '1.2.3.0'). Validated strictly via
    ValidatePattern '^\d+\.\d+\.\d+\.\d+$'; 3-part inputs are rejected at
    parameter binding time. Planner resolution Q1 (strict validation, no
    normalization). When supplied, the value is used verbatim, stamped into
    AppxManifest.xml, and persisted to OPENCLAW_PACKAGE_VERSION in the
    repository-root .env. When omitted, the script reads OPENCLAW_PACKAGE_VERSION
    from the repository-root .env, increments its 4th (revision) segment by one,
    publishes that next revision, and writes the incremented value back to .env.
    With no -Version and a missing/blank OPENCLAW_PACKAGE_VERSION the script
    fails fast with a remediation message before any state-changing stage.

.PARAMETER OutputDir
    Root directory for the versioned bundle. The script writes under
    <OutputDir>/<Version>/. Default: 'artifacts/publish'.

.PARAMETER Configuration
    Build configuration passed to every dotnet publish invocation. Values:
    Debug or Release. Default: Release.

.PARAMETER CertThumbprint
    SHA-1 thumbprint of the code-signing certificate in Cert:\CurrentUser\My.
    Required unless -SkipSign is supplied. When -SkipSign is not set and this
    parameter is empty, the thumbprint is resolved via Resolve-CertThumbprint in
    the following precedence (D7):
      1. an explicit -CertThumbprint value (always wins);
      2. OPENCLAW_CERT_THUMBPRINT in the repository-root .env;
      3. the dotnet user secret 'Signing:CertThumbprint' for
         src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj (set via
         `dotnet user-secrets set 'Signing:CertThumbprint' '<thumbprint>'
          --project src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`);
      4. the OPENCLAW_CERT_THUMBPRINT process-environment variable.
    The original fail-fast contract is preserved: if -SkipSign is not set and no
    thumbprint can be resolved from any source, the script throws before any
    state-changing stage.

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

# Force a reload so a reused PowerShell session does not keep executing an
# older Publish.Helpers module after the file changes on disk.
Import-Module (Join-Path $PSScriptRoot 'Publish.Helpers.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $PSScriptRoot 'Publish.Msix.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $PSScriptRoot 'Publish.Env.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $PSScriptRoot 'Publish.Docker.psm1') -Force -ErrorAction Stop

# --- Main (only runs when executed directly, not when dot-sourced for tests) ---
if ($MyInvocation.InvocationName -ne '.') {

    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $EnvFilePath = Join-Path $RepoRoot '.env'

    # Stage 00: resolve the package version from .env when -Version is omitted.
    # The .env stores the last-published version; with no -Version we read it and
    # increment the 4th (revision) segment to obtain the version to publish. With
    # -Version we use it verbatim. The missing-version guard throws here, before
    # any state-changing stage. The resolved value is persisted to .env later,
    # after the signing fail-fast gate, so an ambiguous signing configuration
    # leaves .env unchanged.
    $envContent = Read-EnvFileContent -Path $EnvFilePath
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $envMap = Get-EnvFileMap -Content $envContent
        $storedVersion = if ($envMap.Contains('OPENCLAW_PACKAGE_VERSION')) { [string]$envMap['OPENCLAW_PACKAGE_VERSION'] } else { '' }
        if ([string]::IsNullOrWhiteSpace($storedVersion)) {
            throw "No -Version supplied and OPENCLAW_PACKAGE_VERSION is missing or blank in $EnvFilePath. Set OPENCLAW_PACKAGE_VERSION to the last-published 4-part version (for example '1.0.2.0') in .env, or pass -Version explicitly. Refusing to invent a version."
        }
        $Version = Step-PackageVersion -Version $storedVersion.Trim()
    }

    # Stage 0a: resolve the signing thumbprint when signing is requested but no
    # explicit -CertThumbprint was provided. Precedence (D7): explicit > .env
    # OPENCLAW_CERT_THUMBPRINT > dotnet user secret > process env. The .env value
    # and the process-env value are both injected (not read inside the resolver)
    # to keep resolution deterministic and testable.
    if (-not $SkipSign -and [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $signingProject = Join-Path $RepoRoot 'src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj'
        $dotEnvMap = Get-EnvFileMap -Content $envContent
        $dotEnvThumbprint = if ($dotEnvMap.Contains('OPENCLAW_CERT_THUMBPRINT')) { [string]$dotEnvMap['OPENCLAW_CERT_THUMBPRINT'] } else { '' }
        $CertThumbprint = Resolve-CertThumbprint `
            -ExplicitThumbprint $CertThumbprint `
            -DotEnvThumbprint $dotEnvThumbprint `
            -ProjectPath $signingProject `
            -EnvThumbprint $env:OPENCLAW_CERT_THUMBPRINT
    }

    # Stage 0b: parameter validation. Fail fast before any state-changing stage.
    if (-not $SkipSign -and [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        throw 'Either -SkipSign or a non-empty -CertThumbprint must be supplied (directly, via OPENCLAW_CERT_THUMBPRINT in .env, via the dotnet user secret Signing:CertThumbprint, or via the OPENCLAW_CERT_THUMBPRINT process-environment variable). Refusing to proceed with an ambiguous signing configuration.'
    }

    # Stage 0c: persist the resolved version to .env (the last-published value),
    # now that the signing gate has passed. Idempotent update-in-place.
    $persistedContent = Set-EnvFileValue -Content $envContent -Key 'OPENCLAW_PACKAGE_VERSION' -Value $Version
    Write-EnvFileContent -Path $EnvFilePath -Content $persistedContent
    Write-Information "[publish] Using version $Version (persisted to $EnvFilePath)" -InformationAction Continue

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

    # Stage 3b: build the container images, save the combined tar, and write the
    # transformed offline bundle compose (issue #142). Runs before Stage 6
    # Write-PublishManifest so the tar and transformed compose are manifested
    # automatically (path + [long] size + SHA-256).
    Write-Information "[docker-images] Building images and writing openclaw-images.tar into $DockerBundleDir" -InformationAction Continue
    Invoke-PublishDockerStage -RepoRoot $RepoRoot -BundleDockerDir $DockerBundleDir -Version $Version -Configuration $Configuration -EnvMap (Get-EnvFileMap -Content $envContent)

    # Stage 4: MSIX pipeline.
    $StagingDir = Join-Path $RepoRoot 'installer/staging'
    $ManifestSource = Join-Path $RepoRoot 'installer/Package.appxmanifest'
    $AssetsDir = Join-Path $RepoRoot 'installer/Assets'
    $BridgeDir = Join-Path $ExecutablesRoot 'OpenClaw.MailBridge'
    $ClientDir = Join-Path $ExecutablesRoot 'OpenClaw.MailBridge.Client'
    $MsixDir = Join-Path $BundleRoot 'msix'
    $MsixPath = Join-Path $MsixDir ("OpenClaw.MailBridge_{0}_x64.msix" -f $Version)

    Write-Information '[msix] Assembling staging layout' -InformationAction Continue
    Invoke-LayoutAssembly -BridgePublishDir $BridgeDir -ClientPublishDir $ClientDir -AssetsDir $AssetsDir -StagingDir $StagingDir

    Write-Information '[msix] Stamping AppxManifest.xml' -InformationAction Continue
    $null = Invoke-VersionStamp -ManifestSourcePath $ManifestSource -StagingDir $StagingDir -Version $Version

    Write-Information '[msix] Generating PRI resource index' -InformationAction Continue
    Invoke-MakePri -StagingDir $StagingDir

    Write-Information "[msix] Packing $MsixPath" -InformationAction Continue
    $null = Invoke-MakeAppx -StagingDir $StagingDir -OutputMsixPath $MsixPath

    if (-not $SkipSign) {
        Write-Information '[msix] Signing MSIX' -InformationAction Continue
        Invoke-SignTool -MsixPath $MsixPath -CertThumbprint $CertThumbprint
    }
    else {
        Write-Information '[msix] Skipping sign (-SkipSign)' -InformationAction Continue
    }

    # Stage 5: stage install scripts into the bundle root so the bundle is
    # self-installing (operator cd's into the bundle and runs .\Install.ps1).
    Write-Information "[install-scripts] Staging Install.ps1, Uninstall.ps1, Install.Helpers.psm1, Install.Preflight.psm1, and Install.Docker.psm1 into $BundleRoot" -InformationAction Continue
    Copy-InstallScriptsIntoBundle -RepoRoot $RepoRoot -BundleRoot $BundleRoot

    # Stage 6: manifest (runs AFTER install-script staging so the manifest
    # lists the staged install scripts).
    Write-Information "[manifest] Writing manifest.json under $BundleRoot" -InformationAction Continue
    $null = Write-PublishManifest -BundleRoot $BundleRoot -Version $Version

    Write-Information "[publish] Bundle written to $BundleRoot" -InformationAction Continue
    return $BundleRoot
}
