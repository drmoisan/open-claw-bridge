<#
.SYNOPSIS
    Pre-tool-use hook that blocks Write operations on the orchestrator checkpoint
    file when the checkpoint asserts completion without verifiable completion
    evidence.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook on Write or Edit operations. The
    hook activates only when the target file_path normalizes to
    artifacts/orchestration/orchestrator-state.json.

    A written checkpoint asserts completion when any of the following hold:

      next_step == "complete"
      completed_steps contains "S12_complete"
      step8_status, step9_status, or step10_status == "completed"

    When completion is asserted, the write is blocked unless ALL of the following
    completion evidence is present:

      - a non-empty issue-num (top-level, falling back to variables.issue-num);
      - a non-empty feature-folder (top-level, falling back to
        variables.feature-folder);
      - a ci_gate object with conclusion == "success" and a non-empty head_sha.

    When completion evidence is missing the hook emits a PreToolUse JSON
    response with hookSpecificOutput.permissionDecision='deny' and a reason that
    names the specific missing evidence so the caller can remediate. When
    completion is not asserted, the write is allowed via
    hookSpecificOutput.permissionDecision='allow' (backward compatibility).

    Edit tool calls supply only old_string/new_string (a partial patch) and
    cannot be reliably validated without the full target file content, so they
    are allowed by this hook, matching enforce-checkpoint-monotonic.ps1. For
    Write calls whose content is not valid JSON, the hook allows the operation
    and defers to downstream tools to surface the error.

.NOTES
    Compatible with PowerShell 7+. Read-only validation gate.
#>
[CmdletBinding()]
param()

# Dot-source the shared validation helpers. Guarded so a missing file produces a
# clear error and so dot-sourcing this hook in tests loads the helpers too.
$script:CompletionHelpersPath = Join-Path $PSScriptRoot 'enforce-completion-helpers.ps1'
. $script:CompletionHelpersPath

function ConvertFrom-CheckpointJson {
    <#
    .SYNOPSIS
        Wrapper around ConvertFrom-Json for the checkpoint content. Mockable.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Json
    )

    return $Json | ConvertFrom-Json -ErrorAction Stop
}

function Get-CheckpointFileContent {
    <#
    .SYNOPSIS
        Reads the on-disk checkpoint content for the read-then-validate Edit path.
    .DESCRIPTION
        Returns the full file text when the path resolves to a file on disk, or
        $null when the file does not exist. Tests inject a CheckpointReader
        scriptblock instead of mocking this function so no temporary files are
        required.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    return Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
}

function Test-IsCheckpointPath {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $NormalizedPath
    )

    return $NormalizedPath -match '(^|/)artifacts/orchestration/orchestrator-state\.json$'
}

function Get-CheckpointStringValue {
    <#
    .SYNOPSIS
        Returns a trimmed string for a payload property, or an empty string when
        the property is absent or its value is not a non-empty string.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $Payload,

        [Parameter(Mandatory)]
        [string] $Name
    )

    if ($null -eq $Payload) {
        return ''
    }
    if (-not ($Payload.PSObject.Properties.Name -contains $Name)) {
        return ''
    }

    $value = $Payload.$Name
    if ($null -eq $value) {
        return ''
    }

    return ([string]$value).Trim()
}

function Test-CompletionAsserted {
    <#
    .SYNOPSIS
        Returns $true when the checkpoint payload asserts completion via
        next_step, completed_steps, or step8/9/10 status fields.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $Payload
    )

    if ($null -eq $Payload) {
        return $false
    }

    $nextStep = Get-CheckpointStringValue -Payload $Payload -Name 'next_step'
    if ($nextStep -eq 'complete') {
        return $true
    }

    if ($Payload.PSObject.Properties.Name -contains 'completed_steps' -and $Payload.completed_steps) {
        foreach ($step in $Payload.completed_steps) {
            if (([string]$step) -eq 'S12_complete') {
                return $true
            }
        }
    }

    foreach ($statusField in @('step8_status', 'step9_status', 'step10_status')) {
        $status = Get-CheckpointStringValue -Payload $Payload -Name $statusField
        if ($status -eq 'completed') {
            return $true
        }
    }

    return $false
}

