# 2026-04-10-msix-installer-package — Spec

- **Issue:** #17
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-10T19-59
- **Status:** Draft
- **Version:** 0.1

## Overview

This feature produces a signed MSIX installer package for OpenClaw MailBridge that installs both the bridge host (`OpenClaw.MailBridge.exe`) and the client CLI (`OpenClaw.MailBridge.Client.exe`), and registers the bridge to start automatically on user logon via an MSIX `windows.startupTask` extension. The package supports in-place upgrade, repair, and uninstall through the standard Windows MSIX lifecycle and is buildable entirely from CI without Visual Studio.

**Critical architectural note**: The bridge uses Outlook COM interop, which requires the interactive user session (Session 1+). Windows Services run in Session 0 and cannot access Outlook COM. The package therefore uses `uap5:Extension Category="windows.startupTask"` — not `desktop6:Extension Category="windows.service"` — to launch the bridge in the logged-on user's session on each logon. This is semantically equivalent to the existing `schtasks /sc onlogon /it` mechanism used by `register-mailbridge-task.ps1`.


## Behavior

### Main path

1. The user double-clicks the signed `.msix` (or runs `Add-AppxPackage`) on a Windows 10 (build 17763+) or Windows 11 machine.
2. Windows installs both executables to `%ProgramFiles%\WindowsApps\OpenClaw.MailBridge_<Version>_x64__<hash>\` (the package's VFS root).
3. Windows registers the startup task declared in the manifest (`TaskId="OpenClawMailBridge"`, `Enabled="true"`). The task is visible in Task Manager > Startup apps.
4. On the next user logon, the startup task fires and launches `OpenClaw.MailBridge.exe` in the interactive user session. `BridgeApplication.cs` auto-seeds `bridge.settings.json` in `%LOCALAPPDATA%\OpenClaw\MailBridge\` if absent.
5. The bridge listens on the named pipe `openclaw_mailbridge_v1`. `OpenClaw.MailBridge.Client.exe` connects to it from any terminal in the same user session.

### Upgrade path

When a package with a higher `Version` (4-part: `Major.Minor.Build.Revision`) and identical `Name` + `Publisher` is installed, Windows atomically stages and swaps the binaries. The startup task registration is preserved automatically. `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` is never touched by the upgrade.

### Uninstall path

Uninstalling via Settings > Apps or `winget uninstall` removes the package VFS files and deregisters the startup task. User config in `%LOCALAPPDATA%\OpenClaw\MailBridge\` is intentionally left in place, consistent with existing `uninstall-mailbridge.ps1` behavior.

### Side-by-side with PowerShell scripts

The existing `scripts/install-mailbridge.ps1` / `uninstall-mailbridge.ps1` / `register-mailbridge-task.ps1` scripts continue to work as an alternative deployment path. The MSIX publish profile uses `PublishSingleFile=false` (directory layout), ensuring `runtimeconfig.json` is present alongside the exe, satisfying the `Assert-DotNet10RuntimeConfig` check in `install-mailbridge.ps1`.

### Notable non-paths

- The bridge does NOT restart automatically on crash under the `startupTask` model (unlike a Windows Service with `RestartOnFailure`). This is a documented limitation; crash-restart behavior may be addressed in a future watchdog feature.
- Config is not seeded by the installer; `BridgeApplication.cs` seeds it on first launch.


## Inputs / Outputs

### `scripts/build-msix.ps1` inputs

| Parameter | Type | Default | Description |
|---|---|---|---|
| `-Version` | `string` | `'1.0.0.0'` | 4-part MSIX version stamped into `AppxManifest.xml` |
| `-OutputDir` | `string` | `'artifacts/msix'` | Directory to write the final `.msix` file |
| `-CertThumbprint` | `string` | _(none)_ | SHA-1 thumbprint of the signing certificate |
| `-SkipSign` | `switch` | `$false` | Skip `signtool.exe` step (dev/test builds) |

### `scripts/build-msix.ps1` outputs

| Artifact | Path | Notes |
|---|---|---|
| Signed MSIX package | `artifacts/msix/OpenClaw.MailBridge_<Version>_x64.msix` | Ready for `Add-AppxPackage` or `winget install` |
| Staging directory | `installer/staging/` | Ephemeral; gitignored; contains assembled layout |

### `scripts/New-MsixDevCert.ps1` inputs

| Parameter | Type | Default | Description |
|---|---|---|---|
| `-Subject` | `string` | `'CN=OpenClaw Dev, O=OpenClaw, C=US'` | Certificate subject; must match `Publisher` in manifest |
| `-OutputDir` | `string` | `'artifacts'` | Directory for exported `.pfx` and `.cer` files |
| `-PfxPassword` | `SecureString` | _(prompted)_ | Password protecting the exported PFX |

### Publish profiles (new files)

| File | Key settings |
|---|---|
| `src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml` | `PublishSingleFile=false`, `SelfContained=true`, `RuntimeIdentifier=win-x64`, `PublishReadyToRun=true` |
| `src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml` | Same as above; output to `artifacts/publish/client/` |

### MSIX package layout (staging directory)

```
installer/staging/
├── AppxManifest.xml          # version-stamped from installer/Package.appxmanifest
├── resources.pri             # generated by MakePri.exe
├── Assets/
│   ├── Square44x44Logo.png
│   ├── Square150x150Logo.png
│   ├── StoreLogo.png
│   └── SplashScreen.png
├── bridge/                   # dotnet publish output for OpenClaw.MailBridge
│   ├── OpenClaw.MailBridge.exe
│   ├── OpenClaw.MailBridge.dll
│   └── ... (all SelfContained DLLs and runtimeconfig.json)
└── client/                   # dotnet publish output for OpenClaw.MailBridge.Client
    ├── OpenClaw.MailBridge.Client.exe
    └── ...
