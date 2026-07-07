<#
.SYNOPSIS
    Portable completion-gate presence checks for the orchestrator-state checkpoint.

.DESCRIPTION
    Provides the destination-runtime PowerShell mirror of the completion-gate
    presence checks the pushed-down validate-orchestrator-output hook needs when the
    authoritative Python validator (`scripts/dev_tools`) is not importable. It
    reuses the shared load, field-accessor, and base-presence primitives from the
    sibling `OrchestratorState.psm1` and imports `.claude/lib/model-routing/ModelRouting.psm1`
    so per-receipt model formulas are available where practical.

    The single public function `Test-OrchestratorStateCompletionReadiness` fails
    closed on a missing checkpoint file, invalid JSON, or an invalid base shape, then
    applies the model-routing "required once delegated" existence gate - the
    delegated-agent set (derived from `delegation_receipts[].agent_name` plus a
    delegating `next_step`) must be a subset of `model_routing_receipts[].agent` -
    mirroring `scripts/dev_tools/_orchestrator_state_model_routing_gate.py`. Deep
    per-receipt routing-contract correctness that requires full Python authority is a
    documented Non-Goal for the portable path; the gate performs the presence-level
    existence check and reports missing receipts with error text containing the
    literal token `model_routing_receipts`, so the completion hook maps a failure to
    its `MODEL_ROUTING_BLOCKED:` block reason. The Python validator remains
    authoritative; this module is the fallback mirror only.
#>

Set-StrictMode -Version Latest

# Import the sibling shared module and the portable model-routing module, resolved
# relative to this module's directory so the imports travel with the pushed-down
# pack regardless of the consumer repository's working directory.
Import-Module (Join-Path -Path $PSScriptRoot -ChildPath 'OrchestratorState.psm1') -Force
$script:ModelRoutingModulePath = Join-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath '..') -ChildPath (Join-Path -Path 'model-routing' -ChildPath 'ModelRouting.psm1')
Import-Module $script:ModelRoutingModulePath -Force

# The subagent types delegated via the Agent tool that can be named by a delegating
# next_step. Pinned to _DELEGATING_AGENTS in
# scripts/dev_tools/_orchestrator_state_model_routing_gate.py. The `orchestrator`
# type is deliberately excluded: it is the caller, never a routing-receipt target.
$script:DELEGATING_AGENTS = @(
    'atomic-planner',
    'atomic-executor',
    'feature-review',
    'task-researcher',
    'prd-feature',
    'pr-author'
)

# Checkpoint keys the gate reads to derive the delegated-agent and receipt-agent sets.
$script:DELEGATION_RECEIPTS_KEY = 'delegation_receipts'
$script:MODEL_ROUTING_RECEIPTS_KEY = 'model_routing_receipts'
$script:NEXT_STEP_KEY = 'next_step'


function Get-OrchestratorStateDelegatedAgent {
    <#
    .SYNOPSIS
        Derive the set of agents a checkpoint has delegated (or is about to).
    .DESCRIPTION
        Private helper mirroring ``_delegated_agents`` in the Python gate. Collects
        each well-formed ``delegation_receipts[]`` entry's non-empty ``agent_name``
        plus the agent implied by a ``next_step`` that names a recognized delegating
        agent. The list form of ``delegation_receipts`` is the authoritative "a
        delegation happened" record; the namespaced (promotion) object form carries
        no agent_name list and contributes no delegated agents.
    .PARAMETER State
        The parsed checkpoint PSCustomObject.
    .OUTPUTS
        System.String[] - the delegated-agent names (may be empty).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [psobject] $State
    )

    $agents = [System.Collections.Generic.HashSet[string]]::new()

    # Collect each list-form delegation receipt's non-empty agent_name. A non-list
    # (namespaced) delegation_receipts value contributes nothing here.
    $receiptsField = Get-OrchestratorStateField -State $State -Name $script:DELEGATION_RECEIPTS_KEY
    if ($receiptsField.Present -and ($receiptsField.Value -is [System.Array])) {
        foreach ($receipt in $receiptsField.Value) {
            if ($receipt -is [System.Management.Automation.PSCustomObject]) {
                $nameField = Get-OrchestratorStateField -State $receipt -Name 'agent_name'
                if ($nameField.Present -and $null -ne $nameField.Value -and
                    -not [string]::IsNullOrWhiteSpace([string]$nameField.Value)) {
                    [void]$agents.Add([string]$nameField.Value)
                }
            }
        }
    }

    # A delegating next_step names the upcoming delegation that may not yet have a
    # receipt; include it only when it matches a recognized delegating agent so a
    # non-delegating label (for example "complete") never triggers the gate.
    $nextStepField = Get-OrchestratorStateField -State $State -Name $script:NEXT_STEP_KEY
    if ($nextStepField.Present -and $null -ne $nextStepField.Value -and
        ($script:DELEGATING_AGENTS -contains [string]$nextStepField.Value)) {
        [void]$agents.Add([string]$nextStepField.Value)
    }

    return [string[]]@($agents)
}

