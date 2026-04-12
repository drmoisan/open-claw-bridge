# OpenClaw MailBridge

OpenClaw MailBridge is a Windows-focused .NET solution for running a local Outlook bridge, querying it through a small CLI client, sharing RPC contracts between components, and automating install/test/bootstrap workflows from PowerShell and Codex tooling.

At runtime, the bridge hosts background workers that talk to classic Outlook over COM on a dedicated STA thread, track bridge state in a local SQLite cache, and expose a local named-pipe RPC surface for a companion client.

## Current State

- The bridge host, client, contracts library, PowerShell scripts, and MSTest/Pester suite are all present in this repository.
- All production and test projects target `net10.0-windows`.
- The bridge scans the default Inbox and Calendar on one dedicated STA thread, persists cached message/event metadata in SQLite, and serves cache-backed results for the full supported RPC surface.
- The client resolves its pipe name from `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` and accepts an optional `--pipe-name` override.
- Non-status RPC methods are fully cache-backed: recent-message, meeting-request, calendar-window, and single-item lookups all return repository-backed results instead of placeholders.
- Response shaping preserves `safe` versus `enhanced` behavior, with `safe` as the default install mode and `enhanced` reserved for post-validation opt-in.
- Acceptance evidence is split between scripted deterministic coverage (`scripts/test-mailbridge.ps1`) and operator-only Windows validation steps documented in `docs/mailbridge-runbook.md`.

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
| Outlook integration | `OutlookScanner` attaches to a running Outlook instance first, creates/logs on only when allowed, resolves the default Inbox and Calendar, and normalizes cached message/event metadata. |
| COM safety | `OutlookStaExecutor` runs Outlook-bound work on a dedicated STA thread. |
| Local persistence | `CacheRepository` creates a local SQLite database under `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db` and persists `messages`, `events`, and `scan_state`. |
| RPC server | `PipeRpcWorker` hosts a named pipe, validates requests deterministically, enforces strict pipe ACL setup, and returns cache-backed JSON RPC-style responses. |
| RPC client | `OpenClaw.MailBridge.Client` builds requests for the supported command set, resolves the configured pipe name, keeps JSON on stdout, and maps bridge/error results into deterministic process exit codes. |
| Shared models | `OpenClaw.MailBridge.Contracts` contains DTOs, settings, error codes, `BridgeIdCodec`, `BodySanitizer`, and `BridgeSettingsValidator`. |
| Install automation | PowerShell scripts create config directories, register an interactive scheduled task, run preflight checks, and smoke-test an installed bridge. |
| Dev/bootstrap tooling | `.codex/codex-web-setup.sh` restores .NET prerequisites in Codex Web and runs an optional repo bootstrap hook. |

## Runtime Architecture

1. `Program.Main` delegates to `BridgeApplication.RunAsync`.
2. `BridgeApplication` resolves the config path, loads settings, validates them, and builds the host.
3. The host registers `BridgeStateStore`, `CacheRepository`, `OutlookStaExecutor`, `OutlookScanner`, `ScanWorker`, and `PipeRpcWorker`.
4. `ScanWorker` initializes the SQLite cache and repeatedly invokes `OutlookScanner` on the dedicated STA executor.
5. `OutlookScanner` attaches to a running Outlook instance first, launches/logs on only when allowed, scans the default Inbox and Calendar, and updates bridge readiness plus stale-cache metadata.
6. `PipeRpcWorker` listens on a named pipe, applies strict local-only ACLs, validates payloads, and serves repository-backed RPC responses.

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
| `list-messages` | `--since`, `--limit` | `list_recent_messages` | Returns cache-backed message rows ordered newest-first. |
| `get-message` | `--id` | `get_message` | Returns a cached message or `NOT_FOUND`. |
| `list-meeting-requests` | `--since`, `--limit` | `list_recent_meeting_requests` | Returns cache-backed meeting-request rows. |
| `list-calendar` | `--start`, `--end`, `--limit` | `list_calendar_window` | Returns cache-backed event rows ordered by start time. |
| `get-event` | `--id` | `get_event` | Returns a cached event or `NOT_FOUND`. |

### Client exit codes

| Exit code | Meaning |
| --- | --- |
| `0` | Success. |
| `2` | Timeout or pipe I/O failure. |
| `3` | Unauthorized access. |
| `4` | Outlook unavailable. |
| `5` | Invalid usage or invalid request. |
| `6` | Other bridge-side failure. |

### Safe versus enhanced responses

