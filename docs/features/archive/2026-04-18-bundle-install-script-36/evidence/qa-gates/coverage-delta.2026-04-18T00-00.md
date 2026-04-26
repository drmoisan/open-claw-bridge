# Coverage Delta

Timestamp: 2026-04-18T00-00
Command: Compare `baseline-pester.2026-04-18T00-00.md` vs `final-pester.2026-04-18T00-00.md` for repo-wide line coverage; targeted new-code coverage computed via per-file `Invoke-Pester -Configuration` runs.
EXIT_CODE: 0
Output Summary: PASS. Baseline repo-wide coverage 81.71%; post-change repo-wide coverage 86.39%; delta +4.68pp (no regression). Targeted new-code coverage exceeds the 90% floor on all three new production files (96.32% / 90.29% / 93.75%). Assertion `post-change >= baseline - 0` is satisfied.

## Numbers

| Scope | Baseline (Phase 0) | Post-change (Phase 5) | Delta |
|---|---|---|---|
| Repo-wide line coverage | 81.71% | 86.39% | +4.68pp |
| `scripts/Install.Helpers.psm1` | N/A (file did not exist) | 96.32% | N/A |
| `scripts/Install.ps1` | N/A (file did not exist) | 90.29% | N/A |
| `scripts/Uninstall.ps1` | N/A (file did not exist) | 93.75% | N/A |
| Repo-wide 80% policy floor | Met (81.71%) | Met (86.39%) | Improved |
| Per-new-file 90% policy floor | N/A | Met on all three files | — |
| Test pass count | 73 | 143 | +70 |
| Test failure count | 0 | 0 | 0 |

## Analysis

- The three new production files add 341 analyzed commands to the coverage denominator (190 + 103 + 48) and 321 executed commands (183 + 93 + 45), contributing 321/341 = 94.13% coverage on new code collectively.
- Repo-wide coverage rose +4.68pp because the new tests cover a higher percentage of commands than the existing baseline files do.
- Regression assertion: `post-change (86.39%) >= baseline (81.71%)` - PASS with a +4.68pp improvement.
- Policy thresholds:
  - `repo-wide >= 80%` - PASS (86.39%).
  - `new-code >= 90%` - PASS on all three files individually (96.32%, 90.29%, 93.75%) and collectively (94.13%).
