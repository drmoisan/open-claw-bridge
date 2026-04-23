# OpenClaw MailBridge

OpenClaw MailBridge is a Windows-first .NET solution that reads Outlook data locally, caches normalized message and calendar metadata in SQLite, and exposes that cached data through a local named-pipe RPC interface.

The repository also contains the local-only HTTP and Docker components required for the complete OpenClaw solution:

- `OpenClaw.MailBridge` remains the Windows process that talks to Outlook over COM.
- `OpenClaw.MailBridge.Client` remains the canonical six-command client for local reads.
- `OpenClaw.HostAdapter` adds an authenticated HTTP layer on the Windows host by shelling out to the client.
- `OpenClaw.Core` runs in Docker Desktop, polls the HostAdapter, persists its own SQLite cache, and serves a local UI plus internal API on loopback only.

## What It Does

- Scans the default Outlook Inbox and Calendar on a dedicated STA thread.
- Stores cached message, meeting-request, and event metadata in `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db`.
- Serves cache-backed read-only responses over a named pipe.
- Keeps `safe` mode as the default response-shaping mode.
- Supports two Windows installation paths today:
  - published binaries plus `install-mailbridge.ps1`
  - MSIX package install
- Supports a scripted bundle install path on top of the above that consumes
  an `artifacts/publish/<version>/` bundle. Run `.\Install.ps1` from inside
  the bundle directory (for example `cd artifacts/publish/<version>;
  .\Install.ps1`). The install scripts ship INSIDE the bundle and self-locate
  via `$PSScriptRoot`:
  - `Install.ps1` unpacks the bundle to `%LOCALAPPDATA%\OpenClaw\<version>\`,
    installs the MSIX, starts the `openclaw-core` and `openclaw-agent`
    compose stack, and writes a single-record install manifest for later
    rollback
  - operator-managed Docker env files stay outside `artifacts/publish/<version>`
    and can be supplied with `-DockerEnvFilePath` and `-AnthropicEnvFilePath`
  - `Uninstall.ps1` reads the install record and reverses the install
- Supports a complete local solution path beyond the bridge transport:
  - `OpenClaw.HostAdapter` on Windows, `OpenClaw.Core` in Docker Desktop, and the `openclaw-agent` assistant service

## How It Works

1. `OpenClaw.MailBridge` starts in the interactive Windows user session.
2. `ScanWorker` invokes `OutlookScanner` on a dedicated STA executor so Outlook COM calls stay on one thread.
3. The bridge caches normalized message and event data in SQLite and records bridge state.
4. `PipeRpcWorker` listens on a local named pipe and returns cached responses for the supported RPC methods.
5. `OpenClaw.MailBridge.Client` resolves the configured pipe name, sends a JSON RPC request, and writes the bridge response to stdout.
6. For the complete OpenClaw solution, `OpenClaw.HostAdapter` shells out to `OpenClaw.MailBridge.Client`, `OpenClaw.Core` polls the HostAdapter from Docker, and the assistant service consumes the same HostAdapter HTTP API.

## Repository Layout

| Path | Purpose |
| --- | --- |
| `src/OpenClaw.MailBridge/` | Windows bridge host and runtime services. |
| `src/OpenClaw.MailBridge.Client/` | Named-pipe client CLI. |
| `src/OpenClaw.MailBridge.Contracts/` | Shared DTOs, error codes, settings, validation, and sanitization helpers. |
| `src/OpenClaw.HostAdapter/` | Windows-host authenticated HTTP adapter that shells out to the client CLI. |
| `src/OpenClaw.HostAdapter.Contracts/` | Shared HTTP envelope types and typed HostAdapter client contract. |
| `src/OpenClaw.Core/` | Local-only ASP.NET Core UI and API with its own SQLite cache. |
| `tests/` | MSTest coverage for the bridge, HostAdapter, and Core, plus Pester coverage for scripts. |
| `scripts/` | Build, test, publish (`Publish.ps1`), scripted bundle install and uninstall (`Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1`; these three are additionally staged into every bundle by `Publish.ps1` and are intended to run from the bundle root via `$PSScriptRoot`, not from the repo `scripts/` directory), scheduled-task install (`install-mailbridge.ps1`, `uninstall-mailbridge.ps1`), MSIX, and acceptance helpers. |
| `installer/` | MSIX manifest and package assets. |
| `deploy/docker/` | Docker assets for `OpenClaw.Core`. |
| `docs/` | Operator runbook, API reference, architecture diagrams, and feature records. |

## Prerequisites

### Runtime

- Windows 10 or Windows 11 for the bridge runtime.
- Classic Outlook installed and available through COM.
- An interactive user session. Outlook COM access is tied to the logged-in user session.

### Development

- .NET SDK `10.0.201` as pinned in [`global.json`](./global.json).
- PowerShell 5.1 or PowerShell 7 for repository scripts.
- Docker Desktop if you want to run `OpenClaw.Core`.
- `csharpier` if you are making C# changes.

## Build And Test

Use the convenience scripts:

```powershell
./scripts/Build.ps1
./scripts/Test.ps1
```

Or run the direct commands:

```powershell
dotnet restore .\OpenClaw.MailBridge.sln
dotnet build .\OpenClaw.MailBridge.sln -c Debug
dotnet test .\OpenClaw.MailBridge.sln -c Debug
```

For restore-only work on non-Windows hosts:

```powershell
dotnet restore .\OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
```

## Run From Source

Start the bridge:

```powershell
dotnet run --project .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj --configuration Debug
```

Query it from the client:

```powershell
dotnet run --project .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj --configuration Debug -- status
```

The bridge creates `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` on first run if it does not already exist.

## Install On Windows With Published Binaries

This is the repository's script-driven install path. It publishes the host and client to a shared install folder, seeds the per-user config file, validates Outlook and runtime prerequisites, registers an interactive scheduled task, and smoke-checks the installed bridge.

1. Publish both executables to the same install directory:

```powershell
dotnet publish .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj -c Release -o "C:\Program Files\OpenClaw\MailBridge"
dotnet publish .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj -c Release -o "C:\Program Files\OpenClaw\MailBridge"
```

2. Run the install helper:

```powershell
.\scripts\install-mailbridge.ps1 -PrimaryUser 'DOMAIN\User' -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

