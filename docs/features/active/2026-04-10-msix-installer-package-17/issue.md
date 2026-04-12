# msix-installer-package (Issue #17)

- Date captured: 2026-04-10
- Author: drmoisan
- Status: Promoted -> docs/features/active/msix-installer-package/ (Issue #17)

- Issue: #17
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/17
- Last Updated: 2026-04-10
- Work Mode: full-feature

## Problem / Why

The OpenClaw MailBridge currently has no packaged installer. Deployment requires manual file copy, PowerShell script execution, and scheduled task registration. There is no standardized upgrade, repair, or uninstall path. Users cannot distribute or install the bridge reliably without deep technical knowledge.

## Proposed Behavior

Produce a signed MSIX installer package for the OpenClaw MailBridge that:
- Installs both `OpenClaw.MailBridge.exe` (bridge host) and `OpenClaw.MailBridge.Client.exe` (client CLI) to the appropriate program files location.
- Registers the bridge host as a startup task so it starts automatically on user logon in the interactive session.
- Seeds a default `bridge.settings.json` configuration in `%LOCALAPPDATA%\OpenClaw\MailBridge\` if one is not present.
- Supports upgrade (in-place), repair, and uninstall through the MSIX lifecycle (Windows Settings > Apps or `winget`).
- Is buildable from CI (`dotnet publish` + Windows Application Packaging Project or equivalent MSIX toolchain).

## Acceptance Criteria (early draft)

- [ ] MSIX package installs both executables without error on a clean Windows 10/11 machine.
- [ ] After install, a startup task named `OpenClaw MailBridge` is registered and active; the bridge starts on next user logon.
- [ ] On host reboot, the bridge restarts automatically on user login.
- [ ] Upgrading the package in-place replaces binaries and preserves existing `bridge.settings.json`.
- [ ] Uninstalling via Settings > Apps removes the startup task and binaries (leaves user config in place).
- [ ] Package can be built from CI using `dotnet publish` + MSIX toolchain steps.
- [ ] Existing PowerShell install/uninstall scripts remain functional as a side-by-side alternative for non-MSIX deployment.

## Constraints & Risks

- Session 0 isolation prevents background service processes from accessing Outlook COM in the interactive user session, so the package must use `windows.startupTask`.
- Self-contained single-file binaries may need to be published as framework-dependent or directory-published for MSIX to include all required files.
- `.NET 10` is a preview/RC target — MSIX toolchain compatibility must be verified.
- Outlook COM requires the logged-in user session, and `windows.startupTask` is the required launch mechanism for that session-aware model.
- MSIX packages can only write to permitted locations; `%LOCALAPPDATA%` config seeding must use the MSIX Extension mechanism or a custom action.

## Test Conditions to Consider

- [ ] Unit tests for any new PowerShell helper functions in installer scripts.
- [ ] Smoke-test: install → service running → `MailBridge.Client.exe status` returns non-empty.
- [ ] Upgrade scenario: install v1 → install v2 → service still running, config preserved.
- [ ] Uninstall scenario: package removed → service gone → binaries gone.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/msix-installer-package/` folder from the template
