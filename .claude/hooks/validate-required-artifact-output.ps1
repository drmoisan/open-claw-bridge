<#
.SYNOPSIS
    Generic SubagentStop hook for agents that must advertise exact artifact paths.

.DESCRIPTION
    Validates that the hook payload is well-formed JSON, the agent output is
    non-empty, each required artifact token is advertised in the output, the
    advertised path matches the configured contract regex, and the file exists
    on disk.

    Each required artifact spec must use:
        token|regex|description

    Example:
        spec-path|^docs/features/active/.+/spec\.md$|spec artifact

.NOTES
    Reads the hook payload from CLAUDE_HOOK_INPUT as JSON. Exits 0 to allow
    termination; exits 1 with an error message to block. Filesystem existence
    checks go through Test-ArtifactFile so tests can mock the boundary without
    writing temporary files.
#>

[CmdletBinding()]
param(
    [string] $AgentName,

    [string[]] $RequiredArtifact
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-ArtifactFile {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return [bool](Test-Path -LiteralPath $Path -PathType Leaf)
}

function ConvertTo-ArtifactSpec {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Spec
    )

    $parts = $Spec -split '\|', 3
    if ($parts.Count -ne 3) {
        throw "Invalid artifact spec '$Spec'. Expected format: token|regex|description."
    }

    return @{
        Token       = $parts[0].Trim()
        Pattern     = $parts[1].Trim()
        Description = $parts[2].Trim()
    }
}

function Get-ArtifactPathFromOutput {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput,

        [Parameter(Mandatory = $true)]
        [string] $Token
    )

    $escapedToken = [regex]::Escape($Token)
    $pattern = "$escapedToken\s*[:=]\s*[""']?([^\s""'\)`]+)|\[$escapedToken\]\(([^)]+)\)"
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

function Invoke-RequiredArtifactOutputValidation {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string] $RawPayload,
        [string] $AgentName,
        [string[]] $RequiredArtifact
    )

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return @{ Ok = $false; Message = "$AgentName hook: CLAUDE_HOOK_INPUT is empty; cannot validate required artifact output." }
    }

    try {
        $payload = $RawPayload | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return @{ Ok = $false; Message = "$AgentName hook: failed to parse CLAUDE_HOOK_INPUT as JSON: $($_.Exception.Message)" }
    }

    $agentOutput = $null
    if ($payload.PSObject.Properties.Name -contains 'output') {
        $agentOutput = $payload.output
    }
    if ([string]::IsNullOrWhiteSpace($agentOutput)) {
        return @{ Ok = $false; Message = "$AgentName hook: agent output is empty; artifact paths must be reported before termination." }
    }

    $errors = [System.Collections.Generic.List[string]]::new()
    foreach ($specText in $RequiredArtifact) {
        try {
            $spec = ConvertTo-ArtifactSpec -Spec $specText
        }
        catch {
            return @{ Ok = $false; Message = "$AgentName hook: $($_.Exception.Message)" }
        }

        $path = Get-ArtifactPathFromOutput -AgentOutput $agentOutput -Token $spec.Token
        if ($null -eq $path) {
            $errors.Add("missing $($spec.Token): <path> for the required $($spec.Description)")
            continue
        }

        if (-not [regex]::IsMatch($path, $spec.Pattern)) {
            $errors.Add("$($spec.Token) '$path' does not match the required location for the $($spec.Description)")
            continue
        }

        if (-not (Test-ArtifactFile -Path $path)) {
            $errors.Add("$($spec.Token) '$path' was advertised for the $($spec.Description) but no file exists at that location")
        }
    }

    if ($errors.Count -gt 0) {
        $message = "$AgentName hook: required artifact validation failed:`n  - " + ($errors -join "`n  - ")
        return @{ Ok = $false; Message = $message }
    }

    return @{ Ok = $true; Message = $null }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

if ([string]::IsNullOrWhiteSpace($AgentName) -or $null -eq $RequiredArtifact -or $RequiredArtifact.Count -eq 0) {
    Write-Error 'validate-required-artifact-output hook: AgentName and at least one RequiredArtifact spec are required.'
    exit 1
}

$result = Invoke-RequiredArtifactOutputValidation `
    -RawPayload $env:CLAUDE_HOOK_INPUT `
    -AgentName $AgentName `
    -RequiredArtifact $RequiredArtifact
if (-not $result.Ok) {
    Write-Error $result.Message
    exit 1
}

exit 0
