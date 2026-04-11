---
title: "populate-attendee-resource-fields-from-outlook - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-36"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# populate-attendee-resource-fields-from-outlook (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

`NormalizeEvent` in `OutlookScanner.cs` hardcodes `null` for `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson` at [OutlookScanner.cs:439-441](src/OpenClaw.MailBridge/OutlookScanner.cs#L439-L441), so these fields are always `null` in the SQLite cache and in every API response. Calendar events never carry attendee or resource information regardless of what Outlook contains, making the bridge's event data incomplete for any consumer that needs to know who is involved in a meeting.

This is listed as deviation #13 (High) in the design audit.

## Proposed Behavior

During normalization inside `NormalizeEvent`, read the `RequiredAttendees`, `OptionalAttendees`, and `Resources` properties from the Outlook appointment item using the existing `OutlookComHelpers.GetOptionalString` helper. Each property is a semicolon-delimited string on `AppointmentItem`. Split each value on `';'`, trim whitespace from each entry, discard empty entries, and serialize the resulting list to a compact JSON array string. Pass `null` if the split produces an empty list.

The resulting strings are stored in `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson` on the `EventDto`. The cache schema already has columns for all three fields; no schema change is required.

**Serialization format:**

```json
["Alice Smith", "bob@example.com", "Carol"]
```

A single helper method (e.g., `ParseAttendeesToJson`) should encapsulate the split-trim-serialize logic and be called for all three fields, keeping `NormalizeEvent` readable.

**`ProtectedFieldsAvailable` adjustment:** once attendee data is populated, the `ProtectedFieldsAvailable` flag on the `EventDto` should be set to `true` when at least one of `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, or `ResourcesJson` is non-null — mirroring the existing pattern on `MessageDto`. Currently `ProtectedFieldsAvailable` is hardcoded `false` for events.

Safe-mode suppression (nulling attendee fields at response time) is a separate concern addressed in `complete-safe-mode-field-suppression`; this feature populates the data into the cache so that enhanced-mode responses can serve it.

## Acceptance Criteria (early draft)

- [ ] `NormalizeEvent` reads `RequiredAttendees` from the Outlook item and serializes it to a JSON array string stored in `EventDto.RequiredAttendeesJson`.
- [ ] `NormalizeEvent` reads `OptionalAttendees` from the Outlook item and serializes it to a JSON array string stored in `EventDto.OptionalAttendeesJson`.
- [ ] `NormalizeEvent` reads `Resources` from the Outlook item and serializes it to a JSON array string stored in `EventDto.ResourcesJson`.
- [ ] Each raw semicolon-delimited string is split on `';'`, each entry is trimmed, and empty entries are discarded before serialization.
- [ ] When the raw property is null, empty, or yields no non-empty entries after splitting, the corresponding JSON field is stored as `null` (not as an empty JSON array `[]`).
- [ ] `EventDto.ProtectedFieldsAvailable` is `true` when at least one of `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, or `ResourcesJson` is non-null; `false` otherwise.
- [ ] A single `ParseAttendeesToJson` helper (or equivalent) is used for all three attendee/resource fields — no copy-paste of the split-trim-serialize logic.
- [ ] When the Outlook item does not expose a given property (COM reflection throws), the field is `null` and no exception propagates from `NormalizeEvent`.
- [ ] In enhanced mode, `get_event` and `list_calendar_window` responses include the populated attendee and resource JSON arrays.
- [ ] In safe mode, attendee and resource fields remain null in the response (enforced by `ResponseShaper` — no change required in this feature, but must not regress).

## Constraints & Risks

- Outlook's `RequiredAttendees`, `OptionalAttendees`, and `Resources` properties on `AppointmentItem` are plain semicolon-delimited display-name strings, not structured collections with email addresses. The serialized JSON arrays will contain whatever display strings Outlook provides; they will not contain SMTP addresses unless Outlook includes them in the display name string.
- The split-on-semicolon approach is consistent with how Outlook exposes these fields in classic COM interop. If a display name itself contains a semicolon (uncommon but possible), the name will be split incorrectly. This is a known limitation of the late-bound string property as opposed to the `Recipients` collection.
- The code uses `GetOptionalString`, which silently returns `null` on any COM exception. If Outlook is in a degraded state at scan time, attendee fields will be `null` for that scan cycle. This is consistent with how all other optional fields behave.
- `System.Text.Json` must be available for the serialization step. Confirm it is already referenced in the project (it is part of the .NET runtime for net10.0-windows and requires no additional package reference).
- This feature intersects with `complete-safe-mode-field-suppression`: once attendee fields contain data, the safe-mode shaper must null them before serving. Deploying this feature without the suppression fix would expose attendee data in safe mode. The two changes should be deployed together or the suppression fix should land first.
- This feature also intersects with `implement-sensitivity-based-redaction`: sensitivity-triggered redaction nulls attendee fields for Private/Confidential items at normalization time, which means those rows will have `null` attendee fields in the cache regardless of what Outlook returns.

## Test Conditions to Consider

- [ ] Unit test: `NormalizeEvent` with a fake appointment item where `RequiredAttendees = "Alice Smith; bob@example.com"` produces `RequiredAttendeesJson = "[\"Alice Smith\",\"bob@example.com\"]"`.
- [ ] Unit test: `NormalizeEvent` with `OptionalAttendees = "Carol; Dave"` produces `OptionalAttendeesJson = "[\"Carol\",\"Dave\"]"`.
- [ ] Unit test: `NormalizeEvent` with `Resources = "Conference Room A"` produces `ResourcesJson = "[\"Conference Room A\"]"`.
- [ ] Unit test: `NormalizeEvent` with an attendee string containing extra whitespace (`" Alice ; Bob "`) produces trimmed entries (`["Alice","Bob"]`).
- [ ] Unit test: `NormalizeEvent` with `RequiredAttendees = ""` (empty) or `null` produces `RequiredAttendeesJson = null` (not `"[]"`).
- [ ] Unit test: `NormalizeEvent` with `RequiredAttendees = ";"` (only delimiter, no entries) produces `RequiredAttendeesJson = null`.
- [ ] Unit test: `NormalizeEvent` where the COM property throws (simulated by absent property on fake) produces `RequiredAttendeesJson = null` with no exception.
- [ ] Unit test: `EventDto.ProtectedFieldsAvailable = true` when `RequiredAttendeesJson` is non-null.
- [ ] Unit test: `EventDto.ProtectedFieldsAvailable = false` when all of `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson` are null.
- [ ] Unit test: `ParseAttendeesToJson(null)` returns `null`; `ParseAttendeesToJson("")` returns `null`; `ParseAttendeesToJson("A; B")` returns a valid JSON array string.
- [ ] Integration scenario: upsert an `EventDto` with non-null `RequiredAttendeesJson` and retrieve it via `GetEventAsync`; confirm the JSON array round-trips correctly through the SQLite TEXT column.
- [ ] CLI/API example (enhanced mode): `get_event` response includes `"required_attendees_json": "[\"Alice\",\"Bob\"]"`, `"optional_attendees_json": null`, `"protected_fields_available": true`.
- [ ] CLI/API example (safe mode): `get_event` response has `"required_attendees_json": null`, `"optional_attendees_json": null`, `"resources_json": null` (suppressed by ResponseShaper).

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/populate-attendee-resource-fields-from-outlook/` folder from the template

