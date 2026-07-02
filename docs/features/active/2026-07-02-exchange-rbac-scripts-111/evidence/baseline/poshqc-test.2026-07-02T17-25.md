# Baseline — PoshQC Test with Coverage

Timestamp: 2026-07-02T17-25
Command: mcp__drm-copilot__run_poshqc_test (workspace_root: repo root; attempted twice, plus once with scan_folders=["tests/scripts"]); then the same bundled pipeline executed directly: `pwsh -NoProfile -File <scratchpad>/run-poshqc-test-openclaw.ps1` importing the bundled `PoshQC.psd1` and calling `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.openclaw.runsettings.psd1` (settings identical to the bundled `pester.runsettings.psd1` except `CodeCoverage.Path` corrected to this repository's 21 PowerShell production files under `scripts/`).
EXIT_CODE: 0

Output Summary:
- MCP tool result: `ok=false`, "Command exited with code 4294967295" on all three attempts. Diagnosed root cause: the bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` entries for the drm-copilot repository (e.g. `scripts/powershell/Publish-DrmCopilotExtension.ps1`, `scripts/dev-tools/Invoke-FullRelease.ps1`), six of which do not exist in this repository; Pester 5.6.1's coverage plugin fails at RunStart with `Resolve-CoverageInfo: Could not resolve coverage path`, so the wrapper exits -1 even though all tests pass. This is a tooling-environment defect, not a repo test failure.
- Direct bundled-pipeline run (same `Invoke-PoshQCTest` code path, corrected coverage scope): EXIT_CODE 0.
- Tests: Passed 281, Failed 0, Skipped 0, Inconclusive 0, NotRun 0 (duration 19.98 s).
- Repo-wide baseline line/command coverage: **88.47%** (1,752 analyzed Commands in 21 Files; all `scripts/**` production `.ps1`/`.psm1`; `.claude/hooks/**` excluded per the documented Issue #66 T4-scaffolding coverage-scope exclusion).
- Repo-wide baseline branch coverage: Pester v5 emits command-level coverage only and produces no branch-percentage metric for PowerShell (Pester reported "88.47% / 0%" where the second value is the unused branch slot). Command coverage counts commands inside every branch arm, so untaken branch arms register as uncovered commands; the 88.47% command figure is the branch-sensitive baseline signal. This matches repository precedent (feature #62 baseline `pester.2026-06-05T22-09.md`, feature #58 `p7-test.md`).
- Raw tool output: `artifacts/pester/pester-junit.xml`, `artifacts/pester/powershell-coverage.xml`, `artifacts/pester/powershell-coverage.koverage.xml` (tool output location permitted by plan conventions; this artifact is authoritative).
