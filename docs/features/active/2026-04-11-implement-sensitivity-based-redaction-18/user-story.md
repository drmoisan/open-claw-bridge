# `2026-04-11-implement-sensitivity-based-redaction` — User Story

- Issue: #18
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-11T11-01

## Story Statement

- As a ..., I want ..., so that ...
- As a ..., I want ..., so that ...

## Problem / Why

The `Sensitivity` integer field is read from Outlook items and stored in the SQLite cache for both messages and events, but its value is never inspected. Items flagged as Private (`Sensitivity=2`) or Confidential (`Sensitivity=3`) are returned to callers with their full field set — subject, sender name, sender email, recipient lists, body preview, location, organizer, and attendees — exposing information that Outlook explicitly marks as restricted.

Additionally, `is_redacted` is currently set to `true` in safe mode for all items regardless of sensitivity, conflating two distinct concepts: mode-based field suppression (a run-mode policy) and sensitivity-based content redaction (a per-item privacy property). This conflation obscures the true reason a response was modified and makes the flag unreliable as a caller-facing signal.


## Personas & Scenarios

- Persona: ...
  - who the user is
  - what they care about
  - their constraints
  - their goals and frustrations
  - their context and motivations
- Scenario: ...
  - A concrete, step-by-step narrative that describes how a user accomplishes a goal in a real-world context using the system.
  - who is acting?
  - what triggered the action?
  - what steps do they take?
  - what obstacles or decisions occur?
  - what outcome do they expect?


## Acceptance Criteria

- [ ] `NormalizeMessage` sets subject to `"Private message"`, nulls `SenderName`, `SenderEmail`, `ToJson`, `CcJson`, `BodyPreview`, and sets `IsRedacted = true` and `ProtectedFieldsAvailable = false` when `Sensitivity` is 2.
- [ ] `NormalizeMessage` applies the same redaction when `Sensitivity` is 3.
- [ ] `NormalizeMessage` does not alter any fields when `Sensitivity` is 0, 1, or `null`.
- [ ] `NormalizeEvent` sets subject to `"Private appointment"`, nulls `Location`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, `BodyPreview`, and sets `IsRedacted = true` and `ProtectedFieldsAvailable = false` when `Sensitivity` is 2.
- [ ] `NormalizeEvent` applies the same redaction when `Sensitivity` is 3.
- [ ] `NormalizeEvent` does not alter any fields when `Sensitivity` is 0, 1, or `null`.
- [ ] The `Sensitivity` integer value is preserved in the DTO after redaction (callers can still observe the reason).
- [ ] `ResponseShaper.ShapeMessage` and `ResponseShaper.ShapeEvent` do not set `IsRedacted = true` in the safe-mode path; that flag is only set by sensitivity-based redaction at normalization time.
- [ ] The safe-mode path in `ResponseShaper` continues to suppress mode-governed fields (body preview, sender fields for messages; body preview for events) independently of the sensitivity redaction path.
- [ ] Items with `is_redacted = true` returned by `list_recent_messages`, `get_message`, `list_calendar_window`, and `get_event` contain only the retained fields enumerated above; no protected field is populated.


## Non-Goals

Call out what is explicitly excluded from this feature.
