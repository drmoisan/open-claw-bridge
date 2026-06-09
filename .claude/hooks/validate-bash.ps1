<#
.SYNOPSIS
    Pre-tool-use hook for Claude Code that blocks dangerous Bash commands.

.DESCRIPTION
    This script is invoked by the Claude Code PreToolUse hook before any Bash
    command is executed. It reads the proposed command string from the
    CLAUDE_TOOL_INPUT environment variable (JSON with a 'command' field) or
    falls back to the first positional argument. If the command matches any
    blocked pattern (destructive operations such as forced deletions, forced
    pushes, or hard resets), the script exits with code 1 to prevent execution.
    Safe commands exit with code 0.

.NOTES
    Compatible with PowerShell 7+.
    This script must not modify any state; it is a read-only validation gate.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$CommandInput
)

# Blocked patterns that represent destructive or irreversible operations.
$blockedPatterns = @(
    'rm -rf',
    'git push --force',
    'git push origin --force',
    'Remove-Item -Recurse -Force',
    'git reset --hard',
    'git push -f'
)

# Resolve the command string from CLAUDE_TOOL_INPUT JSON or positional argument.
$commandToCheck = ''

if ($env:CLAUDE_TOOL_INPUT) {
    try {
        $parsed = $env:CLAUDE_TOOL_INPUT | ConvertFrom-Json
        if ($parsed.command) {
            $commandToCheck = $parsed.command
        }
    }
    catch {
        # If JSON parsing fails, treat the raw environment variable as the command.
        $commandToCheck = $env:CLAUDE_TOOL_INPUT
    }
}

# Fall back to positional argument when the environment variable is absent or empty.
if (-not $commandToCheck -and $CommandInput) {
    $commandToCheck = $CommandInput
}

# When no command is provided, allow execution (nothing to validate).
if (-not $commandToCheck) {
    exit 0
}

# Check the command against each blocked pattern.
foreach ($pattern in $blockedPatterns) {
    if ($commandToCheck.Contains($pattern)) {
        Write-Error "Blocked dangerous command pattern detected: '$pattern'"
        exit 1
    }
}

# Command passed all checks.
exit 0
