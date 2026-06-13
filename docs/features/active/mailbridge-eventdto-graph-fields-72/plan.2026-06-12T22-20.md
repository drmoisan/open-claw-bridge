# mailbridge-eventdto-graph-fields - Plan

- **Issue:** #72
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-12T22-20
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature
- **Language / Tier:** C# (.NET 10), T2 (managed surface of `OpenClaw.MailBridge.Contracts` and consumers)

## Required References

- General code-change policy: `.claude/rules/general-code-change.md`
- General unit-test policy: `.claude/rules/general-unit-test.md`
- C# standards: `.claude/rules/csharp.md`
- Quality tiers / coverage thresholds: `.claude/rules/quality-tiers.md` (line >= 85%, branch >= 75%, uniform T1–T4)
- Architecture boundaries: `.claude/rules/architecture-boundaries.md` (COM confined to `OpenClaw.MailBridge`; `EventDto` + sensitivity label live in leaf `OpenClaw.MailBridge.Contracts`)
- Spec: `docs/features/active/mailbridge-eventdto-graph-fields-72/spec.md` (Status: Approved)
- User story / acceptance criteria: `docs/features/active/mailbridge-eventdto-graph-fields-72/user-story.md`
- Research: `artifacts/research/2026-06-12-issue-72-eventdto-com-analogs-research.md`

**All work must comply with these policies; do not duplicate their content here.**

## Acceptance Criteria Mapping (user-story.md)

The user-story `## Acceptance Criteria` section contains six checkboxes, referenced here as AC1–AC6 in document order:

- **AC1** — `EventDto` exposes all nine new fields with the specified types and remains source-compatible (existing callers compile unmodified). → P1-T2, P1-T3, P7-T2
- **AC2** — `OutlookScanner.NormalizeEvent` populates all nine fields from the specified COM analogs/derivations. → P2-T1, P2-T2, P2-T3, P2-T4
- **AC3** — `ResponseShaper.ShapeEvent` nulls `bodyFull` in safe mode (parity with `BodyPreview`); enhanced mode returns full untruncated `bodyFull`. → P3-T1, P3-T2
- **AC4** — Both SQLite caches (`CacheRepository`, `CoreCacheRepository`) round-trip all nine new fields with idempotent schema migrations. → P4-T1..T4, P5-T1..T4
- **AC5** — A scan of a recurring online meeting yields non-null `iCalUId`, `isOnlineMeeting=true`, and the correct `sensitivityLabel`. → P2-T4, P6-T2
- **AC6** — Existing contract tests pass; new unit tests cover the new fields, the safe/enhanced shaping of `bodyFull`, and the cache round-trip. Coverage thresholds hold: line >= 85%, branch >= 75% (T2). → P1-T4, P2-T6, P3-T3, P4-T5, P5-T5, P6-T3, P7-T1..T5

## Mandatory Toolchain Loop (per implementation task)

Every code/test task runs the seven-stage C# loop in order and restarts from stage 1 if any stage fails or auto-fixes files (see `.claude/rules/general-code-change.md` and `.claude/rules/csharp.md`):

1. **Format** — `dotnet csharpier .`
2. **Lint / analyzers** — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
3. **Type-check (nullable)** — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
4. **Architecture** — verify `ProjectReference` graph against `.claude/rules/architecture-boundaries.md` (no new project references; COM stays in `OpenClaw.MailBridge`)
5. **Test** — `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

Stages 1–3 share the build invocation pattern; treat any analyzer or nullable warning as a failure. Phase boundaries must be build-green and test-green.

## Evidence Locations (canonical — non-overridable)

All evidence artifacts MUST be written under `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/<kind>/` per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Writing to `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, or any non-canonical path is a policy violation.

- Baseline: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/baseline/`
- Final QA gates: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/qa-gates/`
- Regression / coverage delta: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/regression-testing/`

Each command-step artifact MUST include `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`. Test artifacts MUST record numeric coverage headline values.

---

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Policy Read

- [x] [P0-T1] Read repo policy files in required order and record evidence: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/architecture-boundaries.md`.
  - Acceptance: `evidence/baseline/phase0-instructions-read.md` exists with `Timestamp:`, `Policy Order:`, and the explicit list of files read.
