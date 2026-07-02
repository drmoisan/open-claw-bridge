# Publish.Msix.psm1
# Windows SDK / MSIX tooling module for scripts/Publish.ps1.
#
# Purpose
#   Holds the cohesive set of Windows SDK tool resolution and MSIX packaging
#   helpers used by the unified publish orchestrator: Windows SDK tool
#   resolution, AppxManifest.xml version stamping, MSIX staging-layout
#   assembly, and makepri/makeappx/signtool invocation. Extracted from
#   scripts/Publish.Helpers.psm1 to keep both modules under the 500-line cap.
#
# Policy notes
#   - PowerShell 7+ compatible.
#   - Every state-changing helper uses [CmdletBinding(SupportsShouldProcess=$true)]
#     so callers can drive the module under -WhatIf for unit tests.
#   - This module stays under 500 lines per repo policy.
#   - State is passed in explicitly; no script-scoped mutable state is used.
#   - Version strings are strictly validated via [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
#     on every parameter that receives a -Version; no silent normalization.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-WindowsSdkTool {
    <#
    .SYNOPSIS
        Locates a Windows SDK tool executable by probing known SDK bin paths.
    .DESCRIPTION
        Scans ${env:ProgramFiles(x86)}\Windows Kits\10\bin for an \x64\ match,
        preferring the lexicographically highest entry. Falls back to PATH.
        Throws a terminating error if the tool cannot be located.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

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

    $onPath = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    throw "Cannot locate $ToolName. Install the Windows 10 SDK and ensure its bin path is accessible."
}

function Get-StampedAppxManifestXml {
    <#
    .SYNOPSIS
        Returns a new [xml] with Package.Identity.Version set to the supplied version.
    .DESCRIPTION
        Pure helper. Does not perform I/O. Preserves every other attribute on the
        Identity element. Clones the input document so the caller's original is
        not mutated.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseOutputTypeCorrectly', '',
        Justification = 'XmlDocument.Clone returns XmlNode at the static-analysis level; the concrete runtime type is XmlDocument and the function always returns the cast [xml].'
    )]
    [CmdletBinding()]
    [OutputType([System.Xml.XmlDocument])]
    param(
        [Parameter(Mandatory = $true)]
        [xml]$ManifestXml,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version
    )

    [System.Xml.XmlDocument]$clone = $ManifestXml.Clone()
    $clone.Package.Identity.SetAttribute('Version', $Version)
    return $clone
}

function Invoke-VersionStamp {
    <#
    .SYNOPSIS
        Stamps the 4-part version into the staging AppxManifest.xml.
    .DESCRIPTION
        Reads the source manifest, stamps the version via Get-StampedAppxManifestXml,
        and writes to <StagingDir>/AppxManifest.xml only when $PSCmdlet.ShouldProcess
        is satisfied.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestSourcePath,

        [Parameter(Mandatory = $true)]
        [string]$StagingDir,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version
    )

    $destManifest = Join-Path $StagingDir 'AppxManifest.xml'
    [xml]$source = Get-Content -Raw -Path $ManifestSourcePath
    $stamped = Get-StampedAppxManifestXml -ManifestXml $source -Version $Version

    if ($PSCmdlet.ShouldProcess($destManifest, "Write AppxManifest.xml with version $Version")) {
        if (-not (Test-Path $StagingDir)) {
            $null = New-Item -ItemType Directory -Force -Path $StagingDir
        }
        Set-Content -Path $destManifest -Value $stamped.OuterXml -Encoding utf8
    }

    return $destManifest
}

