<#
.SYNOPSIS
    Pre-tool-use hook that is the Layer 1 per-call deterrent for the epic wave barrier.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook on the "Agent" matcher before any Agent
    (Task) call runs. Activates only when CLAUDE_TOOL_INPUT.subagent_type == "orchestrator"
    and the serialized prompt contains the epic-mode kickoff marker "Epic mode: true".

    Resolution and decision procedure:
      1. Resolve the target child feature_folder from the prompt text by scanning for a
         docs/features/active/<token> path, mirroring
         enforce-prd-feature-before-planner.ps1's Find-PrdFeatureFolderFromPrompt
         technique (longest match wins; a .md-suffixed match uses its parent directory).
      2. Read artifacts/orchestration/epic-orchestrator-state.json and locate the
         features[] record whose feature_folder equals the resolved basename.
      3. Look up that feature's depends_on list, and for every dependency, locate its own
         features[] record.
      4. Deny with reason EPIC_WAVE_BARRIER_BLOCKED unless every dependency's merge_status
         is merged or worktree_removed. A missing/unreadable checkpoint, an unresolved
         target feature_folder, or a missing dependency record also denies (fail-closed).

    This is the per-call deterrent (Layer 1) of the two-layer wave-barrier design; the
    retrospective backstop (Layer 2) is the wave-barrier ordering invariant inside
    validate_epic_orchestrator_state_text, enforced separately at epic-orchestrator
    SubagentStop time.

.NOTES
    Compatible with PowerShell 7+. No external module dependencies. Filesystem reads go
    through an injectable wrapper function so tests can mock the boundary without writing
    temporary files.
#>
[CmdletBinding()]
param()

$script:EpicCheckpointPath = 'artifacts/orchestration/epic-orchestrator-state.json'
$script:AllowedMergeStatuses = @('merged', 'worktree_removed')
$script:EpicModeMarker = 'Epic mode: true'

function Get-EpicWaveBarrierCheckpointContent {
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

function Find-EpicWaveBarrierFeatureFolderFromPrompt {
    <#
    .SYNOPSIS
        Scans a prompt string for docs/features/active/<...> path tokens and returns the
        longest unique match's basename. Returns $null when no match is found.
    .DESCRIPTION
        Mirrors enforce-prd-feature-before-planner.ps1's
        Find-PrdFeatureFolderFromPrompt technique: forward- or backslash-separated path
        tokens are accepted, the longest match wins, and a .md-suffixed match resolves to
        its parent directory before the basename is extracted.
    .PARAMETER Prompt
        The delegation prompt text under evaluation.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Prompt
    )

    if (-not $Prompt) {
        return $null
    }

    $pattern = 'docs[\\/]+features[\\/]+active[\\/]+[^\s"''`]+'
    $matchList = [regex]::Matches($Prompt, $pattern)
    if ($matchList.Count -eq 0) {
        return $null
    }

    $unique = @{}
    foreach ($m in $matchList) {
        $normalized = ($m.Value -replace '\\', '/').TrimEnd('/')
        $unique[$normalized] = $true
    }

    $candidates = @(@($unique.Keys) | Sort-Object -Property Length -Descending)
    $best = $candidates[0]

    if ($best -match '\.md$') {
        $best = $best -replace '/[^/]+\.md$', ''
    }

    return ($best -split '/')[-1]
}

function Find-EpicWaveBarrierFeatureRecord {
    <#
    .SYNOPSIS
        Locate the features[] record whose feature_folder equals the target basename.
    .PARAMETER Checkpoint
        Parsed epic checkpoint, or $null when absent/unreadable.
    .PARAMETER FeatureFolder
        The target feature_folder basename.
    .OUTPUTS
        System.Object or $null
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        $Checkpoint,

        [AllowNull()]
        [string] $FeatureFolder
    )

    if ($null -eq $Checkpoint -or [string]::IsNullOrWhiteSpace($FeatureFolder)) {
        return $null
    }
    $checkpointProps = @($Checkpoint.PSObject.Properties.Name)
    if ($checkpointProps -notcontains 'features') {
        return $null
    }

    # Scan every recorded feature for a feature_folder that equals the target basename.
    foreach ($feature in @($Checkpoint.features)) {
        $featureProps = @($feature.PSObject.Properties.Name)
        if ($featureProps -notcontains 'feature_folder') {
            continue
        }
        if (([string]$feature.feature_folder) -eq $FeatureFolder) {
            return $feature
        }
    }
    return $null
}

