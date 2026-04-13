# OpenClaw MailBridge Runbook

## Purpose

This runbook describes how to install, validate, operate, and remove the current OpenClaw MailBridge deployment modes:

1. Windows published binaries plus scheduled-task registration
2. Windows MSIX package install
3. Optional additive `OpenClaw.HostAdapter` plus Docker `OpenClaw.Core`

The bridge remains a local-only, read-only Outlook integration. Outlook COM access stays on the Windows host and in the interactive user session.

## Operational Model

- `OpenClaw.MailBridge` scans the default Outlook Inbox and Calendar on a dedicated STA thread.
- The bridge caches normalized message and event metadata in `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db`.
- `OpenClaw.MailBridge.Client.exe` connects over the configured named pipe and returns JSON responses.
- `OpenClaw.HostAdapter` is optional. It exposes authenticated HTTP routes on the Windows host by shelling out to the client CLI.
- `OpenClaw.Core` is optional. It runs in Docker Desktop, polls the HostAdapter, stores its own SQLite cache at `/data/openclaw.db`, and serves a local UI plus internal API on loopback only.

## Prerequisites

### Windows bridge prerequisites

- Windows 10 or Windows 11
- Classic Outlook installed and available through COM
- A configured Outlook profile with the default Inbox and Calendar present
- .NET 10 runtime if you are using the published-binary path
- An interactive user session for the primary operator

### Additive Docker path prerequisites

- Docker Desktop
- A Windows bridge installation that is already working
- A HostAdapter bearer token file on the Windows host

## Install Path A: Published Binaries Plus Scheduled Task

This is the repository's script-driven install path.

### 1. Publish the host and client into one install folder

```powershell
dotnet publish .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj -c Release -o "C:\Program Files\OpenClaw\MailBridge"
dotnet publish .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj -c Release -o "C:\Program Files\OpenClaw\MailBridge"
```

Expected result:

- `C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.exe`
- `C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe`
- both matching `*.runtimeconfig.json` files

### 2. Run the install helper

```powershell
.\scripts\install-mailbridge.ps1 -PrimaryUser 'DOMAIN\User' -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

What the install helper does:

- creates `%LOCALAPPDATA%\OpenClaw\MailBridge\` and `%LOCALAPPDATA%\OpenClaw\MailBridge\logs\`
- seeds `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` if it does not exist
- validates that the installed host and client runtimeconfig files require `Microsoft.NETCore.App 10.x`
- validates Outlook COM, profile, Inbox, and Calendar prerequisites when not running elevated
- registers an interactive scheduled task for the primary user
- starts the task immediately if that user is already logged on
- waits for the client `status` command to return JSON

If the script is run from an elevated shell, Outlook profile validation is intentionally skipped because MAPI profile access can differ in that context.

### 3. Validate the install

Run the scripted acceptance suites:

```powershell
.\scripts\test-mailbridge.ps1 -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

This validates:

- bridge readiness and status
- cache-backed message list and detail reads
- cache-backed calendar list and detail reads
- safe-mode privacy behavior
- repeated-request hygiene
- installed runtime framework evidence for both host and client

### 4. Task registration details

The scheduled-task deployment uses:

- `/sc onlogon`
- `/it`
- the configured `PrimaryUser`

If you need to re-register the task without reinstalling:

```powershell
.\scripts\register-mailbridge-task.ps1 -PrimaryUser 'DOMAIN\User' -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

### 5. Remove the scheduled-task deployment

```powershell
.\scripts\uninstall-mailbridge.ps1
```

This removes the scheduled task only. It leaves settings, cache, and logs in place.

## Install Path B: MSIX Package

This path installs the bridge and client together and uses an MSIX `windows.startupTask` so the bridge starts at user logon.

### 1. Create a development signing certificate once per machine

```powershell
$pwd = ConvertTo-SecureString 'your-password' -AsPlainText -Force
.\scripts\New-MsixDevCert.ps1 -PfxPassword $pwd -OutputDir artifacts
```

Expected artifacts:

- `artifacts/OpenClaw.MailBridge.pfx`
- `artifacts/OpenClaw.MailBridge.cer`

The certificate script installs the CER into the trusted root store so the local package is accepted by Windows.

### 2. Publish and build the package

```powershell
dotnet publish .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj /p:PublishProfile=msix
dotnet publish .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj /p:PublishProfile=msix
.\scripts\build-msix.ps1 -Version '1.0.0.0' -CertThumbprint 'THUMBPRINT'
```

Expected artifact:

- `artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix`

### 3. Install or upgrade

```powershell
Add-AppxPackage -Path .\artifacts\msix\OpenClaw.MailBridge_1.0.0.0_x64.msix
```

MSIX behavior:

- installs both executables under the Windows Apps package location
- registers the `OpenClawMailBridge` startup task
- preserves `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` across upgrade and uninstall

### 4. Remove the package

```powershell
Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage
```

Operational limitation:

- the startup task launches the bridge on user logon
- it does not restart the bridge automatically after a crash

## Configure The Bridge

Settings file:

```text
%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json
```

Default content:

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

Operating guidance:

- keep `mode` set to `safe` until both scripted acceptance and operator validation are complete
- switch to `enhanced` only if protected-field exposure is acceptable in the target environment
- treat `pipeName` as the shared bridge-client contract; if you change it, update any callers that override pipe resolution

## Install Path C: Additive HostAdapter Plus Docker Core

This path is optional and depends on a working Windows bridge installation.

### 1. Provision HostAdapter configuration on Windows

Create the configuration directory and token file:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\ProgramData\OpenClaw\HostAdapter' | Out-Null
Set-Content -Path 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Value 'replace-with-a-long-random-token'
```

