# 2026-04-10-msix-installer-package - Plan

- **Issue:** #17
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-11T00-00
- **Status:** Active
- **Version:** 0.2

## Required References

- C# Coding Standards: [`.github/instructions/csharp-code-change.instructions.md`](../../../../.github/instructions/csharp-code-change.instructions.md)
- C# Unit Test Policy: [`.github/instructions/csharp-unit-test.instructions.md`](../../../../.github/instructions/csharp-unit-test.instructions.md)
- PowerShell Coding Standards: [`.github/instructions/powershell-code-change.instructions.md`](../../../../.github/instructions/powershell-code-change.instructions.md)
- PowerShell Unit Test Policy: [`.github/instructions/powershell-unit-test.instructions.md`](../../../../.github/instructions/powershell-unit-test.instructions.md)
- GitHub Actions Policy: [`.github/instructions/github-actions.instructions.md`](../../../../.github/instructions/github-actions.instructions.md)

**All work must comply with these policies; do not duplicate their content here.**

## Overview

Produce a signed MSIX installer package for OpenClaw MailBridge that installs both the bridge host (`OpenClaw.MailBridge.exe`) and client CLI (`OpenClaw.MailBridge.Client.exe`), and registers the bridge to start automatically on user logon via an MSIX `windows.startupTask` extension. The package is built entirely from CI using `dotnet publish` + Windows SDK tools (`makeappx.exe`, `signtool.exe`, `MakePri.exe`); no Visual Studio is required. All work is **additive** — new files only; no existing source files are modified.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Context

- [x] [P0-T1] Read all five policy instruction files before making any changes
  - Files: `.github/instructions/csharp-code-change.instructions.md`, `.github/instructions/csharp-unit-test.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`, `.github/instructions/github-actions.instructions.md`
  - Acceptance: `(Get-ChildItem .github/instructions/csharp-code-change.instructions.md, .github/instructions/csharp-unit-test.instructions.md, .github/instructions/powershell-code-change.instructions.md, .github/instructions/powershell-unit-test.instructions.md, .github/instructions/github-actions.instructions.md | Where-Object { $_.Length -gt 0 }).Count -eq 5` returns `True`

- [x] [P0-T2] Run CSharpier baseline format check and confirm the repository is clean
  - Command: `dotnet tool run csharpier . --check`
  - Acceptance: exits with code 0

- [x] [P0-T3] Run MSBuild analyzer baseline build and confirm no analyzer errors
  - Command: `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
  - Acceptance: exits with code 0; output contains `Build succeeded`

- [x] [P0-T4] Run MSBuild nullable/TreatWarningsAsErrors baseline build and confirm no warnings
  - Command: `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true`
  - Acceptance: exits with code 0; output contains `Build succeeded`

- [x] [P0-T5] Run the full dotnet test suite and record the baseline passing test count
  - Command: `dotnet test --settings mailbridge.runsettings`
  - Acceptance: exits with code 0; total passing test count is noted in the conversation before Phase 1 begins (this count is used in P6-T7 for regression comparison)

- [x] [P0-T6] Run PoshQC format baseline on existing PowerShell files in `scripts/` and `tests/scripts/`
  - Command: `mcp_drmcopilotext_run_poshqc_format` targeting `scripts/` and `tests/scripts/`
  - Acceptance: exits with code 0; no formatting violations reported

- [x] [P0-T7] Run PoshQC analyze baseline on existing PowerShell files in `scripts/` and `tests/scripts/`
  - Command: `mcp_drmcopilotext_run_poshqc_analyze` targeting `scripts/` and `tests/scripts/`
  - Acceptance: exits with code 0; no PSScriptAnalyzer violations reported

- [x] [P0-T8] Run PoshQC test baseline on existing Pester files in `tests/scripts/`
  - Command: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/`
  - Acceptance: exits with code 0; all existing Pester tests appear as `Passed`

### Phase 1 — MSIX Manifest & Icon Assets

