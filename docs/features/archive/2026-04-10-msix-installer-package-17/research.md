<!-- markdownlint-disable-file -->

# Task Research Notes: MSIX Installer Package for OpenClaw MailBridge (Issue #17)

## Research Executed

### File Analysis

- `src/OpenClaw.MailBridge/Program.cs`
  - Delegates directly to `BridgeApplication.RunAsync(args)`. No direct `UseWindowsService()` call here.
- `src/OpenClaw.MailBridge/BridgeApplication.cs`
  - **CONFIRMED**: `BuildHost()` calls `builder.Services.AddWindowsService(options => { options.ServiceName = "OpenClaw.MailBridge"; })` at line 52–55.
  - `LoadSettings()` auto-seeds `BridgeSettings.Default` if `bridge.settings.json` is absent — config seeding is already handled by the application at startup.
  - Config path uses `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` — resolves to the **calling user's** `%LOCALAPPDATA%`. In Session 0 (LocalSystem) this resolves to `C:\Windows\System32\config\systemprofile\AppData\Local` — NOT the interactive user's profile.
  - Uses `OutlookStaExecutor` which dedicates a COM STA thread for Outlook interop. Outlook COM requires the interactive user session (Session 1+), not Session 0.
- `src/OpenClaw.MailBridge.Client/Program.cs`
  - Also resolves config via `Environment.SpecialFolder.LocalApplicationData`.
  - Communicates with bridge over a named pipe (`openclaw_mailbridge_v1` by default).
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`
  - `TargetFramework: net10.0-windows`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`.
  - References: `Microsoft.Extensions.Hosting.WindowsServices` v10.0.5, `Microsoft.Data.Sqlite` v8.0.11.
- `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj`
  - Identical publish flags: `SelfContained=true`, `PublishSingleFile=true`, etc.
- `scripts/install-mailbridge.ps1`
  - Creates `%LOCALAPPDATA%\OpenClaw\MailBridge\` and seeds `bridge.settings.json` only if absent.
  - Calls `register-mailbridge-task.ps1` to register an on-logon scheduled task with `/it` (interactive) flag.
  - The `/it` flag is key: it forces the task to run in the interactive user session (Session 1+), enabling Outlook COM access.
- `scripts/register-mailbridge-task.ps1`
  - Registers via `schtasks /create /sc onlogon /ru $PrimaryUser /it /f`.
  - Immediately runs the task if the primary user is logged on.
- `global.json`
  - SDK: `10.0.201`, rollForward: `latestFeature`.
- `tests/scripts/register-mailbridge-task.Tests.ps1`
  - Pester v5 tests with `schtasks` shim, `query user` shim, `Mock Test-Path`.
  - Validates `/sc onlogon`, `/it`, `/ru`, and command-line format.
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`
  - MSTest + FluentAssertions + Moq (implied by policy) + coverlet.

### Code Search Results

- `AddWindowsService`
  - Found in `src/OpenClaw.MailBridge/BridgeApplication.cs` line 52. ServiceName = `"OpenClaw.MailBridge"`.
- `Environment.SpecialFolder.LocalApplicationData`
  - Found in `BridgeApplication.cs` (config path) AND `src/OpenClaw.MailBridge.Client/Program.cs` (pipe name resolution from settings).
- `PublishSingleFile`
  - Both `OpenClaw.MailBridge.csproj` and `OpenClaw.MailBridge.Client.csproj` set `PublishSingleFile=true`.
- `/it` flag in `schtasks`
  - Only in `scripts/register-mailbridge-task.ps1`. This is the mechanism that forces interactive-user-session execution.
- No `.wapproj`, no `Package.appxmanifest`, no `makeappx` references — no existing MSIX packaging artifacts.
- No `.github/workflows/` directory in the worktree — no existing CI pipeline.

### Project Conventions

- Standards referenced: `.github/instructions/csharp-code-change.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`, `.github/instructions/csharp-unit-test.instructions.md`, `.github/instructions/github-actions.instructions.md`.
- C# tests: MSTest + FluentAssertions, no xUnit/NUnit.
- PowerShell tests: Pester v5, file naming `*.Tests.ps1`, mirrors script location.
- PowerShell toolchain: PoshQC format → PSScriptAnalyzer → Pester (via MCP functions).
- C# toolchain: csharpier → msbuild (analyzers) → msbuild (nullable) → vstest.
- GitHub Actions: must pass `actionlint`; no structural job changes without explicit request.

