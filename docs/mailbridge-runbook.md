# OpenClaw MailBridge Runbook

## Overview
Local-only, read-only Windows Outlook bridge for classic Outlook. The bridge runs in the primary interactive user session and serves read APIs over `\\.\pipe\openclaw_mailbridge_v1` to `openclaw-svc` via `OpenClaw.MailBridge.Client.exe`.

## Install
1. Publish binaries to `C:\Program Files\OpenClaw\MailBridge\`.
2. Run:
   ```powershell
   .\scripts\install-mailbridge.ps1 -PrimaryUser '<PRIMARY_USER>'
   ```
3. Installer creates `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` in **safe** mode.

## Task registration
Use:
```powershell
.\scripts\register-mailbridge-task.ps1 -PrimaryUser '<PRIMARY_USER>'
```
Creates on-logon interactive task (`/sc onlogon /it`).

## Uninstall
```powershell
.\scripts\uninstall-mailbridge.ps1
```

## Configuration
Config file: `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`.
Default:
```json
{
  "pipeName": "openclaw_mailbridge_v1",
  "mode": "safe",
  "autostartOutlook": true,
  "inboxPollSeconds": 30,
  "calendarPollSeconds": 300,
  "inboxOverlapMinutes": 5,
  "calendarPastDays": 14,
  "calendarFutureDays": 60,
  "maxItemsPerScan": 500,
  "bodyPreviewMaxChars": 500,
  "logLevel": "Information"
}
```

## Operations
- Bridge states: `starting`, `waiting_for_outlook`, `ready`, `degraded`, `error`.
- Client commands:
  - `status`
  - `list-messages --since <ISO8601> --limit <n>`
  - `get-message --id <bridge_id>`
  - `list-meeting-requests --since <ISO8601> --limit <n>`
  - `list-calendar --start <ISO8601> --end <ISO8601> --limit <n>`
  - `get-event --id <bridge_id>`

## Troubleshooting
- `waiting_for_outlook`: start classic Outlook or set `autostartOutlook=true`.
- `degraded`: inspect HRESULT/high-level startup errors and confirm profile/default folders.
- Pipe access issues: validate ACL grants for SYSTEM, Administrators, primary user SID, openclaw-svc SID; NETWORK denied.

## Enhanced mode caveat
`enhanced` mode is optional and may trigger Outlook protected-property prompts. Keep disabled by default and enable only after operator validation.

## Acceptance test
Run:
```powershell
.\scripts\test-mailbridge.ps1
```
Covers lifecycle, mail/calendar read paths, privacy checks, isolation checks, and COM hygiene loop.
