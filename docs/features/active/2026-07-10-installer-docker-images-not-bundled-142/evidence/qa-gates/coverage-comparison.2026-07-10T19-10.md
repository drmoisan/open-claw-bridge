# Coverage Delta / Threshold Verification (Issue #142, P5-T4)

Timestamp: 2026-07-10T19-10
Command (baseline): Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected> (P0-T8)
Command (post-change): Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected> (P5-T3)
EXIT_CODE: 0 (both)

Coverage tooling note: Pester reports command/statement coverage as the numeric metric (CoverageGutters/JaCoCo). It is used as the line-coverage headline and the branch/command proxy per the plan Conventions.

## Repo-wide (scripts/**)
- Baseline coverage: 89.34% (1,689 commands, 22 files)
- Post-change coverage: 89.91% (1,844 commands, 24 files; +2 files = the two new modules)
- No repo-wide regression: PASS (89.91% >= 89.34%; delta +0.57 pts)
- Repo-wide >= 85% line: PASS

## Per-file (5 changed/added production files)
| File | Line % | Instr % | Line>=85 | Instr>=75 |
|---|---|---|---|---|
| scripts/Publish.Docker.psm1  | 98.02% (99/101)  | 98.35% (119/121) | PASS | PASS |
| scripts/Install.Docker.psm1  | 87.50% (14/16)   | 92.00% (23/25)   | PASS | PASS |
| scripts/Publish.ps1          | 97.56% (80/82)   | 97.98% (97/99)   | PASS | PASS |
| scripts/Publish.Helpers.psm1 | 96.70% (88/91)   | 97.22% (105/108) | PASS | PASS |
| scripts/Install.ps1          | 88.57% (155/175) | 85.65% (179/209) | PASS | PASS |

## No production file excluded
- Corrected runsettings uses `ExcludedPath = @()`; CodeCoverage.Path = `scripts/*.ps1` + `scripts/*.psm1` covers all 24 production PowerShell files (including the two new modules). No production file is excluded from measurement. PASS.

## Remaining uncovered lines on changed files (visible cost, not a violation)
- The only uncovered lines on the two new modules are the `Invoke-DockerExe` seam bodies (`& docker @DockerArgs 2>&1` + the delegate call) — the thinnest possible host-bound wiring, left uncovered by design per the repo Coverage Exclusion Policy. All result-shaping logic was extracted into the pure, unit-tested `ConvertTo-DockerExeResult`, so the seam wiring is the minimal visible cost. Both files still clear the 85%/75% thresholds.

## Verdict
All gates PASS: no repo-wide regression, repo-wide >= 85%, every changed file >= 85% line and >= 75% instruction/command-proxy, no production file excluded. Outcome: PASS (not remediation-required).
