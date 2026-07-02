# Final QA — PoshQC Test with Coverage (full PowerShell scope)

Timestamp: 2026-07-02T18-55
Command: mcp__drm-copilot__run_poshqc_test (workspace_root: repo root, default scope) — failed with exit 4294967295 due to the known bundled-settings defect (foreign `CodeCoverage.Path` entries; diagnosis in baseline `poshqc-test.2026-07-02T17-25.md`); then the same bundled pipeline executed directly: `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.final.runsettings.psd1` (identical to the baseline settings plus the eight new production files; Run.Path = scripts, tests/powershell, tests/scripts — full repo test scope).
EXIT_CODE: 0
Output Summary:
- Tests Passed: **358**, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0 (duration 21.41 s). Baseline was 281 passed; the +77 delta is exactly the new feature suite.
- Repo-wide post-change line/command coverage: **89.66%** (1,963 analyzed Commands in 29 Files); per-file LINE counters from the coverage XML aggregate to 1393/1544 = **90.22%** lines.
- New `OpenClawRbac` module + entry script per-file line coverage (from `artifacts/pester/powershell-coverage.xml`):
  - OpenClawRbac.psm1: 5/5 = 100%
  - OpenClawRbac.Seams.ps1: 59/60 = 98.33%
  - Register-OpenClawServicePrincipal.ps1: 9/9 = 100%
  - New-OpenClawMailboxScope.ps1: 11/11 = 100%
  - Grant-OpenClawRbacRoles.ps1: 31/31 = 100%
  - Set-OpenClawSendOnBehalf.ps1: 12/12 = 100%
  - Test-OpenClawScopeBoundary.ps1: 25/25 = 100%
  - Invoke-OpenClawExchangeRbacSetup.ps1: 16/16 = 100%
  - New-code aggregate: 168/169 = **99.41%** line; command coverage over the same files: **99.53%** (211 commands, Batch 3 gate).
- Branch metric: Pester v5 emits command-level coverage only (no branch percentage for PowerShell); command coverage counts commands inside every branch arm, so untaken arms surface as uncovered commands. Recorded per repository precedent (features #58 and #62).
- Type checking: not applicable for PowerShell per `.claude/rules/powershell.md`.
- Raw tool output: `artifacts/pester/pester-junit.xml`, `artifacts/pester/powershell-coverage.xml`, `artifacts/pester/powershell-coverage.koverage.xml`.
