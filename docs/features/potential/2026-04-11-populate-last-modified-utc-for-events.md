---
title: "populate-last-modified-utc-for-events - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-44"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# populate-last-modified-utc-for-events (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The `last_modified_utc` column exists in the SQLite event schema and is part of each upsert statement in `CacheRepository.cs`, but its value is unconditionally written as `DBNull.Value` at [CacheRepository.cs:421](src/OpenClaw.MailBridge/CacheRepository.cs#L421). The `EventDto` record in `BridgeContracts.cs` does not include a `LastModifiedUtc` field, so there is no carrier for the value between the scanner and the repository. As a result, no client reading the event response can determine when an appointment was last modified in Outlook — the field is structurally present but permanently empty.

This is deviation #11 (Low) in the design audit. Outlook appointment items expose `LastModificationTime` as a standard MAPI property, and the COM helper `GetOptionalDateTimeOffset` already has the pattern to read it safely. The gap is entirely in the data wiring, not in any Outlook capability.

## Proposed Behavior

Four targeted changes wire `LastModificationTime` through the stack end-to-end:

**1. Add `LastModifiedUtc` to `EventDto` (`BridgeContracts.cs`)**

Add `DateTimeOffset? LastModifiedUtc` as a nullable field on the `EventDto` record. It is nullable because `LastModificationTime` is technically optional on COM objects and because the appointment may have been cached before this change was deployed (the column will be NULL for those rows until the next scan cycle).

**2. Read `LastModificationTime` in `NormalizeEvent` (`OutlookScanner.cs`)**

In `NormalizeEvent`, read the property using the existing helper:

```csharp
OutlookComHelpers.GetOptionalDateTimeOffset(item, "LastModificationTime")
```

Pass the result as `LastModifiedUtc` to the `EventDto` constructor. The existing `GetOptionalDateTimeOffset` already handles COM exceptions by returning `null`, so no additional guard is needed.

**3. Write `last_modified_utc` in the upsert (`CacheRepository.cs`)**

Replace the hardcoded `DBNull.Value` at [CacheRepository.cs:421](src/OpenClaw.MailBridge/CacheRepository.cs#L421) with the existing `ToDbValue` helper:

```csharp
cmd.Parameters.AddWithValue("$last_modified_utc", ToDbValue(evt.LastModifiedUtc));
```

`ToDbValue(DateTimeOffset?)` already exists in `CacheRepository` and returns `DBNull.Value` for null or `UtcDateTime.ToString("O")` for a populated value.

**4. Read `last_modified_utc` in `ReadEvent` (`CacheRepository.cs`)**

In the `ReadEvent` method, add `GetDateTimeOffset(reader, "last_modified_utc")` to the `EventDto` constructor call, in the correct positional slot matching the new field in the record definition. This makes the round-trip complete: the column is written on upsert and read back on query.

## Acceptance Criteria (early draft)

- [ ] `EventDto` has a `DateTimeOffset? LastModifiedUtc` field.
- [ ] `NormalizeEvent` reads `LastModificationTime` via `GetOptionalDateTimeOffset` and assigns the result to `LastModifiedUtc` in the constructed `EventDto`.
- [ ] The `CacheRepository` upsert passes `evt.LastModifiedUtc` through `ToDbValue` rather than a hardcoded `DBNull.Value`.
- [ ] The `CacheRepository` `ReadEvent` reader passes `GetDateTimeOffset(reader, "last_modified_utc")` to the `EventDto` constructor.
- [ ] When `LastModificationTime` is available from Outlook, `last_modified_utc` is a non-null ISO-8601 UTC string in the SQLite row after a scan cycle.
- [ ] When `LastModificationTime` is not available (COM returns null), `last_modified_utc` is written as `NULL` in SQLite without error.
- [ ] Existing event upsert and query unit tests continue to pass without modification (the new field is nullable and does not break existing test data that omits it).
- [ ] The `last_modified_utc` value is included in the `EventDto` returned by both `list-calendar-window` and `get-event` responses.

## Constraints & Risks

- `EventDto` is a positional record. Adding `LastModifiedUtc` changes the constructor signature, which requires updating every call site that constructs an `EventDto` directly — including test doubles and fake data builders. The conversation history confirms that `MailBridgeRuntimeTestDoubles.cs` constructs event objects; that file will need to be updated to supply a `null` or a concrete value for the new field.
- The Outlook `LastModificationTime` property is a `DateTime` in local time on some Outlook versions and a UTC value on others. `GetOptionalDateTimeOffset` applies a UTC parsing pass; the returned value should be treated as UTC and stored accordingly. If local-time values are returned by Outlook, the stored timestamp will be incorrect by the system's UTC offset. This risk is the same as for `StartUTC`/`EndUTC` fallback to `Start`/`End` in the existing code.
- This change is purely additive at the contract level and backward-compatible with existing clients. Clients that do not reference `last_modified_utc` are unaffected. Clients that expected the field to always be `null` will receive a non-null value after the next scan cycle; this is a data correction, not a breaking change.
- No schema migration is required. The `last_modified_utc` column already exists in the SQLite schema (confirmed by the existing parameter in the upsert). Existing rows will have `NULL` in that column until they are re-scanned and upserted.

## Test Conditions to Consider

- [ ] Unit test: `EventDto` constructor accepts a non-null `DateTimeOffset?` for `LastModifiedUtc` and stores it correctly.
- [ ] Unit test: `CacheRepository.UpsertEventAsync` writes a non-null ISO-8601 UTC string for `last_modified_utc` when `EventDto.LastModifiedUtc` is populated.
- [ ] Unit test: `CacheRepository.UpsertEventAsync` writes `NULL` for `last_modified_utc` when `EventDto.LastModifiedUtc` is `null`.
- [ ] Unit test: `CacheRepository` round-trip — upsert an event with a known `LastModifiedUtc`, query it back, and assert the returned `EventDto.LastModifiedUtc` equals the original value.
- [ ] Unit test: `NormalizeEvent` with a fake COM object that exposes `LastModificationTime` produces an `EventDto` with the expected `LastModifiedUtc` value.
- [ ] Existing event tests in `MailBridgeRuntimeTestDoubles.cs` updated to include the new positional field with `null` or a test-appropriate value.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/populate-last-modified-utc-for-events/` folder from the template

