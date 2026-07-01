<#
.SYNOPSIS
    SubagentStop hook that validates feature-review artifacts and coverage verdicts.

.DESCRIPTION
    Runs when the feature-review subagent terminates. Validates that the final
    output advertises the required review artifact paths, that those artifacts
    exist in the canonical active-feature location, and that the policy audit
    carries explicit PASS or FAIL coverage verdicts for each language with
    changed files in the branch diff.

    Required advertised output tokens:
      - policy-audit-path
      - code-review-path
      - feature-audit-path

    Optional advertised output token:
      - remediation-inputs-path

    Additional coverage rules:
      - Enumerate languages that have changed files in artifacts/pr_context.summary.txt:
          .ts / .tsx            -> TypeScript
          .py                   -> Python
          .ps1 / .psm1          -> PowerShell
          .cs                   -> CSharp
      - For each changed language, the policy audit must contain a
        coverage-scoped PASS or FAIL verdict.
      - Scope-narrowing phrases on coverage rows are treated as failures.
      - When repo-wide coverage is below 80 percent for an available artifact,
        the policy audit must carry a FAIL verdict for that language.

.NOTES
    Reads the hook payload from CLAUDE_HOOK_INPUT as JSON. Exits 0 to allow
    termination; exits 1 with an error message to block.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-ArtifactFileContent {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @{ Exists = $false; Text = $null; Lines = @() }
    }

    $text = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
    $lines = Get-Content -LiteralPath $Path -ErrorAction Stop
    if ($null -eq $lines) {
        $lines = @()
    }
    elseif ($lines -isnot [array]) {
        $lines = @($lines)
    }

    return @{ Exists = $true; Text = $text; Lines = $lines }
}

function Get-ReviewArtifactPathFromOutput {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AgentOutput,

        [Parameter(Mandatory = $true)]
        [string] $Token
    )

    $escapedToken = [regex]::Escape($Token)
    $pattern = "$escapedToken\s*[:=]\s*[""']?([^\s""'\)`]+)|\[$escapedToken\]\(([^)]+)\)"
    $match = [regex]::Match(
        $AgentOutput,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
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

function Get-ReviewArtifactInfo {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Stem
    )

    $normalized = $Path -replace '\\', '/'
    $pattern = "^docs/features/active/(?<Folder>.+)/$Stem\.(?<Timestamp>\d{4}-\d{2}-\d{2}T\d{2}-\d{2})\.md$"
    $match = [regex]::Match($normalized, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        return $null
    }

    return @{
        Folder    = $match.Groups['Folder'].Value
        Timestamp = $match.Groups['Timestamp'].Value
        Path      = $normalized
    }
}

function Get-ChangedLanguageSet {
    [OutputType([System.Collections.Hashtable])]
    param([string[]]$Lines)

    $langs = [ordered]@{}
    foreach ($line in $Lines) {
        if ($line -notmatch '^\s*-\s+(\S+)\s+\(\+\d+/-\d+\)\s*$') { continue }
        $path = $matches[1]
        $ext = [IO.Path]::GetExtension($path).ToLowerInvariant()
        switch -Regex ($ext) {
            '^\.(ts|tsx)$' { $langs['TypeScript'] = $true }
            '^\.py$' { $langs['Python'] = $true }
            '^\.(ps1|psm1)$' { $langs['PowerShell'] = $true }
            '^\.cs$' { $langs['CSharp'] = $true }
        }
    }
    return $langs
}

function Get-LcovRepoCoverage {
    [OutputType([Nullable[double]])]
    param([string]$Path)

    $file = Get-ArtifactFileContent -Path $Path
    if (-not $file.Exists) { return $null }

    $totalFound = 0
    $totalHit = 0
    foreach ($line in $file.Lines) {
        if ($line.StartsWith('LF:')) {
            $totalFound += [int]($line.Substring(3))
        }
        elseif ($line.StartsWith('LH:')) {
            $totalHit += [int]($line.Substring(3))
        }
    }
    if ($totalFound -le 0) { return $null }
    return [math]::Round(($totalHit * 100.0) / $totalFound, 2)
}