- [x] [P1-T1] Create `installer/Assets/Square44x44Logo.png` as a valid 44x44 pixel PNG placeholder image
  - Acceptance: `Test-Path installer/Assets/Square44x44Logo.png` returns `True`; `(Get-Item installer/Assets/Square44x44Logo.png).Length -gt 100` returns `True`; `([System.IO.File]::ReadAllBytes('installer/Assets/Square44x44Logo.png')[0..3] -join ',') -eq '137,80,78,71'` returns `True` (PNG magic bytes present)

- [x] [P1-T2] Create `installer/Assets/Square150x150Logo.png` as a valid 150x150 pixel PNG placeholder image
  - Acceptance: `Test-Path installer/Assets/Square150x150Logo.png` returns `True`; `([System.IO.File]::ReadAllBytes('installer/Assets/Square150x150Logo.png')[0..3] -join ',') -eq '137,80,78,71'` returns `True`

- [x] [P1-T3] Create `installer/Assets/Wide310x150Logo.png` as a valid 310x150 pixel PNG placeholder image
  - Acceptance: `Test-Path installer/Assets/Wide310x150Logo.png` returns `True`; `([System.IO.File]::ReadAllBytes('installer/Assets/Wide310x150Logo.png')[0..3] -join ',') -eq '137,80,78,71'` returns `True`

- [x] [P1-T4] Create `installer/Assets/StoreLogo.png` as a valid 50x50 pixel PNG placeholder image
  - Acceptance: `Test-Path installer/Assets/StoreLogo.png` returns `True`; `([System.IO.File]::ReadAllBytes('installer/Assets/StoreLogo.png')[0..3] -join ',') -eq '137,80,78,71'` returns `True`

- [x] [P1-T5] Create `installer/Package.appxmanifest` declaring: `Identity` (`Name=OpenClaw.MailBridge`, `Publisher=CN=OpenClaw, O=OpenClaw, C=US`, `Version=1.0.0.0`, `ProcessorArchitecture=x64`); `TargetDeviceFamily` `MinVersion=10.0.17763.0`; `rescap:Capability Name="runFullTrust"`; and `uap5:Extension Category="windows.startupTask"` with `TaskId="OpenClawMailBridge"`, `Enabled="true"`, `DisplayName="OpenClaw MailBridge"`, `Executable="bridge\OpenClaw.MailBridge.exe"`
  - Acceptance: `Test-Path installer/Package.appxmanifest` returns `True`; `$null = [xml](Get-Content installer/Package.appxmanifest)` executes without error (exit code 0); `Select-String -Path installer/Package.appxmanifest -Pattern 'OpenClawMailBridge' -Quiet` returns `True`; `Select-String -Path installer/Package.appxmanifest -Pattern 'windows\.service' -Quiet` returns `False`

- [x] [P1-T6] Append `installer/staging/` to `.gitignore` if not already present, to prevent ephemeral staging files from being committed
  - Acceptance: `Select-String -Path .gitignore -Pattern 'installer/staging' -Quiet` returns `True`; `git check-ignore -v installer/staging/` exits with code 0

- [x] [P1-T7] Append `artifacts/msix/` and `artifacts/publish/` to `.gitignore` if not already present, to exclude CI build outputs from version control
  - Acceptance: `Select-String -Path .gitignore -Pattern 'artifacts/msix' -Quiet` returns `True`; `Select-String -Path .gitignore -Pattern 'artifacts/publish' -Quiet` returns `True`

### Phase 2 — Directory Publish Profiles

- [x] [P2-T1] Create `src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml` with `PublishSingleFile=false`, `SelfContained=true`, `RuntimeIdentifier=win-x64`, `PublishReadyToRun=true`, and `PublishDir` targeting `artifacts/publish/bridge/` relative to the solution root
  - Acceptance: `Test-Path src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml` returns `True`; `Select-String -Path src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml -Pattern '<PublishSingleFile>false</PublishSingleFile>' -Quiet` returns `True`; `Select-String -Path src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml -Pattern '<SelfContained>true</SelfContained>' -Quiet` returns `True`; `Select-String -Path src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml -Pattern '<RuntimeIdentifier>win-x64</RuntimeIdentifier>' -Quiet` returns `True`

