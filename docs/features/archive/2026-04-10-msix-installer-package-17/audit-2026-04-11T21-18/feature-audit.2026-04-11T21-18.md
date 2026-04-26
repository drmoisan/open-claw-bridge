# Feature Audit

- Timestamp: 2026-04-11T21-18
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Issue: `#17`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/msix-installer-package-17`
- Head commit: `281d21cad251522e231dc7a425cee74bcd06fcc3`

## Scope and Baseline

- Base branch: `development`
- Evidence sources:
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary: `artifacts/pr_context.appendix.txt`
  - Canonical feature evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/`
  - Live review commands executed in this session
- Feature folder used: `docs/features/active/2026-04-10-msix-installer-package-17`
- Work mode: `full-feature` (from `issue.md`)
- Authoritative acceptance-criteria sources: `spec.md` and `user-story.md`
- Merge base: `dcb71b791e1ba6f5775d09ab5dee644aec999246`
- Range: `dcb71b791e1ba6f5775d09ab5dee644aec999246..281d21cad251522e231dc7a425cee74bcd06fcc3`
- Provenance note: the refreshed PR-context summary still reports the older head SHA `abd1f73...`; exact commit citations in this audit use local git plus the refreshed appendix, while the summary remains the source for scoping and acceptance blocks.

## Acceptance Criteria Inventory

### Primary source: `user-story.md`

