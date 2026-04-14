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

Privilege note:

- `C:\Program Files\OpenClaw\MailBridge` is an administrator-protected location. If you publish there, run step 1 from an elevated PowerShell session.
- A regular user session can only use step 1 successfully if `-o` targets a user-writable directory instead of `C:\Program Files\...`.

Expected result:

- `C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.exe`
- `C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe`
- both matching `*.runtimeconfig.json` files

### 2. Run the install helper

Before running the install helper, resolve the actual primary user and target profile for the bridge task:

```powershell
whoami
$env:USERPROFILE
$env:LOCALAPPDATA
```

Use the `whoami` value as `-PrimaryUser`. For example, if `whoami` returns `WORKSTATION\dan`, pass `-PrimaryUser 'WORKSTATION\dan'`.

Current execution guidance:

- A regular non-elevated prompt is the preferred context for Outlook profile preflight, but scheduled-task creation may fail with `Access is denied`.
- An elevated prompt can register the scheduled task, but the current scripts derive the bridge config path from the caller's `%LOCALAPPDATA%`. In an elevated shell, that can point to the administrator profile instead of the target operator profile and lead to `Bridge status preflight failed after registration.`

Current elevated-shell workaround:

```powershell
$PrimaryUser = 'WORKSTATION\dan'
$TargetProfile = 'C:\Users\dan'
$env:LOCALAPPDATA = Join-Path $TargetProfile 'AppData\Local'

.\scripts\install-mailbridge.ps1 -PrimaryUser $PrimaryUser -InstallRoot 'C:\Program Files\OpenClaw\MailBridge'
```

Replace both values with the actual target account and profile path for the interactive Outlook user.

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

If step 2 fails, verify the scheduled task and the client command separately:

```powershell
schtasks /query /tn "OpenClaw MailBridge" /v /fo list
& "C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe" status
```

In PowerShell, the client path must be invoked with `&` because `Program Files` contains a space.

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

Implementation note:

- The task command currently passes `--config "<caller LOCALAPPDATA>\OpenClaw\MailBridge\bridge.settings.json"`.
- If you register the task from an elevated shell without first aligning `%LOCALAPPDATA%` to the target user's profile, the task can be created successfully but still start the bridge with the wrong settings path.

### 5. Remove the scheduled-task deployment

```powershell
.\scripts\uninstall-mailbridge.ps1
```

This removes the scheduled task only. It leaves settings, cache, and logs in place.

## Install Path B: MSIX Package

This path installs the bridge and client together and uses an MSIX `windows.startupTask` so the bridge starts at user logon.

### 1. Set session variables once

```powershell
$repoRoot = '<repo-root>'
$pwdText = 'your-password'
```

Step 1 notes:

- Set both values once at the beginning of the session, then reuse them for the remaining MSIX steps.
- `$repoRoot` should point to the workspace root for this repository.
- `$pwdText` is temporary plain text. Clear it after converting it to a `SecureString`.

### 2. Create a development signing certificate once per machine

```powershell
Set-Location $repoRoot
$pwd = ConvertTo-SecureString $pwdText -AsPlainText -Force
Remove-Variable pwdText -ErrorAction SilentlyContinue
$thumbprint = .\scripts\New-MsixDevCert.ps1 -PfxPassword $pwd -OutputDir artifacts 6>$null
```

Step 2 notes:

- Run the command from the workspace root so the relative `.\scripts\...` and `artifacts\...` paths resolve correctly.
- `$pwd` is derived from `$pwdText`, then `$pwdText` is deleted so the plain-text password does not remain in the session unnecessarily.
- `New-MsixDevCert.ps1` returns the certificate thumbprint. Capturing it into `$thumbprint` avoids manual copy and keeps the value available for step 3.
- `6>$null` suppresses the informational output stream so the thumbprint is stored in the variable rather than mixed with screen output.

Expected artifacts:

- `artifacts/OpenClaw.MailBridge.pfx`
- `artifacts/OpenClaw.MailBridge.cer`

The certificate script installs the CER into the trusted root store so the local package is accepted by Windows.

### 3. Publish and build the package

```powershell
Set-Location $repoRoot
dotnet publish .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj /p:PublishProfile=msix
dotnet publish .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj /p:PublishProfile=msix
.\scripts\build-msix.ps1 -Version '1.0.0.0' -CertThumbprint $thumbprint
Remove-Variable thumbprint -ErrorAction SilentlyContinue
```

Step 3 notes:

- Step 3 assumes `$thumbprint` was created in the current PowerShell session by step 2.
- `Remove-Variable thumbprint` clears the temporary variable after signing if you do not need it again in the session.

Expected artifact:

- `artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix`

### 4. Install or upgrade

```powershell
Add-AppxPackage -Path .\artifacts\msix\OpenClaw.MailBridge_1.0.0.0_x64.msix
```

MSIX behavior:

- installs both executables under the Windows Apps package location
- registers the `OpenClawMailBridge` startup task
- preserves `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` across upgrade and uninstall

### 5. Verify package registration and bridge startup

Confirm that the package is installed and the expected payload files exist:

```powershell
$pkg = Get-AppxPackage -Name 'OpenClaw.MailBridge'
$pkg.InstallLocation
Test-Path (Join-Path $pkg.InstallLocation 'bridge\OpenClaw.MailBridge.exe')
Test-Path (Join-Path $pkg.InstallLocation 'client\OpenClaw.MailBridge.Client.exe')
```

