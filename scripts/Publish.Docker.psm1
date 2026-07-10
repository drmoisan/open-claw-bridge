# Publish.Docker.psm1
# Publish-side docker helpers for scripts/Publish.ps1 (issue #142).
#
# Purpose
#   Move container-image acquisition from install time (pull/build, both
#   impossible on an offline target that lacks src/) to publish time. This
#   module builds the openclaw-core and openclaw-agent images, saves all four
#   refs into a single combined tar, and emits a transformed bundle compose
#   that can neither pull nor build.
#
# Build-vector / compose cross-reference (AC2)
#   The docker build vectors in Build-OpenClawDockerImage MUST mirror the
#   tracked docker-compose.yml `build:` blocks:
#     - openclaw-core  -> docker-compose.yml lines 5-10 (dockerfile, target
#       runtime, build-arg BUILD_CONFIGURATION), image line 11.
#     - openclaw-agent -> docker-compose.yml lines 53-57 (dockerfile, build-arg
#       OPENCLAW_AGENT_IMAGE), image line 58.
#   If the tracked compose `build:` args change, these vectors and the exact
#   argument-vector tests in tests/scripts/Publish.Docker.Tests.ps1 must change
#   in step; the tests turn drift into a failure.
#
# Policy notes
#   - PowerShell 7+; stays under 500 lines per repo policy.
#   - Every docker invocation routes through the module-scoped Invoke-DockerExe
#     seam so tests mock the wrapper, never the docker executable.
#   - State is passed in explicitly; no script-scoped mutable state.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-DockerExeResult {
    <#
    .SYNOPSIS
        Shapes a docker invocation's exit code and output into a result object.
    .DESCRIPTION
        Pure helper extracted from Invoke-DockerExe so the result-shaping logic is
        unit-testable without invoking the docker executable. The seam itself is the
        thinnest possible host-bound wiring (`& docker @DockerArgs`), left uncovered
        by design per the repo Coverage Exclusion Policy; this helper carries the
        testable logic.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,

        [AllowNull()]
        $RawOutput
    )

    return [pscustomobject]@{
        ExitCode = $ExitCode
        Output   = [string[]]@($RawOutput | Where-Object { $null -ne $_ } | ForEach-Object { [string]$_ })
    }
}

function Invoke-DockerExe {
    <#
    .SYNOPSIS
        External-executable wrapper seam for the docker CLI.
    .DESCRIPTION
        Runs `docker @DockerArgs 2>&1` and returns a deterministic result object
        { ExitCode; Output } via ConvertTo-DockerExeResult. This wrapper exists so
        callers can be unit-tested by mocking Invoke-DockerExe rather than the
        docker executable directly, per the repo design-seam rule. The parameter
        is named DockerArgs (not Args) to avoid the automatic-variable collision.
        Returning ExitCode explicitly (rather than relying on $LASTEXITCODE at the
        call site) keeps mocked-seam tests deterministic.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$DockerArgs
    )

    $output = & docker @DockerArgs 2>&1
    return ConvertTo-DockerExeResult -ExitCode $LASTEXITCODE -RawOutput $output
}

function Resolve-OpenClawAgentBaseImage {
    <#
    .SYNOPSIS
        Resolves the OPENCLAW_AGENT_IMAGE base image for the agent build.
    .DESCRIPTION
        Pure helper. Returns the trimmed OPENCLAW_AGENT_IMAGE value from the
        supplied env map when present and non-blank; otherwise returns the
        deploy/docker/openclaw-agent.Dockerfile ARG default
        'ghcr.io/openclaw/openclaw:latest'.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$EnvMap
    )

    # Default mirrors the ARG in deploy/docker/openclaw-agent.Dockerfile.
    $default = 'ghcr.io/openclaw/openclaw:latest'
    if ($EnvMap.Contains('OPENCLAW_AGENT_IMAGE')) {
        $value = [string]$EnvMap['OPENCLAW_AGENT_IMAGE']
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }
    return $default
}

