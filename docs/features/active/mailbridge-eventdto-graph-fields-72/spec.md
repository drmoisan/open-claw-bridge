# mailbridge-eventdto-graph-fields — Spec

- **Issue:** #72
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-12T22-20
- **Status:** Approved
- **Version:** 1.0

## Overview

Brief summary of the behavior and scope.
- Target users/personas and primary use cases: The downstream agent triage pipeline (`SchedulingDtoMapper` → `DependencyScorer`) consumes calendar events normalized from Outlook. It currently receives placeholder values for several Graph-shaped fields (`Categories`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `LastModifiedDateTime`) because `EventDto` does not carry them. This feature adds nine Graph-shaped fields to `EventDto` and populates them from Outlook COM analogs so that triage and scoring operate on real data.
- Success metrics or expected impact: Nine fields are populated from the COM bridge for scanned calendar items; `SchedulingDtoMapper` can wire real values instead of placeholders; persistence round-trips the new fields in both SQLite caches without schema-migration failures.

The authoritative source for COM-analog mappings, propagation surface, persistence impact, and source-compatibility is the research artifact `artifacts/research/2026-06-12-issue-72-eventdto-com-analogs-research.md`.

## Behavior

Describe how the feature should behave end-to-end.
- Main user flow (happy path): `OutlookScanner.NormalizeEvent` reads an Outlook `AppointmentItem`, derives the nine new fields from their COM analogs, and constructs an `EventDto` carrying them. `ResponseShaper.ShapeEvent` applies mode-dependent redaction. The shaped DTO is persisted to the bridge SQLite cache (`CacheRepository`) and, downstream over the HTTP boundary, to the Core SQLite cache (`CoreCacheRepository`). Reads from either cache return the same field values that were written.
- Alternate/edge flows:
  - Recurring online meeting: a scan yields non-null `iCalUId`, `isOnlineMeeting=true`, and the correct `sensitivityLabel`. For an occurrence or exception, `seriesMasterId` is non-null; for the series master and non-recurring items, `seriesMasterId` is null.
  - SAFE mode (non-enhanced): `bodyFull` is nulled together with `BodyPreview` and `IsRedacted=true`.
  - ENHANCED mode: `bodyFull` is the raw full COM `Body` text, untruncated and with structure preserved.
- Error handling and recovery behavior: COM read failures fall back to the existing optional-accessor contracts — `GetOptionalBool` returns `false`, `GetOptionalString` and `GetOptionalInt` and `GetOptionalDateTimeOffset` return null. `categories` never returns null; it returns a zero-length `string[]` when `Categories` is null or empty.

## Inputs / Outputs

- Inputs (CLI flags, files, env vars): No new CLI flags or environment variables. Inputs are the Outlook `AppointmentItem` COM properties listed in the COM-analog table below.
- Outputs (artifacts, logs, telemetry): The nine new fields on `EventDto`, persisted in both SQLite caches and surfaced through the existing pipe/HTTP contracts. No new telemetry is introduced by this feature.
- Config keys and defaults: No new config keys. `bodyFull` shaping is governed by the existing `BridgeSettings.Mode` value (`enhanced` vs other).
- Versioning or backward-compatibility constraints: `EventDto` remains source-compatible. New parameters are appended to the positional record after the current last parameter (`ResponseStatus = null`), each optional with a default, so all existing positional callers continue to compile.

## API / CLI Surface

List commands, flags, request/response shapes, and examples.
- Example invocations with expected outputs (concise): No CLI surface change. The contract change is the addition of nine fields to the `EventDto` record.
- Contracts and validation rules: The new `EventDto` parameters, appended after `ResponseStatus = null`:

```csharp
string[]? Categories = null,
bool IsOrganizer = false,
bool IsOnlineMeeting = false,
bool AllowNewTimeProposals = false,
string? ICalUId = null,
string? SeriesMasterId = null,
DateTimeOffset? LastModifiedDateTime = null,
string? BodyFull = null,
string? SensitivityLabel = null
```

### COM-Analog Mapping (authoritative; confidence per research)

