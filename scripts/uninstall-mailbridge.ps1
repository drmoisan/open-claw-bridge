param([string]$TaskName='OpenClaw MailBridge')
$ErrorActionPreference='SilentlyContinue'
schtasks /end /tn $TaskName | Out-Null
schtasks /delete /tn $TaskName /f | Out-Null
Write-Host 'OpenClaw MailBridge task removed.'
