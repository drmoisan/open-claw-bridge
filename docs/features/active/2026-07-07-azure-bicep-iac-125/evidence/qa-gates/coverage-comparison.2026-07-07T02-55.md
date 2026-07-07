# Coverage Delta / Threshold Verification (P6-T4)

- Timestamp: 2026-07-07T02-55 (original comparison, blocked); remediation capture 2026-07-07T04-45
- Command: comparison of `evidence/baseline/poshqc-test.2026-07-07T01-30.md` (P0-T5) against `evidence/qa-gates/final-poshqc-test.2026-07-07T02-50.md` (P6-T3) and `evidence/qa-gates/bicep-secret-scan-poshqc-test.2026-07-07T02-35.md` (P5-T5).
- EXIT_CODE: n/a (comparison of prior artifacts, not a new tool invocation)
- Output Summary: **Coverage comparison complete via the F11-precedent corrected-runsettings bundled-pipeline workaround. Disposition: PASS.**

## Values

| Metric | Baseline (P0-T5, 29 files) | Post-change (P6-T3, 30 files) | New-code (`Test-OpenClawBicepParameterSecrets.ps1`) |
|---|---|---|---|
| Command/line coverage (Pester v5 command-level; command-proxy for branch, per F11 precedent) | 89.66% (1,760/1,963 instructions covered; LINE 1,393/1,544 = missed 151... see note) | 89.94% (1,814/2,017 instructions covered) | 100% (54/54 instructions; 38/38 lines) |
| Test pass count | 358/358 | 365/365 | 7/7 (subset of the 365) |

Note: the baseline's LINE counter (missed=151, covered=1393, total 1544) and the post-change LINE counter (missed=151, covered=1431, total 1582) both hold the pre-existing 29 files' figures unchanged (missed=151 in both), confirming the new file (38 lines, 0 missed) accounts for the entire post-change delta.

## Explanation

The MCP `run_poshqc_test` tool still fails identically to the P0-T5/P5-T5/P6-T3 root cause (bundled `CodeCoverage.Path` hardcoded to `drm-copilot`-repo-only files). Per the established F11 precedent (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/baseline/poshqc-test.2026-07-02T17-25.md`), the bundled `PoshQC.psd1` module was imported directly and `Invoke-PoshQCTest` was called with a scratchpad-only corrected-`CodeCoverage.Path` runsettings file (never committed to the repo tree), listing this repository's actual `scripts/**` production files.

- **No repo-wide coverage regression versus baseline**: **confirmed, numerically**. Baseline command coverage over the 29 pre-existing production files was 89.66% (1,760 covered / 1,963 total instructions). Post-change coverage over all 30 files (the 29 pre-existing files plus the new `Test-OpenClawBicepParameterSecrets.ps1`) is 89.94% (1,814 covered / 2,017 total instructions). The pre-existing 29 files' covered-instruction count is unchanged at 1,760 both before and after (1,814 post-change covered minus 54 new-file covered instructions = 1,760); no pre-existing file's coverage regressed. The repo-wide percentage moved up (not down) because the new file is 100% covered, which is a net improvement, not a regression.
- **New-code coverage for `scripts/Test-OpenClawBicepParameterSecrets.ps1` >= 85% line / >= 75% branch (command-proxy)**: **PASS**. Measured coverage is 100% for both the INSTRUCTION (command) counter (54/54) and the LINE counter (38/38), exceeding both thresholds. This required adding two tests (documented at P5-T5) covering the script's main entry-point block (`exit 0` clean path and `exit 1` dirty path), which the original 5 tests — all of which dot-sourced the script and called the function directly — never exercised.
- **Branch coverage**: per the F11 precedent and `.claude/rules/powershell.md`'s Pester v5.x convention, Pester emits command-level coverage only, with no separate branch-percentage metric for PowerShell. The command-coverage percentage is recorded as the branch-sensitive signal, since command coverage counts commands inside every branch arm — an untaken branch arm registers as one or more uncovered commands. This is recorded exactly as F11 recorded it, not invented for this feature.
- **No production PowerShell file excluded from measurement**: **confirmed**. The corrected `CodeCoverage.Path` lists all 30 `scripts/**` `.ps1`/`.psm1` files present in the repository at this commit, verified by direct enumeration (`Get-ChildItem -Path scripts -Recurse -Include *.ps1,*.psm1 -File` returns exactly 30 files, one-for-one matching the settings list), with no `ExcludedPath` entries — unlike the bundled `drm-copilot` settings, which exclude several of that repo's own CLI-wrapper scripts. Per `.claude/rules/general-unit-test.md`'s Coverage Exclusion Policy, no production file may be excluded, and none was.

## Disposition

**PASS.** Repo-wide coverage did not regress (89.66% -> 89.94%, a net improvement attributable to the new file's 100% coverage), the new script (`scripts/Test-OpenClawBicepParameterSecrets.ps1`) meets both the 85% line and 75% branch (command-proxy) thresholds at 100%, and no production PowerShell file was excluded from measurement. The MCP `run_poshqc_test` tool itself remains defective for this repository (bundled `CodeCoverage.Path` allowlist hardcoded to the `drm-copilot` source repo); this comparison used the F11-precedent corrected-runsettings bundled-pipeline workaround, which invokes the same underlying `Invoke-PoshQCTest` code path with only the coverage-path input corrected, and is not a fabricated result.
