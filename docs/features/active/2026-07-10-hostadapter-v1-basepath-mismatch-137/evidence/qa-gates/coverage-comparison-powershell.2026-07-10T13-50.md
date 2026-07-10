Timestamp: 2026-07-10T13-50

Command: (comparison of P0-T9 baseline vs P5-T3 post-change numeric results; no new command executed, both source runs recorded above)

EXIT_CODE: 0

Output Summary:
- Baseline (P0-T9): repo-wide command/line-coverage = 89.93% (2,015 analyzed Commands in 30 Files, `369` tests passed).
- Post-change (P5-T3): repo-wide command/line-coverage = 89.93% (2,015 analyzed Commands in 30 Files, `370` tests passed — +1 for the new P1-T1 regression test).
- Changed-file coverage: `scripts/Install.Preflight.psm1` is included in both baseline and post-change measured file sets (30 production PowerShell files, no `ExcludedPath` entries). The file's single-line literal change (`$baseUrl` default value) is within a function (`Get-HostAdapterPreflightUri`) already exercised by both the pre-existing test suite and the new P1-T1 regression test.

PASS/FAIL against thresholds:
- No repo-wide regression versus baseline: **PASS** (89.93% == 89.93%, no decrease).
- Line coverage >= 85%: **PASS** (89.93% >= 85%).
- Command-coverage branch proxy >= 75%: **PASS** (89.93% >= 75%; Pester's engine reports command/line coverage as the only numeric metric available in this repository's tooling — per the established workaround note in the plan's Conventions section, this value is used as the branch-coverage proxy since Pester's `CodeCoverage` engine does not compute a separate branch metric).
- No production PowerShell file excluded from measurement: **PASS** (`ExcludedPath = @()` in the corrected runsettings; all 30 files under `scripts/**` enumerated in `CodeCoverage.Path`).

Overall: **PASS** — no regression, both thresholds met, no exclusions.
