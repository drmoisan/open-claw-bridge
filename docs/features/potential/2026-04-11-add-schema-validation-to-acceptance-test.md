---
title: "add-schema-validation-to-acceptance-test - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-47"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# add-schema-validation-to-acceptance-test (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The acceptance test script ([test-mailbridge.ps1](scripts/test-mailbridge.ps1)) verifies that list and get RPC calls succeed (`ok == true`), but it does not validate the structure or content of the returned JSON objects beyond a few specific privacy fields. Specifically:

- Suite B (Mail Read Path) performs no field-by-field schema validation of returned `MessageDto` objects (deviation shown in design audit section 7, Suite B, "Verify schema: FAIL").
- Suite C (Calendar Read Path) performs no field-by-field schema validation of returned `EventDto` objects (deviation shown in section 7, Suite C, "Verify schema: FAIL").
- Suite D's `Assert-SafeModePrivacy` checks only `body_preview`, `sender_name`, and `sender_email` — it does not check that `to_json`, `cc_json`, `organizer`, `required_attendees_json`, `optional_attendees_json`, `resources_json`, or `protected_fields_available` are absent (null) in safe mode.

A passing acceptance test therefore does not confirm that the bridge returns the correct fields in the correct modes. A regression that drops a required field, adds an unexpected field, or leaks a protected field bypasses the acceptance gate entirely. Schema validation is how the acceptance test becomes evidence, not just smoke.

This work item is listed as deviation #22 (Low) in the design audit.

## Proposed Behavior

Add two new PowerShell functions to [test-mailbridge.ps1](scripts/test-mailbridge.ps1) and call them from the appropriate suite positions.

**`Assert-MessageSchema`** — validates a single message object against the expected field set for the active mode:

Required fields (must be non-null in both modes):
- `bridgeId` / `bridge_id`
- `itemKind` / `item_kind`
- `subject` (may be null in safe mode if sensitivity redaction is active, but the field itself must be present)
- `unread`
- `hasAttachments` / `has_attachments`
- `sensitivity`
- `protectedFieldsAvailable` / `protected_fields_available`
- `isRedacted` / `is_redacted`

Safe-mode field suppressions (must be null or absent):
- `bodyPreview` / `body_preview`
- `senderName` / `sender_name`
- `senderEmail` / `sender_email`
- `toJson` / `to_json`
- `ccJson` / `cc_json`

Enhanced-mode fields (must be present and non-null when data exists):
- `bodyPreview` / `body_preview` (may be null if the item had no body, but must not be suppressed by mode)
- `senderName` / `sender_name`
- `senderEmail` / `sender_email`

**`Assert-EventSchema`** — validates a single event object against the expected field set for the active mode:

Required fields (must be present in both modes):
- `bridgeId` / `bridge_id`
- `subject`
- `startUtc` / `start_utc`
- `endUtc` / `end_utc`
- `isRecurring` / `is_recurring`
- `sensitivity`
- `protectedFieldsAvailable` / `protected_fields_available`
- `isRedacted` / `is_redacted`

Safe-mode field suppressions (must be null or absent):
- `bodyPreview` / `body_preview`
- `organizer`
- `requiredAttendeesJson` / `required_attendees_json`
- `optionalAttendeesJson` / `optional_attendees_json`
- `resourcesJson` / `resources_json`

The existing `Assert-SafeModePrivacy` function should be extended or replaced so that the message checks for `to_json` / `cc_json` and the event checks for `organizer` / attendee fields are included.

Both functions should use the existing `Get-BridgeFieldValue` helper (already present at the top of the script) to resolve camelCase and snake_case field name aliases.

The functions should be called:
- After `$messageItems` is populated (Suite B): call `Assert-MessageSchema` on each item when `$messageItems.Count -gt 0`.
- After `$calendarItems` is populated (Suite C): call `Assert-EventSchema` on each item when `$calendarItems.Count -gt 0`.
- Replace the existing `Assert-SafeModePrivacy` call (currently at the end of Suite D) with calls to the more complete `Assert-MessageSchema` and `Assert-EventSchema`.

