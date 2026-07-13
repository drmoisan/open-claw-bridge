# Final QC - Coverage (post-change, numeric)

Timestamp: 2026-07-12T23-05

## Invocation 1 (bundled MCP coverage mode - known-defective)
Command: mcp__drm-copilot__run_poshqc_test (workspace_root = repo worktree root)
EXIT_CODE: 4294967295
Output Summary: Fails on the established bundled-runsettings coverage-path defect. No usable
numeric coverage. The corrected-runsettings invocation below is the numeric source.

## Invocation 2 (corrected-runsettings coverage - numeric source)
Command: Import-Module '<ext>\danmoisan.drm-copilot-1.0.15\resources\powershell\PoshQC\PoshQC.psd1' -Force ;
Invoke-PoshQCTest -Root '<repo worktree root>' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'
(CodeCoverage.Path rewritten to all 36 production PowerShell files under scripts/**, no ExcludedPath)
EXIT_CODE: 0

Output Summary (post-change, repo-wide over 36 production files, 2,404 analyzed commands):
- LINE coverage: 91.09% (1718 covered / 1886 total)
- INSTRUCTION coverage (branch-coverage proxy per repo precedent; CoverageGutters/JaCoCo XML has
  no BRANCH counter): 90.60% (2178 covered / 2404 total)
- METHOD: 91.02% (152/167); CLASS: 94.44% (34/36)
- Full Pester suite: 456 passed, 0 failed.
- Both line (91.09% >= 85%) and branch-proxy (90.60% >= 75%) clear the thresholds.

Per-new-script coverage (the three added scripts):
- scripts/Get-OpenClawControlUiTokenUrl.ps1: LINE 92.31% (12/13), INSTRUCTION 93.75% (15/16)
- scripts/Invoke-OpenClawDeviceTokenRotation.ps1: LINE 96.97% (32/33), INSTRUCTION 93.02% (40/43)
- scripts/Set-OpenClawWebSearchProvider.ps1: LINE 87.50% (35/40), INSTRUCTION 85.71% (48/56)
