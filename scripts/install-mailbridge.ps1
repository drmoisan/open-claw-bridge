[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PrimaryUser,
    [string]$InstallRoot = 'C:\Program Files\OpenClaw\MailBridge'
)

$ErrorActionPreference = 'Stop'

function Get-BridgeDefaultConfiguration {
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param()

    return [ordered]@{
        pipeName            = 'openclaw_mailbridge_v1'
        mode                = 'safe'
        autostartOutlook    = $true
        inboxPollSeconds    = 30
        calendarPollSeconds = 300
        inboxOverlapMinutes = 5
        calendarPastDays    = 14
        calendarFutureDays  = 60
        maxItemsPerScan     = 500
        bodyPreviewMaxChars = 500
        logLevel            = 'Information'
    }
}

function Get-OutlookApplicationType {
    [CmdletBinding()]
    [OutputType([type])]
    param()

    return [type]::GetTypeFromProgID('Outlook.Application', $false)
}

function New-OutlookApplication {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([object])]
    param()

    if ($PSCmdlet.ShouldProcess('Outlook.Application', 'Create COM object')) {
        return New-Object -ComObject Outlook.Application
    }

    return $null
}

function Remove-ComObjectReference {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter()]
        [object]$ComObject
    )

    if ($ComObject -and $PSCmdlet.ShouldProcess('COM object', 'Release COM reference')) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($ComObject)
    }
}

function Test-OutlookProfilePrerequisite {
    [CmdletBinding()]
    param()

    $outlookType = Get-OutlookApplicationType
    if (-not $outlookType) {
        throw 'Outlook COM unavailable (classic Outlook required).'
    }

    $outlook = $null
    $namespace = $null
    try {
        $outlook = New-OutlookApplication
        $namespace = $outlook.GetNamespace('MAPI')
        $namespace.Logon('', '', $null, $null)
        $null = $namespace.GetDefaultFolder(6)
        $null = $namespace.GetDefaultFolder(9)
    }
    catch {
        throw 'Outlook profile preflight failed. Confirm classic Outlook is installed, a profile is configured, and the default Inbox and Calendar exist.'
    }
    finally {
        Remove-ComObjectReference -ComObject $namespace
        Remove-ComObjectReference -ComObject $outlook
    }
}

$configRoot = Join-Path $env:LOCALAPPDATA 'OpenClaw\MailBridge'
$configPath = Join-Path $configRoot 'bridge.settings.json'
$clientPath = Join-Path $InstallRoot 'OpenClaw.MailBridge.Client.exe'

if ($PSCmdlet.ShouldProcess($InstallRoot, 'Install and validate OpenClaw MailBridge prerequisites')) {
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $configRoot 'logs') | Out-Null

    if (-not (Test-Path $configPath)) {
        Get-BridgeDefaultConfiguration | ConvertTo-Json -Depth 4 | Set-Content -Path $configPath -Encoding utf8
    }

    Test-OutlookProfilePrerequisite

    & "$PSScriptRoot/register-mailbridge-task.ps1" -PrimaryUser $PrimaryUser -InstallRoot $InstallRoot

    if (-not (Test-Path $clientPath)) {
        throw "Client executable not found at '$clientPath'. Publish or copy the installed binaries before running install preflight."
    }

    $status = & $clientPath status 2>$null
    if (-not $status) {
        throw 'Bridge status preflight failed after registration.'
    }

    $logPath = Join-Path $configRoot 'logs\bridge.log'
    if (Test-Path $logPath) {
        $logContent = Get-Content $logPath -Raw
        if ($logContent -match 'body_preview|sender_name|sender_email') {
            throw 'Runtime log contains protected Outlook content markers.'
        }
    }

    Write-Output 'Install complete. Safe mode remains the seeded default.'
    Write-Warning 'Enhanced mode may trigger Outlook security prompts; enable only after operator verification.'
}