```

### Versioning constraints

- `Identity Version` is a 4-part uint16 tuple (`Major.Minor.Build.Revision`). Each component must be 0–65535.
- `Name` (`OpenClaw.MailBridge`) and `Publisher` (`CN=OpenClaw, O=OpenClaw, C=US`) must be identical across all versions for in-place upgrade to work.
- The `Publisher` value must exactly match the Subject of the signing certificate.


## API / CLI Surface

### Build script invocations

```powershell
# Dev build (unsigned)
.\scripts\build-msix.ps1 -Version '1.0.0.0' -SkipSign

# CI build (signed with cert thumbprint)
.\scripts\build-msix.ps1 -Version '1.2.0.0' -CertThumbprint 'ABCDEF1234...' -OutputDir 'artifacts/msix'

# Create dev cert and import to Trusted Root
.\scripts\New-MsixDevCert.ps1 -Subject 'CN=OpenClaw Dev, O=OpenClaw, C=US' -OutputDir 'artifacts'
```

### Install / uninstall (end-user)

```powershell
# Install or upgrade
Add-AppxPackage -Path 'artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix'

# Uninstall
Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage
```

### MSIX manifest startup task declaration

```xml
<uap5:Extension Category="windows.startupTask"
                Executable="bridge\OpenClaw.MailBridge.exe"
                EntryPoint="Windows.FullTrustApplication">
  <uap5:StartupTask TaskId="OpenClawMailBridge"
                    Enabled="true"
                    DisplayName="OpenClaw MailBridge"/>
</uap5:Extension>
```

### GitHub Actions trigger

```yaml
on:
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      version:
        required: true
        default: '1.0.0.0'
