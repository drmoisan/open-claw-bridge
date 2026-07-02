# `sensitivity-redaction` — User Story

- Issue: #18 (co-delivers #20)
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-02T09-30

## Story Statement

- As a mailbox owner, I want items I mark Private or Confidential in Outlook to have their subject, body, sender, recipients, organizer, attendees, location, and categories withheld from the bridge cache and every RPC response, so that the assistant can never read or repeat the content of my private items.
- As a mailbox owner, I want those private items to still appear as busy blocks with their times, busy status, and recurrence intact, so that scheduling around them keeps working without exposing what they are.
- As an operator of the bridge in safe mode, I want a reliable signal (`protected_fields_available: false`) that fields were suppressed by mode, and a separate flag (`is_redacted`) that means only sensitivity redaction, so that I can tell why a response was reduced.

## Problem / Why

The bridge reads the Outlook `Sensitivity` value but never acts on it: Private (2) and Confidential (3) items are cached and served with their full content. This violates the master document §2.4 private-meeting rule — the assistant must never ingest the body, subject, or attendee semantics of a private item, while still marking the time unavailable (`PRIVATE_BUSY_ONLY`, §9.1). Separately, safe mode suppresses only part of the protected field set and misuses `is_redacted` as its signal, so the owner has neither complete suppression nor a trustworthy explanation of what was withheld and why.

## Personas & Scenarios

- Persona: **Dan, the mailbox owner.**
  - A senior leader whose calendar mixes routine meetings with private ones (medical appointments, HR conversations, compensation discussions) marked Private in Outlook.
  - Cares about: the privacy boundary Outlook already expresses being honored end-to-end; the assistant still scheduling correctly around private blocks.
  - Constraint: he will not audit cache contents himself; the guarantee must hold by construction, at write time, so no mode or code path can leak content later.
  - Frustration to avoid: discovering that an agent response quoted the subject of a private HR meeting.

- Scenario: **Private appointment stays a busy block.**
  - Dan creates "Discussion with HR — retention offer", marks it Private, invites the HR director.
  - The bridge scans the calendar. Because `Sensitivity=2`, normalization never reads the body, attendees, or organizer; it writes a cache row with subject `"Private appointment"`, nulled location/organizer/attendees/body, empty categories, `is_redacted: true`, `protected_fields_available: false`, and intact start/end/busy-status/recurrence fields.
  - The agent later calls `list_calendar_window` in enhanced mode to propose meeting slots. The private item appears as an opaque busy block; the agent schedules around it. No mode switch, cache read, or shaping path can recover the withheld content, because it was never stored.
  - Dan's expectation is met: the meeting's existence and time are usable; its meaning is invisible.

- Scenario: **Safe mode is honestly labeled.**
  - The bridge runs in safe mode. A normal (non-sensitive) message is served via `get_message`: sender, recipients, resolved addresses, and preview are null, `protected_fields_available` is `false`, and `is_redacted` is `false` — suppression, not redaction. When the operator switches to enhanced mode, the same cached row serves full fields again without a re-scan.

## Acceptance Criteria

- [x] A message or event with `Sensitivity` 2 or 3 is stored and served with placeholder subject (`"Private message"` / `"Private appointment"`), all content, identity, attendee, and category fields withheld, `is_redacted: true`, and `protected_fields_available: false` — in both safe and enhanced modes.
- [x] A redacted private item remains usable as a busy block: times, busy status, meeting status, recurrence fields, ids, `sensitivity`, and `sensitivity_label` are preserved unchanged.
- [x] Private-item content is never ingested: for sensitive items the scanner does not read the body, resolve sender addresses, or enumerate recipients/attendees, and each redaction is logged by bridge id only.
- [x] Items with `Sensitivity` 0, 1, null, or out-of-range values are completely unaffected by redaction.
- [x] Safe mode suppresses the complete protected field set (message: body preview, sender name/email, resolved sender fields, to/cc; event: body preview/full, organizer, attendees, resources, categories) and signals it with `protected_fields_available: false` — without setting `is_redacted`.
- [x] `is_redacted` means exactly one thing — sensitivity redaction — and survives shaping in both modes.

## Non-Goals

- Retroactive correction of previously cached unredacted private items (corrected only by re-scan; deployment note in `spec.md`).
- Suppressing `Location` in safe mode (retained per issue #20's suppression table; it is nulled only by sensitivity redaction).
- Redaction driven by anything other than the Outlook `Sensitivity` integer (no keyword-, category-, or label-based inference).
- Cache schema changes, new RPC methods, or mode-configuration changes.
