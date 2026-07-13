<#
.SYNOPSIS
    SessionStart hook that persists the current Claude Code session id.

.DESCRIPTION
    Invoked by the Claude Code SessionStart hook event (registered in
    .claude/settings.json). Reads the hook payload JSON from standard input,
    falling back to the CLAUDE_HOOK_INPUT environment variable (the existing
    SubagentStop-hook precedent), and extracts the 'session_id' field.

    Persistence channel:
      - When CLAUDE_ENV_FILE is set, appends the line
        'CLAUDE_SESSION_ID=<id>' to that file. Variables persisted there are
        exported to subsequent Bash tool commands in the session, which is how
        this hook provisions the otherwise-unset CLAUDE_SESSION_ID variable.
      - When CLAUDE_ENV_FILE is unset, writes the id to
        .claude/state/current-session-id instead.

    On malformed or empty input (missing/blank payload, unparseable JSON, or an
    absent/blank session_id) the hook performs no write. It always exits 0 so a
    SessionStart hook never blocks session start.

.NOTES
    Compatible with PowerShell 7+. Does not use Invoke-Expression.
#>
[CmdletBinding()]
param()

function Get-PersistSessionIdDecision {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $RawPayload,

        [string] $EnvFilePath,

        [Parameter(Mandatory)]
        [string] $StateFilePath
    )

    $none = [ordered]@{ action = 'none'; sessionId = ''; path = '' }

    if ([string]::IsNullOrWhiteSpace($RawPayload)) {
        return $none
    }

    try {
        $payload = $RawPayload | ConvertFrom-Json -ErrorAction Stop
    } catch {
        Write-Verbose "persist-session-id: ignoring unparseable payload: $($_.Exception.Message)"
        return $none
    }

    $sessionId = $null
    if ($null -ne $payload -and $payload.PSObject.Properties.Name -contains 'session_id') {
        $sessionId = [string]$payload.session_id
    }

    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        return $none
    }

    if (-not [string]::IsNullOrWhiteSpace($EnvFilePath)) {
        return [ordered]@{ action = 'env-file'; sessionId = $sessionId; path = $EnvFilePath }
    }

    return [ordered]@{ action = 'state-file'; sessionId = $sessionId; path = $StateFilePath }
}

function Invoke-PersistSessionIdHook {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [string] $RawPayload,

        [string] $EnvFilePath,

        [Parameter(Mandatory)]
        [string] $StateFilePath,

        [scriptblock] $AppendLine = {
            param([string] $Path, [string] $Line)
            Add-Content -Path $Path -Value $Line -Encoding utf8
        },

        [scriptblock] $WriteStateFile = {
            param([string] $Path, [string] $Content)
            Set-Content -Path $Path -Value $Content -Encoding utf8 -NoNewline
        },

        [scriptblock] $EnsureDirectory = {
            param([string] $Path)
            if (-not (Test-Path -Path $Path)) {
                New-Item -ItemType Directory -Path $Path -Force | Out-Null
            }
        }
    )

    $decision = Get-PersistSessionIdDecision -RawPayload $RawPayload -EnvFilePath $EnvFilePath -StateFilePath $StateFilePath

    switch ($decision.action) {
        'env-file' {
            & $AppendLine $decision.path ("CLAUDE_SESSION_ID={0}" -f $decision.sessionId)
        }
        'state-file' {
            $stateDir = Split-Path -Path $decision.path -Parent
            if ($stateDir) {
                & $EnsureDirectory $stateDir
            }
            & $WriteStateFile $decision.path $decision.sessionId
        }
        default {
            # 'none': malformed or empty input; perform no write.
        }
    }

    return $decision
}

function Read-HookPayload {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [scriptblock] $ReadStandardInput = { [Console]::In.ReadToEnd() },

        [AllowNull()]
        [AllowEmptyString()]
        [string] $FallbackPayload = $env:CLAUDE_HOOK_INPUT
    )

    $raw = ''
    try {
        $raw = & $ReadStandardInput
    } catch {
        $raw = ''
    }

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $FallbackPayload
    }

    return $raw
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$rawPayload = Read-HookPayload
$stateFilePath = Join-Path -Path (Get-Location).Path -ChildPath '.claude/state/current-session-id'
Invoke-PersistSessionIdHook -RawPayload $rawPayload -EnvFilePath $env:CLAUDE_ENV_FILE -StateFilePath $stateFilePath | Out-Null

exit 0
