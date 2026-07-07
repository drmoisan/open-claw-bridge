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
param(
    [Parameter(Mandatory = $false)]
    [string] $CheckpointPath = 'artifacts/orchestration/orchestrator-state.json',

    [Parameter(Mandatory = $false)]
    [string] $ArtifactType = 'orchestrator-state'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '../lib/orchestrator-state/OrchestratorState.psm1') -Force

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

function Test-HumanInteractionShape {
    <#
    .SYNOPSIS
        Validates the optional human_interaction sub-object against the
        invariants documented in .claude/rules/orchestrator-state.md and
        enforced by scripts/dev_tools/validate_orchestrator_state.py,
        enforcing the autonomous-execution mandate at the completion gate.
    .DESCRIPTION
        Returns a hashtable with keys:
          - Ok:      $true if the field is absent or every requirement is resolved.
          - Message: rejection message naming the first unresolved requirement; $null on success.
        A null human_interaction (absent key) passes the gate. When present,
        the requirements array is inspected and DONE is blocked when, in order:
          - requirements is missing or non-array.
          - any requirement has a missing/blank response.
          - any requirement has a response outside the enum
            (scope_change | exception | halt).
          - any requirement has response == 'halt'.
          - any requirement has response == 'exception' with a missing/empty
            runbook_path, or a runbook_path whose file does not exist on disk
            (existence is checked through the injected FileExistsCheck seam).
        FileExistsCheck is an injectable scriptblock so tests can exercise the
        existence branch without writing temporary files. It defaults to
        Test-Path -PathType Leaf.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        $HumanInteraction,

        [Parameter(Mandatory = $false)]
        [scriptblock] $FileExistsCheck = { param($Path) Test-Path -LiteralPath $Path -PathType Leaf }
    )

    if ($null -eq $HumanInteraction) {
        return @{ Ok = $true; Message = $null }
    }

    $hiProps = @($HumanInteraction.PSObject.Properties.Name)
    if ($hiProps -notcontains 'requirements') {
        return @{ Ok = $false; Message = "orchestrator hook: 'human_interaction' is present but 'requirements' array is missing." }
    }

    $requirements = @($HumanInteraction.requirements)
    $allowedResponses = @('scope_change', 'exception', 'halt')

    for ($i = 0; $i -lt $requirements.Count; $i++) {
        $req = $requirements[$i]
        $reqProps = @($req.PSObject.Properties.Name)

        $response = $null
        if ($reqProps -contains 'response') { $response = [string]$req.response }

        if ([string]::IsNullOrWhiteSpace($response)) {
            return @{ Ok = $false; Message = "orchestrator hook: 'human_interaction.requirements[$i]' has no resolved 'response'. Every unautomatable requirement must resolve to one of: scope_change, exception, halt." }
        }

        if ($allowedResponses -notcontains $response) {
            return @{ Ok = $false; Message = "orchestrator hook: 'human_interaction.requirements[$i]' has 'response' value '$response' outside the allowed set (scope_change, exception, halt)." }
        }

        if ($response -eq 'halt') {
            return @{ Ok = $false; Message = "orchestrator hook: 'human_interaction.requirements[$i]' has 'response' == 'halt'; DONE is blocked while a halt is present." }
        }

        if ($response -eq 'exception') {
            $runbookPath = $null
            if ($reqProps -contains 'runbook_path') { $runbookPath = [string]$req.runbook_path }

            if ([string]::IsNullOrWhiteSpace($runbookPath)) {
                return @{ Ok = $false; Message = "orchestrator hook: 'human_interaction.requirements[$i]' has 'response' == 'exception' but no non-empty 'runbook_path'. A permitted exception requires a runbook." }
            }

            if (-not (& $FileExistsCheck $runbookPath)) {
                return @{ Ok = $false; Message = "orchestrator hook: 'human_interaction.requirements[$i]' references runbook_path '$runbookPath' but no file exists at that location." }
            }
        }
    }

    return @{ Ok = $true; Message = $null }
}

