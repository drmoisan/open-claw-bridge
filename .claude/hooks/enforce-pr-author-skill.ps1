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
      2. pr-author skill reads that file and writes artifacts/pr_body_<N>.md AND a sibling
         provenance receipt artifacts/pr_body_<N>.receipt.json
      3. gh pr create --body-file artifacts/pr_body_<N>.md
         (or gh pr edit --body-file ...)

    Shape-only block cases (preserved):
      Case A - gh pr create with --body (inline, no --body-file): blocked.
      Case B - gh pr create with neither --body nor --body-file: blocked.
      Case C - gh pr create or gh pr edit with --body-file but context artifact absent: blocked.

    Provenance block cases (added). After Case C passes for a --body-file command, the
    following checks run in order and the FIRST failure is returned:
      Case D - PR_BODY_PATH_NONCANONICAL: body-file path is not artifacts/pr_body_<N>.md.
      Case E - PR_AUTHOR_RECEIPT_MISSING: sibling artifacts/pr_body_<N>.receipt.json absent.
      Case G - PR_AUTHOR_RECEIPT_NUMBER_MISMATCH: receipt.number != <N> from the filename.
      Case F - PR_AUTHOR_RECEIPT_HASH_MISMATCH: SHA-256 of the body file != receipt.sha256.
      Case H - PR_AUTHOR_RECEIPT_STALE: receipt.created_at is not strictly newer than the
               last-write time of artifacts/pr_context.summary.txt.

    Together these require a canonical, hash-verified, fresh pr-author receipt rather than
    merely a command of the right shape.

.NOTES
    Compatible with PowerShell 7+. No external module dependencies.
#>
[CmdletBinding()]
param()

$script:PrContextArtifactPath = 'artifacts/pr_context.summary.txt'
$script:PrBodyCanonicalPattern = '^artifacts[/\\]pr_body_(\d+)\.md$'

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

function Get-PrContextWriteTime {
    <#
    .SYNOPSIS
        Return the last-write time of the PR context summary artifact.
    .DESCRIPTION
        Adapter seam over the filesystem clock. Tests mock this function so the staleness
        comparison (Case H) can be exercised without touching disk or the real clock.
    .OUTPUTS
        System.DateTime
    #>
    [CmdletBinding()]
    [OutputType([datetime])]
    param()

    return (Get-Item -LiteralPath $script:PrContextArtifactPath).LastWriteTimeUtc
}

function Test-PrBodyReceiptPresence {
    <#
    .SYNOPSIS
        Return whether the pr-author provenance receipt is present for a given receipt path.
    .DESCRIPTION
        Adapter seam over Test-Path. Tests mock this function to control Case E.
    .PARAMETER Path
        The receipt file path (artifacts/pr_body_<N>.receipt.json).
    .OUTPUTS
        System.Boolean
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    return [bool](Test-Path -LiteralPath $Path)
}

function Get-PrBodyReceipt {
    <#
    .SYNOPSIS
        Read and parse the pr-author provenance receipt JSON.
    .DESCRIPTION
        Adapter seam over the filesystem and JSON parsing. Returns an object exposing the
        number, sha256, and created_at fields. Tests mock this function to control Cases
        G, F, and H without touching disk.
    .PARAMETER Path
        The receipt file path (artifacts/pr_body_<N>.receipt.json).
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop)
}

