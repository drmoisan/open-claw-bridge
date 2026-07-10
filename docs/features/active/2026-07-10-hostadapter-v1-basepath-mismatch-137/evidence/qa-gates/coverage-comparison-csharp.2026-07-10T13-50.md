Timestamp: 2026-07-10T13-50

Command: (comparison of P0-T12 baseline vs P5-T6 post-change numeric results; no new command executed, both source runs recorded above)

EXIT_CODE: 0

Output Summary:
- Baseline (P0-T12), `OpenClaw.Core` package: line-rate = 99.29%, branch-rate = 92.28%. Class-level `OpenClaw.Core\Program.cs`: line-rate = 100%, branch-rate = 100%.
- Post-change (P5-T6), `OpenClaw.Core` package: line-rate = 99.29%, branch-rate = 92.28% (unchanged). Class-level `OpenClaw.Core\Program.cs`: line-rate = 100%, branch-rate = 100% (unchanged).
- Changed file: `src/OpenClaw.Core/Program.cs` — single-line literal change inside the existing `PostConfigure` fallback branch, which was already covered at 100%/100% before and after the fix (the branch is exercised both by the pre-existing default-path tests and the new P1-T3 `CoreHostAdapterBaseUrlFallbackTests` test).
- New file: `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` — a test file, excluded from the production coverage denominator per the Coverage Exclusion Policy (`.claude/rules/general-unit-test.md`); not counted toward `OpenClaw.Core`'s production line/branch rate.
- Test counts: `OpenClaw.Core.Tests` baseline 930 passed / post-change 931 passed (+1, the new regression test), 0 failed in both runs.

PASS/FAIL against thresholds:
- No `OpenClaw.Core` regression versus baseline: **PASS** (line-rate 99.29% == 99.29%, branch-rate 92.28% == 92.28%, no decrease).
- Line coverage >= 85%: **PASS** (99.29% >= 85%).
- Branch coverage >= 75%: **PASS** (92.28% >= 75%).

Overall: **PASS** — no regression, both thresholds met.
