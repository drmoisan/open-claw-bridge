<#
.SYNOPSIS
    Pre-tool-use hook that blocks Bash promotion-script bypass attempts.

.DESCRIPTION
    This script is invoked by the Claude Code PreToolUse hook before any Bash
    command runs. It reads the tool input from the CLAUDE_TOOL_INPUT environment
    variable, inspects the attempted command text, and blocks direct promotion
    script execution that would bypass the repository's MCP-only promotion path.

    Forbidden command tokens (legacy promotion-script bypass):
      - new-potential-entry.ps1
      - new_potential_bug_entry
      - potential_to_issue
      - new_active_feature_folder

    Forbidden gh-CLI patterns (raw GitHub issue creation bypass):
      - gh issue create (with any flag suffix)
      - gh issue new
      - gh api against repos/<owner>/<repo>/issues with explicit POST method
        (-X POST or --method POST)

    The hook is read-only: it inspects the attempted command and emits a JSON
    allow-or-block decision without mutating the command text.

.NOTES
    Compatible with PowerShell 7+.
#>
[CmdletBinding()]
param()

$script:PromotionMcpOnlyBlockedReason = 'PROMOTION_MCP_ONLY_BLOCKED: Direct Bash promotion-script execution is not allowed in agent sessions. Use the drm-copilot MCP promotion tools instead.'

$script:PromotionMcpOnlyGhIssueBlockedReason = 'PROMOTION_MCP_ONLY_BLOCKED: Direct GitHub issue creation via `gh` bypasses the approved drm-copilot MCP promotion path (`mcp__drm-copilot__new_potential_entry` -> `mcp__drm-copilot__potential_to_issue` -> `mcp__drm-copilot__new_active_feature_folder`). Use those MCP tools instead.'

function Get-PromotionMcpOnlyBlockedReason {
    <#
    .SYNOPSIS
        Return the canonical deny message for legacy promotion-script bypass attempts.
    .OUTPUTS
        System.String
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return $script:PromotionMcpOnlyBlockedReason
}

function Get-PromotionMcpOnlyGhIssueBlockedReason {
    <#
    .SYNOPSIS
        Return the deny message for raw gh-CLI issue creation bypass attempts.
    .OUTPUTS
        System.String
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return $script:PromotionMcpOnlyGhIssueBlockedReason
}

function Get-PromotionBypassReason {
    <#
    .SYNOPSIS
        Inspect the command text and return the specific deny reason, or $null when allowed.
    .DESCRIPTION
        Returns the legacy promotion-script reason when any forbidden token is present.
        Returns the gh-CLI issue creation reason when a forbidden gh pattern is matched.
        Returns $null when the command is allowed.
    .PARAMETER CommandText
        The Bash command text extracted from CLAUDE_TOOL_INPUT.
    .OUTPUTS
        System.String or $null.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText
    )

    $forbiddenTokens = @(
        'new-potential-entry.ps1',
        'new_potential_bug_entry',
        'potential_to_issue',
        'new_active_feature_folder'
    )

    foreach ($token in $forbiddenTokens) {
        if ($CommandText.IndexOf($token, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return (Get-PromotionMcpOnlyBlockedReason)
        }
    }

    # `gh issue create` and `gh issue new` are direct bypasses of the MCP
    # promotion path. Tolerate any flags after the subcommand.
    if ($CommandText -match '(?i)\bgh\s+issue\s+(?:create|new)\b') {
        return (Get-PromotionMcpOnlyGhIssueBlockedReason)
    }

    # `gh api repos/<owner>/<repo>/issues` is a write surface only when an
    # explicit POST method is supplied. `gh api` defaults to GET, so we only
    # block when -X POST or --method POST is present, to avoid false positives
    # on issue read operations. Use a single regex with lookaheads against the
    # whole command string.
    $ghApiIssuesPostPattern = '(?i)(?=.*\bgh\s+api\b)(?=.*repos/[^/\s]+/[^/\s]+/issues(?:\b|/[^/\s]*$))(?=.*(?:-X\s+POST|--method\s+POST))'
    if ($CommandText -match $ghApiIssuesPostPattern) {
        return (Get-PromotionMcpOnlyGhIssueBlockedReason)
    }

    return $null
}

function Test-PromotionBypassToken {
    <#
    .SYNOPSIS
        Return $true when a Bash command contains a forbidden promotion bypass pattern.
    .PARAMETER CommandText
        The Bash command text extracted from CLAUDE_TOOL_INPUT.
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText
    )

    return ($null -ne (Get-PromotionBypassReason -CommandText $CommandText))
}

function Get-PromotionMcpOnlyBlockDecision {
    <#
    .SYNOPSIS
        Construct the structured block decision for a forbidden Bash command.
    .PARAMETER Reason
        The specific deny reason to surface in the block decision. Defaults to the
        legacy promotion-script reason for backward compatibility.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $Reason
    )

    if (-not $Reason) {
        $Reason = Get-PromotionMcpOnlyBlockedReason
    }

    return [ordered]@{
        decision = 'block'
        reason   = $Reason
    }
}

function Invoke-PromotionMcpOnlyDecision {
    <#
    .SYNOPSIS
        Parse CLAUDE_TOOL_INPUT and return an allow-or-block decision.
    .PARAMETER ToolInputRaw
        The raw JSON tool payload supplied by Claude Code.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    .NOTES
        Missing tool input or missing command text is treated as allow because
        non-Bash invocations or empty Bash requests cannot bypass promotion flow.
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
        throw "enforce-promotion-mcp-only hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $commandText = $toolInput.command
    if (-not $commandText) {
        return [ordered]@{ decision = 'allow' }
    }

    $reason = Get-PromotionBypassReason -CommandText $commandText
    if ($reason) {
        return Get-PromotionMcpOnlyBlockDecision -Reason $reason
    }

    return [ordered]@{ decision = 'allow' }
}

# Allow dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-PromotionMcpOnlyDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress | Write-Output

exit 0
