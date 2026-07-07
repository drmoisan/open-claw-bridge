Timestamp: 2026-07-07T02-45

Command: dotnet test --collect:"XPlat Code Coverage" (run from repository root)

EXIT_CODE: 1

Output Summary:

Test results (all assemblies):
- OpenClaw.Core.Tests: Failed 2, Passed 846, Skipped 0, Total 848 (the 2 failures are the two architecture-boundary tests documented in `evidence/other/architecture-boundary-conflict.md`; no behavioral test regression).
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352.

Coverage — `OpenClaw.Core` package, read from the Cobertura report
`tests/OpenClaw.Core.Tests/TestResults/1fa11a44-5412-4455-8c5d-06b123cf1f0d/coverage.cobertura.xml`:
- Line coverage: 92.87% (line-rate 0.9287).
- Branch coverage: 81.43% (branch-rate 0.8143).

Both values remain well above the T1 thresholds (line >= 85%, branch >= 75%),
essentially unchanged from the Phase 0 baseline (92.88% line / 81.48% branch;
delta -0.01pp line, -0.05pp branch — see `final-qa-05-coverage-delta.md`).

Per-new/changed production file (aggregated from the same Cobertura report):
- `CloudSyncActivityType.cs`, `CloudSyncActivityResultCode.cs`, `CloudSyncActingFlags.cs`: no executable lines (const-only classes; 0/0 lines and branches — a type with no executable behavior, comparable to the interface-only-module clarification in `.claude/rules/general-unit-test.md`).
- `PurviewActivityLogRecord.cs`: 9/13 lines (69.2%); a positional record's compiler-generated members (equality/ToString) are not exercised beyond what the test suite happens to call; 0/0 branches.
- `PurviewActivityLogProjection.cs`: 71/77 lines (92.2%), 68/80 branches (85.0%).
- `GraphSubscriptionManager.cs`: 302/338 lines (89.3%), 27/34 branches (79.4%).
- `NotificationRequestProcessor.cs`: 172/172 lines (100.0%), 24/32 branches (75.0%).
- `GraphDeltaReconciler.cs`: 203/204 lines (99.5%), 38/42 branches (90.5%).

The `EXIT_CODE: 1` reflects the 2 architecture-boundary test failures (documented separately), not a coverage shortfall; coverage collection itself completed successfully and all numeric values above are non-placeholder, measured values.
