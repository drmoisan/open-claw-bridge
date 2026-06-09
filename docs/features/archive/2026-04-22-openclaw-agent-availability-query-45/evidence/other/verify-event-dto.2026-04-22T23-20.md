# AC-4 Verification — EventDto, Scanner, Cache, Migration

Timestamp: 2026-04-22T23-20

## EventDto (additive-only tail parameter)

`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` diff hunk:

```
@@ -108,7 +108,8 @@ public sealed record EventDto(
     string? ResourcesJson,
     string? BodyPreview,
     bool ProtectedFieldsAvailable,
-    bool IsRedacted
+    bool IsRedacted,
+    int? ResponseStatus = null
 );
```

Every pre-existing positional parameter retains its original name, type, position, and nullability. The new `int? ResponseStatus = null` is appended at the tail with a default value so existing callers that construct `EventDto` positionally without specifying the new field continue to compile unchanged.

## OutlookScanner.NormalizeEvent (additive read via existing helper)

`src/OpenClaw.MailBridge/OutlookScanner.cs` diff hunk:

```
@@ -452,7 +452,8 @@ internal sealed class OutlookScanner : IOutlookScanner
                 _settings
             ),
             !string.IsNullOrWhiteSpace(OutlookComHelpers.GetOptionalString(item, "Body")),
-            false
+            false,
+            OutlookComHelpers.GetOptionalInt(item, "ResponseStatus")
         );
```

`OutlookComHelpers.GetOptionalInt` (see `OutlookComHelpers.cs` lines 73–84) wraps the reflection read in a `try/catch` that returns `null` on any exception, which is the same mechanism used for `BusyStatus`, `MeetingStatus`, and `Sensitivity`. Per-event COM errors do not fail the scan.

## Cache schema — additive column and idempotent migration

`src/OpenClaw.MailBridge/CacheRepository.cs` DDL change:

```
- events(... last_modified_utc TEXT NULL, last_seen_utc TEXT NOT NULL);
+ events(... last_modified_utc TEXT NULL, last_seen_utc TEXT NOT NULL, response_status INTEGER NULL);
```

Idempotent migration (new, guarded by `PRAGMA table_info(events)`):

```csharp
private static async Task MigrateEventsSchemaAsync(SqliteConnection conn)
{
    if (await EventsColumnExistsAsync(conn, "response_status"))
    {
        return;
    }

    var alter = conn.CreateCommand();
    alter.CommandText = "ALTER TABLE events ADD COLUMN response_status INTEGER NULL;";
    await alter.ExecuteNonQueryAsync();
}
```

No existing column is dropped, renamed, or altered.

## Upsert and read paths

- `UpsertEventAsync` adds `response_status` to the INSERT column list, the VALUES list, and the `ON CONFLICT … DO UPDATE SET` block.
- `AddEventParameters` binds `$response_status` via the existing `ToDbValue(int?)` helper so a `null` value is written as `DBNull`.
- `ReadEvent` (in `CacheRepository.Readers.cs`) passes `GetNullableInt(reader, "response_status")` into `EventDto` as the new tail parameter.

## Tests (MSTest + FluentAssertions, in-memory SQLite)

- `tests/OpenClaw.MailBridge.Tests/OutlookScannerResponseStatusTests.cs` — 2 tests, both passing:
  - `ScanCalendarAsync_should_populate_ResponseStatus_from_com_when_value_is_accepted` — asserts `EventDto.ResponseStatus == 3`.
  - `ScanCalendarAsync_should_set_ResponseStatus_to_null_when_com_property_is_absent_and_should_not_fail_the_scan` — asserts `EventDto.ResponseStatus == null` and the scan completes.

- `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs` — 2 tests, both passing:
  - `UpsertEvent_then_GetEvent_should_round_trip_response_status_when_declined` — asserts `4` round-trips.
  - `UpsertEvent_then_GetEvent_should_round_trip_response_status_when_null` — asserts `null` round-trips as null (not 0).

- `tests/OpenClaw.MailBridge.Tests/CacheRepositoryMigrationIdempotencyTests.cs` — 1 test, passing:
  - `InitializeAsync_should_be_idempotent_and_keep_events_schema_stable` — runs `InitializeAsync` twice, asserts the second call does not throw and the events-table schema (as reported by `PRAGMA table_info`) is unchanged.

All five tests passed in the Phase 3 toolchain run (see `toolchain-csharp.2026-04-22T23-20.md`). No existing tests regressed (see `csharp-regression-existing-tests.2026-04-22T23-20.md`).

## Partial-class split (file-size compliance)

Two focused partial files were added to keep each primary source file within the 500-line guideline:

- `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` — row materialization helpers (`ReadMessage`, `ReadEvent`, `ToDbValue`, `GetString/GetDateTimeOffset/GetNullableInt/GetBoolean`).
- `src/OpenClaw.MailBridge/OutlookScanner.Normalized.cs` — nested `NormalizedMessage`/`NormalizedEvent` records.

Both files declare the same `internal sealed partial class` type they extract from; no public API surface changed.

AC-4: SATISFIED