function Test-EpicWaveBarrierDependenciesMerged {
    <#
    .SYNOPSIS
        Decision logic: true only when every dependency's merge_status is merged or
        worktree_removed.
    .PARAMETER Checkpoint
        Parsed epic checkpoint, or $null when absent/unreadable.
    .PARAMETER FeatureRecord
        The target feature's own features[] record, or $null when not found.
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [AllowNull()]
        $Checkpoint,

        [AllowNull()]
        $FeatureRecord
    )

    if ($null -eq $Checkpoint -or $null -eq $FeatureRecord) {
        return $false
    }
    $featureProps = @($FeatureRecord.PSObject.Properties.Name)
    if ($featureProps -notcontains 'depends_on') {
        # No dependencies recorded: an empty/absent depends_on list has nothing to block on.
        return $true
    }

    $dependsOn = @($FeatureRecord.depends_on)
    if ($dependsOn.Count -eq 0) {
        return $true
    }

    # Every dependency edge must be durably confirmed merged or worktree_removed before
    # this wave's feature is allowed to start.
    foreach ($dependencyFolder in $dependsOn) {
        $dependencyRecord = Find-EpicWaveBarrierFeatureRecord -Checkpoint $Checkpoint -FeatureFolder ([string]$dependencyFolder)
        if ($null -eq $dependencyRecord) {
            return $false
        }
        $dependencyProps = @($dependencyRecord.PSObject.Properties.Name)
        if ($dependencyProps -notcontains 'merge_status') {
            return $false
        }
        if ($script:AllowedMergeStatuses -notcontains ([string]$dependencyRecord.merge_status)) {
            return $false
        }
    }
    return $true
}

function Get-EpicWaveBarrierAllowDecision {
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

function Get-EpicWaveBarrierBlockDecision {
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

function Invoke-EpicWaveBarrierDecision {
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
        return Get-EpicWaveBarrierAllowDecision
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "enforce-epic-wave-barrier hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $subagent = $toolInput.subagent_type
    if (-not $subagent -or $subagent -ne 'orchestrator') {
        return Get-EpicWaveBarrierAllowDecision
    }

    $prompt = [string]$toolInput.prompt
    if (-not $prompt -or $prompt -notlike "*$script:EpicModeMarker*") {
        return Get-EpicWaveBarrierAllowDecision
    }

    $featureFolder = Find-EpicWaveBarrierFeatureFolderFromPrompt -Prompt $prompt
    if (-not $featureFolder) {
        return Get-EpicWaveBarrierBlockDecision -Reason 'EPIC_WAVE_BARRIER_BLOCKED: an epic-mode orchestrator delegation must reference the target feature folder in the prompt so its dependency edges can be verified.'
    }

    $checkpointRaw = Get-EpicWaveBarrierCheckpointContent
    $checkpoint = $null
    if (-not [string]::IsNullOrWhiteSpace($checkpointRaw)) {
        try {
            $checkpoint = $checkpointRaw | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $checkpoint = $null
        }
    }

    $featureRecord = Find-EpicWaveBarrierFeatureRecord -Checkpoint $checkpoint -FeatureFolder $featureFolder
    if (Test-EpicWaveBarrierDependenciesMerged -Checkpoint $checkpoint -FeatureRecord $featureRecord) {
        return Get-EpicWaveBarrierAllowDecision
    }

    return Get-EpicWaveBarrierBlockDecision -Reason "EPIC_WAVE_BARRIER_BLOCKED: '$featureFolder' cannot start until every dependency in its depends_on list is durably confirmed merged or worktree_removed in the epic checkpoint. The checkpoint was unreadable, the feature record was not found, or a dependency is not yet safe."
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-EpicWaveBarrierDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
