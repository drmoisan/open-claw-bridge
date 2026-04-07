[CmdletBinding()]
param(
    [string]$TaskName = 'OpenClaw MailBridge',
    [string]$ClientPath = 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe',
    [int]$ReadyTimeoutSeconds = 120,
    [string]$OperatorEvidenceOutputPath = 'TestResults\mailbridge-operator-evidence.txt',
    [switch]$ExpectMessageData,
    [switch]$ExpectCalendarData,
    [bool]$OpenClawSvcPipeConnect = $false,
    [bool]$NetworkDenyVerified = $false
)

$ErrorActionPreference = 'Stop'

function Invoke-Json {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutablePath,
        [Parameter(Mandatory = $true)]
        [string[]]$CommandArguments
    )

    $output = & $ClientExecutablePath @CommandArguments
    if (-not $output) {
        throw "No JSON output returned for arguments: $($CommandArguments -join ' ')"
    }

    return $output | ConvertFrom-Json
}

function Get-BridgeFieldValue {
    [CmdletBinding()]
    [OutputType([object])]
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($property) {
            return $property.Value
        }
    }

    return $null
}

function Wait-BridgeReady {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScheduledTaskName,
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutablePath,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    schtasks /run /tn $ScheduledTaskName | Out-Null
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $status = Invoke-Json -ClientExecutablePath $ClientExecutablePath -CommandArguments @('status')
        if ($status.result.state -eq 'ready') {
            return $status
        }
    } while ((Get-Date) -lt $deadline)

    throw 'Bridge readiness deadline expired before status.result.state reached ready.'
}

function Assert-SafeModePrivacy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Items
    )

    foreach ($item in $Items) {
        if (Get-BridgeFieldValue -Object $item -Names @('body_preview', 'bodyPreview')) {
            throw 'Safe mode leaked body_preview.'
        }

        if (Get-BridgeFieldValue -Object $item -Names @('sender_name', 'senderName')) {
            throw 'Safe mode leaked sender_name.'
        }

        if (Get-BridgeFieldValue -Object $item -Names @('sender_email', 'senderEmail')) {
            throw 'Safe mode leaked sender_email.'
        }
    }
}

function Write-OperatorEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [bool]$PrimaryInteractiveSession,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [bool]$SvcPipeConnect,
        [Parameter(Mandatory = $true)]
        [bool]$NetworkDenyState
    )

    $parent = Split-Path -Parent $OutputPath
    if ($parent) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    @(
        "PrimaryInteractiveSession: $PrimaryInteractiveSession"
        "OpenClawSvcPipeConnect: $SvcPipeConnect"
        "NetworkDenyVerified: $NetworkDenyState"
    ) | Set-Content -Path $OutputPath -Encoding utf8
}

$status = Wait-BridgeReady -ScheduledTaskName $TaskName -ClientExecutablePath $ClientPath -TimeoutSeconds $ReadyTimeoutSeconds
if (-not $status.result.mode) {
    throw 'Mode missing from status response.'
}

$since = (Get-Date).ToUniversalTime().AddHours(-24).ToString('o')
$messages = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('list-messages', '--since', $since, '--limit', '20')
if (-not $messages.ok) {
    throw 'list-messages failed.'
}

$messageItems = @($messages.result.items)
if ($ExpectMessageData -and $messageItems.Count -eq 0) {
    throw 'Message cache data was expected, but list-messages returned zero items.'
}

if ($messageItems.Count -gt 0) {
    $firstMessageId = Get-BridgeFieldValue -Object $messageItems[0] -Names @('bridgeId', 'bridge_id')
    $singleMessage = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('get-message', '--id', [string]$firstMessageId)
    if (-not $singleMessage.ok) {
        throw 'get-message failed for a cached message identifier.'
    }
}

$meetingRequests = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('list-meeting-requests', '--since', $since, '--limit', '20')
if (-not $meetingRequests.ok) {
    throw 'list-meeting-requests failed.'
}

$start = (Get-Date).ToUniversalTime().AddDays(-1).ToString('o')
$end = (Get-Date).ToUniversalTime().AddDays(30).ToString('o')
$calendar = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('list-calendar', '--start', $start, '--end', $end, '--limit', '200')
if (-not $calendar.ok) {
    throw 'list-calendar failed.'
}

$calendarItems = @($calendar.result.items)
if ($ExpectCalendarData -and $calendarItems.Count -eq 0) {
    throw 'Calendar cache data was expected, but list-calendar returned zero items.'
}

if ($calendarItems.Count -gt 0) {
    $firstEventId = Get-BridgeFieldValue -Object $calendarItems[0] -Names @('bridgeId', 'bridge_id')
    $singleEvent = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('get-event', '--id', [string]$firstEventId)
    if (-not $singleEvent.ok) {
        throw 'get-event failed for a cached event identifier.'
    }
}

if ($status.result.mode -eq 'safe') {
    Assert-SafeModePrivacy -Items $messageItems
}

$primaryInteractiveSession = [bool](query user 2>$null | Select-String -SimpleMatch $env:USERNAME)
Write-OperatorEvidence -PrimaryInteractiveSession $primaryInteractiveSession -OutputPath $OperatorEvidenceOutputPath -SvcPipeConnect $OpenClawSvcPipeConnect -NetworkDenyState $NetworkDenyVerified

for ($i = 0; $i -lt 25; $i++) {
    $statusResponse = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('status')
    if (-not $statusResponse.ok) {
        throw 'Repeated status call failed during hygiene validation.'
    }

    $messageResponse = Invoke-Json -ClientExecutablePath $ClientPath -CommandArguments @('list-messages', '--since', $since, '--limit', '1')
    if (-not $messageResponse.ok) {
        throw 'Repeated list-messages call failed during hygiene validation.'
    }
}

Write-Output 'AutomatedSuitesPassed: A,B,C,D,F'
Write-Output "OperatorEvidencePath: $OperatorEvidenceOutputPath"
