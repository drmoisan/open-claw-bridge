# Install.Helpers.psm1
# Shared helper module for scripts/Install.ps1 and scripts/Uninstall.ps1.
# Centralizes newest-version discovery, manifest integrity, bundle copy, .env
# guard, MSIX shims, docker readiness + compose shims, and install-record I/O.
# PowerShell 7+, stays under 500 lines. Planner decision Q1 is captured in
# Wait-ComposeHealthy defaults (-TimeoutSeconds 90, -PollIntervalSeconds 3).
# Every state-changing helper uses SupportsShouldProcess for -WhatIf.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-NewestPublishVersion {
    <#
    .SYNOPSIS
        Returns the highest [System.Version] subdirectory under a publish root.
    .DESCRIPTION
        Returns a pscustomobject with Version and Path. Throws when no
        parseable directory exists; the thrown message names $PublishRoot.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishRoot
    )

    $candidates = Get-ChildItem -LiteralPath $PublishRoot -ErrorAction Stop |
        Where-Object { $_.PSIsContainer } |
            ForEach-Object {
                $v = $null
                if ([System.Version]::TryParse($_.Name, [ref]$v)) {
                    [pscustomobject]@{ Version = $v; Path = $_.FullName }
                }
            } |
                Sort-Object -Property Version -Descending

    $winner = @($candidates) | Select-Object -First 1
    if (-not $winner) {
        throw "No version-named subdirectory found under '$PublishRoot'. Expected at least one folder whose name parses as [System.Version] (for example '1.2.3.0')."
    }

    return $winner
}

function Test-ManifestIntegrity {
    <#
    .SYNOPSIS
        Verifies every file under $BundleRoot matches manifest.json by path,
        size, and SHA-256, and that no on-disk file is absent from the manifest.
    .DESCRIPTION
        Accumulates every discrepancy into a single array and throws one
        terminating error listing all of them when non-empty.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundleRoot
    )

    $manifestPath = Join-Path $BundleRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "manifest.json not found at '$manifestPath'. The bundle at '$BundleRoot' is missing its manifest."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

    $discrepancies = [System.Collections.ArrayList]::new()
    $manifestRelative = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($entry in @($manifest.files)) {
        $relNormalized = $entry.path -replace '/', [IO.Path]::DirectorySeparatorChar
        [void]$manifestRelative.Add($entry.path)

        $fullPath = Join-Path $BundleRoot $relNormalized

        if (-not (Test-Path -LiteralPath $fullPath)) {
            [void]$discrepancies.Add("Missing file: '$($entry.path)' (expected at '$fullPath')")
            continue
        }

        $item = Get-Item -LiteralPath $fullPath
        $onDiskSize = [long]$item.Length
        $expectedSize = [long]$entry.size
        if ($onDiskSize -ne $expectedSize) {
            [void]$discrepancies.Add("Size mismatch: '$($entry.path)' (manifest=$expectedSize, disk=$onDiskSize)")
        }

        $onDiskHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = ($entry.sha256).ToLowerInvariant()
        if ($onDiskHash -ne $expectedHash) {
            [void]$discrepancies.Add("SHA-256 mismatch: '$($entry.path)' (manifest=$expectedHash, disk=$onDiskHash)")
        }
    }

    $diskFiles = Get-ChildItem -LiteralPath $BundleRoot -Recurse -File -ErrorAction Stop
    foreach ($file in $diskFiles) {
        if ($file.FullName -ieq $manifestPath) { continue }

        $rel = $file.FullName.Substring($BundleRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar, '/')
        $relForward = $rel -replace [regex]::Escape([string][IO.Path]::DirectorySeparatorChar), '/'
        if (-not $manifestRelative.Contains($relForward)) {
            [void]$discrepancies.Add("Unlisted file on disk: '$relForward' (present under bundle root, absent from manifest)")
        }
    }

    if ($discrepancies.Count -gt 0) {
        $joined = ($discrepancies -join [Environment]::NewLine)
        throw ("Manifest integrity check failed for bundle '$BundleRoot'. Discrepancies:" + [Environment]::NewLine + $joined)
    }
}

function Copy-BundleContents {
    <#
    .SYNOPSIS
        Copies executables/ and docker/ subtrees from a bundle root to a
        destination root, preserving relative paths.
    .DESCRIPTION
        Creates each destination subdirectory and recursively copies under
        ShouldProcess. Caller ensures $DestinationRoot exists.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseSingularNouns', '',
        Justification = 'The spec and plan mandate the noun "Contents" (plural) because the helper copies multiple subtrees (executables/ and docker/). Renaming would breach the planner-approved API surface.'
    )]
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceBundleRoot,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    foreach ($sub in @('executables', 'docker')) {
        $srcDir = Join-Path $SourceBundleRoot $sub
        $dstDir = Join-Path $DestinationRoot $sub

        if ($PSCmdlet.ShouldProcess($dstDir, "Create destination directory")) {
            $null = New-Item -ItemType Directory -Path $dstDir -Force
        }
        if ($PSCmdlet.ShouldProcess($srcDir, "Copy bundle subtree to $dstDir")) {
            Copy-Item -Path (Join-Path $srcDir '*') -Destination $dstDir -Recurse -Force
        }
    }
}