function Get-LcovBranchCoverage {
    # Parses LCOV branch counters (BRF: branches found, BRH: branches hit) and returns a percent.
    # LCOV emits one BRF/BRH line per source file in the report; we sum across the file to compute
    # repo-wide branch coverage. Returns $null when the artifact is absent or when no branch
    # information is recorded (BRF total = 0).
    [OutputType([Nullable[double]])]
    param([string]$Path)

    $file = Get-ArtifactFileContent -Path $Path
    if (-not $file.Exists) { return $null }

    $totalFound = 0
    $totalHit = 0
    foreach ($line in $file.Lines) {
        if ($line.StartsWith('BRF:')) {
            $totalFound += [int]($line.Substring(4))
        }
        elseif ($line.StartsWith('BRH:')) {
            $totalHit += [int]($line.Substring(4))
        }
    }
    if ($totalFound -le 0) { return $null }
    return [math]::Round(($totalHit * 100.0) / $totalFound, 2)
}

function Get-JacocoBranchCoverage {
    [OutputType([Nullable[double]])]
    param([string]$Path)

    $file = Get-ArtifactFileContent -Path $Path
    if (-not $file.Exists) { return $null }

    [xml]$doc = $file.Text
    $counters = $doc.SelectNodes('//counter[@type="BRANCH"]')
    if (-not $counters -or $counters.Count -eq 0) { return $null }

    $missed = 0
    $covered = 0
    foreach ($counter in $counters) {
        $missed += [int]$counter.missed
        $covered += [int]$counter.covered
    }
    $total = $missed + $covered
    if ($total -le 0) { return $null }
    return [math]::Round(($covered * 100.0) / $total, 2)
}

function Get-LanguageBranchCoverage {
    [OutputType([Nullable[double]])]
    param([string]$Language)

    switch ($Language) {
        'TypeScript' { return Get-LcovBranchCoverage -Path 'coverage/lcov.info' }
        'Python' { return Get-LcovBranchCoverage -Path 'artifacts/python/lcov.info' }
        'PowerShell' { return Get-JacocoBranchCoverage -Path 'artifacts/pester/powershell-coverage.xml' }
        'CSharp' { return Get-JacocoBranchCoverage -Path 'artifacts/csharp/coverage.xml' }
    }
    return $null
}

function Get-JacocoRepoCoverage {
    [OutputType([Nullable[double]])]
    param([string]$Path)

    $file = Get-ArtifactFileContent -Path $Path
    if (-not $file.Exists) { return $null }

    [xml]$doc = $file.Text
    $counters = $doc.SelectNodes('//counter[@type="LINE"]')
    if (-not $counters -or $counters.Count -eq 0) { return $null }

    $missed = 0
    $covered = 0
    foreach ($counter in $counters) {
        $missed += [int]$counter.missed
        $covered += [int]$counter.covered
    }
    $total = $missed + $covered
    if ($total -le 0) { return $null }
    return [math]::Round(($covered * 100.0) / $total, 2)
}

function Get-LanguageRepoCoverage {
    [OutputType([Nullable[double]])]
    param(
        [string]$Language
    )

    switch ($Language) {
        'TypeScript' { return Get-LcovRepoCoverage -Path 'coverage/lcov.info' }
        'Python' { return Get-LcovRepoCoverage -Path 'artifacts/python/lcov.info' }
        'PowerShell' { return Get-JacocoRepoCoverage -Path 'artifacts/pester/powershell-coverage.xml' }
        'CSharp' { return Get-JacocoRepoCoverage -Path 'artifacts/csharp/coverage.xml' }
    }
    return $null
}

