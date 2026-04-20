#Requires -Version 7.0
<#
.SYNOPSIS
    Reverses a prior Install.ps1 run by consuming
    %LOCALAPPDATA%\OpenClaw\install-record.json. Runs compose down, removes
    the MSIX, removes the per-version destination folder, and deletes the
    install record.

.DESCRIPTION
    The uninstall sequence runs every step regardless of individual step
    failures. Per-step failures are collected and reported as a single
    terminating error at the end of the run. User configuration under
    %LOCALAPPDATA%\OpenClaw\MailBridge\ is preserved because it lives under a
    sibling directory and is never touched by this script.

    Takes no parameters; all state is loaded from the install record.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param()

Import-Module (Join-Path $PSScriptRoot 'Install.Helpers.psm1') -Force
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Main (only runs when executed directly, not when dot-sourced for tests) ---
if ($MyInvocation.InvocationName -ne '.') {

    # Stage 1: load the install record (throws with "no prior install recorded").
    $InstallRecordPath = Join-Path $env:LOCALAPPDATA 'OpenClaw/install-record.json'
    Write-Information "[uninstall:load] Reading install record $InstallRecordPath" -InformationAction Continue
    $record = Read-InstallRecord -RecordPath $InstallRecordPath

    $failures = @()

    # Stage 2: compose down (skipped when record.skipDocker is true).
    try {
        if (-not $record.skipDocker) {
            Write-Information "[uninstall:docker] docker compose down $($record.composeFilePath)" -InformationAction Continue
            Invoke-ComposeDown -ComposeFilePath $record.composeFilePath -ProjectName $record.composeProjectName
        }
    }
    catch {
        $failures += [pscustomobject]@{ Step = 'compose-down'; Error = $_.Exception.Message }
    }

    # Stage 3: remove MSIX.
    try {
        Write-Information "[uninstall:msix] Remove-AppxPackage $($record.packageFullName)" -InformationAction Continue
        Invoke-MsixRemove -PackageFullName $record.packageFullName
    }
    catch {
        $failures += [pscustomobject]@{ Step = 'msix-remove'; Error = $_.Exception.Message }
    }

    # Stage 4: remove destination folder (missing-target tolerated).
    try {
        Write-Information "[uninstall:folder] Remove-Item $($record.destinationPath)" -InformationAction Continue
        if (Test-Path -LiteralPath $record.destinationPath) {
            if ($PSCmdlet.ShouldProcess($record.destinationPath, 'Remove destination folder')) {
                Remove-Item -LiteralPath $record.destinationPath -Recurse -Force
            }
        }
    }
    catch {
        $failures += [pscustomobject]@{ Step = 'remove-destination'; Error = $_.Exception.Message }
    }

    # Stage 5: remove the install record file.
    try {
        Write-Information "[uninstall:record] Remove-Item $InstallRecordPath" -InformationAction Continue
        if (Test-Path -LiteralPath $InstallRecordPath) {
            if ($PSCmdlet.ShouldProcess($InstallRecordPath, 'Remove install record')) {
                Remove-Item -LiteralPath $InstallRecordPath -Force
            }
        }
    }
    catch {
        $failures += [pscustomobject]@{ Step = 'remove-record'; Error = $_.Exception.Message }
    }

    # Stage 6: terminal report. Throw a single terminating error enumerating
    # every failed step; exit 0 silently on success.
    if ($failures.Count -gt 0) {
        $lines = @($failures | ForEach-Object { "- $($_.Step): $($_.Error)" })
        throw ("Uninstall completed with " + $failures.Count + " failed step(s):" + [Environment]::NewLine + ($lines -join [Environment]::NewLine))
    }
    Write-Information '[uninstall:done] Uninstall completed successfully' -InformationAction Continue
}
