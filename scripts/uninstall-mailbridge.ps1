[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$TaskName = 'OpenClaw MailBridge'
)

$ErrorActionPreference = 'Stop'

if ($PSCmdlet.ShouldProcess($TaskName, 'Remove the registered OpenClaw MailBridge scheduled task')) {
    schtasks /end /tn $TaskName 2>$null | Out-Null
    schtasks /delete /tn $TaskName /f | Out-Null
    Write-Output 'OpenClaw MailBridge scheduled task removed. Cache, logs, and settings were intentionally left in place.'
}