function Invoke-LayoutAssembly {
    <#
    .SYNOPSIS
        Assembles the MSIX staging directory from publish outputs and installer assets.
    .DESCRIPTION
        Clears and recreates $StagingDir, then copies bridge binaries, client binaries,
        and installer assets into it. Throws a terminating error naming the missing
        path when bridge or client directories are absent.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BridgePublishDir,

        [Parameter(Mandatory = $true)]
        [string]$ClientPublishDir,

        [Parameter(Mandatory = $true)]
        [string]$AssetsDir,

        [Parameter(Mandatory = $true)]
        [string]$StagingDir
    )

    if (-not (Test-Path $BridgePublishDir)) {
        throw "Bridge publish directory not found: $BridgePublishDir"
    }
    if (-not (Test-Path $ClientPublishDir)) {
        throw "Client publish directory not found: $ClientPublishDir"
    }

    $bridgeDest = Join-Path $StagingDir 'bridge'
    $clientDest = Join-Path $StagingDir 'client'
    $assetsDest = Join-Path $StagingDir 'Assets'

    if ($PSCmdlet.ShouldProcess($StagingDir, 'Assemble staging layout')) {
        if (Test-Path $StagingDir) {
            Remove-Item -Recurse -Force -Path $StagingDir
        }
        $null = New-Item -ItemType Directory -Force -Path $StagingDir
        $null = New-Item -ItemType Directory -Force -Path $bridgeDest
        $null = New-Item -ItemType Directory -Force -Path $clientDest
        $null = New-Item -ItemType Directory -Force -Path $assetsDest

        Copy-Item -Recurse -Force -Path (Join-Path $BridgePublishDir '*') -Destination $bridgeDest
        Copy-Item -Recurse -Force -Path (Join-Path $ClientPublishDir '*') -Destination $clientDest
        Copy-Item -Recurse -Force -Path (Join-Path $AssetsDir '*') -Destination $assetsDest
    }
}

function Invoke-MakePri {
    <#
    .SYNOPSIS
        Runs makepri createconfig and makepri new against the staging directory.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagingDir
    )

    $makePri = Find-WindowsSdkTool -ToolName 'makepri.exe'
    $configFile = Join-Path $StagingDir 'priconfig.xml'
    $priFile = Join-Path $StagingDir 'resources.pri'

    if ($PSCmdlet.ShouldProcess($priFile, 'Generate PRI resource index')) {
        $createConfigOutput = & $makePri createconfig /cf $configFile /dq en-US /pv 10.0 /o 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "makepri createconfig failed with exit code $LASTEXITCODE. Output: $createConfigOutput"
        }

        $manifestPath = Join-Path $StagingDir 'AppxManifest.xml'
        $newOutput = & $makePri new /pr $StagingDir /cf $configFile /mn $manifestPath /of $priFile /o 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "makepri new failed with exit code $LASTEXITCODE. Output: $newOutput"
        }
    }
}

function Invoke-MakeAppx {
    <#
    .SYNOPSIS
        Packs the staging directory into an MSIX file using makeappx.exe.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagingDir,

        [Parameter(Mandatory = $true)]
        [string]$OutputMsixPath
    )

    $makeAppx = Find-WindowsSdkTool -ToolName 'makeappx.exe'
    $outputDir = Split-Path -Parent $OutputMsixPath

    if ($PSCmdlet.ShouldProcess($OutputMsixPath, 'Pack MSIX package')) {
        if ($outputDir -and -not (Test-Path $outputDir)) {
            $null = New-Item -ItemType Directory -Force -Path $outputDir
        }

        $packOutput = & $makeAppx pack /d $StagingDir /p $OutputMsixPath /nv /o 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "makeappx pack failed with exit code $LASTEXITCODE. Output: $packOutput"
        }
    }

    return $OutputMsixPath
}

function Invoke-SignTool {
    <#
    .SYNOPSIS
        Signs the MSIX file with a code-signing certificate using signtool.exe.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$MsixPath,

        [Parameter(Mandatory = $true)]
        [string]$CertThumbprint
    )

    $signtool = Find-WindowsSdkTool -ToolName 'signtool.exe'

    if ($PSCmdlet.ShouldProcess($MsixPath, 'Sign MSIX package')) {
        $signOutput = & $signtool sign /sha1 $CertThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $MsixPath 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "signtool sign failed with exit code $LASTEXITCODE. Output: $signOutput"
        }
    }
}

Export-ModuleMember -Function @(
    'Find-WindowsSdkTool'
    'Get-StampedAppxManifestXml'
    'Invoke-VersionStamp'
    'Invoke-LayoutAssembly'
    'Invoke-MakePri'
    'Invoke-MakeAppx'
    'Invoke-SignTool'
)