---

## Key Discoveries

### Critical Architectural Constraint: Outlook COM + Session 0 Isolation

**This is the most important finding of this research.**

Windows Services (including MSIX `desktop6:Service` entries) run in **Session 0** — the isolated, non-interactive service host. Outlook COM interop **requires the interactive user session (Session 1+)**. Microsoft KB 257757 explicitly states that automating Office applications from a service is not supported.

Evidence from the codebase:
1. `OutlookStaExecutor` creates a dedicated STA thread for Outlook COM work.
2. `BridgeApplication.LoadSettings()` reads config from `Environment.SpecialFolder.LocalApplicationData`, which in Session 0 (LocalSystem) resolves to `C:\Windows\System32\config\systemprofile\AppData\Local` — the wrong path.
3. `scripts/register-mailbridge-task.ps1` uses `schtasks /it` (interactive flag) specifically to run in the user's session.

**Consequence**: A traditional Windows Service (running as `LocalSystem`, `LocalService`, or `NetworkService`) will break the bridge because:
- Outlook COM calls will fail (no user session access).
- Config will be read from/written to the system profile's `LocalApplicationData`, not the user's.
- Named pipe clients in the user session will need cross-session pipe access (additional ACL complexity).

**Recommended resolution**: Use MSIX `windows.startupTask` instead of `windows.service`. A startup task launches the process in the user's interactive session on logon — exactly equivalent to the current `schtasks /sc onlogon /it` mechanism.

### Q1: MSIX + Windows Service Mechanism

The MSIX manifest `Package.appxmanifest` supports Windows Services via the `desktop6` namespace (Windows 10 1903+, build 18362+):

```xml
xmlns:desktop6="http://schemas.microsoft.com/appx/manifest/desktop/windows10/6"
IgnorableNamespaces="desktop6"

<Extensions>
  <desktop6:Extension Category="windows.service"
                      Executable="OpenClaw.MailBridge.exe"
                      EntryPoint="Windows.FullTrustApplication">
    <desktop6:Service Name="OpenClaw.MailBridge"
                      StartupType="auto"
                      StartAccount="localSystem"/>
  </desktop6:Extension>
</Extensions>
```

`StartAccount` options: `localSystem`, `localService`, `networkService`. A user account requires `<desktop6:Credential>` which requires package identity and is not practical for a general-distribution package.

**Trust requirements**: The package must declare `<rescap:Capability Name="runFullTrust"/>` (Restricted Capability). For Store distribution this requires Microsoft approval; for sideloading/enterprise distribution it works with any valid Authenticode certificate.

**HOWEVER**: As established above, `localSystem`/`localService`/`networkService` all run in Session 0 and cannot access Outlook COM or the user's `%LOCALAPPDATA%`. This makes `windows.service` the wrong mechanism for this bridge.

**Correct MSIX mechanism for this use case**: `windows.startupTask`:

```xml
xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"

<Extensions>
  <uap5:Extension Category="windows.startupTask"
                  Executable="OpenClaw.MailBridge.exe"
                  EntryPoint="Windows.FullTrustApplication">
    <uap5:StartupTask TaskId="OpenClawMailBridge"
                      Enabled="true"
                      DisplayName="OpenClaw MailBridge"/>
  </uap5:Extension>
</Extensions>
```

This launches the process in the user's interactive session on logon — identical semantics to the current `schtasks /sc onlogon /it`.

**Trade-off**: `windows.startupTask` does NOT provide automatic restart on crash. To satisfy the acceptance criterion "restarts on failure/reboot", the bridge executable must implement self-restart (e.g., a watchdog loop or a companion manager process). Alternatively, a Windows Service wrapper process (running as LocalSystem) could monitor and re-launch the bridge process in the user session — but this adds architectural complexity.

### Q2: Publishing Strategy

Both projects use `PublishSingleFile=true` + `SelfContained=true` + `EnableCompressionInSingleFile=true`. The single-file publish produces one large `.exe` per project. At runtime, the exe extracts native dependencies to `%TEMP%\...` (via the .NET single-file host).

