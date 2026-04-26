# Coverage Delta

- Timestamp: 2026-04-18T00-00
- Command: `Compare baseline-pester.2026-04-18T00-00.md vs final-pester.2026-04-18T00-00.md for repo-wide line coverage; targeted coverage computed via Invoke-Pester -Configuration on scripts/Publish.ps1 and scripts/Publish.Helpers.psm1.`
- EXIT_CODE: 0
- Output Summary: PASS. Baseline repo-wide coverage 67.13%; post-change repo-wide coverage 81.71%; delta +14.58pp (no regression — post-change >= baseline). Targeted new-code coverage (Publish.ps1 + Publish.Helpers.psm1) 96.94% (>= 90% threshold). Assertion `post-change >= baseline - 0` is satisfied.

## Numbers

| Scope | Baseline | Post-change | Delta |
|---|---|---|---|
| Repo-wide line coverage | 67.13% | 81.71% | +14.58pp |
| Targeted new-code coverage (`Publish.ps1` + `Publish.Helpers.psm1`) | N/A (files did not exist) | 96.94% | N/A |
| Repo-wide 80% policy floor | Not met (67.13%) | Met (81.71%) | Improved |
| Test pass count | 28 (includes 7 retired build-msix tests) | 72 | +44 |
| Test failure count | 0 | 0 | 0 |

## Analysis

- The baseline repo-wide value was 67.13% because the coverage scope included several `scripts/*.ps1` production files without dedicated tests (`Build.ps1`, `Test.ps1`, `Run-Bridge.ps1`, `Run-Client.ps1`, `dev-tools/run-actionlint.ps1`), and the retired `scripts/build-msix.ps1` (241 lines) dragged the denominator up.
- Retirement of `scripts/build-msix.ps1` in Phase 3 removed its 241 lines from the denominator. The two new files add well-tested lines (>= 90% targeted coverage). Net effect: repo-wide coverage crossed the 80% policy floor without adding any new untested code.
- Regression assertion: `post-change (81.71%) >= baseline (67.13%)` — PASS with a +14.58pp improvement.
- Policy thresholds: `repo-wide >= 80%` — PASS (81.71%); `new-code >= 90%` — PASS (96.94%).
