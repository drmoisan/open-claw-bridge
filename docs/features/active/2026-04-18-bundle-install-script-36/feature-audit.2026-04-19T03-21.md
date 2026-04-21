# Feature Audit — Bundle Install Script (Issue #36)

- Date: 2026-04-19
- Audit Timestamp: 2026-04-19T03-21
- Feature Folder: `docs/features/active/2026-04-18-bundle-install-script-36/`
- Work Mode (from `issue.md`): `full-feature`

## Scope and Baseline

- Feature Branch: `feature/unified-publish-script-34` (branch name carries the prior issue number; HEAD commit message is `feat(#36): bundle install/uninstall scripts consume Publish.ps1 output`)
- Base Branch: `development`
- Merge-Base SHA: `7bd92a8cb772c8f41a85831416a5fec952a2330b` (timestamp 2026-04-18T20:16:13-05:00)
- HEAD SHA: `453343e77121d4592e7179dda731a117b3d2b601`
- Diff volume: 31 files changed, +3130 / -1 lines (8 code/doc files; 23 scoping and evidence files)
- AC sources (per `full-feature` work mode):
  - `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` Definition of Done (11 items) and Seeded Test Conditions (10 items)
  - `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` Acceptance Criteria (14 items)

Branch diff languages:

- PowerShell: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`
- Markdown documentation: `README.md`, `docs/mailbridge-runbook.md`, plus feature scoping and evidence documents
- Python / TypeScript / C#: no changed files

## Acceptance Criteria Inventory

### `spec.md` Definition of Done (11 items)

1. `scripts/Install.ps1` exists and accepts `-SourcePath`, `-Version`, `-AllowUnsigned`, `-SkipDocker`, `-Force`.
2. `scripts/Uninstall.ps1` exists and consumes `install-record.json` with no parameters.
3. `scripts/Install.Helpers.psm1` exists and exports the functions listed in the API / CLI Surface section.
4. Running `.\scripts\Install.ps1` on a clean host with a signed bundle under `artifacts/publish/` installs the MSIX, starts the docker stack, and writes `install-record.json`.
5. Running `.\scripts\Uninstall.ps1` reverses the install and deletes `install-record.json`, while preserving `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
6. `-Force` performs a complete uninstall of any prior install of the same version before installing.
7. Manifest-integrity failure aborts before any destination folder is created, with a terminating error listing all discrepancies.
8. Docker-not-running with the docker stage enabled fails fast with a remediation message.
9. `.env` is never overwritten; `.env.example` is copied to `.env` only when `.env` is absent.
10. Pester coverage >= 90% on new lines in `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1`. Repo-wide line coverage remains >= 80%.
11. PoshQC suite (format -> analyze -> test) passes on all new and modified PowerShell files.
12. `README.md` and `docs/mailbridge-runbook.md` document the new install and uninstall flow without removing the scheduled-task path content.

### `spec.md` Seeded Test Conditions (10 items)

S1. End-to-end `.\scripts\Install.ps1` against a signed bundle installs the MSIX and brings the docker stack up.
S2. `.\scripts\Install.ps1 -SkipDocker` installs MSIX only and records `skipDocker = true`; `Uninstall.ps1` skips compose-down.
S3. `.\scripts\Install.ps1 -AllowUnsigned` installs a bundle produced with `Publish.ps1 -SkipSign`.
S4. `manifest.json` hash mismatch aborts install before any destination folder is created.
S5. `artifacts/publish/` empty of parseable version directories fails fast with a clear error.
S6. Docker Desktop not running with docker stage enabled fails fast with a remediation message.
S7. `.\scripts\Uninstall.ps1` on a healthy install removes MSIX, stops compose, removes destination, deletes install record, and preserves `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
S8. `.\scripts\Install.ps1 -Force` performs an implicit uninstall before reinstall.
S9. Existing `.env` at destination is not overwritten on re-install.
S10. Pester coverage >= 90% on new lines, repo-wide >= 80%.
S11. PoshQC format and analyze produce zero diagnostics on new PowerShell files.

### `user-story.md` Acceptance Criteria (14 items)

U1. `.\scripts\Install.ps1` no-args on clean host installs MSIX + brings docker stack up.
U2. `-SourcePath <path>` or `-Version <v>` overrides newest-bundle auto-detection.
U3. `-AllowUnsigned` installs `Publish.ps1 -SkipSign` bundle.
U4. `-SkipDocker` installs MSIX only and records `skipDocker = true` in the install record.
U5. `-Force` over an existing install performs complete uninstall of prior version before installing.
U6. `manifest.json` hash or size mismatch aborts install before any destination folder is created, with terminating error listing every discrepancy.
U7. Docker Desktop not running with docker stage enabled aborts install with remediation message.
U8. `.env.example` at destination `docker/` is copied to `.env` only when `.env` is absent; existing `.env` is never overwritten.
U9. `%LOCALAPPDATA%\OpenClaw\install-record.json` is written on successful install with the schema documented in spec.md.
U10. `.\scripts\Uninstall.ps1` reads install record and runs, in order, `docker compose down` (when `skipDocker` is false), `Remove-AppxPackage`, `Remove-Item` destination, and deletion of the install record. All steps run regardless of individual failures; failures collected and reported as single terminating error.
U11. User config at `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` is preserved by `Uninstall.ps1`.
U12. Pester coverage >= 90% on new lines in `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1`. Repo-wide coverage remains >= 80%.
U13. PoshQC suite passes on `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, and their Pester test files.
U14. `README.md` and `docs/mailbridge-runbook.md` document the new flow without displacing the scheduled-task install path.

