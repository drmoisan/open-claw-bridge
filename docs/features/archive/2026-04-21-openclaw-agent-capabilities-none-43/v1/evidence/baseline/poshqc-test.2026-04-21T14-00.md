---
Timestamp: 2026-04-21T15:04:00Z
Purpose: Baseline PoshQC Pester + coverage snapshot captured against pre-edit HEAD (git SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028) using Pester 5.6.1 with JaCoCo coverage enabled. Produced by the Phase 6 QA executor after reconstructing the pre-edit tree via `git stash push -u` and replaying the test run before `git stash pop`.
---

# Baseline — PoshQC Test (Pester + Coverage)

Timestamp: 2026-04-21T15:04:00Z

Command: `pwsh -NoProfile -File /tmp/pester-baseline.ps1` — runs `Invoke-Pester` with `New-PesterConfiguration` set to discover all `*.Tests.ps1` under `tests/scripts`, with `CodeCoverage.Enabled = $true` and `CodeCoverage.Path` populated from every `*.ps1`/`*.psm1` under `scripts/` (recursive). Executed against the pre-edit HEAD working tree (stash applied, working tree at SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028 exactly).

EXIT_CODE: 0

Tool Surface: MCP `mcp__drmCopilotExtension__run_poshqc_test` is not available in this executor sandbox. Fallback per the plan directive: direct `Invoke-Pester` invocation from Pester 5.6.1. The runsettings config `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` referenced by the plan does not exist in the repo tree (`Glob **/pester.runsettings*` returns no matches and `scripts/powershell/PoshQC/` does not exist), so the test runner was configured inline to match the intent: discover tests under `tests/scripts`, enable coverage against production scripts, emit JaCoCo output.

Output Summary:
- Total:   186
- Passed:  186
- Failed:  0
- Skipped: 0
- Inconclusive: 0

Coverage (Pester native command-level coverage, treated as the `line coverage` proxy for AC-7 since Pester 5 reports coverage per PowerShell command/statement, which is the repository convention):
- RepoCoverage_CommandsAnalyzed: 1485
- RepoCoverage_CommandsExecuted: 1322
- RepoCoverage_CommandsMissed:   163
- RepoCoverage_Percent:          89.02

Module-scoped coverage for `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`:
- ModuleCoverage_CommandsAnalyzed: 193
- ModuleCoverage_CommandsExecuted: 181
- ModuleCoverage_CommandsMissed:   12
- ModuleCoverage_Percent:          93.78

This baseline is the AC-7 reference anchor for the coverage delta computed at `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md` (P6-T6). JaCoCo XML was written to `TestResults/coverage-baseline.xml`.
