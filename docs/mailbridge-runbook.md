# OpenClaw MailBridge Runbook

## Purpose

This runbook describes how to install, validate, operate, and remove the current OpenClaw MailBridge deployment modes:

1. Windows published binaries plus scheduled-task registration
2. Windows MSIX package install
3. `OpenClaw.HostAdapter` plus Docker `OpenClaw.Core` and the required `openclaw-agent`

The bridge remains a local-only, read-only Outlook integration. Outlook COM access stays on the Windows host and in the interactive user session.

Release builds now use `scripts/Publish.ps1` as the supported package-build entry point. The retired standalone MSIX build entry point is no longer part of the in-repo release path.

## Operational Model

- `OpenClaw.MailBridge` scans the default Outlook Inbox and Calendar on a dedicated STA thread.
- The bridge caches normalized message and event metadata in `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db`.
- `OpenClaw.MailBridge.Client.exe` connects over the configured named pipe and returns JSON responses.
- `OpenClaw.HostAdapter` exposes authenticated HTTP routes on the Windows host by shelling out to the client CLI. It is a required peer of the agent container path.
- `OpenClaw.Core` runs in Docker Desktop, polls the HostAdapter, stores its own SQLite cache at `/data/openclaw.db`, and serves a local UI plus internal API on loopback only.
- `openclaw-agent` is a required peer service when the container stack is deployed. It provides the operator dashboard at `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` and the onboarding-produced gateway token is the only credential the dashboard accepts.

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

### Release bundle prerequisites

- PowerShell 7 or later
- .NET 10 SDK
- Windows 10 SDK tools available for the MSIX stage (`makeappx.exe`, `makepri.exe`, and `signtool.exe`)
- A signing certificate in `Cert:\CurrentUser\My` for signed builds, unless you intentionally pass `-SkipSign`

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

### 5. Temporarily stop or restart the bridge

End the currently running bridge instance without removing the scheduled task, settings, cache, or logs:

```powershell
schtasks /end /tn "OpenClaw MailBridge"
```

Prevent the bridge from starting automatically at the next interactive logon:

```powershell
Disable-ScheduledTask -TaskName "OpenClaw MailBridge"
```

Re-enable the task and start it immediately when resuming operation:

```powershell
Enable-ScheduledTask -TaskName "OpenClaw MailBridge"
schtasks /run /tn "OpenClaw MailBridge"
```

`schtasks /end` terminates only the running instance; the task remains registered and will start again on the next interactive logon unless it has also been disabled. `Disable-ScheduledTask` suppresses the logon trigger without removing the task. Neither command removes settings, cache, or logs.

### 6. Remove the scheduled-task deployment

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
$versionNum = '1.0.1.3'
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

The unified publish entry point `.\scripts\Publish.ps1` produces a single
versioned bundle under `artifacts/publish/<version>/`. The bundle contains
every runnable project's published output, the docker artifact set, the MSIX
installer, and a top-level `manifest.json`.

```powershell
Set-Location $repoRoot
.\scripts\Publish.ps1 -Version $versionNum -CertThumbprint $thumbprint
Remove-Variable thumbprint -ErrorAction SilentlyContinue
```

Step 3 notes:

- Step 3 assumes `$thumbprint` was created in the current PowerShell session by step 2.
- `Remove-Variable thumbprint` clears the temporary variable after signing if you do not need it again in the session.
- For an unsigned dev bundle, pass `-SkipSign` instead of `-CertThumbprint`.
- The `-Version` parameter is strictly validated against the 4-part pattern
  `^\d+\.\d+\.\d+\.\d+$`; 3-part inputs are rejected at parameter binding
  time rather than silently normalized.
- `-OutputDir` can override the bundle root. The default is
  `artifacts/publish`. If you override it, use the matching path in the
  install command.
- `-Configuration` accepts `Debug` or `Release`. The default is `Release`.
- The script removes and recreates `artifacts/publish/<version>/` before
  writing a new bundle for that version.
- `OpenClaw.Core` and `OpenClaw.HostAdapter` are published self-contained for
  `win-x64`. `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client` keep the
  MSIX publish-profile behavior used by the package installer.