## Acceptance Criteria Evaluation

### `spec.md` Definition of Done

| AC | Verdict | Evidence |
|---|---|---|
| 1. `Install.ps1` exists with five parameters | PASS | `scripts/Install.ps1` lines 57-69 declare all five parameters with the required types and `[ValidatePattern]`. `tests/scripts/Install.Tests.ps1` Context `parameter binding`. |
| 2. `Uninstall.ps1` exists with no parameters | PASS | `scripts/Uninstall.ps1` lines 18-19 declare `[CmdletBinding(SupportsShouldProcess=$true)] param()`. `tests/scripts/Uninstall.Tests.ps1` `missing install record` + `stage ordering`. |
| 3. `Install.Helpers.psm1` exports documented API | PASS | `scripts/Install.Helpers.psm1` lines 435-448 export all 13 functions. `tests/scripts/Install.Helpers.Tests.ps1` `export surface` asserts the full set. |
| 4. Clean-host happy path | PASS (unit) | `tests/scripts/Install.Tests.ps1` `stage ordering (happy path)` asserts the canonical 10-step order ending in `Write-InstallRecord`. Integration on a live host is out of scope for repo unit tests; the `definition-of-done-reconciliation` evidence acknowledges this. |
| 5. `Uninstall.ps1` reversal + MailBridge preserved | PASS | `tests/scripts/Uninstall.Tests.ps1` `stage ordering (happy path)` + `preserves user config` assert the required sequence and verify no `Remove-Item` invocation hits `...\OpenClaw\MailBridge\`. |
| 6. `-Force` full uninstall-before-install | PASS | `tests/scripts/Install.Tests.ps1` `-Force over existing install` asserts `Invoke-ComposeDown` -> `Invoke-MsixRemove` precede `Copy-BundleContents`. |
| 7. Manifest-integrity failure aborts pre-destination | PASS | `tests/scripts/Install.Tests.ps1` `manifest integrity failure` asserts `New-Item` is never invoked when `Test-ManifestIntegrity` throws. `tests/scripts/Install.Helpers.Tests.ps1` `Test-ManifestIntegrity` covers multi-discrepancy enumeration and missing-file cases. |
| 8. Docker-not-running remediation | PASS | `tests/scripts/Install.Tests.ps1` `docker not running` asserts the throw message contains `-SkipDocker`. `tests/scripts/Install.Helpers.Tests.ps1` `Test-DockerAvailable` covers the non-zero exit branch. |
| 9. `.env` guard | PASS | `tests/scripts/Install.Helpers.Tests.ps1` `Initialize-DotEnv` exercises both branches (copy when absent, no-op when present). |
| 10. Coverage thresholds | PASS | `artifacts/pester/powershell-coverage.xml` + `evidence/qa-gates/final-pester.2026-04-18T00-00.md`: Install.Helpers.psm1 96.32%, Install.ps1 90.29%, Uninstall.ps1 93.75%, repo-wide 86.39%. |
| 11. PoshQC suite passes | PASS | `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md` (EXIT_CODE 0, all files pre-formatted), `final-poshqc-analyze.2026-04-18T00-00.md` (zero diagnostics), `final-pester.2026-04-18T00-00.md` (143/143 pass). |
| 12. README and runbook updates preserve scheduled-task path | PASS | `docs/mailbridge-runbook.md` contains the new "Install Path D" section; `evidence/other/runbook-path-preservation.2026-04-18T00-00.md` confirms Paths A / B / C headings are preserved. `README.md` retains the scheduled-task bullet and adds the scripted-bundle bullet. |

### `spec.md` Seeded Test Conditions

| AC | Verdict | Evidence |
|---|---|---|
| S1 | PASS (unit) | `tests/scripts/Install.Tests.ps1` `stage ordering (happy path)`. |
| S2 | PASS | `tests/scripts/Install.Tests.ps1` `-SkipDocker path` (records `skipDocker = $true`); `tests/scripts/Uninstall.Tests.ps1` `skipDocker = true`. |
| S3 | PASS (unit) | `tests/scripts/Install.Tests.ps1` `administrator precheck on -AllowUnsigned` positive branch; `tests/scripts/Install.Helpers.Tests.ps1` `Invoke-MsixInstall` positive branch asserts the `-AllowUnsigned` flag reaches `Add-AppxPackage`. |
| S4 | PASS | `tests/scripts/Install.Tests.ps1` `manifest integrity failure`. |
| S5 | PASS | `tests/scripts/Install.Helpers.Tests.ps1` `Find-NewestPublishVersion` throw-on-empty branch. |
| S6 | PASS | `tests/scripts/Install.Tests.ps1` `docker not running`. |
| S7 | PASS | `tests/scripts/Uninstall.Tests.ps1` `stage ordering` + `preserves user config`. |
| S8 | PASS | `tests/scripts/Install.Tests.ps1` `-Force over existing install`. |
| S9 | PASS | `tests/scripts/Install.Helpers.Tests.ps1` `Initialize-DotEnv` `$true` branch. |
| S10 | PASS | See DoD item 10. |
| S11 | PASS | `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md` + `final-poshqc-analyze.2026-04-18T00-00.md`. |

### `user-story.md` Acceptance Criteria

| AC | Verdict | Evidence |
|---|---|---|
| U1 | PASS (unit) | DoD 4 evidence. |
| U2 | PASS | `tests/scripts/Install.Tests.ps1` `parameter binding` (`-SourcePath overrides newest-version auto-detect`) and `-Version path`. |
| U3 | PASS | Seeded S3 evidence. |
| U4 | PASS | Seeded S2 evidence. |
| U5 | PASS | DoD 6 evidence. |
| U6 | PASS | DoD 7 evidence. |
| U7 | PASS | DoD 8 evidence. |
| U8 | PASS | DoD 9 evidence. |
| U9 | PASS | `scripts/Install.ps1` lines 197-207 build the record with the documented schema fields; `tests/scripts/Install.Tests.ps1` `-SkipDocker path` `records skipDocker = $true` captures and inspects the record. |
| U10 | PASS | `scripts/Uninstall.ps1` lines 36-86 implement the exact order and the per-step failure collection. `tests/scripts/Uninstall.Tests.ps1` `stage ordering`, `partial state tolerance`, and `failure collection` cover the contract. |
| U11 | PASS | `tests/scripts/Uninstall.Tests.ps1` `preserves user config` asserts no `Remove-Item` call targets `MailBridge`. |
| U12 | PASS | DoD 10 evidence. |
| U13 | PASS | DoD 11 evidence. |
| U14 | PASS | DoD 12 evidence. |

## Summary

| Source | Total | PASS | PARTIAL | FAIL | UNVERIFIED |
|---|---|---|---|---|---|
| `spec.md` Definition of Done | 12 | 12 | 0 | 0 | 0 |
| `spec.md` Seeded Test Conditions | 11 | 11 | 0 | 0 | 0 |
| `user-story.md` Acceptance Criteria | 14 | 14 | 0 | 0 | 0 |
| Combined | 37 | 37 | 0 | 0 | 0 |

Note on unit-level verification: three criteria (DoD 4, S1, U1) describe a full end-to-end install on a live Windows host with Docker Desktop running and a signed MSIX. These are verified at the unit level via stage-ordering assertions with every helper mocked. This is the same pattern used by the precedent feature #34 (`tests/scripts/Publish.Tests.ps1`) and is required by the repo's unit-test policy that prohibits external services and real Appx side effects in tests. The `definition-of-done-reconciliation` evidence artifact explicitly records this boundary. The PASS verdict reflects unit-level satisfaction.

## Acceptance Criteria Check-off

All acceptance criteria in the authoritative source files were already checked off (`[x]`) by the executor during plan completion.

- `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` Definition of Done: 12/12 already `[x]`.
- `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` Seeded Test Conditions: 11/11 already `[x]`.
- `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` Acceptance Criteria: 14/14 already `[x]`.

Reviewer check-off delta: 0 items newly checked (all items were pre-checked and verification confirms their pre-checked state is correct).

Note on `issue.md`: the `## Acceptance Criteria` section in `issue.md` retains `- [ ]` markers because the work mode is `full-feature` and `issue.md` is not the authoritative AC source under that mode (per `acceptance-criteria-tracking`). The `minor-audit` mode would use `issue.md`; this feature is `full-feature`, so `issue.md` checkboxes are informational only and are intentionally not modified by this review.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` + `user-story.md`
- Total AC items: 37 (12 DoD + 11 Seeded Test Conditions + 14 user-story AC)
- Checked off (delivered): 37
- Remaining (unchecked): 0
- Items remaining: none

## Reviewer Go / No-Go

**Go**. All 37 acceptance-criteria items pass with cited evidence. Policy audit verdict is PASS. Code review produced zero blockers. Coverage, formatting, linting, and tests all pass against the resolved base branch `development`.
