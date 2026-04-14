# Feature Audit

- Timestamp: 2026-04-11T12-22
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Issue: `#17`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/msix-installer-package-17`
- Head commit: `abd1f73cc8515b81046bfdbb89a5474cd8fdc384`

## Scope and Baseline

- Base branch: `development` (from `artifacts/pr_context.summary.txt`)
- Evidence sources:
  - PR context summary: `artifacts/pr_context.summary.txt`
  - PR context appendix: `artifacts/pr_context.appendix.txt`
  - Direct review commands executed in this session
- Feature folder used: `docs/features/active/2026-04-10-msix-installer-package-17`
- Work mode: `full-feature` (from `issue.md` line `- Work Mode: full-feature`)
- Authoritative AC source files: `spec.md` and `user-story.md`
- Merge base: `dcb71b791e1ba6f5775d09ab5dee644aec999246`
- Range: `dcb71b791e1ba6f5775d09ab5dee644aec999246..abd1f73cc8515b81046bfdbb89a5474cd8fdc384`

## Acceptance Criteria Inventory

### Primary source: `user-story.md` — Acceptance Criteria

1. MSIX package installs both executables without error on a clean Windows 10/11 machine.
2. After install, a startup task named `OpenClaw MailBridge` is registered and active; the bridge process starts on next user logon.
3. On host reboot, the bridge restarts automatically on user login (startup task fires on each logon).
4. Upgrading the package in-place replaces binaries and preserves existing `bridge.settings.json` in `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
5. Uninstalling via Settings > Apps removes the startup task and binaries (leaves user config in place).
6. Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
7. Existing PowerShell install/uninstall scripts remain functional as a side-by-side alternative for non-MSIX deployment.
8. New Pester unit tests cover `build-msix.ps1` and `New-MsixDevCert.ps1` helper functions.
9. New MSTest tests validate MSIX publish output layout and manifest content.

### Secondary source: `spec.md` — unique readiness criteria beyond the primary list

10. `installer/Package.appxmanifest` exists with `windows.startupTask`; `windows.service` is not declared.
11. Required MSIX icon assets exist.
12. MSIX publish profiles exist for both projects with directory-layout settings.
13. `README.md` includes MSIX installation instructions.
14. `scripts/build-msix.ps1` can produce a valid `.msix` after publish.
15. `scripts/New-MsixDevCert.ps1` creates and exports a self-signed certificate suitable for signing.
16. `.github/workflows/build-msix.yml` exists and uploads the package artifact.

## Acceptance Criteria Evaluation

