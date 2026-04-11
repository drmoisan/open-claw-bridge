#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and optionally signs an MSIX installer package for OpenClaw MailBridge.

.DESCRIPTION
    Orchestrates the full MSIX build pipeline:
      1. Stamps the version into AppxManifest.xml in the staging directory.
      2. Assembles the staging directory from publish outputs and installer assets.
      3. Generates a PRI resource index (MakePri.exe).
      4. Packs the staging directory into an MSIX file (makeappx.exe).
      5. Signs the MSIX with a code-signing certificate (signtool.exe), unless -SkipSign is specified.

.PARAMETER Version
    The 4-part version string (e.g. '1.0.0.0') to stamp into the manifest and use in the output filename.

.PARAMETER OutputDir
    Directory where the final .msix file is written. Defaults to 'artifacts/msix'.

.PARAMETER CertThumbprint
    SHA1 thumbprint of the code-signing certificate in the current-user certificate store.
    Required unless -SkipSign is specified.

.PARAMETER SkipSign
    When specified, the signing step is skipped. Useful for unsigned CI builds.

.EXAMPLE
    .\build-msix.ps1 -Version '1.2.3.0' -CertThumbprint 'ABCDEF1234...'

.EXAMPLE
    .\build-msix.ps1 -Version '1.0.0.0' -SkipSign
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Version = '1.0.0.0',

    [string]$OutputDir = 'artifacts/msix',

    [string]$CertThumbprint = '',

    [switch]$SkipSign
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Constants ---
$StagingDir = Join-Path $PSScriptRoot '../installer/staging'
$ManifestSource = Join-Path $PSScriptRoot '../installer/Package.appxmanifest'
$AssetsSource = Join-Path $PSScriptRoot '../installer/Assets'
$BridgePublishDir = Join-Path $PSScriptRoot '../artifacts/publish/bridge'
$ClientPublishDir = Join-Path $PSScriptRoot '../artifacts/publish/client'

function Find-WindowsSdkTool {
    <#
    .SYNOPSIS Locates a Windows SDK tool executable by probing known SDK bin paths.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )
    # Probe known Windows SDK bin paths for the requested tool
    $sdkBinRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $sdkBinRoot) {
        $found = Get-ChildItem $sdkBinRoot -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\x64\*' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
        if ($found) {
            return $found.FullName
        }
    }

    # Fall back to PATH resolution
    $onPath = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    Write-Error "Cannot locate $ToolName. Install the Windows 10 SDK and ensure its bin path is accessible."
}

function Invoke-VersionStamp {
    <#
    .SYNOPSIS Stamps the 4-part version into the staging AppxManifest.xml.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestSource,

        [Parameter(Mandatory = $true)]
        [string]$StagingDir,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    $destManifest = Join-Path $StagingDir 'AppxManifest.xml'
    $null = New-Item -ItemType Directory -Force -Path $StagingDir

    # Read the manifest, replace the Identity Version attribute, write to staging
    [xml]$xml = Get-Content -Raw $ManifestSource
    $identityNode = $xml.Package.Identity
    $identityNode.SetAttribute('Version', $Version)
    $xml.Save($destManifest)
    Write-Verbose "Stamped version $Version into $destManifest"
    return $destManifest
}

function Invoke-LayoutAssembly {
    <#
    .SYNOPSIS Assembles the MSIX staging directory from publish outputs and installer assets.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BridgePublishDir,

        [Parameter(Mandatory = $true)]
        [string]$ClientPublishDir,

        [Parameter(Mandatory = $true)]
        [string]$AssetsSource,

        [Parameter(Mandatory = $true)]
        [string]$StagingDir
    )
    # Validate that both publish directories exist before attempting layout
    if (-not (Test-Path $BridgePublishDir)) {
        throw "Bridge publish directory not found: $BridgePublishDir. Run 'dotnet publish /p:PublishProfile=msix' first."
    }
    if (-not (Test-Path $ClientPublishDir)) {
        throw "Client publish directory not found: $ClientPublishDir. Run 'dotnet publish /p:PublishProfile=msix' first."
    }

    $bridgeDest = Join-Path $StagingDir 'bridge'
    $clientDest = Join-Path $StagingDir 'client'
    $assetsDest = Join-Path $StagingDir 'Assets'

    # Create destination subdirectories before copying
    $null = New-Item -ItemType Directory -Force -Path $bridgeDest
    $null = New-Item -ItemType Directory -Force -Path $clientDest

    # Copy published binaries into installer/staging/bridge/ and installer/staging/client/
    Copy-Item -Recurse -Force -Path "$BridgePublishDir\*" -Destination $bridgeDest
    Write-Verbose "Copied bridge binaries to $bridgeDest"

    Copy-Item -Recurse -Force -Path "$ClientPublishDir\*" -Destination $clientDest
    Write-Verbose "Copied client binaries to $clientDest"

    # Copy installer Assets (icons) into staging
    $null = New-Item -ItemType Directory -Force -Path $assetsDest
    Copy-Item -Recurse -Force -Path "$AssetsSource\*" -Destination $assetsDest
    Write-Verbose "Copied assets to $assetsDest"
}