function Get-MissingCompletionEvidence {
    <#
    .SYNOPSIS
        Returns the list of missing completion-evidence descriptions for a
        completion-asserting checkpoint. An empty list means all evidence is
        present.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $Payload,

        [Parameter(Mandatory = $false)]
        [scriptblock] $FolderExistsCheck = { param($p) Test-Path -LiteralPath $p -PathType Container },

        [Parameter(Mandatory = $false)]
        [scriptblock] $RoutingMatrixReader
    )

    $missing = @()

    $issueNum = Get-CheckpointStringValue -Payload $Payload -Name 'issue-num'
    if (-not $issueNum -and $null -ne $Payload -and ($Payload.PSObject.Properties.Name -contains 'variables')) {
        $issueNum = Get-CheckpointStringValue -Payload $Payload.variables -Name 'issue-num'
    }
    if (-not (Test-IsValidIssueNum -Value $issueNum)) {
        # Name the offending value so sentinel/placeholder inputs are explicit.
        $missing += "issue-num value '$issueNum' is not a valid issue number (must be digits-only)"
    }

    $featureFolder = Get-CheckpointStringValue -Payload $Payload -Name 'feature-folder'
    if (-not $featureFolder -and $null -ne $Payload -and ($Payload.PSObject.Properties.Name -contains 'variables')) {
        $featureFolder = Get-CheckpointStringValue -Payload $Payload.variables -Name 'feature-folder'
    }
    if (-not (Test-IsValidFeatureFolder -Value $featureFolder -FolderExistsCheck $FolderExistsCheck)) {
        $missing += "feature-folder value '$featureFolder' is not a valid feature folder (must be under docs/features/active/ and exist)"
    }

    $ciGate = $null
    if ($null -ne $Payload -and ($Payload.PSObject.Properties.Name -contains 'ci_gate')) {
        $ciGate = $Payload.ci_gate
    }
    if ($null -eq $ciGate -or $ciGate -isnot [System.Management.Automation.PSCustomObject]) {
        $missing += 'ci_gate (object with conclusion == "success" and non-empty head_sha)'
    }
    else {
        $conclusion = Get-CheckpointStringValue -Payload $ciGate -Name 'conclusion'
        if ($conclusion -ne 'success') {
            $missing += 'ci_gate.conclusion == "success"'
        }
        $headSha = Get-CheckpointStringValue -Payload $ciGate -Name 'head_sha'
        if (-not $headSha) {
            $missing += 'ci_gate.head_sha'
        }
    }

    # PR-gate evidence is required only when the checkpoint's selected route
    # opts into it via requires_pr_gate in the routing matrix. This replaces the
    # former issue-number special-casing with route-driven enforcement.
    $prGateArgs = @{ Payload = $Payload }
    if ($PSBoundParameters.ContainsKey('RoutingMatrixReader') -and $null -ne $RoutingMatrixReader) {
        $prGateArgs['RoutingMatrixReader'] = $RoutingMatrixReader
    }
    if (Test-RouteRequiresPrGate @prGateArgs) {
        $prGate = $null
        if ($null -ne $Payload -and ($Payload.PSObject.Properties.Name -contains 'pr_gate')) {
            $prGate = $Payload.pr_gate
        }
        if ($null -eq $prGate -or $prGate -isnot [System.Management.Automation.PSCustomObject]) {
            $missing += 'pr_gate (object with pr_number, pr_url, head_branch, and head_sha)'
        }
        else {
            foreach ($field in @('pr_number', 'pr_url', 'head_branch', 'head_sha')) {
                if (-not (Get-CheckpointStringValue -Payload $prGate -Name $field)) {
                    $missing += "pr_gate.$field"
                }
            }
            $prHeadSha = Get-CheckpointStringValue -Payload $prGate -Name 'head_sha'
            $ciHeadSha = Get-CheckpointStringValue -Payload $ciGate -Name 'head_sha'
            if ($prHeadSha -and $ciHeadSha -and $prHeadSha -ne $ciHeadSha) {
                $missing += 'ci_gate.head_sha matching pr_gate.head_sha'
            }
        }
    }

    return [string[]]$missing
}