- [x] [P0-T2] Capture baseline CSharpier format state. Command: `dotnet csharpier --check .`
  - Acceptance: `evidence/baseline/baseline-format.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail, count of files needing format).
- [x] [P0-T3] Capture baseline analyzer/lint build state. Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
  - Acceptance: `evidence/baseline/baseline-lint.md` with required schema fields and `Output Summary:` (warning/error counts).
- [x] [P0-T4] Capture baseline nullable type-check build state. Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
  - Acceptance: `evidence/baseline/baseline-typecheck.md` with required schema fields and `Output Summary:`.
- [x] [P0-T5] Capture baseline test + coverage state. Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Acceptance: `evidence/baseline/baseline-test.md` with required schema fields and `Output Summary:` containing numeric baseline line coverage % and branch coverage % (not placeholders), and passed/failed test counts.
- [x] [P0-T6] Record baseline line counts for files that approach the 500-line cap to size split decisions later. Files: `src/OpenClaw.MailBridge/CacheRepository.cs`, `src/OpenClaw.Core/CoreCacheRepository.cs`, `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`.
  - Acceptance: `evidence/baseline/baseline-file-sizes.md` records the current line count of each file with `Timestamp:`.

### Phase 1 — Contract Changes (`BridgeContracts.cs`)

- [x] [P1-T1] Add an `EventSensitivityLabel` constants/enum-string helper in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (or a sibling file in the same project if it would push `BridgeContracts.cs` past 500 lines) exposing the four values `normal`/`personal`/`private`/`confidential` and a `FromSensitivity(int?)` mapping (0=normal,1=personal,2=private,3=confidential, else null), mirroring `SchedulingDtoMapper.MapSensitivity`.
  - File: `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (or new `EventSensitivityLabel.cs` in `OpenClaw.MailBridge.Contracts/Models/`).
  - Acceptance: Helper compiles; no project reference added; build-green via toolchain loop stages 1–4.