- [x] [P2-T2] Create `src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml` with `PublishSingleFile=false`, `SelfContained=true`, `RuntimeIdentifier=win-x64`, `PublishReadyToRun=true`, and `PublishDir` targeting `artifacts/publish/client/` relative to the solution root
  - Acceptance: `Test-Path src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml` returns `True`; `Select-String -Path src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml -Pattern '<PublishSingleFile>false</PublishSingleFile>' -Quiet` returns `True`; `Select-String -Path src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml -Pattern '<RuntimeIdentifier>win-x64</RuntimeIdentifier>' -Quiet` returns `True`

- [x] [P2-T3] Verify the bridge `msix.pubxml` profile produces a directory-layout publish output containing `OpenClaw.MailBridge.exe` and `OpenClaw.MailBridge.runtimeconfig.json`
  - Command: `dotnet publish src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj /p:PublishProfile=msix`
  - Acceptance: exits with code 0; `Test-Path artifacts/publish/bridge/OpenClaw.MailBridge.exe` returns `True`; `Test-Path artifacts/publish/bridge/OpenClaw.MailBridge.runtimeconfig.json` returns `True`; `(Get-ChildItem artifacts/publish/bridge/ -File).Count -gt 5` returns `True` (directory layout, not a single binary)

- [x] [P2-T4] Verify the client `msix.pubxml` profile produces a directory-layout publish output containing `OpenClaw.MailBridge.Client.exe` and `OpenClaw.MailBridge.Client.runtimeconfig.json`
  - Command: `dotnet publish src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj /p:PublishProfile=msix`
  - Acceptance: exits with code 0; `Test-Path artifacts/publish/client/OpenClaw.MailBridge.Client.exe` returns `True`; `Test-Path artifacts/publish/client/OpenClaw.MailBridge.Client.runtimeconfig.json` returns `True`

### Phase 3 — PowerShell Packaging Scripts

- [x] [P3-T1] Create `scripts/New-MsixDevCert.ps1` skeleton with `#Requires -Version 5.1`, `[CmdletBinding()]`, and parameters: `-Subject` (string, default `'CN=OpenClaw, O=OpenClaw, C=US'`), `-OutputDir` (string, default `'artifacts'`), `-PfxPassword` (SecureString, mandatory)
  - Acceptance: `Test-Path scripts/New-MsixDevCert.ps1` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern '\[CmdletBinding\(\)\]' -Quiet` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'PfxPassword' -Quiet` returns `True`

- [x] [P3-T2] Implement the `New-SelfSignedCertificate` invocation in `New-MsixDevCert.ps1` using `CertStoreLocation='Cert:\CurrentUser\My'`, `Type=CodeSigningCert`, `KeyUsage=DigitalSignature`, and `-Subject $Subject`
  - Acceptance: `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'New-SelfSignedCertificate' -Quiet` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'CodeSigningCert' -Quiet` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'Subject \$Subject' -Quiet` returns `True`

- [x] [P3-T3] Implement PFX and CER export logic in `New-MsixDevCert.ps1` using `Export-PfxCertificate` writing to `<OutputDir>/OpenClaw.MailBridge.pfx` and `Export-Certificate` writing to `<OutputDir>/OpenClaw.MailBridge.cer`
  - Acceptance: `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'Export-PfxCertificate' -Quiet` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'Export-Certificate' -Quiet` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'OpenClaw\.MailBridge\.pfx' -Quiet` returns `True`

- [x] [P3-T4] Implement Trusted Root import in `New-MsixDevCert.ps1` via `Import-Certificate` targeting `Cert:\LocalMachine\Root` to install the signing cert for local sideloading
  - Acceptance: `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'Import-Certificate' -Quiet` returns `True`; `Select-String -Path scripts/New-MsixDevCert.ps1 -Pattern 'LocalMachine' -Quiet` returns `True`