- `safe` mode suppresses protected fields such as `body_preview`, `sender_name`, and `sender_email`.
- `enhanced` mode keeps sanitized/truncated previews and should only be enabled after operator validation.

## Install, Register, Test, And Uninstall Scripts

| Script | Purpose | Notes |
| --- | --- | --- |
| [`scripts/install-mailbridge.ps1`](./scripts/install-mailbridge.ps1) | Creates the install/config directories, seeds default config, performs Outlook preflight checks, registers the scheduled task, and smoke-checks client status. | Intended for installed binaries under `C:\Program Files\OpenClaw\MailBridge`. |
| [`scripts/register-mailbridge-task.ps1`](./scripts/register-mailbridge-task.ps1) | Registers an interactive `schtasks` on-logon entry for the primary user. | Starts the task immediately if that user is already logged on. |
| [`scripts/test-mailbridge.ps1`](./scripts/test-mailbridge.ps1) | Runs the scripted acceptance suites against an installed bridge. | Covers readiness, cache-backed message/event reads, safe-mode privacy checks, operator evidence keys, and repeated request hygiene. |
| [`scripts/uninstall-mailbridge.ps1`](./scripts/uninstall-mailbridge.ps1) | Stops and deletes the scheduled task. | Leaves user data/config in place. |
| [`scripts/Run-Bridge.ps1`](./scripts/Run-Bridge.ps1) | Runs the bridge project in `Development`. | Good for local debugging. |
| [`scripts/Run-Client.ps1`](./scripts/Run-Client.ps1) | Convenience client runner. | Still reflects an older CLI shape and should be updated before relying on it. |
| [`scripts/Build.ps1`](./scripts/Build.ps1) | Builds the solution. | Thin wrapper over `dotnet build`. |
| [`scripts/Test.ps1`](./scripts/Test.ps1) | Runs the solution tests. | Thin wrapper over `dotnet test`. |

## Security And Operational Notes

- The bridge is designed for local machine use through a named pipe, not as a network service.
- The named-pipe ACL explicitly grants access to `SYSTEM`, Administrators, the current user, and `openclaw-svc`, while denying the `NETWORK` SID. Pipe startup fails if required identities cannot be resolved.
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
- Operator runbook: [`docs/mailbridge-runbook.md`](./docs/mailbridge-runbook.md) for install steps, `safe` versus `enhanced` guidance, scripted acceptance suites, and operator-only validation steps.
- Active feature and remediation docs: [`docs/features/active/`](./docs/features/active/)

## MSIX Installation

The MSIX package bundles the bridge host, client CLI, and all runtime dependencies into a single signed Windows
installer that auto-starts the bridge at user logon via a `windows.startupTask` extension.

### One-time dev certificate setup (elevated PowerShell required)

Run this once per developer machine to create a self-signed code-signing cert and install it in the trusted-root store:

```powershell
# Run as Administrator
$pwd = ConvertTo-SecureString 'your-password' -AsPlainText -Force
.\scripts\New-MsixDevCert.ps1 -PfxPassword $pwd -OutputDir artifacts
```

This writes `artifacts/OpenClaw.MailBridge.pfx` and `artifacts/OpenClaw.MailBridge.cer` and installs the CER into
`Cert:\LocalMachine\Root` so Windows accepts the signed package.

### Build the MSIX package

First publish both projects, then pack:

```powershell
dotnet publish src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj /p:PublishProfile=msix
dotnet publish src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj /p:PublishProfile=msix

# Sign and pack (replace THUMBPRINT with the value from New-MsixDevCert.ps1)
.\scripts\build-msix.ps1 -Version '1.0.0.0' -CertThumbprint 'THUMBPRINT'
```

The `.msix` file is written to `artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix`.

### Install

```powershell
Add-AppxPackage -Path artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix
```

The bridge is registered as a startup task (`TaskId = OpenClawMailBridge`) and will start automatically at the next
user logon.

### Upgrade

Build a newer version and run `Add-AppxPackage` again:

```powershell
Add-AppxPackage -Path artifacts/msix/OpenClaw.MailBridge_1.1.0.0_x64.msix
```

Windows replaces the installed version in place.

### Uninstall

```powershell
Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage
```

## Known Gaps And Follow-Ups

- Real Outlook/operator acceptance still requires a Windows machine with classic Outlook, an interactive user session, and a provisioned `openclaw-svc` identity.
- The definitive operator guidance now lives in [`docs/mailbridge-runbook.md`](./docs/mailbridge-runbook.md), including the scripted/operator validation split.
