Timestamp: 2026-04-12T05:41:10Z
Command: Add-AppxPackage -Path 'artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix'; Set-Content $env:LOCALAPPDATA\OpenClaw\MailBridge\bridge.settings.json sentinel payload; Add-AppxPackage -Path 'artifacts/msix/OpenClaw.MailBridge_1.0.1.0_x64.msix'
EXIT_CODE: 0
Output Summary:
- ReconciledVersionTarget=1.0.1.0
- SpecAndUserStoryReconciledToExecutedVersion=True
- InstalledVersionAfterUpgrade=1.0.1.0
- InstallLocationChanged=True
- StartupRegistrationVisible=True
- SentinelSettingPreserved=True
- BridgeDirExistsAfterUpgrade=True
- ClientDirExistsAfterUpgrade=True