function Build-OpenClawDockerImage {
    <#
    .SYNOPSIS
        Builds one openclaw image (core or agent) via the docker seam.
    .DESCRIPTION
        Composes the exact `docker build` argument vector mirroring the tracked
        docker-compose.yml `build:` block for the given -Kind, tags both the
        versioned ref and the floating pre-mvp ref, uses the repo root as the
        build context, and throws on a non-zero docker exit.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version,

        [Parameter(Mandatory = $true)]
        [ValidateSet('core', 'agent')]
        [string]$Kind,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [string]$AgentBaseImage = ''
    )

    if ($Kind -eq 'core') {
        $dockerfile = Join-Path $RepoRoot 'deploy/docker/openclaw-core.Dockerfile'
        $dockerArgs = @(
            'build'
            '-f', $dockerfile
            '--target', 'runtime'
            '--build-arg', "BUILD_CONFIGURATION=$Configuration"
            '-t', "openclaw/core:$Version"
            '-t', 'openclaw/core:pre-mvp'
            $RepoRoot
        )
        $imageRef = "openclaw/core:$Version"
    }
    else {
        $dockerfile = Join-Path $RepoRoot 'deploy/docker/openclaw-agent.Dockerfile'
        $dockerArgs = @(
            'build'
            '-f', $dockerfile
            '--build-arg', "OPENCLAW_AGENT_IMAGE=$AgentBaseImage"
            '-t', "openclaw/agent:$Version"
            '-t', 'openclaw/agent:pre-mvp'
            $RepoRoot
        )
        $imageRef = "openclaw/agent:$Version"
    }

    if ($PSCmdlet.ShouldProcess($imageRef, 'docker build')) {
        $result = Invoke-DockerExe -DockerArgs $dockerArgs
        if ($result.ExitCode -ne 0) {
            throw "docker build failed for image '$imageRef' (exit $($result.ExitCode)). Ensure Docker Desktop is running and that the base images can be pulled. Output: $($result.Output -join [Environment]::NewLine)"
        }
    }
}

function Save-OpenClawDockerImage {
    <#
    .SYNOPSIS
        Saves all four openclaw image refs into a single combined tar.
    .DESCRIPTION
        Issues one `docker save` naming the versioned and pre-mvp refs of both
        images, writing the combined tar to -OutputTarPath. Throws on non-zero
        docker exit. Shared layers are stored once in the combined tar.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version,

        [Parameter(Mandatory = $true)]
        [string]$OutputTarPath
    )

    $dockerArgs = @(
        'save'
        '-o', $OutputTarPath
        "openclaw/core:$Version"
        'openclaw/core:pre-mvp'
        "openclaw/agent:$Version"
        'openclaw/agent:pre-mvp'
    )

    if ($PSCmdlet.ShouldProcess($OutputTarPath, 'docker save')) {
        $result = Invoke-DockerExe -DockerArgs $dockerArgs
        if ($result.ExitCode -ne 0) {
            throw "docker save failed writing '$OutputTarPath' (exit $($result.ExitCode)). Output: $($result.Output -join [Environment]::NewLine)"
        }
    }
}

