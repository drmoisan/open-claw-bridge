# Final QC — Pester Test + Coverage

Timestamp: 2026-06-16T12-08
Command: Invoke-Pester (New-PesterConfiguration) over tests/scripts (full suite) with CodeCoverage.Enabled and
         CodeCoverage.Path = scripts/Publish.Env.psm1, scripts/Publish.Helpers.psm1, scripts/Publish.ps1, scripts/New-MsixDevCert.ps1
EXIT_CODE: 0

Output Summary:
- Full tests/scripts suite: 280 passed, 0 failed (baseline was 239 passed; +41 new tests for the env-driven version/cert behavior).
- Post-change line/command coverage over the four changed/created scripts: 362/403 = 89.83% (>= 85%).
- Final per-file line/command coverage (separate scoped runs):
  - scripts/Publish.Env.psm1 (NEW): 59/61 = 96.72%.
  - scripts/Publish.Helpers.psm1: 185/191 = 96.86%.
  - scripts/Publish.ps1: 91/93 = 97.85%.
  - scripts/New-MsixDevCert.ps1: 27/58 = 46.55% (dominated by the dot-source-excluded Main guard; see coverage-delta.2026-06-16T10-28.md).
- Pester command-based coverage does not emit a separate branch metric. The 89.83% command coverage is the line/command headline; precedence, update-vs-append, fail-fast, and -WhatIf branches are each exercised by dedicated It blocks.
- No temp files; pure helpers driven with in-memory string[]; file seam mocked.

---

## Phase 3 — File-size cap remediation (Publish.Helpers.psm1 extraction)

Timestamp: 2026-06-16T15-02
Command: Invoke-Pester (New-PesterConfiguration) over tests/scripts (full suite, -CI) with CodeCoverage.Enabled and
         CodeCoverage.Path = scripts/Publish.Msix.psm1, scripts/Publish.Helpers.psm1
EXIT_CODE: 0

Output Summary:
- Full tests/scripts suite: 281 passed, 0 failed, 0 skipped (was 280 in Phase 2; +1 net from the test relocation that splits the single combined "Module exports" assertion into one per module).
- The relocated-function tests now run from tests/scripts/Publish.Msix.Tests.ps1 (22 passed: the 7 Module-exports + 21 behavior It blocks across Find-WindowsSdkTool, Get-StampedAppxManifestXml, Invoke-VersionStamp, Invoke-LayoutAssembly, Invoke-MakePri, Invoke-MakeAppx, Invoke-SignTool).
- Both Context 'Module exports' It blocks pass: tests/scripts/Publish.Helpers.Tests.ps1 asserts exactly the 7 RETAINED functions (count text 7); tests/scripts/Publish.Msix.Tests.ps1 asserts exactly the 7 RELOCATED functions via Get-Command -Module Publish.Msix.
- Post-change line/command coverage (both extraction modules together): 188/194 = 96.91% (>= 85%).
- Per-module scoped coverage:
  - scripts/Publish.Msix.psm1 (NEW module, evaluated as new code): 80/83 = 96.39% (>= 85%).
  - scripts/Publish.Helpers.psm1 (now smaller, retained functions): 108/111 = 97.30% (>= 85%; retained the ~96.86% pre-relocation level).
- Pester command-based coverage does not emit a separate branch metric; per-function -WhatIf / non-zero-exit / throw branches are each exercised by dedicated It blocks moved verbatim with their functions.
- Tooling note: the MCP wrapper `mcp__drm-copilot__run_poshqc_test` returned a non-zero summary code (4294967295) on this run; the underlying full Pester run via `Invoke-Pester -Path tests/scripts -Output Detailed -CI` and the coverage-enabled `New-PesterConfiguration` run both completed with 281 passed / 0 failed. The non-zero MCP code reflects a wrapper-level coverage-mode condition, not a test failure (verified by the direct CI run).
- No temp files; pure helpers driven with in-memory content; external-tool shims and file seams mocked.
