<#
.SYNOPSIS
    SubagentStop hook for the orchestrator subagent.

.DESCRIPTION
    Blocks termination of the orchestrator subagent unless the orchestration
    checkpoint file at artifacts/orchestration/orchestrator-state.json has been
    updated with current `completed_steps` and `next_step` fields.

    Per the powershell-orchestration-state-machine and atomic-plan-contract
    skills, the orchestrator must persist progress to the checkpoint file
    after every completed step. This hook confirms that:

      - the hook payload is well-formed JSON,
      - the orchestrator's final output is non-empty,
      - the checkpoint file exists on disk,
      - the checkpoint is valid JSON,
      - the checkpoint contains the required progress fields:
            objective, completed_steps, next_step, last_updated,
      - the `objective` field is non-empty.

.NOTES
    Reads the hook payload from CLAUDE_HOOK_INPUT as JSON. Exits 0 to allow
    termination; exits 1 with an error message to block. Filesystem reads go
    through Get-CheckpointFileContent so tests can mock the boundary without
    writing temporary files.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CheckpointFileContent {
    <#
    .SYNOPSIS
        Thin wrapper around the filesystem-read boundary for the checkpoint.
    .DESCRIPTION
        Returns a hashtable with two keys:
          - Exists:  $true when the path resolves to a file on disk.
          - Content: full file text as a single string ($null when missing).
        Tests mock this function to inject checkpoint content without temp files.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @{ Exists = $false; Content = $null }
    }

    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
    return @{ Exists = $true; Content = $content }
}

function Test-RemediationLoopShape {
    <#
    .SYNOPSIS
        Validates the optional remediation_loop sub-object against the schema
        at .claude/schemas/orchestrator-state.schema.json.
    .DESCRIPTION
        Returns a hashtable with keys:
          - Ok:      $true if the field is absent or every cycle is well-formed.
          - Message: rejection message naming the first malformed cycle; $null on success.
        Rejection conditions, in order:
          - cycles array missing or non-array.
          - any cycle missing required field plan_path (or plan_path empty).
          - any cycle where exit_condition_met == true and blocking_count != 0.
          - any cycle where execution_status in {in_progress, complete, failed}
            while preflight.final_status != 'clear'.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        $RemediationLoop
    )

    if ($null -eq $RemediationLoop) {
        return @{ Ok = $true; Message = $null }
    }

    $loopProps = @($RemediationLoop.PSObject.Properties.Name)
    if ($loopProps -notcontains 'cycles') {
        return @{ Ok = $false; Message = "orchestrator hook: 'remediation_loop' is present but 'cycles' array is missing." }
    }

    $cycles = @($RemediationLoop.cycles)
    $executionInProgress = @('in_progress', 'complete', 'failed')

    for ($i = 0; $i -lt $cycles.Count; $i++) {
        $cycle = $cycles[$i]
        $cycleProps = @($cycle.PSObject.Properties.Name)

        if ($cycleProps -notcontains 'plan_path' -or [string]::IsNullOrWhiteSpace([string]$cycle.plan_path)) {
            return @{ Ok = $false; Message = "orchestrator hook: 'remediation_loop.cycles[$i]' is missing required field 'plan_path'." }
        }

        $exitMet = $null
        if ($cycleProps -contains 'exit_condition_met') { $exitMet = $cycle.exit_condition_met }
        $blockingCount = $null
        if ($cycleProps -contains 'blocking_count') { $blockingCount = $cycle.blocking_count }

        if ($exitMet -eq $true -and $blockingCount -ne 0) {
            return @{ Ok = $false; Message = "orchestrator hook: 'remediation_loop.cycles[$i]' is malformed: 'exit_condition_met' is true but 'blocking_count' is '$blockingCount' (must be 0)." }
        }

        $execStatus = $null
        if ($cycleProps -contains 'execution_status') { $execStatus = [string]$cycle.execution_status }

        if ($executionInProgress -contains $execStatus) {
            $preflight = $null
            if ($cycleProps -contains 'preflight') { $preflight = $cycle.preflight }
            $finalStatus = $null
            if ($null -ne $preflight) {
                $preflightProps = @($preflight.PSObject.Properties.Name)
                if ($preflightProps -contains 'final_status') { $finalStatus = [string]$preflight.final_status }
            }
            if ($finalStatus -ne 'clear') {
                return @{ Ok = $false; Message = "orchestrator hook: 'remediation_loop.cycles[$i]' is malformed: 'execution_status' is '$execStatus' but 'preflight.final_status' is '$finalStatus' (must be 'clear')." }
            }
        }
    }

    return @{ Ok = $true; Message = $null }
}

