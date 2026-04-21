# Definition of Done / Acceptance Criteria Reconciliation (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: Line-by-line comparison of refreshed `spec.md` Definition of Done + Seeded Test Conditions and refreshed `user-story.md` Acceptance Criteria against Phase A-G evidence artifacts.
EXIT_CODE: 0
Output Summary:

## spec.md Definition of Done (15 items)

| DoD item | Status | Evidence |
|---|:---:|---|
| `scripts/Install.ps1` exists and accepts `-SourcePath`, `-AllowUnsigned`, `-SkipDocker`, `-Force`. `-Version` is NOT a parameter. | PASS | `scripts/Install.ps1` param() block (PE-T2); `install-ps1-auto-detect-removed.refinement.2026-04-19T00-00.md` (PE-T6) |
| `scripts/Uninstall.ps1` exists and consumes `install-record.json` with no parameters. | PASS | `uninstall-ps1-no-change.refinement.2026-04-19T00-00.md` (PF-T6) |
| `scripts/Install.Helpers.psm1` exists and exports the functions listed in the API / CLI Surface section. | PASS | Export-ModuleMember list at scripts/Install.Helpers.psm1 (PD-T5); Install.Helpers export-surface test (PD-T6) |
| Running `.\scripts\Install.ps1` on a clean host with a signed bundle under `artifacts/publish/` installs the MSIX, starts the docker stack, and writes `install-record.json`. | PASS | Install.ps1 stage-ordering test (PE-T13); final-pester.refinement.2026-04-19T00-00.md (PG-T3) |
| Running `.\scripts\Uninstall.ps1` reverses the install and deletes `install-record.json`, while preserving `%LOCALAPPDATA%\OpenClaw\MailBridge\`. | PASS | `tests/scripts/Uninstall.Tests.ps1` (unchanged; 143-tests baseline covers this) |
| `-Force` performs a complete uninstall of any prior install of the same version before installing. | PASS | `-Force over existing install` context in Install.Tests.ps1 (baseline coverage, still passing) |
| Manifest-integrity failure aborts before any destination folder is created. | PASS | `manifest integrity failure` context test (PG-T3) |
| Docker-not-running with the docker stage enabled fails fast with a remediation message. | PASS | `docker not running` context test (PG-T3) |
| `.env` is never overwritten; `.env.example` is copied to `.env` only when `.env` is absent. | PASS | Install.Helpers.Tests.ps1 `Initialize-DotEnv` context (PG-T3) |
| Pester coverage >= 90% on new lines; repo-wide >= 80%. | PASS | `coverage-delta.refinement.2026-04-19T00-00.md` (PG-T4) |
| PoshQC suite (format -> analyze -> test) passes on all new and modified PowerShell files. | PASS | `final-poshqc-format.refinement.2026-04-19T00-00.md` (PG-T1); `final-poshqc-analyze.refinement.2026-04-19T00-00.md` (PG-T2); `final-pester.refinement.2026-04-19T00-00.md` (PG-T3) |
| `README.md` and `docs/mailbridge-runbook.md` document the new install and uninstall flow without removing the scheduled-task path content. | PASS | `docs-refinement.refinement.2026-04-19T00-00.md` (PF-T7) |
| `Publish.ps1` copies `Install.ps1`, `Uninstall.ps1`, and `Install.Helpers.psm1` into every bundle root. | PASS | `Copy-InstallScriptsIntoBundle` helper (PC-T2) + Publish.ps1 stage-5 wiring (PC-T11) + Publish.Tests.ps1 ordering assertion (PC-T13) |
| `manifest.json` uses the `{ version, files }` schema and includes the install scripts in `files`. | PASS | `Write-PublishManifest` schema change (PC-T10) + schema-assertion test (PC-T12) |
| Running `.\Install.ps1` from a bundle root installs the bundle without any `-Version` or auto-detect parameter. | PASS | Install.ps1 bundle-selection stage rewrite (PE-T4) + `bundle-root self-location` context tests (PE-T14) |

## spec.md Seeded Test Conditions (12 items)

Existing 10 items (unchanged behavior) still PASS per `final-pester.refinement.2026-04-19T00-00.md`. Two new items added in Phase A:

| Seeded Test Condition | Status | Evidence |
|---|:---:|---|
| Running `.\Install.ps1` from a directory without `manifest.json` fails fast with a clear error naming the directory searched. | PASS | `bundle-root self-location` context `It 'throws with the bundle root path in the message when manifest.json is absent'` (PE-T14) |
| The bundle produced by `Publish.ps1` contains `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1` at the bundle root and the manifest lists them under `files`. | PASS | `Copy-InstallScriptsIntoBundle` helper + Publish.ps1 stage ordering + manifest emitter tests (PC-T2/T11/T12/T13) |

## user-story.md Acceptance Criteria (16 items)

| AC item | Status | Evidence |
|---|:---:|---|
| `.\scripts\Install.ps1` with no arguments installs the MSIX and brings the docker stack up. | PASS | stage-ordering happy path test (PE-T13) |
| Running `.\Install.ps1` from a bundle root installs that bundle; `-SourcePath` overrides default `$PSScriptRoot`. | PASS | parameter binding context + bundle-root self-location context (PE-T11, PE-T14) |
| `.\scripts\Install.ps1 -AllowUnsigned` installs the MSIX. | PASS | administrator-precheck context (unchanged; passes in PG-T3) |
| `.\scripts\Install.ps1 -SkipDocker` installs MSIX only and records `skipDocker = true`. | PASS | `-SkipDocker path` context (PG-T3) |
| `.\scripts\Install.ps1 -Force` performs complete uninstall before reinstall. | PASS | `-Force over existing install` context (PG-T3) |
| `manifest.json` hash/size mismatch aborts install before destination folder is created. | PASS | `manifest integrity failure` context (PG-T3) |
| Docker not running aborts install with remediation. | PASS | `docker not running` context (PG-T3) |
| `.env.example` copied only when `.env` is absent. | PASS | `Initialize-DotEnv` context in Install.Helpers.Tests.ps1 (PG-T3) |
| `install-record.json` written on successful install. | PASS | `Write-InstallRecord` mock call ordering in stage-ordering test (PG-T3) |
| `Uninstall.ps1` runs compose down / Remove-AppxPackage / Remove-Item / delete record, collects failures. | PASS | `Uninstall.Tests.ps1` suite (PG-T3) |
| User config at `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` preserved by `Uninstall.ps1`. | PASS | `Uninstall.ps1` leaves sibling path untouched; Uninstall.Tests.ps1 asserts no operation against that path |
| Coverage >= 90% on new lines; repo-wide >= 80%. | PASS | `coverage-delta.refinement.2026-04-19T00-00.md` (PG-T4) |
| PoshQC suite passes. | PASS | PG-T1 + PG-T2 + PG-T3 artifacts |
| README + runbook document the flow. | PASS | `docs-refinement.refinement.2026-04-19T00-00.md` (PF-T7) |
| `Publish.ps1` copies the install scripts into every bundle root. | PASS | `Copy-InstallScriptsIntoBundle` helper + Publish.ps1 stage-5 (PC-T2/T11); Publish.Tests.ps1 ordering (PC-T13) |
| `manifest.json` uses `{ version, files }`; `Get-ManifestVersion` returns the top-level version field. | PASS | `Write-PublishManifest` (PC-T10) + `Get-ManifestVersion` (PD-T3) + tests (PC-T12, PD-T8) |

## Acceptance Criteria Status Summary

- Source: `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` (Definition of Done, Seeded Test Conditions) and `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` (Acceptance Criteria).
- Total AC items across sources: 15 (DoD) + 12 (Seeded) + 16 (user-story AC) = 43.
- Checked off (delivered): 43.
- Remaining (unchecked): 0.
