<#
.SYNOPSIS
    Model-routing reference formulas for the orchestrator, ported from the Python references.

.DESCRIPTION
    Provides the destination-runtime PowerShell ports of the two self-contained,
    pure model-routing formulas that the `orchestrate` skill instructs the
    orchestrator to run:

      - Get-ComplexityFloor    port of scripts/dev_tools/compute_complexity_floor.py
      - Resolve-DelegationModel port of scripts/dev_tools/resolve_delegation_model.py

    Both functions are pure and deterministic: they read no file at runtime and
    encode only the fixed band ordering, the base complexity-to-model table, the
    preferred overlay, and the disabled-mode clamp as module-scope constants.
    Those literals are pinned to config/orchestration-routing.json (model_policy /
    model_budget) by a static config-parity Pester test, and the Python modules
    remain the validator's authoritative reference. This module is one half of a
    two-language mirror; it never imports validator logic.
#>

Set-StrictMode -Version Latest

# The fixed complexity-band vocabulary, ordered from lowest to highest rigor.
# The array order defines "higher" and "lower" band comparisons used by the
# floor computation, mirroring BAND_ORDER in compute_complexity_floor.py.
$script:BAND_ORDER = @('C1', 'C2', 'C3', 'C4')

# The lowest band, returned when no floor signal is present (LOWEST_BAND).
$script:LOWEST_BAND = 'C1'

# Every present floor signal contributes this uniform candidate band, per the
# model_policy.complexity contract (each [floor] signal contributes C3).
$script:FLOOR_CANDIDATE_BAND = 'C3'

# Floors never exceed this ceiling; C4 is judgment-only and never floor-forced,
# so the computed floor is clamped to at most C3 (FLOOR_CEILING_BAND).
$script:FLOOR_CEILING_BAND = 'C3'

# The three session-level fable policies (model_budget.fable_policy).
$script:DISABLED_POLICY = 'disabled'
$script:PREFERRED_POLICY = 'preferred'

# The model tier removed from consideration under the disabled policy, the tier
# a disabled-mode fable cell clamps down to, and the recorded clamp reason.
$script:FABLE_MODEL = 'fable'
$script:DISABLED_CLAMP_MODEL = 'opus'
$script:DISABLED_CLAMP_REASON = 'fable_disabled'

# The base complexity-to-model table applied uniformly across delegated agents
# (BASE_COMPLEXITY_TO_MODEL). Pinned to model_policy.complexity_to_model.
$script:BASE_COMPLEXITY_TO_MODEL = @{
    C1 = 'haiku'
    C2 = 'sonnet'
    C3 = 'opus'
    C4 = 'fable'
}

# The agents whose C3 cell the preferred overlay redirects to fable. No other
# agent and no other band is affected (PREFERRED_OVERLAY_AGENTS).
$script:PREFERRED_OVERLAY_AGENTS = @(
    'atomic-planner',
    'prd-feature',
    'feature-review',
    'task-researcher'
)

# The single band and target model the preferred overlay applies.
$script:PREFERRED_OVERLAY_BAND = 'C3'
$script:PREFERRED_OVERLAY_MODEL = 'fable'


function Get-ComplexityFloor {
    <#
    .SYNOPSIS
        Compute the deterministic complexity-band floor from present floor signals.

    .DESCRIPTION
        Faithful PowerShell port of compute_complexity_floor
        (scripts/dev_tools/compute_complexity_floor.py). Returns the deterministic
        lower-bound complexity band implied by the set of present floor signals:
        each present floor signal contributes a candidate band of C3, the floor is
        the maximum triggered candidate band, and the floor never exceeds C3
        (C4 is never floor-forced). With no floor signal present the floor is the
        lowest band C1. The function is pure: it reads no file and does not mutate
        its input, and the result is independent of input ordering.

    .PARAMETER SignalsPresent
        The names of the present signals flagged [floor] in the
        model_policy.complexity catalog. Every element is treated as a triggered
        floor signal contributing the candidate band C3. An empty collection means
        no floor signal is present.

    .OUTPUTS
        System.String. The floor band: C1 when no floor signal is present,
        otherwise the maximum triggered candidate band clamped to at most C3.
        C4 is never returned.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $SignalsPresent
    )

    # With no present floor signal there is no candidate band to raise the floor
    # above the lowest band, so the floor is C1 (mirrors the empty-input guard).
    if (-not $SignalsPresent -or $SignalsPresent.Count -eq 0) {
        return $script:LOWEST_BAND
    }

    # Each present floor signal contributes the uniform candidate band; the floor
    # is the maximum triggered candidate rank across all of them. Because every
    # signal contributes the same candidate band, the max equals that rank.
    $candidateRank = $script:BAND_ORDER.IndexOf($script:FLOOR_CANDIDATE_BAND)
    $highestRank = $candidateRank

    # Clamp with the ceiling rank so the floor can never exceed C3; this is what
    # keeps C4 from ever being floor-forced regardless of how many signals exist.
    $ceilingRank = $script:BAND_ORDER.IndexOf($script:FLOOR_CEILING_BAND)
    $floorRank = [Math]::Min($highestRank, $ceilingRank)
    return $script:BAND_ORDER[$floorRank]
}

