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
- Exposes Graph-shaped calendar event fields on `EventDto` (`categories`, `isOrganizer`, `isOnlineMeeting`, `allowNewTimeProposals`, `iCalUId`, `seriesMasterId`, `lastModifiedDateTime`, `bodyFull`, `sensitivityLabel`) populated from Outlook COM. See [`docs/api-reference.md`](./docs/api-reference.md) for the full field contract.
- Serves cache-backed read-only responses over a named pipe.
- Keeps `safe` mode as the default response-shaping mode.
- Primary installation path is the end-to-end scripted bundle: `scripts/Publish.ps1`
  produces an `artifacts/publish/<version>/` bundle, and the `Install.ps1` script
  staged inside that bundle performs the complete install in one run (MSIX bridge
  and client, HostAdapter, and the `openclaw-core` + `openclaw-agent` Docker
  compose stack), writing a single install record for later rollback. See
  [Install On Windows (Recommended)](#install-on-windows-recommended-end-to-end-scripted-bundle).
- Two narrower Windows installation paths remain available as alternatives:
  - published binaries plus `install-mailbridge.ps1` (bridge plus scheduled task only)
  - MSIX package install (bridge and client only)
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

## Install On Windows (Recommended): End-To-End Scripted Bundle

This is the recommended installation path. `scripts/Publish.ps1` builds a single
versioned bundle under `artifacts/publish/<version>/`, and the `Install.ps1`
script staged inside that bundle performs the complete install in one run: it
verifies the bundle manifest, copies the executables, starts the HostAdapter,
installs the MSIX bridge and client, starts the `openclaw-core` and
`openclaw-agent` Docker compose stack, and writes a single install record to
`%LOCALAPPDATA%\OpenClaw\install-record.json` for later rollback.

### Prerequisites

- Windows 10 or Windows 11 with Classic Outlook available over COM.
- An interactive user session signed in as the operator.
- .NET SDK `10.0.201` (see [`global.json`](./global.json)) and PowerShell 7 or later.
- Docker Desktop installed and running (required unless you pass `-SkipDocker`).
- An Anthropic API key for the assistant service.
- A code-signing certificate thumbprint for a signed bundle. For a local
  development bundle you can skip signing with `-SkipSign`; the matching install
  then requires `-AllowUnsigned` in an elevated PowerShell session.

All commands below assume you start from the repository root (the directory
that contains `OpenClaw.MailBridge.sln`). Change into your own clone, then
capture `$repoRoot` from your current location and reuse it:

```powershell
# Run this from inside your cloned open-claw-bridge directory.
$repoRoot = (Get-Location).Path
```

### Step 1 — Build a release bundle

`scripts/Publish.ps1` is env-driven. It reads the last-published version from
`OPENCLAW_PACKAGE_VERSION` in the repository-root `.env`, auto-increments the
4th (revision) segment, publishes that next revision, and writes the new value
back to `.env`. With this in place you do not pass `-Version` on each publish:

```powershell
# Publishes the next revision after OPENCLAW_PACKAGE_VERSION in .env, signed.
.\scripts\Publish.ps1 -CertThumbprint 'THUMBPRINT'
```

For a local development bundle without signing:

```powershell
.\scripts\Publish.ps1 -SkipSign
```

Seed `OPENCLAW_PACKAGE_VERSION` in `.env` once before the first env-driven
publish (see `.env.example` for the documented key and its format). If
`OPENCLAW_PACKAGE_VERSION` is missing or blank and you do not pass `-Version`,
the script fails fast rather than inventing a version.

To pin a specific version, pass `-Version`; the supplied value is used verbatim
and persisted back to `OPENCLAW_PACKAGE_VERSION` in `.env`:

```powershell
.\scripts\Publish.ps1 -Version '1.0.2.2' -CertThumbprint 'THUMBPRINT'
```

The bundle, including the staged `Install.ps1` and `Uninstall.ps1`, is written
to `artifacts\publish\<version>\`, where `<version>` is the version that was
published (the auto-incremented value or your `-Version`).

After publishing, capture the published version for the remaining steps. The
script wrote it to `OPENCLAW_PACKAGE_VERSION` in `.env`, so read it back:

```powershell
$versionNum = (Get-Content (Join-Path $repoRoot '.env') |
  Where-Object { $_ -match '^OPENCLAW_PACKAGE_VERSION=' }) -replace '^OPENCLAW_PACKAGE_VERSION=', ''
```

### Step 2 — Prepare operator configuration outside the bundle

The bundle directory is manifest-controlled. Keep machine-local env files
outside it, in a version-neutral location you can reuse for every install and
update:

```powershell
$operatorConfig = Join-Path $env:LOCALAPPDATA 'OpenClaw\operator-config'
New-Item -ItemType Directory -Force -Path (Join-Path $operatorConfig 'secrets') | Out-Null
Copy-Item (Join-Path $repoRoot ("artifacts\publish\{0}\docker\.env.example" -f $versionNum)) (Join-Path $operatorConfig '.env') -Force
```

Add your Anthropic API key to the secrets env file:

```powershell
Set-Content -Path (Join-Path $operatorConfig 'secrets\.env.anthropic') -Value 'ANTHROPIC_API_KEY=replace-with-your-real-key'
```

Generate the gateway token the `openclaw-agent` container requires and write it
into the operator `.env`:

```powershell
pwsh -NoProfile -File .\scripts\Invoke-OpenClawAgentOnboarding.ps1 -EnvFilePath (Join-Path $operatorConfig '.env')
```

Confirm both operator files exist before installing. Both commands must return
`True`:

```powershell
Test-Path (Join-Path $operatorConfig '.env')
Test-Path (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

### Step 3 — Run the bundled installer

Change into the bundle directory so `Install.ps1` self-locates via
`$PSScriptRoot`, then run it with the operator env files:

```powershell
Set-Location (Join-Path $repoRoot ("artifacts\publish\{0}" -f $versionNum))
.\Install.ps1 `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

For an unsigned (`-SkipSign`) bundle, run an elevated PowerShell session and add
`-AllowUnsigned`:

```powershell
.\Install.ps1 -AllowUnsigned `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

To install only the MSIX bridge and client and defer the Docker stage:

```powershell
.\Install.ps1 -SkipDocker
```

### Step 4 — Verify the install

```powershell
curl.exe http://127.0.0.1:8081/health/ready
curl.exe http://127.0.0.1:8081/api/status
pwsh -NoProfile -File (Join-Path $repoRoot 'scripts\Invoke-OpenClawContainerPathValidation.ps1') -PassThru
```

Open the local UI at `http://127.0.0.1:8081` and the assistant dashboard at
`http://127.0.0.1:18789/`.

### Uninstall

`Install.ps1` records everything it changed. Reverse the most recent install
from that version's bundle directory:

```powershell
Set-Location (Join-Path $repoRoot ("artifacts\publish\{0}" -f $versionNum))
.\Uninstall.ps1
```

`Uninstall.ps1` runs compose down, removes the MSIX, deletes the per-version
destination folder, and removes the install record. User configuration under
`%LOCALAPPDATA%\OpenClaw\MailBridge\` is preserved.

If `Uninstall.ps1` fails, you can reconstruct the install artifact with the following:

```pwsh
$pkg  = (Get-AppxPackage -Name 'OpenClaw.MailBridge').PackageFullName
$dest = (Get-ChildItem "$env:LOCALAPPDATA\OpenClaw" -Directory |
          Where-Object Name -ne 'MailBridge' | Select-Object -First 1).FullName

[pscustomobject]@{
    installedAt        = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    version            = 'unknown'
    sourcePath         = 'unknown'
    destinationPath    = $dest
    packageFullName    = $pkg
    composeProjectName = 'openclaw'          # hard-coded in Install.ps1:437
    composeFilePath    = "$dest\docker\docker-compose.yml"
    skipDocker         = $false              # $true if you never ran Docker
    allowUnsigned      = $false
} | ConvertTo-Json | Set-Content "$env:LOCALAPPDATA\OpenClaw\install-record.json" -Encoding utf8

.\scripts\Uninstall.ps1
```

If uninstall still does not work, components can be removed individually:

```powershell
# ============================================================================
# OpenClaw forced manual uninstall (no install-record.json required)
# Mirrors scripts/Uninstall.ps1 stages; each step tolerates missing components.
# Run in PowerShell 7 (pwsh). Elevation not required unless MSIX removal
# demands it for your account.
# ============================================================================

# --- Stage 1: stop the docker compose stack (project name is hard-coded
#     'openclaw' in Install.ps1). First try compose down; if the compose file
#     or project is gone, force-remove any containers labeled with the project.
docker compose -p openclaw down 2>$null
docker ps -aq --filter "label=com.docker.compose.project=openclaw" |
    ForEach-Object { docker rm -f $_ }

# Remove any leftover compose networks for the project (ignore errors).
docker network ls -q --filter "label=com.docker.compose.project=openclaw" |
    ForEach-Object { docker network rm $_ 2>$null }

# --- Stage 2: stop the HostAdapter process if it is still running
#     (Install.ps1 Stage 7a launches it from the bundle folder).
Get-Process -Name 'OpenClaw.HostAdapter' -ErrorAction SilentlyContinue |
    Stop-Process -Force -Confirm:$false

# --- Stage 3: remove the MSIX package (no-op if not installed).
$pkg = Get-AppxPackage -Name 'OpenClaw.MailBridge' -ErrorAction SilentlyContinue
if ($pkg) {
    Write-Host "Removing MSIX $($pkg.PackageFullName)"
    Remove-AppxPackage -Package $pkg.PackageFullName
} else {
    Write-Host 'MSIX OpenClaw.MailBridge not installed; skipping.'
}

# --- Stage 4: remove the per-version bundle folder(s) under
#     %LOCALAPPDATA%\OpenClaw. The 'MailBridge' sibling holds user config,
#     cache, and logs and is intentionally preserved (same as Uninstall.ps1).
Get-ChildItem "$env:LOCALAPPDATA\OpenClaw" -Directory -ErrorAction SilentlyContinue |
    Where-Object Name -ne 'MailBridge' |
    ForEach-Object {
        Write-Host "Removing bundle folder $($_.FullName)"
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -Confirm:$false
    }

# --- Stage 5: remove a stale install record if one exists.
Remove-Item "$env:LOCALAPPDATA\OpenClaw\install-record.json" -Force -Confirm:$false -ErrorAction SilentlyContinue

# --- Stage 6 (legacy only): remove the old 'OpenClaw MailBridge' scheduled
#     task if it was ever registered (register-mailbridge-task.ps1 era).
schtasks /end /tn 'OpenClaw MailBridge' 2>$null | Out-Null
schtasks /delete /tn 'OpenClaw MailBridge' /f 2>$null | Out-Null

# --- Verification: everything below should come back empty/absent.
Write-Host "`n=== Verification ==="
docker ps -a --filter "label=com.docker.compose.project=openclaw"
Get-AppxPackage -Name 'OpenClaw.MailBridge' -ErrorAction SilentlyContinue
Get-Process -Name 'OpenClaw.HostAdapter' -ErrorAction SilentlyContinue
Get-ChildItem "$env:LOCALAPPDATA\OpenClaw" -ErrorAction SilentlyContinue |
    Select-Object Name   # only 'MailBridge' (user config) should remain
```

## Update An Installed Package After Development Work

After changing code, rebuild a new bundle and reinstall it over the existing
install. The installer treats each version as its own destination folder under
`%LOCALAPPDATA%\OpenClaw\<version>\` and keeps a single install record, so an
update requires `-Force`. With `-Force`, the installer runs the prior compose
down, removes the prior destination folder, installs the new MSIX over the same
package name, and starts the new compose stack.

These steps assume the operator configuration from
[Step 2 above](#step-2--prepare-operator-configuration-outside-the-bundle) is
already in place and reusable, and that `$repoRoot` and `$operatorConfig` are
still set in your session.

### Step 1 — Verify your changes build and pass tests

```powershell
Set-Location $repoRoot
.\scripts\Build.ps1
.\scripts\Test.ps1
```

### Step 2 — Publish a new bundle

Publish env-driven. `Publish.ps1` reads `OPENCLAW_PACKAGE_VERSION` from `.env`,
auto-increments the revision, and persists the new value, so each update
publishes the next revision above the installed one without picking a version by
hand:

```powershell
.\scripts\Publish.ps1 -CertThumbprint 'THUMBPRINT'
```

Development (unsigned) build:

```powershell
.\scripts\Publish.ps1 -SkipSign
```

Then capture the version that was published for the reinstall step:

```powershell
$versionNum = (Get-Content (Join-Path $repoRoot '.env') |
  Where-Object { $_ -match '^OPENCLAW_PACKAGE_VERSION=' }) -replace '^OPENCLAW_PACKAGE_VERSION=', ''
```

To pin a specific version instead, pass `-Version '1.0.2.3'`; it is used verbatim
and persisted to `.env`.

### Step 3 — Reinstall from the new bundle with -Force

```powershell
Set-Location (Join-Path $repoRoot ("artifacts\publish\{0}" -f $versionNum))
.\Install.ps1 -Force `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

For an unsigned bundle, run an elevated PowerShell session and add
`-AllowUnsigned`:

```powershell
.\Install.ps1 -Force -AllowUnsigned `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

Reinstalling the same version (for example to re-apply a rebuild without bumping
the version) uses the same `-Force` command from that version's bundle directory.

### Step 4 — Verify the update

```powershell
curl.exe http://127.0.0.1:8081/health/ready
curl.exe http://127.0.0.1:8081/api/status
pwsh -NoProfile -File (Join-Path $repoRoot 'scripts\Invoke-OpenClawContainerPathValidation.ps1') -PassThru
```

## Run From Source (Alternative)

For development and quick checks you can run the projects directly without
installing a bundle.

Start the bridge:

```powershell
dotnet run --project .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj --configuration Debug
```

Query it from the client:

```powershell
dotnet run --project .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj --configuration Debug -- status
```

The bridge creates `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` on first run if it does not already exist.

## Alternative Install: Published Binaries With Scheduled Task

This is a narrower script-driven install path that installs only the bridge and
client. Prefer the [recommended scripted bundle](#install-on-windows-recommended-end-to-end-scripted-bundle)
unless you specifically need the scheduled-task deployment. It publishes the host
and client to a shared install folder, seeds the per-user config file, validates
Outlook and runtime prerequisites, registers an interactive scheduled task, and
smoke-checks the installed bridge.

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

## Alternative Install: Build And Install The MSIX Package

The repository also contains an MSIX packaging path that installs the bridge and
client together and starts the bridge on user logon through a
`windows.startupTask`. This installs only the bridge and client; prefer the
[recommended scripted bundle](#install-on-windows-recommended-end-to-end-scripted-bundle)
for the complete solution.

#### Signing certificate options

The build never creates a certificate; certificate creation is always an
explicit operator step. `Publish.ps1` resolves the signing thumbprint with this
precedence: explicit `-CertThumbprint` > `OPENCLAW_CERT_THUMBPRINT` in the
repository-root `.env` > the dotnet user secret `Signing:CertThumbprint` > the
`OPENCLAW_CERT_THUMBPRINT` process-environment variable. With none of these and
no `-SkipSign`, the script fails fast before any state-changing stage.

Option A — create and trust a development signing certificate once per machine.
`New-MsixDevCert.ps1` writes the created certificate's thumbprint to
`OPENCLAW_CERT_THUMBPRINT` in the repository-root `.env`, so a subsequent
`Publish.ps1` resolves it automatically (no `-CertThumbprint` needed):

```powershell
$pfxPassword = ConvertTo-SecureString 'your-password' -AsPlainText -Force
.\scripts\New-MsixDevCert.ps1 -PfxPassword $pfxPassword -OutputDir artifacts
```

Option B — store an existing installed code-signing certificate's thumbprint in
`.env`. Locate the certificate in your user store and write its thumbprint to
`OPENCLAW_CERT_THUMBPRINT`:

```powershell
Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
  Format-Table Subject, Thumbprint, NotAfter
# Copy the chosen Thumbprint, then set it in .env (update in place, preserving
# other keys and comments):
$thumb = '<paste-the-thumbprint>'
Import-Module .\scripts\Publish.Env.psm1 -Force
$envPath = Join-Path (Get-Location).Path '.env'
$content = Read-EnvFileContent -Path $envPath
Write-EnvFileContent -Path $envPath -Content (Set-EnvFileValue -Content $content -Key 'OPENCLAW_CERT_THUMBPRINT' -Value $thumb)
```

You can also edit `.env` by hand and set `OPENCLAW_CERT_THUMBPRINT=<thumbprint>`
directly; see `.env.example` for the documented key.

Publish a full release bundle with `scripts/Publish.ps1`. The unified entry
point publishes every runnable `src/` project, copies the docker artifact
set, builds (and optionally signs) the MSIX, and writes a top-level
`manifest.json` that enumerates every file in the bundle with its size and
SHA-256 hash. The output is written to `artifacts/publish/<version>/`.

`Publish.ps1` is env-driven: with no `-Version` it reads
`OPENCLAW_PACKAGE_VERSION` from `.env`, increments the revision, publishes that
next revision, and persists the new value back to `.env`.

Signed release build (thumbprint resolved from `.env`, version auto-incremented):

```powershell
.\scripts\Publish.ps1
```

Dev (unsigned) build:

```powershell
.\scripts\Publish.ps1 -SkipSign
```

Pin a specific version and/or thumbprint explicitly when needed:

```powershell
.\scripts\Publish.ps1 -Version '1.0.0.0' -CertThumbprint 'THUMBPRINT'
```

Supported parameters:

- `-Version` — optional 4-part version string (for example `1.2.3.0`). Strict
  validation via `ValidatePattern`; 3-part inputs are rejected. When omitted,
  the version is read from `OPENCLAW_PACKAGE_VERSION` in `.env` and the revision
  is auto-incremented. When supplied, it is used verbatim. Either way the
  resulting version is persisted to `OPENCLAW_PACKAGE_VERSION` in `.env`.
- `-OutputDir` — root directory for the bundle. Default: `artifacts/publish`.
- `-Configuration` — `Debug` or `Release`. Default: `Release`.
- `-CertThumbprint` — SHA-1 thumbprint of the code-signing certificate in
  `Cert:\CurrentUser\My`. Optional; when omitted it is resolved from `.env`,
  the dotnet user secret, or the process environment (see precedence above).
  A resolvable thumbprint or `-SkipSign` is required.
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

These steps apply only to the alternative install paths above (published binaries
or MSIX). The [recommended scripted bundle](#install-on-windows-recommended-end-to-end-scripted-bundle)
already performs the HostAdapter, `OpenClaw.Core`, and assistant setup; you do
not need this section if you installed with `Install.ps1`.

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
http://127.0.0.1:8081
```

Validate the local-only path:

```powershell
$token = (Get-Content 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Raw).Trim()
curl.exe -H "Authorization: Bearer $token" http://127.0.0.1:4319/status
curl.exe http://127.0.0.1:8081/health/ready
curl.exe http://127.0.0.1:8081/api/status
```

Operational notes:

- `OpenClaw.Core` publishes only to `127.0.0.1:${OPENCLAW_HTTP_PORT:-8081}`.
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

## Start, Stop, And Restart OpenClaw Services

The complete solution has three long-running pieces: the Windows **MailBridge**
process, the Windows **HostAdapter** process, and the **Docker** containers
`openclaw-core` and `openclaw-agent`. After a reboot they recover differently:

- MailBridge relaunches at the next interactive logon (its MSIX startup task, or
  the scheduled task in the published-binaries install).
- The Docker containers restart automatically when Docker Desktop next starts,
  because the compose file sets `restart: unless-stopped` (unless you explicitly
  stopped them).
- The HostAdapter that `Install.ps1` launches is a plain background process; it
  is **not** registered as a service or startup task, so it does not restart on
  reboot and must be started manually.

The commands below restart each piece manually without a reboot. They locate the
installed paths from the install record so no version or absolute path is
hard-coded:

```powershell
$record = Get-Content (Join-Path $env:LOCALAPPDATA 'OpenClaw\install-record.json') -Raw | ConvertFrom-Json
$composeFile = $record.composeFilePath
$dockerDir = Split-Path $composeFile -Parent
```

### Docker services (openclaw-core and openclaw-agent)

The installer runs compose with project name `openclaw` and the bundle's base
compose file, so use the same project and file here.

```powershell
# Status
docker compose --project-name openclaw -f $composeFile ps

# Stop (preserves containers, volumes, and the /workspace volume)
docker compose --project-name openclaw -f $composeFile stop

# Start the stopped containers again in place
docker compose --project-name openclaw -f $composeFile start

# Restart a single service
docker compose --project-name openclaw -f $composeFile restart openclaw-core
```

If the containers were removed with `down` (not just stopped), recreate them
(the `--project-directory` lets compose read the installed `.env`):

```powershell
docker compose --project-name openclaw --project-directory $dockerDir -f $composeFile up -d openclaw-core openclaw-agent
```

For a run-from-source setup (repo-root compose files with the dev overlay), the
equivalent commands are documented in
[`docs/mailbridge-runbook.md`](./docs/mailbridge-runbook.md).

### Windows MailBridge

For the recommended scripted-bundle and MSIX installs, the bridge is a packaged
app started at logon by its MSIX startup task.

```powershell
# Status
Get-Process OpenClaw.MailBridge -ErrorAction SilentlyContinue

# Stop the running instance
Get-Process OpenClaw.MailBridge -ErrorAction SilentlyContinue | Stop-Process

# Start without signing out (launch the packaged app directly)
$pkg = Get-AppxPackage -Name 'OpenClaw.MailBridge'
Start-Process ("shell:appsFolder\{0}!OpenClaw.MailBridge" -f $pkg.PackageFamilyName)
```

For the published-binaries + scheduled-task install, control the task instead:

```powershell
# Stop the running instance (task stays registered)
schtasks /end /tn "OpenClaw MailBridge"

# Start it immediately
schtasks /run /tn "OpenClaw MailBridge"

# Suppress or re-enable the automatic logon start
Disable-ScheduledTask -TaskName "OpenClaw MailBridge"
Enable-ScheduledTask  -TaskName "OpenClaw MailBridge"
```

### Windows HostAdapter

The scripted installer starts the HostAdapter from the install directory and
does not auto-register it, so start or restart it manually (for example after a
reboot):

```powershell
# Stop the running instance
Get-Process OpenClaw.HostAdapter -ErrorAction SilentlyContinue | Stop-Process

# Start it again from the installed bundle on the loopback address
$hostAdapterExe = Join-Path $record.destinationPath 'executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:4319'
Start-Process -FilePath $hostAdapterExe
```

Start order: bring up the MailBridge and HostAdapter before relying on the
containers, which poll the HostAdapter. If you rotate the HostAdapter token,
restart the HostAdapter and the `openclaw-core` container so both sides read the
same token.

Verify after restart:

```powershell
$token = (Get-Content 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Raw).Trim()
curl.exe -H "Authorization: Bearer $token" http://127.0.0.1:4319/status
curl.exe http://127.0.0.1:8081/health/ready
curl.exe http://127.0.0.1:8081/api/status
```

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

- `safe` is the default mode and redacts protected fields such as sender details, body previews, and full event body text (`bodyFull`).
- `enhanced` returns sanitized preview content plus the full event body text (`bodyFull`) and should only be enabled after operator validation.
- `pipeName` controls the named pipe used by the bridge and client.

## Canonical Client Commands

`OpenClaw.MailBridge.Client` is still the canonical transport interface and supports these commands:

- `status`
- `list-messages --since <utc> --limit <n>`
- `get-message --id <bridgeId>`
- `list-meeting-requests --since <utc> --limit <n>`
- `list-calendar --start <utc> --end <utc> --limit <n>`
- `get-event --id <bridgeId>`

The HostAdapter preserves this contract exactly and exposes it through a Microsoft Graph-shaped HTTP surface. The `{id}` path segment is the configured mailbox identifier (`HostAdapterOptions.MailboxId`, default `me`):

- `GET /status`
- `GET /users/{id}/messages` (`?$filter=receivedDateTime ge <iso8601>&$top=<n>`)
- `GET /users/{id}/messages/{messageId}`
- `GET /users/{id}/messages` (`?$filter=meetingMessageType ne null&$top=<n>` for meeting requests)
- `GET /users/{id}/calendarView` (`?startDateTime=<iso8601>&endDateTime=<iso8601>&$top=<n>`)
- `GET /users/{id}/events/{eventId}`
- `GET /users/{id}/mailboxSettings` (config-sourced mailbox time zone and working hours; returns `ApiEnvelope<MailboxSettingsDto>`)
- `GET /users/{id}/calendar/getSchedule` (`?startDateTime=<iso8601>&endDateTime=<iso8601>`; free/busy grid computed from bridge calendar data; returns `ApiEnvelope<FreeBusyScheduleDto>`)
- `POST /users/{assistantMailbox}/sendMail` (Graph-shaped outbound send through Outlook COM on the bridge STA thread; requires >= 1 recipient across To/CC/BCC and `body.contentType` in {Text, HTML}; returns **202 Accepted** with `ApiEnvelope<object?>` `{ ok: true, data: null }` on success). Send-on-behalf (a sending `fromEmailAddress`) is deferred to PI-1; the bridge mail-sender seam already accepts a future `fromEmailAddress` without breaking callers.

> Breaking change (adapter version `1.0.0`): the earlier bespoke `/v1/*` routes (`/v1/status`, `/v1/messages`, `/v1/meeting-requests`, `/v1/calendar`, `/v1/events/{bridgeId}`) were replaced by the Graph-shaped surface above. Request and response envelope shapes are unchanged. Meeting requests are served by the `/users/{id}/messages` route filtered on `meetingMessageType`. Clients configure the adapter base URL without a `/v1/` segment (for example `http://127.0.0.1:4319/`).

## Additional Documentation

- Operator runbook: [`docs/mailbridge-runbook.md`](./docs/mailbridge-runbook.md)
- API reference: [`docs/api-reference.md`](./docs/api-reference.md)
- Architecture diagrams: [`docs/architecture-diagrams.md`](./docs/architecture-diagrams.md)
- Active feature records: [`docs/features/active/`](./docs/features/active/)
