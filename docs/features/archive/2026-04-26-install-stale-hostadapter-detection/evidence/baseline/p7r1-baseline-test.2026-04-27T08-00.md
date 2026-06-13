# Pre-Remediation Test Baseline (P7r1)

Timestamp: 2026-04-27T08-00
Command: mcp__drmCopilotExtension__run_poshqc_test (workspace_root=c:\Users\DanMoisan\repos\open-claw-bridge, scan_folders=["tests/scripts/Install.Helpers.Tests.ps1","tests/scripts/Install.Preflight.Tests.ps1"]); coverage scope reference: artifacts/pester/install-layer-coverage.xml (2026-04-26 direct Pester run, authoritative pre-remediation per-module figures)
EXIT_CODE: 0

## Output Summary

Pre-remediation Pester run (this baseline run):

- `tests/scripts/Install.Helpers.Tests.ps1`: 46 tests, 0 failures.
- `tests/scripts/Install.Preflight.Tests.ps1`: 12 tests, 0 failures.
- Combined: 58 passed, 0 failed.
- Source: `artifacts/pester/pester-junit.xml` (regenerated 2026-04-27 by the MCP test run above).

Pre-remediation per-module coverage headlines (sourced from `artifacts/pester/install-layer-coverage.xml` produced 2026-04-26 — the most recent direct Pester coverage run scoped to the Install layer; the MCP `run_poshqc_test` coverage scope is fixed at `.claude/hooks` and does not emit module-scope coverage for `scripts/`):

- `scripts/Install.Helpers.psm1`: 132 / 148 = **89.2%** (matches the AC-14b shortfall figure in `evidence/qa-gates/p7-coverage-delta.md`).
- `scripts/Install.Preflight.psm1`: 97 / 108 = **89.8%** (matches the AC-14b shortfall figure in `evidence/qa-gates/p7-coverage-delta.md`).

These two values are reproduced verbatim from the `<class name="scripts/Install.Helpers" sourcefilename="Install.Helpers.psm1">` and `<class name="scripts/Install.Preflight" sourcefilename="Install.Preflight.psm1">` LINE counters in the linked XML and equal the coverage-delta artifact within rounding.

Verdict: baseline captured. Both modules are 1 percentage point or less below the 90% AC-14b threshold; remediation tasks P2-T1..P2-T5 and P3-T1..P3-T3 target the specific missed branches enumerated in `p7r1-missed-lines.2026-04-27T08-00.md`.
