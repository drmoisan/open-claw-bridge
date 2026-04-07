# OpenClaw MailBridge

OpenClaw MailBridge is a Windows-focused .NET solution for running a local Outlook bridge, querying it through a small CLI client, sharing RPC contracts between components, and automating install/test/bootstrap workflows from PowerShell and Codex tooling.

At runtime, the bridge hosts background workers that talk to classic Outlook over COM on a dedicated STA thread, track bridge state in a local SQLite cache, and expose a local named-pipe RPC surface for a companion client.

## Current State

- The bridge host, client, contracts library, PowerShell scripts, and MSTest suite are all present in this repository.
- The bridge currently implements settings loading/validation, host startup, Outlook connectivity/state transitions, scan timestamp persistence, and the `get_status` RPC path.
- The broader RPC contract surface for messages, meeting requests, calendar windows, and event lookup is already defined end to end, but the current server implementation returns placeholder empty collections for supported non-status methods.
- This README reflects the code as it exists today. Some older secondary docs and helper scripts in the repo still show an earlier client invocation shape.

## Solution Layout

| Path | Purpose |
| --- | --- |
| `src/OpenClaw.MailBridge/` | Windows bridge host executable and runtime services. |
| `src/OpenClaw.MailBridge.Client/` | Command-line named-pipe client for querying the bridge. |
| `src/OpenClaw.MailBridge.Contracts/` | Shared contracts, settings model, validators, ID helpers, and sanitizers. |
| `tests/OpenClaw.MailBridge.Tests/` | MSTest + FluentAssertions coverage for runtime, contracts, and bootstrap scripts. |
| `scripts/` | Build, test, run, install, uninstall, task-registration, and acceptance helpers. |
| `docs/setup.md` | Workspace and environment setup notes. |
| `docs/mailbridge-runbook.md` | Operator-facing install/runbook guidance. |
| `.codex/codex-web-setup.sh` | Codex Web bootstrap script for cloud/dev bootstrap scenarios. |

## What The Repo Provides

| Area | What is implemented today |
| --- | --- |
| Bridge startup | `BridgeApplication` loads or creates `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`, validates settings, builds the generic host, and runs it. |
| Outlook integration | `OutlookScanner` detects or starts classic Outlook, updates bridge lifecycle state, and records scan timestamps. |
| COM safety | `OutlookStaExecutor` runs Outlook-bound work on a dedicated STA thread. |
| Local persistence | `CacheRepository` creates a local SQLite database under `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db` and persists scan-state timestamps. |
| RPC server | `PipeRpcWorker` hosts a named pipe, validates request size and method names, and returns JSON RPC-style responses. |
| RPC client | `OpenClaw.MailBridge.Client` builds requests for the supported command set and converts bridge/error results into process exit codes. |
| Shared models | `OpenClaw.MailBridge.Contracts` contains DTOs, settings, error codes, `BridgeIdCodec`, `BodySanitizer`, and `BridgeSettingsValidator`. |
| Install automation | PowerShell scripts create config directories, register an interactive scheduled task, run preflight checks, and smoke-test an installed bridge. |
| Dev/bootstrap tooling | `.codex/codex-web-setup.sh` restores .NET prerequisites in Codex Web and runs an optional repo bootstrap hook. |

## Runtime Architecture

1. `Program.Main` delegates to `BridgeApplication.RunAsync`.
2. `BridgeApplication` resolves the config path, loads settings, validates them, and builds the host.
3. The host registers `BridgeStateStore`, `CacheRepository`, `OutlookStaExecutor`, `OutlookScanner`, `ScanWorker`, and `PipeRpcWorker`.
4. `ScanWorker` initializes the SQLite cache and repeatedly invokes `OutlookScanner` on the dedicated STA executor.
5. `OutlookScanner` either attaches to a running Outlook instance or launches/logs on to Outlook, then updates bridge readiness and scan timestamps.
6. `PipeRpcWorker` listens on a named pipe, applies a local-only ACL, validates payloads, and serves RPC responses.

## Prerequisites

### Runtime prerequisites

- Windows.
- Classic Outlook installed and available through COM.
- A user session capable of running an interactive scheduled task if you want the installed bridge to auto-start at logon.