Then sign out and sign back in so the MSIX `windows.startupTask` can launch the bridge in the interactive user session. After signing back in, verify the bridge indirectly:

```powershell
Get-Process OpenClaw.MailBridge -ErrorAction SilentlyContinue
Get-ChildItem "$env:LOCALAPPDATA\OpenClaw\MailBridge\logs" -ErrorAction SilentlyContinue
Get-Content "$env:LOCALAPPDATA\OpenClaw\MailBridge\logs\bridge.log" -Tail 50 -ErrorAction SilentlyContinue
```

MSIX validation note:

- The current package manifest exposes only `OpenClaw.MailBridge` as an application entry point.
- Do not attempt to run `client\OpenClaw.MailBridge.Client.exe` directly from `%ProgramFiles%\WindowsApps\...` for Path B validation. In the current package shape, direct execution from the package install location returns `Access is denied`.
- If terminal-invokable client access is required from an MSIX-only install, the package needs an additional supported exposure mechanism in a future change.

### 6. Remove the package

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

Privilege guidance:

- Step C.1 may require an elevated PowerShell session because it writes under `C:\ProgramData\OpenClaw\HostAdapter`.
- Steps C.2 and C.3 should normally run in the regular interactive user session after the files already exist.
- Prefer the normal user session for runtime operations unless local policy explicitly requires otherwise. This keeps Docker Desktop access, `%LOCALAPPDATA%`, and other user-session behavior aligned with the operator account.

### 1. Provision HostAdapter configuration on Windows

Create the configuration directory and token file:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\ProgramData\OpenClaw\HostAdapter' | Out-Null
Set-Content -Path 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Value 'replace-with-a-long-random-token'
```

Token guidance:

- Replace `replace-with-a-long-random-token` with a real secret value and keep it.
- This token is not a one-time placeholder. It is the bearer token that the HostAdapter expects and that `OpenClaw.Core` later reads through the configured token-file bind mount.
- Save the token in `C:\ProgramData\OpenClaw\HostAdapter\adapter.token` and keep that file available. If you regenerate the token later, update the file before restarting HostAdapter or Core.
- Treat the token file as a secret. Limit its ACLs to the intended local operator or service context.

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

Step C.2 note:

- Run this from the normal interactive user session unless machine policy requires otherwise.

Validation:

```powershell
$token = (Get-Content 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Raw).Trim()
curl.exe -H "Authorization: Bearer $token" http://127.0.0.1:4319/v1/status
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

Step C.3 note:

- Run Docker Desktop and the `docker compose` command from the normal user session that owns Docker Desktop access.

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
& "C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe" status
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
| `Access is denied` during `install-mailbridge.ps1` or `register-mailbridge-task.ps1` | The session does not have enough rights to create or update the scheduled task | Rerun the command from an elevated PowerShell session, and use the real `whoami` value for `-PrimaryUser`. |
| `Bridge status preflight failed after registration.` | The task was created but the bridge did not start correctly, often because the task points `--config` at the wrong `%LOCALAPPDATA%` path | In the elevated shell, set `$env:LOCALAPPDATA` to the target user's `AppData\Local`, then rerun `install-mailbridge.ps1`. Afterward, inspect `schtasks /query /tn "OpenClaw MailBridge" /v /fo list`, the bridge log, and the manual client `status` command. |
| Install helper reports wrong framework | Published output is not targeting `.NET 10` runtimeconfig files | Republish both host and client, then rerun `install-mailbridge.ps1`. |
| `Bridge executable not found` or `Client executable not found` | Install folder is incomplete | Republish both projects into the same install directory. |
| `Access is denied` when running `client\OpenClaw.MailBridge.Client.exe` from `%ProgramFiles%\WindowsApps\...` after MSIX install | The package is installed, but the current manifest does not expose the packaged client as a terminal-invokable entry point | Treat the MSIX install as successful, sign out and back in so the startup task can launch the bridge, and validate the bridge through process and log checks instead of direct client execution. |
| PowerShell reports that `C:\Program` is not recognized when running the client executable | The executable path contains a space and was entered without the PowerShell call operator | Run `& "C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe" status` instead of entering the path bare. |
| HostAdapter returns `401` | Bearer token missing or invalid | Read the expected token from the configured token file and retry. |
| HostAdapter cannot start | Missing or empty token file, or incorrect client path | Recreate `adapter.token` and verify `ClientExecutablePath` in `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json`. |
| HostAdapter or Core stops working after the token file was edited or replaced | The bearer token changed and one or both components are still using the old value | Update `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`, then restart HostAdapter and restart the Core container so both sides read the same current token. |
| Docker readiness returns `503` | SQLite not initialized or HostAdapter unreachable | Check the container logs, confirm `host.docker.internal` resolves, and verify the HostAdapter is running on `127.0.0.1:4319`. |
| Empty calendar result set | Request window is outside the cached calendar range | Confirm `calendarPastDays` and `calendarFutureDays`; empty results are expected outside the cached window. |
| Safe mode seems to hide sender or preview fields | Bridge is operating as designed | Keep `safe` mode if privacy is required, or switch to `enhanced` only after operator approval. |