- [x] [P1-T2] Append the nine new optional parameters to the `EventDto` record in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` after `int? ResponseStatus = null`, in this exact order with defaults: `string[]? Categories = null`, `bool IsOrganizer = false`, `bool IsOnlineMeeting = false`, `bool AllowNewTimeProposals = false`, `string? ICalUId = null`, `string? SeriesMasterId = null`, `DateTimeOffset? LastModifiedDateTime = null`, `string? BodyFull = null`, `string? SensitivityLabel = null`.
  - File: `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (EventDto record at ~lines 94-113).
  - Acceptance: `EventDto` declares 27 positional parameters total; the nine new ones are trailing optional with defaults; file remains under 500 lines (else perform split in P1-T1's sibling-file path).
- [x] [P1-T3] Verify source-compatibility of all existing positional `new EventDto(...)` construction sites by building the full solution without editing any call site.
  - Files verified (no edits): `src/OpenClaw.MailBridge/OutlookScanner.cs:447`, `src/OpenClaw.MailBridge/CacheRepository.Readers.cs:34`, `src/OpenClaw.Core/CoreCacheRepository.cs:642`, and all test construction sites listed in the research §3.1 table.
  - Acceptance: `dotnet build OpenClaw.MailBridge.sln -c Debug` succeeds with zero errors and no changes to those call sites. (AC1)
- [x] [P1-T4] Add a unit test asserting the `EventSensitivityLabel.FromSensitivity` mapping for inputs 0, 1, 2, 3, null, and an out-of-range value (e.g., 99), using `[DataTestMethod]`/`[DataRow]`, MSTest + FluentAssertions.
  - File: `tests/OpenClaw.MailBridge.Tests/EventSensitivityLabelTests.cs` (new).
  - Acceptance: Test class compiles and all rows pass; covers the four valid values plus null and out-of-range branches. (AC6)
- [x] [P1-T5] Run the full seven-stage C# toolchain loop and confirm phase boundary is build-green and test-green.
  - Acceptance: All five stages pass in a single pass; `evidence/qa-gates/phase1-toolchain.md` records the test stage with numeric coverage in `Output Summary:`.

### Phase 2 — Scanner Population (`OutlookScanner.NormalizeEvent`)

- [x] [P2-T1] In `src/OpenClaw.MailBridge/OutlookScanner.cs`, read COM `Body` once into a local in `NormalizeEvent` and reuse it for both the `BodyPreview` shaping and the new `bodyFull` value (avoid a redundant COM read), per spec §"Limits".
  - File: `src/OpenClaw.MailBridge/OutlookScanner.cs` (`NormalizeEvent` ~lines 431-472).
  - Acceptance: A single `GetOptionalString(item, "Body")` read feeds both `ShapePreview(...)` and the raw `bodyFull`; build-green.
- [x] [P2-T2] Add a private static `string[]` splitter helper to a NEW partial-class file `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (partial class `OutlookScanner`; the class is already `internal sealed partial`) that splits `Categories` on `", "` with per-token `Trim()` and `RemoveEmptyEntries`, returning `Array.Empty<string>()` for null/empty input (never null). Do NOT add this helper to `OutlookScanner.cs`, which is already over the 500-line cap.
  - File: `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (new partial of `OutlookScanner`).
  - Acceptance: Helper returns non-null `string[]`; empty input yields a zero-length array; the helper resides in `OutlookScanner.GraphFields.cs`, not `OutlookScanner.cs`.
- [x] [P2-T3] Add a private static `seriesMasterId` derivation helper to the NEW partial-class file `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (partial class `OutlookScanner`) from `GetOptionalInt(item, "RecurrenceState")`: return null for not-recurring (0) and master; return `GlobalAppointmentID` for occurrence/exception. Confirm OlRecurrenceState integers at implementation time per research OQ-1; logic must be null for master/non-recurring and `GlobalAppointmentID` for occurrence/exception. Do NOT add this helper to `OutlookScanner.cs`, which is already over the 500-line cap.
  - File: `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (new partial of `OutlookScanner`).
  - Acceptance: Helper resides in `OutlookScanner.GraphFields.cs` (not `OutlookScanner.cs`) and returns null for master and non-recurring inputs and `GlobalAppointmentID` for the non-master recurring states; the integer mapping is documented in an in-code comment citing OQ-1.
- [x] [P2-T4] Populate the nine new `EventDto` arguments in the `NormalizeEvent` constructor call (this method lives in `OutlookScanner.cs`, so the nine named-argument additions occur there): `Categories` (P2-T2 split helper from `OutlookScanner.GraphFields.cs`), `IsOrganizer = GetOptionalInt(item,"ResponseStatus") == 1`, `IsOnlineMeeting = GetOptionalBool(item,"IsOnlineMeeting")`, `AllowNewTimeProposals = GetOptionalBool(item,"AllowNewTimeProposal")` (singular COM name), `ICalUId = globalAppointmentId`, `SeriesMasterId` (P2-T3 helper from `OutlookScanner.GraphFields.cs`), `LastModifiedDateTime = GetOptionalUtcDateTimeOffset(item,"LastModificationTime")`, `BodyFull` (raw local from P2-T1, not normalized), `SensitivityLabel = EventSensitivityLabel.FromSensitivity(GetOptionalInt(item,"Sensitivity"))`.
  - File: `src/OpenClaw.MailBridge/OutlookScanner.cs` (`new EventDto(...)` at ~line 447).
  - Acceptance: All nine fields are passed by name (use named arguments for the appended parameters); COM interop stays in `OpenClaw.MailBridge`; build-green. (AC2, AC5)
- [x] [P2-T5] Verify the 500-line cap after Phase 2 changes: place the new helpers (P2-T2, P2-T3) in `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` so `OutlookScanner.cs` does not grow further beyond its pre-existing over-cap state, and confirm both files stay under 500 lines (the new partial must be under 500; `OutlookScanner.cs` must not increase beyond its pre-existing count). Record pre/post line counts for both files.
  - Files: `src/OpenClaw.MailBridge/OutlookScanner.cs`, `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs`.
  - Acceptance: `evidence/qa-gates/phase2-filesize.md` records pre/post line counts for both `OutlookScanner.cs` and `OutlookScanner.GraphFields.cs`, noting the pre-existing over-cap status of `OutlookScanner.cs` (507 lines); `OutlookScanner.GraphFields.cs` is under 500 lines and `OutlookScanner.cs` does not grow further.
- [x] [P2-T6] Add scanner unit tests via the existing COM-double pattern (parallel to `OutlookScannerResponseStatusTests.cs`) asserting each new field is populated correctly: categories split (including empty), `isOrganizer` for ResponseStatus 1 vs other, `isOnlineMeeting`, `allowNewTimeProposals`, `iCalUId == GlobalAppointmentID`, `seriesMasterId` for each recurrence state, `lastModifiedDateTime`, `bodyFull` raw, and `sensitivityLabel`.
  - File: `tests/OpenClaw.MailBridge.Tests/OutlookScannerGraphFieldsTests.cs` (new).
  - Acceptance: Tests use Moq/COM-double seams (no live COM, no temp files, deterministic); all assertions pass. (AC2, AC6)
- [x] [P2-T7] Run the full toolchain loop; phase boundary build-green and test-green.
  - Acceptance: All stages pass; `evidence/qa-gates/phase2-toolchain.md` records the test stage with numeric coverage.

### Phase 3 — Redaction (`ResponseShaper.ShapeEvent`)

- [x] [P3-T1] Update `ShapeEvent` in `src/OpenClaw.MailBridge/ResponseShaper.cs` so the safe-mode branch sets `BodyFull = null` alongside `BodyPreview = null` and `IsRedacted = true`.
  - File: `src/OpenClaw.MailBridge/ResponseShaper.cs` (`ShapeEvent` lines 34-54).
  - Acceptance: Safe-mode `with`-expression nulls `BodyFull`; build-green. (AC3)
- [x] [P3-T2] Update the enhanced-mode branch of `ShapeEvent` so `BodyFull` passes through unchanged (raw, untruncated, NOT through `BodySanitizer.NormalizePreview`); `BodyPreview` continues to use `ShapePreview`.
  - File: `src/OpenClaw.MailBridge/ResponseShaper.cs`.
  - Acceptance: Enhanced-mode `BodyFull` equals the input `evt.BodyFull` verbatim. (AC3)
- [x] [P3-T3] Add unit tests for `ShapeEvent` body shaping: safe mode nulls `BodyFull` and sets `IsRedacted=true`; enhanced mode returns full untruncated `BodyFull` (assert a body longer than `BodyPreviewMaxChars` is not truncated in `BodyFull`).
  - File: `tests/OpenClaw.MailBridge.Tests/ResponseShaperEventBodyFullTests.cs` (new), or extend an existing `ResponseShaper` test file if present.
  - Acceptance: Both branches asserted with FluentAssertions; tests pass. (AC3, AC6)
- [x] [P3-T4] Run the full toolchain loop; phase boundary build-green and test-green.
  - Acceptance: All stages pass; `evidence/qa-gates/phase3-toolchain.md` records the test stage with numeric coverage.

### Phase 4 — Bridge Cache Persistence (`CacheRepository`)

- [x] [P4-T1] Add the eight new columns to the bridge `events` `CREATE TABLE IF NOT EXISTS` DDL in `src/OpenClaw.MailBridge/CacheRepository.cs` (`InitializeAsync`, ~line 93): `categories_json TEXT NULL`, `is_organizer INTEGER NOT NULL DEFAULT 0`, `is_online_meeting INTEGER NOT NULL DEFAULT 0`, `allow_new_time_proposals INTEGER NOT NULL DEFAULT 0`, `ical_uid TEXT NULL`, `series_master_id TEXT NULL`, `body_full TEXT NULL`, `sensitivity_label TEXT NULL`. (`last_modified_utc` already exists; no new column.)
  - File: `src/OpenClaw.MailBridge/CacheRepository.cs`.
  - Acceptance: Fresh-database DDL includes all eight new columns; build-green.
- [x] [P4-T2] Extend `MigrateEventsSchemaAsync` in `CacheRepository.cs` to idempotently `ALTER TABLE events ADD COLUMN` each of the eight new columns, guarded by `EventsColumnExistsAsync` (mirroring the existing `response_status` pattern).
  - File: `src/OpenClaw.MailBridge/CacheRepository.cs` (~lines 105-115).
  - Acceptance: Running migration twice produces no "duplicate column" error; each column has its own guarded ALTER. (AC4)
- [x] [P4-T3] Extend the `UpsertEventAsync` INSERT column list, VALUES list, and `ON CONFLICT DO UPDATE SET` clause in `CacheRepository.cs` (~lines 264-297) to include the eight new columns plus `last_modified_utc`.
  - File: `src/OpenClaw.MailBridge/CacheRepository.cs`.
  - Acceptance: All eight new columns plus `last_modified_utc` appear in INSERT, VALUES, and the conflict-update clause.
- [x] [P4-T4] Extend `AddEventParameters` in `CacheRepository.cs` (~lines 418-464): wire `$last_modified_utc` to `ToDbValue(evt.LastModifiedDateTime)` (replacing the hardcoded `DBNull.Value` at line 458), and add the eight new parameters (`categories_json` via JSON serialization of `evt.Categories`, the three bool flags as 1/0, `ical_uid`, `series_master_id`, `body_full`, `sensitivity_label` with `?? DBNull.Value` for nullables). Extend the `ReadEvent` materializer in `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` (~lines 34-54) to read all nine new columns by column name (matching the existing GetString/ReadString-by-name pattern), supplying them as the trailing constructor arguments in the same order as the appended EventDto parameters (deserialize categories_json to string[]).
  - Files: `src/OpenClaw.MailBridge/CacheRepository.cs`, `src/OpenClaw.MailBridge/CacheRepository.Readers.cs`.
  - Acceptance: Writer parameters and reader columns align with the `EventDto` parameter order; `categories` round-trips via JSON; build-green. (AC4)
- [x] [P4-T5] Add a round-trip unit test for the bridge cache asserting that an `EventDto` carrying non-default values for all nine new fields is written and read back identically (including empty-categories and populated-categories cases). Use the named-parameter `EventDto` construction pattern from `CacheRepositoryResponseStatusTests.cs`. No temp files (use in-memory or shared-cache SQLite per existing test pattern).
  - File: `tests/OpenClaw.MailBridge.Tests/CacheRepositoryGraphFieldsTests.cs` (new).
  - Acceptance: Write-then-read returns equal values for all nine fields; idempotent migration verified by initializing twice; tests pass. (AC4, AC6)
- [x] [P4-T6] Verify `CacheRepository.cs` remains under the 500-line cap after the additions; if it exceeds 500 lines, move the writer-parameter helper or DDL/migration into a new partial-class file (e.g., `CacheRepository.Schema.cs`) in `OpenClaw.MailBridge`.
  - File: `src/OpenClaw.MailBridge/CacheRepository.cs` (and new partial if split is required).
  - Acceptance: `evidence/qa-gates/phase4-filesize.md` records the post-change line count; no source file exceeds 500 lines.
- [x] [P4-T7] Run the full toolchain loop; phase boundary build-green and test-green.
  - Acceptance: All stages pass; `evidence/qa-gates/phase4-toolchain.md` records the test stage with numeric coverage.

### Phase 5 — Core Cache Persistence (`CoreCacheRepository`)

- [x] [P5-T1] Add the eight new columns to the Core `events` `CREATE TABLE IF NOT EXISTS` DDL in `src/OpenClaw.Core/CoreCacheRepository.cs` (`InitializeAsync`, ~lines 94-117) and add a `last_modified_utc TEXT NULL` column (the Core schema does not currently have it): same column set as the bridge cache plus `last_modified_utc`.
  - File: `src/OpenClaw.Core/CoreCacheRepository.cs`.
  - Acceptance: Fresh-database Core DDL includes the new columns; build-green.
- [x] [P5-T2] Add a new idempotent migration helper in `CoreCacheRepository.cs` modeled on the bridge's `MigrateEventsSchemaAsync` (with a Core `EventsColumnExistsAsync` equivalent) that adds each new column via guarded `ALTER TABLE events ADD COLUMN`, and call it from `InitializeAsync`. Scope: the nine `EventDto` graph-field columns plus `last_modified_utc` only. Do NOT add a `response_status` column (deferred to issue #80 per spec Non-Goals).
  - File: `src/OpenClaw.Core/CoreCacheRepository.cs`.
  - Acceptance: Migration is idempotent (running twice is safe); `response_status` is not touched; an in-code comment cites issue #80 for the deferred gap. (AC4)
- [x] [P5-T3] Extend the Core `INSERT INTO events` column list, VALUES list, and `ON CONFLICT DO UPDATE SET` clause in `CoreCacheRepository.cs` (~lines 222-253) to include the eight new columns plus `last_modified_utc`.
  - File: `src/OpenClaw.Core/CoreCacheRepository.cs`.
  - Acceptance: New columns appear in INSERT, VALUES, and conflict-update clauses.
- [x] [P5-T4] Extend `AddEventParameters` in `CoreCacheRepository.cs` (~lines 538-589) to bind the eight new parameters plus `$last_modified_utc` (JSON-serialize `Categories`, bools as 1/0, nullables with `?? DBNull.Value`), and extend `ReadEvent` (~lines 642-661) to read all nine new columns by column name (matching the existing GetString/ReadString-by-name pattern), supplying them as the trailing constructor arguments in the same order as the appended EventDto parameters (deserialize categories_json to string[]).
  - File: `src/OpenClaw.Core/CoreCacheRepository.cs`.
  - Acceptance: Writer parameters and reader columns align with `EventDto` parameter order; build-green. (AC4)
- [x] [P5-T5] Add a round-trip unit test for the Core cache asserting an `EventDto` with non-default values for all nine new fields writes and reads back identically (empty and populated categories), and that the new migration is idempotent across two `InitializeAsync` calls. No temp files.
  - File: `tests/OpenClaw.Core.Tests/CoreCacheRepositoryGraphFieldsTests.cs` (new).
  - Acceptance: Write-then-read equality for all nine fields; idempotent migration verified; tests pass. (AC4, AC6)
- [x] [P5-T6] Verify the additive change does not worsen the existing 500-line condition of `CoreCacheRepository.cs` beyond a reasonable margin; if the file grows materially, extract the schema/migration helpers into a new partial-class file (e.g., `CoreCacheRepository.Schema.cs`) in `OpenClaw.Core`. Record the file as a pre-existing over-cap file.
  - File: `src/OpenClaw.Core/CoreCacheRepository.cs` (and new partial if split is performed).
  - Acceptance: `evidence/qa-gates/phase5-filesize.md` records pre- and post-change line counts; any new file added stays under 500 lines; note the pre-existing over-cap status of `CoreCacheRepository.cs`.
- [x] [P5-T7] Run the full toolchain loop; phase boundary build-green and test-green.
  - Acceptance: All stages pass; `evidence/qa-gates/phase5-toolchain.md` records the test stage with numeric coverage.

### Phase 6 — Downstream Mapper Propagation (`SchedulingDtoMapper`)

- [x] [P6-T1] Update `MapEvent` in `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` (~lines 65-97) to wire the new `EventDto` fields into `SchedulingEventDto` in place of placeholders: `Categories = evt.Categories ?? Array.Empty<string>()`, `IsOrganizer = evt.IsOrganizer`, `IsOnlineMeeting = evt.IsOnlineMeeting`, `AllowNewTimeProposals = evt.AllowNewTimeProposals`, `LastModifiedDateTime = evt.LastModifiedDateTime`, `SeriesMasterId = evt.SeriesMasterId`. Set ICalUId = evt.ICalUId (the new EventDto field, which P2-T4 populates from GlobalAppointmentID per spec); do not read evt.GlobalAppointmentId directly in the mapper. For `Sensitivity`, continue using `MapSensitivity(evt.Sensitivity)` (or `evt.SensitivityLabel` if preferred for parity — choose the one consistent with the existing switch).
  - File: `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`.
  - Acceptance: No remaining hardcoded `Array.Empty<string>()`/`false`/`null` placeholders for the six wired fields; build-green.
- [x] [P6-T2] Update or add a `SchedulingDtoMapper` unit test asserting a recurring online meeting `EventDto` (non-null `ICalUId`, `IsOnlineMeeting=true`, `Sensitivity=2/private`, occurrence `SeriesMasterId`) maps to a `SchedulingEventDto` with the same values; assert categories pass-through.
  - File: existing `SchedulingDtoMapper` test file under `tests/OpenClaw.Core.Tests/` (update), or new `SchedulingDtoMapperGraphFieldsTests.cs`.
  - Acceptance: Mapped DTO carries the expected non-placeholder values; tests pass. (AC5, AC6)
- [x] [P6-T3] Update the `SchedulingDtoMapper` XML doc remarks (~lines 14-20, 71-72, 84) and `SchedulingEventDto` doc comments where they state fields are "not yet available (#71-#76)" for the now-populated fields, to reflect that #72 supplies them.
  - Files: `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`, `src/OpenClaw.Core/Agent/Contracts/SchedulingEventDto.cs`.
  - Acceptance: Doc comments no longer claim the #72 fields are unavailable; build-green.
- [x] [P6-T4] Run the full toolchain loop; phase boundary build-green and test-green.
  - Acceptance: All stages pass; `evidence/qa-gates/phase6-toolchain.md` records the test stage with numeric coverage.

### Phase 7 — Final QA Loop, Coverage, and Acceptance Verification

- [x] [P7-T1] Run final CSharpier format check. Command: `dotnet csharpier --check .`
  - Acceptance: `evidence/qa-gates/final-format.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (EXIT_CODE 0). If it changes files, restart the loop.
- [x] [P7-T2] Run final analyzer/lint build. Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
  - Acceptance: `evidence/qa-gates/final-lint.md` with schema fields and `Output Summary:` (0 errors; warnings enumerated). Confirms AC1 source-compatibility (no call-site edits required).
- [x] [P7-T3] Run final nullable type-check build. Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
  - Acceptance: `evidence/qa-gates/final-typecheck.md` with schema fields and `Output Summary:` (EXIT_CODE 0).
- [x] [P7-T4] Verify architecture boundaries: confirm no new `ProjectReference` edges were added and COM interop remains only in `OpenClaw.MailBridge` (the sensitivity-label helper and `EventDto` changes live only in `OpenClaw.MailBridge.Contracts`).
  - Acceptance: `evidence/qa-gates/final-architecture.md` lists the unchanged project-reference graph and confirms COM confinement; no violations.
- [x] [P7-T5] Run final test + coverage. Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Acceptance: `evidence/qa-gates/final-test.md` with schema fields and `Output Summary:` containing numeric post-change line coverage % and branch coverage %, plus passed/failed counts; all tests pass.
- [x] [P7-T6] Produce a coverage delta/threshold verification artifact comparing baseline (P0-T5) to post-change (P7-T5): report baseline coverage, post-change coverage, and changed-code coverage; confirm line >= 85%, branch >= 75%, and no regression on changed lines.
  - Acceptance: `evidence/regression-testing/coverage-delta.md` records baseline %, post-change %, changed-code %, and PASS/FAIL against thresholds. If any threshold or no-regression condition fails, outcome is remediation-required (not PASS). (AC6)
- [x] [P7-T7] Map each acceptance criterion AC1–AC6 to the tests/evidence that satisfy it and confirm all six are covered.
  - Acceptance: `evidence/qa-gates/acceptance-traceability.md` lists AC1–AC6 with the satisfying P#-T# tasks, test files, and evidence paths; all six are marked satisfied.

## Test Plan

- Unit (MailBridge.Tests): `EventSensitivityLabelTests`, `OutlookScannerGraphFieldsTests`, `ResponseShaperEventBodyFullTests`, `CacheRepositoryGraphFieldsTests`.
- Unit (Core.Tests): `CoreCacheRepositoryGraphFieldsTests`, `SchedulingDtoMapper` graph-fields tests.
- Determinism: MSTest + Moq + FluentAssertions; `TimeProvider`/`FakeTimeProvider` where time is involved; no `Thread.Sleep`/`Task.Delay`; no temp files; SQLite tests use in-memory/shared-cache per existing patterns.
- Coverage evidence:
  - Baseline: `evidence/baseline/baseline-test.md`
  - Post-change: `evidence/qa-gates/final-test.md`
  - Comparison: `evidence/regression-testing/coverage-delta.md`
- Thresholds: line >= 85%, branch >= 75% (uniform T1–T4); no regression on changed lines.

## Open Questions / Notes

- OQ-1 (research): exact `OlRecurrenceState` integer values must be confirmed at implementation time in P2-T3; the derivation logic is structured to be insensitive to the exact integers as long as master/non-recurring → null and occurrence/exception → `GlobalAppointmentID`.
- `CoreCacheRepository.cs` is already over the 500-line cap as a pre-existing condition; P5-T6 guards against materially worsening it and prescribes a partial-class split if needed.
- Out of scope (do not address): the `CoreCacheRepository` missing-`response_status` gap (issue #80); the MAPI `PidLidGlobalObjectId` true-iCalUId path; `Location`/`Body` heuristics for `isOnlineMeeting` add-in gaps.
- `SchedulingEventDto` already declares all target fields; this feature wires the previously-hardcoded placeholders in `SchedulingDtoMapper.MapEvent` to the new `EventDto` fields.
</content>
</invoke>
