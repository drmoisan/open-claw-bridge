<#
.SYNOPSIS
    SubagentStop hook for the task-researcher subagent.

.DESCRIPTION
    Blocks termination of the task-researcher subagent unless the agent's
    final output advertises a `research-path` token, the path is rooted
    under artifacts/research/, matches the documented filename convention,
    and the file exists on disk.

.NOTES
    Reads the hook payload from CLAUDE_HOOK_INPUT as JSON. Exits 0 to allow
    termination; exits 1 with an error message to block. Filesystem reads go
    through Test-ResearchFile so tests can mock the boundary without writing
    temporary files.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-ResearchFile {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return [bool](Test-Path -LiteralPath $Path -PathType Leaf)
}

function Get-ResearchPathFromOutput {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput
    )

    $pattern = 'research-path\s*[:=]\s*["'']?([^\s"''\)`]+)|\[research-path\]\(([^)]+)\)'
    $match = [regex]::Match($AgentOutput, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        return $null
    }

    $value = if ($match.Groups[1].Success) { $match.Groups[1].Value } else { $match.Groups[2].Value }
    $value = $value.Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value
}

function Test-IsUnderResearchRoot {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalized = $Path -replace '\\', '/'
    return $normalized.StartsWith('artifacts/research/', [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-IsValidResearchFileName {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalized = $Path -replace '\\', '/'
    $fileName = [System.IO.Path]::GetFileName($normalized)
    return [regex]::IsMatch(
        $fileName,
        '^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-[A-Za-z0-9][A-Za-z0-9-]*-research\.md$'
    )
}

function Invoke-TaskResearcherOutputValidation {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string] $RawPayload
    )

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return @{ Ok = $false; Message = 'task-researcher hook: CLAUDE_HOOK_INPUT is empty; cannot validate task-researcher output.' }
    }

    try {
        $payload = $RawPayload | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return @{ Ok = $false; Message = "task-researcher hook: failed to parse CLAUDE_HOOK_INPUT as JSON: $($_.Exception.Message)" }
    }

    $agentOutput = $null
    if ($payload.PSObject.Properties.Name -contains 'output') {
        $agentOutput = $payload.output
    }
    if ([string]::IsNullOrWhiteSpace($agentOutput)) {
        return @{ Ok = $false; Message = 'task-researcher hook: agent output is empty; researcher must return research-path before termination.' }
    }

    $researchPath = Get-ResearchPathFromOutput -AgentOutput $agentOutput
    if ($null -eq $researchPath) {
        return @{ Ok = $false; Message = 'task-researcher hook: agent output does not advertise a research-path. Researcher must report `research-path: <path>` pointing to artifacts/research/.' }
    }

    if (-not (Test-IsUnderResearchRoot -Path $researchPath)) {
        return @{ Ok = $false; Message = "task-researcher hook: research-path '$researchPath' is not under artifacts/research/. All research artifacts must be written to artifacts/research/." }
    }

    if (-not (Test-IsValidResearchFileName -Path $researchPath)) {
        return @{ Ok = $false; Message = "task-researcher hook: research-path '$researchPath' does not match the required filename convention `artifacts/research/<timestamp>-<short-name>-research.md`." }
    }

    if (-not (Test-ResearchFile -Path $researchPath)) {
        return @{ Ok = $false; Message = "task-researcher hook: researcher advertised research-path '$researchPath' but no file exists at that location." }
    }

    return @{ Ok = $true; Message = $null }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$result = Invoke-TaskResearcherOutputValidation -RawPayload $env:CLAUDE_HOOK_INPUT
if (-not $result.Ok) {
    Write-Error $result.Message
    exit 1
}

exit 0
