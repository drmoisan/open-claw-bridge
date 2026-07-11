# Install.Docker.psm1
# Install-side docker helpers for scripts/Install.ps1 (issue #142).
#
# Purpose
#   Load the bundled combined image tar (produced at publish time by
#   Publish.Docker.psm1) into the local docker image store before compose up,
#   so the offline install path never pulls or builds.
#
# Self-containment
#   Install.ps1 imports only files staged inside the bundle, so this module must
#   not import any other repo module. It therefore defines its own module-scoped
#   Invoke-DockerExe seam rather than sharing the publish module's.
#
# Policy notes
#   - PowerShell 7+; stays under 500 lines per repo policy.
#   - Every docker invocation routes through the module-scoped Invoke-DockerExe
#     seam so tests mock the wrapper, never the docker executable.

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
        External-executable wrapper seam for the docker CLI (install side).
    .DESCRIPTION
        Runs `docker @DockerArgs 2>&1` and returns a deterministic result object
        { ExitCode; Output } via ConvertTo-DockerExeResult. Callers are unit-tested
        by mocking this wrapper rather than the docker executable, per the repo
        design-seam rule. The parameter is named DockerArgs (not Args) to avoid
        the automatic-variable collision.
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

function Invoke-DockerImageLoad {
    <#
    .SYNOPSIS
        Loads the bundled combined image tar into the local docker image store.
    .DESCRIPTION
        Throws with remediation text naming the expected tar path when the tar is
        missing (directing the operator to re-run scripts/Publish.ps1 to produce a
        bundle containing docker/openclaw-images.tar). Otherwise, under
        ShouldProcess, runs `docker load -i <ImageTarPath>` via the seam and throws
        on a non-zero docker exit, including the docker output in the message.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ImageTarPath
    )

    if (-not (Test-Path -LiteralPath $ImageTarPath)) {
        throw "Bundled container image tar not found at '$ImageTarPath'. This bundle was not produced with prebuilt images. Re-run scripts/Publish.ps1 to produce a bundle whose docker/ directory contains 'openclaw-images.tar', then re-run Install.ps1."
    }

    if ($PSCmdlet.ShouldProcess($ImageTarPath, 'docker load')) {
        $result = Invoke-DockerExe -DockerArgs @('load', '-i', $ImageTarPath)
        if ($result.ExitCode -ne 0) {
            throw "docker load failed for '$ImageTarPath' (exit $($result.ExitCode)). Output: $($result.Output -join [Environment]::NewLine)"
        }
    }
}

Export-ModuleMember -Function @(
    'Invoke-DockerExe'
    'Invoke-DockerImageLoad'
)
