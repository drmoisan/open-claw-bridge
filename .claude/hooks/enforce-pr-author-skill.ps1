<#
.SYNOPSIS
    Pre-tool-use hook that enforces the pr-author skill is used before gh pr create or gh pr edit.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook before any Bash command runs. Reads
    tool input JSON from the CLAUDE_TOOL_INPUT environment variable, inspects the
    attempted command, and blocks gh pr create / gh pr edit commands that bypass the
    pr-author skill workflow.

    Required sequence:
      1. mcp__drm-copilot__collect_pr_context writes artifacts/pr_context.summary.txt
      2. pr-author skill produces the body text; the pr-author agent writes
         artifacts/pr_body_<N>.md and a sibling integrity receipt
         artifacts/pr_body_<N>.receipt.json
      3. gh pr create --body-file artifacts/pr_body_<N>.md
         (or gh pr edit --body-file ...)

    Block cases:
      Case A - gh pr create or gh pr edit with --body (inline, no --body-file): blocked.
      Case B - gh pr create with neither --body nor --body-file: blocked.
      Case C - gh pr create or gh pr edit with --body-file but context artifact absent: blocked.
      Preflight - --body-file/context present: orchestrator-state checkpoint must pass
                  --require-pr-creation-ready before receipt verification runs, else blocked.
      Receipt - preflight passed: the SHA-256 receipt is verified in five ordered checks
                (Section below). The first failing check blocks.

    Receipt verification decision order on the --body-file-with-context path:
      PR_BODY_PATH_NONCANONICAL -> PR_AUTHOR_RECEIPT_MISSING -> PR_AUTHOR_RECEIPT_NUMBER_MISMATCH
      -> PR_AUTHOR_RECEIPT_HASH_MISMATCH -> PR_AUTHOR_RECEIPT_STALE -> allow.

.NOTES
    Compatible with PowerShell 7+. No external module dependencies.

    Enforcement strength: the SHA-256 receipt is a policy-level integrity check that binds the
    PR body bytes to the receipt the pr-author agent wrote. It is not a cryptographic or security
    boundary: any actor with Write access to artifacts/ can replace both the body file and the
    receipt together, because all agents share the same filesystem and the runtime exposes no
    native agent-identity signal at Bash PreToolUse time. The mechanism prevents accidental bypass
    and requires a deliberate, documented act to circumvent. It MUST NOT be described as
    tamper-proof or as a security boundary.
#>
[CmdletBinding()]
param()

$script:PrContextArtifactPath = 'artifacts/pr_context.summary.txt'
$script:OrchestratorStateCheckpointPath = 'artifacts/orchestration/orchestrator-state.json'

Import-Module (Join-Path $PSScriptRoot '../lib/orchestrator-state/OrchestratorState.psm1') -Force

function Get-PrContextArtifactExistence {
    <#
    .SYNOPSIS
        Wrapper around Test-Path for the PR context artifact. Tests mock this function.
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    return [bool](Test-Path -LiteralPath $script:PrContextArtifactPath)
}

function Get-PrBodyFileBytes {
    <#
    .SYNOPSIS
        Read the raw bytes of the PR body file. Tests mock this function (read seam).
    .DESCRIPTION
        Returns the byte content of the supplied body-file path, or $null when the file is absent.
        This is the injectable boundary for body-file bytes in tests; no test writes the body file
        to disk. The bytes are hashed inline by the receipt verification function.
    .PARAMETER BodyFilePath
        The relative path to the PR body file (for example artifacts/pr_body_5.md).
    .OUTPUTS
        System.Byte[] or $null
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'The plural noun names the byte-array return; the seam name is fixed by the receipt contract.')]
    [CmdletBinding()]
    [OutputType([byte[]])]
    param(
        [Parameter(Mandatory)]
        [string] $BodyFilePath
    )

    if (-not (Test-Path -LiteralPath $BodyFilePath)) {
        return $null
    }

    return [System.IO.File]::ReadAllBytes($BodyFilePath)
}

function Get-PrAuthorReceiptContent {
    <#
    .SYNOPSIS
        Read the raw JSON text of the PR body receipt. Tests mock this function (read seam).
    .DESCRIPTION
        Returns the raw text content of the sibling receipt file artifacts/pr_body_<N>.receipt.json,
        or $null when the receipt file is absent. This is the injectable boundary for receipt
        content in tests; no test writes the receipt file to disk.
    .PARAMETER ReceiptFilePath
        The relative path to the receipt file (for example artifacts/pr_body_5.receipt.json).
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $ReceiptFilePath
    )

    if (-not (Test-Path -LiteralPath $ReceiptFilePath)) {
        return $null
    }

    return (Get-Content -LiteralPath $ReceiptFilePath -Raw)
}

