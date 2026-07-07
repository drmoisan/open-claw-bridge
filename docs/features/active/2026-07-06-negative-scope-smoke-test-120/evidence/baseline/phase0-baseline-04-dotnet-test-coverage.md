# Phase 0 Baseline 04 — dotnet test with Coverage (Issue #120)

Timestamp: 2026-07-06T23-11
Command: `dotnet test --collect:"XPlat Code Coverage"` (run from repository root)
EXIT_CODE: 0

Output Summary:

Test results (all assemblies):
- OpenClaw.Core.Tests: Failed 0, Passed 716, Skipped 0, Total 716.
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352.

Coverage — `OpenClaw.Core` package (authoritative baseline for this feature's tier-T1 module),
read from the Cobertura report
`tests/OpenClaw.Core.Tests/TestResults/7d01a725-2f1e-4e12-b0e0-b2355e051dfb/coverage.cobertura.xml`:
- Line coverage: 92.49% (line-rate 0.9249).
- Branch coverage: 80.90% (branch-rate 0.809).

For reference, the whole-report aggregate (which also includes OpenClaw.HostAdapter.Contracts
at 100% and OpenClaw.MailBridge.Contracts at 28.37% line) is line-rate 0.9023 (90.23%),
branch-rate 0.7787 (77.87%), lines-covered 5406/5991, branches-covered 1309/1681. The
`OpenClaw.Core`-specific package numbers above are the baseline used for the Phase 6
coverage-delta comparison, since this feature only adds code to `OpenClaw.Core`.

Both `OpenClaw.Core` baseline values already exceed the T1 thresholds (line >= 85%,
branch >= 75%).