- [x] [P3-T5] Create `scripts/build-msix.ps1` skeleton with `#Requires -Version 5.1`, `$ErrorActionPreference = 'Stop'`, `[CmdletBinding()]`, and parameters: `-Version` (string, default `'1.0.0.0'`), `-OutputDir` (string, default `'artifacts/msix'`), `-CertThumbprint` (string), `-SkipSign` (switch)
  - Acceptance: `Test-Path scripts/build-msix.ps1` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'ErrorActionPreference' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'SkipSign' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'CertThumbprint' -Quiet` returns `True`

- [x] [P3-T6] Implement `Invoke-VersionStamp` helper function in `build-msix.ps1` that reads `installer/Package.appxmanifest`, replaces the `Version` attribute in the `Identity` element with the `-Version` parameter value, and writes the stamped result to `installer/staging/AppxManifest.xml`
  - Acceptance: `Select-String -Path scripts/build-msix.ps1 -Pattern 'function Invoke-VersionStamp' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'AppxManifest\.xml' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'Identity' -Quiet` returns `True`

- [x] [P3-T7] Implement `Invoke-LayoutAssembly` helper function in `build-msix.ps1` that: (a) validates `artifacts/publish/bridge/` and `artifacts/publish/client/` exist and throws a terminating error if either is absent; (b) copies bridge and client publish outputs to `installer/staging/bridge/` and `installer/staging/client/`; (c) copies `installer/Assets/` to `installer/staging/Assets/`
  - Acceptance: `Select-String -Path scripts/build-msix.ps1 -Pattern 'function Invoke-LayoutAssembly' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'throw' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'artifacts/publish/bridge' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'installer/staging/bridge' -Quiet` returns `True`

- [x] [P3-T8] Implement `Invoke-MakePri` helper function in `build-msix.ps1` that locates `MakePri.exe` from the Windows SDK path, runs `makepri createconfig` to generate a default PRI configuration, then runs `makepri new` to produce `installer/staging/resources.pri`
  - Acceptance: `Select-String -Path scripts/build-msix.ps1 -Pattern 'function Invoke-MakePri' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'MakePri' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'resources\.pri' -Quiet` returns `True`

- [x] [P3-T9] Implement `Invoke-MakeAppx` helper function in `build-msix.ps1` that locates `makeappx.exe` from the Windows SDK path and invokes it with `pack /d installer/staging /p <OutputDir>/OpenClaw.MailBridge_<Version>_x64.msix /nv` arguments
  - Acceptance: `Select-String -Path scripts/build-msix.ps1 -Pattern 'function Invoke-MakeAppx' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'makeappx' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern '/nv' -Quiet` returns `True`

- [x] [P3-T10] Implement `Invoke-SignTool` helper function in `build-msix.ps1`, gated on `(-not $SkipSign)`, that invokes `signtool.exe sign /sha1 $CertThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 <msix-path>`
  - Acceptance: `Select-String -Path scripts/build-msix.ps1 -Pattern 'function Invoke-SignTool' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'signtool' -Quiet` returns `True`; `Select-String -Path scripts/build-msix.ps1 -Pattern 'SkipSign' -Quiet` returns `True`

- [x] [P3-T11] Implement the main orchestration body of `build-msix.ps1` that calls `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, and `Invoke-SignTool` in that order, each preceded by a `Write-Information` progress message
  - Acceptance: `(Select-String -Path scripts/build-msix.ps1 -Pattern 'Invoke-VersionStamp|Invoke-LayoutAssembly|Invoke-MakePri|Invoke-MakeAppx|Invoke-SignTool').Count -ge 10` returns `True` (each function defined once and called once equals 10 matches minimum); `Select-String -Path scripts/build-msix.ps1 -Pattern 'Write-Information' -Quiet` returns `True`

### Phase 4 — CI GitHub Actions Workflow