function Get-PrContextSummaryLastWriteUtc {
    <#
    .SYNOPSIS
        Return the UTC last-write time of the PR context summary. Tests mock this function (seam).
    .DESCRIPTION
        Returns the LastWriteTimeUtc of artifacts/pr_context.summary.txt, or $null when the file is
        absent. The staleness check compares the receipt's created_at against this value; both are
        artifact metadata, so no wall-clock seam is required.
    .OUTPUTS
        System.DateTime or $null
    #>
    [CmdletBinding()]
    [OutputType([datetime])]
    param()

    if (-not (Test-Path -LiteralPath $script:PrContextArtifactPath)) {
        return $null
    }

    return (Get-Item -LiteralPath $script:PrContextArtifactPath).LastWriteTimeUtc
}

. (Join-Path $PSScriptRoot 'enforce-pr-author-skill.epic-base-branch.ps1')

function Test-PrAuthorReceiptVerification {
    <#
    .SYNOPSIS
        Verify the SHA-256 receipt and return a block-reason string, or $null when verified.
    .DESCRIPTION
        Runs the six ordered receipt checks on the --body-file-with-context path. Each check is its
        own short-circuiting branch; the first failure returns its reason code:
          1. PR_BODY_PATH_NONCANONICAL        - --body-file path does not match the canonical
                                                 artifacts/pr_body_<N>.md pattern (case-sensitive).
          2. PR_AUTHOR_RECEIPT_MISSING        - sibling artifacts/pr_body_<N>.receipt.json absent.
          3. PR_AUTHOR_RECEIPT_NUMBER_MISMATCH- receipt.number (integer) != <N> from the path.
          4. PR_AUTHOR_RECEIPT_HASH_MISMATCH  - inline SHA-256 (lowercase hex) of the body bytes
                                                 != receipt.sha256.
          5. PR_AUTHOR_RECEIPT_STALE          - receipt.created_at (UTC) not strictly newer than the
                                                 context summary last-write time.
          6. EPIC_BASE_BRANCH_MISMATCH        - under epic_mode, gh pr create does not carry a
                                                 matching --base <epic_context.integration_branch>.
        Returns $null when all six checks pass (allow). All disk access flows through the four
        injectable seams (Get-PrBodyFileBytes, Get-PrAuthorReceiptContent,
        Get-PrContextSummaryLastWriteUtc, Get-PrAuthorCheckpointContent); SHA-256 is computed
        inline. This is a policy-level integrity check, not a cryptographic control: any actor
        with Write access to artifacts/ can replace the body file and the receipt together.
    .PARAMETER CommandText
        The Bash command text containing the --body-file argument.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText
    )

    # Check 1: the --body-file argument must match the canonical artifacts/pr_body_<N>.md pattern.
    # The match is case-sensitive (-cmatch) so a non-canonical path is rejected before any read.
    if ($CommandText -cnotmatch '--body-file\s+artifacts/pr_body_(\d+)\.md\b') {
        return "PR_BODY_PATH_NONCANONICAL: ``--body-file`` must reference a canonical ``artifacts/pr_body_<N>.md`` file produced by the pr-author skill. The path supplied does not match ``artifacts/pr_body_<N>.md``."
    }

    $bodyNumber = [int]$Matches[1]
    $bodyFilePath = "artifacts/pr_body_$bodyNumber.md"
    $receiptFilePath = "artifacts/pr_body_$bodyNumber.receipt.json"

    # Check 2: the sibling receipt file must exist (read via the injectable seam).
    $receiptRaw = Get-PrAuthorReceiptContent -ReceiptFilePath $receiptFilePath
    if ([string]::IsNullOrWhiteSpace($receiptRaw)) {
        return "PR_AUTHOR_RECEIPT_MISSING: ``$receiptFilePath`` is absent. The pr-author agent must write the SHA-256 receipt alongside ``$bodyFilePath`` before issuing ``gh pr create``/``gh pr edit --body-file``."
    }

    # A receipt that is present but not valid JSON cannot be verified; treat it as missing content.
    try {
        $receipt = $receiptRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        return "PR_AUTHOR_RECEIPT_MISSING: ``$receiptFilePath`` is not valid JSON. The pr-author agent must write a well-formed receipt with ``number``, ``sha256``, and ``created_at``."
    }

    # Check 3: the receipt number must equal the <N> extracted from the canonical path.
    $receiptNumber = $null
    $parsedNumber = 0
    if ([int]::TryParse([string]$receipt.number, [ref] $parsedNumber)) {
        $receiptNumber = $parsedNumber
    }
    if ($receiptNumber -ne $bodyNumber) {
        return "PR_AUTHOR_RECEIPT_NUMBER_MISMATCH: ``$receiptFilePath`` ``number`` ($($receipt.number)) does not equal the body-file number ($bodyNumber). The receipt must bind to ``$bodyFilePath``."
    }

    # Check 4: the inline SHA-256 (lowercase hex) of the body bytes must equal receipt.sha256.
    $bodyBytes = Get-PrBodyFileBytes -BodyFilePath $bodyFilePath
    if ($null -eq $bodyBytes) {
        return "PR_AUTHOR_RECEIPT_HASH_MISMATCH: ``$bodyFilePath`` could not be read to verify its SHA-256 against ``$receiptFilePath``."
    }

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash($bodyBytes)
    } finally {
        $sha256.Dispose()
    }
    $computedHash = ([System.BitConverter]::ToString($hashBytes) -replace '-', '').ToLowerInvariant()

    if ($computedHash -ne ([string]$receipt.sha256).ToLowerInvariant()) {
        return "PR_AUTHOR_RECEIPT_HASH_MISMATCH: the SHA-256 of ``$bodyFilePath`` does not equal ``sha256`` in ``$receiptFilePath``. The body file was modified after the receipt was written."
    }

    # Check 5: receipt.created_at (UTC) must be strictly newer than the context summary last-write.
    $createdAt = [DateTime]::MinValue
    $createdParsed = [DateTime]::TryParse(
        [string]$receipt.created_at,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor [System.Globalization.DateTimeStyles]::AssumeUniversal,
        [ref] $createdAt)
    if (-not $createdParsed) {
        return "PR_AUTHOR_RECEIPT_STALE: ``$receiptFilePath`` has a missing or unparseable ``created_at``. The pr-author agent must record a UTC ISO-8601 ``created_at`` strictly newer than ``$script:PrContextArtifactPath``."
    }

    $contextLastWrite = Get-PrContextSummaryLastWriteUtc
    if (($null -eq $contextLastWrite) -or ($createdAt -le $contextLastWrite)) {
        return "PR_AUTHOR_RECEIPT_STALE: ``$receiptFilePath`` ``created_at`` is not strictly newer than the last-write time of ``$script:PrContextArtifactPath``. The pr-author agent must regenerate the body and receipt after refreshing the PR context."
    }

    # Check 6: under epic_mode, gh pr create must carry a matching --base override.
    $epicBaseBranchReason = Test-EpicBaseBranchOverride -CommandText $CommandText
    if ($epicBaseBranchReason) {
        return $epicBaseBranchReason
    }

    return $null
}

