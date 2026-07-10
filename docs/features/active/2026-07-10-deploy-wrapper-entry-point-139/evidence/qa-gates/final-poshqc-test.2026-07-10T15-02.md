Timestamp: 2026-07-10T15-02

Command (1, failing MCP invocation): mcp__drm-copilot__run_poshqc_test (workspace_root = repo root, full repository)
EXIT_CODE: non-zero (`{"ok":false,"summary":"Command exited with code 4294967295."}`, same known coverage-path defect as baseline, F11 #111 / F16 #125 / #135 / #137)

Command (2, corrected-runsettings workaround, full repo, post-change): `pwsh -NoProfile -File <scratchpad>\run-poshqc-test-139.ps1`, using the same corrected runsettings as the P0-T8 baseline with one addition: `scripts/Deploy.ps1` added to `CodeCoverage.Path` (the file did not exist at baseline time; it is now the 28th enumerated production file, keeping `ExcludedPath` empty per the Coverage Exclusion Policy).
EXIT_CODE: 0

Output Summary: `Tests Passed: 380, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0` — full pass, including every new test from P1-T1 (Publish.ps1 output-contract regression) and P2-T12 through P2-T20 (all 9 Deploy.ps1 tests). 380 = 370 (P0-T8 baseline) + 1 (P1-T1 regression test) + 9 (Deploy.Tests.ps1). Coverage: `Covered 89.94% / 0%. 2,057 analyzed Commands in 31 Files.` Numeric repo-wide post-change command/line-coverage percentage: **89.94%** (line/command-coverage proxy, same convention as the baseline). All 28 production PowerShell files under `scripts/**` (27 baseline files + the new `scripts/Deploy.ps1`) are measured; none excluded.