| Field | Type | COM analog / derivation | Confidence | Fallback |
|---|---|---|---|---|
| `categories` | `string[]` | `AppointmentItem.Categories` (comma-space delimited string), split on `", "` with per-token trim | HIGH | Empty `string[]` (never null) |
| `isOrganizer` | `bool` | Derived: `ResponseStatus == 1` (olResponseOrganized); no address-book lookup | HIGH | `false` |
| `isOnlineMeeting` | `bool` | `AppointmentItem.IsOnlineMeeting` | MEDIUM | `false` |
| `allowNewTimeProposals` | `bool` | `AppointmentItem.AllowNewTimeProposal` (COM property is singular) | HIGH | `false` |
| `iCalUId` | `string?` | Reuse `GlobalAppointmentID` (already on the DTO); MAPI `PidLidGlobalObjectId` path rejected | HIGH (no native iCalUId); MEDIUM (GlobalAppointmentID relationship) | null |
| `seriesMasterId` | `string?` | From `RecurrenceState` (OlRecurrenceState): null for not-recurring and master; `GlobalAppointmentID` for occurrence/exception | MEDIUM | null |
| `lastModifiedDateTime` | `DateTimeOffset?` | `AppointmentItem.LastModificationTime` | HIGH | null |
| `bodyFull` | `string?` | Raw full `AppointmentItem.Body`, untruncated, not normalized | HIGH | null |
| `sensitivityLabel` | `string?` (normal/personal/private/confidential) | Derived from the existing `Sensitivity` int via `SchedulingDtoMapper.MapSensitivity` mapping (0=normal,1=personal,2=private,3=confidential) | HIGH | null for unrecognized values |

## Data & State

Data flow, storage, or state changes introduced by this feature.
- Data transformations and invariants:
  - `categories` is always a non-null `string[]`; empty when the source is null/empty.
  - `bodyFull` is the raw COM `Body` text. COM `Body` is plain text (HTML is in `HTMLBody`), so tag-stripping is not relevant. `bodyFull` is NOT passed through `BodySanitizer.NormalizePreview`, which truncates and collapses whitespace.
  - `sensitivityLabel` is derived from the existing `Sensitivity` int and reuses the `SchedulingDtoMapper.MapSensitivity` switch logic; it is not an independent COM read.
  - `iCalUId` equals `GlobalAppointmentID`. This is an Outlook-specific opaque identifier, not the RFC 5545 UID. Downstream consumers treating `iCalUId` as a true iCalendar UID may not achieve cross-system interoperability. This limitation is accepted for #72.
  - `seriesMasterId` derivation depends on the OlRecurrenceState integer mapping (NotRecurring=0, Master=1, Occurrence=2, Exception=3). The exact integer assignment for master/occurrence/exception is to be confirmed at implementation time (research OQ-1); the logic must return null for the master and non-recurring cases and `GlobalAppointmentID` for occurrence/exception cases.
- Caching or persistence details: New columns are required in BOTH SQLite caches.
  - `src/OpenClaw.MailBridge/CacheRepository.cs`: add eight new columns via the existing idempotent `MigrateEventsSchemaAsync` ALTER-TABLE pattern (`categories_json` TEXT NULL, `is_organizer`, `is_online_meeting`, `allow_new_time_proposals` INTEGER NOT NULL DEFAULT 0, `ical_uid`, `series_master_id`, `body_full`, `sensitivity_label` TEXT NULL). `lastModifiedDateTime` reuses the already-present-but-unwired `last_modified_utc` column; no migration is needed for it, but the write must be wired.
  - `src/OpenClaw.Core/CoreCacheRepository.cs`: add the mirroring columns via a new idempotent migration helper modeled on `MigrateEventsSchemaAsync`. The `ReadEvent` materializer must be extended to read the new columns.
  - `categories` is serialized as a JSON array column (`categories_json`), consistent with the existing `RequiredAttendeesJson`/`ResourcesJson` columns.
- Migration or backfill requirements (if any): Additive idempotent schema migrations only. No backfill of existing rows is required; pre-existing rows default to the optional defaults on read.

## Constraints & Risks

