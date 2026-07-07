#Requires -Version 7
<#
.SYNOPSIS
    Scans Bicep parameter files for secret-shaped literal values.

.DESCRIPTION
    Enumerates `.bicepparam` and `.json` files under a target directory and checks
    each file's text against a small set of secret-shaped regex patterns (an
    `AccountKey=`/`SharedAccessKey=` substring, a `;`-delimited connection-string
    shape, a contiguous base64-looking token of 32+ characters, or a
    `password`/`secret`/`key`-named identifier bound to a non-empty string
    literal). Used as the parameter-file guard for F16 (issue #125): no secret,
    connection string, or credential may be committed under
    `deploy/azure/parameters/`.

    A missing or empty target directory is treated as clean, not an error, since a
    fresh clone will not yet have prod parameter files beyond `main.dev.bicepparam`.

.PARAMETER Path
    Directory containing `.bicepparam`/`.json` parameter files to scan. Defaults to
    `deploy/azure/parameters`.

.EXAMPLE
    .\Test-OpenClawBicepParameterSecrets.ps1
    Scans `deploy/azure/parameters` and exits 0 (clean) or 1 (secret-shaped literal found).

.EXAMPLE
    Test-OpenClawBicepParameterSecrets -Path 'deploy/azure/parameters'
    Calls the underlying function directly (e.g. from a Pester test) and returns a
    result object instead of exiting the process.
#>
[CmdletBinding()]
param(
    [string]$Path = 'deploy/azure/parameters'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Secret-shaped patterns scanned against each parameter file's raw text.
$script:OpenClawBicepSecretPatterns = @(
    @{ Name = 'AccountKey/SharedAccessKey substring'; Pattern = '(?i)(AccountKey|SharedAccessKey)\s*=' }
    @{ Name = 'Connection-string shape (key=value; pairs)'; Pattern = '[A-Za-z][A-Za-z0-9_]*\s*=\s*[^;]+;\s*[A-Za-z][A-Za-z0-9_]*\s*=\s*[^;]+' }
    @{ Name = 'Contiguous base64-looking token (32+ chars)'; Pattern = '[A-Za-z0-9+/]{32,}={0,2}' }
    @{ Name = 'password/secret/key-named literal binding'; Pattern = "(?i)\b(password|secret|key)\b\s*[:=]\s*['`"][^'`"]{4,}['`"]" }
)

function Test-OpenClawBicepParameterSecrets {
    <#
    .SYNOPSIS
        Scans `.bicepparam`/`.json` files under -Path for secret-shaped literal values.

    .DESCRIPTION
        Enumerates `.bicepparam` and `.json` files under the target directory
        (recursively) and checks each file's raw text against the module-scoped
        secret-shaped regex pattern set. Returns a result object identifying the
        offending file(s) and matched pattern name(s) when any match is found, or a
        clean result (`IsClean = $true`) when none is found - including when the
        target directory is empty or does not exist.

    .PARAMETER Path
        Directory to scan. Defaults to `deploy/azure/parameters`. A missing or
        empty directory returns a clean result rather than throwing.

    .OUTPUTS
        [pscustomobject] with properties `IsClean` (bool), `ScannedPath` (string),
        `FileCount` (int), and `Findings` (array of `pscustomobject` with
        `FilePath` and `PatternName`).
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '',
        Justification = 'Function name is mandated by the F16 plan/spec (issue #125) and matches the script file name already referenced by _bicep-validate.yml; it scans for multiple kinds of secret-shaped literals in one call.')]
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [string]$Path = 'deploy/azure/parameters'
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{
            IsClean     = $true
            ScannedPath = $Path
            FileCount   = 0
            Findings    = @()
        }
    }

    $files = @(
        Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in @('.bicepparam', '.json') }
    )

    $findings = [System.Collections.Generic.List[pscustomobject]]::new()
    foreach ($file in $files) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrEmpty($content)) {
            continue
        }
        foreach ($patternInfo in $script:OpenClawBicepSecretPatterns) {
            if ($content -match $patternInfo.Pattern) {
                $findings.Add(
                    [pscustomobject]@{
                        FilePath    = $file.FullName
                        PatternName = $patternInfo.Name
                    }
                )
            }
        }
    }

    return [pscustomobject]@{
        IsClean     = $findings.Count -eq 0
        ScannedPath = $Path
        FileCount   = $files.Count
        Findings    = @($findings)
    }
}

# --- Main (only runs when executed directly, not when dot-sourced for testing) ---
if ($MyInvocation.InvocationName -ne '.') {
    $result = Test-OpenClawBicepParameterSecrets -Path $Path

    if ($result.IsClean) {
        Write-Output "Clean: scanned $($result.FileCount) parameter file(s) under '$($result.ScannedPath)'; no secret-shaped literal found."
        exit 0
    }

    foreach ($finding in $result.Findings) {
        Write-Error "Secret-shaped literal found in '$($finding.FilePath)' (pattern: $($finding.PatternName))." -ErrorAction Continue
    }
    exit 1
}
