<#
.SYNOPSIS
    Pre-tool-use hook for Claude Code that blocks dangerous Bash commands.

.DESCRIPTION
    This script is invoked by the Claude Code PreToolUse hook before any Bash
    command is executed. It reads the proposed command string from the
    CLAUDE_TOOL_INPUT (or CLAUDE_HOOK_INPUT) environment variable (JSON with a
    'command' field) or falls back to the first positional argument. If the
    command matches any blocked pattern (destructive operations such as forced
    deletions, forced pushes, or hard resets), the script writes a PreToolUse
    deny decision to stdout and exits with code 0. The deny decision uses the
    Claude Code PreToolUse schema:

        {"hookSpecificOutput":{"hookEventName":"PreToolUse",
         "permissionDecision":"deny","permissionDecisionReason":"<reason>"}}

    Safe commands emit no decision and exit with code 0 (an absent decision is a
    valid allow at PreToolUse). The legacy top-level decision/block form and the
    deny-path 'exit 1' are intentionally NOT used: PreToolUse fail-opens on both,
    so they would silently fail to block.

.NOTES
    Compatible with PowerShell 7+.
    This script must not modify any state; it is a read-only validation gate.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$CommandInput
)

function Get-BlockedBashPattern {
    [CmdletBinding()]
    [OutputType([string[]])]
    param()

    return [string[]]@(
        'rm -rf',
        'git push --force',
        'git push origin --force',
        'Remove-Item -Recurse -Force',
        'git reset --hard',
        'git push -f'
    )
}

function Get-BlockedPatternMatch {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string] $Command
    )

    if (-not $Command) {
        return $null
    }

    foreach ($pattern in (Get-BlockedBashPattern)) {
        if ($Command.Contains($pattern)) {
            return $pattern
        }
    }

    return $null
}

function Get-BashBlockReason {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string] $Command
    )

    $pattern = Get-BlockedPatternMatch -Command $Command
    if (-not $pattern) {
        return $null
    }

    return "Blocked dangerous command pattern detected: '$pattern'"
}

function Get-BashDenyDecision {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)]
        [string] $Reason
    )

    [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $Reason
        }
    }
}

function Get-BashCommandToCheck {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string] $ToolInputRaw,

        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string] $PositionalInput
    )

    if ($ToolInputRaw) {
        try {
            $parsed = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
            if ($parsed.command) {
                return [string]$parsed.command
            }
        } catch {
            # If JSON parsing fails, treat the raw input as the command.
            return $ToolInputRaw
        }
    }

    if ($PositionalInput) {
        return $PositionalInput
    }

    return ''
}

function Invoke-ValidateBashDecision {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string] $ToolInputRaw,

        [Parameter(Mandatory = $false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string] $PositionalInput
    )

    $commandToCheck = Get-BashCommandToCheck -ToolInputRaw $ToolInputRaw -PositionalInput $PositionalInput

    $reason = Get-BashBlockReason -Command $commandToCheck
    if ($reason) {
        return Get-BashDenyDecision -Reason $reason
    }

    return $null
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$toolInputRaw = $env:CLAUDE_TOOL_INPUT
if (-not $toolInputRaw) {
    $toolInputRaw = $env:CLAUDE_HOOK_INPUT
}

$decision = Invoke-ValidateBashDecision -ToolInputRaw $toolInputRaw -PositionalInput $CommandInput
if ($null -ne $decision -and $decision.hookSpecificOutput.permissionDecision -eq 'deny') {
    $decision | ConvertTo-Json -Compress -Depth 5 | Write-Output
}

exit 0
