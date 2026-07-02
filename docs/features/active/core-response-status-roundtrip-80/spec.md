# core-response-status-roundtrip (Spec)

- **Issue:** #80
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-01
- **Status:** Draft
- **Version:** 0.2

## Context
- Summary of the bug and its impact (link to repro/playbook entry): `CoreCacheRepository` (Core's SQLite event cache) does not persist `EventDto.ResponseStatus`. The bridge-side `CacheRepository` added a `response_status INTEGER NULL` column with an idempotent guarded ALTER migration and round-trips the value; the Core-side repository never received the equivalent change. When Core caches events via `UpsertEventsAsync`, the `ResponseStatus` value is silently dropped, and `GetEventAsync` / `ListEventsAsync` always return `ResponseStatus: null` (hardcoded in `ReadEvent`, `src/OpenClaw.Core/CoreCacheRepository.Events.cs` line 248). See GitHub issue #80 and `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.
- Observed environment(s): all environments running the Core service SQLite event cache; the defect is in code, not environment-specific.
- Customer impact and severity (who is affected, how often, how bad): every Core consumer of cached events is affected on every read. The deterministic scheduling normalizer / `SchedulingDtoMapper` cannot rely on `ResponseStatus` after a Core cache round-trip. Data loss is silent (no error, no log); severity is elevated because `IsOrganizer` derivation at the bridge uses `ResponseStatus == 1` (`src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs:65`), so downstream normalization depends on this field's fidelity.
- First observed date and version(s) impacted: the gap was deliberately deferred out of the issue-#72 Core schema work (recorded as a non-goal in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` lines 10 and 107, which reference issue #80). Confirmed by orchestrator research 2026-07-01.

## Repro & Evidence
- Steps to reproduce (with data/flags/inputs):
  1. Construct a `CoreCacheRepository` against an in-memory shared-cache SQLite database and call `InitializeAsync`.
  2. Build an `EventDto` with `ResponseStatus: 4` (Declined).
  3. Call `UpsertEventsAsync([evt], bridgeStatus, requestId, observedAtUtc)`.
  4. Call `GetEventAsync(bridgeId)` for the same event.
- Expected vs actual behavior: expected `loaded.ResponseStatus == 4`; actual `loaded.ResponseStatus == null`. The null case is also lossy in principle: null is only "preserved" because every read is forced to null.
- Logs/screenshots/error snippets: none — the drop is silent. Code evidence: (a) `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` — `events` DDL (lines 56-88) has no `response_status` column, and the file's doc comments (lines 10, 107) state the column is intentionally deferred to issue #80; (b) `src/OpenClaw.Core/CoreCacheRepository.Events.cs` — `AddEventParameters` binds no `$response_status` parameter and `ReadEvent` (line ~248) hardcodes `ResponseStatus: null`.
- Frequency / determinism (always, intermittent, data-dependent): always, deterministic — 100% of Core cache round-trips drop the value.

## Scope & Non-Goals
- In scope:
  - Add `response_status INTEGER NULL` to the Core `events` table: in `CreateTablesSql` (fresh databases) and via a guarded, idempotent ALTER in `MigrateEventsSchemaAsync` (existing databases), in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`.
  - Wire the write path (`UpsertEventsAsync` INSERT/UPSERT SQL and `AddEventParameters`) and the read path (`ReadEvent`) in `src/OpenClaw.Core/CoreCacheRepository.Events.cs`.
  - Update the now-stale doc comments in `CoreCacheRepository.Schema.cs` that describe the column as deferred to issue #80.
  - Add a regression test in `tests/OpenClaw.Core.Tests/` mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`.
- Out of scope / non-goals:
  - Any change to the bridge-side `CacheRepository` (already correct; it is the reference implementation).
  - Any change to `EventDto` or other contract types (`ResponseStatus` already exists as `int? ResponseStatus = null` in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs:118`).
  - Changes to `IsOrganizer` derivation, `SchedulingDtoMapper`, or any consumer logic.
  - Opportunistic refactors of the repository partials (minimal targeted fix per the bugfix workflow).
- Explicitly excluded systems, integrations, or datasets: bridge cache database, `messages` table, `poll_cursors`, `ingest_runs`, `bridge_status_snapshots`; Outlook/Graph scanning code.

## Root Cause Analysis
- Current hypothesis or confirmed root cause: confirmed. The issue-#72 Core schema work intentionally excluded `response_status` (documented non-goal deferring it to issue #80), so the Core `events` DDL, migration column list, upsert SQL/parameters, and reader were all built without it. The reader compensates by hardcoding `ResponseStatus: null`, which turns a missing column into silent data loss instead of an error.
- Signals/evidence supporting it:
  - `src/OpenClaw.Core/CoreCacheRepository.Schema.cs:10` — "Per the spec Non-Goals (issue #80), no `response_status` column is added here."
  - `src/OpenClaw.Core/CoreCacheRepository.Schema.cs:107` — "The `response_status` column is intentionally NOT added here; that gap is deferred to issue #80."
  - `src/OpenClaw.Core/CoreCacheRepository.Events.cs:248` — `ResponseStatus: null,` in `ReadEvent`.
  - Bridge-side contrast: `src/OpenClaw.MailBridge/CacheRepository.Schema.cs:43-45` adds the column behind a `PRAGMA table_info` guard.
- Affected components/modules (paths, services, pipelines): `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, `src/OpenClaw.Core/CoreCacheRepository.Events.cs`; downstream, any Core consumer of cached events (scheduling normalization path).

## Proposed Fix

### Design summary (what changes where):
Mirror the bridge-side pattern in the Core repository partials:
1. `CoreCacheRepository.Schema.cs`: add `response_status INTEGER NULL` to the `events` DDL in `CreateTablesSql`; add a guarded ALTER for `response_status` in `MigrateEventsSchemaAsync` (either as an entry alongside the existing column list or as an explicit guarded ALTER, mirroring `src/OpenClaw.MailBridge/CacheRepository.Schema.cs:43-45`); correct the two doc comments that describe the column as deferred.
2. `CoreCacheRepository.Events.cs`: add `response_status` to the INSERT column list, VALUES list, and `ON CONFLICT ... DO UPDATE SET` clause in `UpsertEventsAsync`; bind `$response_status` via `ToDbValue(evt.ResponseStatus)` in `AddEventParameters`; replace the hardcoded `ResponseStatus: null` in `ReadEvent` with `ReadNullableInt(reader, "response_status")`.

### Boundaries and invariants to preserve:
- Idempotent initialization: `InitializeAsync` must remain safe to run repeatedly on both fresh and already-migrated databases (each ALTER guarded by `PRAGMA table_info`, matching `EventsColumnExistsAsync`).
- Null fidelity: SQL NULL round-trips to `null` (`int?`), never coerced to 0.
- No change to any other column's write/read behavior, the upsert conflict semantics, or the `ListEventsAsync` ordering/filtering.
- Partial-class layout: schema/migration logic stays in the Schema partial, event persistence stays in the Events partial; the (pre-existing over-cap) base `CoreCacheRepository.cs` does not grow.

### Dependencies or blocked work:
None. `EventDto.ResponseStatus` already exists in the contracts assembly; the bridge-side reference implementation and its test are already merged.

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:
- `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` — DDL, migration, doc comments.
- `src/OpenClaw.Core/CoreCacheRepository.Events.cs` — upsert SQL, parameter binding, reader.
- `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs` (new) — regression test.

#### Functions/classes/CLI commands impacted:
- `CoreCacheRepository.CreateTablesSql` (constant), `CoreCacheRepository.MigrateEventsSchemaAsync`, `CoreCacheRepository.UpsertEventsAsync`, `CoreCacheRepository.AddEventParameters`, `CoreCacheRepository.ReadEvent`. No public API surface changes; `CoreCacheRepository` is `internal`.

#### Data flow and validation changes:
- Write: `EventDto.ResponseStatus` (`int?`) → `$response_status` parameter via existing `ToDbValue(int?)` helper (null → `DBNull.Value`) → `response_status INTEGER NULL` column.
- Read: `response_status` column → existing `ReadNullableInt` helper → `EventDto.ResponseStatus`.
- No new validation: the field is a pass-through nullable integer, consistent with `busy_status`, `meeting_status`, and `sensitivity`.

#### Error handling and logging updates:
None required. Migration failures surface as `SqliteException` from `ExecuteNonQueryAsync`, consistent with the existing migration paths. No logging changes.

#### Rollback/feature-flag considerations (if applicable):
No feature flag. The added column is `NULL`-able with no default constraint; rolling back the code leaves an unused nullable column, which is harmless. Pre-fix rows read back with `ResponseStatus == null`, identical to current behavior.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:
- Column: `response_status INTEGER NULL` on the Core `events` table.
- DTO field: `EventDto.ResponseStatus` (`int?`, positional record parameter with default `null`). Values follow the Outlook `ResponseStatus` integer convention already used bridge-side (e.g. 1 = Organized, 4 = Declined).

#### Required configuration keys and defaults:
None. No configuration changes.

#### Backward-compatibility expectations:
- Existing Core cache databases upgrade in place via the guarded ALTER; no data rewrite.
- Rows written before the fix have SQL NULL in the new column and read back as `null` — the same value they read back today.
- No change to the `EventDto` contract, HTTP APIs, or bridge cache schema.

#### Performance constraints (latency/throughput/memory):
Negligible. One additional `PRAGMA table_info` check during initialization and one additional column per event row. No measurable latency or memory impact expected; no benchmark gate applies to this path.

## Assumptions, Constraints, Dependencies
- Assumptions (environment, data, access): SQLite via `Microsoft.Data.Sqlite`; tests can use in-memory shared-cache databases (`Mode=Memory;Cache=Shared`) as the existing Core cache tests do.
- Constraints (budget, performance, compatibility):
  - 500-line file cap: add to the existing partials (`CoreCacheRepository.Schema.cs` at 241 lines, `CoreCacheRepository.Events.cs` at 259 lines); do not create oversized files and do not grow the over-cap base file.
  - No temporary files in tests: follow the in-memory shared-cache SQLite pattern already used by `CoreCacheRepositoryGraphFieldsTests.cs` and `CacheRepositoryResponseStatusTests.cs`.
  - Minimal targeted fix per the bugfix workflow: no opportunistic refactors.
- External dependencies (services, libraries, releases): none beyond already-approved packages (`Microsoft.Data.Sqlite`, MSTest, FluentAssertions).

## Data / API / Config Impact
- User-facing or API changes: none. `EventDto` is unchanged; only Core cache persistence fidelity improves.
- Data or migration considerations: additive, idempotent, guarded `ALTER TABLE events ADD COLUMN response_status INTEGER NULL` on existing Core databases; column present in DDL for fresh databases. No backfill (historical values were never captured and cannot be recovered).
- Logging/telemetry updates (if any): none.
- Compatibility notes (CLI flags, config schemas, versioning): none.

## Test Strategy
- Regression tests to add or update: new `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs`, mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs` but exercising `CoreCacheRepository.UpsertEventsAsync` / `GetEventAsync`. Must fail against the pre-fix code (reads return null) and pass after the fix.
- Unit tests (MSTest + FluentAssertions, repository convention) for the fixed behavior and boundaries:
  - Non-null round-trip: write `ResponseStatus: 4`, read back `4`.
  - Null round-trip: write `ResponseStatus: null`, read back `null` (not 0).
- Edge cases and negative scenarios (invalid inputs, missing data, boundary values):
  - Migration idempotency: `InitializeAsync` called twice on the same database completes without a "duplicate column" error (pattern already covered for issue-#72 columns; extend or mirror for `response_status` if not covered by the shared migration loop).
  - Existing-database upgrade: a database created without the column gains it via the guarded ALTER (covered by the migration path exercised in initialization tests).
- Error handling and logging verification: no new error paths; verify no analyzer or nullable warnings are introduced.
- Coverage impact and targets for changed lines/modules: line >= 85% and branch >= 75% hold repo-wide; all changed lines in the two partials are covered by the round-trip and initialization tests. No coverage regression on changed lines.
- Toolchain commands to run (format → lint → type-check → test): `dotnet tool restore`; `dotnet csharpier check .`; `dotnet build` (analyzers + nullable as errors); architecture-boundary tests as part of the test run; `dotnet test --collect:"XPlat Code Coverage"`. Restart the loop from formatting if any stage fails or changes files.
- Manual validation steps (if required): none; behavior is fully covered by automated tests.

## Acceptance Criteria
- [x] The Core `events` schema includes a `response_status INTEGER NULL` column in both `CreateTablesSql` (fresh database) and an idempotent, guarded ALTER migration in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (existing-database upgrade); running `InitializeAsync` twice on the same database raises no error.
- [x] `EventDto.ResponseStatus` survives a Core cache write/read round-trip (`UpsertEventsAsync` then `GetEventAsync`) for a non-null value (e.g. 4 = Declined) and for null (read back as null, not 0).
- [x] No behavior change for any other `EventDto` field: all existing tests in `tests/OpenClaw.Core.Tests/` pass unchanged.
- [x] A regression test in `tests/OpenClaw.Core.Tests/` (mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`) fails before the fix and passes after.
- [x] Full C# toolchain passes: `dotnet csharpier check .`, `dotnet build` (analyzers clean), `dotnet test --collect:"XPlat Code Coverage"`; line coverage >= 85%, branch coverage >= 75%, and all changed lines are covered.

## Risks & Mitigations
- Technical or operational risks:
  - Missing one of the three write-path touchpoints (INSERT column list, VALUES list, `DO UPDATE SET` clause) would cause a SQL parameter/column mismatch or a non-updating upsert. Mitigation: the round-trip regression test exercises insert and read; the upsert conflict branch is exercised by re-upserting the same `bridge_id` if needed for branch coverage.
  - Stale doc comments in `CoreCacheRepository.Schema.cs` (currently citing issue #80 as a deferral) would misdocument the schema after the fix. Mitigation: comment updates are in scope.
- Mitigations and rollbacks: rollback is a code revert; the nullable column is inert without the code and requires no schema rollback.

## Rollout & Follow-up
- Release/rollout steps: standard PR merge to `main`; the guarded migration upgrades existing Core databases on next service initialization. No coordinated deployment steps.
- Post-fix monitoring or clean-up tasks: none required; optionally spot-check a Core cache after upgrade to confirm the column exists and populates.
- Links: GitHub issue #80; `docs/features/active/core-response-status-roundtrip-80/issue.md`; `docs/features/active/core-response-status-roundtrip-80/user-story.md`; `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`; bridge reference implementation `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` and test `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`.