### Development prerequisites

- .NET SDK `10.0.201` from [`global.json`](./global.json).
- PowerShell for the helper scripts in [`scripts/`](./scripts).
- `csharpier` for the repo's C# formatting policy.

### Non-Windows contributor note

The runtime projects target `net10.0-windows`, so the bridge itself is Windows-only. The Codex/bootstrap workflow supports non-Windows restore scenarios by enabling Windows targeting during restore when needed.

## Build And Test

### Simple script entry points

```powershell
./scripts/Build.ps1
./scripts/Test.ps1
```

### Direct .NET commands

```powershell
dotnet restore .\OpenClaw.MailBridge.sln
dotnet build .\OpenClaw.MailBridge.sln -c Debug
dotnet test .\OpenClaw.MailBridge.sln -c Debug
```

On non-Windows environments that only need restore/bootstrap:

```powershell
dotnet restore .\OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
```

## Running The Bridge Locally

The bridge defaults to a per-user config file under `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`. If the file does not exist, the bridge creates it with default settings.

### Run the host

```powershell
dotnet run --project .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj --configuration Debug
```

You can also force an explicit config path:

```powershell
dotnet run --project .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj --configuration Debug -- --config "$env:LOCALAPPDATA\OpenClaw\MailBridge\bridge.settings.json"
```

### Query the bridge from the client

```powershell
dotnet run --project .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj --configuration Debug -- status
```

Example success shape:

```json
{
  "id": "3cfab8f0-9f1a-4f0d-8829-7d31fdb0edcb",
  "ok": true,
  "result": {
    "state": "ready",
    "mode": "safe",
    "outlookConnected": true,
    "cacheStale": false,
    "staleReason": null,
    "lastInboxScanUtc": "2026-04-06T20:00:00+00:00",
    "lastCalendarScanUtc": "2026-04-06T20:00:00+00:00"
  },
  "error": null
}
```

## Configuration Reference

The bridge settings model lives in [`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`](./src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs).

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

| Setting | Default | Notes |
| --- | --- | --- |
| `pipeName` | `openclaw_mailbridge_v1` | Named pipe used by the bridge host. |
| `mode` | `safe` | Validator currently allows `safe` or `enhanced`. |
| `autostartOutlook` | `true` | Allows the scanner to create/log on to Outlook when it is not already running. |
| `inboxPollSeconds` | `30` | Scan cadence used by `ScanWorker`. Must be `>= 5`. |
| `calendarPollSeconds` | `300` | Validated even though the current worker loop uses inbox cadence only. Must be `>= 30`. |
| `inboxOverlapMinutes` | `5` | Reserved for message windowing behavior. |
| `calendarPastDays` | `14` | Reserved for calendar query range behavior. |
| `calendarFutureDays` | `60` | Reserved for calendar query range behavior. |
| `maxItemsPerScan` | `500` | Must be between `1` and `2000`. |
| `bodyPreviewMaxChars` | `500` | Must be between `1` and `2000`. |
| `logLevel` | `Information` | Forwarded into the runtime's logging setup. |

## CLI And RPC Reference

The client command surface is defined in [`src/OpenClaw.MailBridge.Client/Program.cs`](./src/OpenClaw.MailBridge.Client/Program.cs), and the RPC method constants live in [`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`](./src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs).

| Client command | Required options | RPC method | Current server behavior |
| --- | --- | --- | --- |
| `status` | none | `get_status` | Implemented. Returns lifecycle state, mode, cache flags, and latest scan timestamps. |
| `list-messages` | `--since`, `--limit` | `list_recent_messages` | Accepted, but currently returns a placeholder success payload with an empty `items` collection. |
| `get-message` | `--id` | `get_message` | Accepted, but currently returns the same placeholder empty `items` payload. |
| `list-meeting-requests` | `--since`, `--limit` | `list_recent_meeting_requests` | Accepted, but currently returns the same placeholder empty `items` payload. |
| `list-calendar` | `--start`, `--end`, `--limit` | `list_calendar_window` | Accepted, but currently returns the same placeholder empty `items` payload. |
| `get-event` | `--id` | `get_event` | Accepted, but currently returns the same placeholder empty `items` payload. |

