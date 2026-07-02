# sensitivity-redaction — Spec

- **Issue:** #18 (co-delivers #20)
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T09-30
- **Status:** Draft
- **Version:** 1.0

## Overview

Implement per-item sensitivity redaction at normalization/cache-write time (issue #18) and complete safe-mode field suppression at response-shaping/read time (issue #20) as one coordinated change.

- Items with `Sensitivity=2` (Private) or `Sensitivity=3` (Confidential) are redacted inside `NormalizeMessage`/`NormalizeEvent` before the DTO is written to the SQLite cache. The redacted item remains usable as a busy block per master document §2.4 and the `PRIVATE_BUSY_ONLY` decision class (§9.1): scheduling-mechanical fields are retained; content, identity, and attendee semantics are removed.
- Safe-mode shaping in `ResponseShaper` is extended to suppress the full protected field set and to set `ProtectedFieldsAvailable = false`, and it stops setting `IsRedacted`. The `IsRedacted` flag becomes the exclusive signal of sensitivity redaction; `ProtectedFieldsAvailable = false` becomes the signal that protected fields are absent (whether never available, redacted, or suppressed).
- Target users: the mailbox owner (privacy guarantee) and pipe-RPC consumers of `list_recent_messages`, `get_message`, `list_recent_meeting_requests`, `list_calendar_window`, `get_event`.
- Success: no protected field of a Private/Confidential item is ever cached or served, in either mode; existing non-sensitive behavior is unchanged except for the documented `IsRedacted`/suppression semantics.

## Reconciliation with Current Code (April specs are stale)

The issue bodies (2026-04-11) predate the #71/#72/#73 Graph-field additions. Current DTO shapes in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`:

- `MessageDto` gained: `SenderEmailResolved`, `FromEmailAddress`, `ConversationId`, `MeetingMessageType` (issue #73; populated via `IMessageSource`/`ComMessageSource`).
- `EventDto` gained: `ResponseStatus`, `Categories`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime`, `BodyFull`, `SensitivityLabel` (issues #71/#72; populated in `OutlookScanner.GraphFields.cs` `BuildEventDto`).

Additionally, `ResponseShaper.ShapeEvent` safe mode already nulls `BodyFull` and the three attendee JSON fields (landed with #71/#72), so parts of issue #20's proposal are already implemented; the remaining #20 gap is `ToJson`/`CcJson` (messages), `Organizer` (events), and `ProtectedFieldsAvailable = false` (both).

### Delta table — fields added since the April specs and their treatment

Guiding rule (master §2.4, §9.1): a private item's body, subject, attendee/organizer identity, and content-derived fields are never ingested or served; scheduling-mechanical fields (times, busy status, recurrence flags, ids, sensitivity itself) are retained so the item still works as a busy block.

| New field | DTO | #18 redaction (Sensitivity 2/3) | #20 safe-mode suppression | Rationale |
|---|---|---|---|---|
| `SenderEmailResolved` | Message | null | null | Protected identity; follows the `SenderEmail` treatment (resolved true-SMTP is strictly more sensitive than the raw value). |
| `FromEmailAddress` | Message | null | null | Protected identity (on-behalf-of sender); follows sender-field treatment. |
| `ConversationId` | Message | retain | retain | Opaque correlation/threading id; mechanical, carries no content semantics. Ids are retained per §2.4 busy-block rule. |
| `MeetingMessageType` | Message | retain | retain | Mechanical transaction type (request/cancel/response); parallels retained `ItemKind`/`MessageClass`. |
| `ResponseStatus` | Event | retain | retain | The owner's own response state; scheduling-mechanical, reveals nothing about other participants or content. |
| `Categories` | Event | empty array | empty array | Content-derived cues: master §2.3 uses categories as dependency/classification signals, i.e. semantics. Conservative §2.4 reading: not retained for private items, and suppressed in safe mode. Empty array (never null) preserves the existing `categories` non-null invariant. |
| `IsOrganizer` | Event | retain | retain | Boolean about the owner only; mechanical, no third-party identity. |
| `IsOnlineMeeting` | Event | retain | retain | Mechanical modality flag; not body/subject/attendee semantics. |
| `AllowNewTimeProposals` | Event | retain | retain | Scheduling-mechanical flag. |
| `ICalUId` | Event | retain | retain | Opaque id; retained per §2.4 busy-block rule. |
| `SeriesMasterId` | Event | retain | retain | Opaque recurrence-linkage id; mechanical. |
| `LastModifiedDateTime` | Event | retain | retain | Timestamp; mechanical. |
| `BodyFull` | Event | **null** | null (already implemented) | Content. MUST be redacted for private items; the April #18 spec could not list it because it did not exist. |
| `SensitivityLabel` | Event | retain | retain | The reason signal (`"private"`/`"confidential"` via `EventSensitivityLabel.FromSensitivity`); callers must be able to observe why the item is redacted, matching the retained `Sensitivity` integer. |

## Behavior

### A. Normalization-time sensitivity redaction (#18)

Trigger: `Sensitivity == 2 || Sensitivity == 3` read from the COM item. Values `0`, `1`, `null`, and any out-of-range value (e.g. -1, 4, 99) are non-sensitive: no redaction, no field changes, `IsRedacted = false` as today.

**`NormalizeMessage` (OutlookScanner.cs), full field disposition:**

| Field | Redacted value |
|---|---|
| `Subject` | literal `"Private message"` (both 2 and 3) |
| `SenderName`, `SenderEmail`, `SenderEmailResolved`, `FromEmailAddress`, `ToJson`, `CcJson`, `BodyPreview` | `null` |
| `IsRedacted` | `true` |
| `ProtectedFieldsAvailable` | `false` |
| `BridgeId`, `ItemKind`, `MessageClass`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `ConversationId`, `MeetingMessageType` | retained unchanged |

**`NormalizeEvent`/`BuildEventDto` (OutlookScanner.cs / OutlookScanner.GraphFields.cs), full field disposition:**

| Field | Redacted value |
|---|---|
| `Subject` | literal `"Private appointment"` (both 2 and 3) |
| `Location`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, `BodyPreview`, `BodyFull` | `null` |
| `Categories` | `Array.Empty<string>()` (non-null invariant preserved; serializes as `"[]"`) |
| `IsRedacted` | `true` |
| `ProtectedFieldsAvailable` | `false` |
| `BridgeId`, `GlobalAppointmentId`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `SensitivityLabel`, `ResponseStatus`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime` | retained unchanged |

**Never-ingest ordering requirement.** `Sensitivity` must be read before protected content is read. For a sensitive item, normalization must not:

- invoke `ResponseShaper.ShapePreview` (issue #18 caveat: `NormalizeMessage` currently pre-computes `BodyPreview` at line ~369, and `BuildEventDto` reads `Body` at line ~39 before the DTO is built);
- read the COM `Body`;
- enumerate recipients/attendees (`ComMessageSource.ToRecipients`/`CcRecipients`, `ReadAttendees`);
- run sender SMTP resolution (`ComMessageSource.SenderEmailResolved`/`FromEmailAddress` walk `Sender.AddressEntry`/`PropertyAccessor`).

This satisfies master §2.4 "does not ingest" literally rather than read-then-discard, and avoids wasted COM traffic. Mechanical members (`EntryID`, `MessageClass`, dates, flags, ids, `Sensitivity`) are still read.

**Logging.** Each redaction is logged (Information level, consistent with existing scanner logging) with the bridge id only — never subject, sender, body, or attendee data — recording that the item was treated as busy-only (master §2.4).

### B. Safe-mode shaping suppression (#20)

`ResponseShaper` (verified current state: `ShapeMessage` safe branch at lines 25-31 nulls `BodyPreview`/`SenderName`/`SenderEmail` and sets `IsRedacted = true`; `ShapeEvent` safe branch at lines 57-65 nulls `BodyPreview`/`BodyFull`/three attendee JSON fields and sets `IsRedacted = true`; both enhanced branches force `IsRedacted = false`).

**`ShapeMessage`, safe mode after this change:** nulls `BodyPreview`, `SenderName`, `SenderEmail` (existing), plus `ToJson`, `CcJson` (#20), plus `SenderEmailResolved`, `FromEmailAddress` (new-field delta); sets `ProtectedFieldsAvailable = false`; does not touch `IsRedacted`. All other fields pass through unchanged, including `Subject`, `ConversationId`, `MeetingMessageType`.

**`ShapeEvent`, safe mode after this change:** nulls `BodyPreview`, `BodyFull`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson` (existing), plus `Organizer` (#20); sets `Categories = Array.Empty<string>()` (new-field delta); sets `ProtectedFieldsAvailable = false`; does not touch `IsRedacted`. `Location` is retained in safe mode — issue #20's constraint flags this for confirmation, and the decision is: safe mode retains `Location` (matching #20's suppression table); only sensitivity redaction removes `Location`. All mechanical fields pass through unchanged.

**Enhanced mode (both shapers):** all fields pass through; `BodyPreview` is still sanitized/truncated via `ShapePreview`; `BodyFull` remains verbatim; `ProtectedFieldsAvailable` is not forced; `IsRedacted` is not touched (the current `IsRedacted = false` assignment in both enhanced branches is removed — see composition invariants).

Shaping operates at read time only; stored rows are not modified. `ProtectedFieldsAvailable` read directly from the cache will not reflect the safe-mode override (documented #20 constraint).

### C. Composition invariants (#18 x #20)

1. A `Sensitivity` 2/3 item served in **enhanced** mode is still redacted: redaction happened at cache-write, and enhanced shaping must preserve `IsRedacted = true`. This requires removing the current `IsRedacted = false` assignment from both enhanced branches — a load-bearing behavioral fix, since today enhanced shaping would falsify the flag on a redacted item.
2. A redacted DTO passing through **safe**-mode shaping keeps `IsRedacted = true` (fields are already null; re-nulling is a no-op and must not throw).
3. Safe mode never sets `IsRedacted = true`. The flag is written only by normalization-time redaction.
4. `ProtectedFieldsAvailable = false` on both paths: written by redaction, forced by safe-mode shaping.
5. Boundary values `Sensitivity` 0, 1, `null`, out-of-range: untouched by redaction.

### Error handling

- Redaction decision is a pure integer comparison; no new exception paths. Fail-soft COM reads are unchanged for non-sensitive items.
- Shaping already-null fields must not throw (existing record `with` semantics guarantee this; covered by tests).

## Inputs / Outputs

- Inputs: `Sensitivity` COM member (already read); `BridgeSettings.Mode` (`safe`/`enhanced`, existing). No new CLI flags, env vars, or config keys.
- Outputs: redacted `MessageDto`/`EventDto` rows in the SQLite cache; shaped RPC responses; redaction log lines (bridge id only).
- Backward compatibility: two deliberate breaking behavioral changes for RPC consumers — (1) `is_redacted` no longer signals safe mode; use `protected_fields_available: false`; (2) safe-mode responses now null `to_json`, `cc_json`, `sender_email_resolved`, `from_email_address`, `organizer` and empty `categories`. Wire shape (field names/types) is unchanged.

## API / CLI Surface

No new methods. Affected existing methods: `list_recent_messages`, `get_message`, `list_recent_meeting_requests`, `list_calendar_window`, `get_event`.

Example — `get_message` for a Private item (identical in safe and enhanced modes):

```json
{ "subject": "Private message", "sender_name": null, "sender_email": null,
  "sender_email_resolved": null, "from_email_address": null, "to_json": null,
  "cc_json": null, "body_preview": null, "sensitivity": 2,
  "is_redacted": true, "protected_fields_available": false }
```

Example — `get_event` for a Confidential item (both modes):

```json
{ "subject": "Private appointment", "location": null, "organizer": null,
  "required_attendees_json": null, "optional_attendees_json": null,
  "resources_json": null, "body_preview": null, "body_full": null,
  "categories": [], "sensitivity": 3, "sensitivity_label": "confidential",
  "is_redacted": true, "protected_fields_available": false }
```

Example — safe-mode message for a non-sensitive item:

```json
{ "subject": "Quarterly report", "sender_name": null, "sender_email": null,
  "to_json": null, "cc_json": null, "body_preview": null,
  "is_redacted": false, "protected_fields_available": false }
```

## Data & State

- **Cache round-trip:** redacted values are written through `UpsertMessageAsync`/`UpsertEventAsync` (`CacheRepository`), so all subsequent reads serve redacted data regardless of mode. Verified: `is_redacted` and `protected_fields_available` columns exist and round-trip; all #71/#72/#73 columns (`sender_email_resolved`, `categories_json`, `body_full`, `sensitivity_label`, etc.) exist in `CacheRepository.Schema.cs`.
- **No schema changes** (verified): the change writes different values into existing columns; no new columns, tables, or migrations.
- **Deployment note (issue #18):** previously cached unredacted Private/Confidential rows are corrected only when a subsequent scan re-upserts them. A cache flush or forced re-scan is recommended after deployment; until then, stale rows may serve unredacted protected fields in enhanced mode.
- `Categories` redaction/suppression uses an empty array, matching the reader invariant in `CacheRepository.Readers.cs` (`GetCategories` never returns null) and the scanner invariant (`SplitCategories` never returns null).

## Constraints & Risks

- **Breaking behavioral changes** enumerated in Inputs/Outputs; called out for consumers in the change description per repo policy.
- **500-line cap (measured 2026-07-02):** `OutlookScanner.cs` = 465 lines (near cap; its own doc comment says it must not grow), `OutlookScanner.GraphFields.cs` = 117, `ResponseShaper.cs` = 77. Redaction logic must therefore live in a **new partial-class file** (e.g. `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs`) rather than in `OutlookScanner.cs`. `ResponseShaper.cs` has headroom for the shaping changes.
- **COM confinement:** all COM reads remain in `OpenClaw.MailBridge` (`OutlookScanner`, `ComMessageSource`); redaction is a pure transform over already-read integers and constructed DTOs. No contracts-project change beyond none-required (DTO shapes unchanged).
- **Testing constraints:** MSTest + FluentAssertions (repo test framework; note `.claude/rules/csharp.md` says xUnit, but the existing suite is MSTest — follow the suite). No temporary files in tests; cache tests follow the existing in-memory/SQLite patterns in `CacheRepository*Tests`. Deterministic tests only.
- **Performance:** skipping body/recipient/sender-resolution COM reads for sensitive items reduces per-item COM traffic; no other latency impact.

## Implementation Strategy

- **New:** `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` — partial-class file containing the sensitivity check (`IsSensitive(int? sensitivity)` — true only for 2 and 3) and message/event redaction application; pure, no COM access.
- **Update:** `OutlookScanner.cs` `NormalizeMessage` — read `Sensitivity` before protected reads; for sensitive items skip `ShapePreview`, `ComMessageSource` recipient/sender resolution, and build the redacted DTO directly.
- **Update:** `OutlookScanner.GraphFields.cs` `BuildEventDto` — read `Sensitivity` before `Body`/attendees; branch to redacted construction for sensitive items (or delegate to the new partial).
- **Update:** `ResponseShaper.cs` — safe-mode branches gain the additional nulls, `Categories` empty array, and `ProtectedFieldsAvailable = false`; both branches stop assigning `IsRedacted`.
- **Update tests** (see below) and add new MSTest classes, e.g. `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionTests.cs`, `ResponseShaperSafeModeSuppressionTests.cs`, and a cache round-trip test following the `CacheRepository*Tests` pattern.
- **Logging:** one `LogInformation` per redacted item in the scanner (bridge id only).
- No dependency changes. No feature flag; the behavior is unconditional. Rollback path is revert.

### Existing tests whose behavior deliberately changes

The `IsRedacted`-in-safe-mode removal breaks these current assertions (enumerated from the suite, 2026-07-02):

| Test | File | Change |
|---|---|---|
| `ShapeMessage_in_safe_mode_should_redact_sender_fields_and_clear_preview` | `ResponseShaperTests.cs:25` | `IsRedacted.Should().BeTrue()` becomes false-preserving assertion; rename to reflect suppression semantics; add `ProtectedFieldsAvailable`/`ToJson`/`CcJson` assertions. |
| `ShapeEvent_in_safe_mode_should_clear_preview_and_redact` | `ResponseShaperTests.cs:56` | Same `IsRedacted` change; add `Organizer`/`ProtectedFieldsAvailable` assertions. |
| `ShapeEvent_in_safe_mode_should_null_body_full_and_set_redacted` | `ResponseShaperEventBodyFullTests.cs:55` | `IsRedacted.Should().BeTrue()` removed/inverted; rename. |
| `ShapeEvent_in_safe_mode_should_null_all_three_attendee_fields` | `ResponseShaperEventBodyFullTests.cs:81` | Same `IsRedacted` change. |
| Enhanced-mode tests asserting `IsRedacted.Should().BeFalse()` | `ResponseShaperTests.cs:44,69`; `ResponseShaperEventBodyFullTests.cs:98,124` | Still pass (input flag is false and now passes through); semantics shift from "shaper resets to false" to "shaper preserves false" — extend with preserve-true cases (invariant C1). |
| `Safe_mode_message_shaping_should_suppress_body_preview_sender_name_and_sender_email` | `MailBridgeTests.cs:38` | Unaffected regression guard; must continue to pass. |

`BridgeContractsCoverageTests`, `CacheRepository*Tests`, `OutlookScanner*Tests` construct DTOs with explicit `IsRedacted: false` values and are unaffected.

## Acceptance Criteria

### Group A — Normalization-time sensitivity redaction (#18)

- [x] `NormalizeMessage` with `Sensitivity` 2 or 3 produces a DTO with `Subject = "Private message"`; `SenderName`, `SenderEmail`, `SenderEmailResolved`, `FromEmailAddress`, `ToJson`, `CcJson`, and `BodyPreview` all null; `IsRedacted = true`; `ProtectedFieldsAvailable = false`.
- [x] `NormalizeMessage` retains `BridgeId`, `ItemKind`, `MessageClass`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `ConversationId`, and `MeetingMessageType` unchanged on redacted messages.
- [x] `NormalizeEvent` with `Sensitivity` 2 or 3 produces a DTO with `Subject = "Private appointment"`; `Location`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, `BodyPreview`, and `BodyFull` all null; `Categories` an empty array; `IsRedacted = true`; `ProtectedFieldsAvailable = false`.
- [x] `NormalizeEvent` retains `BridgeId`, `GlobalAppointmentId`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `SensitivityLabel`, `ResponseStatus`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, and `LastModifiedDateTime` unchanged on redacted events.
- [x] For a sensitive item (Sensitivity 2/3), normalization does not invoke `ResponseShaper.ShapePreview` and does not read or resolve protected COM content (body, sender SMTP resolution, recipient/attendee enumeration) that would be discarded by redaction.
- [x] Redaction is applied at cache-write time: a `Sensitivity=2` message upserted via `UpsertMessageAsync` and read back via `GetMessageAsync` (and the event equivalent via `UpsertEventAsync`/`GetEventAsync`) returns redacted values without any `ResponseShaper` involvement.
- [x] Each redaction is logged with the bridge id only; no protected content appears in log output (master §2.4 busy-only logging).

### Group B — Safe-mode shaping suppression (#20)

- [x] `ResponseShaper.ShapeMessage` in safe mode nulls `ToJson`, `CcJson`, `SenderEmailResolved`, and `FromEmailAddress`, and sets `ProtectedFieldsAvailable = false`, in addition to the existing `BodyPreview`/`SenderName`/`SenderEmail` suppression (no regression).
- [x] `ResponseShaper.ShapeMessage` in safe mode retains all other message fields unchanged (`BridgeId`, `ItemKind`, `Subject`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `MessageClass`, `ConversationId`, `MeetingMessageType`).
- [x] `ResponseShaper.ShapeEvent` in safe mode nulls `Organizer` and sets `ProtectedFieldsAvailable = false`, and sets `Categories` to an empty array, in addition to the existing `BodyPreview`/`BodyFull`/`RequiredAttendeesJson`/`OptionalAttendeesJson`/`ResourcesJson` suppression (no regression).
- [x] `ResponseShaper.ShapeEvent` in safe mode retains `Location` and all mechanical fields unchanged (`BridgeId`, `GlobalAppointmentId`, `Subject`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `SensitivityLabel`, `ResponseStatus`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime`).
- [x] Enhanced-mode shaping does not null any suppressed field and does not force `ProtectedFieldsAvailable = false`; original DTO values pass through (with `BodyPreview` still sanitized/truncated and `BodyFull` returned verbatim).
- [x] A `MessageDto` or `EventDto` whose protected fields are already null is shaped without error in both modes.

### Group C — Composition invariants (#18 x #20)

- [x] A `Sensitivity=2` (or 3) item served in enhanced mode is still redacted: the cache-written redaction survives enhanced shaping and `IsRedacted` remains `true`.
- [x] A redacted DTO passing through safe-mode shaping keeps `IsRedacted = true`.
- [x] Neither `ShapeMessage` nor `ShapeEvent` mutates `IsRedacted` in either mode: safe mode never sets it `true`; enhanced mode never resets a `true` value to `false`. The flag is set only by sensitivity redaction at normalization time.
- [x] `ProtectedFieldsAvailable = false` holds on both paths: set by redaction at normalization time and forced by safe-mode shaping at read time.
- [x] Boundary values are untouched by redaction: `Sensitivity` 0, 1, `null`, and out-of-range values (e.g. -1, 4, 99) produce unredacted DTOs with all original fields intact and `IsRedacted = false`.

### Toolchain and coverage

- [x] Full toolchain passes in a single pass (format, lint, type check, architecture, unit tests, contract checks, integration tests); line coverage >= 85%, branch coverage >= 75%, and changed lines are covered with no regression.
  - Re-verified 2026-07-02T10-11 (remediation cycle 1): OutlookScanner.Redaction.cs branch coverage >= 75% per evidence/qa-gates/coverage-remediation-verification.2026-07-02T10-11.md.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable), including the deliberate `IsRedacted` assertion updates enumerated above
- [ ] Edge cases and error handling covered by tests (boundary sensitivity values, already-null fields, cache round-trip)
- [ ] Docs updated (README, docs/features/active/... links); deployment note about stale cached private items recorded in the change description
- [ ] Telemetry/logging added or updated (redaction log line, bridge id only)
- [ ] Toolchain pass completed (format → lint → type-check → architecture → test)