function Invoke-OrchestratorOutputValidation {
    <#
    .SYNOPSIS
        Parses the hook payload and returns an ok-or-block decision.
    .DESCRIPTION
        Returns a hashtable with keys:
          - Ok:      $true to allow termination, $false to block.
          - Message: error message when blocking; $null on success.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string] $RawPayload,
        [string] $CheckpointPath = 'artifacts/orchestration/orchestrator-state.json'
    )

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return @{ Ok = $false; Message = 'orchestrator hook: CLAUDE_HOOK_INPUT is empty; cannot validate orchestrator output.' }
    }

    try {
        $payload = $RawPayload | ConvertFrom-Json -ErrorAction Stop
    } catch {
        return @{ Ok = $false; Message = "orchestrator hook: failed to parse CLAUDE_HOOK_INPUT as JSON: $($_.Exception.Message)" }
    }

    $agentOutput = $null
    if ($payload.PSObject.Properties.Name -contains 'output') {
        $agentOutput = $payload.output
    }
    if ([string]::IsNullOrWhiteSpace($agentOutput)) {
        return @{ Ok = $false; Message = 'orchestrator hook: agent output is empty; orchestrator must report final completion summary before termination.' }
    }

    $file = Get-CheckpointFileContent -Path $CheckpointPath
    if (-not $file.Exists) {
        return @{ Ok = $false; Message = "orchestrator hook: checkpoint file '$CheckpointPath' does not exist. Orchestrator must persist progress per powershell-orchestration-state-machine before termination." }
    }

    if ([string]::IsNullOrWhiteSpace($file.Content)) {
        return @{ Ok = $false; Message = "orchestrator hook: checkpoint file '$CheckpointPath' is empty; cannot validate orchestrator progress." }
    }

    try {
        $checkpoint = $file.Content | ConvertFrom-Json -ErrorAction Stop
    } catch {
        return @{ Ok = $false; Message = "orchestrator hook: checkpoint file '$CheckpointPath' is not valid JSON: $($_.Exception.Message)" }
    }

    $requiredFields = @('objective', 'completed_steps', 'next_step', 'last_updated')
    $missingFields = New-Object System.Collections.Generic.List[string]
    $checkpointProps = @($checkpoint.PSObject.Properties.Name)
    foreach ($field in $requiredFields) {
        if ($checkpointProps -notcontains $field) {
            $missingFields.Add($field)
        }
    }

    if ($missingFields.Count -gt 0) {
        $missingList = ($missingFields -join ', ')
        return @{ Ok = $false; Message = "orchestrator hook: checkpoint file '$CheckpointPath' is missing required field(s): $missingList. Orchestrator must persist objective, completed_steps, next_step, and last_updated." }
    }

    if ([string]::IsNullOrWhiteSpace([string]$checkpoint.objective)) {
        return @{ Ok = $false; Message = "orchestrator hook: checkpoint file '$CheckpointPath' has an empty 'objective' field; orchestrator must record the active objective." }
    }

    $remediationLoop = $null
    if ($checkpointProps -contains 'remediation_loop') {
        $remediationLoop = $checkpoint.remediation_loop
    }
    $loopResult = Test-RemediationLoopShape -RemediationLoop $remediationLoop
    if (-not $loopResult.Ok) {
        return @{ Ok = $false; Message = $loopResult.Message }
    }

    return @{ Ok = $true; Message = $null }
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

$result = Invoke-OrchestratorOutputValidation -RawPayload $env:CLAUDE_HOOK_INPUT
if (-not $result.Ok) {
    Write-Error $result.Message
    exit 1
}

exit 0