- The docker stage copies deployment inputs only. It copies compose files,
  `deploy/docker/**`, and `.env.example` when present; it does not build or
  export docker images.
- `secrets/` paths are excluded from the docker bundle. The
  `deploy/docker/openclaw-assistant/` tree is copied verbatim, so review that
  content before distributing a bundle. The compose stack now bakes that tree
  into the assistant wrapper image instead of bind-mounting it from the host.
- `manifest.json` is written last. It lists each bundled file except itself
  with a forward-slash relative path, byte size, and SHA-256 hash.
- Binary hashes can differ between publish runs because the MSIX-bound
  projects retain ReadyToRun publishing. Treat the manifest as an integrity
  record for the produced bundle, not as proof that all binary outputs are
  byte-identical across separate builds.
- The script emits local artifacts only. It does not upload files, create a
  GitHub Release, or publish to a release server.

Migration note for operators who previously ran the separate `dotnet publish`
plus MSIX build recipe:

- Replace the separate `dotnet publish` calls and the prior MSIX-build script
  with a single `Publish.ps1` call. The new entry point publishes every
  runnable `src/` project (not just the MailBridge-side executables), copies
  the docker artifact set, and writes `manifest.json` enumerating every file
  in the bundle with size and SHA-256 hash.
- The MSIX output path changed from `artifacts/msix/` to
  `artifacts/publish/<version>/msix/`. Update any install scripts accordingly.

Expected bundle layout:

- `artifacts/publish/1.0.0.0/executables/OpenClaw.Core/`
- `artifacts/publish/1.0.0.0/executables/OpenClaw.HostAdapter/`
- `artifacts/publish/1.0.0.0/executables/OpenClaw.MailBridge/`
- `artifacts/publish/1.0.0.0/executables/OpenClaw.MailBridge.Client/`
- `artifacts/publish/1.0.0.0/docker/` - docker compose files, `.env.example`
  when present, and `deploy/docker/**`
- `artifacts/publish/1.0.0.0/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix` - MSIX installer
- `artifacts/publish/1.0.0.0/manifest.json` - full bundle manifest

### 4. Install or upgrade

```powershell
Add-AppxPackage -Path .\artifacts\publish\1.0.0.0\msix\OpenClaw.MailBridge_1.0.0.0_x64.msix
```

MSIX behavior:

- installs both executables under the Windows Apps package location
- registers the `OpenClawMailBridge` startup task
- preserves `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` across upgrade and uninstall

Version guidance:

- Use a new four-part package version for each rebuilt package you intend to
  install over an existing MSIX. Windows blocks installation when the installed
  package has the same identity and version but different contents.
- For local rebuild testing with the same version, remove the installed package
  first, then install the rebuilt `.msix`.

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

For the scripted bundle flow that wraps these steps together with docker compose, see Install Path D below.

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

This path depends on a working Windows bridge installation and completes the required container deployment topology (HostAdapter plus `openclaw-core` plus `openclaw-agent`).

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

### 3. Start the OpenClaw container stack in Docker Desktop

Copy `.env.example` to `.env` and confirm these values:

- `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319/v1`
- `HOSTADAPTER_TOKEN_FILE=C:\ProgramData\OpenClaw\HostAdapter\adapter.token`
- `OPENCLAW_HTTP_PORT=8081`

Start the default local stack:

```powershell
docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up --build -d openclaw-core openclaw-agent
```

Step C.3 note:

- Run Docker Desktop and the `docker compose` command from the normal user session that owns Docker Desktop access.

Validate the container path. The validation script runs the same endpoint checks, saves the responses into named result properties, and reports whether the observed responses are expected:

```powershell
.\scripts\Invoke-OpenClawContainerPathValidation.ps1
```

When `-CoreBaseUrl` is omitted, the validation script reads
`OPENCLAW_HTTP_PORT` from `-EnvFilePath` (default `./.env`). If `.env`
contains `OPENCLAW_HTTP_PORT=8081`, the Core probes target
`http://127.0.0.1:8081`.

Expected behavior:

- `OverallResult` is `Expected` only when every container and endpoint check returns the expected response.
- `DockerEngine` is expected when Docker is reachable and returns a server version.
- `CoreContainerExists` and `AgentContainerExists` are expected when Docker can inspect the expected containers.
- `CoreContainerRunning` and `AgentContainerRunning` are expected when the containers are in the `running` state.
- `CoreContainerHealthy` and `AgentContainerHealthy` are expected when Docker reports each container health status as `healthy`.
- `Live` is expected when `/health/live` returns `200` with JSON status `live`.
- `Ready` is expected when `/health/ready` returns `200` with JSON status `ready`, `sqliteReady=true`, and `hostAdapterReachable=true`.
- `CoreStatus` is expected when `/api/status` returns `200`, reports ready dependencies, and includes cache count and bridge freshness diagnostics.
- `AgentDashboard` is expected when `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` returns `200` with a response body.
- `AgentReadyz` is expected when `/readyz` returns `200`.
- `HostAdapterInContainer` is expected when `docker compose exec openclaw-agent` returns HTTP `200` from `http://host.docker.internal:4319/v1/status` with the bind-mounted bearer token.
- `GatewayTokenPresence` is expected when `OPENCLAW_GATEWAY_TOKEN` is present and non-empty in the target `.env`.
- If `OverallResult` is `Unexpected`, inspect the diagnostics table. For structured details, rerun the script with `-PassThru` and inspect `SupportingDiagnostics`.

The default stack command also starts `openclaw-agent`. Use `docker compose ps openclaw-agent`, `docker compose logs openclaw-agent`, or `docker compose stop openclaw-agent` only when you need to inspect or control the assistant independently.

### 4. Fallback behavior

If the HostAdapter or Docker path is unavailable, continue using:

```powershell
& "C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe" status
```

The Windows bridge and client remain the canonical fallback path for troubleshooting.

## Install Path D: Scripted Bundle Install

Use this path for a bundle produced by `scripts/Publish.ps1`. The bundle root
under `artifacts/publish/<version>/` is manifest-controlled. Do not add
machine-local files such as `docker/.env` or `docker/secrets/.env.anthropic` to
that directory.

Prepare version-neutral operator configuration outside the publish bundle:

