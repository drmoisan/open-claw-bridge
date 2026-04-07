[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PrimaryUser,
    [string]$InstallRoot = 'C:\Program Files\OpenClaw\MailBridge',
    [string]$TaskName = 'OpenClaw MailBridge'
)

$ErrorActionPreference = 'Stop'

function Get-RegisterMailBridgeTaskCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath
    )

    return '"{0}" --config "{1}"' -f $ExecutablePath, $ConfigPath
}

$configPath = Join-Path $env:LOCALAPPDATA 'OpenClaw\MailBridge\bridge.settings.json'
$exe = Join-Path $InstallRoot 'OpenClaw.MailBridge.exe'

if (-not (Test-Path $exe)) {
    throw "Bridge executable not found at '$exe'."
}

if (-not (Test-Path $configPath)) {
    throw "Bridge settings file not found at '$configPath'. Run install-mailbridge.ps1 first."
}

$taskCommand = Get-RegisterMailBridgeTaskCommand -ExecutablePath $exe -ConfigPath $configPath

if ($PSCmdlet.ShouldProcess($TaskName, 'Register interactive on-logon bridge task')) {
    schtasks /create /tn $TaskName /tr $taskCommand /sc onlogon /ru $PrimaryUser /it /f | Out-Null

    $loggedOn = query user 2>$null | Select-String -SimpleMatch $PrimaryUser
    if ($loggedOn) {
        schtasks /run /tn $TaskName | Out-Null
    }
}
