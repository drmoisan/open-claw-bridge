# Expect-Fail Evidence — SchedulingWorkerDedupeTests before the pipeline change (P3-T5)

Timestamp: 2026-07-02T12-10
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerDedupeTests"` (repo root)
EXIT_CODE: 1

Output Summary:
Run before the P4-T1 pipeline change (worker does not yet consult or write the store). Observed outcomes match the plan's expectations exactly — 4 failed, 1 passed, 5 total:

| Test | Expected | Observed |
|---|---|---|
| (a) `RunCycle_StoreHit_SkipsSendAndCompletesWithoutThrowing` (skip-on-hit) | FAIL | FAIL |
| (b) `RunCycle_StoreMiss_SendsThenRecordsKeyWithInjectedClockTimestamp` (record-on-success) | FAIL | FAIL |
| (c) `RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate` (no-record-on-failure) | FAIL | FAIL |
| (d) `RunCycle_SendDisabled_NeverTouchesStoreAndNeverSends` (kill-switch composition) | PASS | PASS |
| (e) `RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal` (restart persistence) | FAIL | FAIL |

Failure reasons: (a) `SendMailAsync` is invoked despite the store hit; (b) `RecordAsync` is never invoked; (c) `IsRecordedAsync` is never consulted; (e) `SendMailAsync` is invoked twice (once per worker) because no record is persisted between cycles. Test (d) passes because the `SendEnabled=false` path already returns before any send, and the store is untouched today.