function Get-PrBodyFileHash {
    <#
    .SYNOPSIS
        Return the lowercase hex SHA-256 of the PR body file's bytes.
    .DESCRIPTION
        Wraps Get-FileHash so tests can mock the hashing seam (Case F) without a real file.
    .PARAMETER Path
        The PR body file path (artifacts/pr_body_<N>.md).
    .OUTPUTS
        System.String
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PrBodyFilePath {
    <#
    .SYNOPSIS
        Extract the --body-file value from a command string.
    .DESCRIPTION
        Pure parser. Supports both `--body-file <value>` and `--body-file=<value>` forms,
        and values wrapped in single or double quotes. Returns the unquoted path, or $null
        when no --body-file value is present.
    .PARAMETER CommandText
        The Bash command text extracted from CLAUDE_TOOL_INPUT.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $CommandText
    )

    # --body-file=VALUE or --body-file VALUE, where VALUE is a quoted string or a bare token.
    $pattern = '(?i)--body-file(?:=|\s+)(?:"([^"]*)"|''([^'']*)''|([^\s]+))'
    $match = [regex]::Match($CommandText, $pattern)
    if (-not $match.Success) {
        return $null
    }

    foreach ($groupIndex in 1, 2, 3) {
        $group = $match.Groups[$groupIndex]
        if ($group.Success) {
            return $group.Value
        }
    }

    return $null
}

function Get-PrAuthorProvenanceReason {
    <#
    .SYNOPSIS
        Pure decision core for provenance checks D, E, G, F, and H over resolved facts.
    .DESCRIPTION
        Given the already-resolved facts about the body-file path and receipt, return the
        first failing block reason string, or $null when all provenance checks pass. This
        function performs no I/O; the caller resolves facts through the adapter seams.
    .PARAMETER BodyFilePath
        The parsed --body-file value.
    .PARAMETER ReceiptExists
        Whether the sibling receipt file exists.
    .PARAMETER Receipt
        The parsed receipt object (number, sha256, created_at), or $null when absent.
    .PARAMETER BodyFileHash
        The lowercase hex SHA-256 of the body file, or $null when not computed.
    .PARAMETER ContextWriteTime
        The last-write time (UTC) of the PR context summary artifact.
    .OUTPUTS
        System.String or $null
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $BodyFilePath,

        [Parameter(Mandatory)]
        [bool] $ReceiptExists,

        [Parameter()]
        [object] $Receipt,

        [Parameter()]
        [AllowNull()]
        [string] $BodyFileHash,

        [Parameter()]
        [AllowNull()]
        [Nullable[datetime]] $ContextWriteTime
    )

    # Case D: path must match the canonical regex artifacts/pr_body_<N>.md.
    $canonicalMatch = [regex]::Match($BodyFilePath, $script:PrBodyCanonicalPattern)
    if (-not $canonicalMatch.Success) {
        return "PR_BODY_PATH_NONCANONICAL: ``--body-file`` value ``$BodyFilePath`` is not a canonical pr-author body. The path must match ``artifacts/pr_body_<N>.md``. Apply the pr-author handoff to produce ``artifacts/pr_body_<N>.md`` and its sibling receipt, then reference that file."
    }

    $bodyNumber = $canonicalMatch.Groups[1].Value

    # Case E: sibling receipt must exist.
    if (-not $ReceiptExists) {
        return "PR_AUTHOR_RECEIPT_MISSING: the pr-author receipt ``artifacts/pr_body_$bodyNumber.receipt.json`` is absent. The pr-author handoff must emit a provenance receipt alongside ``$BodyFilePath`` before the PR body can be used."
    }

    # Case G: receipt number must match the filename number.
    $receiptNumber = [string]$Receipt.number
    if ($receiptNumber -ne $bodyNumber) {
        return "PR_AUTHOR_RECEIPT_NUMBER_MISMATCH: receipt ``number`` (``$receiptNumber``) does not match the body filename number (``$bodyNumber``). Re-run the pr-author handoff so the receipt and ``$BodyFilePath`` agree."
    }

    # Case F: body-file hash must match the receipt hash.
    $receiptHash = ([string]$Receipt.sha256).ToLowerInvariant()
    $actualHash = ([string]$BodyFileHash).ToLowerInvariant()
    if ($actualHash -ne $receiptHash) {
        return "PR_AUTHOR_RECEIPT_HASH_MISMATCH: SHA-256 of ``$BodyFilePath`` (``$actualHash``) does not match the receipt ``sha256`` (``$receiptHash``). The body was modified after the pr-author handoff; re-run the handoff to regenerate the body and receipt."
    }

    # Case H: receipt must be strictly newer than the context summary write time.
    $createdAt = [datetimeoffset]::Parse(
        [string]$Receipt.created_at,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
    ).UtcDateTime
    $contextTime = ([datetime]$ContextWriteTime).ToUniversalTime()
    if ($createdAt -le $contextTime) {
        return "PR_AUTHOR_RECEIPT_STALE: receipt ``created_at`` (``$($Receipt.created_at)``) is not newer than the PR context summary write time (``$($contextTime.ToString('o'))``). The receipt predates the current context; re-run ``mcp__drm-copilot__collect_pr_context`` and the pr-author handoff."
    }

    return $null
}

