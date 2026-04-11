# 2026-04-11-complete-safe-mode-field-suppression — Spec

- **Issue:** #20
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-11T11-03
- **Status:** Draft
- **Version:** 0.1

## Overview

The safe-mode path in `ResponseShaper` suppresses `BodyPreview`, `SenderName`, and `SenderEmail` from messages, and `BodyPreview` from events, but leaves a broader set of protected fields populated. Fields containing recipient address lists (`ToJson`, `CcJson`), meeting participant identity (`Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`), and the `ProtectedFieldsAvailable` flag are returned as-is in safe mode, exposing information that the spec requires to be suppressed.

`ProtectedFieldsAvailable` is also never set to `false` in the safe-mode path, so callers have no reliable signal that suppress-on-read has occurred and cannot distinguish a suppressed-but-present field from a field that was simply never available from Outlook.

This is classified as a **Critical** deviation in the design audit (deviation #7, blocking exit criterion).


## Behavior

The safe-mode branch in `ResponseShaper.ShapeMessage` ([ResponseShaper.cs:24-30](src/OpenClaw.MailBridge/ResponseShaper.cs#L24-L30)) must additionally null `ToJson` and `CcJson`, and set `ProtectedFieldsAvailable = false`.

The safe-mode branch in `ResponseShaper.ShapeEvent` ([ResponseShaper.cs:46-50](src/OpenClaw.MailBridge/ResponseShaper.cs#L46-L50)) must additionally null `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson`, and set `ProtectedFieldsAvailable = false`.

Both changes are applied at response-shaping time (when serving a pipe RPC request), not at cache-write time. The underlying `MessageDto` and `EventDto` records stored in SQLite continue to hold the original values so that subsequent mode changes do not require a re-scan.

Enhanced-mode responses are unaffected: those paths already return all available fields with `ProtectedFieldsAvailable` reflecting whether data was present at scan time.

**Fields suppressed by safe mode after this change:**

| DTO | Fields nulled in safe mode |
|---|---|
| `MessageDto` | `BodyPreview`, `SenderName`, `SenderEmail`, `ToJson`, `CcJson` |
| `EventDto` | `BodyPreview`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson` |

**Flag set in safe mode (both DTOs):** `ProtectedFieldsAvailable = false`


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

- This change affects only the response-shaping layer; the underlying cache schema and scan logic are not modified. Any caller that currently reads `to_json`, `cc_json`, `organizer`, or attendee fields from safe-mode responses will receive `null` after this change. This is a breaking behavioral change for any consumer that was relying on those fields being present in safe mode.
- `ProtectedFieldsAvailable` is stored in the cache as the value set at normalization time (currently always `true` when any sender field was non-null for messages, and always `false` for events). The safe-mode shaping overrides it to `false` at response time without modifying the stored row. Code that reads `ProtectedFieldsAvailable` directly from the cache (e.g., in diagnostic or test queries) will not reflect the safe-mode override.
- This work intersects with the sensitivity-based redaction feature (`implement-sensitivity-based-redaction`). That feature removes `IsRedacted = true` from the safe-mode shaping paths, reserving it exclusively for sensitivity-triggered redaction. These two changes must be coordinated: applying one without the other leaves either a broken `IsRedacted` semantic or incomplete field suppression.
- `Location` on `EventDto` is not listed in the spec's safe-mode suppression requirements; it is retained as populated. Confirm against the authoritative spec before implementation.


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
- [ ] Unit test: `ShapeMessage` in safe mode — input DTO has non-null values for all 17 fields; assert `ToJson`, `CcJson`, `BodyPreview`, `SenderName`, `SenderEmail` are null and `ProtectedFieldsAvailable` is false in the shaped output; assert all other fields (`BridgeId`, `Subject`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `MessageClass`, `IsRedacted`) retain their original values.
- [ ] Unit test: `ShapeMessage` in enhanced mode — all fields pass through unchanged (except `BodyPreview` which is sanitized/truncated).
- [ ] Unit test: `ShapeEvent` in safe mode — input DTO has non-null values for all fields; assert `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, `BodyPreview` are null and `ProtectedFieldsAvailable` is false in the shaped output; assert `BridgeId`, `Subject`, `StartUtc`, `EndUtc`, `Location`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `IsRedacted` retain their original values.
- [ ] Unit test: `ShapeEvent` in enhanced mode — all fields pass through unchanged (except `BodyPreview`).
- [ ] Unit test: `ShapeMessage` in safe mode with a DTO where `ToJson` and `CcJson` are already null — no exception, output is valid.
- [ ] Unit test: `ShapeEvent` in safe mode with a DTO where all attendee fields are already null — no exception, output is valid.
- [ ] Regression: existing safe-mode tests for `SenderName`, `SenderEmail`, `BodyPreview` suppression continue to pass.
- [ ] Integration scenario: issue `list_recent_messages` against a bridge in safe mode where the cache contains messages with non-null `to_json` and `cc_json`; verify the response contains `null` for those fields and `protected_fields_available: false`.
- [ ] Integration scenario: issue `list_calendar_window` against a bridge in safe mode where the cache contains events with non-null organizer and attendee fields; verify the response contains `null` for those fields and `protected_fields_available: false`.
- [ ] CLI/API example (safe mode message): `{ "bridge_id": "...", "subject": "...", "sender_name": null, "sender_email": null, "to_json": null, "cc_json": null, "body_preview": null, "protected_fields_available": false }`.
- [ ] CLI/API example (safe mode event): `{ "bridge_id": "...", "subject": "...", "organizer": null, "required_attendees_json": null, "optional_attendees_json": null, "resources_json": null, "body_preview": null, "protected_fields_available": false }`.