function Get-OrchestratorStateRoutingReceiptAgent {
    <#
    .SYNOPSIS
        Collect the set of agents that carry a model-routing receipt.
    .DESCRIPTION
        Private helper mirroring the receipt-agent harvest in the Python gate. Reads
        the checkpoint's ``model_routing_receipts[]`` array and returns the set of
        non-empty ``agent`` values present on well-formed receipt objects. A non-list
        value contributes no agents (the existence gate then reports every delegated
        agent as unreceipted, preserving fail-closed semantics).
    .PARAMETER State
        The parsed checkpoint PSCustomObject.
    .OUTPUTS
        System.String[] - the receipt-agent names (may be empty).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [psobject] $State
    )

    $agents = [System.Collections.Generic.HashSet[string]]::new()

    $receiptsField = Get-OrchestratorStateField -State $State -Name $script:MODEL_ROUTING_RECEIPTS_KEY
    if ($receiptsField.Present -and ($receiptsField.Value -is [System.Array])) {
        # Record each well-formed receipt's non-empty agent so the existence gate can
        # test the delegated-agent set against it.
        foreach ($receipt in $receiptsField.Value) {
            if ($receipt -is [System.Management.Automation.PSCustomObject]) {
                $agentField = Get-OrchestratorStateField -State $receipt -Name 'agent'
                if ($agentField.Present -and $null -ne $agentField.Value -and
                    -not [string]::IsNullOrWhiteSpace([string]$agentField.Value)) {
                    [void]$agents.Add([string]$agentField.Value)
                }
            }
        }
    }

    return [string[]]@($agents)
}

function Get-OrchestratorStateModelRoutingGateError {
    <#
    .SYNOPSIS
        Return the required-once-delegated existence-gate errors.
    .DESCRIPTION
        Private gate mirroring ``validate_model_routing_gate`` at the presence level:
        it fires only when the checkpoint has delegated (or is about to delegate to)
        at least one agent, then reports one error per delegated agent that lacks a
        matching ``model_routing_receipts[]`` entry. A delegation-free checkpoint
        contributes zero errors, preserving backward compatibility. Each error names
        the literal token ``model_routing_receipts`` so the completion hook routes a
        failure to ``MODEL_ROUTING_BLOCKED:``.
    .PARAMETER State
        The parsed checkpoint PSCustomObject.
    .OUTPUTS
        System.String[] - zero or more error strings; empty when the gate is satisfied
        or does not fire.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [psobject] $State
    )

    $errors = [System.Collections.Generic.List[string]]::new()

    # Backward-compat gate: a delegation-free checkpoint imposes no routing-receipt
    # requirement, so return early with no errors.
    $delegated = @(Get-OrchestratorStateDelegatedAgent -State $State)
    if ($delegated.Count -eq 0) {
        return $errors.ToArray()
    }

    $receiptAgents = @(Get-OrchestratorStateRoutingReceiptAgent -State $State)

    # Existence invariant: the routing-receipt agent set must be a superset of the
    # delegated-agent set. Report each delegated agent with no receipt, sorted for
    # deterministic error ordering.
    $missing = $delegated | Where-Object { $receiptAgents -notcontains $_ } | Sort-Object
    foreach ($agent in $missing) {
        $errors.Add("Checkpoint model_routing_receipts is missing a receipt for delegated agent: $agent.")
    }

    return $errors.ToArray()
}

function Test-OrchestratorStateCompletionReadiness {
    <#
    .SYNOPSIS
        Validate a checkpoint satisfies the portable completion-gate presence checks.
    .DESCRIPTION
        Public entry point used by the pushed-down validate-orchestrator-output hook
        when the authoritative Python validator is not importable. Loads the
        checkpoint (fail-closed on missing file / invalid JSON / invalid base shape),
        runs the base-presence check (required keys, step-status validity,
        blocked_reason validity), then applies the model-routing required-once-
        delegated existence gate. Returns a hashtable compatible with the hook's
        invoker contract: ExitCode is 1 whenever any error is present, and Output
        carries the newline-joined error text (empty on success). A missing routing
        receipt yields error text containing ``model_routing_receipts`` so the hook
        surfaces it under ``MODEL_ROUTING_BLOCKED:``.
    .PARAMETER CheckpointPath
        The path to the orchestrator-state checkpoint JSON file.
    .OUTPUTS
        System.Collections.Hashtable with keys ExitCode (int, 0 or 1) and Output (string).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $CheckpointPath
    )

    # Fail closed when the checkpoint cannot be loaded: the load error is the whole
    # output and ExitCode is 1.
    $loaded = Get-OrchestratorStateCheckpoint -CheckpointPath $CheckpointPath
    if (-not $loaded.Ok) {
        return @{ ExitCode = 1; Output = $loaded.Error }
    }

    # Accumulate base-presence errors and existence-gate errors; any error yields a
    # non-zero ExitCode so the completion hook blocks DONE.
    $errors = [System.Collections.Generic.List[string]]::new()
    $errors.AddRange([string[]]@(Get-OrchestratorStateBasePresenceError -State $loaded.State))
    $errors.AddRange([string[]]@(Get-OrchestratorStateModelRoutingGateError -State $loaded.State))

    if ($errors.Count -gt 0) {
        return @{ ExitCode = 1; Output = ($errors -join [System.Environment]::NewLine) }
    }

    return @{ ExitCode = 0; Output = '' }
}

Export-ModuleMember -Function Test-OrchestratorStateCompletionReadiness