function Get-PrAuthorBypassReason {
    <#
    .SYNOPSIS
        Inspect the command text and return a block reason string, or $null when the command is allowed.
    .DESCRIPTION
        Returns PR_AUTHOR_SKILL_BLOCKED when gh pr create or gh pr edit is run with --body (inline,
        no --body-file), or when gh pr create is run with no body flag at all. Returns
        PR_CONTEXT_MISSING when --body-file is present but the context artifact does not exist on
        disk. When --body-file is present and the context artifact exists, verifies the SHA-256
        receipt via Test-PrAuthorReceiptVerification and returns its block reason
        (PR_BODY_PATH_NONCANONICAL / PR_AUTHOR_RECEIPT_*) when verification fails. Returns $null for
        all allowed patterns. Cases A, B, and C are evaluated first and unchanged; receipt
        verification only extends the previously-allowed --body-file-with-context path.
    .PARAMETER CommandText
        The Bash command text extracted from CLAUDE_TOOL_INPUT.
    .PARAMETER ContextExists
        Whether artifacts/pr_context.summary.txt currently exists on disk.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText,

        [Parameter(Mandatory)]
        [bool] $ContextExists
    )

    # Only act on gh pr create or gh pr edit subcommands.
    $isPrCreate = $CommandText -match '(?i)\bgh\s+pr\s+create\b'
    $isPrEdit = $CommandText -match '(?i)\bgh\s+pr\s+edit\b'

    if (-not $isPrCreate -and -not $isPrEdit) {
        return $null
    }

    $hasBodyFile = $CommandText -match '(?i)--body-file\b'
    $hasInlineBody = $CommandText -match '(?i)--body(?!-file)\b'

    # Case A: gh pr create OR gh pr edit with inline --body (not --body-file). Evaluated before the
    # gh pr edit no-body allow short-circuit so inline-body edits are blocked, not allowed.
    if (($isPrCreate -or $isPrEdit) -and $hasInlineBody -and -not $hasBodyFile) {
        return "PR_AUTHOR_SKILL_BLOCKED: ``gh pr create`` and ``gh pr edit`` must use ``--body-file`` with a file produced by the pr-author skill from ``$script:PrContextArtifactPath``. Run ``mcp__drm-copilot__collect_pr_context`` to generate the context file, apply the pr-author skill to produce ``artifacts/pr_body_<N>.md``, then pass that file via ``--body-file``."
    }

    if ($isPrCreate) {
        # Case B: gh pr create with no body flag at all.
        if (-not $hasInlineBody -and -not $hasBodyFile) {
            return "PR_AUTHOR_SKILL_BLOCKED: New PRs require ``--body-file``. Run ``mcp__drm-copilot__collect_pr_context`` to generate ``$script:PrContextArtifactPath``, apply the pr-author skill to produce ``artifacts/pr_body_<N>.md``, then pass that file via ``--body-file``."
        }
    }

    if ($isPrEdit) {
        # gh pr edit with no --body or --body-file (e.g., --title, --add-label, --reviewer) is allowed.
        if (-not $hasInlineBody -and -not $hasBodyFile) {
            return $null
        }
    }

    # Case C: --body-file present but context artifact is absent.
    if ($hasBodyFile -and -not $ContextExists) {
        return "PR_CONTEXT_MISSING: ``$script:PrContextArtifactPath`` is absent. Run ``mcp__drm-copilot__collect_pr_context`` before creating or editing the PR body."
    }

    # Orchestrator-state preflight: runs inside this same PreToolUse hook (so it cannot be
    # bypassed by invoking gh pr create/edit directly) before receipt verification.
    if ($hasBodyFile -and $ContextExists) {
        $preflightResult = Invoke-OrchestratorStatePreflight -CheckpointPath $script:OrchestratorStateCheckpointPath
        if ($preflightResult.HasErrors) {
            $preflightSummary = if ([string]::IsNullOrWhiteSpace($preflightResult.ErrorText)) {
                "checkpoint missing at $script:OrchestratorStateCheckpointPath"
            } else {
                $preflightResult.ErrorText
            }
            return "ORCHESTRATOR_STATE_PREFLIGHT_FAILED: $preflightSummary"
        }
    }

    # Receipt verification: extends, and does not replace, the previously-allowed path.
    if ($hasBodyFile -and $ContextExists) {
        $receiptReason = Test-PrAuthorReceiptVerification -CommandText $CommandText
        if ($receiptReason) {
            return $receiptReason
        }
    }

    return $null
}