- [ ] [P4-T1] Create `.github/workflows/build-msix.yml` with `name: Build MSIX Package`, triggers on `push: tags: ['v*']` and `workflow_dispatch` with a required `version` string input (default `'1.0.0.0'`), and a job named `build-msix` running on `windows-latest`
  - Acceptance: `Test-Path .github/workflows/build-msix.yml` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern "v\*" -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'workflow_dispatch' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'windows-latest' -Quiet` returns `True`

- [ ] [P4-T2] Add `actions/checkout@v4` and `actions/setup-dotnet@v4` steps with `dotnet-version: '10.0.x'` to the `build-msix` job
  - Acceptance: `Select-String -Path .github/workflows/build-msix.yml -Pattern 'actions/checkout@v4' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'actions/setup-dotnet@v4' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern '10\.0\.x' -Quiet` returns `True`

- [ ] [P4-T3] Add `dotnet publish` steps to the workflow for `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client`, each specifying `/p:PublishProfile=msix`
  - Acceptance: `(Select-String -Path .github/workflows/build-msix.yml -Pattern 'dotnet publish').Count -ge 2` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'PublishProfile=msix' -Quiet` returns `True`

- [ ] [P4-T4] Add a PowerShell step to the workflow that runs `scripts/New-MsixDevCert.ps1` (CI dev cert creation) followed by `scripts/build-msix.ps1` with `-Version` sourced from `github.ref_name` on tag push or `github.event.inputs.version` on dispatch, and `-CertThumbprint` from the cert step output
  - Acceptance: `Select-String -Path .github/workflows/build-msix.yml -Pattern 'New-MsixDevCert\.ps1' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'build-msix\.ps1' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'github\.ref_name' -Quiet` returns `True`

- [ ] [P4-T5] Add `actions/upload-artifact@v4` step to the workflow that uploads `artifacts/msix/*.msix` as the artifact named `msix-package`
  - Acceptance: `Select-String -Path .github/workflows/build-msix.yml -Pattern 'upload-artifact' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'msix-package' -Quiet` returns `True`; `Select-String -Path .github/workflows/build-msix.yml -Pattern 'artifacts/msix' -Quiet` returns `True`

### Phase 5 — Tests & Documentation

#### Pester: `tests/scripts/build-msix.Tests.ps1`

- [x] [P5-T1] Create `tests/scripts/build-msix.Tests.ps1` with a `Describe 'build-msix.ps1'` block and a `BeforeAll` that dot-sources `scripts/build-msix.ps1` and defines shims for `makeappx`, `signtool`, and `MakePri` external commands
  - Acceptance: `Test-Path tests/scripts/build-msix.Tests.ps1` returns `True`; `Select-String -Path tests/scripts/build-msix.Tests.ps1 -Pattern 'BeforeAll' -Quiet` returns `True`; `Select-String -Path tests/scripts/build-msix.Tests.ps1 -Pattern '\. .*build-msix\.ps1' -Quiet` returns `True`

- [x] [P5-T2] Add Pester test `'stamps the 4-part version into AppxManifest.xml'` in `build-msix.Tests.ps1` verifying that `Invoke-VersionStamp` writes the supplied version string to the `Version` attribute in the staging `AppxManifest.xml`
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/build-msix.Tests.ps1` exits 0; output contains `stamps the 4-part version into AppxManifest.xml` with status `Passed`

- [x] [P5-T3] Add Pester test `'throws a terminating error when bridge publish directory is absent'` in `build-msix.Tests.ps1` verifying that `Invoke-LayoutAssembly` throws when `artifacts/publish/bridge/` does not exist
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/build-msix.Tests.ps1` exits 0; output contains `throws a terminating error when bridge publish directory is absent` with status `Passed`

- [x] [P5-T4] Add Pester test `'copies bridge binaries to installer/staging/bridge/'` in `build-msix.Tests.ps1` verifying that `Invoke-LayoutAssembly` copies the bridge publish output directory to `installer/staging/bridge/`
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/build-msix.Tests.ps1` exits 0; output contains `copies bridge binaries to installer/staging/bridge/` with status `Passed`

- [x] [P5-T5] Add Pester test `'passes pack /d /p /nv arguments to makeappx'` in `build-msix.Tests.ps1` verifying that the `makeappx` shim receives arguments that include `pack`, `/d`, `/p`, and `/nv`
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/build-msix.Tests.ps1` exits 0; output contains `passes pack /d /p /nv arguments to makeappx` with status `Passed`