function Resolve-EditedCheckpointContent {
    <#
    .SYNOPSIS
        Returns the patched checkpoint content for an Edit-tool call, or $null
        when the patch cannot be applied against the on-disk checkpoint.
    .DESCRIPTION
        Implements the read-then-validate Edit path. When the tool input carries
        an old_string (an Edit patch), the on-disk checkpoint is read through the
        injectable CheckpointReader seam and the old_string -> new_string
        replacement is applied in memory (no on-disk mutation). Returns $null
        when there is no old_string, the on-disk file does not exist, or the
        old_string is not present in the on-disk content, signalling the caller
        to allow (defer).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $ToolInput,

        [Parameter(Mandatory)]
        [scriptblock] $CheckpointReader
    )

    $oldString = $null
    if ($null -ne $ToolInput -and ($ToolInput.PSObject.Properties.Name -contains 'old_string')) {
        $oldString = [string]$ToolInput.old_string
    }
    if ([string]::IsNullOrEmpty($oldString)) {
        return $null
    }

    $newString = ''
    if ($ToolInput.PSObject.Properties.Name -contains 'new_string') {
        $newString = [string]$ToolInput.new_string
    }

    $onDisk = & $CheckpointReader 'artifacts/orchestration/orchestrator-state.json'
    if ([string]::IsNullOrEmpty([string]$onDisk)) {
        # The on-disk checkpoint does not exist (or is empty); cannot patch.
        return $null
    }

    $onDiskText = [string]$onDisk
    if (-not $onDiskText.Contains($oldString)) {
        # The old_string is not present, so the patch does not apply here.
        return $null
    }

    # Apply the patch in memory using a literal (non-regex) replacement.
    return $onDiskText.Replace($oldString, $newString)
}

function Invoke-CompletionConsistencyDecision {
    <#
    .SYNOPSIS
        Parses CLAUDE_TOOL_INPUT and returns an allow-or-block decision based on
        completion-evidence consistency.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $ToolInputRaw,

        [Parameter(Mandatory = $false)]
        [scriptblock] $FolderExistsCheck = { param($p) Test-Path -LiteralPath $p -PathType Container },

        [Parameter(Mandatory = $false)]
        [scriptblock] $CheckpointReader = { param($Path) Get-CheckpointFileContent -Path $Path },

        [Parameter(Mandatory = $false)]
        [scriptblock] $RoutingMatrixReader
    )

    if (-not $ToolInputRaw) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "enforce-completion-consistency hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $filePath = $toolInput.file_path
    if (-not $filePath) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    $normalized = $filePath -replace '\\', '/'
    if (-not (Test-IsCheckpointPath -NormalizedPath $normalized)) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    # Write tool: validate the content payload directly. Edit tool: no content is
    # supplied, so read the on-disk checkpoint through the injectable seam and
    # apply the old_string -> new_string patch in memory (read-then-validate).
    $content = $toolInput.content
    if (-not $content) {
        $content = Resolve-EditedCheckpointContent -ToolInput $toolInput -CheckpointReader $CheckpointReader
        if (-not $content) {
            # No content, and the Edit could not be resolved against on-disk
            # state (missing file or non-matching patch): defer and allow.
            return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
        }
    }

    try {
        $payload = ConvertFrom-CheckpointJson -Json $content
    }
    catch {
        # The content itself is not valid JSON. Let downstream tools surface the
        # error rather than blocking with a misleading reason here.
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    if (-not (Test-CompletionAsserted -Payload $payload)) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    $missingArgs = @{ Payload = $payload; FolderExistsCheck = $FolderExistsCheck }
    if ($PSBoundParameters.ContainsKey('RoutingMatrixReader') -and $null -ne $RoutingMatrixReader) {
        $missingArgs['RoutingMatrixReader'] = $RoutingMatrixReader
    }
    $missing = Get-MissingCompletionEvidence @missingArgs
    if ($missing.Count -eq 0) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    $reason = "COMPLETION_CONSISTENCY_BLOCKED: the checkpoint asserts completion but is missing required completion evidence: $($missing -join ', '). A completion-asserting checkpoint must include a non-empty issue-num, a non-empty feature-folder, and a ci_gate object with conclusion == 'success' and a non-empty head_sha; routes whose requires_pr_gate is true must also include pr_gate evidence with a matching head_sha. Supply the missing evidence or remove the completion assertion."
    return [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    }
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-CompletionConsistencyDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
}
catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