Create `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json`:

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

### 2. Start the HostAdapter on the Windows host

```powershell
$env:ASPNETCORE_URLS = 'http://127.0.0.1:4319'
dotnet run --project .\src\OpenClaw.HostAdapter\OpenClaw.HostAdapter.csproj --configuration Release
```

Validation:

```powershell
curl.exe -H "Authorization: Bearer $(Get-Content 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Raw).Trim()" http://127.0.0.1:4319/v1/status
```

Expected result:

- HTTP `200`
- an `ApiEnvelope<BridgeStatusDto>` payload

### 3. Start `OpenClaw.Core` in Docker Desktop

Copy `.env.example` to `.env` and confirm these values:

- `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319/v1`
- `HOSTADAPTER_TOKEN_FILE=C:\ProgramData\OpenClaw\HostAdapter\adapter.token`
- `OPENCLAW_HTTP_PORT=8080`

Start the container:

```powershell
docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up --build -d openclaw-core
```

Validate the container path:

```powershell
curl.exe http://127.0.0.1:8080/health/live
curl.exe http://127.0.0.1:8080/health/ready
curl.exe http://127.0.0.1:8080/api/status
```

Expected behavior:

- `/health/live` returns `200` when the app is running
- `/health/ready` returns `200` only when SQLite is ready and the HostAdapter is reachable
- `/api/status` reports cache counts, bridge freshness, and poll timestamps

### 4. Fallback behavior

If the HostAdapter or Docker path is unavailable, continue using:

```powershell
C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe status
```

The Windows bridge and client remain the canonical fallback path for troubleshooting.

## Scripted Acceptance Evidence

Run:

```powershell
.\scripts\test-mailbridge.ps1 -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

The script records:

- `PublishedBridgeTargetFramework`
- `PublishedClientTargetFramework`
- `BridgeRuntimeFramework`
- `ClientRuntimeFramework`
- `PrimaryInteractiveSession`
- `OpenClawSvcPipeConnect`
- `NetworkDenyVerified`

The script reports `AutomatedSuitesPassed: A,B,C,D,F` when the automated acceptance suites complete successfully.

## Operator-Only Validation

Record these checks separately after the scripted suites pass:

1. Confirm the bridge is running in the primary interactive user session.
2. Confirm classic Outlook is using the intended profile and default Inbox and Calendar.
3. Confirm `openclaw-svc` can connect to the pipe if that identity is part of the target environment.
4. Confirm the pipe ACL denies the `NETWORK` SID.
5. Confirm the HostAdapter token file ACL is limited to the intended local operator path.
6. If using Docker, confirm the published UI/API endpoint remains loopback-only.

## Troubleshooting

| Symptom | Likely cause | Corrective action |
| --- | --- | --- |
| `waiting_for_outlook` | Outlook is not running and the bridge cannot connect | Start classic Outlook or confirm `autostartOutlook` is enabled. |
| `degraded` with stale cache | Outlook scan failed or the running instance became unavailable | Review bridge logs, confirm Outlook profile health, and rerun acceptance after Outlook stabilizes. |
| Install helper reports wrong framework | Published output is not targeting `.NET 10` runtimeconfig files | Republish both host and client, then rerun `install-mailbridge.ps1`. |
| `Bridge executable not found` or `Client executable not found` | Install folder is incomplete | Republish both projects into the same install directory. |
| HostAdapter returns `401` | Bearer token missing or invalid | Read the expected token from the configured token file and retry. |
| HostAdapter cannot start | Missing or empty token file, or incorrect client path | Recreate `adapter.token` and verify `ClientExecutablePath` in `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json`. |
| Docker readiness returns `503` | SQLite not initialized or HostAdapter unreachable | Check the container logs, confirm `host.docker.internal` resolves, and verify the HostAdapter is running on `127.0.0.1:4319`. |
| Empty calendar result set | Request window is outside the cached calendar range | Confirm `calendarPastDays` and `calendarFutureDays`; empty results are expected outside the cached window. |
| Safe mode seems to hide sender or preview fields | Bridge is operating as designed | Keep `safe` mode if privacy is required, or switch to `enhanced` only after operator approval. |
