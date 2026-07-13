# Coverage Delta / Threshold Verification (AC13)

Timestamp: 2026-07-12T11-35

Source: baseline `FEATURE/evidence/baseline/poshqc-coverage.2026-07-11T19-34.md` (P0-T9) vs. post-change `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-11T19-34.md` / `FEATURE/evidence/regression-testing/ac14-full-regression.2026-07-11T19-34.md` (P3-T4/P4-T3), all measured via the identical corrected-runsettings `Invoke-PoshQCTest` method for an apples-to-apples comparison.

## Line Coverage

| File | Baseline | Post-change | Delta | >= 85%? |
|---|---|---|---|---|
| `scripts/Install.ps1` | 88.57% (155/175) | 89.36% (168/188) | +0.79 pp | PASS |
| `OpenClawContainerValidation.psm1` | 91.67% (132/144) | 92.90% (157/169) | +1.23 pp | PASS |
| Aggregate | 89.97% (287/319) | 91.04% (325/357) | +1.07 pp | PASS |

## Instruction Coverage (branch-coverage proxy — Pester's built-in coverage tool produces no distinct `BRANCH` counter; see baseline artifact for the established repository precedent citing this limitation)

| File | Baseline | Post-change | Delta | >= 75%? |
|---|---|---|---|---|
| `scripts/Install.ps1` | 85.65% (179/209) | 86.55% (193/223) | +0.90 pp | PASS |
| `OpenClawContainerValidation.psm1` | 89.89% (169/188) | 91.40% (202/221) | +1.51 pp | PASS |
| Aggregate | 87.66% (348/397) | 88.96% (395/444) | +1.30 pp | PASS |

## No-Regression Check

Both files' line and instruction coverage percentages increased (not decreased) after the change — no regression on previously-covered lines. The denominator grew (175->188 lines for `Install.ps1`; 144->169 lines for the module) because new production lines were added by this fix; every newly-added line is exercised by the new unit tests (P1-T7 through P1-T10) and guard tests (P2-T1/P2-T7), so the added lines are not merely diluting the percentage — they are themselves covered.

## No-Exclusion Check

No production PowerShell file is excluded from measurement. The corrected `CodeCoverage.Path` used for both baseline and post-change runs lists exactly `scripts/Install.ps1` and `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` with an empty `ExcludedPath`, per the Coverage Exclusion Policy.

## Overall Verdict

**PASS.** Line coverage >= 85% and branch-coverage-proxy (instruction coverage) >= 75% on both production files, with no regression on previously-covered lines and no production file excluded from measurement.
