<#
.SYNOPSIS
    SubagentStop hook for the atomic-executor subagent.

.DESCRIPTION
    Blocks termination of the atomic-executor subagent unless the output and
    plan state satisfy the executor's mechanical contract.

    Enforced checks:
      - hook payload is valid JSON and contains non-empty output,
      - output advertises `plan-path: <path>` (or supported equivalent forms),
      - the advertised plan exists on disk,
      - preflight-only runs return an exact preflight signal and may not claim
        non-canonical blocked text,
      - non-preflight runs may not stop in a blocked state,
      - non-preflight plans contain checkboxes and all are checked,
      - non-preflight output includes `AC Status Summary`,
      - non-preflight output includes command/status reporting,
      - for each language referenced by explicit file paths in the plan, the
        output includes a PASS or FAIL status line for that language.

.NOTES
    Reads the hook payload from CLAUDE_HOOK_INPUT as JSON. Exits 0 to allow
    termination; exits 1 with an error message to block. Plan file contents are
    read through Get-PlanFileContent so tests can mock the filesystem boundary
    without writing temporary files.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-PlanFileContent {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @{ Exists = $false; Lines = @() }
    }

    $lines = Get-Content -LiteralPath $Path -ErrorAction Stop
    if ($null -eq $lines) {
        $lines = @()
    }
    elseif ($lines -isnot [array]) {
        $lines = @($lines)
    }

    return @{ Exists = $true; Lines = $lines }
}

function Get-PlanPathFromOutput {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput
    )

    $pattern = 'plan-path\s*[:=]\s*["'']?([^\s"''\)`]+)|\[plan-path\]\(([^)]+)\)'
    $match = [regex]::Match($AgentOutput, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        return $null
    }

    $value = if ($match.Groups[1].Success) { $match.Groups[1].Value } else { $match.Groups[2].Value }
    $value = $value.Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value
}

function Test-IsPreflightPlan {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Lines
    )

    return @($Lines | Where-Object { $_ -match '(?i)^\s*DIRECTIVE:\s*PREFLIGHT VALIDATION ONLY\s*$' }).Count -gt 0
}

function Get-TouchedLanguagesFromPlan {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Lines
    )

    $languages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $pathPattern = '(?i)(?:[.\w*?-]+[\\/])+[.\w*?-]+'

    foreach ($line in $Lines) {
        foreach ($match in [regex]::Matches($line, $pathPattern)) {
            $extension = [System.IO.Path]::GetExtension($match.Value).ToLowerInvariant()
            switch -Regex ($extension) {
                '^\.(ts|tsx)$' { $null = $languages.Add('TypeScript') }
                '^\.py$' { $null = $languages.Add('Python') }
                '^\.(ps1|psm1|psd1)$' { $null = $languages.Add('PowerShell') }
                '^\.cs$' { $null = $languages.Add('CSharp') }
            }
        }
    }

    return [string[]]@($languages)
}

function Test-OutputHasLanguageStatus {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput,

        [Parameter(Mandatory = $true)]
        [string] $Language
    )

    $labelMap = @{
        'TypeScript' = @('TypeScript', 'typescript')
        'Python'     = @('Python', 'python')
        'PowerShell' = @('PowerShell', 'powershell')
        'CSharp'     = @('C#', 'CSharp', 'csharp', '\.NET', 'dotnet')
    }

    $labels = $labelMap[$Language]
    $labelPattern = '(?i)(' + (($labels | ForEach-Object { $_ }) -join '|') + ')'
    return [regex]::IsMatch($AgentOutput, "(?im)^.*$labelPattern.*\b(PASS|FAIL)\b.*$")
}

function Test-HasPreflightSignal {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput
    )

    return [regex]::IsMatch(
        $AgentOutput,
        '(?m)^\s*PREFLIGHT:\s*(ALL CLEAR|REVISIONS REQUIRED)\s*$'
    )
}

function Test-HasCanonicalBlockedText {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput
    )

    if ($AgentOutput -notmatch 'BLOCKED') {
        return $true
    }

    if ($AgentOutput -notmatch 'BLOCKED at preflight \(before \[P0-T1\]\)') {
        return $false
    }

    if ($AgentOutput -notmatch '(?i)plan delta' -and $AgentOutput -notmatch '\[P\d+-T\d+\]') {
        return $false
    }

    return $true
}

