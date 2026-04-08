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

function Test-IsElevated {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $currentPrincipal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)

    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
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

function Get-RuntimeFrameworkDescription {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$RuntimeConfig
    )

    return '{0} {1}' -f $RuntimeConfig.runtimeOptions.framework.name, $RuntimeConfig.runtimeOptions.framework.version
}

function Assert-DotNet10RuntimeConfig {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Justification = 'Assert communicates an explicit validation contract already used by the tests.')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeConfigPath,
        [Parameter(Mandatory = $true)]
        [string]$ComponentName
    )

    if (-not (Test-Path $RuntimeConfigPath)) {
        throw "$ComponentName runtimeconfig not found at '$RuntimeConfigPath'."
    }

    $runtimeConfig = Get-Content -Path $RuntimeConfigPath -Raw | ConvertFrom-Json
    $frameworkName = [string]$runtimeConfig.runtimeOptions.framework.name
    $frameworkVersion = [string]$runtimeConfig.runtimeOptions.framework.version
    $frameworkDescription = Get-RuntimeFrameworkDescription -RuntimeConfig $runtimeConfig

    # Reject installs that were published against the wrong shared framework before registration or smoke checks proceed.
    if ($frameworkName -ne 'Microsoft.NETCore.App' -or $frameworkVersion -notlike '10.*') {
        throw "$ComponentName runtimeconfig requires .NET 10. Expected Microsoft.NETCore.App 10.x but found $frameworkDescription at '$RuntimeConfigPath'."
    }
}

$configRoot = Join-Path $env:LOCALAPPDATA 'OpenClaw\MailBridge'
$configPath = Join-Path $configRoot 'bridge.settings.json'
$bridgeRuntimeConfigPath = Join-Path $InstallRoot 'OpenClaw.MailBridge.runtimeconfig.json'
$clientRuntimeConfigPath = Join-Path $InstallRoot 'OpenClaw.MailBridge.Client.runtimeconfig.json'
$clientPath = Join-Path $InstallRoot 'OpenClaw.MailBridge.Client.exe'
$isElevated = Test-IsElevated

if ($PSCmdlet.ShouldProcess($InstallRoot, 'Install and validate OpenClaw MailBridge prerequisites')) {
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $configRoot 'logs') | Out-Null

    if (-not (Test-Path $configPath)) {
        Get-BridgeDefaultConfiguration | ConvertTo-Json -Depth 4 | Set-Content -Path $configPath -Encoding utf8
    }

    # Elevated shells can lose access to the user's MAPI profile, so keep the check in the interactive user context only.
    if (-not $isElevated) {
        Test-OutlookProfilePrerequisite
    }
    else {
        Write-Warning 'Skipping Outlook profile preflight in the elevated session. Validate Outlook prerequisites from the primary interactive user session.'
    }

    Assert-DotNet10RuntimeConfig -RuntimeConfigPath $bridgeRuntimeConfigPath -ComponentName 'Bridge host'
    Assert-DotNet10RuntimeConfig -RuntimeConfigPath $clientRuntimeConfigPath -ComponentName 'Client'

    & "$PSScriptRoot/register-mailbridge-task.ps1" -PrimaryUser $PrimaryUser -InstallRoot $InstallRoot -Confirm:$false

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

