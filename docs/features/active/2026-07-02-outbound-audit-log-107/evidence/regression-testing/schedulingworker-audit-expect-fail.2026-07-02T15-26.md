# Phase 4 — [expect-fail] SchedulingWorker Audit Emission Tests (red run)

Timestamp: 2026-07-02T15-26
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerAuditTests"`
EXIT_CODE: 1
Output Summary: Failed! Failed: 8, Passed: 0, Total: 8. The file compiles (all referenced types exist after P4-T5); every emission-behavior test fails because `ProposeAndActAsync` does not yet emit audit records. Failing tests:
- RunCycle_SendDisabled_WritesSendDisabledRecord
- RunCycle_DedupeHit_WritesDedupeSkippedRecord
- RunCycle_SendSuccess_WritesSentRecordAfterSendAndBeforeDedupeRecord
- RunCycle_SendFailure_WritesSendFailedWithErrorDetailBeforePropagation
- RunCycle_SendSuccess_CorrelationIdIsGuidAndMatchesForwardedValue
- RunCycle_AuditWriteFailure_OnSuccessPath_ContinuesAndLogsError
- RunCycle_AuditWriteFailure_OnFailurePath_DoesNotMaskOriginalException
- RunCycle_SendSuccess_RecordedAtUtcEqualsFakeTimeProviderValue

This is the expected red state of the P4-T6 [expect-fail] task; the emissions are implemented in P4-T7 and the pass-after run is recorded by P4-T8.