- [x] [P5-T6] Add Pester test `'does not invoke signtool when -SkipSign is passed'` in `build-msix.Tests.ps1` verifying that the `signtool` shim call count is 0 when the script is invoked with `-SkipSign`
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/build-msix.Tests.ps1` exits 0; output contains `does not invoke signtool when -SkipSign is passed` with status `Passed`

#### Pester: `tests/scripts/New-MsixDevCert.Tests.ps1`

- [x] [P5-T7] Create `tests/scripts/New-MsixDevCert.Tests.ps1` with a `Describe 'New-MsixDevCert.ps1'` block and a `BeforeAll` that dot-sources `scripts/New-MsixDevCert.ps1` and defines shims for `New-SelfSignedCertificate`, `Export-PfxCertificate`, `Export-Certificate`, and `Import-Certificate`
  - Acceptance: `Test-Path tests/scripts/New-MsixDevCert.Tests.ps1` returns `True`; `Select-String -Path tests/scripts/New-MsixDevCert.Tests.ps1 -Pattern 'BeforeAll' -Quiet` returns `True`; `Select-String -Path tests/scripts/New-MsixDevCert.Tests.ps1 -Pattern '\. .*New-MsixDevCert\.ps1' -Quiet` returns `True`

- [x] [P5-T8] Add Pester test `'passes -Subject CN to New-SelfSignedCertificate'` in `New-MsixDevCert.Tests.ps1` verifying that the `New-SelfSignedCertificate` shim receives a `-Subject` argument that exactly equals the value passed to the script's `-Subject` parameter
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/New-MsixDevCert.Tests.ps1` exits 0; output contains `passes -Subject CN to New-SelfSignedCertificate` with status `Passed`