```powershell
$operatorConfig = Join-Path $env:LOCALAPPDATA 'OpenClaw\operator-config'
New-Item -ItemType Directory -Force -Path (Join-Path $operatorConfig 'secrets') | Out-Null
Copy-Item (Join-Path $repoRoot ("artifacts\publish\{0}\docker\.env.example" -f $versionNum)) (Join-Path $operatorConfig '.env') -Force
notepad (Join-Path $operatorConfig '.env')
notepad (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

Configuration requirements:

- The operator `.env` file must contain the same Docker settings normally used
  by compose, including `OPENCLAW_GATEWAY_TOKEN` after onboarding.
- The Anthropic env file must contain `ANTHROPIC_API_KEY=<real key>`.
- Keep these files out of `artifacts/publish/<version>/`; the installer copies
  them into the installed Docker directory after manifest verification.
- When Docker is enabled, `Install.ps1` fails before MSIX installation if the
  installed Docker directory does not have `secrets/.env.anthropic`.

Before installing, confirm that both operator-managed files exist:

```powershell
Test-Path (Join-Path $operatorConfig '.env')
Test-Path (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

Both commands must return `True`. If either returns `False`, create or correct
the missing file before running `Install.ps1`.

Run the installer via its full path and pass the operator-managed files:

```powershell
$bundle = Join-Path $repoRoot ("artifacts\publish\{0}" -f $versionNum)
& (Join-Path $bundle 'Install.ps1') `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

If a prior attempt failed after creating `%LOCALAPPDATA%\OpenClaw\<version>\`,
rerun the installer with `-Force` and the same env-file parameters:

```powershell
& (Join-Path $bundle 'Install.ps1') -Force `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

If the Docker stage is intentionally deferred, run:

```powershell
& (Join-Path $bundle 'Install.ps1') -SkipDocker
```

Then place the operator env files under
`%LOCALAPPDATA%\OpenClaw\<version>\docker\` before starting compose manually.

## OpenClaw Agent (Required)

The repository supports an external OpenClaw assistant runtime (`openclaw-agent`) that provides AI-powered triage, summarization, and scheduling analysis of mail and calendar data. This service sits beside `openclaw-core` as a separate consumer of the HostAdapter HTTP API.

### Prerequisites

- Docker Desktop installed and running.
- A working HostAdapter with a valid token file (as configured in Install Path C above).
- `OPENCLAW_AGENT_IMAGE` set in `.env` to the verified upstream base image name from the OpenClaw platform documentation at `docs.openclaw.ai`.

### Start and stop

Start the assistant alongside the existing stack:

```powershell
docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up -d openclaw-agent
```

Stop the assistant without affecting `openclaw-core`:

```powershell
docker compose stop openclaw-agent
```

Stop `openclaw-core` without affecting the assistant:

```powershell
docker compose stop openclaw-core
```

Temporarily stop the full container stack without removing containers, volumes, or networks:

```powershell
docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml stop
```

Restart the stopped services in place:

```powershell
docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml start
```

`docker compose stop` preserves containers, volumes, and networks, so configuration and the `/workspace` volume persist across restarts. Use `docker compose down` only when intentionally tearing down the deployment. Stopping `openclaw-agent` does not affect `openclaw-core`, and vice versa. Both services independently consume the HostAdapter API.

The assistant configuration workspace is no longer bind-mounted from the host. The compose build bakes `deploy/docker/openclaw-assistant/` into a local wrapper image and Docker populates a managed `/workspace` volume from that image on first start.

### Connectivity verification

Run the aggregated validation script. It performs container inspection, `/health/live`, `/health/ready`, `/api/status`, agent dashboard root reachability, `/readyz`, in-container HostAdapter reachability, `.env` token presence, and a live dashboard auth probe in a single pass and reports a single `OverallResult`.

```powershell
pwsh -NoProfile -File scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru
```

When `-CoreBaseUrl` is omitted, the script derives the Core probe URL from
`OPENCLAW_HTTP_PORT` in the selected `.env`; `OPENCLAW_HTTP_PORT=8081`
resolves to `http://127.0.0.1:8081`.

`OverallResult: Expected` means every probe passed. `OverallResult: Unexpected` will list the specific failing probes in `SupportingDiagnostics`.

### Dashboard access

The page served at `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` is the OpenClaw Gateway Dashboard. For this loopback-only deployment, `deploy/docker/openclaw-assistant/openclaw.json` sets `gateway.auth.mode` to `token` and references `${OPENCLAW_GATEWAY_TOKEN}` from `.env`. The token is produced by `scripts/Invoke-OpenClawAgentOnboarding.ps1` and written to the repository-root `.env`. The HostAdapter bearer token remains separate and is still used only for the agent's HTTP calls to `OpenClaw.HostAdapter`.

#### Onboarding parameter overrides

`scripts/Invoke-OpenClawAgentOnboarding.ps1` exposes an optional `-OnboardBinaryPath` parameter (default `dist/index.js`). The default matches the upstream onboarding binary location in the GitHub Container Registry image at the time of this release. Supply an override only when an upstream release renames or relocates the entry-point binary (for example, `-OnboardBinaryPath 'openclaw.mjs'`). No other invocation change is required; the new value substitutes directly into the `docker compose run` argument list.

### Troubleshooting

| Symptom | Likely cause | Corrective action |
| --- | --- | --- |
| `401 Unauthorized` from HostAdapter | Token file missing, empty, or invalid inside the container | Verify `HOSTADAPTER_TOKEN_FILE` in `.env` points to the correct host path and the file is non-empty. Confirm the bind mount at `/run/openclaw/hostadapter.token` is present inside the container. |
| Container exits immediately on startup | `OPENCLAW_AGENT_IMAGE` is unset or set to the placeholder value | Set `OPENCLAW_AGENT_IMAGE` in `.env` to a valid, verified image reference. |
| `host.docker.internal` resolution failure | Docker Desktop networking not available or not using dev compose | Ensure you include `-f docker-compose.dev.yml` in the compose command, or verify Docker Desktop is running with host networking support enabled. |

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
| `Add-AppxPackage` fails with `0x80073CFB` and says the package has the same identity but different contents | A package with the same identity and version is already installed, and the rebuilt `.msix` is not byte-identical to the installed package | Publish with an incremented four-part version, or remove the installed package with `Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage`, then rerun `Add-AppxPackage`. If the package is installed for other users, remove it for those users from an elevated PowerShell session before reusing the same version. |
| `Access is denied` when running `client\OpenClaw.MailBridge.Client.exe` from `%ProgramFiles%\WindowsApps\...` after MSIX install | The package is installed, but the current manifest does not expose the packaged client as a terminal-invokable entry point | Treat the MSIX install as successful, sign out and back in so the startup task can launch the bridge, and validate the bridge through process and log checks instead of direct client execution. |
| PowerShell reports that `C:\Program` is not recognized when running the client executable | The executable path contains a space and was entered without the PowerShell call operator | Run `& "C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe" status` instead of entering the path bare. |
| HostAdapter returns `401` | Bearer token missing or invalid | Read the expected token from the configured token file and retry. |
| HostAdapter cannot start | Missing or empty token file, or incorrect client path | Recreate `adapter.token` and verify `ClientExecutablePath` in `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json`. |
| HostAdapter or Core stops working after the token file was edited or replaced | The bearer token changed and one or both components are still using the old value | Update `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`, then restart HostAdapter and restart the Core container so both sides read the same current token. |
| Docker readiness returns `503` | SQLite not initialized or HostAdapter unreachable | Check the container logs, confirm `host.docker.internal` resolves, and verify the HostAdapter is running on `127.0.0.1:4319`. |
| Empty calendar result set | Request window is outside the cached calendar range | Confirm `calendarPastDays` and `calendarFutureDays`; empty results are expected outside the cached window. |
| Safe mode seems to hide sender or preview fields | Bridge is operating as designed | Keep `safe` mode if privacy is required, or switch to `enhanced` only after operator approval. |
| `No prior install recorded` when running `Uninstall.ps1` | `%LOCALAPPDATA%\OpenClaw\install-record.json` is absent | Confirm that an install was performed via `Install.ps1`. If the install was performed via Path A or Path B, use the matching uninstall path instead (`uninstall-mailbridge.ps1` for Path A; `Get-AppxPackage ... | Remove-AppxPackage` for Path B). |
| `Docker Desktop is not running or not installed. Start Docker Desktop and retry, or pass -SkipDocker to skip the container stage.` when running `Install.ps1` | `docker info` returned a non-zero exit code | Start Docker Desktop and rerun `Install.ps1`. If a docker-free install is acceptable, pass `-SkipDocker`; `Uninstall.ps1` later honors the recorded `skipDocker = true` and skips the compose-down step. |
| `Required Docker secret file not found at '<path>\docker\secrets\.env.anthropic'...` when running `Install.ps1` | Docker installation was requested, but the Anthropic env file was not supplied to the installed Docker directory | Keep the secret outside the publish bundle and rerun `Install.ps1` with `-AnthropicEnvFilePath <operator-config>\secrets\.env.anthropic`, or pass `-SkipDocker` and stage Docker configuration manually before starting compose. |
| `Manifest integrity check failed for bundle '<path>'. Discrepancies: ...` when running `Install.ps1` | One or more files under the bundle root do not match `manifest.json` by size or SHA-256, or on-disk files are absent from the manifest. This includes manually added files such as `docker/.env` or `docker/secrets/.env.anthropic`. | Remove machine-local files from `artifacts/publish/<version>/`. Keep operator env files outside the bundle and pass them with `Install.ps1 -DockerEnvFilePath ... -AnthropicEnvFilePath ...`, or re-publish the bundle with `scripts\Publish.ps1` if packaged files were changed. No destination folder is created when manifest integrity fails. |
| `manifest.json not found at '<path>\manifest.json'. Ensure Install.ps1 is executed from a bundle directory produced by Publish.ps1...` when running `Install.ps1` | The script resolved the bundle root from `$PSScriptRoot` (or `-SourcePath`) but no `manifest.json` sits at that root | Ensure the script is being run from the bundle directory produced by `Publish.ps1` (i.e. `cd artifacts/publish/<version>; .\Install.ps1`), not from the repo's `scripts/` directory. Pass `-SourcePath` only to override for dev/test scenarios. |
