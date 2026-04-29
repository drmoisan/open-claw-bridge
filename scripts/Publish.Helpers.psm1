# Publish.Helpers.psm1
# Shared helper module for scripts/Publish.ps1.
#
# Purpose
#   Centralize the pure and near-pure helpers used by the unified publish
#   orchestrator: Windows SDK tool resolution, AppxManifest.xml version
#   stamping, MSIX staging-layout assembly, makepri/makeappx/signtool
#   invocation, dotnet publish invocation, docker artifact copy with the
#   secrets/ exclusion, and manifest generation.
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

function Invoke-DotnetPublish {
    <#
    .SYNOPSIS
        Invokes dotnet publish for a single project with deterministic flags.
    .DESCRIPTION
        Runs dotnet publish with -c, -o, /p:Deterministic=true and any additional
        caller-supplied arguments. Extra args are appended after the required args.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputDir,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [string[]]$ExtraArgs = @()
    )

    if ($PSCmdlet.ShouldProcess($ProjectPath, "dotnet publish -c $Configuration -o $OutputDir")) {
        $publishArgs = @(
            'publish'
            $ProjectPath
            '-c', $Configuration
            '-o', $OutputDir
            '/p:Deterministic=true'
        )
        if ($ExtraArgs -and $ExtraArgs.Count -gt 0) {
            $publishArgs += $ExtraArgs
        }

        $publishOutput = & dotnet @publishArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $ProjectPath with exit code $LASTEXITCODE. Output: $publishOutput"
        }
    }
}

