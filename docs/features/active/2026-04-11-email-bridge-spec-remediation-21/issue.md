# email-bridge-spec-remediation (Issue #21)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Promoted -> docs/features/active/email-bridge-spec-remediation/ (Issue #21)

- Issue: #21
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/21
- Last Updated: 2026-04-11
- Work Mode: full-feature

## Problem / Why

A full acceptance audit of the delivered `OpenClaw.MailBridge` implementation against the authoritative spec (see [docs/design-audit.md](../../design-audit.md)) identified 22 deviations. Seven of these are blocking exit criteria, meaning the bridge cannot be considered spec-compliant or safe to operate until they are resolved.

The most severe gaps are:

- **Sensitivity-based redaction is entirely absent.** Private and confidential Outlook items are exposed to callers without any subject replacement, field nulling, or `is_redacted` flag driven by sensitivity. This is a privacy and security failure.
- **Safe-mode field suppression is incomplete.** `to_json`, `cc_json`, `organizer`, attendee/resource fields, and `protected_fields_available=false` are not enforced in safe mode. Protected data leaks in the default operating mode.
- **Calendar overlap filter uses the wrong semantics.** The current `[Start] >= start AND [Start] < end` filter misses appointments that span the query boundary, producing an incorrect calendar view.
- **`MeetingItem` normalization is wrong and incomplete.** `item_kind` is set to `"meeting"` instead of the required `"meeting_message"`, and `GetAssociatedAppointment(false)` is never called.
- **`AddWindowsService()` is configured in the bridge host.** The spec explicitly forbids creating a Windows service for the bridge. The capability is present in the build even if not currently registered.
- **File logging is absent.** The bridge writes to console only; `logs\bridge.log` is never created, violating the operational observability requirement.
- **COM access is late-bound via reflection** instead of using the Outlook PIA / Object Library as required by the spec.

This tracking issue covers the complete remediation effort.

## Proposed Behavior

After remediation, the bridge must satisfy all exit criteria in section 11 of the design audit:

- Sensitivity-based redaction active for `Sensitivity` values 2 (Private) and 3 (Confidential) on both messages and events.
- Safe mode enforces the exact field set defined in spec section 11, including `protected_fields_available=false` and suppression of `to_json`, `cc_json`, `organizer`, attendee/resource fields.
- Calendar scan uses the overlap filter `[Start] <= '{end}' AND [End] >= '{start}'`.
- `MeetingItem` entries carry `item_kind = "meeting_message"` and a populated `AssociatedAppointmentBridgeId` where an appointment is found.
- `AddWindowsService` and the `Microsoft.Extensions.Hosting.WindowsServices` package are removed.
- File logging writes to `%LOCALAPPDATA%\OpenClaw\MailBridge\logs\bridge.log`.
- COM access uses the Outlook PIA / Object Library (early-bound interop).
- All medium and low deviations are resolved or explicitly deferred with documented rationale.

## Acceptance Criteria (early draft)

These map directly to the blocking exit criteria in section 11 of the design audit.

### Critical — blocking

