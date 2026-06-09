<#
.SYNOPSIS
    Pre-tool-use hook that blocks writes to non-canonical evidence storage locations.

.DESCRIPTION
    This script is invoked by the Claude Code PreToolUse hook before any Write or Edit
    operation. It reads the tool input from the CLAUDE_TOOL_INPUT environment variable
    (JSON with a 'file_path' field) and rejects the operation when the target path is
    a non-canonical evidence location.

    Forbidden path prefixes (case-sensitive, normalized to forward-slash):
      - artifacts/baselines/
      - artifacts/baseline/
      - artifacts/qa/
      - artifacts/qa-gates/
      - artifacts/coverage/
      - artifacts/evidence/
      - artifacts/regression-testing/
      - artifacts/post-change/

    All other paths pass through, including canonical evidence paths of the form
    <FEATURE>/evidence/<kind>/ and permitted artifacts/ sub-paths such as
    artifacts/orchestration/, artifacts/research/, artifacts/pr_context,
    artifacts/reviews/, artifacts/status/, artifacts/python/, artifacts/pester/,
    and artifacts/csharp/.

    If the file_path resolves to a forbidden prefix, the script writes a JSON response
    to stdout with 'decision': 'block' and exits with code 0 so Claude Code surfaces
    the reason. For allowed paths, 'decision': 'allow' is written to stdout and the
    script exits 0. On hard failure (malformed JSON input), the script exits 1.

.NOTES
    Compatible with PowerShell 7+.
    This script must not modify any state; it is a read-only validation gate.
#>
[CmdletBinding()]
param()

function Test-EvidenceLocationForbidden {
    <#
    .SYNOPSIS
        Returns $true when the supplied file path targets a forbidden evidence sub-path.
    .PARAMETER FilePath
        The raw file_path value from the Claude Code tool-input JSON.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $FilePath
    )

    # Normalize separators so both absolute Windows paths and relative POSIX paths match.
    $normalized = $FilePath -replace '\\', '/'

    $forbiddenPrefixes = @(
        'artifacts/baselines/',
        'artifacts/baseline/',
        'artifacts/qa/',
        'artifacts/qa-gates/',
        'artifacts/coverage/',
        'artifacts/evidence/',
        'artifacts/regression-testing/',
        'artifacts/post-change/'
    )

    # Match the prefix either at the start of the string or after any directory separator,
    # to handle both relative and absolute path forms.
    foreach ($prefix in $forbiddenPrefixes) {
        $escapedPrefix = [regex]::Escape($prefix)
        if ($normalized -match "(^|/)$escapedPrefix") {
            return $true
        }
    }

    return $false
}

function Get-EvidenceLocationBlockDecision {
    <#
    .SYNOPSIS
        Constructs a block-decision ordered dictionary for the supplied forbidden path.
    .PARAMETER FilePath
        The file path that triggered the block.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)]
        [string] $FilePath
    )

    [ordered]@{
        decision = 'block'
        reason   = "EVIDENCE_LOCATION_BLOCKED: '$FilePath' is not a canonical evidence location. Use <FEATURE>/evidence/<kind>/ instead. See .claude/skills/evidence-and-timestamp-conventions/SKILL.md for the canonical scheme."
    }
}

function Invoke-EvidenceLocationDecision {
    <#
    .SYNOPSIS
        Parses the Claude Code tool-input JSON and returns an allow-or-block decision.
    .PARAMETER ToolInputRaw
        The raw JSON string from $env:CLAUDE_TOOL_INPUT. An empty or null value
        results in an allow decision (non-file tool calls have no file_path).
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $ToolInputRaw
    )

    if (-not $ToolInputRaw) {
        return [ordered]@{ decision = 'allow' }
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        # Malformed JSON is a hard failure; caller exits 1 to surface the issue.
        throw "enforce-evidence-locations hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $filePath = $toolInput.file_path
    if (-not $filePath) {
        return [ordered]@{ decision = 'allow' }
    }

    if (Test-EvidenceLocationForbidden -FilePath $filePath) {
        return Get-EvidenceLocationBlockDecision -FilePath $filePath
    }

    return [ordered]@{ decision = 'allow' }
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-EvidenceLocationDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress | Write-Output

exit 0