```

The workflow uploads the signed `.msix` as a GitHub Actions artifact named `msix-package`.


## Data & State

### Data flow

1. `dotnet publish` (using `msix.pubxml`) writes a directory-layout binary tree to `artifacts/publish/bridge/` and `artifacts/publish/client/`.
2. `build-msix.ps1` copies those trees into `installer/staging/bridge/` and `installer/staging/client/`, stamps the version into `staging/AppxManifest.xml`, copies `installer/Assets/`, and invokes `MakePri.exe` to generate `staging/resources.pri`.
3. `makeappx.exe pack` reads `staging/` and emits `artifacts/msix/OpenClaw.MailBridge_<Version>_x64.msix`.
4. `signtool.exe sign` signs the `.msix` in place.
5. At install time, Windows extracts package files to the immutable VFS (`%ProgramFiles%\WindowsApps\...`).
6. On first logon after install, the startup task fires `OpenClaw.MailBridge.exe`. `BridgeApplication.LoadSettings()` writes `bridge.settings.json` to `%LOCALAPPDATA%\OpenClaw\MailBridge\` if absent.

### Persistence

- MSIX package VFS: read-only after install; managed by Windows.
- `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`: written by the bridge at first launch; never touched by MSIX upgrade or uninstall.
- `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.db` (SQLite cache): persisted across upgrades; never touched by MSIX.

### Migration / backfill

No migration required. The bridge's existing `LoadSettings()` self-seeding logic handles first-run config initialization for both MSIX and non-MSIX deployments.


## Constraints & Risks

- **Session 0 / Outlook COM (critical)**: Windows Services run in Session 0 and cannot access Outlook COM objects or the interactive user's `%LOCALAPPDATA%`. The package MUST use `windows.startupTask`, not `windows.service`. This is a hard architectural constraint driven by `OutlookStaExecutor` and `BridgeApplication.LoadSettings()`.
- **No crash-restart**: `windows.startupTask` does not restart the process on crash. The bridge restarts only on the next user logon. Watchdog logic is out of scope for this feature.
- **`runFullTrust` capability**: Required for the startup task to launch a full-trust process. For Microsoft Store distribution this requires partner-center approval. For sideloading and enterprise deployment, any valid Authenticode certificate suffices.
- **Publisher identity binding**: The `Publisher` attribute in `Identity` must exactly match the certificate Subject. Changing the publisher across versions breaks upgrade continuity.
- **`PublishSingleFile=false` required**: MSIX file enumeration requires individual files on disk. The MSIX-specific publish profiles disable single-file output. The standard (non-MSIX) build targets are unchanged.
- **Icon assets**: MSIX refuses to pack without the declared icon files. Placeholder PNGs at the required sizes (44×44, 150×150, 50×50 StoreLogo, 620×300 SplashScreen) must be committed to `installer/Assets/` before the package can be built.
- **Windows SDK availability**: `makeappx.exe`, `signtool.exe`, and `MakePri.exe` are required. The `windows-latest` GitHub Actions runner includes the Windows SDK. If the runner-provided SDK is insufficient, `Microsoft.Windows.SDK.BuildTools` (NuGet) can supply these tools without requiring Visual Studio.
- **Minimum OS**: Manifest declares `MinVersion="10.0.17763.0"` (Windows 10 1809). `windows.startupTask` is available from Windows 10 1803 (17134); 1809 is chosen for broader ecosystem compatibility.
- **Self-signed cert install restriction**: A self-signed package can only be installed on machines that have the signing cert in their Trusted Root store. `New-MsixDevCert.ps1` handles this for dev/CI. Production distribution requires a commercial cert or Azure Trusted Signing.


## Implementation Strategy

### Scope — new files only (no changes to existing source)

| File | Purpose |
|---|---|
| `installer/Package.appxmanifest` | MSIX manifest; source of truth for identity, startup task, and capabilities |
| `installer/Assets/*.png` | Required icon assets (Square44x44Logo, Square150x150Logo, StoreLogo, SplashScreen) |
| `scripts/build-msix.ps1` | Orchestrates: version stamping → layout assembly → MakePri → makeappx → signtool |
| `scripts/New-MsixDevCert.ps1` | Creates and exports a self-signed code-signing cert for dev/CI use |
| `src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml` | Bridge host publish profile (`PublishSingleFile=false`) |
| `src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml` | Client publish profile (`PublishSingleFile=false`) |
| `.github/workflows/build-msix.yml` | CI workflow: publish → pack → sign → upload artifact |
| `tests/scripts/build-msix.Tests.ps1` | Pester v5 unit tests for `build-msix.ps1` helper functions |
| `tests/scripts/New-MsixDevCert.Tests.ps1` | Pester v5 unit tests for `New-MsixDevCert.ps1` |
| `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` | MSTest tests validating manifest content and publish output layout |

### Existing files — no changes required

`Program.cs`, `BridgeApplication.cs`, both `.csproj` files, `install-mailbridge.ps1`, `uninstall-mailbridge.ps1`, `register-mailbridge-task.ps1`. The `AddWindowsService()` call in `BridgeApplication.BuildHost()` is already present and correct for both MSIX startup-task and non-MSIX scheduled-task deployments.

### Dependency changes

No new NuGet packages required. `makeappx.exe`, `MakePri.exe`, and `signtool.exe` are consumed from the Windows SDK on the runner. Optional: add `Microsoft.Windows.SDK.BuildTools` as a NuGet tool reference if the runner SDK version proves insufficient.

### Logging / telemetry

`build-msix.ps1` writes progress via `Write-Information` (stream 6) and errors via `Write-Error`. No changes to bridge host telemetry are required.

### Rollout

The MSIX package is a standalone deployment artifact. It does not replace the existing PowerShell script path; both coexist. The CI workflow is gated on tag push, so no change to the main branch build pipeline until the workflow file is merged.


## Definition of Done

- [x] `installer/Package.appxmanifest` exists with `windows.startupTask` extension; `windows.service` is NOT declared.
- [x] `installer/Assets/` contains all four required PNG icons at correct sizes.
- [ ] `scripts/build-msix.ps1` produces a valid `.msix` when invoked after `dotnet publish` on a `windows-latest` runner.
- [x] `scripts/New-MsixDevCert.ps1` creates and exports a self-signed cert; the exported PFX can sign the MSIX.
- [x] MSIX publish profiles exist for both projects with `PublishSingleFile=false` and `SelfContained=true`.
- [x] `.github/workflows/build-msix.yml` triggers on `v*` tags and `workflow_dispatch`; uploads the `.msix` as a GitHub Actions artifact named `msix-package`.
- [ ] Acceptance criteria 1–9 verified (see user-story.md).
- [x] All new Pester tests pass (`drmcopilotextension-run_poshqc_test` targeting `tests/scripts/`).
- [x] All new MSTest tests pass (`dotnet test`).
- [x] PoshQC suite passes (format → analyze → test) on all new PowerShell files.
- [x] `README.md` updated with MSIX install instructions alongside existing PowerShell script instructions.


## Seeded Test Conditions (from potential)

- [x] Unit tests for `build-msix.ps1`: version stamping, missing publish directory error, layout assembly, `makeappx.exe` argument validation, `-SkipSign` flag behavior.
- [x] Unit tests for `New-MsixDevCert.ps1`: correct Subject CN, PFX export to specified path.
- [ ] MSTest `MsixPackageTests.cs`: manifest parses as valid XML; `startupTask` extension present with correct `Executable`; `Version` attribute is a valid 4-part version; `OpenClaw.MailBridge.exe` and `OpenClaw.MailBridge.Client.exe` present in publish output when `MSIX_PUBLISH_DIR` env var is set.
- [x] Smoke-test (manual / integration): install `.msix` → log off and back on → bridge process visible in Task Manager → `OpenClaw.MailBridge.Client.exe status` returns non-empty JSON.
- [ ] Upgrade scenario: install v1.0.0.0 → install v1.1.0.0 → startup task still registered → `bridge.settings.json` unchanged.
- [ ] Uninstall scenario: `Remove-AppxPackage` → startup task absent from Task Manager → `bridge\` and `client\` directories gone → `bridge.settings.json` still present in `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
