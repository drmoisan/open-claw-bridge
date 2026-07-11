<#
.SYNOPSIS
    Pre-tool-use hook that blocks epic-planner and epic-orchestrator delegations
    originating from an orchestrator agent.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook on the "Agent" matcher before any
    Agent (Task) call runs. Activates only when the delegation target
    subagent_type is 'epic-planner' or 'epic-orchestrator'.

    Caller identity resolution:
      - The full PreToolUse payload (CLAUDE_HOOK_INPUT) carries a top-level
        'agent_type' field only when the tool call is made from inside a
        subagent context. A main-thread call carries no 'agent_type'.
      - The Agent tool input (CLAUDE_TOOL_INPUT, or the payload's 'tool_input'
        object) carries the delegation target 'subagent_type'.

    Decision procedure:
      1. Resolve the target subagent_type from CLAUDE_TOOL_INPUT, falling back
         to the payload's tool_input object. A non-epic target allows.
      2. Resolve the calling agent_type from the payload. An absent or empty
         agent_type indicates a main-thread invocation, which allows.
      3. Deny with reason EPIC_INVOCATION_ORIGIN_BLOCKED when the calling
         agent_type is exactly 'orchestrator'. Both epic agents delegate to
         Agent(orchestrator); an orchestrator-originated invocation would nest
         orchestrator inside its own delegation chain.

.NOTES
    Compatible with PowerShell 7+. No external module dependencies. Read-only
    validation gate; malformed JSON in either payload throws so the entrypoint
    exits 1.
#>
[CmdletBinding()]
param()

$script:GatedSubagentTypes = @('epic-planner', 'epic-orchestrator')
$script:ProhibitedCallerAgentType = 'orchestrator'

function Get-EpicInvocationOriginAllowDecision {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param()

    return [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName      = 'PreToolUse'
            permissionDecision = 'allow'
        }
    }
}

function Get-EpicInvocationOriginBlockDecision {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)]
        [string] $Reason
    )

    return [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $Reason
        }
    }
}

function ConvertFrom-EpicInvocationOriginPayload {
    <#
    .SYNOPSIS
        Parses a raw JSON payload string, returning $null for a blank payload
        and throwing a named error for malformed JSON.
    .PARAMETER RawPayload
        The raw JSON text under evaluation.
    .PARAMETER PayloadName
        The payload's environment-variable name, used in the error message.
    .OUTPUTS
        System.Object or $null
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string] $RawPayload,

        [Parameter(Mandatory)]
        [string] $PayloadName
    )

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return $null
    }

    try {
        return $RawPayload | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "enforce-epic-invocation-origin hook received malformed JSON in ${PayloadName}: $_"
    }
}

function Get-EpicInvocationOriginTargetSubagent {
    <#
    .SYNOPSIS
        Resolves the delegation target subagent_type from the tool input,
        falling back to the full payload's tool_input object.
    .PARAMETER ToolInput
        Parsed CLAUDE_TOOL_INPUT object, or $null when absent.
    .PARAMETER HookInput
        Parsed CLAUDE_HOOK_INPUT object, or $null when absent.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [AllowNull()]
        $ToolInput,

        [AllowNull()]
        $HookInput
    )

    if ($null -ne $ToolInput -and
        (@($ToolInput.PSObject.Properties.Name) -contains 'subagent_type') -and
        -not [string]::IsNullOrWhiteSpace([string]$ToolInput.subagent_type)) {
        return [string]$ToolInput.subagent_type
    }

    if ($null -ne $HookInput -and
        (@($HookInput.PSObject.Properties.Name) -contains 'tool_input')) {
        $nested = $HookInput.tool_input
        if ($null -ne $nested -and
            (@($nested.PSObject.Properties.Name) -contains 'subagent_type') -and
            -not [string]::IsNullOrWhiteSpace([string]$nested.subagent_type)) {
            return [string]$nested.subagent_type
        }
    }

    return $null
}

function Get-EpicInvocationOriginCallerAgentType {
    <#
    .SYNOPSIS
        Resolves the calling agent_type from the full hook payload. Returns
        $null for a main-thread invocation (no agent_type field).
    .PARAMETER HookInput
        Parsed CLAUDE_HOOK_INPUT object, or $null when absent.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [AllowNull()]
        $HookInput
    )

    if ($null -eq $HookInput) {
        return $null
    }
    if (@($HookInput.PSObject.Properties.Name) -notcontains 'agent_type') {
        return $null
    }

    $agentType = [string]$HookInput.agent_type
    if ([string]::IsNullOrWhiteSpace($agentType)) {
        return $null
    }
    return $agentType
}

function Invoke-EpicInvocationOriginDecision {
    <#
    .SYNOPSIS
        Parses the hook payloads and returns an allow-or-block decision.
    .PARAMETER HookInputRaw
        The raw full PreToolUse payload JSON supplied via CLAUDE_HOOK_INPUT.
    .PARAMETER ToolInputRaw
        The raw Agent tool input JSON supplied via CLAUDE_TOOL_INPUT.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string] $HookInputRaw,

        [AllowNull()]
        [AllowEmptyString()]
        [string] $ToolInputRaw
    )

    # The tool input identifies the delegation target; a non-epic target is
    # outside this hook's scope, so the hook input is not parsed for it.
    $toolInput = ConvertFrom-EpicInvocationOriginPayload -RawPayload $ToolInputRaw -PayloadName 'CLAUDE_TOOL_INPUT'
    $hookInputParsed = $false
    $hookInput = $null

    $target = Get-EpicInvocationOriginTargetSubagent -ToolInput $toolInput -HookInput $hookInput
    if (-not $target -and -not [string]::IsNullOrWhiteSpace($HookInputRaw)) {
        # Fallback: some harness surfaces supply only the full payload, whose
        # tool_input object carries the target subagent_type.
        $hookInput = ConvertFrom-EpicInvocationOriginPayload -RawPayload $HookInputRaw -PayloadName 'CLAUDE_HOOK_INPUT'
        $hookInputParsed = $true
        $target = Get-EpicInvocationOriginTargetSubagent -ToolInput $toolInput -HookInput $hookInput
    }

    if (-not $target -or $script:GatedSubagentTypes -notcontains $target) {
        return Get-EpicInvocationOriginAllowDecision
    }

    if (-not $hookInputParsed) {
        $hookInput = ConvertFrom-EpicInvocationOriginPayload -RawPayload $HookInputRaw -PayloadName 'CLAUDE_HOOK_INPUT'
    }

    # An absent agent_type marks a main-thread invocation, which is the
    # intended entry point for both epic agents; only an orchestrator-context
    # invocation is prohibited.
    $caller = Get-EpicInvocationOriginCallerAgentType -HookInput $hookInput
    if ($caller -ne $script:ProhibitedCallerAgentType) {
        return Get-EpicInvocationOriginAllowDecision
    }

    $reason = "EPIC_INVOCATION_ORIGIN_BLOCKED: Agent($target) must not be invoked from an orchestrator agent. Both epic-planner and epic-orchestrator delegate to Agent(orchestrator), so an orchestrator-originated invocation would nest orchestrator inside its own delegation chain. Invoke $target from the main session instead."
    return Get-EpicInvocationOriginBlockDecision -Reason $reason
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-EpicInvocationOriginDecision -HookInputRaw $env:CLAUDE_HOOK_INPUT -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
