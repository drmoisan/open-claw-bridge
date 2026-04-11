# `2026-04-11-complete-safe-mode-field-suppression` — User Story

- Issue: #20
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-11T11-03

## Story Statement

- As a ..., I want ..., so that ...
- As a ..., I want ..., so that ...

## Problem / Why

The safe-mode path in `ResponseShaper` suppresses `BodyPreview`, `SenderName`, and `SenderEmail` from messages, and `BodyPreview` from events, but leaves a broader set of protected fields populated. Fields containing recipient address lists (`ToJson`, `CcJson`), meeting participant identity (`Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`), and the `ProtectedFieldsAvailable` flag are returned as-is in safe mode, exposing information that the spec requires to be suppressed.

`ProtectedFieldsAvailable` is also never set to `false` in the safe-mode path, so callers have no reliable signal that suppress-on-read has occurred and cannot distinguish a suppressed-but-present field from a field that was simply never available from Outlook.

This is classified as a **Critical** deviation in the design audit (deviation #7, blocking exit criterion).


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

- [ ] `ResponseShaper.ShapeMessage` in safe mode sets `ToJson = null`.
- [ ] `ResponseShaper.ShapeMessage` in safe mode sets `CcJson = null`.
- [ ] `ResponseShaper.ShapeMessage` in safe mode sets `ProtectedFieldsAvailable = false`.
- [ ] `ResponseShaper.ShapeMessage` in safe mode continues to set `BodyPreview = null`, `SenderName = null`, `SenderEmail = null` (no regression).
- [ ] `ResponseShaper.ShapeMessage` in enhanced mode does not null `ToJson`, `CcJson`, or set `ProtectedFieldsAvailable = false`; original DTO values are preserved.
- [ ] `ResponseShaper.ShapeEvent` in safe mode sets `Organizer = null`.
- [ ] `ResponseShaper.ShapeEvent` in safe mode sets `RequiredAttendeesJson = null`.
- [ ] `ResponseShaper.ShapeEvent` in safe mode sets `OptionalAttendeesJson = null`.
- [ ] `ResponseShaper.ShapeEvent` in safe mode sets `ResourcesJson = null`.
- [ ] `ResponseShaper.ShapeEvent` in safe mode sets `ProtectedFieldsAvailable = false`.
- [ ] `ResponseShaper.ShapeEvent` in safe mode continues to set `BodyPreview = null` (no regression).
- [ ] `ResponseShaper.ShapeEvent` in enhanced mode does not null organizer or attendee fields; original DTO values are preserved.
- [ ] A `MessageDto` or `EventDto` with all protected fields already null (e.g., a freshly-scanned item where Outlook returned no data) is shaped without error in both modes.


## Non-Goals

Call out what is explicitly excluded from this feature.
