# fix(core): CoreCacheRepository drops EventDto.ResponseStatus on round-trip (missing response_status column)

- Issue: #80
- Type: bug
- Work Mode: full-bug

## Summary

- `CoreCacheRepository` (Core's SQLite event cache) does not persist `EventDto.ResponseStatus`. The bridge-side `CacheRepository` (`src/OpenClaw.MailBridge/CacheRepository.cs`) added a `response_status` column with an idempotent `ALTER TABLE events ADD COLUMN response_status INTEGER NULL` migration and round-trips the value. `CoreCacheRepository` (`src/OpenClaw.Core/CoreCacheRepository.cs`) has no `response_status` column, so when Core caches events the `ResponseStatus` value is silently dropped and re-reads return null.
- Impact: Core consumers of cached events (e.g. the deterministic scheduling normalizer / `SchedulingDtoMapper`) cannot rely on `ResponseStatus` after a Core cache round-trip.
- Suggested fix: add a `response_status INTEGER NULL` column behind an idempotent migration mirroring the bridge-side `MigrateEventsSchemaAsync` pattern; wire write and read paths; add a regression test asserting `ResponseStatus` survives a Core write/read cycle.

## Acceptance Criteria

- [x] The Core `events` schema includes a `response_status INTEGER NULL` column in both `CreateTablesSql` (fresh database) and an idempotent, guarded ALTER migration in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (existing-database upgrade); running `InitializeAsync` twice on the same database raises no error.
- [x] `EventDto.ResponseStatus` survives a Core cache write/read round-trip (`UpsertEventsAsync` then `GetEventAsync`) for a non-null value (e.g. 4 = Declined) and for null (read back as null, not 0).
- [x] No behavior change for any other `EventDto` field: all existing tests in `tests/OpenClaw.Core.Tests/` pass unchanged.
- [x] A regression test in `tests/OpenClaw.Core.Tests/` (mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`) fails before the fix and passes after.
- [x] Full C# toolchain passes: `dotnet csharpier check .`, `dotnet build` (analyzers clean), `dotnet test --collect:"XPlat Code Coverage"`; line coverage >= 85%, branch coverage >= 75%, and all changed lines are covered.
