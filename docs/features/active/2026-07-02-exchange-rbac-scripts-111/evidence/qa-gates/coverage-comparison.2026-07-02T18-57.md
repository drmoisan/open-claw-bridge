# Final QA — Coverage Delta / Threshold Verification (P5-T4)

Timestamp: 2026-07-02T18-57
Command: comparison of baseline artifact `evidence/baseline/poshqc-test.2026-07-02T17-25.md` (P0-T5) against post-change artifact `evidence/qa-gates/final-poshqc-test.2026-07-02T18-55.md` (P5-T3); per-file values parsed from `artifacts/pester/powershell-coverage.xml`.
EXIT_CODE: 0

## Numeric comparison

| Measure | Baseline (P0-T5) | Post-change (P5-T3) | Delta |
|---|---|---|---|
| Repo-wide command coverage | 88.47% (1,752 commands, 21 files) | 89.66% (1,963 commands, 29 files) | +1.19 pp |
| Tests passing | 281 | 358 | +77 (new feature suite) |
| New-code line coverage (MODULE/** + entry script) | n/a (files did not exist) | 168/169 = 99.41% (command: 99.53%) | new |

## Threshold verdicts

- **(a) New-code coverage — line >= 85%: PASS.** 99.41% line (168/169; command 99.53%) across `scripts/powershell/modules/OpenClawRbac/**` plus `scripts/Invoke-OpenClawExchangeRbacSetup.ps1`. Lowest single file: OpenClawRbac.Seams.ps1 at 98.33% (one uncovered line). Branch >= 75%: Pester v5 produces no branch metric for PowerShell; the command-coverage figure (99.53%) counts every command in every branch arm, and all conditional arms in the new code are exercised by tests (idempotent/create, ShouldProcess true/false, missing/resolved cmdlet, existing-ACE/other-error, all four boundary matrix cells, both parameter-set routes). Recorded as PASS on the branch-sensitive command metric, consistent with repository precedent (features #58, #62); the absence of a native PowerShell branch percentage is a documented toolchain limitation, not a placeholder.
- **(b) No repo-wide coverage regression: PASS.** 89.66% post-change vs 88.47% baseline (+1.19 pp) on the identical command metric and identical pre-existing file set (the 21 baseline files are unchanged; the 8 new files raised the aggregate).
- **(c) No production file excluded from measurement: PASS.** The final coverage scope contains all 21 baseline production `scripts/**` files plus all 8 new measurable production files (29 total). The only feature file not in the denominator is `OpenClawRbac.psd1`, a data-only manifest with no executable code (type-only/data module clarification in `.claude/rules/general-unit-test.md`). `.claude/hooks/**` remains outside coverage per the documented Issue #66 T4-scaffolding coverage-scope exclusion, unchanged from baseline.

## Overall: PASS
