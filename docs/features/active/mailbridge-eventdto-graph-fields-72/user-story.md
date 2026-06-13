# `mailbridge-eventdto-graph-fields` — User Story

- Issue: #72
- Owner: drmoisan
- Status: Approved
- Last Updated: 2026-06-12T22-20

## Story Statement

- As the downstream agent triage pipeline (`SchedulingDtoMapper` → `DependencyScorer`), I want `EventDto` to carry the nine Graph-shaped calendar fields populated from Outlook COM, so that scoring and triage operate on real values instead of hardcoded placeholders.
- As a maintainer of the bridge, I want the new fields appended to `EventDto` source-compatibly and round-tripped through both SQLite caches, so that the change adds capability without breaking existing callers or persistence.

## Problem / Why

`EventDto` does not carry several fields that the Graph-shaped consumer model expects (`categories`, `isOrganizer`, `isOnlineMeeting`, `allowNewTimeProposals`, `iCalUId`, `seriesMasterId`, `lastModifiedDateTime`, full body, sensitivity label). `SchedulingDtoMapper.MapEvent` currently substitutes placeholder values (`Array.Empty<string>()`, `false`, `null`) for the fields it cannot source. As a result, triage signals that depend on these fields — for example `isOnlineMeeting` contributing a dependency point and `categories` matching protected categories — cannot be computed accurately. Populating the fields from their Outlook COM analogs is the prerequisite for accurate triage.

## Personas & Scenarios

- Persona: Triage pipeline consumer
  - who the user is: The agent runtime code that maps `EventDto` to `SchedulingEventDto` and scores meeting dependencies.
  - what they care about: Accurate, populated calendar fields; deterministic mapping; no placeholder data.
  - their constraints: Reads only cached, contract-shaped data over the HTTP boundary; does not call Outlook directly.
  - their goals and frustrations: Goal is correct dependency scoring; current frustration is that several scoring inputs are hardcoded placeholders.
  - their context and motivations: Operates downstream of the COM bridge and depends on the bridge to supply real field values.
- Scenario: Scanning a recurring online meeting
  - who is acting? The bridge scanner (`OutlookScanner.NormalizeEvent`) during a calendar scan.
  - what triggered the action? A scheduled or requested calendar scan reaches a recurring online meeting appointment.
  - what steps do they take? The scanner reads the COM analogs, derives `isOrganizer` from `ResponseStatus`, derives `seriesMasterId` from `RecurrenceState`, maps `sensitivityLabel` from the `Sensitivity` int, reuses `GlobalAppointmentID` for `iCalUId`, and reads `IsOnlineMeeting`, `AllowNewTimeProposal`, `LastModificationTime`, `Categories`, and `Body`.
  - what obstacles or decisions occur? In SAFE mode, `bodyFull` must be nulled with `BodyPreview`. `isOnlineMeeting` may report `false` for some add-in meetings (MEDIUM confidence).
  - what outcome do they expect? The resulting `EventDto` has non-null `iCalUId`, `isOnlineMeeting=true`, and the correct `sensitivityLabel`, and persists/reads back identically from both caches.

## Acceptance Criteria

- [x] `EventDto` exposes all nine new fields with the specified types and remains source-compatible (all existing in-repo callers compile without modification to their call sites).
- [x] `OutlookScanner.NormalizeEvent` populates all nine fields from the specified COM analogs/derivations.
- [x] `ResponseShaper.ShapeEvent` nulls `bodyFull` in safe mode (redaction parity with `BodyPreview`); enhanced mode returns the full untruncated `bodyFull`.
- [x] Both SQLite caches (`CacheRepository`, `CoreCacheRepository`) round-trip all nine new fields (write then read returns the same values), with idempotent schema migrations.
- [x] A scan of a recurring online meeting yields non-null `iCalUId`, `isOnlineMeeting=true`, and the correct `sensitivityLabel`.
- [x] Existing contract tests pass; new unit tests cover the new fields, the safe/enhanced shaping of `bodyFull`, and the cache round-trip. Coverage thresholds hold: line >= 85%, branch >= 75% (T2).

## Non-Goals

- The pre-existing `CoreCacheRepository` missing-`response_status`-column gap is excluded from #72 and tracked separately as issue #80.
- The MAPI named-property (`PidLidGlobalObjectId`) path for a true RFC 5545 iCalendar UID is excluded; `iCalUId` reuses `GlobalAppointmentID`.
- `Location`/`Body` heuristics to compensate for `isOnlineMeeting` add-in detection gaps are excluded.