3. Run the scripted acceptance suites:

```powershell
.\scripts\test-mailbridge.ps1 -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

Important behavior:

- The install script seeds `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` with `safe` mode defaults.
- The runtimeconfig files for both the host and client must require `.NET 10`, or install preflight fails.
- The scheduled task is registered with `/sc onlogon /it`, so the bridge runs in the primary interactive user session.
- If you run the install script in an elevated shell, the Outlook profile preflight is skipped. Validate Outlook prerequisites from the primary interactive user session before treating the install as complete.

To remove the scheduled-task deployment:

```powershell
.\scripts\uninstall-mailbridge.ps1
```

## Build And Install The MSIX Package

The repository also contains an MSIX packaging path that installs the bridge and client together and starts the bridge on user logon through a `windows.startupTask`.

Create and trust a development signing certificate once per machine:

```powershell
$pwd = ConvertTo-SecureString 'your-password' -AsPlainText -Force
.\scripts\New-MsixDevCert.ps1 -PfxPassword $pwd -OutputDir artifacts
```

Publish a full release bundle with `scripts/Publish.ps1`. The unified entry
point publishes every runnable `src/` project, copies the docker artifact
set, builds (and optionally signs) the MSIX, and writes a top-level
`manifest.json` that enumerates every file in the bundle with its size and
SHA-256 hash. The output is written to `artifacts/publish/<version>/`.

Signed release build:

```powershell
.\scripts\Publish.ps1 -Version '1.0.0.0' -CertThumbprint 'THUMBPRINT'
```

Dev (unsigned) build:

```powershell
.\scripts\Publish.ps1 -Version '1.0.0.0' -SkipSign
```

Supported parameters:

- `-Version` — mandatory 4-part version string (for example `1.2.3.0`).
  Strict validation via `ValidatePattern`; 3-part inputs are rejected.
- `-OutputDir` — root directory for the bundle. Default: `artifacts/publish`.
- `-Configuration` — `Debug` or `Release`. Default: `Release`.
- `-CertThumbprint` — SHA-1 thumbprint of the code-signing certificate in
  `Cert:\CurrentUser\My`. Required unless `-SkipSign` is supplied.
- `-SkipSign` — switch; when present the MSIX is packed without signing.

Install or upgrade the generated package:

```powershell
Add-AppxPackage -Path .\artifacts\publish\1.0.0.0\msix\OpenClaw.MailBridge_1.0.0.0_x64.msix
```

Remove it:

```powershell
Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage
```

Notes:

- The MSIX package does not replace the PowerShell install path. Both deployment models are supported.
- The MSIX installs only `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client`.
- The MSIX does not install or configure `OpenClaw.HostAdapter`, `OpenClaw.Core`, Docker assets, or the `openclaw-agent` assistant service.
- If you are setting up the complete OpenClaw solution, the remaining components below are additional required steps after the bridge or MSIX install completes.
- MSIX install, upgrade, and uninstall preserve `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`.
- The startup task runs on user logon. It does not provide automatic crash restart.

## Complete Solution Setup After Bridge Or MSIX Install

The bridge install is only the first stage. To run the complete OpenClaw solution described in this repository, keep the Windows bridge installed and running, then complete the HostAdapter, `OpenClaw.Core`, and assistant setup below.

### 1. Provision the HostAdapter token and config

Create the token file:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\ProgramData\OpenClaw\HostAdapter' | Out-Null
Set-Content -Path 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Value 'replace-with-a-long-random-token'
```

