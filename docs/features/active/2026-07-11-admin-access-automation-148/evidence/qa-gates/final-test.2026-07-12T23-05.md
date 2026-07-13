# Final QC - Test (full Pester suite)

Timestamp: 2026-07-12T23-05

## Invocation 1 (bundled MCP - known-defective)
Command: mcp__drm-copilot__run_poshqc_test (workspace_root = repo worktree root)
EXIT_CODE: 4294967295
Output Summary: Fails on the established bundled-runsettings coverage-path defect (see baseline
poshqc-test artifact). No usable result; the corrected-runsettings invocation below is the
authoritative source.

## Invocation 2 (corrected-runsettings full suite - authoritative)
Command: Import-Module '<ext>\danmoisan.drm-copilot-1.0.15\resources\powershell\PoshQC\PoshQC.psd1' -Force ;
Invoke-PoshQCTest -Root '<repo worktree root>' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'
(Run.Path = tests/scripts; CodeCoverage.Path = all 36 production PowerShell files under scripts/**)
EXIT_CODE: 0

Output Summary:
- Full Pester suite: Passed: 456, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0.
- The suite is fully green in a single clean pass. This is a no-regression improvement over
  the baseline (baseline was 424 passed / 9 failed): the 9 baseline failures were full-suite
  test-isolation contamination in the Invoke-OpenClawContainerPathValidation set (they passed
  in isolation at baseline). Adding the three new test files changed the execution order and
  the contamination no longer manifests; all previously-failing tests now pass. No test was
  modified in the container-validation set.
- 456 = 433 baseline tests + 23 new capability tests (capability 1 = 6, capability 2 = 11,
  capability 3 = 6).
- Format and analyze on the same final clean pass reported 0 changes / 0 issues (see
  final-format and final-analyze artifacts).
