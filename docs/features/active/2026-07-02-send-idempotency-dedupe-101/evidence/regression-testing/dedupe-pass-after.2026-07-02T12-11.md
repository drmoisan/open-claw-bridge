# Pass-After Evidence — SchedulingWorkerDedupeTests after the pipeline change (P4-T2)

Timestamp: 2026-07-02T12-11
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerDedupeTests"` (repo root)
EXIT_CODE: 0

Output Summary:
Run after the P4-T1 pipeline change (consult-before-send / record-after-success inside the `SendEnabled` else branch of `ProposeAndActAsync`). All five dedupe tests pass — Failed: 0, Passed: 5, Skipped: 0, Total: 5:

| Test | Observed |
|---|---|
| (a) `RunCycle_StoreHit_SkipsSendAndCompletesWithoutThrowing` (skip-on-hit) | PASS |
| (b) `RunCycle_StoreMiss_SendsThenRecordsKeyWithInjectedClockTimestamp` (record-on-success, send-before-record ordering, `FakeTimeProvider` timestamp) | PASS |
| (c) `RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate` (no-record-on-failure, per-message isolation) | PASS |
| (d) `RunCycle_SendDisabled_NeverTouchesStoreAndNeverSends` (kill-switch composition) | PASS |
| (e) `RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal` (restart persistence over one shared in-memory SQLite database) | PASS |

Fail-before counterpart: `dedupe-expect-fail.2026-07-02T12-10.md` in this directory (EXIT_CODE 1; tests a/b/c/e failed as expected, d passed).
