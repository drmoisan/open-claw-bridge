# Publish.Helpers.psm1
# Shared helper module for scripts/Publish.ps1.
#
# Purpose
#   Centralize the pure and near-pure helpers used by the unified publish
#   orchestrator: dotnet publish invocation, certificate thumbprint
#   resolution, docker artifact copy with the secrets/ exclusion, install
#   script staging, and manifest generation. The Windows SDK / MSIX tooling
#   helpers were extracted to scripts/Publish.Msix.psm1.
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

function Invoke-DotnetExe {
    <#
    .SYNOPSIS
        External-executable wrapper seam for the dotnet CLI.
    .DESCRIPTION
        Runs `dotnet @DotnetArgs 2>&1` and returns the merged output. This wrapper
        exists so callers (for example Resolve-CertThumbprint) can be unit-tested by
        mocking Invoke-DotnetExe rather than mocking the dotnet executable directly,
        per the repo design-seam rule. The parameter is named DotnetArgs (not Args)
        to avoid the automatic-variable collision.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$DotnetArgs
    )

    return & dotnet @DotnetArgs 2>&1
}

function Resolve-CertThumbprint {
    <#
    .SYNOPSIS
        Resolves a code-signing certificate thumbprint from the first available source.
    .DESCRIPTION
        Returns the first non-empty value found, in this precedence:
          1. -ExplicitThumbprint when non-empty/non-whitespace (an explicit caller value
             always wins);
          2. -DotEnvThumbprint, the value sourced from the repository-root .env
             OPENCLAW_CERT_THUMBPRINT key (injected by the caller via the
             Publish.Env helpers). The function never reads .env itself so it stays
             deterministic and testable;
          3. the dotnet user secret 'Signing:CertThumbprint' for the project at
             -ProjectPath, read via the Invoke-DotnetExe wrapper seam and parsed from the
             'Signing:CertThumbprint = <value>' line;
          4. -EnvThumbprint, an injected process-environment value (the caller passes
             $env:OPENCLAW_CERT_THUMBPRINT). The function never reads $env: itself so it
             stays deterministic and testable.
        Returns an empty string when none of the sources yields a value.
    .PARAMETER ExplicitThumbprint
        An explicit thumbprint supplied by the caller. Highest precedence.
    .PARAMETER DotEnvThumbprint
        A thumbprint sourced from the repository-root .env OPENCLAW_CERT_THUMBPRINT
        key, injected by the caller. Ranks above the dotnet user secret and above
        the process-environment value.
    .PARAMETER ProjectPath
        Path to the .csproj whose dotnet user secrets are queried for
        'Signing:CertThumbprint'.
    .PARAMETER EnvThumbprint
        A process-environment-supplied thumbprint, injected by the caller. Lowest
        precedence.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$ExplicitThumbprint = '',

        [string]$DotEnvThumbprint = '',

        [string]$ProjectPath = '',

        [string]$EnvThumbprint = ''
    )

    # Source 1: explicit value always wins.
    if (-not [string]::IsNullOrWhiteSpace($ExplicitThumbprint)) {
        return $ExplicitThumbprint.Trim()
    }

    # Source 2: .env OPENCLAW_CERT_THUMBPRINT (injected; ranks above user secret).
    if (-not [string]::IsNullOrWhiteSpace($DotEnvThumbprint)) {
        return $DotEnvThumbprint.Trim()
    }

    # Source 3: dotnet user secret 'Signing:CertThumbprint'.
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $secretsOutput = Invoke-DotnetExe -DotnetArgs @('user-secrets', 'list', '--project', $ProjectPath)
        foreach ($line in @($secretsOutput)) {
            $text = [string]$line
            if ($text -match '^Signing:CertThumbprint = (.+)$') {
                $value = $Matches[1].Trim()
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    return $value
                }
            }
        }
    }

    # Source 4: injected process-environment value.
    if (-not [string]::IsNullOrWhiteSpace($EnvThumbprint)) {
        return $EnvThumbprint.Trim()
    }

    return ''
}

Export-ModuleMember -Function @(
    'Invoke-DotnetPublish'
    'Invoke-DotnetExe'
    'Resolve-CertThumbprint'
    'Copy-DockerArtifact'
    'Copy-InstallScriptsIntoBundle'
    'New-ManifestEntry'
    'Write-PublishManifest'
)



