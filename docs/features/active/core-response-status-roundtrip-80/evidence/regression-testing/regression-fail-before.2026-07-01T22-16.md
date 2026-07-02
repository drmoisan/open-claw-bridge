# Regression Evidence — Fail Before Fix (Issue #80)

Timestamp: 2026-07-02T11-14
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositoryResponseStatusTests" --no-build
EXIT_CODE: 1
Output Summary:
- Result: Failed! - Failed: 2, Passed: 1, Skipped: 0, Total: 3 (OpenClaw.Core.Tests.dll, net10.0).
- FAILED `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_declined`:
  "Expected loaded!.ResponseStatus to be 4 because Declined (4) must round-trip through SQLite, but found <null>." (expected 4, actual null)
- FAILED `InitializeAsync_should_add_response_status_column_to_existing_database`:
  "Expected loaded!.ResponseStatus to be 4 because the migrated column must persist and return the written value, but found <null>." (expected 4, actual null)
- PASSED `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_null`:
  This test passes pre-fix only because the pre-fix `ReadEvent` in
  `src/OpenClaw.Core/CoreCacheRepository.Events.cs` hardcodes `ResponseStatus: null,` — every read
  returns null regardless of what was written. The pass is an artifact of the defect, not evidence
  of correct null round-trip behavior; the same defect is what makes tests 1 and 3 fail.
- This satisfies the fail-before half of AC-4: the regression test demonstrably fails against
  pre-fix production code (no `response_status` column in the Core schema, no write binding, and a
  hardcoded null read).