function Resolve-DelegationModel {
    <#
    .SYNOPSIS
        Resolve the delegation model tier for an agent, band, and fable policy.

    .DESCRIPTION
        Faithful PowerShell port of resolve_delegation_model
        (scripts/dev_tools/resolve_delegation_model.py). Applies the model_policy
        selection formula to a single delegation: it computes the pre-clamp
        table_model (the base complexity_to_model table plus any preferred overlay)
        and the post-clamp model, recording the clamp provenance. The preferred
        overlay redirects only the C3 cell to fable and only for the four overlay
        agents; atomic-executor and pr-author C3 cells stay opus under every
        policy. Under the disabled policy a fable table cell clamps to opus with
        clamped_from = fable and clamp_reason = fable_disabled. The function is
        pure: it reads no file and mutates no input.

    .PARAMETER Agent
        The target delegate agent name (for example atomic-planner). Only
        participates in preferred-overlay eligibility.

    .PARAMETER Band
        The assessed complexity band, one of C1..C4. Used as the key into the
        base complexity_to_model table. A band outside the table is the PowerShell
        analog of the Python KeyError and causes a terminating error (throw).

    .PARAMETER FablePolicy
        The session fable policy, one of disabled, available, or preferred.

    .OUTPUTS
        System.Collections.Hashtable. A hashtable with keys table_model (the
        pre-clamp table lookup, including any overlay), model (the post-clamp
        result), clamped_from (fable when a clamp occurred, else $null), and
        clamp_reason (fable_disabled when a clamp occurred, else $null).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Agent,
        [Parameter(Mandatory = $true)]
        [string] $Band,
        [Parameter(Mandatory = $true)]
        [string] $FablePolicy
    )

    # The preferred overlay redirects only the C3 cell to fable, and only for the
    # overlay agents; every other case reads the base table unchanged. The three
    # conditions (policy, agent membership, band) must all hold for the overlay.
    if ($FablePolicy -eq $script:PREFERRED_POLICY -and
        $script:PREFERRED_OVERLAY_AGENTS -contains $Agent -and
        $Band -eq $script:PREFERRED_OVERLAY_BAND) {
        $tableModel = $script:PREFERRED_OVERLAY_MODEL
    }
    else {
        # A band outside the base table is the PowerShell analog of the Python
        # KeyError: fail fast rather than return a silently wrong value.
        if (-not $script:BASE_COMPLEXITY_TO_MODEL.ContainsKey($Band)) {
            throw "Unknown complexity band '$Band'; expected one of $($script:BAND_ORDER -join ', ')."
        }
        $tableModel = $script:BASE_COMPLEXITY_TO_MODEL[$Band]
    }

    # Under the disabled policy, fable is removed from consideration: a fable
    # table cell clamps down to opus and records the clamp provenance.
    if ($FablePolicy -eq $script:DISABLED_POLICY -and $tableModel -eq $script:FABLE_MODEL) {
        return @{
            table_model  = $tableModel
            model        = $script:DISABLED_CLAMP_MODEL
            clamped_from = $script:FABLE_MODEL
            clamp_reason = $script:DISABLED_CLAMP_REASON
        }
    }

    # No clamp applies: the resolved model is the table model verbatim.
    return @{
        table_model  = $tableModel
        model        = $tableModel
        clamped_from = $null
        clamp_reason = $null
    }
}

Export-ModuleMember -Function Get-ComplexityFloor, Resolve-DelegationModel