function Invoke-PrAuthorSkillDecision {
    <#
    .SYNOPSIS
        Parse CLAUDE_TOOL_INPUT and return an allow-or-block decision.
    .PARAMETER ToolInputRaw
        The raw JSON tool payload supplied by Claude Code.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    .NOTES
        Missing tool input or missing command text is treated as allow.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $ToolInputRaw
    )

    if (-not $ToolInputRaw) {
        return Get-PrAuthorSkillAllowDecision
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "enforce-pr-author-skill hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $commandText = $toolInput.command
    if (-not $commandText) {
        return Get-PrAuthorSkillAllowDecision
    }

    $contextExists = Get-PrContextArtifactExistence
    $reason = Get-PrAuthorBypassReason -CommandText $commandText -ContextExists $contextExists

    if ($reason) {
        return Get-PrAuthorSkillBlockDecision -Reason $reason
    }

    return Get-PrAuthorSkillAllowDecision
}

function Get-PrAuthorSkillAllowDecision {
    <#
    .SYNOPSIS
        Construct the PreToolUse allow decision for a permitted Bash command.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    #>
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

function Get-PrAuthorSkillBlockDecision {
    <#
    .SYNOPSIS
        Construct the PreToolUse deny decision for a forbidden Bash command.
    .PARAMETER Reason
        The specific deny reason to surface in the decision.
    .OUTPUTS
        System.Collections.Specialized.OrderedDictionary
    #>
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

function Test-PrAuthorBypassRequired {
    <#
    .SYNOPSIS
        Return $true when a Bash command requires the pr-author skill to run first.
    .PARAMETER CommandText
        The Bash command text extracted from CLAUDE_TOOL_INPUT.
    .PARAMETER ContextExists
        Whether artifacts/pr_context.summary.txt currently exists on disk.
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText,

        [Parameter(Mandatory)]
        [bool] $ContextExists
    )

    return ($null -ne (Get-PrAuthorBypassReason -CommandText $CommandText -ContextExists $ContextExists))
}

# Allow dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-PrAuthorSkillDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
} catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
