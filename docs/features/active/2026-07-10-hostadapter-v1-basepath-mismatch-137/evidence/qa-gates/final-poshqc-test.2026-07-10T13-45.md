Timestamp: 2026-07-10T13-45

Command (1, failing MCP invocation, reproduced for completeness): mcp__drm-copilot__run_poshqc_test (workspace_root=C:\Users\DanMoisan\repos\open-claw-bridge)
EXIT_CODE: non-zero (known coverage-path defect, same as baseline — bundled settings reference `drm-copilot`-repo-only paths, not re-run redundantly here since already reproduced at baseline P0-T9)

Command (2, corrected-runsettings workaround invocation): `pwsh -NoProfile -File <scratchpad>\run-poshqc-test-135.ps1 -SettingsPath <scratchpad>\pester.runsettings.corrected.psd1` (same corrected runsettings as baseline: `CodeCoverage.Path` enumerates this repository's actual production PowerShell files under `scripts/**`, empty `ExcludedPath`).
EXIT_CODE: 0

Output Summary: `Tests Passed: 370, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0` (up from the baseline's 369 — includes the new P1-T1 regression test `Get-HostAdapterPreflightUri default base URL`, which now passes). Coverage result: `Covered 89.93% / 0%. 2,015 analyzed Commands in 30 Files.` Numeric repo-wide post-change command/line-coverage percentage: **89.93%** — unchanged from the 89.93% baseline (no regression). All 30 production PowerShell files under `scripts/**` measured (none excluded).
