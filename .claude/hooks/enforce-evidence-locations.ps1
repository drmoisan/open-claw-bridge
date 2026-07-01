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
      - artifacts/research/

    All other paths pass through, including canonical evidence paths of the form
    <FEATURE>/evidence/<kind>/ and permitted artifacts/ sub-paths such as
    artifacts/orchestration/, artifacts/pr_context, artifacts/reviews/,
    artifacts/status/, artifacts/python/, artifacts/pester/, and
    artifacts/csharp/. Research output is no longer an artifacts/ sub-path; it
    is written to the tracked roots docs/features/<feature>/research/
    (feature-associated) or docs/research/ (one-off).

    If the file_path resolves to a forbidden prefix, the script writes a PreToolUse JSON
    response to stdout with hookSpecificOutput.permissionDecision = 'deny' and exits with
    code 0 so Claude Code surfaces the reason. For allowed paths, a PreToolUse response
    with permissionDecision = 'allow' is written to stdout and the script exits 0. On hard
    failure (malformed JSON input), the script exits 1.

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
        'artifacts/post-change/',
        'artifacts/research/'
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
        Constructs a deny-decision ordered dictionary for the supplied forbidden path.
    .PARAMETER FilePath
        The file path that triggered the deny decision.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)]
        [string] $FilePath
    )

    [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = "EVIDENCE_LOCATION_BLOCKED: '$FilePath' is not a canonical evidence location. Use <FEATURE>/evidence/<kind>/ instead. See .claude/skills/evidence-and-timestamp-conventions/SKILL.md for the canonical scheme."
        }
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
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        # Malformed JSON is a hard failure; caller exits 1 to surface the issue.
        throw "enforce-evidence-locations hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $filePath = $toolInput.file_path
    if (-not $filePath) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    if (Test-EvidenceLocationForbidden -FilePath $filePath) {
        return Get-EvidenceLocationBlockDecision -FilePath $filePath
    }

    return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
}

function Invoke-EvidenceLocationEntryPoint {
    <#
    .SYNOPSIS
        Runs the evidence-location decision and returns the process exit code.
    .DESCRIPTION
        Wraps the dispatch logic that the hook entry point performs so it can be
        exercised by unit tests. On success it writes the compact JSON decision to
        the output stream and returns 0. On a hard failure (malformed JSON) it
        writes the error record and returns 1. This function does not call exit;
        the thin entry-point wiring converts the returned code into a process exit.
    .PARAMETER ToolInputRaw
        The raw JSON string from $env:CLAUDE_TOOL_INPUT.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [string] $ToolInputRaw = $env:CLAUDE_TOOL_INPUT
    )

    try {
        $decision = Invoke-EvidenceLocationDecision -ToolInputRaw $ToolInputRaw
    } catch {
        Write-Error $_
        return 1
    }

    $decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

    return 0
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

exit (Invoke-EvidenceLocationEntryPoint)
