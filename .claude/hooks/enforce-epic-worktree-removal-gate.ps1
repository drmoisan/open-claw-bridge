<#
.SYNOPSIS
    Pre-tool-use hook that gates git worktree remove behind epic checkpoint merge state.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook on the "Bash" matcher before any Bash
    command runs. Regex-matches git worktree remove against CLAUDE_TOOL_INPUT.command,
    extracts the target worktree path argument, reads
    artifacts/orchestration/epic-orchestrator-state.json, and finds the features[] record
    whose worktree_path matches. Allows removal only when that record's merge_status is
    merged or worktree_removed. Denies with reason EPIC_WORKTREE_REMOVAL_BLOCKED when the
    checkpoint is unreadable, no matching record exists, or merge_status is anything else -
    fail-closed, following the enforce-orchestration-preimplementation-gate.ps1 precedent of
    treating an unreadable/no-match checkpoint as deny.

.NOTES
    Compatible with PowerShell 7+. No external module dependencies. Filesystem reads go
    through an injectable wrapper function so tests can mock the boundary without writing
    temporary files.
#>
[CmdletBinding()]
param()

$script:EpicCheckpointPath = 'artifacts/orchestration/epic-orchestrator-state.json'
$script:AllowedMergeStatuses = @('merged', 'worktree_removed')

function Get-EpicWorktreeGateCheckpointContent {
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

function Get-EpicWorktreeRemovalCommandPath {
    <#
    .SYNOPSIS
        Extract the target worktree path argument from a git worktree remove command.
    .PARAMETER CommandText
        The Bash command text under evaluation.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText
    )

    if ($CommandText -match '(?i)\bgit\s+worktree\s+remove\s+(?<path>\S+)') {
        return $Matches['path'].Trim('"''')
    }
    return $null
}

function Find-EpicWorktreeFeatureRecord {
    <#
    .SYNOPSIS
        Locate the features[] record whose worktree_path matches the target path.
    .PARAMETER Checkpoint
        Parsed epic checkpoint, or $null when absent/unreadable.
    .PARAMETER WorktreePath
        The target worktree path extracted from the command text.
    .OUTPUTS
        System.Object or $null
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        $Checkpoint,

        [AllowNull()]
        [string] $WorktreePath
    )

    if ($null -eq $Checkpoint -or [string]::IsNullOrWhiteSpace($WorktreePath)) {
        return $null
    }
    $checkpointProps = @($Checkpoint.PSObject.Properties.Name)
    if ($checkpointProps -notcontains 'features') {
        return $null
    }

    $normalizedTarget = ($WorktreePath -replace '\\', '/').TrimEnd('/')

    # Scan every recorded feature for a worktree_path that matches the removal target;
    # path separators are normalized so Windows- and POSIX-style paths compare equal.
    foreach ($feature in @($Checkpoint.features)) {
        $featureProps = @($feature.PSObject.Properties.Name)
        if ($featureProps -notcontains 'worktree_path') {
            continue
        }
        $normalizedFeaturePath = (([string]$feature.worktree_path) -replace '\\', '/').TrimEnd('/')
        if ($normalizedFeaturePath -eq $normalizedTarget) {
            return $feature
        }
    }
    return $null
}

function Test-EpicWorktreeRemovalAllowed {
    <#
    .SYNOPSIS
        Decision logic: allow only when the matching feature record's merge_status is
        merged or worktree_removed.
    .PARAMETER FeatureRecord
        The matched features[] record, or $null when no match was found.
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [AllowNull()]
        $FeatureRecord
    )

    if ($null -eq $FeatureRecord) {
        return $false
    }
    $props = @($FeatureRecord.PSObject.Properties.Name)
    if ($props -notcontains 'merge_status') {
        return $false
    }
    return $script:AllowedMergeStatuses -contains ([string]$FeatureRecord.merge_status)
}

function Get-EpicWorktreeGateAllowDecision {
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

function Get-EpicWorktreeGateBlockDecision {
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

function Invoke-EpicWorktreeRemovalGateDecision {
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
        return Get-EpicWorktreeGateAllowDecision
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "enforce-epic-worktree-removal-gate hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $commandText = $toolInput.command
    if (-not $commandText) {
        return Get-EpicWorktreeGateAllowDecision
    }

    if ($commandText -notmatch '(?i)\bgit\s+worktree\s+remove\b') {
        return Get-EpicWorktreeGateAllowDecision
    }

    $worktreePath = Get-EpicWorktreeRemovalCommandPath -CommandText $commandText

    $checkpointRaw = Get-EpicWorktreeGateCheckpointContent
    $checkpoint = $null
    if (-not [string]::IsNullOrWhiteSpace($checkpointRaw)) {
        try {
            $checkpoint = $checkpointRaw | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $checkpoint = $null
        }
    }

    $featureRecord = Find-EpicWorktreeFeatureRecord -Checkpoint $checkpoint -WorktreePath $worktreePath
    if (Test-EpicWorktreeRemovalAllowed -FeatureRecord $featureRecord) {
        return Get-EpicWorktreeGateAllowDecision
    }

    return Get-EpicWorktreeGateBlockDecision -Reason "EPIC_WORKTREE_REMOVAL_BLOCKED: git worktree remove for '$worktreePath' requires a matching epic checkpoint features[] record with merge_status in {merged, worktree_removed}. The checkpoint was unreadable, no matching record was found, or merge_status was not yet safe for removal."
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-EpicWorktreeRemovalGateDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