function Invoke-RoutingContractValidation {
    <#
    .SYNOPSIS
        Runs the authoritative Python routing-contract validator against the
        on-disk checkpoint and reports whether it emitted errors.
    .DESCRIPTION
        Invokes the validator through an injectable subprocess scriptblock seam.
        The default Invoker runs the authoritative Python CLI:
          python -m scripts.dev_tools.validate_orchestration_artifacts \
              <ArtifactType> <CheckpointPath> --require-complete
        Tests inject a mock scriptblock so no Python process runs. The function
        does not reimplement routing logic; it delegates to the Python validator.
        ArtifactType defaults to 'orchestrator-state' so the default invocation
        string is unchanged for every existing caller of this hook.

        Returns a hashtable with keys:
          - HasErrors:  $true when the validator reported a non-zero exit or
                        produced any error text; $false when clean.
          - ErrorText:  the validator's combined output text (empty on success).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $CheckpointPath,

        [Parameter(Mandatory = $false)]
        [string] $ArtifactType = 'orchestrator-state',

        [Parameter(Mandatory = $false)]
        [scriptblock] $Invoker = {
            param($Path, $Type)
            # Capability detection: use the authoritative Python CLI when
            # scripts.dev_tools is importable (drm-copilot); otherwise fall back to
            # the portable PowerShell completion module that travels with the
            # pushed-down pack. The portable path performs the presence-level
            # required-once-delegated existence gate and still fails closed.
            if (Test-PythonOrchestratorValidatorAvailable) {
                $output = & python -m scripts.dev_tools.validate_orchestration_artifacts `
                    $Type $Path --require-complete --require-model-routing 2>&1
                [pscustomobject]@{
                    ExitCode = $LASTEXITCODE
                    Output   = ($output | Out-String)
                }
            } else {
                # Import the portable completion module only when its function is not
                # already available, so a repeated call (or a test that pre-imports and
                # mocks the function) does not reload the module and reset the seam.
                if (-not (Get-Command -Name Test-OrchestratorStateCompletionReadiness -ErrorAction SilentlyContinue)) {
                    Import-Module (Join-Path $PSScriptRoot '../lib/orchestrator-state/OrchestratorStateCompletion.psm1') -Force
                }
                $portable = Test-OrchestratorStateCompletionReadiness -CheckpointPath $Path
                [pscustomobject]@{
                    ExitCode = $portable.ExitCode
                    Output   = $portable.Output
                }
            }
        }
    )

    $result = & $Invoker $CheckpointPath $ArtifactType
    $exitCode = 0
    if ($null -ne $result -and ($result.PSObject.Properties.Name -contains 'ExitCode')) {
        $exitCode = [int]$result.ExitCode
    }
    $outputText = ''
    if ($null -ne $result -and ($result.PSObject.Properties.Name -contains 'Output')) {
        $outputText = ([string]$result.Output).Trim()
    }

    # The validator signals a routing-contract failure either through a non-zero
    # exit code or through emitted error text; either condition blocks DONE.
    $hasErrors = ($exitCode -ne 0) -or (-not [string]::IsNullOrWhiteSpace($outputText))
    return @{ HasErrors = $hasErrors; ErrorText = $outputText }
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
        [string] $CheckpointPath = 'artifacts/orchestration/orchestrator-state.json',
        [string] $ArtifactType = 'orchestrator-state',

        [Parameter(Mandatory = $false)]
        [scriptblock] $RoutingInvoker
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

    $humanInteraction = $null
    if ($checkpointProps -contains 'human_interaction') {
        $humanInteraction = $checkpoint.human_interaction
    }
    $hiResult = Test-HumanInteractionShape -HumanInteraction $humanInteraction
    if (-not $hiResult.Ok) {
        return @{ Ok = $false; Message = $hiResult.Message }
    }

    # Delegate to the authoritative Python routing-contract validator. The
    # optional RoutingInvoker seam lets tests inject a mock; the default seam
    # produces the real subprocess call.
    $routingArgs = @{ CheckpointPath = $CheckpointPath; ArtifactType = $ArtifactType }
    if ($PSBoundParameters.ContainsKey('RoutingInvoker') -and $null -ne $RoutingInvoker) {
        $routingArgs['Invoker'] = $RoutingInvoker
    }
    $routingResult = Invoke-RoutingContractValidation @routingArgs
    if ($routingResult.HasErrors) {
        # One subprocess call now covers both --require-complete and
        # --require-model-routing. Surface a model-routing gate failure under its
        # own block reason (its errors name model_routing_receipts or
        # complexity_assessments); otherwise fall back to the routing-contract
        # block reason for a generic completion/routing failure.
        if ($routingResult.ErrorText -match 'model_routing_receipts|complexity_assessments') {
            return @{ Ok = $false; Message = "MODEL_ROUTING_BLOCKED: $($routingResult.ErrorText)" }
        }
        return @{ Ok = $false; Message = "ROUTING_CONTRACT_BLOCKED: $($routingResult.ErrorText)" }
    }

    return @{ Ok = $true; Message = $null }
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

$result = Invoke-OrchestratorOutputValidation -RawPayload $env:CLAUDE_HOOK_INPUT -CheckpointPath $CheckpointPath -ArtifactType $ArtifactType
if (-not $result.Ok) {
    Write-Error $result.Message
    exit 1
}

exit 0
