# `2026-04-10-msix-installer-package` — User Story

- Issue: #17
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-10T19-59

## Story Statement

- As a **developer or IT operator deploying OpenClaw MailBridge to a new Windows machine**, I want to install both the bridge host and client CLI with a single double-click (or `Add-AppxPackage` command), so that the bridge starts automatically on logon without any manual file copy, script execution, or task registration.
- As a **CI/CD pipeline maintainer**, I want to produce a signed MSIX package from a tag push using only `dotnet publish` and Windows SDK tools (no Visual Studio), so that every release has a distributable, upgradeable installer artifact without manual intervention.


## Problem / Why

The OpenClaw MailBridge currently has no packaged installer. Deployment requires manual file copy, PowerShell script execution, and scheduled task registration. There is no standardized upgrade, repair, or uninstall path. Users cannot distribute or install the bridge reliably without deep technical knowledge.

Operators who need to deploy the bridge on a fresh machine must know which scripts to run, in what order, and how to validate the result. There is no repeatable, auditable path for upgrade or rollback, and uninstall leaves artifacts unless the operator knows to run a second script. A signed MSIX package eliminates all of these manual steps and delegates lifecycle management to the Windows platform.


## Personas & Scenarios

- **Persona: Developer / Power User (primary)**
  - A developer on the OpenClaw project who needs to install the bridge on a personal Windows 11 laptop or a fresh VM for testing.
  - Cares about a fast, repeatable install and clean uninstall. Does not want to manually register scheduled tasks or troubleshoot PATH issues.
  - Constraint: may not have admin rights to install to `%ProgramFiles%` directly, but can run MSIX sideload with developer mode enabled. Must trust the signing cert.
  - Goal: get the bridge running on logon within one minute. Wants to be able to upgrade by re-running `Add-AppxPackage` with the new version.
  - Frustration: current install script requires understanding which PowerShell scripts to run and in what order; no easy way to confirm the bridge is registered correctly.

- **Persona: IT Operator / Enterprise Deployer (secondary)**
  - Responsible for deploying the bridge to a shared workstation or a small team of machines via Intune or a similar MDM tool.
  - Cares about auditability, upgrade continuity, and silent install/uninstall without user prompts.
  - Constraint: cannot run arbitrary PowerShell on managed machines; needs a standard Windows installer format compatible with enterprise deployment tooling.
  - Goal: deploy the MSIX silently, confirm startup task is registered, and be able to upgrade to new versions without losing user config.

- **Scenario: Fresh install on a new developer machine**
  - **Trigger**: A developer clones the repo or downloads the MSIX artifact from a GitHub Actions run.
  - **Steps**:
    1. Developer downloads `OpenClaw.MailBridge_1.0.0.0_x64.msix` from the CI artifact.
    2. Developer runs `New-MsixDevCert.ps1` to install the signing cert into Trusted Root (one-time step for dev builds).
    3. Developer double-clicks the `.msix`. Windows displays the package installer dialog showing name, publisher, and capabilities.
    4. Developer clicks Install. Windows extracts files to the package VFS and registers the `OpenClawMailBridge` startup task.
    5. Developer logs off and back on (or reboots). The startup task fires; `OpenClaw.MailBridge.exe` launches in the user's session.
    6. `bridge.settings.json` is auto-created in `%LOCALAPPDATA%\OpenClaw\MailBridge\` on first run.
    7. Developer runs `OpenClaw.MailBridge.Client.exe status` from any terminal and receives a non-empty JSON response confirming the bridge is alive.
  - **Expected outcome**: Bridge is running; startup task is listed as enabled in Task Manager > Startup apps; config file is present. No manual script execution required.

- **Scenario: In-place upgrade**
  - **Trigger**: A new MSIX version (`1.1.0.0`) is available.
  - **Steps**:
    1. Operator runs `Add-AppxPackage -Path OpenClaw.MailBridge_1.1.0.0_x64.msix`.
    2. Windows stages the new version and atomically swaps binaries. The startup task is preserved.
    3. On next logon, the updated `OpenClaw.MailBridge.exe` starts. `bridge.settings.json` is untouched.
  - **Expected outcome**: New binaries running; existing config preserved; no re-registration of startup task required.


## Acceptance Criteria

- [ ] MSIX package installs both executables without error on a clean Windows 10/11 machine.
- [ ] After install, a startup task named `OpenClaw MailBridge` is registered and active; the bridge process starts on next user logon.
- [ ] On host reboot, the bridge restarts automatically on user login (startup task fires on each logon).
- [ ] Upgrading the package in-place replaces binaries and preserves existing `bridge.settings.json` in `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
- [ ] Uninstalling via Settings > Apps removes the startup task and binaries (leaves user config in place).
- [ ] Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
- [x] Existing PowerShell install/uninstall scripts remain functional as a side-by-side alternative for non-MSIX deployment.
- [x] New Pester unit tests cover `build-msix.ps1` and `New-MsixDevCert.ps1` helper functions.
- [x] New MSTest tests validate MSIX publish output layout and manifest content.


## Non-Goals

- **Windows Service registration**: The bridge is explicitly NOT registered as a Windows Service. Session 0 isolation makes Windows Services incompatible with Outlook COM interop. The startup task model is the correct and only supported mechanism.
- **Automatic crash-restart**: The `windows.startupTask` mechanism does not restart the process on crash. Watchdog / self-restart behavior is deferred to a future feature.
- **Microsoft Store publication**: The `runFullTrust` capability requires Microsoft approval for Store distribution. This feature targets sideloading and enterprise deployment only.
- **Multi-user / per-machine install**: The startup task fires for the user who installs the package. Per-machine provisioning for all users is out of scope.
- **macOS / Linux packaging**: This feature is Windows-only. No cross-platform installer is in scope.
- **UI / tray icon**: The bridge host remains a background process with no system tray or GUI. No visual elements beyond the required MSIX icon assets are added.
- **Automated config migration**: No migration of existing `bridge.settings.json` values across schema versions. The bridge's existing self-seeding logic is the only config initialization mechanism.
