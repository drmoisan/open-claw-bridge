param(
 [string]$TaskName='OpenClaw MailBridge',
 [string]$ClientPath='C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe'
)
$ErrorActionPreference='Stop'

function Invoke-Json([string[]]$Args) {
  $out = & $ClientPath @Args
  if (-not $out) { throw "No output for $Args" }
  return $out | ConvertFrom-Json
}

# A lifecycle
schtasks /run /tn $TaskName | Out-Null
$deadline = (Get-Date).AddMinutes(2)
do {
  Start-Sleep -Seconds 2
  $status = Invoke-Json @('status')
} while ($status.result.state -ne 'ready' -and (Get-Date) -lt $deadline)
if ($status.result.state -ne 'ready') { throw 'Bridge failed to become ready.' }
if (-not $status.result.mode) { throw 'Mode missing.' }

# B mail
$since=(Get-Date).ToUniversalTime().AddHours(-24).ToString('o')
$msgs=Invoke-Json @('list-messages','--since',$since,'--limit','20')
if (-not $msgs.ok) { throw 'list-messages failed' }

# C calendar
$start=(Get-Date).ToUniversalTime().AddDays(-1).ToString('o')
$end=(Get-Date).ToUniversalTime().AddDays(30).ToString('o')
$cal=Invoke-Json @('list-calendar','--start',$start,'--end',$end,'--limit','200')
if (-not $cal.ok) { throw 'list-calendar failed' }

# D privacy
if ($msgs.result.items) {
  foreach ($m in $msgs.result.items) {
    if ($m.body_preview) { throw 'Safe mode leaked body_preview' }
    if ($m.sender_name -or $m.sender_email) { throw 'Safe mode leaked sender fields' }
  }
}

# E isolation doc checks
Write-Host 'Verify openclaw-svc connectivity manually if not running under that account.'
Write-Host 'Verify NETWORK deny in pipe ACL from bridge logs or ACL dump.'

# F hygiene smoke
for ($i=0; $i -lt 100; $i++) {
  $null=Invoke-Json @('status')
  $null=Invoke-Json @('list-messages','--since',$since,'--limit','1')
}
Write-Host 'Acceptance script completed.'