function Test-LanguageCoverageRow {
    [OutputType([System.Collections.Hashtable])]
    param(
        [string]$AuditText,
        [string]$Language,
        [Nullable[double]]$RepoWidePct,
        [Nullable[double]]$BranchPct
    )

    $languageLabelMap = @{
        'TypeScript' = @('TypeScript', 'typescript')
        'Python'     = @('Python', 'python', 'pytest')
        'PowerShell' = @('PowerShell', 'powershell', 'pester')
        'CSharp'     = @('C#', 'CSharp', 'csharp', '.NET', 'dotnet')
    }

    $labels = $languageLabelMap[$Language]
    $labelPattern = '(?i)(' + (($labels | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')'

    $lines = $AuditText -split "`r?`n"
    $languageLines = $lines | Where-Object { $_ -match $labelPattern }

    if (-not $languageLines -or $languageLines.Count -eq 0) {
        return @{
            Ok     = $false
            Reason = "$Language has changed files on the branch but the policy-audit does not mention $Language."
        }
    }

    $coverageLines = $languageLines | Where-Object { $_ -match '(?i)(coverage|lcov|line[s]?\s+hit|pester)' }
    if (-not $coverageLines -or $coverageLines.Count -eq 0) {
        return @{
            Ok     = $false
            Reason = "$Language has changed files on the branch but no coverage-scoped row in the policy-audit mentions $Language."
        }
    }

    $narrowingPattern = '(?i)(informational only|context only|out of plan scope|out of scope|not applicable|\bN/A\b|\bUNVERIFIED\b)'
    $narrowing = $coverageLines | Where-Object { $_ -match $narrowingPattern }
    if ($narrowing -and $narrowing.Count -gt 0) {
        $first = ($narrowing | Select-Object -First 1).ToString().Trim()
        return @{
            Ok     = $false
            Reason = "$Language has changed files on the branch but a coverage-scoped row narrows scope: '$first'. Scope narrowing is not permitted for languages with changed files."
        }
    }

    $verdictLines = $coverageLines | Where-Object { $_ -match '\b(PASS|FAIL)\b' }
    if (-not $verdictLines -or $verdictLines.Count -eq 0) {
        return @{
            Ok     = $false
            Reason = "$Language coverage rows contain neither a PASS nor a FAIL verdict."
        }
    }

    if ($null -ne $RepoWidePct -and $RepoWidePct -lt 85.0) {
        $failLines = $coverageLines | Where-Object { $_ -match '\bFAIL\b' }
        if (-not $failLines -or $failLines.Count -eq 0) {
            return @{
                Ok     = $false
                Reason = ("{0} repo-wide coverage is {1}% (below the 85% line coverage floor) but the policy-audit contains no FAIL verdict on a coverage row for {0}." -f $Language, $RepoWidePct)
            }
        }
    }

    $BranchFloor = 75.0
    if ($null -ne $BranchPct -and $BranchPct -lt $BranchFloor) {
        return @{
            Ok     = $false
            Reason = ("{0} branch coverage is {1}% (below the 75% branch coverage floor); policy-audit must record FAIL on the corresponding coverage row." -f $Language, $BranchPct)
        }
    }

    return @{ Ok = $true; Reason = $null }
}

function Invoke-FeatureReviewCoverageValidation {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string] $RawPayload
    )

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return @{ Ok = $false; Message = 'feature-review hook: CLAUDE_HOOK_INPUT is empty; cannot validate review output.' }
    }

    try {
        $payload = $RawPayload | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return @{ Ok = $false; Message = "feature-review hook: failed to parse CLAUDE_HOOK_INPUT as JSON: $($_.Exception.Message)" }
    }

    $agentOutput = $null
    if ($payload.PSObject.Properties.Name -contains 'output') {
        $agentOutput = $payload.output
    }
    if ([string]::IsNullOrWhiteSpace($agentOutput)) {
        return @{ Ok = $false; Message = 'feature-review hook: agent output is empty; feature-review must report required artifact paths before termination.' }
    }

    $requiredArtifacts = @(
        @{ Token = 'policy-audit-path'; Stem = 'policy-audit'; Description = 'policy audit artifact' },
        @{ Token = 'code-review-path'; Stem = 'code-review'; Description = 'code review artifact' },
        @{ Token = 'feature-audit-path'; Stem = 'feature-audit'; Description = 'feature audit artifact' }
    )

    $artifactMeta = @{}
    $errors = [System.Collections.Generic.List[string]]::new()

    foreach ($artifact in $requiredArtifacts) {
        $path = Get-ReviewArtifactPathFromOutput -AgentOutput $agentOutput -Token $artifact.Token
        if ($null -eq $path) {
            $errors.Add("missing $($artifact.Token): <path> for the required $($artifact.Description)")
            continue
        }

        $metadata = Get-ReviewArtifactInfo -Path $path -Stem $artifact.Stem
        if ($null -eq $metadata) {
            $errors.Add("$($artifact.Token) '$path' is outside the required docs/features/active/.../$($artifact.Stem).<timestamp>.md location")
            continue
        }

        $file = Get-ArtifactFileContent -Path $path
        if (-not $file.Exists) {
            $errors.Add("$($artifact.Token) '$path' was advertised for the $($artifact.Description) but no file exists at that location")
            continue
        }

        $metadata['Text'] = $file.Text
        $artifactMeta[$artifact.Stem] = $metadata
    }

    if ($artifactMeta.ContainsKey('policy-audit')) {
        $reference = $artifactMeta['policy-audit']
        foreach ($stem in @('code-review', 'feature-audit')) {
            if (-not $artifactMeta.ContainsKey($stem)) {
                continue
            }
            $candidate = $artifactMeta[$stem]
            if ($candidate.Folder -ne $reference.Folder -or $candidate.Timestamp -ne $reference.Timestamp) {
                $errors.Add("$stem artifact must share the same feature folder and timestamp as the policy audit artifact.")
            }
        }

        $remediationPath = Get-ReviewArtifactPathFromOutput -AgentOutput $agentOutput -Token 'remediation-inputs-path'
        if ($null -ne $remediationPath) {
            $remediation = Get-ReviewArtifactInfo -Path $remediationPath -Stem 'remediation-inputs'
            if ($null -eq $remediation) {
                $errors.Add("remediation-inputs-path '$remediationPath' is outside the required docs/features/active/.../remediation-inputs.<timestamp>.md location")
            }
            elseif ($remediation.Folder -ne $reference.Folder -or $remediation.Timestamp -ne $reference.Timestamp) {
                $errors.Add('remediation-inputs artifact must share the same feature folder and timestamp as the policy audit artifact.')
            }
            elseif (-not (Get-ArtifactFileContent -Path $remediationPath).Exists) {
                $errors.Add("remediation-inputs-path '$remediationPath' was advertised but no file exists at that location")
            }
        }
    }

    if ($errors.Count -gt 0) {
        $message = "feature-review hook: required review artifact validation failed:`n  - " + ($errors -join "`n  - ")
        return @{ Ok = $false; Message = $message }
    }

    $prSummary = Get-ArtifactFileContent -Path 'artifacts/pr_context.summary.txt'
    $changedLanguages = if ($prSummary.Exists) { Get-ChangedLanguageSet -Lines $prSummary.Lines } else { [ordered]@{} }
    if ($changedLanguages.Count -eq 0) {
        return @{ Ok = $true; Message = $null }
    }

    $policyAuditText = $artifactMeta['policy-audit'].Text
    $coverageFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($lang in $changedLanguages.Keys) {
        $repoPct = Get-LanguageRepoCoverage -Language $lang
        $branchPct = Get-LanguageBranchCoverage -Language $lang
        $result = Test-LanguageCoverageRow -AuditText $policyAuditText -Language $lang -RepoWidePct $repoPct -BranchPct $branchPct
        if (-not $result.Ok) {
            $coverageFailures.Add($result.Reason)
        }
    }

    if ($coverageFailures.Count -gt 0) {
        $message = "feature-review hook: coverage validation failed against branch diff:`n  - " + ($coverageFailures -join "`n  - ")
        return @{ Ok = $false; Message = $message }
    }

    return @{ Ok = $true; Message = $null }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$result = Invoke-FeatureReviewCoverageValidation -RawPayload $env:CLAUDE_HOOK_INPUT
if (-not $result.Ok) {
    Write-Error $result.Message
    exit 1
}

exit 0