function Initialize-DotEnv {
    <#
    .SYNOPSIS
        Copies .env.example to .env when .env is absent; never overwrites an
        existing .env and emits no warning when skipping.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestDockerDir
    )

    $destEnv = Join-Path $DestDockerDir '.env'
    $srcExample = Join-Path $DestDockerDir '.env.example'

    if (Test-Path -LiteralPath $destEnv) {
        return
    }

    if ($PSCmdlet.ShouldProcess($destEnv, "Copy .env.example to .env")) {
        Copy-Item -LiteralPath $srcExample -Destination $destEnv
    }
}

function Invoke-MsixInstall {
    <#
    .SYNOPSIS
        Installs an MSIX package via Add-AppxPackage, optionally -AllowUnsigned.
        Re-throws with context that names MsixPath and the AllowUnsigned state.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$MsixPath,

        [switch]$AllowUnsigned
    )

    if (-not $PSCmdlet.ShouldProcess($MsixPath, "Add-AppxPackage")) {
        return
    }

    try {
        if ($AllowUnsigned) {
            Add-AppxPackage -Path $MsixPath -AllowUnsigned
        }
        else {
            Add-AppxPackage -Path $MsixPath
        }
    }
    catch {
        throw "Failed to install MSIX at '$MsixPath' (AllowUnsigned=$([bool]$AllowUnsigned)): $($_.Exception.Message)"
    }
}

function Invoke-MsixCapture {
    <#
    .SYNOPSIS
        Returns PackageFullName of the installed OpenClaw.MailBridge MSIX.
        Throws when no matching package is present.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $pkg = Get-AppxPackage -Name 'OpenClaw.MailBridge' -ErrorAction SilentlyContinue
    if (-not $pkg) {
        throw "No installed MSIX found with identity 'OpenClaw.MailBridge'. Add-AppxPackage may have failed silently or the package may already have been removed."
    }

    return $pkg.PackageFullName
}

function Invoke-MsixRemove {
    <#
    .SYNOPSIS
        Removes the OpenClaw.MailBridge MSIX. Silent no-op when the package is
        already absent.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageFullName
    )

    $pkg = Get-AppxPackage -Name 'OpenClaw.MailBridge' -ErrorAction SilentlyContinue
    if (-not $pkg) {
        return
    }

    if ($PSCmdlet.ShouldProcess($PackageFullName, 'Remove-AppxPackage')) {
        Remove-AppxPackage -Package $PackageFullName
    }
}

function Test-DockerAvailable {
    <#
    .SYNOPSIS
        Returns $true when `docker info` succeeds; throws with -SkipDocker
        remediation otherwise.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $null = & docker info 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop is not running or not installed. Start Docker Desktop and retry, or pass -SkipDocker to skip the container stage.'
    }
    return $true
}

function Invoke-ComposeUp {
    <#
    .SYNOPSIS
        Runs `docker compose ... up -d openclaw-core openclaw-agent` with
        explicit --project-name, --project-directory, and -f flags.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestDockerDir,

        [Parameter(Mandatory = $true)]
        [string]$ComposeFilePath,

        [string]$ProjectName = 'openclaw'
    )

    if (-not $PSCmdlet.ShouldProcess($ComposeFilePath, "docker compose up -d openclaw-core openclaw-agent")) {
        return
    }

    & docker compose --project-name $ProjectName --project-directory $DestDockerDir -f $ComposeFilePath up -d openclaw-core openclaw-agent
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed (exit $LASTEXITCODE) for compose file '$ComposeFilePath'. Ensure Docker Desktop is running and images are available."
    }
}