function Invoke-MakePri {
    <#
    .SYNOPSIS Generates the PRI resource index for the MSIX package using MakePri.exe.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagingDir
    )
    $makePri = Find-WindowsSdkTool -ToolName 'makepri.exe'
    $configFile = Join-Path $StagingDir 'priconfig.xml'
    $priFile = Join-Path $StagingDir 'resources.pri'

    # Generate a default PRI configuration file
    & $makePri createconfig /cf $configFile /dq en-US /pv 10.0 /o
    if ($LASTEXITCODE -ne 0) {
        Write-Error "MakePri createconfig failed with exit code $LASTEXITCODE"
    }

    # Build the PRI resource index from the staging directory
    & $makePri new /pr $StagingDir /cf $configFile /mn (Join-Path $StagingDir 'AppxManifest.xml') /of $priFile /o
    if ($LASTEXITCODE -ne 0) {
        Write-Error "MakePri new failed with exit code $LASTEXITCODE"
    }
    Write-Verbose "Generated $priFile"
}

function Invoke-MakeAppx {
    <#
    .SYNOPSIS Packs the staging directory into an MSIX file using makeappx.exe.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagingDir,

        [Parameter(Mandatory = $true)]
        [string]$OutputDir,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    $makeAppx = Find-WindowsSdkTool -ToolName 'makeappx.exe'
    $null = New-Item -ItemType Directory -Force -Path $OutputDir
    $msixPath = Join-Path $OutputDir "OpenClaw.MailBridge_${Version}_x64.msix"

    # Pack the staging directory into the MSIX file; /nv skips manifest validation
    # Redirect to Out-Host so stdout doesn't pollute the function's return value
    & $makeAppx pack /d $StagingDir /p $msixPath /nv /o | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makeappx pack failed with exit code $LASTEXITCODE"
    }
    Write-Verbose "Packed MSIX to $msixPath"
    return $msixPath
}

function Invoke-SignTool {
    <#
    .SYNOPSIS Signs the MSIX file with a code-signing certificate using signtool.exe.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$MsixPath,

        [Parameter(Mandatory = $true)]
        [string]$CertThumbprint
    )
    # Only sign when -SkipSign is not set (caller is responsible for the SkipSign guard)
    $signtool = Find-WindowsSdkTool -ToolName 'signtool.exe'

    # Sign with SHA256 digest and a RFC 3161 timestamp server
    & $signtool sign /sha1 $CertThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $MsixPath
    if ($LASTEXITCODE -ne 0) {
        Write-Error "signtool sign failed with exit code $LASTEXITCODE"
    }
    Write-Verbose "Signed $MsixPath"
}

# --- Main (only runs when executed directly, not when dot-sourced for testing) ---
if ($MyInvocation.InvocationName -ne '.') {
    Write-Information "=== Build MSIX: version $Version ===" -InformationAction Continue

    Write-Information "Step 1: Stamping version into AppxManifest.xml..." -InformationAction Continue
    Invoke-VersionStamp -ManifestSource $ManifestSource -StagingDir $StagingDir -Version $Version

    Write-Information "Step 2: Assembling staging layout..." -InformationAction Continue
    Invoke-LayoutAssembly -BridgePublishDir $BridgePublishDir -ClientPublishDir $ClientPublishDir `
        -AssetsSource $AssetsSource -StagingDir $StagingDir

    Write-Information "Step 3: Generating PRI resource index..." -InformationAction Continue
    Invoke-MakePri -StagingDir $StagingDir

    Write-Information "Step 4: Packing MSIX..." -InformationAction Continue
    $msixPath = Invoke-MakeAppx -StagingDir $StagingDir -OutputDir $OutputDir -Version $Version

    if (-not $SkipSign) {
        Write-Information "Step 5: Signing MSIX..." -InformationAction Continue
        Invoke-SignTool -MsixPath $msixPath -CertThumbprint $CertThumbprint
    }
    else {
        Write-Information "Step 5: Signing skipped (-SkipSign specified)." -InformationAction Continue
    }

    Write-Information "=== Build complete: $msixPath ===" -InformationAction Continue
    return $msixPath
}