- [x] [P5-T9] Add Pester test `'exports PFX to the specified OutputDir'` in `New-MsixDevCert.Tests.ps1` verifying that `Export-PfxCertificate` receives a `-FilePath` whose directory component matches the value passed to the script's `-OutputDir` parameter
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/New-MsixDevCert.Tests.ps1` exits 0; output contains `exports PFX to the specified OutputDir` with status `Passed`

#### MSTest: `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`

- [x] [P5-T10] Create `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` with `[TestClass]` attribute, `using` directives for `Microsoft.VisualStudio.TestTools.UnitTesting`, `FluentAssertions`, and `System.Xml.Linq`, and a private `RepoRoot` helper that walks up from `AppContext.BaseDirectory` to locate the solution root (identified by `OpenClaw.MailBridge.sln`)
  - Acceptance: `Test-Path tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` returns `True`; `dotnet build OpenClaw.MailBridge.sln /p:Configuration=Debug "/p:Platform=Any CPU"` exits with code 0 after adding this file

- [x] [P5-T11] Add MSTest method `Manifest_ParsesAsValidXml` to `MsixPackageTests.cs` that loads `installer/Package.appxmanifest` via `XDocument.Load()` and asserts no exception is thrown
  - Acceptance: `dotnet test --settings mailbridge.runsettings --filter "FullyQualifiedName~MsixPackageTests.Manifest_ParsesAsValidXml"` exits with code 0 and output contains `Passed`

- [x] [P5-T12] Add MSTest method `Manifest_ContainsStartupTaskExtension_WithCorrectExecutable` to `MsixPackageTests.cs` that parses the manifest, locates the `uap5:StartupTask` element, and asserts `TaskId` equals `"OpenClawMailBridge"` and the parent `uap5:Extension` `Executable` attribute equals `"bridge\OpenClaw.MailBridge.exe"`
  - Acceptance: `dotnet test --settings mailbridge.runsettings --filter "FullyQualifiedName~MsixPackageTests.Manifest_ContainsStartupTaskExtension_WithCorrectExecutable"` exits 0 and output contains `Passed`

- [x] [P5-T13] Add MSTest method `Manifest_IdentityVersion_IsValid4PartVersion` to `MsixPackageTests.cs` that reads the `Version` attribute from the manifest `Identity` element and asserts it matches the regex `^\d+\.\d+\.\d+\.\d+$`
  - Acceptance: `dotnet test --settings mailbridge.runsettings --filter "FullyQualifiedName~MsixPackageTests.Manifest_IdentityVersion_IsValid4PartVersion"` exits 0 and output contains `Passed`

- [x] [P5-T14] Add MSTest method `Manifest_DoesNotDeclareWindowsService` to `MsixPackageTests.cs` that reads the raw manifest text and asserts it does not contain the substring `"windows.service"`
  - Acceptance: `dotnet test --settings mailbridge.runsettings --filter "FullyQualifiedName~MsixPackageTests.Manifest_DoesNotDeclareWindowsService"` exits 0 and output contains `Passed`

- [x] [P5-T15] Add MSTest method `PublishOutput_BridgeDirectory_ContainsBridgeExecutable` to `MsixPackageTests.cs` that reads `MSIX_PUBLISH_DIR` from environment, calls `Assert.Inconclusive("MSIX_PUBLISH_DIR not set")` if absent, and asserts `Path.Combine(msixPublishDir, "bridge", "OpenClaw.MailBridge.exe")` exists on disk
  - Acceptance: `dotnet test --settings mailbridge.runsettings --filter "FullyQualifiedName~MsixPackageTests.PublishOutput_BridgeDirectory_ContainsBridgeExecutable"` exits 0 and output contains `Passed` or `Inconclusive`

- [x] [P5-T16] Add MSTest method `PublishOutput_ClientDirectory_ContainsClientExecutable` to `MsixPackageTests.cs` that reads `MSIX_PUBLISH_DIR` from environment, calls `Assert.Inconclusive("MSIX_PUBLISH_DIR not set")` if absent, and asserts `Path.Combine(msixPublishDir, "client", "OpenClaw.MailBridge.Client.exe")` exists on disk
  - Acceptance: `dotnet test --settings mailbridge.runsettings --filter "FullyQualifiedName~MsixPackageTests.PublishOutput_ClientDirectory_ContainsClientExecutable"` exits 0 and output contains `Passed` or `Inconclusive`

#### Documentation

- [x] [P5-T17] Add an `## MSIX Installation` section to `README.md` documenting: (a) one-time dev cert setup via `New-MsixDevCert.ps1`; (b) install via `Add-AppxPackage`; (c) upgrade by re-running `Add-AppxPackage` with the new version; (d) uninstall via `Remove-AppxPackage`
  - Acceptance: `Select-String -Path README.md -Pattern '## MSIX' -Quiet` returns `True`; `Select-String -Path README.md -Pattern 'Add-AppxPackage' -Quiet` returns `True`; `Select-String -Path README.md -Pattern 'New-MsixDevCert' -Quiet` returns `True`

### Phase 6 — Final QA Loop

- [x] [P6-T1] Run PoshQC format on all new PowerShell files and confirm no violations
  - Command: `mcp_drmcopilotext_run_poshqc_format` targeting `scripts/build-msix.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/build-msix.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`
  - Acceptance: exits with code 0; output reports no formatting violations

- [x] [P6-T2] Run PoshQC analyze on all new PowerShell files and confirm no PSScriptAnalyzer violations
  - Command: `mcp_drmcopilotext_run_poshqc_analyze` targeting `scripts/build-msix.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/build-msix.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`
  - Acceptance: exits with code 0; output reports no violations

- [x] [P6-T3] Run PoshQC test on the full `tests/scripts/` directory and confirm all 7 new Pester scenarios pass
  - Command: `mcp_drmcopilotext_run_poshqc_test` targeting `tests/scripts/`
  - Acceptance: exits with code 0; `build-msix.Tests` contributes 5 passing tests (P5-T2 through P5-T6) and `New-MsixDevCert.Tests` contributes 2 passing tests (P5-T8 through P5-T9); no failures reported

