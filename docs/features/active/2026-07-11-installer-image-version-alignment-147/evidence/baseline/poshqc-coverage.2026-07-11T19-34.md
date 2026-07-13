# Baseline — PoshQC Test-and-Coverage (Issue #147, P0-T9)

Timestamp: 2026-07-12T09-20

## Attempt 1 — MCP tool, coverage mode, scoped to the two production files

Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root=`C:\Users\DanMoisan\repos\open-claw-bridge\.claude\worktrees\agent-a6be2688bad114dd0`, scan_folders=`["scripts/Install.ps1", "scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1"]`)

EXIT_CODE: 0 (tool returned `"ok":true`)

Output Summary: The MCP tool returned a bare success summary ("Ran bundled PoshQC test ... with 2 selected scan folder(s)."), reproducing the established defect pattern (`#111`, `#125`, `#135`, `#137`, `#139`, `#142`, `#144`): it does not surface per-file numeric line/branch coverage percentages, nor pass/fail counts, in its response. Per the plan's established convention, the numeric coverage values below are captured via the corrected-runsettings workaround (Attempt 2).

## Attempt 2 — Corrected-runsettings `Invoke-PoshQCTest` workaround (numeric coverage source)

Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\.vscode-insiders\extensions\danmoisan.drm-copilot-1.0.15\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root 'C:\Users\DanMoisan\repos\open-claw-bridge\.claude\worktrees\agent-a6be2688bad114dd0' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'"` where `<scratchpad>\pester.runsettings.corrected.psd1` is a SCRATCHPAD-only copy of the bundled `pester.runsettings.psd1` with `CodeCoverage.Path` rewritten to exactly the two in-scope production files (`scripts/Install.ps1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`) and no `ExcludedPath` entry (no production file excluded from measurement, per the Coverage Exclusion Policy).

EXIT_CODE: 0

Output Summary: Full `tests/scripts` + `tests/powershell` suite run (default `Run.Path` from the corrected settings: `scripts`, `tests/powershell`, `tests/scripts`). **Tests Passed: 407, Failed: 9, Skipped: 0, Inconclusive: 0, NotRun: 0** (416 total). All 9 pre-existing failures are in the six `Invoke-OpenClawContainerPathValidation.*.Tests.ps1` files named in AC14's regression scope (`Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` x1, `Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` x2, `Invoke-OpenClawContainerPathValidation.Tests.ps1` x5, `Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` x1), and are pre-existing on this branch prior to any change made under this plan — they are environment-dependent assertions on real OS-level connection-refused error message text and default-path resolution (e.g. `Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1:52` expects `'./.env'` but observes the operator env path on this host; `Invoke-OpenClawContainerPathValidation.Tests.ps1:304` expects a literal `'Connection refused'` string but the host OS returns `'No connection could be made because the target machine actively refused it. (127.0.0.1:8081)'`). No file touched by this plan's Phase 1/Phase 2 tasks caused these failures; they are captured here as the pre-change baseline so Phase 3 (P3-T4/AC14) can confirm "no NEW failures" against this exact set of 9. Full JUnit detail: `artifacts/pester/pester-junit.xml` (transient build artifact, not persisted evidence).

Coverage result parsed from `artifacts/pester/powershell-coverage.xml` (JaCoCo/CoverageGutters format; Pester's built-in PowerShell coverage tool does not produce a distinct `BRANCH` counter — only `INSTRUCTION`, `LINE`, `METHOD`, `CLASS`, consistent with prior repo precedent, e.g. `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/evidence/baseline/poshqc-coverage.2026-07-10T20-30.md`):

| File | LINE missed/covered | Line coverage % | INSTRUCTION missed/covered (branch-coverage proxy) | Instruction coverage % |
|---|---|---|---|---|
| `scripts/Install.ps1` | 20 / 155 (of 175) | 88.57% | 30 / 179 (of 209) | 85.65% |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | 12 / 132 (of 144) | 91.67% | 19 / 169 (of 188) | 89.89% |
| Aggregate (both files) | 32 / 287 (of 319) | 89.97% | 49 / 348 (of 397) | 87.66% |

## Baseline Coverage Summary (numeric, no placeholders)

| Metric | Value |
|---|---|
| Line coverage — `scripts/Install.ps1` | 88.57% (155/175) |
| Line coverage — `OpenClawContainerValidation.psm1` | 91.67% (132/144) |
| Line coverage — aggregate | 89.97% (287/319) |
| Instruction coverage (branch-coverage proxy) — `scripts/Install.ps1` | 85.65% (179/209) |
| Instruction coverage (branch-coverage proxy) — `OpenClawContainerValidation.psm1` | 89.89% (169/188) |
| Instruction coverage (branch-coverage proxy) — aggregate | 87.66% (348/397) |
| Full suite pass/fail | 407 passed / 9 failed (pre-existing, unrelated to this plan's scope) |
