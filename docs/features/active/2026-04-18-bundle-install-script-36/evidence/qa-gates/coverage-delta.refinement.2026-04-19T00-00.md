# Coverage Delta (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: Compare `coverage-baseline.refinement.xml` (PB-T8) against `coverage-final.refinement.xml` (PG-T3).
EXIT_CODE: 0
Output Summary:

## Repo-scoped (5 in-scope files) line coverage

- Baseline (PB-T8): 95.26% (543/570)
- Post-change (PG-T3): 95.47% (548/574)
- Delta: +0.21 percentage points; 143 tests -> 150 tests (+7).
- Assertion `post-change >= baseline - 0`: PASS.

## Per-file targeted coverage

| File | Baseline | Post-change | Delta | Refinement-changed | >= 90% |
|---|---:|---:|---:|:---:|:---:|
| `scripts/Install.ps1` | 90.29% (93/103) | 91.01% (81/89) | +0.72 pp | YES | PASS |
| `scripts/Install.Helpers.psm1` | 96.32% (183/190) | 95.92% (188/196) | -0.40 pp | YES | PASS |
| `scripts/Uninstall.ps1` | 93.75% (45/48) | 93.75% (45/48) | 0.00 pp | no | PASS |
| `scripts/Publish.ps1` | 97.06% (66/68) | 97.14% (68/70) | +0.08 pp | YES | PASS |
| `scripts/Publish.Helpers.psm1` | 96.89% (156/161) | 97.08% (166/171) | +0.19 pp | YES | PASS |

All four refinement-changed production files maintain >= 90% line coverage. `Install.Helpers.psm1` slipped 0.40 pp but remains above the 90% floor; the drop reflects the new `Get-ManifestVersion` and schema-assertion lines, of which a small number of defensive-throw branches are not exercised under the 4 new Pester tests (branches are still reachable via negative-input tests, but the mocked `Get-Content` / `Test-Path` mock pair does not traverse every defensive branch). Repo-scoped coverage improved by +0.21 pp.

Acceptance: repo-wide >= 80% PASS; no-regression (post-change >= baseline) PASS; per-file targeted >= 90% PASS.
