Timestamp: 2026-04-13T00:12:28Z
Command: `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "<validation wrapper that backed up and restored %LOCALAPPDATA%\\OpenClaw\\MailBridge\\bridge.settings.json, ran OpenClaw.MailBridge.Client.exe status, exercised HostAdapter on 127.0.0.1:4319, launched docker run --name openclaw-core-validation -p 127.0.0.1:8082:8080 openclaw/core:pre-mvp with the HostAdapter token bind mount, stopped OpenClaw.MailBridge.exe --config TestResults/qa-csharp/phase7-bridge.settings.json to force degraded state, then restarted the bridge and removed the temporary container>"`
EXIT_CODE: 0
Output Summary: Safe-state validation passed and degraded-state validation passed. In safe state, the existing bridge/client stack remained operational: `OpenClaw.MailBridge.Client.exe status` returned `ok: true`, bridge state `ready`, and mode `safe`; HostAdapter `/v1/status` returned `200`, `/v1/messages` returned `200` with 5 items, and `/v1/calendar` returned `200` with 5 items. The Docker Desktop validation Core container on `127.0.0.1:8082` returned `/health/live` `200`, `/health/ready` `200`, `/api/status` `200`, `/api/messages/recent` `200` with 5 cached items, and `/api/events/window` `200` with 5 cached items. In degraded state, after stopping the bridge process that used `TestResults/qa-csharp/phase7-bridge.settings.json`, HostAdapter `/v1/status` returned `502` with error code `TRANSPORT_FAILURE`; the validation Core container returned `/health/ready` `503` with status `degraded`, kept `/api/status` at `200`, and continued serving cached messages and cached events at `200` with 5 items each. Post-run cleanup restored `%LOCALAPPDATA%\\OpenClaw\\MailBridge\\bridge.settings.json` to its original sentinel content, restarted `OpenClaw.MailBridge.exe` successfully, revalidated bridge readiness through `OpenClaw.MailBridge.Client.exe status --pipe-name openclaw-mail-bridge`, and removed the temporary `openclaw-core-validation` container.
SafeStateResult: PASS
SafeStateDetails:
- Existing bridge/client fallback command succeeded: `OpenClaw.MailBridge.Client.exe status` -> `ready` / `safe`.
- HostAdapter path succeeded on the Windows host: `/v1/status` -> `200`, `/v1/messages` -> `200` with 5 items, `/v1/calendar` -> `200` with 5 items.
- Docker Desktop path succeeded through the temporary validation container: `/health/live` -> `200`, `/health/ready` -> `200`, `/api/status` -> `200`, `/api/messages/recent` -> `200` with 5 items, `/api/events/window` -> `200` with 5 items.
DegradedStateResult: PASS
DegradedStateDetails:
- Forced degraded condition: stopped `OpenClaw.MailBridge.exe` process running with `--config TestResults/qa-csharp/phase7-bridge.settings.json`.
- HostAdapter reflected downstream bridge failure: `/v1/status` -> `502`, error code `TRANSPORT_FAILURE`.
- Docker Desktop validation container reflected degraded readiness while preserving cached reads: `/health/ready` -> `503` with `degraded`, `/api/status` -> `200`, `/api/messages/recent` -> `200` with 5 cached items, `/api/events/window` -> `200` with 5 cached items.
Evidence:
- Existing bridge/client stack proof before degradation: `OpenClaw.MailBridge.Client.exe status` returned `ok: true`, `state: ready`, `mode: safe`.
- Existing bridge/client stack proof after cleanup: `OpenClaw.MailBridge.Client.exe status --pipe-name openclaw-mail-bridge` returned `ok: true`, `state: ready`, `mode: safe`.
- Docker validation container log captured at `TestResults/p7-t10-validation-live/core-validation.log`.
