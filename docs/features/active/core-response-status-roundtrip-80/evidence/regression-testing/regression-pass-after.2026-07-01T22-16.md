# Regression Evidence — Pass After Fix (Issue #80)

Timestamp: 2026-07-02T11-20
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositoryResponseStatusTests" --no-build
EXIT_CODE: 0
Output Summary:
- Result: Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3 (OpenClaw.Core.Tests.dll, net10.0).
- `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_declined` — PASSED (4 round-trips).
- `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_null` — PASSED (NULL round-trips as null, not 0).
- `InitializeAsync_should_add_response_status_column_to_existing_database` — PASSED (guarded ALTER
  upgrade path adds the column to a pre-#80 database; second InitializeAsync is idempotent; value 4
  round-trips post-migration).
- Fix applied only to `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (DDL column, guarded ALTER,
  doc-comment corrections) and `src/OpenClaw.Core/CoreCacheRepository.Events.cs` (INSERT column,
  VALUES parameter, DO UPDATE SET clause, parameter binding, ReadNullableInt read).
- Completes the pass-after half of AC-4 and demonstrates AC-1/AC-2.