Create `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json` so the adapter can locate the installed client executable:

```json
{
  "OpenClaw": {
    "HostAdapter": {
      "TokenFilePath": "C:\\ProgramData\\OpenClaw\\HostAdapter\\adapter.token",
      "ClientExecutablePath": "C:\\Program Files\\OpenClaw\\MailBridge\\OpenClaw.MailBridge.Client.exe",
      "DefaultLimit": 100,
      "MaxLimit": 250
    }
  }
}
```

### 2. Start the HostAdapter on Windows

```powershell
$env:ASPNETCORE_URLS = 'http://127.0.0.1:4319'
dotnet run --project .\src\OpenClaw.HostAdapter\OpenClaw.HostAdapter.csproj --configuration Release
```

You can also publish the HostAdapter and run the published executable instead. The default external config path remains `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json`.

### 3. Start the OpenClaw container stack with Docker Desktop

Copy `.env.example` to `.env`, then update at least `HOSTADAPTER_TOKEN_FILE` if your token file lives somewhere else.

Start the default local stack:

```powershell
docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up --build -d openclaw-core openclaw-agent
```

Open the local UI:

```text
http://127.0.0.1:8080
```

Validate the local-only path:

```powershell
$token = (Get-Content 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Raw).Trim()
curl.exe -H "Authorization: Bearer $token" http://127.0.0.1:4319/v1/status
curl.exe http://127.0.0.1:8080/health/ready
curl.exe http://127.0.0.1:8080/api/status
```

Operational notes:

- `OpenClaw.Core` publishes only to `127.0.0.1:${OPENCLAW_HTTP_PORT:-8080}`.
- The container keeps its own SQLite database at `/data/openclaw.db`.
- If the HostAdapter or container path is unavailable, use `OpenClaw.MailBridge.Client` directly on the Windows host as the fallback troubleshooting path.

### 4. Manage the OpenClaw Agent (Required)

The container stack includes `openclaw-agent`, a required peer service that provides the operator dashboard and consumes the HostAdapter HTTP API alongside `openclaw-core`. The agent provides AI-powered triage, summarization, and scheduling analysis of mail and calendar data.

**Naming distinction:** `OpenClaw.Core` is the repository-owned UI and cache container. `openclaw-agent` is the external OpenClaw assistant runtime. Both are required when the container stack is deployed; they independently consume the HostAdapter API.

