# Final QA — Acceptance Criteria Verification (spec.md, issue #107)

Timestamp: 2026-07-02T15-33
Command: mapping of the six spec ACs to passing tests and evidence artifacts (all test runs referenced below exited 0 on the final pass; see the P5-T3 artifact)
EXIT_CODE: 0

## AC1 — `audit_log` table, idempotent migration, query-by-message-id most-recent-first, restart survival
- Tests (all passing, `CoreCacheRepositoryAuditLogTests`): `RecordAsync_then_GetByMessageIdAsync_should_round_trip_all_fields`, `RecordAsync_with_null_optionals_should_round_trip_nulls`, `GetByMessageIdAsync_should_order_most_recent_first`, `GetByMessageIdAsync_identical_timestamps_should_tie_break_by_id_desc`, `Second_repository_instance_should_read_records_written_by_the_first`, `InitializeAsync_twice_should_not_throw_and_audit_log_should_exist`, `InitializeAsync_should_add_audit_log_to_pre_existing_database`, `Store_methods_should_work_on_fresh_database_without_InitializeAsync`, plus the 12 `ArgumentException` guard rows.
- Evidence: `evidence/regression-testing/repository-audit-tests.2026-07-02T15-15.md` (23/23 pass)
- Status: PASS

## AC2 — One record per Stage 0 decision point with master §13 step 12 fields
- Tests (all passing, `SchedulingWorkerAuditTests`): `RunCycle_SendDisabled_WritesSendDisabledRecord` (a), `RunCycle_DedupeHit_WritesDedupeSkippedRecord` (b), `RunCycle_SendSuccess_WritesSentRecordAfterSendAndBeforeDedupeRecord` (c), `RunCycle_SendFailure_WritesSendFailedWithErrorDetailBeforePropagation` (d).
- Evidence: red run `evidence/regression-testing/schedulingworker-audit-expect-fail.2026-07-02T15-26.md`; green run `evidence/regression-testing/schedulingworker-audit-pass-after.2026-07-02T15-30.md`
- Status: PASS

## AC3 — Audit-write failure does not break processing and does not mask the original send exception
- Tests: `RunCycle_AuditWriteFailure_OnSuccessPath_ContinuesAndLogsError`, `RunCycle_AuditWriteFailure_OnFailurePath_DoesNotMaskOriginalException` (Error-level log verified via captured `Mock<ILogger<SchedulingWorker>>`).
- Evidence: same red/green pair as AC2
- Status: PASS

## AC4 — Repository clock-free; correlation id is a worker GUID forwarded as the HostAdapter request id
- Repository clock-free: `CoreCacheRepository.AuditLog.cs` contains no `TimeProvider`/`DateTime.Now`/`DateTime.UtcNow` usage (P2-T2 acceptance; grep-verified; banned-API analyzer also enforces).
- Tests: `RunCycle_SendSuccess_CorrelationIdIsGuidAndMatchesForwardedValue` (e), `RunCycle_SendSuccess_RecordedAtUtcEqualsFakeTimeProviderValue` (g), and the P3-T3 seam tests `SendMailAsync_SuppliedCorrelationId_ForwardsVerbatimAsRequestId` / `SendMailAsync_NullCorrelationId_ForwardsNullRequestId` in `HostAdapterSchedulingServiceTests`.
- Status: PASS

## AC5 — Property-based round-trip (CsCheck) with UTC normalization
- Test: `CoreCacheRepositoryAuditLogPropertyTests.RecordAsync_GetByMessageIdAsync_RoundTripsAfterUtcNormalization` (iter 100; non-UTC offsets on every `DateTimeOffset` field; null/non-null optional combinations).
- Evidence: `evidence/regression-testing/repository-audit-tests.2026-07-02T15-15.md`
- Status: PASS

## AC6 — Full toolchain pass; coverage thresholds hold with changed lines covered
- Evidence: `evidence/qa-gates/final-csharpier.2026-07-02T15-31.md` (format), `evidence/qa-gates/final-dotnet-build.2026-07-02T15-32.md` (lint + type-check, 0 warnings), `evidence/qa-gates/final-dotnet-test-coverage.2026-07-02T15-33.md` (architecture + unit + property + contract tests, 807 passed), `evidence/qa-gates/coverage-comparison.2026-07-02T15-33.md` (line 96.83% pooled / 98.82% Core; branch 89.96% pooled / 92.05% Core; no changed-line regression).
- Status: PASS

All six ACs mapped and passing; no unmapped AC remains.
