# OpenClaw MailBridge Runbook

## Overview

OpenClaw MailBridge is a local-only, read-only Windows Outlook bridge for classic Outlook. The bridge runs inside the primary interactive user session, scans the default Inbox and Calendar on one dedicated STA thread, caches normalized metadata in SQLite, and serves that cached data over a named pipe to `OpenClaw.MailBridge.Client.exe`.

The repository also includes an additive pre-MVP deployment path for `OpenClaw.HostAdapter` and `OpenClaw.Core`. In that model, the Windows host continues to own Outlook access and the named pipe, `OpenClaw.HostAdapter` exposes authenticated HTTP read routes, and `OpenClaw.Core` runs locally in Docker Desktop with loopback-only publishing on `127.0.0.1`.

The supported host, client, contracts, and test projects target `net10.0-windows`, and the installed runtime evidence must confirm `Microsoft.NETCore.App 10.0.0` for both the bridge host and the client before Windows acceptance is reported as complete.

## Install

1. Publish the host and client to `C:\Program Files\OpenClaw\MailBridge\` using the `net10.0-windows` target.
2. Run:

   ```powershell
   .\scripts\install-mailbridge.ps1 -PrimaryUser '<PRIMARY_USER>'
   ```

3. The installer seeds `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` in **safe** mode, validates classic Outlook/profile/default-folder prerequisites, verifies the installed runtimeconfig files require .NET 10, and registers the on-logon interactive task.

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
- Keep the bridge in `safe` mode until scripted acceptance and operator-only validation are both complete.

### Enhanced mode caveats

- `enhanced` is opt-in.
- Enhanced mode returns sanitized and truncated preview data and may expose additional protected metadata already defined by the contracts.
- Enable enhanced mode only after operator validation because Outlook protected-property prompts may appear in some environments.

## Additive HostAdapter And Core Deployment

1. Provision the HostAdapter `token file` on Windows, for example at `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`, and keep its ACL restricted to the intended local operator path.
2. Start `OpenClaw.HostAdapter` on the Windows host so it can shell out to `OpenClaw.MailBridge.Client`.
3. Copy `.env.example` to `.env` and point `HOSTADAPTER_TOKEN_FILE` at the Windows token file.
4. Run `docker compose --env-file .env -f docker-compose.yml -f docker-compose.dev.yml up --build openclaw-core`.
5. Open the local-only UI on `http://127.0.0.1:8080` and confirm `/health/ready` reports ready before treating the container path as healthy.

If the additive path is unavailable, use the fallback to `OpenClaw.MailBridge.Client` on the Windows host while the HostAdapter or container issue is being diagnosed.

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

The acceptance output must also record these framework-evidence keys before Suite A begins:

- `PublishedBridgeTargetFramework`
- `PublishedClientTargetFramework`
- `BridgeRuntimeFramework`
- `ClientRuntimeFramework`

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
5. Confirm the installed `OpenClaw.MailBridge.runtimeconfig.json` and `OpenClaw.MailBridge.Client.runtimeconfig.json` files require `Microsoft.NETCore.App 10.0.0`.

## Troubleshooting

- `waiting_for_outlook`: start classic Outlook or set `autostartOutlook=true`.
- `degraded`: inspect startup/log output, verify Outlook profile/default folders, and confirm stale-cache reason.
- `requires .NET 10`: the installed runtimeconfig files do not require the expected framework; republish and rerun `scripts/install-mailbridge.ps1`.
- Pipe access failures: validate ACL grants for `SYSTEM`, Administrators, the primary user SID, and `openclaw-svc`, with `NETWORK` denied.
- Missing cached message or calendar data: run the scripted acceptance suites again after Outlook has had time to populate the cache.
- HostAdapter token errors: confirm the `token file` exists, that the Docker bind mount points to the correct Windows path, and that the container receives it at `/run/openclaw/hostadapter.token`.
- Container readiness failures: check `/health/ready`, verify `OpenClaw__HostAdapter__BaseUrl` still targets `host.docker.internal`, and confirm the loopback publish remains `127.0.0.1:${OPENCLAW_HTTP_PORT:-8080}:8080`.
- Cached-data warnings in a `degraded` state are expected when the bridge is stale. Keep serving cached reads with the warning visible, then fall back to `OpenClaw.MailBridge.Client` for direct Windows-host troubleshooting if the degraded condition persists.
