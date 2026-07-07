<#
.SYNOPSIS
    Dot-sourced by enforce-pr-author-skill.ps1 to keep that file under the repository's
    500-line hard cap.

.DESCRIPTION
    Contains the epic-mode base-branch override check (Get-PrAuthorCheckpointContent and
    Test-EpicBaseBranchOverride) extracted verbatim from enforce-pr-author-skill.ps1. This is a
    structural extraction only: no logic, decision behavior, or reason-string wording changed.
#>

function Get-PrAuthorCheckpointContent {
    <#
    .SYNOPSIS
        Read the raw JSON text of the per-feature orchestrator checkpoint. Tests mock this
        function (read seam) for the epic-mode base-branch override check.
    .DESCRIPTION
        Returns the raw text content of artifacts/orchestration/orchestrator-state.json, or
        $null when the file is absent. This is the injectable boundary for checkpoint content
        used by Test-EpicBaseBranchOverride; no test writes the checkpoint file to disk.
    .PARAMETER CheckpointPath
        The relative path to the per-feature orchestrator checkpoint.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [string] $CheckpointPath = 'artifacts/orchestration/orchestrator-state.json'
    )

    if (-not (Test-Path -LiteralPath $CheckpointPath)) {
        return $null
    }

    return (Get-Content -LiteralPath $CheckpointPath -Raw)
}

function Test-EpicBaseBranchOverride {
    <#
    .SYNOPSIS
        Sixth ordered check: enforce the epic-mode --base override for gh pr create.
    .DESCRIPTION
        Reads the per-feature checkpoint via the injectable Get-PrAuthorCheckpointContent
        seam. When the checkpoint has epic_mode == true, a gh pr create command text MUST
        contain --base <epic_context.integration_branch> with the exact branch value recorded
        in the checkpoint; a missing --base, or a --base value that does not match, is denied
        with reason EPIC_BASE_BRANCH_MISMATCH. When epic_mode is absent or false, when the
        checkpoint is unreadable, or when the command is not gh pr create (the base branch is
        set at create time, not edit time), this check is a no-op and standalone behavior is
        unchanged.
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

    # The epic-mode base-branch override only constrains gh pr create; gh pr edit does not
    # re-target the base branch, so non-create commands are out of scope for this check.
    if ($CommandText -notmatch '(?i)\bgh\s+pr\s+create\b') {
        return $null
    }

    $checkpointRaw = Get-PrAuthorCheckpointContent
    if ([string]::IsNullOrWhiteSpace($checkpointRaw)) {
        return $null
    }

    try {
        $checkpoint = $checkpointRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        return $null
    }

    $checkpointProps = @($checkpoint.PSObject.Properties.Name)
    if ($checkpointProps -notcontains 'epic_mode' -or -not [bool]$checkpoint.epic_mode) {
        return $null
    }

    $integrationBranch = $null
    if ($checkpointProps -contains 'epic_context' -and $null -ne $checkpoint.epic_context) {
        $epicContextProps = @($checkpoint.epic_context.PSObject.Properties.Name)
        if ($epicContextProps -contains 'integration_branch') {
            $integrationBranch = [string]$checkpoint.epic_context.integration_branch
        }
    }

    if ([string]::IsNullOrWhiteSpace($integrationBranch)) {
        return "EPIC_BASE_BRANCH_MISMATCH: checkpoint has epic_mode == true but no ``epic_context.integration_branch`` is recorded; ``gh pr create`` cannot be verified against the required ``--base`` value."
    }

    if ($CommandText -cnotmatch [regex]::Escape("--base $integrationBranch")) {
        return "EPIC_BASE_BRANCH_MISMATCH: ``gh pr create`` must pass ``--base $integrationBranch`` (``epic_context.integration_branch``) under ``epic_mode``; the command does not carry a matching ``--base`` argument."
    }

    return $null
}