Performance, compatibility, security, rollout constraints.
- Limits (latency/throughput/memory) and acceptable trade-offs: COM `Body` is read once already in `NormalizeEvent`; the implementation reads it once into a local and uses it for both `BodyPreview` and `bodyFull` to avoid a redundant COM read.
- Security/privacy considerations: `bodyFull` carries full appointment body text. `ResponseShaper.ShapeEvent` MUST null `bodyFull` in SAFE mode together with `BodyPreview`. Failing to do so is a redaction regression that leaks body content in SAFE mode.
- Operational/rollout risks and mitigations:
  - `isOnlineMeeting` (MEDIUM confidence) returns `false` for some third-party add-in meetings (for example Teams meetings inserted via the Teams Outlook add-in may report `false` despite containing a join URL). No `Location`/`Body` heuristic is used; the known gap is accepted. Downstream consumers can layer heuristics if needed.
  - The two SQLite `ReadEvent` materializers construct `EventDto` positionally; after appending optional parameters they continue to compile but will not populate the new fields until explicitly extended to read the new columns. Both reader methods must be updated.

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` — append nine optional parameters to `EventDto`.
  - `src/OpenClaw.MailBridge/OutlookScanner.cs` `NormalizeEvent` — populate the nine fields from the COM analogs/derivations.
  - `src/OpenClaw.MailBridge/ResponseShaper.cs` `ShapeEvent` — null `bodyFull` in SAFE mode; return full untruncated `bodyFull` in ENHANCED mode.
  - `src/OpenClaw.MailBridge/CacheRepository.cs` and `CacheRepository.Readers.cs` — extend the DDL, `MigrateEventsSchemaAsync`, `AddEventParameters` (including wiring `last_modified_utc`), and `ReadEvent`.
  - `src/OpenClaw.Core/CoreCacheRepository.cs` — extend the DDL, add a mirroring idempotent migration helper, extend `AddEventParameters` and `ReadEvent`.
  - `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` — wire the nine new fields through `MapEvent` in place of the current placeholder values; reuse `MapSensitivity` for `sensitivityLabel`.
- New classes/functions/commands to add or update: No new public classes are required. A private helper for the `categories` string split may be added in `NormalizeEvent`; a `GetOptionalStringArray` helper in `OutlookComHelpers` is optional and not required.
- Dependency changes (new/removed packages) and rationale: None.
- Logging/telemetry additions and locations: None.
- Rollout plan (feature flags, staged deploys, fallback path): No feature flag. The change is additive and source-compatible. The `bodyFull` shaping is governed by the existing `BridgeSettings.Mode` value.

### Locked Design Decisions (authoritative — do not reopen)

- **Source compatibility:** New parameters are appended after `ResponseStatus = null`, each optional with a default.
- **`bodyFull` shaping/redaction:** ENHANCED = raw full COM `Body`, not passed through `BodySanitizer.NormalizePreview`. SAFE = nulled with `BodyPreview`, `IsRedacted=true`. `ResponseShaper.ShapeEvent` must null `bodyFull` in SAFE mode.
- **`sensitivityLabel`:** Field on `EventDto` (enum string), derived from the existing `Sensitivity` int using 0=normal, 1=personal, 2=private, 3=confidential (reuse `SchedulingDtoMapper.MapSensitivity`).
- **`categories` persistence:** JSON array column in both SQLite caches.
- **`isOrganizer` derivation:** `ResponseStatus == 1` (olResponseOrganized); no address-book lookup.
- **`iCalUId` derivation:** Reuse `GlobalAppointmentID`; MAPI `PidLidGlobalObjectId` path rejected as disproportionate.
- **`seriesMasterId` derivation:** From `RecurrenceState`; null for non-recurring and master items.
- **`isOnlineMeeting`:** COM `IsOnlineMeeting` (MEDIUM confidence; documented add-in limitation).
- **`allowNewTimeProposals`:** COM `AllowNewTimeProposal` (singular).
- **`lastModifiedDateTime`:** COM `LastModificationTime`.
- **Persistence:** New columns in both `CacheRepository` (idempotent ALTER-TABLE) and `CoreCacheRepository` (mirroring idempotent helper); bridge `lastModifiedDateTime` reuses `last_modified_utc`.

### Non-Goals / Constraints

- The pre-existing `CoreCacheRepository` missing-`response_status`-column gap is OUT of scope for #72 and is tracked separately as issue #80.
- The MAPI named-property (`PidLidGlobalObjectId`) path for a true iCalendar UID is out of scope.
- `Location`/`Body` heuristics to compensate for `isOnlineMeeting` add-in gaps are out of scope.

## Definition of Done

- [x] Acceptance criteria documented and mapped to tests or demos
- [x] Behavior matches acceptance criteria in all documented environments
- [x] Tests updated/added (unit/integration as applicable)
- [x] Edge cases and error handling covered by tests
- [x] Docs updated (README, docs/features/active/... links)
- [x] Telemetry/logging added or updated (if applicable)
- [x] Toolchain pass completed (format → lint → type-check → test)
