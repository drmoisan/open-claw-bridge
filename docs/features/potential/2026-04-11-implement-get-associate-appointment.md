# implement-get-associate-appointment (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

When a `MeetingItem` (a meeting request) is found in the inbox scan, the scanner currently assigns `item_kind = "meeting"` and does not call `GetAssociatedAppointment(false)` on the COM object. This produces two gaps:

1. `item_kind` is wrong — the spec requires `"meeting_message"`, which is what the SQL filter in `ListRecentMeetingRequestsAsync` must match. Because `CacheRepository` filters on the literal string `'meeting'`, the two deviations compound: fix one without the other and the meeting-request query breaks.
2. No appointment linkage is captured. Clients that receive a `meeting_message` entry have no way to correlate it with the corresponding calendar event because the scanner never reads the associated `AppointmentItem`. A new `AssociatedAppointmentBridgeId` field on `MessageDto` is needed to surface this link.

These are design-audit deviations 4 and 5 (High severity, blocking exit criterion "MeetingItem normalization").

## Proposed Behavior

During the inbox scan, after `IsMeetingItem` returns `true` for an item:

1. Set `item_kind = "meeting_message"` in `NormalizeMessage`.
2. Call `GetAssociatedAppointment(false)` on the `MeetingItem` COM object via `OutlookComHelpers.InvokeMember`. The `false` argument is mandatory — passing `true` would add the appointment to the calendar, which is a write operation categorically forbidden by the bridge spec.
3. If an appointment object is returned, read its `GlobalAppointmentID` and `EntryID`, compute the corresponding bridge ID with `BridgeIdCodec.EventId`, and store it in a new `AssociatedAppointmentBridgeId` field on `MessageDto`.
4. Release the appointment COM object in a `finally` block via `_com.ReleaseAll`.
5. If `GetAssociatedAppointment` returns `null` (meeting was declined or appointment was removed), `AssociatedAppointmentBridgeId` is left `null` — this is not an error.
6. Update the SQL filter in `CacheRepository.ListRecentMeetingRequestsAsync` from `item_kind = 'meeting'` to `item_kind = 'meeting_message'`. Update the `messages` table schema and upsert statement to include `associated_appointment_bridge_id`.
7. No changes to the client CLI argument surface are required; the new field is surfaced in the existing JSON payload.

## Acceptance Criteria (early draft)

- [ ] `NormalizeMessage` sets `ItemKind = "meeting_message"` for items where `IsMeetingItem` returns `true`.
- [ ] `NormalizeMessage` calls `GetAssociatedAppointment` with the argument `false` — verified in tests via a mock COM helper.
- [ ] `GetAssociatedAppointment(true)` is never called anywhere in the production code path.
- [ ] When `GetAssociatedAppointment(false)` returns a non-null appointment object, `MessageDto.AssociatedAppointmentBridgeId` is populated with the bridge-encoded event ID.
- [ ] When `GetAssociatedAppointment(false)` returns `null`, `MessageDto.AssociatedAppointmentBridgeId` is `null` and no exception is thrown.
- [ ] The appointment COM object returned by `GetAssociatedAppointment` is always released in a `finally` block, even when subsequent property reads fail.
- [ ] `CacheRepository.ListRecentMeetingRequestsAsync` SQL filter uses `item_kind = 'meeting_message'`.
- [ ] The `messages` table schema and upsert logic persist and retrieve `associated_appointment_bridge_id` correctly. Existing rows with `item_kind = 'meeting'` remain queryable via `GetMessageAsync` without breaking the read path.
- [ ] `MessageDto` includes the `AssociatedAppointmentBridgeId` field; all existing callers compile without modification (field is nullable with a `null` default).
- [ ] Unit tests cover: meeting item with valid appointment → bridge ID populated; meeting item with null appointment → null field, no throw; non-meeting item → field is null, `GetAssociatedAppointment` not called.
- [ ] All existing unit tests continue to pass.

## Constraints & Risks

- **STA thread requirement.** `GetAssociatedAppointment` is a COM method and must be invoked on the STA thread managed by `OutlookStaExecutor`. `NormalizeMessage` is already called within the STA executor context; no additional marshaling is required, but this constraint must be preserved if `NormalizeMessage` is ever refactored.
- **Late-bound COM.** The bridge uses `OutlookComHelpers.InvokeMember` (reflection-based dispatch) for all COM access. `GetAssociatedAppointment` must follow the same pattern until the PIA migration (deviation 2) is completed.
- **COM object lifetime.** The `AppointmentItem` returned by `GetAssociatedAppointment` is a new COM reference. Failure to release it will contribute to the existing COM leak problem (deviation 14). A `finally` block around `_com.ReleaseAll` is mandatory.
- **DB schema migration.** Adding `associated_appointment_bridge_id` to the `messages` table requires a schema change. Because the bridge uses SQLite and `CacheRepository.InitializeAsync` runs `CREATE TABLE IF NOT EXISTS`, a column-add migration (`ALTER TABLE … ADD COLUMN`) must be issued for existing databases. This affects the `CacheRepository` initialization path.
- **SQL filter rename.** Changing `'meeting'` to `'meeting_message'` in `ListRecentMeetingRequestsAsync` is a breaking change for any cached rows inserted before this fix. During the transition window, the cache will not return old meeting-request rows under the new filter. This is acceptable because the cache is ephemeral and refreshed on the next inbox scan.
- **Scope boundary.** This feature corrects `item_kind` and adds the appointment linkage field. Sensitivity-based redaction (deviation 6) and safe-mode field suppression (deviation 7) are separate features and must not be mixed into this change.

## Test Conditions to Consider

- [ ] `NormalizeMessage` with a mock `MeetingItem` COM object that has a valid `GetAssociatedAppointment(false)` return → `ItemKind = "meeting_message"`, `AssociatedAppointmentBridgeId` is non-null and correctly encoded.
- [ ] `NormalizeMessage` with a mock `MeetingItem` where `GetAssociatedAppointment(false)` returns `null` → `AssociatedAppointmentBridgeId = null`, no exception, COM object not double-released.
- [ ] `NormalizeMessage` with a normal `MailItem` (non-meeting) → `ItemKind = "mail"`, `AssociatedAppointmentBridgeId = null`, `GetAssociatedAppointment` not called.
- [ ] `CacheRepository.UpsertMessageAsync` and `ReadMessage` round-trip a `MessageDto` with a non-null `AssociatedAppointmentBridgeId`.
- [ ] `CacheRepository.ListRecentMeetingRequestsAsync` returns only rows with `item_kind = 'meeting_message'` and excludes `item_kind = 'mail'`.
- [ ] Schema migration: `InitializeAsync` against an existing DB that lacks `associated_appointment_bridge_id` completes without error (column-add path).

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/implement-get-associate-appointment/` folder from the template

