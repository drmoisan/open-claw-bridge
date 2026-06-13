# `mailbridge-event-attendees-json` — User Story

- Issue: #71
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-06-13T10-31

## Story Statement

- As the OpenClaw agent consuming normalized calendar events, I want each event to carry its
  required, optional, and resource attendees as Graph-shaped JSON, so that I can reason about
  meeting participants without a separate Outlook round-trip.
- As an operator running the bridge in safe mode, I want attendee names and emails redacted, so
  that PII is not exposed when protected fields are disabled.

## Problem / Why

`EventDto` carries `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson`, but
`OutlookScanner.BuildEventDto` hardcodes all three to `null`. Downstream consumers (deferred from
#70) cannot see who is invited to a meeting. The fields must be populated from the COM
`AppointmentItem.Recipients` collection while respecting the bridge's redaction model.

## Personas & Scenarios

- Persona: OpenClaw agent (programmatic consumer)
  - who: an automated agent reading cached calendar events over HostAdapter HTTP.
  - what they care about: accurate, deterministic attendee data in a stable JSON shape.
  - constraints: consumes contract-shaped data only; never calls Outlook directly.
  - goals: classify attendees as required/optional/resource and read each name and email.
- Scenario: scanning a meeting with known attendees
  - who is acting: the bridge calendar scan on the dedicated STA thread.
  - trigger: a calendar poll normalizes an `AppointmentItem` that has recipients.
  - steps: enumerate `Recipients`, classify each by `Type` (1/2/3), serialize each group to
    `[{"name","email"}]`, populate the three `EventDto` fields.
  - obstacles/decisions: a recipient missing a name or SMTP address; a recipient with an
    out-of-range `Type`; safe mode requiring redaction.
  - expected outcome: in enhanced mode the three fields contain correct JSON; in safe mode they
    are null.

## Acceptance Criteria

- [x] A scan of a meeting with known attendees returns non-null `RequiredAttendeesJson`,
  `OptionalAttendeesJson`, and `ResourcesJson` (as applicable) with correct names and emails in
  enhanced mode.
- [x] Each populated field is a JSON array of `{"name","email"}` objects matching the Graph
  `emailAddress` shape (lowercase `name` and `email` keys, collection order preserved).
- [x] A unit test asserts the JSON structure per attendee type: `Type 1` -> required,
  `Type 2` -> optional, `Type 3` -> resource; recipients with other `Type` values are excluded.
- [x] Safe mode (`BridgeSettings.Mode == "safe"`) nulls all three attendee JSON fields via
  `ResponseShaper.ShapeEvent`, matching the existing redaction of `SenderName`/`SenderEmail`, and a
  unit test asserts this redaction.
- [x] A recipient missing a name or resolvable email emits an empty string for the missing value
  with both keys still present, covered by a unit test.
- [x] No `EventDto` contract shape change; line and branch coverage thresholds hold
  (line >= 85%, branch >= 75%) with no regression on changed lines.

## Non-Goals

- Reshaping `EventDto` or any contract record (covered by #72).
- Populating message `ToJson`/`CcJson` recipient fields (out of scope for this issue).
- Changing the SQLite schema or `CacheRepository` column set.
- Resolving Exchange DN-to-SMTP address translation beyond what the COM recipient surface provides.
