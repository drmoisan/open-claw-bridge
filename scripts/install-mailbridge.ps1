param(
  [Parameter(Mandatory=$true)][string]$PrimaryUser,
  [string]$InstallRoot = 'C:\Program Files\OpenClaw\MailBridge'
)
$ErrorActionPreference='Stop'

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
$cfgRoot = Join-Path $env:LOCALAPPDATA 'OpenClaw\MailBridge'
New-Item -ItemType Directory -Force -Path (Join-Path $cfgRoot 'logs') | Out-Null

$defaultCfg = @{
 pipeName='openclaw_mailbridge_v1'; mode='safe'; autostartOutlook=$true; inboxPollSeconds=30; calendarPollSeconds=300; inboxOverlapMinutes=5; calendarPastDays=14; calendarFutureDays=60; maxItemsPerScan=500; bodyPreviewMaxChars=500; logLevel='Information'
}
$cfgPath = Join-Path $cfgRoot 'bridge.settings.json'
if (-not (Test-Path $cfgPath)) { $defaultCfg | ConvertTo-Json | Set-Content -Encoding UTF8 $cfgPath }

# preflight 1-3: COM + default folders
$outlookType = [type]::GetTypeFromProgID('Outlook.Application', $false)
if (-not $outlookType) { throw 'Outlook COM unavailable (classic Outlook required).' }
$outlook = New-Object -ComObject Outlook.Application
$ns = $outlook.GetNamespace('MAPI')
$ns.Logon('', '', $null, $null)
$null = $ns.GetDefaultFolder(6)
$null = $ns.GetDefaultFolder(9)

& "$PSScriptRoot/register-mailbridge-task.ps1" -PrimaryUser $PrimaryUser -InstallRoot $InstallRoot
Start-Sleep -Seconds 2
$status = & (Join-Path $InstallRoot 'OpenClaw.MailBridge.Client.exe') status 2>$null
if (-not $status) { throw 'Bridge status preflight failed.' }

if (Test-Path (Join-Path $cfgRoot 'logs\bridge.log')) {
  $logContent = Get-Content (Join-Path $cfgRoot 'logs\bridge.log') -Raw
  if ($logContent -match 'body_preview') { throw 'Log contains prohibited body content.' }
}

Write-Host 'Install complete. Enhanced mode remains disabled by default.'
Write-Warning 'Enhanced mode may trigger Outlook security prompts; enable only after operator verification.'
