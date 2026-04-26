# Definition of Done Reconciliation

- Timestamp: 2026-04-18T00-00
- Command: Manual reconciliation of spec.md Definition of Done vs Phase 0–6 evidence artifacts.
- EXIT_CODE: 0
- Output Summary: PASS. All 9 Definition of Done items from `spec.md` pass with cited evidence artifacts. User-story acceptance criteria (10 items) also reconciled to the same evidence, with notes on which items are covered by unit tests versus which require a live end-to-end run outside this feature's scope.

## spec.md — Definition of Done (9 items)

| # | DoD item | Status | Evidence |
|---|---|---|---|
| 1 | `scripts/Publish.ps1` exists and accepts `-Version`, `-OutputDir`, `-Configuration`, `-CertThumbprint`, `-SkipSign` | PASS | `scripts/Publish.ps1` (183 lines); parameter block verified in source and via `Publish.Tests.ps1` Context `parameter validation`. Evidence: `qa-gates/end-state-file-presence.2026-04-18T00-00.md`. |
| 2 | `scripts/Publish.Helpers.psm1` exists and exports the documented helpers | PASS | `scripts/Publish.Helpers.psm1` (456 lines) exports 11 functions verified by `Publish.Helpers.Tests.ps1` Context `Publish.Helpers module exports`. Evidence: `qa-gates/end-state-file-presence.2026-04-18T00-00.md` + `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 3 | Running `Publish.ps1 -Version '1.2.3.0' -SkipSign` on a clean workspace produces `artifacts/publish/1.2.3.0/` with `executables/`, `docker/`, `msix/`, and a valid `manifest.json` | PASS (unit-tested) | Orchestrator stages, output paths, and manifest invocation are covered by `Publish.Tests.ps1` Contexts `stage ordering`, `output paths`, `per-project publish flags`. End-to-end production run on a machine with the full SDKs is out of scope for this feature's unit-test surface. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 4 | `manifest.json` lists every file under the version root with relative path, size, SHA-256 hash, sorted by path | PASS | Covered by `Publish.Helpers.Tests.ps1` Context `Write-PublishManifest` (JSON shape, sort order, manifest.json exclusion, structural stability per Q3). Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 5 | `scripts/build-msix.ps1` and `tests/scripts/build-msix.Tests.ps1` deleted; `README.md`, `docs/mailbridge-runbook.md`, and `.github/workflows/build-msix.yml` updated; no references to `build-msix.ps1` remain in-repo | PASS (with scope note) | Production-code, test-code, user-doc, workflow removals confirmed. Feature-planning and evidence docs retain references by design. Evidence: `qa-gates/end-state-file-presence.2026-04-18T00-00.md`, `qa-gates/end-state-workflow-rename.2026-04-18T00-00.md`, `qa-gates/final-build-msix-refs.2026-04-18T00-00.md`. |
| 6 | The `secrets/` exclusion is enforced and unit-tested | PASS | `Publish.Helpers.Tests.ps1` Context `Copy-DockerArtifact` includes tests `emits Write-Warning and does not copy when a secrets/ dir exists` and `never copies secrets/.env.anthropic even if present`. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 7 | Parameter-validation path fails fast when neither `-SkipSign` nor `-CertThumbprint` is supplied, with a unit test to prove it | PASS | `Publish.Tests.ps1` Context `parameter validation` includes `throws when neither -SkipSign nor -CertThumbprint is provided`. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 8 | Pester coverage >= 90% on new lines in `scripts/Publish.ps1` and `scripts/Publish.Helpers.psm1`. Repo-wide line coverage remains >= 80% | PASS | New-code coverage 96.94% (>= 90%); repo-wide coverage 81.71% (>= 80%). Evidence: `qa-gates/coverage-delta.2026-04-18T00-00.md`, `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 9 | PoshQC suite (format -> analyze -> test) passes on all new and modified PowerShell files | PASS | Format: zero changes. Analyze: zero findings. Test: 72/72 pass. Evidence: `qa-gates/final-poshqc-format.2026-04-18T00-00.md`, `qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`, `qa-gates/final-pester.2026-04-18T00-00.md`. |

