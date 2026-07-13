# PR Notes — installer-image-version-alignment (Issue #147)

Timestamp: 2026-07-12T11-55

## Summary

Adds a Stage 9 guard to `scripts/Install.ps1` that detects a version mismatch between the Control UI (`openclaw/core`) and gateway (`openclaw/agent`) container images pinned in the staged `docker-compose.yml`, and between those tags and the bundle's resolved version, aborting before `Invoke-DockerImageLoad`/`Invoke-ComposeUp` run. Supporting pure parse/compare functions are added to `OpenClawContainerValidation.psm1`.

## Files Changed

Production (3, all pre-existing, modified):
- `scripts/Install.ps1` — new script-scope local functions `Get-ComposeServiceImageTag`, `Assert-ComposeImageVersionAligned`; Stage 9 guard call wired in before `Invoke-DockerImageLoad`, inside the existing `-SkipDocker` gate. 455 -> 496 lines.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` — new exported functions `ConvertFrom-OpenClawImageReference`, `Test-OpenClawImageVersionAligned`; existing multi-line comment-based help on several probe/helper functions condensed to single-line `.SYNOPSIS` (behavior-preserving) to stay under the 500-line cap. 452 -> 495 lines.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` — `FunctionsToExport` extended with both new function names.

Test files (originally scoped: 2; extended during P3-T4 remediation to 4 — see Notable Finding below):
- NEW `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` (110 lines, 12 tests).
- MODIFIED `tests/scripts/Install.DockerStage.Tests.ps1` — new `Get-Content` mock branch for `*docker-compose.yml`; new `Context 'image version alignment guard'` (5 tests). 202 -> 304 lines.
- MODIFIED `tests/scripts/Install.Tests.ps1` — same `*docker-compose.yml` mock branch added (fixture-only fix; see Notable Finding). 485 -> 491 lines.
- MODIFIED `tests/scripts/Install.Force.Tests.ps1` — same `*docker-compose.yml` mock branch added (fixture-only fix; see Notable Finding). 249 -> 255 lines.

## Notable Finding (flagged for reviewer attention)

The plan's stated scope was exactly 2 test files. Implementing the Stage 9 guard exactly as specified caused 19 new failures in the AC14-named regression files `Install.Tests.ps1` (13) and `Install.Force.Tests.ps1` (6), because those files' shared `Get-Content` mock had no `docker-compose.yml` fixture branch — the guard is new, real behavior that both files' non-`-SkipDocker` install scenarios now exercise. Per the plan's own P3-T4 task text ("If any test fails, apply the needed fix... then re-run this task"), the identical fixture branch already used in `Install.DockerStage.Tests.ps1` was applied to both files (test-fixture-only change, no production logic touched), and the toolchain was rerun clean. Full detail: `evidence/regression-testing/ac14-full-regression.2026-07-11T19-34.md`.

## Risks

- Regex-based `Get-ComposeServiceImageTag` assumes the compose file's structural shape enforced by `Convert-ComposeToBundleCompose` (4-space `image:` indentation). A publish-side formatting change without updating this guard would cause a false "no matching image: line found" error on an otherwise-valid bundle. Documented in `spec.md` Risks & Mitigations.
- Intentional duplication of the pure tag-vs-version comparison between the module function (`Test-OpenClawImageVersionAligned`) and the `Install.ps1`-local helper (`Assert-ComposeImageVersionAligned`) creates two places that must stay semantically consistent if comparison rules change. AC11 (no `Import-Module` in `Install.ps1`) gives feature-review a concrete, repeatable check against accidental re-introduction of the module-import class of defect from #142/#144.

## Validation Performed

- Full PowerShell toolchain (format -> analyze -> test-with-coverage) run over the full repository: 0 format changes, 0 analyzer findings, 424/433 tests passing (9 pre-existing baseline failures, unrelated to this change, in `Invoke-OpenClawContainerPathValidation.*.Tests.ps1`).
- Coverage: `Install.ps1` 89.36% line / 86.55% instruction; `OpenClawContainerValidation.psm1` 92.90% line / 91.40% instruction — both above the 85%/75% thresholds and improved over baseline.
- AC1-AC14 all verified PASS; `spec.md`'s Acceptance Criteria section fully checked off.
- #142 and #144 invariants verified green; all 8 named non-goal files confirmed byte-identical (`git diff --name-only` empty).

## Evidence Links

- Baseline: `evidence/baseline/phase0-instructions-read.2026-07-11T19-34.md`, `evidence/baseline/poshqc-format.2026-07-11T19-34.md`, `evidence/baseline/poshqc-analyze.2026-07-11T19-34.md`, `evidence/baseline/poshqc-coverage.2026-07-11T19-34.md`
- QA gates: `evidence/qa-gates/phase1-poshqc-loop.2026-07-11T19-34.md`, `evidence/qa-gates/phase2-poshqc-loop.2026-07-11T19-34.md`, `evidence/qa-gates/final-poshqc-format.2026-07-11T19-34.md`, `evidence/qa-gates/final-poshqc-analyze.2026-07-11T19-34.md`, `evidence/qa-gates/final-poshqc-test.2026-07-11T19-34.md`, `evidence/qa-gates/coverage-comparison.2026-07-11T19-34.md`, `evidence/qa-gates/non-goal-diffs.2026-07-11T19-34.md`, `evidence/qa-gates/ac-summary.2026-07-11T19-34.md`
- Regression: `evidence/regression-testing/ps-expect-fail-image-version-guard.2026-07-11T19-34.md`, `evidence/regression-testing/ac11-no-import-module.2026-07-11T19-34.md`, `evidence/regression-testing/ac9-issue142-invariants.2026-07-11T19-34.md`, `evidence/regression-testing/ac10-issue144-invariants.2026-07-11T19-34.md`, `evidence/regression-testing/ac14-full-regression.2026-07-11T19-34.md`
- Issue update mirror: `evidence/issue-updates/issue-147.2026-07-11T19-34.md`