The agent image is built locally from the upstream `ghcr.io/openclaw/openclaw:latest` runtime (published at the [GitHub Container Registry](https://github.com/openclaw/openclaw/pkgs/container/openclaw)). Set `OPENCLAW_AGENT_IMAGE` in your `.env` file if you wish to pin the upstream base image to a specific version tag.

**First-run onboarding.** Before starting the stack the first time, run `scripts/Invoke-OpenClawAgentOnboarding.ps1`. The script executes the upstream `openclaw onboard` command inside a throwaway container, captures the generated `OPENCLAW_GATEWAY_TOKEN`, and writes it to the repository-root `.env`. The dashboard will not accept any other credential; no placeholder default is shipped.

```powershell
pwsh -NoProfile -File scripts/Invoke-OpenClawAgentOnboarding.ps1
```

The onboarding script accepts an optional `-OnboardBinaryPath` parameter. Its default value `dist/index.js` matches the upstream onboarding binary location published in the GitHub Container Registry image at the time of this release. Supply `-OnboardBinaryPath <new-path>` only when the upstream image renames or relocates the entry-point binary (for example, after an upstream release that moves it to `openclaw.mjs`). The parameter accepts any path that is resolvable inside the `openclaw-agent` image.

The default stack startup command in Step 3 already brings up both `openclaw-core` and `openclaw-agent`. Use the commands below when you need to inspect or control the agent independently:

The agent workspace under `deploy/docker/openclaw-assistant/` is baked into the local wrapper image and copied into a Docker-managed `/workspace` volume on first start. The entrypoint script seeds workspace files only when they are absent, so onboarding state persists across container restarts.

Check agent service status:

```powershell
docker compose ps openclaw-agent
```

View agent logs:

```powershell
docker compose logs openclaw-agent
```

Stop only the agent without affecting `openclaw-core`:

```powershell
docker compose stop openclaw-agent
```

Validate the full container path (container health, endpoints, HostAdapter reachability from inside the container, token presence, dashboard auth):

```powershell
pwsh -NoProfile -File scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru
```

When `-CoreBaseUrl` is omitted, the validation script reads
`OPENCLAW_HTTP_PORT` from `-EnvFilePath` (default `./.env`). For example,
`OPENCLAW_HTTP_PORT=8081` validates Core at `http://127.0.0.1:8081`.

The dashboard at `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` authenticates against the `OPENCLAW_GATEWAY_TOKEN` in `.env` produced by the onboarding script. Open the URL after the container is healthy; the dashboard reads the token without an operator paste step.

Note: `OPENCLAW_AGENT_IMAGE` defaults to `ghcr.io/openclaw/openclaw:latest`. Pin to a specific version tag in `.env` for reproducible deployments of the local wrapper image.

## Bridge Configuration

The bridge settings file lives at `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`.

Default values:

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

Key behavior:

- `safe` is the default mode and redacts protected fields such as sender details and body previews.
- `enhanced` returns sanitized preview content and should only be enabled after operator validation.
- `pipeName` controls the named pipe used by the bridge and client.

## Canonical Client Commands

`OpenClaw.MailBridge.Client` is still the canonical transport interface and supports these commands:

- `status`
- `list-messages --since <utc> --limit <n>`
- `get-message --id <bridgeId>`
- `list-meeting-requests --since <utc> --limit <n>`
- `list-calendar --start <utc> --end <utc> --limit <n>`
- `get-event --id <bridgeId>`

The HostAdapter preserves this contract exactly and exposes it through:

- `GET /v1/status`
- `GET /v1/messages`
- `GET /v1/messages/{bridgeId}`
- `GET /v1/meeting-requests`
- `GET /v1/calendar`
- `GET /v1/events/{bridgeId}`

## Additional Documentation

- Operator runbook: [`docs/mailbridge-runbook.md`](./docs/mailbridge-runbook.md)
- API reference: [`docs/api-reference.md`](./docs/api-reference.md)
- Architecture diagrams: [`docs/architecture-diagrams.md`](./docs/architecture-diagrams.md)
- Active feature records: [`docs/features/active/`](./docs/features/active/)
