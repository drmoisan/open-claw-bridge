Timestamp: 2026-04-07T14:04:00Z
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-mailbridge.ps1
EXIT_CODE: 1
Output Summary:
Phase 6 acceptance is blocked on this machine because the installed-bridge prerequisites are not satisfied.
- InstallRootExists: False (`C:\Program Files\OpenClaw\MailBridge` was not writable from this session)
- ClientExists: False (`C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe` not present)
- OutlookComAvailable: True
- Publish attempt to `C:\Program Files\OpenClaw\MailBridge` failed with access denied.
- The scheduled task and installed safe-mode bridge were therefore not provisioned for the exact acceptance-script path.
