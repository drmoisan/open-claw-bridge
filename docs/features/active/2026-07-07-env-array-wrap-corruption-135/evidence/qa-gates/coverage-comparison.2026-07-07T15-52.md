# Coverage Delta / Threshold Verification — Issue #135

Timestamp: 2026-07-07T15-52

Command: analysis derived from `FEATURE/evidence/baseline/poshqc-test.2026-07-07T15-36.md` (P0-T8) and `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-07T15-49.md` (P2-T3); per-file breakdown extracted from the post-change raw tool output `artifacts/pester/powershell-coverage.koverage.xml` (Cobertura-style XML written by the corrected-runsettings run).

EXIT_CODE: 0 (both underlying test runs referenced above completed with `LASTEXITCODE=0`)

## Repo-Wide Coverage

| Metric | Baseline (P0-T8) | Post-change (P2-T3) | Delta |
|---|---|---|---|
| Command/line coverage | 89.94% (2,017 analyzed Commands, 30 Files) | 89.93% (2,015 analyzed Commands, 30 Files) | -0.01 pp |
| Tests passed | 365/365 | 367/367 | +2 (the two new regression tests) |
| Files measured | 30 | 30 | unchanged — no production file excluded |

**PASS** — Line coverage 89.93% >= 85% threshold. Command-coverage branch proxy 89.93% >= 75% threshold (per established repo precedent, F11/F16, Pester v5 emits command-level coverage only). No production PowerShell file was excluded from measurement (30 files measured both before and after, matching the full `scripts/**` `.ps1`/`.psm1` glob with no `ExcludedPath` entry).

The 0.01 percentage point repo-wide decrease is fully explained by the analyzed-command count dropping from 2,017 to 2,015 — exactly the two redundant `@(...)` wrap lines removed by this fix (one in `scripts/Publish.ps1`, one in `scripts/New-MsixDevCert.ps1`). Both removed lines were unconditionally executed (covered) by every test that invokes each script, so removing them shrinks both the numerator and denominator by one already-covered unit each, which mechanically nudges the ratio down by a fraction of a percentage point. This is not a gap in test coverage of any surviving or new code.

## Per-Changed-File Coverage

Extracted from the post-change Cobertura-style XML (`artifacts/pester/powershell-coverage.koverage.xml`, generated 2026-07-07T15:45 local):

| File | Post-change LINE coverage | Reconstructed pre-change LINE coverage | Basis for reconstruction |
|---|---|---|---|
| `scripts/Publish.ps1` | 77/79 = 97.47% (missed=2, covered=77) | 78/80 = 97.50% (missed=2, covered=78) | Pre-change file had exactly one additional line (the removed `@(...)` wrap), and that line was unconditionally executed by every `Publish.Tests.ps1` test that invokes `& $script:ScriptPath` (all of them reach line 118 before any conditional branch), so it was covered under the pre-change baseline. |
| `scripts/New-MsixDevCert.ps1` | 21/44 = 47.73% (missed=23, covered=21) | 22/45 = 48.89% (missed=23, covered=22) | Same reasoning: the removed line (72) sits at the top of `Save-CertThumbprintToEnv`, unconditionally executed by every test in the `Save-CertThumbprintToEnv (AC-5)` context and the new regression context, so it was covered pre-change. |

The raw baseline coverage XML (P0-T8) was written to the same scratchpad-configured output path as the P2-T3 run and was overwritten when P2-T3 executed; the pre-change per-file percentages above are reconstructed analytically rather than read from a preserved raw file. The reconstruction is exact, not estimated: removing an always-covered, unconditional single-statement assignment line necessarily decreases both the covered-line count and the total-line count by exactly one for that file, and no other production line in either file changed.

**Assessment against `.claude/rules/general-unit-test.md`'s "no regression on changed lines" gate:** this gate concerns the lines that were changed, not the file's aggregate ratio. Line-level hit data for the exact changed lines confirms both are covered post-change:
- `scripts/Publish.ps1` line 118 (`$envContent = Read-EnvFileContent -Path $EnvFilePath`): `mi="0" ci="1"` (0 missed instructions, hit at least once).
- `scripts/New-MsixDevCert.ps1` line 72 (`$content = Read-EnvFileContent -Path $EnvPath`): `mi="0" ci="1"` (0 missed instructions, hit at least once).

**PASS** on the "no regression on changed lines" gate — both changed lines are covered. The per-file aggregate percentage decrease (0.03 pp for `Publish.ps1`, 1.16 pp for `New-MsixDevCert.ps1`) is an arithmetic consequence of deleting an already-covered line and does not represent any newly-untested behavior; both files' new regression tests (P1-T7, P1-T8) directly exercise the fixed call sites and pass.

## Overall Disposition

- Repo-wide line coverage: PASS (89.93% >= 85%).
- Repo-wide branch-coverage proxy: PASS (89.93% >= 75%).
- No production file excluded from measurement: PASS (30/30 files measured, unchanged).
- No regression on changed lines: PASS (both changed lines confirmed covered, `mi=0`).
- Per-file aggregate ratio: informational note recorded above (small mechanical decrease from line deletion, not a functional test gap); no changed line is uncovered.

All thresholds required by `.claude/rules/general-unit-test.md` and `.claude/rules/quality-tiers.md` are satisfied. No remediation is required.