function Convert-ComposeToBundleCompose {
    <#
    .SYNOPSIS
        Transforms tracked compose lines into offline bundle compose lines.
    .DESCRIPTION
        Pure function. For openclaw-core and openclaw-agent, removes the
        4-space-indented `build:` key and its deeper-indented block, rewrites the
        4-space `image:` value to the versioned tag, and inserts `pull_policy:
        never` immediately after. Every other input line is preserved
        byte-for-byte. Throws (naming the service) when either service block does
        not contain exactly one `build:` key and exactly one `image:` key, so a
        drifted compose fails fast rather than emitting a partial bundle compose.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$ComposeContent,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version
    )

    $imageForService = @{
        'openclaw-core'  = 'openclaw/core'
        'openclaw-agent' = 'openclaw/agent'
    }
    $buildCounts = @{ 'openclaw-core' = 0; 'openclaw-agent' = 0 }
    $imageCounts = @{ 'openclaw-core' = 0; 'openclaw-agent' = 0 }

    $result = [System.Collections.Generic.List[string]]::new()
    $current = $null
    $removingBuild = $false

    foreach ($line in $ComposeContent) {
        $indent = $line.Length - ($line.TrimStart(' ')).Length

        # Structural context: top-level keys (indent 0) reset the service; a
        # 2-space-indented key is a service name.
        if ($indent -eq 0 -and $line.Trim().Length -gt 0) {
            $current = $null
            $removingBuild = $false
        }
        elseif ($indent -eq 2 -and $line.Trim().Length -gt 0) {
            $removingBuild = $false
            $name = $line.Trim().TrimEnd(':')
            if ($imageForService.Contains($name)) { $current = $name } else { $current = 'other' }
        }

        # Drop the deeper-indented body of a build block.
        if ($removingBuild) {
            if ($indent -gt 4) { continue }
            $removingBuild = $false
        }

        if ($current -eq 'openclaw-core' -or $current -eq 'openclaw-agent') {
            if ($indent -eq 4 -and $line -match '^ {4}build:\s*$') {
                $buildCounts[$current]++
                $removingBuild = $true
                continue
            }
            if ($indent -eq 4 -and $line -match '^ {4}image:\s*') {
                $imageCounts[$current]++
                $result.Add("    image: $($imageForService[$current]):$Version")
                $result.Add('    pull_policy: never')
                continue
            }
        }

        $result.Add($line)
    }

    foreach ($svc in @('openclaw-core', 'openclaw-agent')) {
        if ($buildCounts[$svc] -ne 1 -or $imageCounts[$svc] -ne 1) {
            throw "Compose transform drift for service '$svc': expected exactly one 4-space 'build:' key and one 4-space 'image:' key (found build=$($buildCounts[$svc]), image=$($imageCounts[$svc])). Refusing to emit a partial bundle compose (issue #142). Reconcile docker-compose.yml with Convert-ComposeToBundleCompose."
        }
    }

    return $result.ToArray()
}

function Write-BundleCompose {
    <#
    .SYNOPSIS
        Writes the transformed bundle compose file.
    .DESCRIPTION
        Thin writer: reads the tracked compose via Get-Content -LiteralPath, calls
        the pure Convert-ComposeToBundleCompose, and writes the result to
        -BundleComposePath under ShouldProcess. All transform logic stays in the
        pure function.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TrackedComposePath,

        [Parameter(Mandatory = $true)]
        [string]$BundleComposePath,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version
    )

    $trackedLines = Get-Content -LiteralPath $TrackedComposePath
    $bundleLines = Convert-ComposeToBundleCompose -ComposeContent $trackedLines -Version $Version

    if ($PSCmdlet.ShouldProcess($BundleComposePath, 'Write transformed bundle docker-compose.yml')) {
        Set-Content -LiteralPath $BundleComposePath -Value $bundleLines -Encoding utf8
    }
}

function Invoke-PublishDockerStage {
    <#
    .SYNOPSIS
        Publish Stage 3b facade: build images, save the tar, write bundle compose.
    .DESCRIPTION
        Resolves the agent base image, builds openclaw-core then openclaw-agent,
        saves all four refs into <BundleDockerDir>/openclaw-images.tar, then writes
        the transformed bundle compose. Exposed as a single facade so the
        Publish.ps1 orchestrator adds one call and one test mock.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$BundleDockerDir,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
        [string]$Version,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$EnvMap
    )

    $agentBaseImage = Resolve-OpenClawAgentBaseImage -EnvMap $EnvMap

    Build-OpenClawDockerImage -RepoRoot $RepoRoot -Version $Version -Kind 'core' -Configuration $Configuration
    Build-OpenClawDockerImage -RepoRoot $RepoRoot -Version $Version -Kind 'agent' -Configuration $Configuration -AgentBaseImage $agentBaseImage

    $tarPath = Join-Path $BundleDockerDir 'openclaw-images.tar'
    Save-OpenClawDockerImage -Version $Version -OutputTarPath $tarPath

    $trackedCompose = Join-Path $RepoRoot 'docker-compose.yml'
    $bundleCompose = Join-Path $BundleDockerDir 'docker-compose.yml'
    Write-BundleCompose -TrackedComposePath $trackedCompose -BundleComposePath $bundleCompose -Version $Version
}

Export-ModuleMember -Function @(
    'Invoke-DockerExe'
    'Resolve-OpenClawAgentBaseImage'
    'Build-OpenClawDockerImage'
    'Save-OpenClawDockerImage'
    'Convert-ComposeToBundleCompose'
    'Write-BundleCompose'
    'Invoke-PublishDockerStage'
)