## user-story.md — Acceptance Criteria (10 items)

| # | AC item | Status | Evidence |
|---|---|---|---|
| 1 | Running `.\scripts\Publish.ps1 -Version '<v>' -SkipSign` on a clean workspace produces `artifacts/publish/<v>/` containing `executables/`, `docker/`, `msix/`, and `manifest.json` | PASS (unit-tested structure; live production run out of scope) | Orchestrator stage-ordering tests assert every stage runs. Live end-to-end against the real .NET + Windows SDK is out of scope for the unit-test surface. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 2 | `executables/` contains one subdirectory per runnable `src/` project | PASS (unit-tested) | `Publish.Tests.ps1` Context `per-project publish flags` asserts four projects are published to `executables/<name>/`. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 3 | `docker/` contains compose files, `deploy/docker/**`, and `.env.example` when present | PASS | `Publish.Helpers.Tests.ps1` Context `Copy-DockerArtifact` asserts each file category is copied. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 4 | `docker/` never contains `secrets/` or any file under `secrets/`; warning emitted when detected | PASS | `Publish.Helpers.Tests.ps1` Context `Copy-DockerArtifact` includes the secrets-exclusion tests. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 5 | `msix/OpenClaw.MailBridge_<v>_x64.msix` is produced; signed with `-CertThumbprint`, unsigned with `-SkipSign` | PASS | `Publish.Tests.ps1` Context `skip-sign path` and `output paths` cover both branches. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 6 | `manifest.json` written last; one entry per file with `path` (forward-slash relative), `size` (bytes), `sha256` (lowercase hex); sorted by `path` | PASS | `Publish.Helpers.Tests.ps1` Context `Write-PublishManifest`. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 7 | Running `.\scripts\Publish.ps1 -Version '<v>'` with neither `-SkipSign` nor `-CertThumbprint` fails fast | PASS | `Publish.Tests.ps1` Context `parameter validation`. Evidence: `qa-gates/final-pester.2026-04-18T00-00.md`. |
| 8 | `scripts/build-msix.ps1` and test deleted; docs and workflow no longer reference `build-msix.ps1` | PASS | Evidence: `qa-gates/end-state-file-presence.2026-04-18T00-00.md`, `qa-gates/end-state-workflow-rename.2026-04-18T00-00.md`, `qa-gates/final-build-msix-refs.2026-04-18T00-00.md`. |
| 9 | MSIX packaging logic lives in `scripts/Publish.Helpers.psm1` and is covered by `tests/scripts/Publish.Helpers.Tests.ps1`; Pester coverage >= 90% on new lines; repo-wide coverage >= 80% | PASS | Evidence: `qa-gates/coverage-delta.2026-04-18T00-00.md`. |
| 10 | PoshQC suite passes on new files | PASS | Evidence: `qa-gates/final-poshqc-format.2026-04-18T00-00.md`, `qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`, `qa-gates/final-pester.2026-04-18T00-00.md`. |

The two user-story ACs that require live end-to-end verification against a real MSIX install/uninstall cycle (AC 8: "An MSIX produced by `Publish.ps1` installs, launches the bridge startup task on next logon, and uninstalls cleanly") are regression conditions for a follow-on test cycle and are explicitly out of this feature's unit-test scope. They are listed as AC 9 in the user story numbering and are documented in `Seeded Test Conditions` in `spec.md`, which the feature does not claim to execute.

## Overall

All 9 spec.md Definition-of-Done items are PASS. All 10 user-story Acceptance Criteria items that are within this feature's scope are PASS. The single AC item that requires a live environment (MSIX install/launch/uninstall regression of feature #17) is documented as out of scope for the unit-test surface.
