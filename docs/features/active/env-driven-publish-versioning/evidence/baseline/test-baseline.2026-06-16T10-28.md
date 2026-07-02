# Baseline — Pester Test + Coverage

Timestamp: 2026-06-16T11-25
Command: Invoke-Pester -Path tests/scripts -Output Detailed -CI  (full suite)
         + coverage run: New-PesterConfiguration with CodeCoverage.Path = scripts/Publish.ps1, scripts/Publish.Helpers.psm1, scripts/New-MsixDevCert.ps1
EXIT_CODE: 0

Output Summary:
- Full suite: Tests Passed: 239, Failed: 0, Skipped: 0 (completed in ~30.85s).
- Coverage scoped to the three pre-existing in-scope scripts (Publish.ps1, Publish.Helpers.psm1, New-MsixDevCert.ps1):
  - Line (command) coverage: 274 / 308 commands executed = 88.96%.
  - Branch coverage is not reported separately by Pester's command-based coverage model; line/command coverage of 88.96% is the headline. The repo's coverage policy (line >= 85% / branch >= 75%) is evaluated post-change in P2-T4 with the same command model as the no-regression reference.
- Note: the MCP `run_poshqc_test` tool reported a nonzero internal exit code (4294967295) in coverage mode in this environment; the authoritative CI command `Invoke-Pester -Path tests/scripts -Output Detailed -CI` (per .github/workflows/ci.yml and powershell.md) was used directly to obtain the baseline pass count and coverage. This same direct command is used for the post-change comparison so baseline and post-change are measured identically.
- Baseline line/command coverage headline for the changed-script surface: 88.96% (274/308).
