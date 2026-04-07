# OpenClaw MailBridge Runbook

## Overview

OpenClaw MailBridge is a local-only, read-only Windows Outlook bridge for classic Outlook. The bridge runs inside the primary interactive user session, scans the default Inbox and Calendar on one dedicated STA thread, caches normalized metadata in SQLite, and serves that cached data over a named pipe to `OpenClaw.MailBridge.Client.exe`.

## Install

1. Publish binaries to `C:\Program Files\OpenClaw\MailBridge\`.
2. Run:

   ```powershell
   .\scripts\install-mailbridge.ps1 -PrimaryUser '<PRIMARY_USER>'
   ```

3. The installer seeds `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` in **safe** mode, validates classic Outlook/profile/default-folder prerequisites, and registers the on-logon interactive task.

## Configuration

Config file: `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`

Default settings:

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

### Safe mode defaults

- `safe` is the default install mode.
- Safe mode suppresses protected response fields such as `body_preview`, `sender_name`, and `sender_email` before cached messages are returned.

# OpenClaw MailBridge Runbook

## Overview

OpenClaw MailBridge is a local-only, read-only Windows Outlook bridge for classic Outlook. The bridge runs inside the primary interactive user session, scans the default Inbox and Calendar on one dedicated STA thread, caches normalized metadata in SQLite, and serves that cached data over a named pipe to `OpenClaw.MailBridge.Client.exe`.

## Install

1. Publish binaries to `C:\Program Files\OpenClaw\MailBridge\`.
2. Run:

   ```powershell
   .\scripts\install-mailbridge.ps1 -PrimaryUser '<PRIMARY_USER>'
   ```

3. The installer seeds `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` in **safe** mode, validates classic Outlook/profile/default-folder prerequisites, and registers the on-logon interactive task.

## Configuration

Config file: `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`

Default settings:

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

### Safe mode defaults

- `safe` is the default install mode.
- Safe mode suppresses protected response fields such as `body_preview`, `sender_name`, and `sender_email` before cached messages are returned.

### Enhanced mode caveats

- `enhanced` is opt-in.
- Enhanced mode returns sanitized and truncated preview data and may expose additional protected metadata already defined by the contracts.
- Enable enhanced mode only after operator validation because Outlook protected-property prompts may appear in some environments.

## Task registration

Use:

```powershell
.\scripts\register-mailbridge-task.ps1 -PrimaryUser '<PRIMARY_USER>'
```

The registration path preserves `/sc onlogon /it` so the bridge stays tied to the interactive user session.

## Uninstall

Use:

```powershell
.\scripts\uninstall-mailbridge.ps1
```

Uninstall removes the scheduled task only. Cache, logs, and settings are intentionally left in place for troubleshooting and rollback.

## Scripted acceptance suites

Run:

```powershell
.\scripts\test-mailbridge.ps1
```

The scripted suites cover:

- **Suite A** — readiness and status
- **Suite B** — cache-backed message list/get
- **Suite C** — cache-backed calendar list/get
- **Suite D** — safe-mode privacy enforcement
- **Suite F** — repeated-request hygiene

The script also writes operator evidence keys to its output path:

- `PrimaryInteractiveSession`
- `OpenClawSvcPipeConnect`
- `NetworkDenyVerified`

## Operator-only validation steps

The following checks remain operator validation work and should be recorded separately after the scripted suites pass:

1. Confirm the bridge is running in the **primary interactive user session**.
2. Confirm `openclaw-svc` can connect to the pipe in the target environment.
3. Confirm the pipe ACL denies the `NETWORK` SID.
4. Confirm classic Outlook is using the expected profile and that the default Inbox and Calendar are present.

## Troubleshooting

- `waiting_for_outlook`: start classic Outlook or set `autostartOutlook=true`.
- `degraded`: inspect startup/log output, verify Outlook profile/default folders, and confirm stale-cache reason.
- Pipe access failures: validate ACL grants for `SYSTEM`, Administrators, the primary user SID, and `openclaw-svc`, with `NETWORK` denied.
- Missing cached message or calendar data: run the scripted acceptance suites again after Outlook has had time to populate the cache.
