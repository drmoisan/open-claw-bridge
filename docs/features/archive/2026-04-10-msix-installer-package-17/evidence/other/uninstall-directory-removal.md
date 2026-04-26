Timestamp: 2026-04-12T05:41:10Z
Command: $package = Get-AppxPackage -Name 'OpenClaw.MailBridge'; $bridgePath = Join-Path $package.InstallLocation 'bridge'; $clientPath = Join-Path $package.InstallLocation 'client'; $package | Remove-AppxPackage; Test-Path $bridgePath; Test-Path $clientPath; Test-Path $env:LOCALAPPDATA\OpenClaw\MailBridge\bridge.settings.json
EXIT_CODE: 0
Output Summary:
- PackagePresentAfterUninstall=False
- StartupRegistrationVisible=False
- BridgeDirectoryRemoved=True
- ClientDirectoryRemoved=True
- SettingsFileStillPresent=True
- CheckedBridgePath=C:\Program Files\WindowsApps\OpenClaw.MailBridge_1.0.1.0_x64__124xeds558nzw\bridge
- CheckedClientPath=C:\Program Files\WindowsApps\OpenClaw.MailBridge_1.0.1.0_x64__124xeds558nzw\client
