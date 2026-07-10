# PR Notes (Issue #142)

Timestamp: 2026-07-10T19-10

## Summary
Ships prebuilt container images inside the publish bundle and loads them at install time, so the scripted-bundle install no longer tries to pull (denied) or build (no `src/` in the bundle context) and fails at `docker compose up`.

## Changed / added production files (5)
- NEW `scripts/Publish.Docker.psm1` — `Invoke-DockerExe` seam + `ConvertTo-DockerExeResult`, `Resolve-OpenClawAgentBaseImage`, `Build-OpenClawDockerImage`, `Save-OpenClawDockerImage`, `Convert-ComposeToBundleCompose`, `Write-BundleCompose`, `Invoke-PublishDockerStage`.
- NEW `scripts/Install.Docker.psm1` — self-contained `Invoke-DockerExe` seam + `ConvertTo-DockerExeResult` + `Invoke-DockerImageLoad`.
- MOD `scripts/Publish.ps1` — imports Publish.Docker.psm1; Stage 3b (build/save/transform) after Copy-DockerArtifact, before MSIX and manifest; Stage 5 message names Install.Docker.psm1.
- MOD `scripts/Publish.Helpers.psm1` — `New-ManifestEntry` size `[int]`->`[long]`; `Copy-DockerArtifact` stops copying docker-compose.dev.yml; `Copy-InstallScriptsIntoBundle` stages Install.Docker.psm1.
- MOD `scripts/Install.ps1` — imports Install.Docker.psm1; Stage 9 calls Invoke-DockerImageLoad before Invoke-ComposeUp inside the -SkipDocker gate.

## New / updated test files (7)
- NEW `tests/scripts/Publish.Docker.Tests.ps1`, `tests/scripts/Install.Docker.Tests.ps1`, `tests/scripts/Install.DockerStage.Tests.ps1`.
- MOD `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1`.

## Validation performed
- PoshQC format: clean. PoshQC analyze: 0 findings.
- Pester (full tests/scripts): 406 passed, 0 failed.
- Coverage (corrected runsettings): repo-wide 89.91% (baseline 89.34%, no regression); all 5 changed files >= 85% line and >= 75% instruction. Evidence: evidence/qa-gates/coverage-comparison.2026-07-10T19-10.md.
- Non-goal invariants: git diff empty for docker-compose.yml, docker-compose.dev.yml, deploy/docker, Install.Helpers.psm1, Uninstall.ps1, and the two Install.Helpers test files.
- Fail-before evidence: evidence/regression-testing/ps-expect-fail-manifest-size and ps-expect-fail-install-load.

## Risks
- Build-arg / compose-structure drift is caught by exact arg-vector tests and the transform drift-guard (throws).
- Bundle size grows by the combined image tar (>2 GiB plausible); the `[long]` manifest fix removes the overflow failure mode.

## Manual post-merge validation (from spec.md Rollout)
- Run the full `Publish.ps1` -> `Install.ps1` integration retest on a machine without the `src/` tree in the bundle; confirm both services start healthy from loaded images with no build or registry attempt (`pull_policy: never`).

## Evidence index
- Baselines: evidence/baseline/ (phase0-instructions-read, poshqc-format, poshqc-analyze, poshqc-test)
- Per-phase loops: evidence/qa-gates/phase1..phase4-poshqc-loop
- Final QA: evidence/qa-gates/final-poshqc-format, final-poshqc-analyze, final-poshqc-test, coverage-comparison, invariant-diffs, seam-hermeticity-checks, ac-summary
- Fail-before: evidence/regression-testing/ps-expect-fail-manifest-size, ps-expect-fail-install-load
- Issue mirror: evidence/issue-updates/issue-142.2026-07-10T19-10.md