function Get-PrAuthorBypassReason {
    <#
    .SYNOPSIS
        Inspect the command text and return a block reason string, or $null when the command is allowed.
    .DESCRIPTION
        Returns PR_AUTHOR_SKILL_BLOCKED when gh pr create is run with --body (inline) or with no body
        flag at all (Cases A and B). Returns PR_CONTEXT_MISSING when --body-file is present but the
        context artifact does not exist on disk (Case C). When --body-file is present and the context
        exists, resolves provenance facts through the adapter seams and delegates to
        Get-PrAuthorProvenanceReason for Cases D, E, G, F, and H. Returns $null for all allowed patterns.
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

    if ($isPrCreate) {
        # Case A: gh pr create with inline --body (not --body-file).
        if ($hasInlineBody -and -not $hasBodyFile) {
            return "PR_AUTHOR_SKILL_BLOCKED: ``gh pr create`` must use ``--body-file`` with a file produced by the pr-author skill from ``$script:PrContextArtifactPath``. Run ``mcp__drm-copilot__collect_pr_context`` to generate the context file, apply the pr-author skill to produce ``artifacts/pr_body_<N>.md``, then pass that file via ``--body-file``."
        }

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

    # Provenance checks (Cases D, E, G, F, H) for --body-file commands once Case C passes.
    if ($hasBodyFile) {
        $bodyFilePath = Get-PrBodyFilePath -CommandText $CommandText
        if ($null -eq $bodyFilePath) {
            # --body-file flag present but no parseable value: treat as non-canonical.
            $bodyFilePath = ''
        }

        # Resolve provenance facts lazily through the adapter seams. Only read the receipt
        # and hash when the path is canonical and the receipt exists, so earlier failure
        # cases do not trigger unnecessary I/O.
        $receiptExists = $false
        $receipt = $null
        $bodyFileHash = $null
        $contextWriteTime = $null

        if ($bodyFilePath -match $script:PrBodyCanonicalPattern) {
            $receiptPath = $bodyFilePath -replace '\.md$', '.receipt.json'
            $receiptExists = Test-PrBodyReceiptPresence -Path $receiptPath
            if ($receiptExists) {
                $receipt = Get-PrBodyReceipt -Path $receiptPath
                $bodyFileHash = Get-PrBodyFileHash -Path $bodyFilePath
                $contextWriteTime = Get-PrContextWriteTime
            }
        }

        return Get-PrAuthorProvenanceReason `
            -BodyFilePath $bodyFilePath `
            -ReceiptExists $receiptExists `
            -Receipt $receipt `
            -BodyFileHash $bodyFileHash `
            -ContextWriteTime $contextWriteTime
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
        return [ordered]@{ decision = 'allow' }
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "enforce-pr-author-skill hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $commandText = $toolInput.command
    if (-not $commandText) {
        return [ordered]@{ decision = 'allow' }
    }

    $contextExists = Get-PrContextArtifactExistence
    $reason = Get-PrAuthorBypassReason -CommandText $commandText -ContextExists $contextExists

    if ($reason) {
        return [ordered]@{
            decision = 'block'
            reason   = $reason
        }
    }

    return [ordered]@{ decision = 'allow' }
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
}
catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress | Write-Output

exit 0