### Client exit codes

| Exit code | Meaning |
| --- | --- |
| `0` | Success. |
| `2` | Timeout or pipe I/O failure. |
| `3` | Unauthorized access. |
| `4` | Outlook unavailable. |
| `5` | Invalid usage or invalid request. |
| `6` | Other bridge-side failure. |

### Important current limitation

The client currently always connects to the hard-coded pipe name `openclaw_mailbridge_v1`. It does not yet honor a pipe override from the command line or bridge settings.

## Install, Register, Test, And Uninstall Scripts

| Script | Purpose | Notes |
| --- | --- | --- |
| [`scripts/install-mailbridge.ps1`](./scripts/install-mailbridge.ps1) | Creates the install/config directories, seeds default config, performs Outlook preflight checks, registers the scheduled task, and smoke-checks client status. | Intended for installed binaries under `C:\Program Files\OpenClaw\MailBridge`. |
| [`scripts/register-mailbridge-task.ps1`](./scripts/register-mailbridge-task.ps1) | Registers an interactive `schtasks` on-logon entry for the primary user. | Starts the task immediately if that user is already logged on. |
| [`scripts/test-mailbridge.ps1`](./scripts/test-mailbridge.ps1) | Runs an acceptance-style smoke test against an installed bridge. | Verifies lifecycle readiness, query calls, privacy expectations, and repeated request hygiene. |
| [`scripts/uninstall-mailbridge.ps1`](./scripts/uninstall-mailbridge.ps1) | Stops and deletes the scheduled task. | Leaves user data/config in place. |
| [`scripts/Run-Bridge.ps1`](./scripts/Run-Bridge.ps1) | Runs the bridge project in `Development`. | Good for local debugging. |
| [`scripts/Run-Client.ps1`](./scripts/Run-Client.ps1) | Convenience client runner. | Still reflects an older CLI shape and should be updated before relying on it. |
| [`scripts/Build.ps1`](./scripts/Build.ps1) | Builds the solution. | Thin wrapper over `dotnet build`. |
| [`scripts/Test.ps1`](./scripts/Test.ps1) | Runs the solution tests. | Thin wrapper over `dotnet test`. |

## Security And Operational Notes

- The bridge is designed for local machine use through a named pipe, not as a network service.
- The named-pipe ACL explicitly grants access to `SYSTEM`, Administrators, the current user, and optionally `openclaw-svc`, while denying the `NETWORK` SID.
- `safe` and `enhanced` modes are part of the settings contract. The repository defaults to `safe`.
- `BodySanitizer` removes HTML tags, squashes whitespace, and replaces Windows file paths with `[path]` in preview text.
- SQLite persistence is local to the current user profile under `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db`.

## Testing

The test project uses:

- MSTest
- FluentAssertions
- `coverlet.collector`

Current test coverage includes:

- contracts and helper utilities
- bridge startup and argument parsing
- state-store behavior
- scanner behavior and failure handling
- pipe RPC validation and status responses
- cache repository scan-state persistence
- Codex Web bootstrap behavior for `.NET` detection and restore flows

## Codex Web Bootstrap

[`.codex/codex-web-setup.sh`](./.codex/codex-web-setup.sh) is the repo bootstrap entry point for Codex Web and similar cloud/dev environments. It:

- prints repository and git diagnostics
- detects package-manager manifests
- restores the pinned .NET SDK when needed
- restores local .NET tools when a tool manifest exists
- enables Windows targeting for restore on non-Windows hosts when the repo targets `-windows`
- runs an optional repo bootstrap hook such as `.codex/setup.sh`

## Additional Docs

- Workspace setup: [`docs/setup.md`](./docs/setup.md)
- Operator runbook: [`docs/mailbridge-runbook.md`](./docs/mailbridge-runbook.md)
- Active feature and remediation docs: [`docs/features/active/`](./docs/features/active/)

## Known Gaps And Follow-Ups

- The message, meeting-request, calendar, and event RPC methods are contract-complete but still stubbed in the current bridge worker.
- The convenience client helper script and some older docs still reference an earlier command shape and pipe-name example.
- The local SQLite schema already includes `messages` and `events` tables, but the current runtime path only persists `scan_state`.
