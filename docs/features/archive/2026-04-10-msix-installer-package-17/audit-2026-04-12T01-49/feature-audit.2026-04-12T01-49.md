# Feature Audit

- Timestamp: 2026-04-12T01-49
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Issue: `#17`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/msix-installer-package-17`
- Head commit: `e43e8a7f2880f9ec7ca0769d0d1976f880073929`

## Scope and Baseline

- Base branch: `development`
- PR context summary: `artifacts/pr_context.summary.txt`
- PR context appendix: `artifacts/pr_context.appendix.txt`
- Feature folder used: `docs/features/active/2026-04-10-msix-installer-package-17`
- Work mode: `full-feature` (from `issue.md`)
- Authoritative acceptance-criteria sources: `spec.md` and `user-story.md`
- Merge base: `dcb71b791e1ba6f5775d09ab5dee644aec999246`
- Range: `dcb71b791e1ba6f5775d09ab5dee644aec999246..e43e8a7f2880f9ec7ca0769d0d1976f880073929`
- Scope note: the refreshed PR-context artifacts show the workspace contains uncommitted feature-folder evidence and requirement-file updates; this audit evaluates that current on-disk branch state.

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
12. `scripts/build-msix.ps1` produces a valid `.msix` when invoked after `dotnet publish` on a `windows-latest` runner.
13. `scripts/New-MsixDevCert.ps1` creates and exports a self-signed certificate suitable for signing.
14. MSIX publish profiles exist for both projects with `PublishSingleFile=false` and `SelfContained=true`.
15. `.github/workflows/build-msix.yml` exists and uploads the package as the `msix-package` artifact.
16. Acceptance criteria `1-9` are verified in `user-story.md`.
17. All new Pester tests pass.
18. All new MSTest tests pass.
19. PoshQC suite passes on the new PowerShell files.
20. `README.md` includes MSIX installation instructions.
21. Unit tests for `build-msix.ps1` cover version stamping, missing publish directory error, layout assembly, `makeappx.exe` argument validation, and `-SkipSign` behavior.
22. Unit tests for `New-MsixDevCert.ps1` cover subject handling and PFX export path.
23. `MsixPackageTests.cs` proves the publish-output assertions when `MSIX_PUBLISH_DIR` is set.
24. Smoke-test evidence proves install, next-logon startup, and client status response.
25. Upgrade evidence proves install `v1.0.0.0` to install `v1.0.1.0` with startup-task preservation and settings retention.
26. Uninstall evidence proves startup-task removal, `bridge/` removal, `client/` removal, and `bridge.settings.json` retention.

## Acceptance Criteria Evaluation

| Criterion | Status | Evidence | Verification command(s) | Notes |
|-----------|--------|----------|--------------------------|-------|
| 1. Install both executables cleanly on Windows 10/11 | PASS | `evidence/other/install-v1.2026-04-11T19-44.md` | Canonical evidence inspection | User-story item remains evidence-backed. |
| 2. Startup task registers and bridge starts on next logon | PASS | `evidence/other/logon-startup.2026-04-11T20-31.md` | Canonical evidence inspection | User-story item remains evidence-backed. |
| 3. Bridge restarts on logon after reboot | PASS | `evidence/other/reboot-logon.2026-04-11T20-33.md` | Canonical evidence inspection | User-story item remains evidence-backed. |
| 4. Upgrade preserves settings and replaces binaries | PASS | `evidence/other/pre-upgrade-settings.2026-04-11T19-44.md`; `evidence/other/upgrade-v2.2026-04-11T19-44.md`; `evidence/other/upgrade-version-reconciliation.md` | Canonical evidence inspection | The requirement wording is now reconciled to `1.0.1.0`. |
| 5. Uninstall removes startup task and binaries while retaining config | PASS | `evidence/other/uninstall.2026-04-11T19-44.md`; `evidence/other/uninstall-directory-removal.md` | Canonical evidence inspection | Explicit bridge/client directory removal is now recorded. |
| 6. CI can build the package with `dotnet publish` + `makeappx.exe` | PASS | `evidence/other/ci-path-success.md`; `.github/workflows/build-msix.yml` | Static inspection plus canonical CI evidence inspection | The workflow now signs the package and a successful `windows-latest` run is recorded. |
| 7. Existing PowerShell install/uninstall scripts remain a functional alternative | PASS | `spec.md`; unchanged script paths; targeted PowerShell QA evidence | Canonical evidence inspection | No regression signal was introduced in the legacy script path. |
| 8. New Pester unit tests cover both helper scripts | PASS | `tests/scripts/build-msix.Tests.ps1`; `tests/scripts/New-MsixDevCert.Tests.ps1`; `evidence/qa-gates/poshqc-test.2026-04-11T20-41.md` | Canonical evidence inspection | Coverage remains sufficient and targeted. |
| 9. New MSTest tests validate layout and manifest content | PASS | `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`; `evidence/other/msix-publish-dir-assertion.md`; `evidence/qa-gates/csharp-test-coverage.md` | Canonical evidence inspection | The formerly conditional publish-output branch is now evidenced. |
| 10. Manifest contains startup task and no service declaration | PASS | `installer/Package.appxmanifest`; `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` | Static inspection | Spec item is evidence-backed. |
| 11. Required icon assets exist | PASS | `installer/Assets/*`; `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` | Static inspection | Spec item is evidence-backed. |
| 12. `build-msix.ps1` produces a valid `.msix` on `windows-latest` | PASS | `evidence/other/ci-path-success.md` | Canonical CI evidence inspection | Spec item is now directly supported by a successful GitHub Actions run. |
| 13. `New-MsixDevCert.ps1` creates and exports a signing certificate | PASS | `evidence/other/dev-cert-create.2026-04-11T19-44.md`; workflow build step | Canonical evidence inspection | Spec item is evidence-backed. |
| 14. Publish profiles exist with directory-layout settings | PASS | `src/OpenClaw.MailBridge/Properties/PublishProfiles/msix.pubxml`; `src/OpenClaw.MailBridge.Client/Properties/PublishProfiles/msix.pubxml` | Static inspection | Spec item is evidence-backed. |
| 15. Workflow uploads `msix-package` artifact | PASS | `.github/workflows/build-msix.yml`; `evidence/other/ci-path-success.md` | Static inspection plus canonical CI evidence inspection | Artifact upload is now both declared and executed. |
| 16. Acceptance criteria `1-9` are verified in `user-story.md` | PASS | `user-story.md`; `evidence/qa-gates/acceptance-status.2026-04-11T20-44.md` | Acceptance source inspection | The aggregate acceptance closure is now supported. |
| 17. All new Pester tests pass | PASS | `evidence/qa-gates/poshqc-test.2026-04-11T20-41.md` | Canonical evidence inspection | Spec item is evidence-backed. |
| 18. All new MSTest tests pass | PASS | `evidence/qa-gates/csharp-test-coverage.md` | Canonical evidence inspection | Full solution test run passed with no failures. |
| 19. PoshQC suite passes on the new PowerShell files | PASS | `evidence/qa-gates/poshqc-format.2026-04-11T20-41.md`; `evidence/qa-gates/poshqc-analyze.2026-04-11T20-41.md`; `evidence/qa-gates/poshqc-test.2026-04-11T20-41.md` | Canonical evidence inspection | Spec item is evidence-backed. |
| 20. README includes MSIX installation guidance | PASS | `README.md` `MSIX Installation` section | Static inspection | Spec item is evidence-backed. |
| 21. `build-msix.ps1` unit-test coverage is present | PASS | `tests/scripts/build-msix.Tests.ps1` | Static inspection | Spec seeded condition is evidence-backed. |
| 22. `New-MsixDevCert.ps1` unit-test coverage is present | PASS | `tests/scripts/New-MsixDevCert.Tests.ps1` | Static inspection | Spec seeded condition is evidence-backed. |
| 23. `MsixPackageTests.cs` proves `MSIX_PUBLISH_DIR` assertions | PASS | `evidence/other/msix-publish-dir-assertion.md` | Canonical evidence inspection | No `Assert.Inconclusive` path was taken. |
| 24. Smoke-test evidence proves install and startup behavior | PASS | `evidence/other/install-v1.2026-04-11T19-44.md`; `evidence/other/logon-startup.2026-04-11T20-31.md`; `evidence/other/reboot-logon.2026-04-11T20-33.md` | Canonical evidence inspection | Spec seeded condition is evidence-backed. |
| 25. Upgrade evidence proves `v1.0.0.0` to `v1.0.1.0` behavior | PASS | `evidence/other/upgrade-version-reconciliation.md` | Canonical evidence inspection | The on-disk requirement wording now matches the executed evidence. |
| 26. Uninstall evidence proves directory removal plus config retention | PASS | `evidence/other/uninstall-directory-removal.md` | Canonical evidence inspection | Explicit bridge/client removal is recorded. |

## Summary

**Overall feature readiness: PASS**

The previously open remediation items are closed in the current on-disk branch state. The branch now has evidence-backed support for the signed CI packaging path, the publish-output assertion path, the reconciled upgrade wording, explicit uninstall directory removal, and full acceptance-criteria closure across both authoritative requirement files.

Remaining gaps preventing PASS:

- none

## Acceptance Criteria Check-off

No checkbox edits were required during this review pass because the current authoritative source files already show all acceptance criteria checked and the present evidence supports those checkoffs.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`, `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`
- Total AC items: `26`
- Checked off (delivered): `26`
- Remaining (unchecked): `0`
- Items remaining:
  - `none`