**MSIX compatibility assessment**:
- A single-file exe CAN be included in an MSIX package — MSIX sees it as a single file.
- The runtime extraction to `%TEMP%` happens outside MSIX's VFS, which is fine — MSIX does not virtualize `%TEMP%`.
- The MSIX VFS covers `%ProgramFiles%`, `%SystemRoot%`, and `%SystemDrive%\ProgramData`. The bridge install root (`C:\Program Files\OpenClaw\MailBridge\`) maps to the package's `VFS\ProgramFilesX64\OpenClaw\MailBridge\`.

**Recommended approach**: Add a separate MSIX publish profile with `PublishSingleFile=false` (keep `SelfContained=true`). This produces a directory layout with all DLLs and files enumerated, which:
1. Lets MSIX enumerate all files for its manifest `<Files>` section.
2. Avoids the startup extraction overhead in production.
3. Makes MSIX repair work cleanly (all files are individually tracked).

**Publish profile changes needed** (new file `src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml`):
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>x64</Platform>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishDir>$(MSBuildThisFileDirectory)..\..\..\..\artifacts\publish\bridge\</PublishDir>
  </PropertyGroup>
</Project>
```

Similarly for the client project.

### Q3: WAP vs makeappx.exe Directly

**Windows Application Packaging Project (`.wapproj`)** requires Visual Studio tooling (`Microsoft.DesktopBridge.props`) and is not reliably buildable with `dotnet` CLI or `msbuild` alone without VS installed. Not recommended for CI-first.

**makeappx.exe + MakePri.exe directly** is the CI-friendly approach:
- `makeappx.exe` is available via the Windows SDK (`C:\Program Files (x86)\Windows Kits\10\bin\{version}\x64\makeappx.exe`).
- `windows-latest` GitHub Actions runners include the Windows SDK — `makeappx.exe` and `signtool.exe` are available without additional installation.
- `Microsoft.Windows.SDK.BuildTools` NuGet package (v10.0.x) ships `makeappx.exe`, `signtool.exe`, and `MakePri.exe` and can be used in a CI pipeline without a full SDK install.

**Recommended toolchain** (CI-first, no VS dependency):
1. `dotnet publish` with MSIX profile → publish directory per project
2. Assemble MSIX layout directory (`AppxManifest.xml` + published files)
3. `MakePri.exe createconfig` + `MakePri.exe new` to generate `resources.pri` (required even for non-localized apps)
4. `makeappx.exe pack` → `.msix`
5. `signtool.exe sign` → signed `.msix`

The packaging script (`scripts/build-msix.ps1`) drives all steps. No `.wapproj` required.

### Q4: Service Account / Session Model

As established in the architectural constraint section, **the bridge CANNOT run as LocalSystem/LocalService/NetworkService** because Outlook COM requires the interactive user session.

**Recommended model**: `windows.startupTask` (user-session process, not a service).

If the acceptance criterion "Windows Service named `OpenClaw MailBridge` exists" must be met literally, the only viable pattern is:
- **Hybrid**: A thin `LocalSystem` Windows Service (`desktop6:Extension Category="windows.service"`) that calls `CreateProcessAsUser` to spawn the bridge in the logged-on user's session. This is a complex pattern used by some desktop app launchers but adds significant implementation complexity and requires `SeImpersonatePrivilege`.
- **Verdict**: Not recommended for this project. Use `startupTask` and address the "service restart on crash" requirement via internal watchdog logic.

For the `windows.startupTask` approach, no service account is needed — the process runs as the logged-in user, accessing their Outlook profile and `%LOCALAPPDATA%` natively.

### Q5: Config Seeding

**No MSIX custom action required.** The bridge already self-seeds:

```csharp
// BridgeApplication.cs lines 66–73
if (!SettingsStoreExists(path))
{
    WriteSettingsStore(
        path,
        JsonSerializer.Serialize(BridgeSettings.Default, new JsonSerializerOptions { WriteIndented = true })
    );
    return BridgeSettings.Default;
}
```

On first launch (triggered by the startup task on logon), the bridge writes `bridge.settings.json` if absent. `%LOCALAPPDATA%` is NOT virtualized by MSIX — writes go to the real user profile path. This is by design: MSIX only virtualizes `%ProgramFiles%`, `%SystemRoot%`, and `%ProgramData%`.

The existing `install-mailbridge.ps1` also seeds config before registering the task. For MSIX, the app's self-seeding is sufficient — no additional mechanism needed.

**Important**: The `install-mailbridge.ps1` script checks for a `runtimeconfig.json` file alongside the exe (`Assert-DotNet10RuntimeConfig`). With `PublishSingleFile=false` for the MSIX build, the `runtimeconfig.json` will exist. With `PublishSingleFile=true`, it does NOT exist (it's bundled inside the exe). The MSIX publish profile should use `PublishSingleFile=false` to preserve the runtimeconfig for the PowerShell script side-by-side compatibility.

### Q6: Upgrade / Repair / Uninstall Behavior

**Identity fields governing upgrade**:
```xml
<Identity Name="OpenClaw.MailBridge"
          Publisher="CN=OpenClaw, O=OpenClaw, C=US"
          Version="1.0.0.0"
          ProcessorArchitecture="x64"/>
```

- `Name` + `Publisher` must be **identical** across versions for in-place upgrade.
- `Version` must be **strictly higher** (semantic: `Major.Minor.Build.Revision`, all uint16) for upgrade to replace the existing installation.
- `ProcessorArchitecture` must match (`x64` for self-contained .NET on 64-bit Windows).

**Upgrade behavior**: MSIX upgrade is atomic — new package is staged, then swapped. The startup task registration is preserved automatically (MSIX manages task lifecycle). User data in `%LOCALAPPDATA%` is never touched by upgrade.

**Repair behavior**: `winget repair` or Settings > Apps > Repair reinstalls package files to original state. User config in `%LOCALAPPDATA%` is unaffected.

**Uninstall behavior**: MSIX removes all files in the package VFS and deregisters any declared extensions (startup tasks, services). User config (`%LOCALAPPDATA%\OpenClaw\MailBridge\`) is NOT removed — consistent with the existing `uninstall-mailbridge.ps1` behavior ("Cache, logs, and settings were intentionally left in place").

### Q7: CI Build Pipeline

GitHub Actions using `windows-latest` runner (which has Windows SDK 10.x pre-installed):

```yaml
name: build-msix
on:
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      version:
        required: true
        default: '1.0.0.0'

jobs:
  msix:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Publish bridge host
        run: |
          dotnet publish src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained `
            -p:PublishSingleFile=false `
            -p:PublishReadyToRun=true `
            --output artifacts/publish/bridge

      - name: Publish client
        run: |
          dotnet publish src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained `
            -p:PublishSingleFile=false `
            -p:PublishReadyToRun=true `
            --output artifacts/publish/client

      - name: Create MSIX package
        run: scripts/build-msix.ps1 -Version '${{ github.event.inputs.version }}' -OutputDir artifacts/msix

      - name: Create and import self-signed cert (CI only)
        run: scripts/New-MsixDevCert.ps1 -Thumbprint (scripts/build-msix.ps1 -GetThumbprint)
        # For production: use Azure Trusted Signing Action

      - name: Sign MSIX
        run: |
          $sdk = (Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Directory | 
            Where-Object Name -match '^\d' | Sort-Object Name -Descending | Select-Object -First 1).FullName
          & "$sdk\x64\signtool.exe" sign /fd SHA256 /a artifacts/msix/OpenClaw.MailBridge.msix

      - name: Upload MSIX artifact
        uses: actions/upload-artifact@v4
        with:
          name: msix-package
          path: artifacts/msix/*.msix
```

**`Microsoft.Windows.SDK.BuildTools` alternative** (if `windows-latest` SDK version is insufficient):
```xml
<!-- In a packaging .csproj -->
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.1742" />
```
This ships `makeappx.exe` and `signtool.exe` to the NuGet packages directory, callable from the build script.

### Q8: Current Codebase Impact

**`Program.cs` (bridge host)**:
- Does NOT call `UseWindowsService()` directly — delegates to `BridgeApplication.RunAsync()`.
- `BridgeApplication.BuildHost()` calls `AddWindowsService()` ✅ — already correct.
- No changes needed to `Program.cs`.

**`BridgeApplication.cs`**:
- `AddWindowsService()` is present ✅.
- Config path via `Environment.SpecialFolder.LocalApplicationData` ✅ (works correctly for user-session startup task).
- No changes needed for the startup-task model.
- **If** a true Windows Service model were pursued (not recommended), the config path resolution would need to accept a `--config` override to point to the user's profile path.

**Other files needing changes**:
- `scripts/install-mailbridge.ps1` — The `Assert-DotNet10RuntimeConfig` check needs to be conditional or skipped when the MSIX version (no runtimeconfig.json with single-file publish) is being used. Since MSIX uses `PublishSingleFile=false`, runtimeconfig.json WILL exist — no change needed.
- `scripts/uninstall-mailbridge.ps1` — Side-by-side with MSIX; no change needed (MSIX uninstall is via Settings/winget).
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` — No changes needed; MSIX-specific publish flags go in the publish profile, not the project file.

**New files to create**:
- `installer/Package.appxmanifest` — MSIX manifest with `startupTask` extension
- `installer/AppxManifest.xml` — Symlink or copy of manifest for makeappx
- `installer/Assets/` — MSIX tile icons (44x44, 150x150, etc.)
- `scripts/build-msix.ps1` — CI packaging script
- `scripts/New-MsixDevCert.ps1` — Dev/CI self-signed cert helper
- `src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml` — MSIX publish profile
- `src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml` — Client publish profile
- `.github/workflows/build-msix.yml` — GitHub Actions workflow
- `tests/scripts/build-msix.Tests.ps1` — Pester tests for build-msix.ps1 helpers
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` — MSTest manifest/layout validation

### Q9: Signing

**Development / testing (local)** — self-signed cert:
```powershell
# New-MsixDevCert.ps1
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=OpenClaw Dev, O=OpenClaw, C=US" `
    -KeyUsage DigitalSignature `
    -FriendlyName "OpenClaw MSIX Dev Cert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export PFX for CI
$pwd = ConvertTo-SecureString "devpassword" -Force -AsPlainText
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
    -FilePath "artifacts/openclaw-dev.pfx" -Password $pwd

# Install to Trusted Root so the signed MSIX can be installed on this machine
Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath "artifacts/openclaw-dev.cer"
Import-Certificate -FilePath "artifacts/openclaw-dev.cer" -CertStoreLocation "Cert:\LocalMachine\Root"
```

The `Package.appxmanifest` `Publisher` attribute MUST match the certificate Subject exactly.

**CI (GitHub Actions)** — ephemeral self-signed cert per build:
```yaml
- name: Create CI signing cert
  id: cert
  shell: pwsh
  run: |
    $cert = New-SelfSignedCertificate -Type Custom `
      -Subject "CN=OpenClaw CI, O=OpenClaw, C=US" `
      -KeyUsage DigitalSignature `
      -FriendlyName "OpenClaw MSIX CI" `
      -CertStoreLocation "Cert:\CurrentUser\My" `
      -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
    echo "thumbprint=$($cert.Thumbprint)" >> $env:GITHUB_OUTPUT
```

**Production** — use `azure/trusted-signing-action@v0` (Azure Trusted Signing service, replaces EV cert model) or export a secret PFX stored as a GitHub Actions secret and import it at build time.

**Note**: Self-signed MSIX packages can only be installed on machines that have the signing cert in their Trusted Root store. For distribution, a commercial code-signing cert or Azure Trusted Signing is required.

### Q10: Test Strategy

**New Pester tests** (`tests/scripts/build-msix.Tests.ps1`):
- `Describe 'build-msix.ps1'`
  - `It 'generates correct AppxManifest.xml Version field from semver input'`
  - `It 'fails explicitly when publish directory is missing'`
  - `It 'assembles expected file layout in staging directory'` (mock file copy, verify structure)
  - `It 'calls makeappx.exe with correct /d and /p arguments'` (shim makeappx.exe)
  - `It 'skips signing step when -SkipSign is passed'`

**New Pester tests** (`tests/scripts/New-MsixDevCert.Tests.ps1`):
- `It 'creates a certificate with correct Subject CN'`
- `It 'exports PFX to the specified output path'`

**New MSTest tests** (`tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`):
- `Verify_AppxManifest_Version_Matches_Assembly_Version` — parse `Package.appxmanifest` XML, confirm `Version` attribute is parseable as 4-part version.
- `Verify_StartupTask_Extension_Is_Declared` — parse manifest, confirm `startupTask` extension exists with correct `Executable`.
- `Verify_Published_Bridge_Exe_Exists_In_Publish_Output` — integration test (run only when `MSIX_PUBLISH_DIR` env var is set); confirms `OpenClaw.MailBridge.exe` and `OpenClaw.MailBridge.Client.exe` are present in the publish output.
- `Verify_BridgeApplication_Configures_WindowsService` — already partly covered by `MailBridgeRuntimeTests`; add explicit check that `IHostLifetime` is `WindowsServiceLifetime` when built with `AddWindowsService()`.

---

## Recommended Approach

### Architecture: `windows.startupTask` (not `windows.service`)

Given the Outlook COM Session 0 constraint, the MSIX package will use `windows.startupTask` to launch the bridge in the interactive user session on logon. This is semantically identical to the current `schtasks /sc onlogon /it` mechanism.

The acceptance criterion "Windows Service named `OpenClaw MailBridge` exists, is set to Automatic start" must be **revised** to: "The bridge process starts automatically on user logon via the MSIX startup task." Running as a Windows Service is architecturally incompatible with Outlook COM interop.

For crash restart semantics (equivalent to service restart on failure), the bridge can implement a self-restart loop in `Program.cs`, or a separate watchdog startup task can be declared.

### Package Manifest Skeleton

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap uap5 rescap">

  <Identity Name="OpenClaw.MailBridge"
            Publisher="CN=OpenClaw, O=OpenClaw, C=US"
            Version="1.0.0.0"
            ProcessorArchitecture="x64"/>

  <Properties>
    <DisplayName>OpenClaw MailBridge</DisplayName>
    <PublisherDisplayName>OpenClaw</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0"/>
  </Dependencies>

  <Resources>
    <Resource Language="en-US"/>
  </Resources>

  <Applications>
    <Application Id="OpenClaw.MailBridge"
                 Executable="bridge\OpenClaw.MailBridge.exe"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="OpenClaw MailBridge"
                          Description="Outlook COM bridge over named pipe"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png"
                          BackgroundColor="transparent">
        <uap:DefaultTile/>
        <uap:SplashScreen Image="Assets\SplashScreen.png"/>
      </uap:VisualElements>
      <Extensions>
        <uap5:Extension Category="windows.startupTask"
                        Executable="bridge\OpenClaw.MailBridge.exe"
                        EntryPoint="Windows.FullTrustApplication">
          <uap5:StartupTask TaskId="OpenClawMailBridge"
                            Enabled="true"
                            DisplayName="OpenClaw MailBridge"/>
        </uap5:Extension>
      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust"/>
  </Capabilities>
</Package>
```

`OpenClaw.MailBridge.Client.exe` is packaged inside the same MSIX under `client\` and is not declared as a separate Application (it is a CLI tool invoked by the user, not launched by the system).

### MSIX Layout Directory Structure

```
installer/
├── Package.appxmanifest           # Canonical manifest (source of truth)
├── Assets/
│   ├── Square44x44Logo.png        # 44x44 app icon (required)
│   ├── Square150x150Logo.png      # 150x150 app icon (required)
│   ├── StoreLogo.png              # 50x50 store icon
│   └── SplashScreen.png           # 620x300 splash screen
└── staging/                       # Built by build-msix.ps1 (gitignored)
    ├── AppxManifest.xml           # Version-stamped copy of Package.appxmanifest
    ├── resources.pri              # Generated by MakePri.exe
    ├── Assets/                    # Icons copied from installer/Assets/
    ├── bridge/                    # Output of bridge host dotnet publish
    │   ├── OpenClaw.MailBridge.exe
    │   ├── OpenClaw.MailBridge.dll
    │   └── ... (all SelfContained DLLs)
    └── client/                    # Output of client dotnet publish
        ├── OpenClaw.MailBridge.Client.exe
        └── ...
```

### CI Script Flow (`scripts/build-msix.ps1`)

```powershell
param(
    [string]$Version = '1.0.0.0',
    [string]$OutputDir = 'artifacts/msix',
    [string]$CertThumbprint,
    [switch]$SkipSign
)

# 1. Validate version format (4-part)
# 2. Stamp version into a copy of installer/Package.appxmanifest -> staging/AppxManifest.xml
# 3. Copy Assets/ to staging/Assets/
# 4. Copy bridge publish output -> staging/bridge/
# 5. Copy client publish output -> staging/client/
# 6. Run MakePri.exe createconfig + new -> staging/resources.pri
# 7. Run makeappx.exe pack /d staging/ /p artifacts/msix/OpenClaw.MailBridge_{Version}_x64.msix /o
# 8. Unless -SkipSign: Run signtool.exe sign /sha1 $CertThumbprint /fd SHA256 /v <msix>
```

### Rejected Alternatives

- **`.wapproj` (Windows Application Packaging Project)**: Requires Visual Studio MSBuild targets not available in `dotnet` CLI on CI runners without VS installed. Adds VS dependency to CI chain. Not recommended for CI-first approach.
- **`windows.service` in MSIX manifest**: Architecturally incompatible with Outlook COM interop (Session 0 isolation). Would break `%LOCALAPPDATA%` config resolution and all COM calls. Rejected in favor of `windows.startupTask`.
- **Package Support Framework (PSF)**: A Microsoft shim layer for legacy app compatibility. Not needed here — the bridge is a modern .NET app that already handles its own config seeding. PSF adds complexity without benefit.
- **Hybrid LocalSystem service + `CreateProcessAsUser`**: Would allow "real" Windows Service semantics while launching the bridge in the user session. Technically viable but introduces P/Invoke complexity, `SeImpersonatePrivilege` requirements, and significant surface area for security issues. Not recommended for this project scope.

---

## Implementation Guidance

- **Objectives**:
  1. Produce a signed `.msix` package that installs both executables and registers a startup task.
  2. Maintain backward compatibility with the existing PowerShell script installation path.
  3. Support in-place upgrade, repair, and uninstall via standard Windows MSIX lifecycle.
  4. Build entirely from CI without Visual Studio.

- **Key Tasks**:
  1. Create `installer/Package.appxmanifest` with `startupTask` extension.
  2. Create MSIX asset icons (minimum: 44x44, 150x150, StoreLogo, SplashScreen — can be placeholders for dev).
  3. Create `scripts/build-msix.ps1` (version stamping, layout assembly, makeappx, signtool).
  4. Create `scripts/New-MsixDevCert.ps1` (dev/CI self-signed cert helper).
  5. Add MSIX publish profiles for both projects (`PublishSingleFile=false`, `SelfContained=true`).
  6. Create `.github/workflows/build-msix.yml` (trigger on tags, upload artifact).
  7. Update `plan.md` with phased atomic tasks reflecting the above.
  8. Add Pester tests for `build-msix.ps1` and `New-MsixDevCert.ps1`.
  9. Add MSTest tests for manifest validation.
  10. Revise acceptance criteria in `spec.md` and `user-story.md`: replace "Windows Service" with "startup task" and document the Session 0 constraint.

- **Dependencies**:
  - `windows-latest` GitHub Actions runner (includes Windows SDK with `makeappx.exe`, `signtool.exe`, `MakePri.exe`).
  - .NET 10 SDK (already pinned via `global.json`).
  - No new NuGet packages required for core packaging (SDK tools are runner-provided).
  - Optional: `Microsoft.Windows.SDK.BuildTools` NuGet package if runner SDK version is insufficient.

- **Blockers / Risks**:
  1. **CRITICAL**: Acceptance criteria require "Windows Service" — this must be revised to "startup task" before implementation. Running as a Windows Service breaks Outlook COM. Needs stakeholder sign-off on the architecture change.
  2. **Crash restart**: `windows.startupTask` does not restart on crash. Either implement a watchdog loop in the bridge or document this as a known limitation vs. the service model.
  3. **Publisher identity**: The `Publisher` CN in the manifest must match the signing cert Subject exactly. For production distribution, a commercial cert or Azure Trusted Signing must be procured before a public release.
  4. **`runFullTrust` capability**: Required for startup tasks and full-trust execution. For Store distribution this needs Microsoft approval. For sideloading/enterprise deployment it works without restriction.
  5. **Icon assets**: MSIX requires specific icon sizes. Placeholder icons must be created before the package can be built even in dev mode.
  6. **`MinVersion` in manifest**: Set to `10.0.17763.0` (Windows 10 1809) for broad compatibility. The `windows.startupTask` feature is available from Windows 10 1803 (17134).

- **Success Criteria**:
  - `scripts/build-msix.ps1` produces a valid `.msix` when run after `dotnet publish`.
  - The signed MSIX installs on a clean Windows 10/11 machine via `Add-AppxPackage`.
  - After install, the bridge process starts on next user logon (visible in Task Manager).
  - `OpenClaw.MailBridge.Client.exe status` returns non-empty JSON after the bridge starts.
  - Installing a higher-versioned MSIX over an existing installation preserves `bridge.settings.json`.
  - Uninstalling via Settings > Apps removes binaries and the startup task, leaves user config.
  - All new Pester tests pass (`mcp_drmcopilotext_run_poshqc_test`).
  - All new MSTest tests pass (`dotnet test`).
  - CI workflow produces an `.msix` artifact on tag push.