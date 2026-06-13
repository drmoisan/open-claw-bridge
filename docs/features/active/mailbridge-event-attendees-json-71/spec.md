# mailbridge-event-attendees-json — Spec

- **Issue:** #71
- **Parent (optional):** Track M (MailBridge DTO/scanner): #72 -> #71 -> #73
- **Owner:** drmoisan
- **Last Updated:** 2026-06-13T10-31
- **Status:** Draft
- **Version:** 0.1

## Overview

`OutlookScanner.BuildEventDto` (`src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs`) currently
hardcodes `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson` to `null`
(positional arguments at lines 44–46). The `EventDto` contract already carries these three nullable
`string?` fields (`BridgeContracts.cs` lines 106–108); they are persisted by `CacheRepository` but
never populated. Downstream agents (deferred from #70) need attendee data on calendar events.

This feature populates the three fields from the COM `AppointmentItem.Recipients` collection,
emitting each as a JSON array of `{"name","email"}` objects matching the Graph `emailAddress`
shape, and extends safe-mode redaction so the attendee PII is nulled in safe mode for parity with
the existing message-path redaction of `SenderName`/`SenderEmail`.

- Target users/personas and primary use cases: the OpenClaw agent consuming normalized calendar
  events over the HostAdapter HTTP surface; it requires required/optional/resource attendee lists.
- Success metrics or expected impact: a scan of a meeting with known attendees returns non-null
  attendee JSON with correct names and emails in enhanced mode; safe mode returns null.

## Behavior

- Main user flow (happy path): During calendar normalization, `BuildEventDto` enumerates the COM
  `Recipients` collection of the appointment item. Each recipient is classified by its `Type`
  property (`OlMeetingRecipientType`: `1 = olRequired`, `2 = olOptional`, `3 = olResource`).
  Recipients are grouped by type and each group is serialized to a JSON array of
  `{"name","email"}` objects, in collection order. The three serialized strings populate
  `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson`.
- Alternate/edge flows:
  - A meeting with no recipients of a given type yields an empty JSON array `[]` for that field
    (not null), so the absence of a type is distinguishable from an unread field. (Final null-vs-
    empty decision is an open question for planning; see Constraints & Risks.)
  - A recipient missing a name or resolvable SMTP address emits the available value and an empty
    string for the missing one; both fields are always present in each object.
  - `Type` values outside `{1,2,3}` are ignored (not placed in any of the three fields).
- Error handling and recovery behavior: COM access failures while reading an individual recipient
  must not abort the scan of the event. The scanner follows the existing optional-read pattern
  (`OutlookComHelpers.GetOptional*`) and fails soft per recipient. All COM objects obtained while
  enumerating `Recipients` (the collection and each `Recipient`) are released deterministically
  per the COM-confinement rule.

## Inputs / Outputs

- Inputs: the COM `AppointmentItem` (`object item`) passed to `BuildEventDto`; the active
  `BridgeSettings` (for mode/redaction).
- Outputs: the populated `EventDto` with three attendee JSON strings; persisted by
  `CacheRepository` to the existing `required_attendees_json` / `optional_attendees_json` /
  `resources_json` columns (no schema change).
- Config keys and defaults: existing `BridgeSettings.Mode` (`safe` | `enhanced`). No new config.
- Versioning or backward-compatibility constraints: no contract shape change. `EventDto`
  positional arguments are unchanged in count and order; only the three `null` literals become
  populated expressions.

## API / CLI Surface

- No public-API shape change. `EventDto` record is unchanged.
- JSON contract per field: `[{"name":"<display name>","email":"<smtp address>"}, ...]`. Property
  names are lowercase `name` and `email` to match the Graph `emailAddress` shape used elsewhere.
- Serialization must be deterministic (stable property order, no culture-dependent formatting).

## Data & State

- Data transformations and invariants: COM `Recipients` -> grouped by `Type` -> JSON arrays.
  Each emitted object always has both `name` and `email` keys. Collection order is preserved.
- Caching or persistence details: values flow through the existing `CacheRepository` event-insert
  path (`required_attendees_json`, `optional_attendees_json`, `resources_json`); no migration.
- Migration or backfill requirements: none.

## Constraints & Risks

- Limits: recipient enumeration adds COM round-trips per event; reuse a single pass over the
  `Recipients` collection and release wrappers deterministically to avoid RCW accumulation.
- Security/privacy considerations: attendee names and emails are PII. Safe mode MUST null all
  three attendee JSON fields in `ResponseShaper.ShapeEvent`, matching the existing message-path
  redaction of `SenderName`/`SenderEmail`. Enhanced mode returns the populated values.
- `ProtectedFieldsAvailable`: attendee details are protected fields. The existing event value is
  derived from body availability; planning must decide whether attendee readability contributes to
  or is gated by `ProtectedFieldsAvailable` without weakening the existing body-based signal.
- Operational/rollout risks and mitigations: COM read failure on a single recipient must not fail
  the event scan (fail-soft per recipient).
- Open question (for planning): empty-collection representation — empty array `[]` versus `null`.
  Recommendation: empty array for type-with-no-recipients, reserving `null` for safe-mode redaction
  and unread state, so consumers can distinguish "no attendees of this type" from "redacted".

## Implementation Strategy

- Implementation scope:
  1. `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (or a new `OutlookScanner.*.cs`
     partial if the 500-line cap is at risk) — add a recipient-enumeration + JSON-serialization
     helper and replace the three `null` literals at lines 44–46 with its results.
  2. `src/OpenClaw.MailBridge/ResponseShaper.cs` — null the three attendee JSON fields in the
     safe-mode branch of `ShapeEvent` for redaction parity.
- New classes/functions: a private helper on the `OutlookScanner` partial that returns the three
  serialized strings (or a small immutable carrier) from the COM `Recipients` collection; reuse
  `OutlookComHelpers` for optional reads and `_com.ReleaseAll` for deterministic release.
- Dependency changes: none. Use `System.Text.Json` already in use by the contracts/host.
- Logging/telemetry additions: none beyond existing scan logging.
- Rollout plan: no feature flag; behavior is gated by existing `safe`/`enhanced` mode.

## Definition of Done

- [x] Acceptance criteria documented and mapped to tests or demos
- [x] Behavior matches acceptance criteria in all documented environments
- [x] Tests updated/added (unit/integration as applicable)
- [x] Edge cases and error handling covered by tests
- [x] Docs updated (README, docs/features/active/... links) if applicable
- [x] Telemetry/logging added or updated (if applicable)
- [x] Toolchain pass completed (format → lint → type-check → test)