1. MSIX package installs both executables without error on a clean Windows 10/11 machine.
2. After install, a startup task named `OpenClaw MailBridge` is registered and active; the bridge process starts on next user logon.
3. On host reboot, the bridge restarts automatically on user login (startup task fires on each logon).
4. Upgrading the package in-place replaces binaries and preserves existing `bridge.settings.json` in `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
5. Uninstalling via Settings > Apps removes the startup task and binaries (leaves user config in place).
6. Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
7. Existing PowerShell install/uninstall scripts remain functional as a side-by-side alternative for non-MSIX deployment.
8. New Pester unit tests cover `build-msix.ps1` and `New-MsixDevCert.ps1` helper functions.
9. New MSTest tests validate MSIX publish output layout and manifest content.

### Secondary source: `spec.md`

10. `installer/Package.appxmanifest` exists with `windows.startupTask`; `windows.service` is not declared.
11. Required MSIX icon assets exist.
12. MSIX publish profiles exist for both projects with directory-layout settings.
13. `.github/workflows/build-msix.yml` exists and uploads the `.msix` as the `msix-package` artifact.
14. `scripts/build-msix.ps1` produces a valid `.msix` after publish.
15. `scripts/New-MsixDevCert.ps1` creates and exports a self-signed certificate suitable for signing.
16. `README.md` includes MSIX installation instructions.
17. `MsixPackageTests.cs` publish-output assertions are exercised when `MSIX_PUBLISH_DIR` is set.
18. Upgrade evidence matches the exact `install v1.0.0.0 -> install v1.1.0.0` wording in `spec.md`.
19. Uninstall evidence explicitly proves the `bridge/` and `client/` directories are gone while user config remains.

## Acceptance Criteria Evaluation

| Criterion | Status | Evidence | Verification command(s) | Notes |
|-----------|--------|----------|--------------------------|-------|
| 1. Install both executables cleanly on Windows 10/11 | PASS | `evidence/other/install-v1.2026-04-11T19-44.md` records `Add-AppxPackage` success plus both executables present in the installed package location. | Canonical evidence inspection; live branch review | User-story line `59` is already checked. |
| 2. Startup task registers and bridge starts on next logon | PASS | `evidence/other/logon-startup.2026-04-11T20-31.md` records `StartupRegistrationVisible=True`, `BridgeProcessRunning=True`, and `ClientStatusReturnedJson=True`. | Canonical evidence inspection | User-story line `60` is already checked. |
| 3. Bridge restarts automatically on user login after reboot | PASS | `evidence/other/reboot-logon.2026-04-11T20-33.md` records `BridgeProcessRunningAfterReboot=True` and `ClientStatusReturnedJson=True`. | Canonical evidence inspection | User-story line `61` is already checked. |
| 4. Upgrade preserves binaries and `%LOCALAPPDATA%` settings | PASS | `evidence/other/pre-upgrade-settings.2026-04-11T19-44.md` seeds `SentinelValue=phase4-upgrade-check`; `evidence/other/upgrade-v2.2026-04-11T19-44.md` records `SentinelSettingPreserved=True`. | Canonical evidence inspection | User-story line `62` is already checked. |
| 5. Uninstall removes startup task and binaries, leaves config | PASS | `evidence/other/uninstall.2026-04-11T19-44.md` records `PackagePresentAfterUninstall=False`, `StartupRegistrationVisible=False`, and `SettingsFileStillPresent=True`. | Canonical evidence inspection | This satisfies the user-story criterion even though the stricter spec wording still wants explicit directory-level proof. User-story line `63` is already checked. |
| 6. Package can be built from CI using `dotnet publish` + `makeappx.exe` | PARTIAL | Static workflow evidence exists: `evidence/other/workflow-static-check.2026-04-11T15-33.md` and `evidence/qa-gates/actionlint-build-msix.2026-04-11T20-44.md`. The workflow is present, but no successful `windows-latest` run artifact exists, and workflow lines `42-43` use `-WhatIf` and `-SkipSign`. | `./scripts/dev-tools/run-actionlint.ps1`; static inspection of `.github/workflows/build-msix.yml` | `user-story.md` line `64` remains unchecked. |
| 7. Existing PowerShell install/uninstall scripts remain a functional alternative | PASS | The branch does not modify `scripts/install-mailbridge.ps1` or `scripts/uninstall-mailbridge.ps1`; the solution test pass and targeted PowerShell QA evidence show no regression signal. | `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`; canonical PowerShell QA evidence | User-story line `65` is already checked. |
| 8. New Pester unit tests cover the helper scripts | PASS | `tests/scripts/build-msix.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` exist and are covered by `evidence/qa-gates/poshqc-test.2026-04-11T20-41.md`. | Canonical targeted PoshQC test artifact | User-story line `66` is already checked. |
| 9. New MSTest tests validate MSIX publish output layout and manifest content | PASS | `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` exists; live `dotnet test` passed; the file covers manifest, startup-task, icon, and publish-profile assertions. | `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` | User-story line `67` is already checked, but the conditional publish-output branch remains an open spec item below. |
| 10. Manifest exists with startup task and no service declaration | PASS | `installer/Package.appxmanifest` contains `windows.startupTask`; `MsixPackageTests` validates it and also checks absence of `windows.service`. | Static manifest inspection; live `dotnet test` | Spec line `229` is checked. |
| 11. Required icon assets exist | PASS | `MsixPackageTests.RequiredIconAssets_AllExist` covers the required PNG files, and those files exist on disk. | Live `dotnet test`; static file inspection | Spec line `230` is checked. |
| 12. Publish profiles exist for both projects with directory-layout settings | PASS | Both `msix.pubxml` files contain `PublishSingleFile=false`, `SelfContained=true`, and `RuntimeIdentifier=win-x64`. | Static inspection of both publish profiles | Spec line `233` is checked. |
| 13. Workflow exists and uploads the package artifact | PASS | `.github/workflows/build-msix.yml` exists and includes tag trigger, workflow dispatch, publish steps, build step, and `actions/upload-artifact@v4` with `name: msix-package`. | Static inspection; `workflow-static-check`; `actionlint` | Spec line `234` is checked. |
| 14. `build-msix.ps1` produces a valid `.msix` after publish | PASS | `evidence/other/publish-bridge.2026-04-11T19-38.md`, `evidence/other/publish-client.2026-04-11T19-38.md`, and `evidence/other/build-msix-v1.2026-04-11T19-39.md` record publish success and signed package creation. | Canonical evidence inspection | This is proven locally, but the stricter `windows-latest` runner wording in spec line `231` remains open. |
| 15. `New-MsixDevCert.ps1` creates and exports a signing certificate | PASS | `evidence/other/dev-cert-create.2026-04-11T19-44.md` records thumbprint, `.pfx` path, and `.cer` path. | Canonical evidence inspection | Spec line `232` is checked. |
| 16. README includes MSIX installation instructions | PASS | `README.md` contains `## MSIX Installation`, `New-MsixDevCert`, build, install, upgrade, and uninstall sections. | Static inspection of `README.md` | Spec line `239` is checked. |
| 17. `MsixPackageTests` publish-output assertions run with `MSIX_PUBLISH_DIR` | PARTIAL | The conditional assertions exist, but the live solution test run kept `3` tests skipped and no canonical evidence shows `MSIX_PUBLISH_DIR` was set. | `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`; static inspection of `MsixPackageTests.cs` | `spec.md` line `246` remains unchecked. |
| 18. Upgrade evidence matches the exact `1.1.0.0` wording in `spec.md` | PARTIAL | Upgrade evidence exists, but `evidence/other/build-msix-v2.2026-04-11T19-44.md` uses version `1.0.1.0`, not `1.1.0.0`. | Canonical evidence inspection | `spec.md` line `248` remains unchecked because the current evidence does not match the exact wording. |
| 19. Uninstall evidence explicitly proves `bridge/` and `client/` directories are gone | PARTIAL | `evidence/other/uninstall.2026-04-11T19-44.md` proves package absence and startup-task removal, but it does not explicitly record path-level disappearance of the package subdirectories. | Canonical evidence inspection | `spec.md` line `249` remains unchecked. |