function Invoke-ExecutorOutputValidation {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string] $RawPayload
    )

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return @{ Ok = $false; Message = 'atomic-executor hook: CLAUDE_HOOK_INPUT is empty; cannot validate executor output.' }
    }

    try {
        $payload = $RawPayload | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return @{ Ok = $false; Message = "atomic-executor hook: failed to parse CLAUDE_HOOK_INPUT as JSON: $($_.Exception.Message)" }
    }

    $agentOutput = $null
    if ($payload.PSObject.Properties.Name -contains 'output') {
        $agentOutput = $payload.output
    }
    if ([string]::IsNullOrWhiteSpace($agentOutput)) {
        return @{ Ok = $false; Message = 'atomic-executor hook: agent output is empty; executor must return plan-path and final completion summary.' }
    }

    $planPath = Get-PlanPathFromOutput -AgentOutput $agentOutput
    if ($null -eq $planPath) {
        return @{ Ok = $false; Message = 'atomic-executor hook: agent output does not advertise a plan-path. Executor must report `plan-path: <path>` per atomic-plan-contract.' }
    }

    $file = Get-PlanFileContent -Path $planPath
    if (-not $file.Exists) {
        return @{ Ok = $false; Message = "atomic-executor hook: executor advertised plan-path '$planPath' but no file exists at that location." }
    }

    $isPreflight = Test-IsPreflightPlan -Lines $file.Lines
    if ($isPreflight) {
        if (-not (Test-HasCanonicalBlockedText -AgentOutput $agentOutput)) {
            return @{ Ok = $false; Message = 'atomic-executor hook: blocked preflight output must include the exact text `BLOCKED at preflight (before [P0-T1])` plus a concrete plan delta.' }
        }
        if ($agentOutput -notmatch 'BLOCKED' -and -not (Test-HasPreflightSignal -AgentOutput $agentOutput)) {
            return @{ Ok = $false; Message = 'atomic-executor hook: preflight output must include `PREFLIGHT: ALL CLEAR` or `PREFLIGHT: REVISIONS REQUIRED`.' }
        }
        return @{ Ok = $true; Message = $null }
    }

    if ($agentOutput -match 'BLOCKED') {
        return @{ Ok = $false; Message = 'atomic-executor hook: blocking is permitted only during preflight validation (before [P0-T1]).' }
    }

    $checkboxLineCount = 0
    $uncheckedLineNumbers = [System.Collections.Generic.List[int]]::new()
    $uncheckedPattern = '^\s*-\s\[\s\]\s'
    $checkedPattern = '^\s*-\s\[[xX]\]\s'

    for ($i = 0; $i -lt $file.Lines.Count; $i++) {
        $line = $file.Lines[$i]
        if ($line -match $uncheckedPattern) {
            $checkboxLineCount++
            $uncheckedLineNumbers.Add($i + 1)
        }
        elseif ($line -match $checkedPattern) {
            $checkboxLineCount++
        }
    }

    if ($checkboxLineCount -eq 0) {
        return @{ Ok = $false; Message = "atomic-executor hook: plan file '$planPath' contains no task checkboxes; executor cannot complete against an empty plan." }
    }

    if ($uncheckedLineNumbers.Count -gt 0) {
        $reported = $uncheckedLineNumbers
        $suffix = ''
        if ($uncheckedLineNumbers.Count -gt 5) {
            $reported = $uncheckedLineNumbers.GetRange(0, 5)
            $suffix = ' (showing first 5)'
        }
        $lineList = ($reported -join ', ')
        $message = "atomic-executor hook: plan file '$planPath' has $($uncheckedLineNumbers.Count) unchecked task(s) at line(s) ${lineList}${suffix}. All `- [ ]` items must be ticked to `- [x]` before termination."
        return @{ Ok = $false; Message = $message }
    }

    if ($agentOutput -notmatch '(?i)\bAC Status Summary\b') {
        return @{ Ok = $false; Message = 'atomic-executor hook: completion output must include an `AC Status Summary` section.' }
    }

    $hasCommandEvidence = $agentOutput -match '(?i)(Commands Run|Command[s]?:|poetry run |npx |pwsh |git |mcp__drm-copilot__)'
    $hasStatusEvidence = $agentOutput -match '(?i)\b(PASS|FAIL)\b'
    if (-not $hasCommandEvidence -or -not $hasStatusEvidence) {
        return @{ Ok = $false; Message = 'atomic-executor hook: completion output must report commands run and pass/fail status results.' }
    }

    $missingLanguageStatuses = [System.Collections.Generic.List[string]]::new()
    foreach ($language in Get-TouchedLanguagesFromPlan -Lines $file.Lines) {
        if (-not (Test-OutputHasLanguageStatus -AgentOutput $agentOutput -Language $language)) {
            $missingLanguageStatuses.Add($language)
        }
    }

    if ($missingLanguageStatuses.Count -gt 0) {
        $missingList = $missingLanguageStatuses -join ', '
        return @{ Ok = $false; Message = "atomic-executor hook: completion output is missing explicit PASS/FAIL toolchain status lines for: $missingList." }
    }

    return @{ Ok = $true; Message = $null }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$result = Invoke-ExecutorOutputValidation -RawPayload $env:CLAUDE_HOOK_INPUT
if (-not $result.Ok) {
    Write-Error $result.Message
    exit 1
}

exit 0
