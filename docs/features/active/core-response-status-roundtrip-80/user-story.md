# core-response-status-roundtrip (User Story)

- **Issue:** #80
- **Owner:** drmoisan
- **Last Updated:** 2026-07-01
- **Status:** Draft

Note: this feature is a bug fix (work mode `full-bug`, AC source `spec.md`). This user-story file exists because the repository's planner gate mechanically requires it; the authoritative specification is `docs/features/active/core-response-status-roundtrip-80/spec.md`.

## Story

As the deterministic scheduling agent consuming Core-cached calendar events, I need `EventDto.ResponseStatus` to survive a Core cache write/read round-trip, so that scheduling normalization (e.g. `SchedulingDtoMapper` and organizer/attendee-response logic downstream of the bridge's `ResponseStatus == 1` organizer derivation) operates on the actual meeting response state instead of an always-null value silently substituted by `CoreCacheRepository`.

## Acceptance Criteria

- [x] The Core `events` schema includes a `response_status INTEGER NULL` column in both `CreateTablesSql` (fresh database) and an idempotent, guarded ALTER migration in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (existing-database upgrade); running `InitializeAsync` twice on the same database raises no error.
- [x] `EventDto.ResponseStatus` survives a Core cache write/read round-trip (`UpsertEventsAsync` then `GetEventAsync`) for a non-null value (e.g. 4 = Declined) and for null (read back as null, not 0).
- [x] No behavior change for any other `EventDto` field: all existing tests in `tests/OpenClaw.Core.Tests/` pass unchanged.
- [x] A regression test in `tests/OpenClaw.Core.Tests/` (mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`) fails before the fix and passes after.
- [x] Full C# toolchain passes: `dotnet csharpier check .`, `dotnet build` (analyzers clean), `dotnet test --collect:"XPlat Code Coverage"`; line coverage >= 85%, branch coverage >= 75%, and all changed lines are covered.