function Wait-ComposeHealthy {
    <#
    .SYNOPSIS
        Polls `docker compose ps --format json` until openclaw-core and
        openclaw-agent both report running + healthy (or healthcheck absent).
    .DESCRIPTION
        Planner decision Q1: defaults are -TimeoutSeconds 90 and
        -PollIntervalSeconds 3. The 90-second ceiling provides a 60-second
        safety margin above the longer docker-compose.yml start_period (30s
        on openclaw-agent). Emits Write-Information with elapsed seconds per
        cycle. Throws on timeout with the failing service name.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ComposeFilePath,
        [string]$ProjectName = 'openclaw',
        [int]$TimeoutSeconds = 90,
        [int]$PollIntervalSeconds = 3
    )
    $requiredServices = @('openclaw-core', 'openclaw-agent')
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = @{}
    while ((Get-Date) -lt $deadline) {
        $elapsed = [int](($TimeoutSeconds) - ($deadline - (Get-Date)).TotalSeconds)
        Write-Information "[compose:health] elapsed=${elapsed}s (timeout=${TimeoutSeconds}s)" -InformationAction Continue
        $raw = & docker compose --project-name $ProjectName -f $ComposeFilePath ps --format json 2>$null
        if ($LASTEXITCODE -ne 0) {
            Start-Sleep -Seconds $PollIntervalSeconds
            continue
        }
        $entries = @()
        try {
            # Normalize an array of lines or a raw string to a single trimmed payload.
            $payload = if ($raw -is [array]) { ($raw -join "`n").Trim() } else { ([string]$raw).Trim() }
            if ($payload) {
                # docker compose may emit either a JSON array or one JSON object per line.
                if ($payload.StartsWith('[')) {
                    $entries = @($payload | ConvertFrom-Json)
                }
                else {
                    $entries = @(($payload -split "`r?`n" | Where-Object { $_ }) | ForEach-Object { $_ | ConvertFrom-Json })
                }
            }
        }
        catch {
            $entries = @()
        }
        $allReady = $true
        foreach ($svc in $requiredServices) {
            $entry = $entries | Where-Object { $_.Service -eq $svc } | Select-Object -First 1
            if (-not $entry) {
                $allReady = $false
                $lastState[$svc] = @{ State = '(absent)'; Health = '(absent)' }
                continue
            }
            $state = $entry.State
            $health = if ($null -eq $entry.Health) { '' } else { [string]$entry.Health }
            $lastState[$svc] = @{ State = $state; Health = $health }
            $running = ($state -eq 'running')
            $healthy = ([string]::IsNullOrEmpty($health)) -or ($health -eq 'healthy')
            if (-not ($running -and $healthy)) { $allReady = $false }
        }
        if ($allReady) { return }
        Start-Sleep -Seconds $PollIntervalSeconds
    }
    $failing = $lastState.Keys |
        Where-Object { $lastState[$_].State -ne 'running' -or (-not [string]::IsNullOrEmpty($lastState[$_].Health) -and $lastState[$_].Health -ne 'healthy') } |
            Select-Object -First 1
    if (-not $failing) {
        $failing = 'openclaw-core'
    }
    $s = $lastState[$failing].State
    $h = $lastState[$failing].Health
    throw "Timed out after ${TimeoutSeconds}s waiting for '$failing' to reach running + healthy. Last observed State='$s' Health='$h'. Check 'docker compose logs' for the service."
}

function Invoke-ComposeDown {
    <#
    .SYNOPSIS
        Runs `docker compose down` at uninstall time. No --volumes flag;
        openclaw_data volume is preserved per spec.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ComposeFilePath,

        [string]$ProjectName = 'openclaw'
    )

    if (-not $PSCmdlet.ShouldProcess($ComposeFilePath, "docker compose down")) {
        return
    }

    & docker compose --project-name $ProjectName -f $ComposeFilePath down
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose down failed (exit $LASTEXITCODE) for compose file '$ComposeFilePath'."
    }
}

function Write-InstallRecord {
    <#
    .SYNOPSIS
        Serializes $Record as JSON and writes it to $RecordPath (UTF-8).
        Ensures the parent directory exists and overwrites any prior record.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Record,

        [Parameter(Mandatory = $true)]
        [string]$RecordPath
    )

    $parent = Split-Path -Path $RecordPath -Parent
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        if ($PSCmdlet.ShouldProcess($parent, 'Create install-record parent directory')) {
            $null = New-Item -ItemType Directory -Path $parent -Force
        }
    }

    $json = $Record | ConvertTo-Json -Depth 5
    if ($PSCmdlet.ShouldProcess($RecordPath, 'Write install record')) {
        Set-Content -LiteralPath $RecordPath -Value $json -Encoding utf8
    }
}

function Read-InstallRecord {
    <#
    .SYNOPSIS
        Reads and parses the install record. Throws a clear "no prior install
        recorded" message when the file is absent.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RecordPath
    )

    if (-not (Test-Path -LiteralPath $RecordPath)) {
        throw "No prior install recorded. Expected install record at '$RecordPath'. Run Install.ps1 before attempting uninstall."
    }

    return Get-Content -LiteralPath $RecordPath -Raw | ConvertFrom-Json
}

Export-ModuleMember -Function `
    'Find-NewestPublishVersion', `
    'Test-ManifestIntegrity', `
    'Copy-BundleContents', `
    'Initialize-DotEnv', `
    'Invoke-MsixInstall', `
    'Invoke-MsixCapture', `
    'Invoke-MsixRemove', `
    'Test-DockerAvailable', `
    'Invoke-ComposeUp', `
    'Wait-ComposeHealthy', `
    'Invoke-ComposeDown', `
    'Write-InstallRecord', `
    'Read-InstallRecord'
