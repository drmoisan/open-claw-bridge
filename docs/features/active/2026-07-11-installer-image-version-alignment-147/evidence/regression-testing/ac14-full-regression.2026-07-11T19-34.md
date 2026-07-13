# AC14 — Full Regression Run (No New Failures)

Timestamp: 2026-07-12T11-05

Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\.vscode-insiders\extensions\danmoisan.drm-copilot-1.0.15\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root '<repo>' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'"` — full default-`Run.Path` suite (`scripts`, `tests/powershell`, `tests/scripts`), matching the exact method used for the P0-T9 baseline so results are apples-to-apples comparable.

EXIT_CODE: 0

## First attempt (before the P3-T4 remediation described below)

Running only the eight named AC14 files as a curated subset (rather than the full default `Run.Path`) initially showed 46 failures, including `CommandNotFoundException: Could not find Command Invoke-VersionStamp` in `Publish.ps1`/`Publish.Docker.Tests.ps1`/`Publish.Helpers.Tests.ps1`. Re-running via the full default `Run.Path` (this artifact's Command) eliminated all `Publish.*` failures, confirming the `Invoke-VersionStamp` errors were a test-isolation artifact of running a narrow curated file list out of the suite's natural discovery order, not a real regression — `Install.Docker.Tests.ps1` and all three `Publish.*.Tests.ps1` files show **0 failures** in every full-suite run, both before and after the fix below.

The full-suite run did, however, reveal a genuine regression: **19 new failures** in `tests/scripts/Install.Tests.ps1` (13) and `tests/scripts/Install.Force.Tests.ps1` (6). Root cause: both files' `$script:GetContentMock` had no `*docker-compose.yml` branch (mirroring the pre-P2-T1 state of `Install.DockerStage.Tests.ps1`), so the new `Assert-ComposeImageVersionAligned` guard call could not find a matching `image:` line and threw `RuntimeException: No matching 'image: openclaw/core:<tag>' line found...`, breaking every non-`-SkipDocker` scenario in both files.

## Remediation (per this task's own instruction: "If any test fails, apply the needed fix and restart from Phase 1 or Phase 2 as applicable, then re-run this task")

Applied the identical `*docker-compose.yml` mock branch already used in `Install.DockerStage.Tests.ps1` (P2-T1) to `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Force.Tests.ps1` (both returning `openclaw/core:1.2.3.0` / `openclaw/agent:1.2.3.0`, matching each file's existing `Get-ManifestVersion` mock of `'1.2.3.0'`). This is a test-fixture-only change (no production logic touched) required because the new Stage 9 guard is new, real behavior that these pre-existing non-`-SkipDocker` install scenarios now exercise. **This extends the plan's stated "Test files (2)" scope to 4 test files; flagged here and in the final completion report for visibility.** Restarted the Phase 2 toolchain loop (format -> analyze -> test) for the two newly-touched files: 0 files changed by format, 0 PSScriptAnalyzer findings, and a targeted re-run of `Install.Tests.ps1` + `Install.Force.Tests.ps1` + `Install.Docker.Tests.ps1` + `Install.DockerStage.Tests.ps1` showed **54 passed / 0 failed**.

## Final full-suite result (post-remediation)

Output Summary: **Tests Passed: 424, Failed: 9** (433 total; 416 baseline + 17 new tests [12 from `OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` + 5 new `Context 'image version alignment guard'` cases in `Install.DockerStage.Tests.ps1`] = 433). The 9 failures are byte-for-byte the same set recorded in the P0-T9 baseline (`Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` x1, `.Readyz.Tests.ps1` x2, `.Tests.ps1` x5, `.TokenPresence.Tests.ps1` x1) — confirmed identical by comparing `<testsuite name="..." failures="N">` counts in `artifacts/pester/pester-junit.xml` against the baseline artifact. **Zero new failures relative to the P0-T9 baseline.** AC14 is satisfied.

Coverage result parsed from `artifacts/pester/powershell-coverage.xml` for this same run (used again at P4-T3/P4-T4 as the post-change numeric coverage source):

| File | LINE missed/covered | Line coverage % | INSTRUCTION missed/covered | Instruction coverage % |
|---|---|---|---|---|
| `scripts/Install.ps1` | 20 / 168 (of 188) | 89.36% | 30 / 193 (of 223) | 86.55% |
| `OpenClawContainerValidation.psm1` | 12 / 157 (of 169) | 92.90% | 19 / 202 (of 221) | 91.40% |
| Aggregate | 32 / 325 (of 357) | 91.04% | 49 / 395 (of 444) | 88.96% |

All values are at or above baseline for both files (no regression); both exceed the 85% line / 75% branch-proxy thresholds.
