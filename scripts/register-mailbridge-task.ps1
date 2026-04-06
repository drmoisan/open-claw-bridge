param(
  [Parameter(Mandatory=$true)][string]$PrimaryUser,
  [string]$InstallRoot = 'C:\Program Files\OpenClaw\MailBridge',
  [string]$TaskName = 'OpenClaw MailBridge'
)

$ErrorActionPreference='Stop'
$configPath = Join-Path $env:LOCALAPPDATA 'OpenClaw\MailBridge\bridge.settings.json'
$exe = Join-Path $InstallRoot 'OpenClaw.MailBridge.exe'
$tr = '"{0}" --config "{1}"' -f $exe,$configPath
schtasks /create /tn $TaskName /tr $tr /sc onlogon /ru $PrimaryUser /it /f | Out-Null

$loggedOn = (query user 2>$null | Select-String -SimpleMatch $PrimaryUser)
if ($loggedOn) { schtasks /run /tn $TaskName | Out-Null }