## Summary

**Overall feature readiness: NEEDS REVISION**

The branch now proves the core local MSIX lifecycle and the static packaging/test surface. The remaining blockers are concentrated in the CI and final evidence-sync path rather than the implementation itself. The unresolved items are: the CI workflow criterion in `user-story.md`, the conditional publish-output test branch, the exact upgrade-version wording in `spec.md`, and explicit uninstall directory-removal proof.

Top gaps preventing PASS:

1. Produce canonical evidence of a successful `windows-latest` CI build path and align the workflow with the signed-package story requirement.
2. Exercise the `MSIX_PUBLISH_DIR` publish-output assertions or replace them with equivalent deterministic proof.
3. Reconcile the two remaining spec evidence mismatches for exact upgrade wording and explicit uninstall directory removal.

Recommended follow-up verification steps:

- Run the GitHub Actions workflow successfully on `windows-latest` and mirror the result into a canonical feature evidence artifact.
- Add or run a targeted verification step that sets `MSIX_PUBLISH_DIR` and executes the publish-output assertions.
- Capture exact `1.1.0.0` upgrade evidence and explicit uninstall directory checks, or narrow the spec wording if the intended verification is less strict.

## Acceptance Criteria Check-off

No acceptance-checkbox changes were made during this review.

- `user-story.md` already has criteria `1-5` and `7-9` checked.
- `user-story.md` criterion `6` remains unchecked because it evaluated as `PARTIAL`.
- `spec.md` items already backed by evidence remain checked.
- `spec.md` lines `231`, `235`, `246`, `248`, and `249` remain unchecked because they evaluated as `PARTIAL` or depend on the unresolved CI criterion.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`, `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`
- Total AC items: `26`
- Checked off (delivered): `19`
- Remaining (unchecked): `7`
- Items remaining:
  - `spec.md` :: `scripts/build-msix.ps1` produces a valid `.msix` when invoked after `dotnet publish` on a `windows-latest` runner.
  - `spec.md` :: Acceptance criteria `1–9` verified (see `user-story.md`).
  - `spec.md` :: `MSTest MsixPackageTests.cs` publish-output assertion when `MSIX_PUBLISH_DIR` is set.
  - `spec.md` :: Upgrade scenario: install `v1.0.0.0` -> install `v1.1.0.0` -> startup task still registered -> `bridge.settings.json` unchanged.
  - `spec.md` :: Uninstall scenario: `Remove-AppxPackage` -> startup task absent -> `bridge/` and `client/` directories gone -> `bridge.settings.json` still present.
  - `user-story.md` :: Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
  - `aggregate` :: Not all spec and user-story acceptance checkboxes are complete because the CI-run proof set is still incomplete.