| Criterion | Status | Evidence | Verification command(s) | Notes |
|-----------|--------|----------|--------------------------|-------|
| 1. Install both executables cleanly on Windows 10/11 | UNVERIFIED | Static evidence exists (manifest, assets, publish profiles, scripts), but `artifacts/pr_context.summary.txt` reports no canonical verification evidence parsed. | Human/operator follow-up: build the `.msix`, then run `Add-AppxPackage -Path <package.msix>` on a clean Windows 10/11 machine. | No install artifact or operator evidence was found in the feature folder. |
| 2. Startup task registers and bridge starts on next logon | UNVERIFIED | `installer/Package.appxmanifest` declares `windows.startupTask`; `MsixPackageTests.cs` validates the startup task fields. | Human/operator follow-up after install: log off and back on, then confirm Task Manager > Startup apps and bridge process presence. | Static structure supports the design, but runtime evidence is missing. |
| 3. Bridge restarts automatically on user login after reboot | UNVERIFIED | Startup-task architecture in `spec.md` and the manifest implies logon-based launch, but no reboot evidence exists. | Human/operator follow-up: reboot the test machine, sign back in, and verify the bridge starts. | No reboot/logon artifact exists. |
| 4. Upgrade preserves binaries and `%LOCALAPPDATA%` settings | UNVERIFIED | `spec.md` documents package identity continuity and user-config location outside the package VFS. | Human/operator follow-up: install v1, then `Add-AppxPackage` v2 and compare `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`. | No upgrade evidence or automation artifact exists. |
| 5. Uninstall removes startup task and binaries, leaves config | UNVERIFIED | `spec.md` documents this expectation; no operator evidence exists. | Human/operator follow-up: `Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage`, then verify startup task removal and persisted config. | No uninstall artifact exists. |
| 6. Package can be built from CI using `dotnet publish` + `makeappx.exe` | FAIL | The feature docs and plan claim `.github/workflows/build-msix.yml`, but the branch contains no `.github/workflows/` directory. | Static verification used: repository file listing and workflow search. | This criterion cannot pass until the CI workflow exists and is validated. |
| 7. Existing PowerShell install/uninstall scripts remain functional alternative | PASS | Direct `Invoke-Pester -Path 'tests/scripts'` passed `26/26`; the branch does not modify the existing install/uninstall scripts. | `Invoke-Pester -Path 'tests/scripts' -PassThru` | This criterion was already checked in `user-story.md`; no new checkbox change needed. |
| 8. New Pester unit tests cover the helper scripts | PASS | `tests/scripts/build-msix.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` exist; direct targeted run passed `7/7`. | `Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1','tests/scripts/New-MsixDevCert.Tests.ps1' -PassThru` | This criterion was already checked in `user-story.md`. |
| 9. New MSTest tests validate layout and manifest content | PASS | `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` exists; `dotnet test` passed and the test file covers manifest, startup task, icon assets, and publish profiles. | `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` | This criterion was already checked in `user-story.md`. |
| 10. Manifest exists with startup task and no service declaration | PASS | `installer/Package.appxmanifest` contains `windows.startupTask`, `runFullTrust`, and no `windows.service` reference. `MsixPackageTests` verifies these properties. | `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings`; static manifest inspection | Secondary spec criterion satisfied. |
| 11. Required icon assets exist | PASS | `installer/Assets/Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, and `StoreLogo.png` exist; `MsixPackageTests.RequiredIconAssets_AllExist` covers them. | `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` | Secondary spec criterion satisfied. |
| 12. Publish profiles exist for both projects with directory-layout settings | PASS | Both `msix.pubxml` files exist and contain `PublishDir`, `RuntimeIdentifier=win-x64`, `SelfContained=true`, and `PublishSingleFile=false`. | Static inspection of `src/.../PublishProfiles/msix.pubxml` and `dotnet test ...` | Secondary spec criterion satisfied. |
| 13. README includes MSIX installation instructions | PASS | `README.md` contains `## MSIX Installation`, `New-MsixDevCert`, and `Add-AppxPackage` instructions. | Static inspection of `README.md` | Secondary spec criterion satisfied. |
| 14. `build-msix.ps1` can produce a valid `.msix` after publish | UNVERIFIED | The script and unit tests exist, but this review did not produce an end-to-end `.msix` package with `MakePri.exe` / `makeappx.exe`. | Local follow-up: run the two `dotnet publish` commands, then run `./scripts/build-msix.ps1 -Version '<version>' -SkipSign` on a Windows SDK-equipped machine. | The repository contains `installer/staging/AppxManifest.xml`, but a committed staging file is not acceptable proof of a valid packaging run. |
| 15. `New-MsixDevCert.ps1` creates and exports a signing certificate | UNVERIFIED | The helper tests verify subject forwarding and PFX output-path behavior, but the script was not run end-to-end during this review because it requires elevation and writes to `Cert:\LocalMachine\Root`. | Local follow-up: run `./scripts/New-MsixDevCert.ps1 -PfxPassword <secure-string> -OutputDir artifacts` in an elevated PowerShell session. | Script structure is plausible, but no end-to-end evidence exists. |
| 16. Workflow file exists and uploads the package artifact | FAIL | No `.github/workflows/build-msix.yml` file exists in the branch. | Static repository file listing and workflow search | Secondary spec criterion fails for the same reason as criterion 6. |

## Summary

**Overall feature readiness: NEEDS REVISION**

The branch has solid structural progress, and the static/package-shape tests are in place. However, the primary installer-lifecycle criteria (install, startup at logon, reboot behavior, upgrade preservation, uninstall cleanup) remain unverified, and the CI workflow criterion fails outright because the workflow file is missing. Supporting documentation is also inconsistent because `issue.md` still describes a Windows Service rather than the startup-task design implemented elsewhere.

Top gaps preventing PASS:

1. Add the missing CI workflow and validate it.
2. Produce canonical operator or integration evidence for install, logon, reboot, upgrade, and uninstall behavior.
3. Resolve documentation drift (`issue.md`) and repository hygiene issues (`installer/staging/AppxManifest.xml`).

Recommended follow-up verification steps:

- Build the package end-to-end on a Windows SDK-equipped machine.
- Install the package on a clean Windows 10/11 machine and capture startup-task/logon evidence.
- Run upgrade and uninstall scenarios and store the results as canonical evidence artifacts.

## Acceptance Criteria Check-off

No acceptance-checkbox changes were made during this review.

- In `user-story.md`, the three criteria that evaluated as `PASS` (`side-by-side PowerShell scripts`, `new Pester coverage`, `new MSTest coverage`) were already checked `[x]` before this review.
- The remaining unchecked criteria in `user-story.md` stay unchecked because they are `UNVERIFIED` or `FAIL`.
- In `spec.md`, the already checked structural items remain checked, while the workflow / end-to-end / full-verification items remain unresolved.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`, `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`
- Total AC items reviewed: `16`
- Checked off (already delivered and verified in source files): `7`
- Remaining (unchecked or not fully verified): `9`
- Items remaining:
  - MSIX package installs both executables without error on a clean Windows 10/11 machine.
  - After install, a startup task named `OpenClaw MailBridge` is registered and active; the bridge process starts on next user logon.
  - On host reboot, the bridge restarts automatically on user login.
  - Upgrading the package in-place preserves existing `bridge.settings.json`.
  - Uninstalling via Settings > Apps removes the startup task and binaries (leaves user config in place).
  - Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
  - `scripts/build-msix.ps1` produces a valid `.msix` after publish.
  - `scripts/New-MsixDevCert.ps1` creates and exports a working signing certificate.
  - `.github/workflows/build-msix.yml` exists and uploads the package artifact.