# Final QC — PoshQC Test-and-Coverage (Issue #144, P2-T4)

- Timestamp: 2026-07-10T20-30

## Attempt 1 — MCP tool, coverage mode

- Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root=`C:\Users\DanMoisan\repos\open-claw-bridge`, scan_folders=`["tests/scripts"]`)
- EXIT_CODE: 4294967295 (tool returned `"ok":false`)
- Output Summary: Reproduces the established bundled-runsettings coverage-path defect (`#111`, `#125`, `#135`, `#137`, `#139`, `#142`). Numeric coverage is captured via the corrected-runsettings workaround (Attempt 2).

## Attempt 2 — Corrected-runsettings Invoke-PoshQCTest (numeric coverage source)

- Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\repos\drm-copilot\packages\mcp-server\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root 'C:\Users\DanMoisan\repos\open-claw-bridge' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'"` (corrected settings measure exactly the 2 in-scope production files; empty `ExcludedPath` per the Coverage Exclusion Policy)
- EXIT_CODE: 0
- Output Summary: **Tests Passed: 416, Failed: 0.** Coverage parsed from `artifacts/pester/powershell-coverage.xml` (JaCoCo; Pester's PowerShell coverage tool emits `INSTRUCTION`/`LINE`/`METHOD`/`CLASS`, no `BRANCH` counter, so instruction/command coverage is used as the branch-coverage proxy per established repository precedent, e.g. `docs/features/active/2026-07-07-env-array-wrap-corruption-135/evidence/qa-gates/coverage-comparison.2026-07-09T19-13.md`):
  - Aggregate `<report>` counters across the 2 production files: `LINE missed="23" covered="280"` -> **line coverage = 280/303 = 92.41%**.
  - Aggregate `INSTRUCTION missed="34" covered="377"` -> **command/instruction coverage (branch proxy) = 377/411 = 91.73%**.

## Coverage Comparison vs Baseline (P0-T10)

| Metric | Baseline (P0-T10) | Post-Change (P2-T4) | Delta | Status |
|---|---|---|---|---|
| Line coverage (2 production files) | 91.73% (255/278) | 92.41% (280/303) | +0.68 pp | PASS (no regression; >= 85%) |
| Command/instruction coverage (branch proxy) | 91.08% (347/381) | 91.73% (377/411) | +0.65 pp | PASS (no regression; >= 75%) |
| Missed lines (absolute) | 23 | 23 | 0 | No new uncovered lines |
| Full suite pass/fail | 406 / 0 | 416 / 0 | +10 pass | PASS |
| Production file exclusion | none | none (`ExcludedPath` empty; both production files measured) | — | PASS |

## No-Regression-on-Changed-Lines Check

The change added 25 covered production lines (covered 255 -> 280) while the absolute missed-line count stayed at 23. All newly added production code — `Get-OpenClawOperatorEnvFilePath`, `Resolve-OpenClawDefaultEnvFilePath`, `Test-OpenClawGatewayTokenInContainer`, the `-EnvFilePath` resolution block, and the aggregation wiring — is exercised by the new Phase 1 tests. No changed or added line reduced coverage; both thresholds are met with margin.

## Overall Disposition

PASS. Line coverage (92.41%) >= 85%; command/instruction branch proxy (91.73%) >= 75%; no repo-file regression; no new uncovered lines on changed code; no production file excluded from measurement.