- [x] [P6-T4] Run CSharpier format check after all new C# files are added and confirm no formatting violations
  - Command: `dotnet tool run csharpier . --check`
  - Acceptance: exits with code 0; output reports no files need reformatting

- [x] [P6-T5] Run MSBuild analyzer build after all changes and confirm no new analyzer violations
  - Command: `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
  - Acceptance: exits with code 0; output contains `Build succeeded`; no analyzer errors beyond the P0-T3 baseline

- [x] [P6-T6] Run MSBuild nullable/TreatWarningsAsErrors build and confirm `MsixPackageTests.cs` introduces no nullable violations
  - Command: `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true`
  - Acceptance: exits with code 0; output contains `Build succeeded`

- [x] [P6-T7] Run the full dotnet test suite and confirm total test count equals the P0-T5 baseline plus 6 new MSTest tests from `MsixPackageTests`
  - Command: `dotnet test --settings mailbridge.runsettings`
  - Acceptance: exits with code 0; output contains `MsixPackageTests`; total tests run equals baseline count + 6; no pre-existing tests regress to `Failed`

- [x] [P6-T8] Verify `installer/Package.appxmanifest` passes XML well-formedness in a PowerShell session
  - Command: `$null = [xml](Get-Content -Raw installer/Package.appxmanifest)`
  - Acceptance: PowerShell session exits with code 0; no XML parse exception is thrown

- [x] [P6-T9] Verify `.gitignore` correctly excludes both `installer/staging/` and `artifacts/msix/` from version control
  - Commands: `git check-ignore -v installer/staging/`; `git check-ignore -v artifacts/msix/`
  - Acceptance: both commands exit with code 0; each output line identifies the matching `.gitignore` rule

## Test Plan

- **Pester unit tests** (`tests/scripts/build-msix.Tests.ps1`): 5 scenarios — version stamping, missing-publish-directory error, layout assembly file copy, `makeappx` argument format, `-SkipSign` guard suppressing `signtool`.
- **Pester unit tests** (`tests/scripts/New-MsixDevCert.Tests.ps1`): 2 scenarios — cert Subject CN forwarding, PFX export path.
- **MSTest tests** (`tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`): 6 methods — manifest XML validity, `startupTask` extension content, `Identity Version` format, absence of `windows.service`, conditional bridge exe presence, conditional client exe presence.
- **Manual smoke test** (non-gating note): Install the produced `.msix` on a Windows 10/11 VM, log off and back on, confirm `OpenClaw.MailBridge.exe` appears in Task Manager, run `OpenClaw.MailBridge.Client.exe status` and confirm non-empty JSON response.
- **Manual upgrade test** (non-gating note): Install v1.0.0.0, then install v1.1.0.0; confirm the startup task is still registered and `bridge.settings.json` is unchanged.

## Open Questions / Notes

- **`MakePri.exe` location on runners**: The Windows SDK binary path varies by runner image. `Invoke-MakePri` should probe `${env:ProgramFiles(x86)}\Windows Kits\10\bin\<version>\x64\makepri.exe` and fall back to the `WindowsSdkVerBinPath` MSBuild property. If neither resolves, the function must emit a clear `Write-Error` and exit non-zero.
- **Commercial signing cert**: `New-MsixDevCert.ps1` produces a self-signed cert suitable for sideloading and CI only. Production distribution requires a commercial Authenticode cert or Azure Trusted Signing. Tracked as a future issue.
- **Crash-restart**: `windows.startupTask` does not restart the bridge on crash. The bridge restarts only on the next user logon. Watchdog behavior is explicitly deferred to a future feature.
- **No changes to existing source**: `Program.cs`, `BridgeApplication.cs`, both `.csproj` files, `install-mailbridge.ps1`, `uninstall-mailbridge.ps1`, and `register-mailbridge-task.ps1` are not modified. The existing `AddWindowsService()` call in `BridgeApplication.cs` is compatible with the MSIX startup-task model.
