# Publish.Env.psm1
# Pure .env helpers plus a thin file-I/O seam for scripts/Publish.ps1 and
# scripts/New-MsixDevCert.ps1.
#
# Purpose
#   The publish version (OPENCLAW_PACKAGE_VERSION) and the code-signing
#   certificate thumbprint (OPENCLAW_CERT_THUMBPRINT) are driven from the
#   repository-root .env file. This module holds the small, pure helpers that
#   parse and rewrite .env content, plus a 4-part version-increment helper, and
#   a narrow read/write seam so the pure helpers never touch disk and the unit
#   tests can drive them with in-memory string[] content.
#
# Policy notes
#   - PowerShell 7+ compatible.
#   - Get-EnvFileMap, Set-EnvFileValue, and Step-PackageVersion are PURE: they
#     accept and return values only and perform no I/O, so tests drive them with
#     in-memory string[] content (no temp files; repo no-temp-files rule).
#   - Read-EnvFileContent / Write-EnvFileContent are the only file-I/O seam.
#     Write-EnvFileContent uses SupportsShouldProcess for -WhatIf testability.
#   - This module stays under 500 lines per repo policy.
#   - State is passed in explicitly; no script-scoped mutable state is used.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EnvFileMap {
    <#
    .SYNOPSIS
        Parses .env line content into an ordered key/value map.
    .DESCRIPTION
        Pure helper. Does not perform I/O. Accepts the .env file as a string
        array and returns an ordered hashtable of KEY -> VALUE. Blank lines and
        comment lines (first non-whitespace character is '#') are ignored. Only
        the first occurrence of a duplicate key is kept (first-wins), matching
        how a typical dotenv loader resolves duplicates. A line without an '='
        is ignored.
    .PARAMETER Content
        The .env file contents as a string array (one element per line).
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$Content
    )

    $map = [ordered]@{}
    foreach ($line in $Content) {
        if ($null -eq $line) { continue }
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0) { continue }
        if ($trimmed.StartsWith('#')) { continue }

        $eq = $line.IndexOf('=')
        if ($eq -lt 1) { continue }

        $key = $line.Substring(0, $eq).Trim()
        if ($key.Length -eq 0) { continue }
        $value = $line.Substring($eq + 1)

        if (-not $map.Contains($key)) {
            $map[$key] = $value
        }
    }
    return $map
}

function Set-EnvFileValue {
    <#
    .SYNOPSIS
        Returns new .env content with a key set to a value (update or append).
    .DESCRIPTION
        Pure helper. Does not perform I/O. Given .env content as a string array,
        a key, and a value, returns a NEW string array where:
          - if the key is already present (as KEY=... on a non-comment line) its
            value is updated in place, preserving the key's position, surrounding
            comments, and all unrelated keys and lines;
          - if the key is absent, a new 'KEY=VALUE' line is appended at the end.
        Idempotent: re-applying the same key/value yields content with the same
        single KEY=VALUE line (no duplicate key, no disturbed unrelated lines).
        Only the first matching key occurrence is updated; any later duplicate
        lines are left untouched (Get-EnvFileMap is first-wins, so the first
        line is the authoritative one).
    .PARAMETER Content
        The current .env file contents as a string array.
    .PARAMETER Key
        The environment key to set (for example 'OPENCLAW_PACKAGE_VERSION').
    .PARAMETER Value
        The value to assign to the key.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Pure function: returns a new string[] with the key set; performs no I/O and changes no system state. The Set- verb describes the value transform, not a system mutation.'
    )]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseOutputTypeCorrectly', '',
        Justification = 'The function returns [string[]] (declared in OutputType). The unary-comma return operator (used to preserve single-element and empty arrays through the pipeline) is reported by static analysis as Object[]; the runtime type is the cast [string[]].'
    )]
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$Content,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Key,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    $newLine = "$Key=$Value"
    $result = [System.Collections.Generic.List[string]]::new()
    $updated = $false

    foreach ($line in $Content) {
        if (-not $updated -and $null -ne $line) {
            $trimmed = $line.Trim()
            if ($trimmed.Length -gt 0 -and -not $trimmed.StartsWith('#')) {
                $eq = $line.IndexOf('=')
                if ($eq -ge 1) {
                    $lineKey = $line.Substring(0, $eq).Trim()
                    if ($lineKey -eq $Key) {
                        $result.Add($newLine)
                        $updated = $true
                        continue
                    }
                }
            }
        }
        $result.Add($line)
    }

    if (-not $updated) {
        $result.Add($newLine)
    }

    return , ([string[]]$result.ToArray())
}

function Step-PackageVersion {
    <#
    .SYNOPSIS
        Returns a 4-part version with the 4th (revision) segment incremented.
    .DESCRIPTION
        Pure helper. Validates the input strictly against
        '^\d+\.\d+\.\d+\.\d+$' and returns a new 4-part version string whose
        revision (4th) segment is incremented by one. Throws a terminating error
        with remediation text on malformed input (matching the publish cadence
        1.0.2.0 -> 1.0.2.1).
    .PARAMETER Version
        The current 4-part version string to increment.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Version
    )

    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "OPENCLAW_PACKAGE_VERSION value '$Version' is not a valid 4-part version (expected '^\d+\.\d+\.\d+\.\d+$', for example '1.0.2.0'). Correct the value before publishing."
    }

    $parts = $Version.Split('.')
    $revision = [int]$parts[3] + 1
    return "$($parts[0]).$($parts[1]).$($parts[2]).$revision"
}

function Read-EnvFileContent {
    <#
    .SYNOPSIS
        File-I/O seam: reads a .env file into a string array of lines.
    .DESCRIPTION
        The only read side of the file seam. Returns the file content as a
        string array (one element per line), or an empty array when the file
        does not exist. The pure helpers (Get-EnvFileMap, Set-EnvFileValue)
        operate on the array this returns, so they never touch disk and tests
        mock this function instead of writing temp files.
    .PARAMETER Path
        Absolute path to the .env file.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseOutputTypeCorrectly', '',
        Justification = 'The function returns [string[]] (declared in OutputType). The unary-comma return operator (used to preserve single-element and empty arrays through the pipeline) is reported by static analysis as Object[]; the runtime type is the cast [string[]].'
    )]
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return , ([string[]]@())
    }
    $lines = Get-Content -LiteralPath $Path
    if ($null -eq $lines) { return , ([string[]]@()) }
    return , ([string[]]@($lines))
}

function Write-EnvFileContent {
    <#
    .SYNOPSIS
        File-I/O seam: writes a string array of lines back to a .env file.
    .DESCRIPTION
        The only write side of the file seam. Writes the supplied string array
        as the full contents of the .env file when $PSCmdlet.ShouldProcess is
        satisfied, so callers can drive it under -WhatIf in tests.
    .PARAMETER Path
        Absolute path to the .env file to write.
    .PARAMETER Content
        The full .env contents to write, as a string array of lines.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$Content
    )

    if ($PSCmdlet.ShouldProcess($Path, 'Write .env contents')) {
        Set-Content -LiteralPath $Path -Value $Content -Encoding utf8
    }
}

Export-ModuleMember -Function @(
    'Get-EnvFileMap'
    'Set-EnvFileValue'
    'Step-PackageVersion'
    'Read-EnvFileContent'
    'Write-EnvFileContent'
)
