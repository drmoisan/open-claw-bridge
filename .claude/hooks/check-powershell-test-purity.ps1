<#
.SYNOPSIS
    Pre-tool-use hook for Claude Code that blocks forbidden patterns in PowerShell unit tests.

.DESCRIPTION
    This script is invoked by the Claude Code PreToolUse hook before any Write or Edit
    operation on a file path matching a Pester test file (*.Tests.ps1 or tests/**/*.ps1).
    It reads the tool input from the CLAUDE_TOOL_INPUT environment variable (JSON with
    'file_path' and a content field: 'content' for Write, 'new_string' for Edit) and
    rejects the operation when the proposed content introduces forbidden runtime
    dependencies or violates the mocking rules.

    Forbidden patterns in Pester unit tests include:
      - direct external-executable mocking (Mock git, Mock gh, Mock actionlint, etc.)
        instead of mocking the wrapper function (Invoke-GitExe, Invoke-GhExe, etc.)
      - temporary filesystem usage (New-TemporaryFile, [System.IO.Path]::GetTempFileName,
        [System.IO.Path]::GetTempPath, $env:TEMP usage, $env:TMP usage)
      - network access (Invoke-WebRequest, Invoke-RestMethod, System.Net.Http.*,
        System.Net.WebRequest, System.Net.Sockets)
      - subprocess execution (Start-Process with raw executables)
      - time-based flakiness (Start-Sleep)

    If the content contains any forbidden pattern, the script writes a JSON response
    to stdout with 'decision': 'block' and exits with code 0 to let Claude Code surface
    the reason.

.NOTES
    Compatible with PowerShell 7+.
    This script must not modify any state; it is a read-only validation gate.
    It only inspects tool inputs targeting Pester test paths; all other paths pass through.
#>
[CmdletBinding()]
param()

$toolInputRaw = $env:CLAUDE_TOOL_INPUT
if (-not $toolInputRaw) {
    exit 0
}

try {
    $toolInput = $toolInputRaw | ConvertFrom-Json -ErrorAction Stop
}
catch {
    exit 0
}

$filePath = $toolInput.file_path
if (-not $filePath) {
    exit 0
}

$normalized = $filePath -replace '\\', '/'
$isPowerShellTestFile = ($normalized -match '(^|/)tests/.*\.ps1$') -or ($normalized -match '\.Tests\.ps1$')
if (-not $isPowerShellTestFile) {
    exit 0
}

$content = $null
if ($null -ne $toolInput.content) {
    $content = [string]$toolInput.content
}
elseif ($null -ne $toolInput.new_string) {
    $content = [string]$toolInput.new_string
}

if (-not $content) {
    exit 0
}

$forbiddenPatterns = @(
    @{ Pattern = '(?m)^\s*Mock\s+git\b'; Reason = 'direct Mock git forbidden in Pester tests; mock the Invoke-GitExe wrapper instead' },
    @{ Pattern = '(?m)^\s*Mock\s+gh\b'; Reason = 'direct Mock gh forbidden in Pester tests; mock the Invoke-GhExe wrapper instead' },
    @{ Pattern = '(?m)^\s*Mock\s+actionlint\b'; Reason = 'direct Mock actionlint forbidden in Pester tests; mock the Invoke-ActionlintExe wrapper instead' },
    @{ Pattern = "(?m)^\s*Mock\s+['""]git['""]"; Reason = 'direct Mock ''git'' forbidden in Pester tests; mock the Invoke-GitExe wrapper instead' },
    @{ Pattern = "(?m)^\s*Mock\s+['""]gh['""]"; Reason = 'direct Mock ''gh'' forbidden in Pester tests; mock the Invoke-GhExe wrapper instead' },
    @{ Pattern = '\bNew-TemporaryFile\b'; Reason = 'New-TemporaryFile forbidden in Pester unit tests' },
    @{ Pattern = '\[System\.IO\.Path\]::GetTempFileName'; Reason = 'temporary files forbidden in Pester unit tests' },
    @{ Pattern = '\[System\.IO\.Path\]::GetTempPath'; Reason = 'temp path usage forbidden in Pester unit tests' },
    @{ Pattern = '\$env:TEMP\b'; Reason = '$env:TEMP usage forbidden in Pester unit tests' },
    @{ Pattern = '\$env:TMP\b'; Reason = '$env:TMP usage forbidden in Pester unit tests' },
    @{ Pattern = '\bInvoke-WebRequest\b'; Reason = 'network access (Invoke-WebRequest) forbidden in Pester unit tests' },
    @{ Pattern = '\bInvoke-RestMethod\b'; Reason = 'network access (Invoke-RestMethod) forbidden in Pester unit tests' },
    @{ Pattern = '\[System\.Net\.Http\.'; Reason = 'System.Net.Http usage forbidden in Pester unit tests' },
    @{ Pattern = '\[System\.Net\.WebRequest\]'; Reason = 'System.Net.WebRequest usage forbidden in Pester unit tests' },
    @{ Pattern = '\[System\.Net\.Sockets\.'; Reason = 'raw socket access forbidden in Pester unit tests' },
    @{ Pattern = '\bStart-Process\b'; Reason = 'Start-Process forbidden in Pester unit tests; mock the wrapper seam instead' },
    @{ Pattern = '\bStart-Sleep\b'; Reason = 'Start-Sleep forbidden in Pester unit tests; avoid timing hacks' }
)

$violations = @()
foreach ($entry in $forbiddenPatterns) {
    if ($content -match $entry.Pattern) {
        $violations += $entry.Reason
    }
}

if ($violations.Count -eq 0) {
    exit 0
}

$uniqueViolations = $violations | Select-Object -Unique
$reason = "PowerShell unit test purity violations in '$filePath': " + ($uniqueViolations -join '; ') + ". Replace with wrapper-seam mocks, in-memory fakes, or pure code paths per .claude/rules/powershell.md."

$response = @{
    decision = 'block'
    reason   = $reason
} | ConvertTo-Json -Compress

Write-Output $response
exit 0

