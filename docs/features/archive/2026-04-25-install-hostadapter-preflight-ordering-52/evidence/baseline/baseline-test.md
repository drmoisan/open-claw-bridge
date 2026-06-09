# Phase 0 — Baseline Test Evidence

Timestamp: 2026-04-25T00:08:00Z

Command: mcp_drmcopilotext_run_poshqc_test

EXIT_CODE: 0

Output Summary: 185 tests passed, 0 failed, 0 errors.

Coverage Notes:
- The PoshQC standard coverage output (powershell-coverage.xml, powershell-coverage.koverage.xml) captures only `.claude/hooks` scripts in the standard run (0/284 lines covered), as those hooks are not exercised by the test suite.
- The most recent refinement coverage report (artifacts/pester/coverage-final.refinement.xml, dated 2026-04-19) covers the production scripts scope: 2150 of 2240 coverable lines covered = **95.98% line coverage**.
- Baseline coverage headline: **95.98%** (scripts scope, from coverage-final.refinement.xml dated 2026-04-19).
- The Install.ps1 file is within the scripts coverage scope tracked by refinement reports.
