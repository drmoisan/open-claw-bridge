# 2026-04-11-implement-sensitivity-based-redaction — Spec

- **Issue:** #18
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-11T11-01
- **Status:** Draft
- **Version:** 0.1

## Overview

The `Sensitivity` integer field is read from Outlook items and stored in the SQLite cache for both messages and events, but its value is never inspected. Items flagged as Private (`Sensitivity=2`) or Confidential (`Sensitivity=3`) are returned to callers with their full field set — subject, sender name, sender email, recipient lists, body preview, location, organizer, and attendees — exposing information that Outlook explicitly marks as restricted.

Additionally, `is_redacted` is currently set to `true` in safe mode for all items regardless of sensitivity, conflating two distinct concepts: mode-based field suppression (a run-mode policy) and sensitivity-based content redaction (a per-item privacy property). This conflation obscures the true reason a response was modified and makes the flag unreliable as a caller-facing signal.


## Behavior

During normalization — inside `NormalizeMessage` and `NormalizeEvent` in `OutlookScanner.cs` — check the `Sensitivity` integer value before constructing the `MessageDto` or `EventDto`. If `Sensitivity` is 2 (Private) or 3 (Confidential):

**For messages (`NormalizeMessage`):**
- Replace the subject with the literal string `"Private message"`.
- Set `SenderName`, `SenderEmail`, `ToJson`, `CcJson`, and `BodyPreview` to `null`.
- Set `IsRedacted = true`.
- Set `ProtectedFieldsAvailable = false`.
- Retain `Sensitivity`, `ReceivedUtc`, `SentUtc`, `Importance`, `Unread`, `HasAttachments`, `ItemKind`, `BridgeId`, and `MessageClass` unchanged.

**For events (`NormalizeEvent`):**
- Replace the subject with the literal string `"Private appointment"`.
- Set `Location`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, and `BodyPreview` to `null`.
- Set `IsRedacted = true`.
- Set `ProtectedFieldsAvailable = false`.
- Retain `Sensitivity`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `BridgeId`, and `GlobalAppointmentId` unchanged.

Redaction is applied at normalization time so the redacted values are written to the cache. Subsequent reads from the cache through `ResponseShaper` serve already-redacted data.

The `is_redacted` flag is reserved exclusively for sensitivity-based redaction. `ResponseShaper`'s safe-mode path suppresses fields (body preview, sender fields) without setting `is_redacted = true`; those are separate operations serving a different purpose.


## Inputs / Outputs

- Inputs (CLI flags, files, env vars)
- Outputs (artifacts, logs, telemetry)
- Config keys and defaults:
- Versioning or backward-compatibility constraints:

## API / CLI Surface

List commands, flags, request/response shapes, and examples.
- Example invocations with expected outputs (concise):
- Contracts and validation rules:

## Data & State

Data flow, storage, or state changes introduced by this feature.
- Data transformations and invariants:
- Caching or persistence details:
- Migration or backfill requirements (if any):

## Constraints & Risks

- Redaction occurs at write time (normalization), so previously cached Private/Confidential items with unredacted data in the SQLite cache will not be retroactively corrected until the cache row is refreshed by a subsequent scan. A cache flush or re-scan may be required after deployment.
- The `BodyPreview` field is computed in `NormalizeMessage` by calling `ResponseShaper.ShapePreview` before the DTO is built. The redaction logic must null `BodyPreview` after the DTO is constructed (or skip the `ShapePreview` call for sensitive items) to avoid unnecessary processing of content that will be discarded.
- The `is_redacted` semantic change — removing it from the safe-mode path in `ResponseShaper` — is a breaking behavioral change for callers that currently rely on `is_redacted = true` to detect safe-mode suppression. Any consumer interpreting `is_redacted` as "fields were suppressed due to mode" will see a change in signal.
- Outlook sensitivity levels are integers with documented values (0=Normal, 1=Personal, 2=Private, 3=Confidential). Values outside this range should be treated as non-sensitive (no redaction). The implementation should not assume exhaustive enum coverage.


## Implementation Strategy

- Implementation scope (what changes, not sequencing):
- New classes/functions/commands to add or update:
- Dependency changes (new/removed packages) and rationale:
- Logging/telemetry additions and locations:
- Rollout plan (feature flags, staged deploys, fallback path):

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)
- [ ] Unit test: `NormalizeMessage` with `Sensitivity=2` produces `subject="Private message"`, all protected fields null, `IsRedacted=true`, `ProtectedFieldsAvailable=false`, `Sensitivity=2` preserved.
- [ ] Unit test: `NormalizeMessage` with `Sensitivity=3` produces equivalent redacted output with subject `"Private message"`.
- [ ] Unit test: `NormalizeMessage` with `Sensitivity=0`, `Sensitivity=1`, and `Sensitivity=null` produces an unredacted DTO with original subject and fields intact.
- [ ] Unit test: `NormalizeEvent` with `Sensitivity=2` produces `subject="Private appointment"`, `Location`/`Organizer`/attendee/resource/preview fields null, `IsRedacted=true`, `ProtectedFieldsAvailable=false`.
- [ ] Unit test: `NormalizeEvent` with `Sensitivity=3` produces equivalent redacted output.
- [ ] Unit test: `NormalizeEvent` with non-sensitive `Sensitivity` values produces an unredacted DTO.
- [ ] Unit test: `ResponseShaper.ShapeMessage` in safe mode does not set `IsRedacted=true` on a non-sensitive item.
- [ ] Unit test: `ResponseShaper.ShapeMessage` preserves `IsRedacted=true` on an already-redacted DTO (sensitivity-redacted item passed through the shaper).
- [ ] Unit test: `ResponseShaper.ShapeEvent` in safe mode does not set `IsRedacted=true`.
- [ ] Integration scenario: an item with `Sensitivity=2` upserted via `UpsertMessageAsync` and then retrieved via `GetMessageAsync` returns a DTO with all protected fields null and `is_redacted=true` — without calling `ResponseShaper`.
- [ ] CLI/API example: `get_message` for a Private item returns `{ "subject": "Private message", "sender_name": null, "sender_email": null, "body_preview": null, "is_redacted": true }` in both safe and enhanced modes.
- [ ] CLI/API example: `get_event` for a Confidential item returns `{ "subject": "Private appointment", "location": null, "organizer": null, "is_redacted": true }` in both modes.
