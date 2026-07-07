<#
.SYNOPSIS
    Pre-tool-use hook that gates gh pr merge --merge behind epic-mode checkpoint state.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook on the "Bash" matcher before any Bash
    command runs. Regex-matches gh pr merge with a --merge flag against
    CLAUDE_TOOL_INPUT.command and, when matched, allows the merge only when one of two
    checkpoint-only conditions holds:

      1. Child-feature path: artifacts/orchestration/orchestrator-state.json exists,
         epic_mode == true, and step9_status == "passed" (the per-feature orchestrator has
         already run S9 step 6's CI-green gate before attempting its own merge-on-green).
      2. Epic-integration path: artifacts/orchestration/epic-orchestrator-state.json exists,
         epic_merge_pr.ci_gate.conclusion == "success", and, when the command names an
         explicit PR number, that number matches epic_merge_pr.pr_number.

    Otherwise the command is denied with reason EPIC_MERGE_GATE_BLOCKED. A missing or
    unreadable checkpoint in either branch fails closed (denies); standalone (non-epic)
    orchestration never sets epic_mode or populates epic_merge_pr, so it is structurally
    prevented from invoking gh pr merge --merge at all.

    Design decision: this gate trusts the on-disk checkpoint rather than shelling out live
    to gh pr view for a real-time head-SHA check, matching the same non-adversarial,
    policy-level-not-cryptographic posture already accepted for
    enforce-pr-author-skill.ps1's own receipt mechanism. It is not a cryptographic control.

.NOTES
    Compatible with PowerShell 7+. No external module dependencies. Filesystem reads go
    through injectable wrapper functions so tests can mock the boundary without writing
    temporary files.
#>
[CmdletBinding()]
param()

$script:ChildCheckpointPath = 'artifacts/orchestration/orchestrator-state.json'
$script:EpicCheckpointPath = 'artifacts/orchestration/epic-orchestrator-state.json'

function Get-ChildOrchestratorCheckpointContent {
    <#
    .SYNOPSIS
        Read the raw JSON text of the per-feature orchestrator checkpoint. Tests mock
        this function (read seam).
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if (-not (Test-Path -LiteralPath $script:ChildCheckpointPath -PathType Leaf)) {
        return $null
    }
    return (Get-Content -LiteralPath $script:ChildCheckpointPath -Raw)
}

function Get-EpicOrchestratorCheckpointContent {
    <#
    .SYNOPSIS
        Read the raw JSON text of the epic checkpoint. Tests mock this function
        (read seam).
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if (-not (Test-Path -LiteralPath $script:EpicCheckpointPath -PathType Leaf)) {
        return $null
    }
    return (Get-Content -LiteralPath $script:EpicCheckpointPath -Raw)
}

function ConvertFrom-EpicMergeGateJson {
    <#
    .SYNOPSIS
        Parse checkpoint JSON text, returning $null on unreadable/invalid content.
    .PARAMETER Raw
        Raw checkpoint text, or $null when the file does not exist.
    .OUTPUTS
        System.Object or $null
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string] $Raw
    )

    if ([string]::IsNullOrWhiteSpace($Raw)) {
        return $null
    }
    try {
        return ($Raw | ConvertFrom-Json -ErrorAction Stop)
    } catch {
        return $null
    }
}

function Get-EpicMergeGateCommandPrNumber {
    <#
    .SYNOPSIS
        Extract an explicit PR number argument from a gh pr merge command, or $null.
    .PARAMETER CommandText
        The Bash command text under evaluation.
    .OUTPUTS
        System.Nullable[int]
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText
    )

    if ($CommandText -match '(?i)\bgh\s+pr\s+merge\s+(\d+)\b') {
        return [int]$Matches[1]
    }
    return $null
}

function Test-ChildCheckpointAllowsEpicMerge {
    <#
    .SYNOPSIS
        Decision logic for the child-feature checkpoint path (branch 1).
    .PARAMETER Checkpoint
        Parsed child orchestrator checkpoint, or $null when absent/unreadable.
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [AllowNull()]
        $Checkpoint
    )

    if ($null -eq $Checkpoint) {
        return $false
    }
    $props = @($Checkpoint.PSObject.Properties.Name)
    if ($props -notcontains 'epic_mode' -or -not [bool]$Checkpoint.epic_mode) {
        return $false
    }
    if ($props -notcontains 'step9_status') {
        return $false
    }
    return ([string]$Checkpoint.step9_status) -eq 'passed'
}