## Acceptance Criteria (early draft)

- [ ] `Assert-MessageSchema` exists as a function in [test-mailbridge.ps1](scripts/test-mailbridge.ps1).
- [ ] `Assert-MessageSchema` throws a descriptive error if any required field is missing from a message object.
- [ ] `Assert-MessageSchema` throws if `body_preview`, `sender_name`, `sender_email`, `to_json`, or `cc_json` is non-null on a message returned in safe mode.
- [ ] `Assert-MessageSchema` does not throw for these fields when the bridge is in enhanced mode.
- [ ] `Assert-EventSchema` exists as a function in [test-mailbridge.ps1](scripts/test-mailbridge.ps1).
- [ ] `Assert-EventSchema` throws a descriptive error if any required field is missing from an event object.
- [ ] `Assert-EventSchema` throws if `body_preview`, `organizer`, `required_attendees_json`, `optional_attendees_json`, or `resources_json` is non-null on an event returned in safe mode.
- [ ] `Assert-EventSchema` does not throw for these fields when the bridge is in enhanced mode.
- [ ] Both functions are called when the corresponding item collections are non-empty (skip gracefully when empty to avoid false failures on clean mailboxes).
- [ ] The existing `Assert-SafeModePrivacy` call is updated or replaced to avoid duplicate checks.
- [ ] Schema validation failures produce messages that identify which field failed and on which object (include `bridgeId` or index in the error text).
- [ ] The final `Write-Output 'AutomatedSuitesPassed: A,B,C,D,F'` line is reached only when all schema checks pass.

## Constraints & Risks

- The test script currently uses `Get-BridgeFieldValue` to handle both camelCase and snake_case field name aliases returned by the bridge. The schema validation functions must use the same helper consistently; do not hard-code a single casing convention, as the serialization format may differ between bridge versions.
- Schema validation can only be exercised when `$ExpectMessageData` or `$ExpectCalendarData` is `$true` and the bridge actually has cached data. On a clean mailbox with no messages or appointments, the item count will be zero and the validation is skipped. This is not a test gap — the schema check requires live data and is verified by the operator at deployment time.
- The `protectedFieldsAvailable` / `protected_fields_available` field is currently always written as `0` (false) because the bridge does not yet implement Outlook PIA or protected-field read access (deviation #2). Schema validation should assert that the field is present and is a boolean value, but should not assert that it is `true` — doing so would cause all acceptance tests to fail until deviation #2 is resolved. Asserting presence of the field is sufficient for now.
- Adding schema enforcement to an existing acceptance script that passes today means any schema regression will now cause a failure, which is the intent. However, if other pending remediations (e.g., `complete-safe-mode-field-suppression`) have not yet been deployed, running this updated script against the current bridge build will fail on the `to_json` / `organizer` checks. The schema validation should be deployed together with or after the suppression fixes, not before them.
- The `Assert-SafeModePrivacy` function currently exists and is called. If it is replaced rather than extended, any test that imports or dot-sources the function by name will need to be updated. Prefer extending the existing function or making the new functions supersets of it.

## Test Conditions to Consider

- [ ] Run the updated script against a bridge in safe mode with real message data: confirm `Assert-MessageSchema` passes for all required fields and throws on any suppression leak.
- [ ] Run the updated script against a bridge in safe mode with real calendar data: confirm `Assert-EventSchema` passes and throws on attendee field exposure.
- [ ] Manually introduce a schema regression (temporarily remove a field from the bridge response) and confirm the validation throws with a descriptive message.
- [ ] Run the script against a bridge in enhanced mode and confirm that `body_preview`, `sender_name`, and `sender_email` do not cause false failures.
- [ ] Run the script against an empty mailbox (`$ExpectMessageData = $false`): confirm schema checks are skipped without error.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/add-schema-validation-to-acceptance-test/` folder from the template

