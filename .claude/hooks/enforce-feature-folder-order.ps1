<#
.SYNOPSIS
    Pre-tool-use hook that blocks writes to a feature folder's plan.md when issue.md,
    spec.md, or user-story.md are not yet present in that same folder.

.DESCRIPTION
    Invoked by the Claude Code PreToolUse hook on Write or Edit operations. Reads
    tool input JSON from the CLAUDE_TOOL_INPUT environment variable. When the
    target file_path matches a feature-folder plan.md path under
    docs/features/(active|archive)/<folder>/plan.md, the script verifies that
    each of issue.md, spec.md, and user-story.md exists in the same folder.

    If any of the three sibling files is missing, the script emits a PreToolUse
    JSON response with hookSpecificOutput.permissionDecision='deny' and exits 0 so
    Claude Code surfaces the reason. All other paths pass through with
    permissionDecision='allow'.

    Filesystem reads go through Get-FeatureFolderFileExistence so tests can
    inject a fake without touching disk.

.NOTES
    Compatible with PowerShell 7+. Read-only validation gate; no state mutation.
#>
[CmdletBinding()]
param()

function Get-FeatureFolderFileExistence {
    <#
    .SYNOPSIS
        Wrapper around Test-Path for sibling-file existence checks. Tests mock this.
    .PARAMETER Path
        Absolute or workspace-relative file path to check.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    return [bool](Test-Path -LiteralPath $Path -PathType Leaf)
}

function Get-FeatureFolderMissingFile {
    <#
    .SYNOPSIS
        Returns the list of required sibling files missing alongside plan.md.
    .PARAMETER PlanFilePath
        Normalized (forward-slash) path to the plan.md target.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [string] $PlanFilePath
    )

    $folder = $PlanFilePath -replace '/plan\.md$', ''
    $required = @('issue.md', 'spec.md', 'user-story.md')
    [System.Collections.Generic.List[string]] $missing = [System.Collections.Generic.List[string]]::new()

    foreach ($name in $required) {
        $siblingPath = "$folder/$name"
        if (-not (Get-FeatureFolderFileExistence -Path $siblingPath)) {
            $missing.Add($name)
        }
    }

    return [string[]] $missing.ToArray()
}

function Test-IsFeaturePlanPath {
    <#
    .SYNOPSIS
        Returns $true if the normalized path targets a feature-folder plan.md.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $NormalizedPath
    )

    return $NormalizedPath -match '(^|/)docs/features/(active|archive)/[^/]+/plan\.md$'
}

function Invoke-FeatureFolderOrderDecision {
    <#
    .SYNOPSIS
        Parses CLAUDE_TOOL_INPUT and produces an allow-or-block decision.
    .PARAMETER ToolInputRaw
        Raw JSON string. Empty/null returns allow.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $ToolInputRaw
    )

    if (-not $ToolInputRaw) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    try {
        $toolInput = $ToolInputRaw | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "enforce-feature-folder-order hook received malformed JSON in CLAUDE_TOOL_INPUT: $_"
    }

    $filePath = $toolInput.file_path
    if (-not $filePath) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    $normalized = $filePath -replace '\\', '/'

    if (-not (Test-IsFeaturePlanPath -NormalizedPath $normalized)) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    $missing = Get-FeatureFolderMissingFile -PlanFilePath $normalized
    if ($missing.Count -eq 0) {
        return [ordered]@{ hookSpecificOutput = [ordered]@{ hookEventName = 'PreToolUse'; permissionDecision = 'allow' } }
    }

    $list = ($missing -join ', ')
    return [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = "FEATURE_FOLDER_ORDER_BLOCKED: cannot write plan.md before producing prerequisite documents. Missing in feature folder: $list. Invoke the prd-feature subagent to generate the missing file(s) before authoring plan.md."
        }
    }
}

# Guard allows dot-sourcing in tests without executing the entrypoint.
if ($MyInvocation.InvocationName -eq '.') {
    return
}

try {
    $decision = Invoke-FeatureFolderOrderDecision -ToolInputRaw $env:CLAUDE_TOOL_INPUT
}
catch {
    Write-Error $_
    exit 1
}

$decision | ConvertTo-Json -Compress -Depth 5 | Write-Output

exit 0
