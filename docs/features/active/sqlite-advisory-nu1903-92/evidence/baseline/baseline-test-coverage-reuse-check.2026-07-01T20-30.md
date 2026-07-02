# Baseline Test & Coverage Reuse Check — Issue #92

Timestamp: 2026-07-01T20-30

Command: (validation of existing artifact) — read docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-test-coverage.md

EXIT_CODE: 0

Output Summary:
- Reused baseline artifact `baseline-test-coverage.md` (2026-07-01T19-46) is present and schema-complete.
- It records `Command: dotnet test OpenClaw.MailBridge.sln -c Release --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, `EXIT_CODE: 0`.
- Test counts: 587 passed, 5 skipped, 0 failed across 3 assemblies (HostAdapter.Tests 100/100, Core.Tests 210/210, MailBridge.Tests 277 passed/5 skipped/282 total).
- Coverage (pooled, cobertura, excludes [*.Tests]*): line 90.73% (3486/3842), branch 79.31% (874/1102). Both meet policy (line >= 85%, branch >= 75%).
- Working tree matches reverted baseline; these are the authoritative pre-change baseline figures for the P2-T7/P2-T8 delta comparison.
- Reuse is VALID; no recapture required. Supports AC-4.
