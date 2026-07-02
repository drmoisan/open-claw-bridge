# sensitivity-redaction — Issue

- Issue: #18
- Type: feature
- Work Mode: full-feature
- Co-delivers: #20

## Problem Statement

Two coordinated privacy defects exist in the mail bridge, one at cache-write time and one at response-read time. They must be fixed together because the corrected semantics of the `IsRedacted` flag span both layers.

**Issue #18 (normalization-time sensitivity redaction).** The `Sensitivity` integer is read from Outlook items and stored in the SQLite cache for both messages and events, but its value is never inspected. Items flagged Private (`Sensitivity=2`) or Confidential (`Sensitivity=3`) are cached and served with their full field set — subject, sender identity, recipient lists, body preview and full body, location, organizer, attendees, and categories — exposing information Outlook explicitly marks as restricted. Master document §2.4 (private-meeting rule) requires that the assistant never ingest the body, subject, or attendee semantics of a private item while still treating it as a busy block (`PRIVATE_BUSY_ONLY`, §9.1).

**Issue #20 (safe-mode field-suppression completion).** The safe-mode path in `ResponseShaper` suppresses `BodyPreview`, `SenderName`, and `SenderEmail` for messages (and `BodyPreview`, `BodyFull`, and the three attendee JSON fields for events), but leaves other protected fields populated — `ToJson`, `CcJson`, resolved sender identity fields on messages, and `Organizer` on events — and never sets `ProtectedFieldsAvailable = false`, so callers cannot distinguish a suppressed field from a field Outlook never provided.

**The conflation defect (spans both).** `IsRedacted` is currently set to `true` by safe-mode shaping for every item regardless of sensitivity, conflating mode-based field suppression (a run-mode policy) with sensitivity-based content redaction (a per-item privacy property). #18 reserves `IsRedacted` exclusively for sensitivity redaction and removes it from safe-mode shaping; #20 completes safe-mode suppression breadth and sets `ProtectedFieldsAvailable = false` on the shaping path. Applying one without the other leaves either a broken `IsRedacted` semantic or incomplete field suppression (stated in issue #20's constraints). Both land in this feature.

**Staleness reconciliation.** The issue bodies were written 2026-04-11, before the issue #71/#72/#73 Graph-field additions. `MessageDto` now also carries `SenderEmailResolved`, `FromEmailAddress`, `ConversationId`, and `MeetingMessageType`; `EventDto` now also carries `ResponseStatus`, `Categories`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime`, `BodyFull`, and `SensitivityLabel`. The acceptance criteria below extend both field sets accordingly; `spec.md` records the field-by-field delta and rationale.

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

## Constraints & Risks (consolidated)

- Redaction occurs at write time; previously cached unredacted Private/Confidential rows are corrected only when a subsequent scan refreshes them. A cache flush or re-scan is recommended after deployment (issue #18 deployment note).
- Removing `IsRedacted = true` from safe-mode shaping is a deliberate breaking behavioral change for callers that used the flag to detect safe-mode suppression. Safe-mode suppression is now signaled by `ProtectedFieldsAvailable = false`.
- Safe-mode nulling of `ToJson`, `CcJson`, `Organizer`, and resolved sender fields is a breaking behavioral change for consumers that read those fields from safe-mode responses.
- Safe-mode shaping overrides `ProtectedFieldsAvailable` at response time without modifying the stored row; direct cache reads do not reflect the override.
- Outlook sensitivity values outside 0-3 are treated as non-sensitive; the implementation must not assume exhaustive enum coverage.

## Source

- `github-issue-18.md` (from `docs/features/potential/2026-04-11-implement-sensitivity-based-redaction.md`)
- `github-issue-20.md` (from `docs/features/potential/2026-04-11-complete-safe-mode-field-suppression.md`)
- Reconciled against current `BridgeContracts.cs`, `OutlookScanner.cs`, `OutlookScanner.GraphFields.cs`, `ResponseShaper.cs` (post-#71/#72/#73); see `spec.md` for the field-by-field delta.