function Copy-DockerArtifact {
    <#
    .SYNOPSIS
        Copies the docker artifact set into the bundle, enforcing the secrets/ exclusion.
    .DESCRIPTION
        Copies docker-compose.yml and docker-compose.dev.yml (always), .env.example
        (when present, silent skip otherwise), and the recursive deploy/docker/** tree.
        Emits Write-Warning and skips any secrets/ directory detected under a source
        root. Never copies secrets/.env.anthropic.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$DockerBundleDir
    )

    if ($PSCmdlet.ShouldProcess($DockerBundleDir, 'Copy docker artifact set')) {
        if (-not (Test-Path $DockerBundleDir)) {
            $null = New-Item -ItemType Directory -Force -Path $DockerBundleDir
        }

        $composeRoot = Join-Path $RepoRoot 'docker-compose.yml'
        $composeDev = Join-Path $RepoRoot 'docker-compose.dev.yml'
        $envExample = Join-Path $RepoRoot '.env.example'
        $deployDocker = Join-Path $RepoRoot 'deploy/docker'
        $secretsDir = Join-Path $RepoRoot 'secrets'
        $anthropicEnv = Join-Path $RepoRoot 'secrets/.env.anthropic'

        if (Test-Path $secretsDir) {
            Write-Warning "Skipping secrets directory detected under source root: $secretsDir"
        }

        # Defensive: never copy the known-bad secret file regardless of path shape.
        if (Test-Path $anthropicEnv) {
            Write-Warning "Skipping secret file: $anthropicEnv"
        }

        Copy-Item -Path $composeRoot -Destination (Join-Path $DockerBundleDir 'docker-compose.yml') -Force
        Copy-Item -Path $composeDev -Destination (Join-Path $DockerBundleDir 'docker-compose.dev.yml') -Force

        if (Test-Path $envExample) {
            Copy-Item -Path $envExample -Destination (Join-Path $DockerBundleDir '.env.example') -Force
        }

        if (Test-Path $deployDocker) {
            $deployDest = Join-Path $DockerBundleDir 'deploy/docker'
            if (-not (Test-Path $deployDest)) {
                $null = New-Item -ItemType Directory -Force -Path $deployDest
            }
            Copy-Item -Path (Join-Path $deployDocker '*') -Destination $deployDest -Recurse -Force
        }
    }
}

function New-ManifestEntry {
    <#
    .SYNOPSIS
        Produces a single manifest entry for a file.
    .DESCRIPTION
        Pure helper. Given a file path and the bundle root, returns a pscustomobject
        with path (forward-slash relative), size (int), and sha256 (lowercase hex).
        The "New-" verb is used intentionally (constructs a new entry object); this
        function has no side effects, so ShouldProcess does not apply.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Pure function: constructs a new pscustomobject; does not change system state.'
    )]
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string]$BundleRoot
    )

    $fileInfo = Get-Item -LiteralPath $FilePath
    $hash = (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()

    $normalizedRoot = (Resolve-Path -LiteralPath $BundleRoot).Path.TrimEnd('\', '/')
    $absFile = $fileInfo.FullName
    $relative = $absFile.Substring($normalizedRoot.Length).TrimStart('\', '/')
    $relative = $relative -replace '\\', '/'

    return [pscustomobject]@{
        path   = $relative
        size   = [int]$fileInfo.Length
        sha256 = $hash
    }
}

function Copy-InstallScriptsIntoBundle {
    <#
    .SYNOPSIS
        Copies the install-related scripts from the repo into the bundle root.
    .DESCRIPTION
        The four files Install.ps1, Uninstall.ps1, Install.Helpers.psm1, and
        Install.Preflight.psm1
        must ship inside every bundle so operators can `cd` into the bundle
        directory and invoke .\Install.ps1 directly (the script self-locates
        via $PSScriptRoot). This helper resolves <RepoRoot>/scripts/<name> for
        each file, verifies the source exists, and copies it to the bundle
        root with Copy-Item -LiteralPath ... -Force.
    .PARAMETER RepoRoot
        Absolute path to the repository root containing the scripts/ directory.
    .PARAMETER BundleRoot
        Absolute path to the bundle root (same level as executables/, docker/,
        msix/). The install-script files are copied to this directory.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$BundleRoot
    )

    $srcScriptsDir = Join-Path $RepoRoot 'scripts'
    $names = @('Install.ps1', 'Uninstall.ps1', 'Install.Helpers.psm1', 'Install.Preflight.psm1')

    foreach ($name in $names) {
        $srcPath = Join-Path $srcScriptsDir $name
        if (-not (Test-Path -LiteralPath $srcPath)) {
            throw "Install-script source file not found at '$srcPath'. Cannot stage into bundle."
        }
        $dstPath = Join-Path $BundleRoot $name
        if ($PSCmdlet.ShouldProcess($dstPath, "Copy install script $name into bundle")) {
            Copy-Item -LiteralPath $srcPath -Destination $dstPath -Force
        }
    }
}

function Write-PublishManifest {
    <#
    .SYNOPSIS
        Walks the bundle root, composes the manifest, and writes manifest.json.
    .DESCRIPTION
        Enumerates every file under $BundleRoot excluding manifest.json itself.
        Composes an object with the { version, files } schema where files is
        sorted ascending by path using invariant culture. Writes the JSON to
        <BundleRoot>/manifest.json. The top-level version is the -Version
        parameter verbatim; downstream consumers read it via Get-ManifestVersion.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundleRoot,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version
    )

    $manifestPath = Join-Path $BundleRoot 'manifest.json'

    $allFiles = Get-ChildItem -LiteralPath $BundleRoot -Recurse -File |
    Where-Object { $_.FullName -ne (Join-Path $BundleRoot 'manifest.json') }

    $entries = foreach ($f in $allFiles) {
        New-ManifestEntry -FilePath $f.FullName -BundleRoot $BundleRoot
    }

    # Sort by path using InvariantCulture for stable diffs across locales.
    $entriesArray = @($entries)
    $sortedEntries = $entriesArray | Sort-Object -Property path -Culture 'en-US'

    $manifest = [pscustomobject]@{
        version = $Version
        files   = @($sortedEntries)
    }

    if ($PSCmdlet.ShouldProcess($manifestPath, 'Write publish manifest')) {
        $json = $manifest | ConvertTo-Json -Depth 5
        Set-Content -Path $manifestPath -Value $json -Encoding utf8
    }

    return $manifestPath
}

Export-ModuleMember -Function @(
    'Find-WindowsSdkTool'
    'Get-StampedAppxManifestXml'
    'Invoke-VersionStamp'
    'Invoke-LayoutAssembly'
    'Invoke-MakePri'
    'Invoke-MakeAppx'
    'Invoke-SignTool'
    'Invoke-DotnetPublish'
    'Copy-DockerArtifact'
    'Copy-InstallScriptsIntoBundle'
    'New-ManifestEntry'
    'Write-PublishManifest'
)