function Test-EpicCheckpointAllowsMerge {
    <#
    .SYNOPSIS
        Decision logic for the epic-integration checkpoint path (branch 2).
    .PARAMETER Checkpoint
        Parsed epic checkpoint, or $null when absent/unreadable.
    .PARAMETER CommandPrNumber
        The explicit PR number parsed from the command, or $null when the command
        does not name one (implying the current branch's PR).
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [AllowNull()]
        $Checkpoint,

        [AllowNull()]
        [Nullable[int]] $CommandPrNumber
    )

    if ($null -eq $Checkpoint) {
        return $false
    }
    $props = @($Checkpoint.PSObject.Properties.Name)
    if ($props -notcontains 'epic_merge_pr' -or $null -eq $Checkpoint.epic_merge_pr) {
        return $false
    }
    $mergePr = $Checkpoint.epic_merge_pr
    $mergePrProps = @($mergePr.PSObject.Properties.Name)
    if ($mergePrProps -notcontains 'ci_gate' -or $null -eq $mergePr.ci_gate) {
        return $false
    }
    $ciGateProps = @($mergePr.ci_gate.PSObject.Properties.Name)
    if ($ciGateProps -notcontains 'conclusion' -or ([string]$mergePr.ci_gate.conclusion) -ne 'success') {
        return $false
    }

    # When the command names an explicit PR number, it must match the checkpoint's
    # recorded epic_merge_pr.pr_number; a bare "gh pr merge --merge" implicitly targets
    # the current branch's PR and is trusted per the checkpoint-only design decision.
    if ($null -ne $CommandPrNumber) {
        if ($mergePrProps -notcontains 'pr_number') {
            return $false
        }
        $checkpointPrNumber = 0
        if (-not [int]::TryParse([string]$mergePr.pr_number, [ref] $checkpointPrNumber)) {
            return $false
        }
        if ($checkpointPrNumber -ne $CommandPrNumber) {
            return $false
        }
    }

    return $true
}

function Get-EpicMergeGateAllowDecision {
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

function Get-EpicMergeGateBlockDecision {
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

function Invoke-EpicMergeGateDecision {
    <#
    .SYNOPSIS
        Parses CLAUDE_TOOL_INPUT and returns an allow-or-block decision.
    .PARAMETER ToolInputRaw
        The raw JSON tool payload supplied by Claude Code.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $ToolInputRaw
    )

    if (-not $ToolInputRaw) {
        return Get-EpicMergeGateAllowDecision
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "enforce-epic-merge-gate hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $commandText = $toolInput.command
    if (-not $commandText) {
        return Get-EpicMergeGateAllowDecision
    }

    # Only a gh pr merge invocation carrying --merge is in scope for this gate; every
    # other Bash command is unaffected.
    if ($commandText -notmatch '(?i)\bgh\s+pr\s+merge\b' -or $commandText -notmatch '--merge\b') {
        return Get-EpicMergeGateAllowDecision
    }

    $commandPrNumber = Get-EpicMergeGateCommandPrNumber -CommandText $commandText

    $childCheckpoint = ConvertFrom-EpicMergeGateJson -Raw (Get-ChildOrchestratorCheckpointContent)
    if (Test-ChildCheckpointAllowsEpicMerge -Checkpoint $childCheckpoint) {
        return Get-EpicMergeGateAllowDecision
    }

    $epicCheckpoint = ConvertFrom-EpicMergeGateJson -Raw (Get-EpicOrchestratorCheckpointContent)
    if (Test-EpicCheckpointAllowsMerge -Checkpoint $epicCheckpoint -CommandPrNumber $commandPrNumber) {
        return Get-EpicMergeGateAllowDecision
    }

    return Get-EpicMergeGateBlockDecision -Reason 'EPIC_MERGE_GATE_BLOCKED: gh pr merge --merge requires either a per-feature checkpoint with epic_mode == true and step9_status == "passed", or an epic checkpoint with epic_merge_pr.ci_gate.conclusion == "success" and a matching pr_number. Neither checkpoint satisfied this gate.'
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-EpicMergeGateDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
