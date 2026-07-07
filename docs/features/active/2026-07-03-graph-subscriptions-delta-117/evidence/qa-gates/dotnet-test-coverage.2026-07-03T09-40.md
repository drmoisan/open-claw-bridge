# Final QA Gate — Tests and Coverage (Remediation Cycle 1, Issue #117)

Timestamp: 2026-07-03T09-40
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "artifacts/csharp/remediation-final-117"
EXIT_CODE: 0
Output Summary:
- Tests: 1163 passed, 0 failed, 5 skipped (pre-existing environment-gated COM/publish skips), 1168 total — final clean pass (loop step 3; includes the NetArchTest architecture suites inside Core.Tests).
  - OpenClaw.Core.Tests: 716/716 passed (707 baseline + 9 new remediation tests)
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped
  - OpenClaw.HostAdapter.Tests: 100/100 passed
- Pooled line coverage: 5631/6065 = 92.84% (gate >= 85%: PASS)
- Pooled branch coverage: 1318/1576 = 83.63% (gate >= 75%: PASS)
- Parsing convention: dedupe duplicate class entries per file+line within each cobertura report, sum deduped per-report totals across the three reports (same method as the P0-T4 baseline and the reviewer).
- Raw cobertura reports retained under `artifacts/csharp/remediation-final-117/` (3 reports).

## Loop history (restarts per the toolchain loop rule)

- Pass 1 (2026-07-03T09-28): format 0 / check 0 / build 0; test run had 1 intermittent failure in the new `SubscriptionRenewalWorkerTests.Loop_continues_with_warning_when_the_sweep_throws_TaskCanceledException_without_stop_requested`. Root cause: the fake-time polling loop advanced 1 minute per yield while the worker's sweep continuation lagged under coverage instrumentation, so the second sweep could observe the record as expired (POST recreate) instead of due (PATCH renew) — a scheduling-sensitive assertion. A secondary hazard (concurrent polling of the unsynchronized CapturingLogger list) was removed in the same fix.
- Fix: the test now proves loop survival via an atomic list-call counter on the throw-once store with a never-due record (no Graph verb sensitivity); logger asserted only after StopAsync. Verified stable across 8 consecutive coverage-instrumented runs of all three loop-continues tests.
- Pass 2 (final, this artifact): format 0 / check 0 / build 0 warnings 0 errors / test 1163 passed 0 failed — all stages clean in a single pass.