- [ ] Sensitivity-based redaction is implemented: private/confidential messages suppress subject (replaced with `"Private message"`), sender, recipients, and body preview; `is_redacted=true`.
- [ ] Sensitivity-based redaction is implemented: private/confidential events suppress subject (replaced with `"Private appointment"`), location, organizer, attendees, resources, and body preview; `is_redacted=true`.
- [ ] `is_redacted` is driven by `Sensitivity`, not by mode. Mode-based suppression controls field presence independently.
- [ ] Calendar scan filter matches `[Start] <= '{end}' AND [End] >= '{start}'` and returns events that span the query boundary.
- [ ] Safe-mode `ShapeMessage` nulls `ToJson`, `CcJson`, and sets `ProtectedFieldsAvailable=false`.
- [ ] Safe-mode `ShapeEvent` nulls `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, and sets `ProtectedFieldsAvailable=false`.

### High — blocking

- [ ] `MeetingItem` entries use `item_kind = "meeting_message"` in both `NormalizeMessage` and the `ListRecentMeetingRequestsAsync` SQL filter.
- [ ] `GetAssociatedAppointment(false)` is called for meeting items and the result is encoded as `AssociatedAppointmentBridgeId`. `GetAssociatedAppointment(true)` is never called.
- [ ] `AddWindowsService(...)` is removed from `BridgeApplication.cs` and `Microsoft.Extensions.Hosting.WindowsServices` is removed from the project file.
- [ ] File logging is implemented: `bridge.log` is written under `%LOCALAPPDATA%\OpenClaw\MailBridge\logs\`.
- [ ] `to_json` and `cc_json` are populated from Outlook `To`/`CC` recipients in `NormalizeMessage`.
- [ ] `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson` are populated from Outlook in `NormalizeEvent`.
- [ ] COM access is refactored to use the Outlook PIA / Object Library (early-bound interop) as specified.

### Medium

- [ ] Pipe ACL grants `FullControl` (not `ReadWrite | CreateNewInstance`) to SYSTEM and Administrators.
- [ ] `schema_version` is written to `scan_state` during `CacheRepository.InitializeAsync()`.
- [ ] Individual COM items from `EnumerateItems` are released after normalization via `_com.Release`.
- [ ] COM hygiene acceptance test loop runs 100 iterations (not 25), with process-count and handle monitoring.
- [ ] Suite E (pipe isolation) is automated: `openclaw-svc` connectivity verified; unapproved account denial verified.
- [ ] Config validation covers `InboxOverlapMinutes`, `CalendarPastDays`, `CalendarFutureDays`, and `LogLevel`.

### Low

- [ ] `last_modified_utc` is populated for events from `LastModificationTime`.
- [ ] `BridgeState.error` is entered on an unrecoverable failure path (e.g., pipe ACL failure).
- [ ] `Microsoft.Data.Sqlite` is updated to the .NET 10-aligned version.
- [ ] `System.IO.Pipes.AccessControl` preview package is replaced with a stable release.
- [ ] Acceptance test (`test-mailbridge.ps1`) validates returned JSON field schemas for both messages and events.

## Constraints & Risks

- **PIA / early-bound COM refactor (deviation 2) is a prerequisite for several other fixes.** The late-bound reflection approach makes it harder to safely call methods like `GetAssociatedAppointment` with typed arguments. This refactor should happen before or alongside the meeting-item and redaction fixes.
- **DB schema migration required.** Adding `associated_appointment_bridge_id` to the `messages` table and writing `schema_version` to `scan_state` require `ALTER TABLE` migration logic in `CacheRepository.InitializeAsync`. Existing databases must be handled without data loss.
- **Safe-mode suppression and sensitivity-based redaction must remain separate code paths.** Conflating them (as currently done with `is_redacted`) was the root cause of the existing architectural deviation. Any fix must keep the two concepts distinct.
- **`ListRecentMeetingRequestsAsync` SQL filter rename** (`'meeting'` → `'meeting_message'`) invalidates cached rows from before the fix. This is acceptable because the cache is ephemeral (refreshed on the next inbox scan), but it should be documented in the runbook.
- **Suite E (pipe isolation) requires a second Windows user account** (`openclaw-svc`) to be present at test time. Automation may not be feasible in all developer environments and may need to remain an operator-verified step with documented evidence.
- **Removing `AddWindowsService`** changes the host builder; verify that `OnLogon` task-triggered execution is unaffected.

## Test Conditions to Consider

### Unit coverage areas
- [ ] `ResponseShaper.ShapeMessage` — safe mode suppresses `sender_name`, `sender_email`, `to_json`, `cc_json`, `body_preview`; sets `protected_fields_available=false`.
- [ ] `ResponseShaper.ShapeEvent` — safe mode suppresses `organizer`, attendee/resource fields, `body_preview`; sets `protected_fields_available=false`.
- [ ] `OutlookScanner.NormalizeMessage` — sensitivity 2 or 3 triggers subject replacement, field nulling, `is_redacted=true`; sensitivity 0/1 does not.
- [ ] `OutlookScanner.NormalizeEvent` — same sensitivity rules for calendar items.
- [ ] `OutlookScanner.NormalizeMessage` — `item_kind = "meeting_message"` for meeting items; `AssociatedAppointmentBridgeId` populated when appointment returned; null when not.
- [ ] `CacheRepository.ListRecentMeetingRequestsAsync` — returns only rows with `item_kind = 'meeting_message'`.
- [ ] `CacheRepository` round-trip for `associated_appointment_bridge_id`.
- [ ] Calendar filter string construction uses overlap semantics.

### Integration / acceptance scenarios
- [ ] Suite B: returned message JSON matches safe-mode schema exactly (no extra fields).
- [ ] Suite C: returned event JSON matches safe-mode schema exactly; overlap filter returns events spanning query boundary.
- [ ] Suite D: private/confidential items are redacted; safe-mode omissions verified field-by-field.
- [ ] Suite E: `openclaw-svc` pipe access verified; unapproved account denied.
- [ ] Suite F: 100-iteration COM hygiene loop with process and handle monitoring.

## Child Issues

Each deviation group below has or will have its own feature/issue entry:

| # | Deviation | Severity | Status |
|---|---|---|---|
| 1 | Sensitivity-based redaction absent | Critical | Needs issue |
| 2 | Calendar overlap filter wrong | Critical | Needs issue |
| 3 | Safe-mode field suppression incomplete | Critical | Needs issue |
| 4 | `GetAssociatedAppointment(false)` not called; wrong `item_kind` | High | [2026-04-11-implement-get-associate-appointment.md](2026-04-11-implement-get-associate-appointment.md) |
| 5 | File logging absent | High | Needs issue |
| 6 | `AddWindowsService` present | High | Needs issue |
| 7 | `to_json` / `cc_json` never populated | High | Needs issue |
| 8 | Attendee/resource fields never populated | High | Needs issue |
| 9 | Late-bound COM (no PIA) | High | Needs issue |
| 10 | `is_redacted` conflates sensitivity and mode | High | Covered by deviation 1 |
| 11 | Pipe ACL: SYSTEM/Admins wrong permission | Medium | Needs issue |
| 12 | `schema_version` never written | Medium | Needs issue |
| 13 | COM items not released in `EnumerateItems` | Medium | Needs issue |
| 14 | COM hygiene test: 25 iterations, no process/handle check | Medium | Needs issue |
| 15 | Suite E not automated | Medium | Needs issue |
| 16 | Config validation incomplete | Medium | Needs issue |
| 17 | `last_modified_utc` always null for events | Low | Needs issue |
| 18 | `BridgeState.error` never entered | Low | Needs issue |
| 19 | `Microsoft.Data.Sqlite` wrong version | Low | Needs issue |
| 20 | `System.IO.Pipes.AccessControl` preview version | Low | Needs issue |
| 21 | Acceptance test missing schema validation | Low | Needs issue |
| 22 | No explicit hard-exit on pipe ACL failure | Low | Needs issue |

## Next Step

- [ ] Promote to GitHub issue (tracking issue template)
- [ ] Create `docs/features/active/email-bridge-spec-remediation/` folder from the template
- [ ] Create child issues for each deviation that does not yet have a feature entry