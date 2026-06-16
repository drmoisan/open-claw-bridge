# mailbridge-messagedto-resolved-fields — Spec

- **Issue:** #73
- **Parent (optional):** Deferred from #70; final issue of Track M (#72 -> #71 -> #73)
- **Owner:** drmoisan
- **Last Updated:** 2026-06-13T13-34
- **Status:** Approved
- **Version:** 1.0

## Overview

Extend `MessageDto` so a normalized Outlook message carries the fields required by Master
Section 9.2 `NormalizedMeetingContext` (messageSender, messageFrom, conversationId,
isMeetingMessage). Resolve all values from the message source object during normalization in
`OpenClaw.MailBridge`, populate the two recipient-JSON fields that are presently hardcoded null,
and propagate the new fields through the scheduling DTO mapper and both SQLite cache repositories.

- Target users/personas: the OpenClaw agent deterministic scheduling core (consumes
  `NormalizedMeetingContext`); downstream meeting-context normalization.
- Success metric: a meeting-request `MessageDto` exposes a valid SMTP sender, a non-null To JSON
  array, and a non-empty ConversationId, with unit coverage of both meeting and ordinary-mail paths.

## Locked Design Decisions (operator-confirmed 2026-06-13)

- **D-A (FromEmailAddress semantics):** `FromEmailAddress` resolves the on-behalf-of/delegate
  identity. Read `SentOnBehalfOfEmailAddress` (SMTP-resolved via the same mechanism as the sender);
  when the message is not delegate-sent, fall back to the resolved sender. Do not alias
  `SenderEmailResolved` outright — the field must reflect From vs Sender distinction per Master 9.2.
- **D-B (MeetingMessageType type):** `MeetingMessageType` is `int?` carrying the raw COM
  `OlMeetingType` value (0=request, 1=cancellation, 2=declined, 3=accepted, 4=tentative), null for
  ordinary mail. This matches the existing `int?` convention for `Importance`, `Sensitivity`,
  `BusyStatus`, `MeetingStatus`, `ResponseStatus`. Graph-vocabulary string mapping happens in
  `SchedulingDtoMapper` (replacing the current hardcoded `"meetingRequest"`), keeping the bridge
  contract a faithful projection of the source model.
- **D-C (SMTP resolution depth):** SMTP resolution is fail-soft and attempts a true SMTP address
  (PropertyAccessor `PR_SMTP_ADDRESS` or `GetExchangeUser().PrimarySmtpAddress`) inside try/catch,
  falling back to `AddressEntry.Address`, then the raw `SenderEmailAddress`/`SentOnBehalfOf`
  value. This is required so internal Exchange senders (whose `AddressEntry.Address` is a legacy
  Exchange DN, not SMTP) satisfy the acceptance signal. Non-Exchange and resolution failures degrade
  gracefully without throwing.
- **D-D (model-agnostic adapter seam — operator directive):** Field resolution must not bind core
  normalization logic to the concrete Outlook COM types. Introduce a unifying interface expressing
  the data the normalizer needs (resolved sender SMTP, resolved from SMTP, recipients with
  name/email/type, conversation id, meeting type) and a COM data-type adapter that maps the COM
  `MailItem`/`MeetingItem` onto that interface. The interface and the COM adapter stay inside
  `OpenClaw.MailBridge` (COM remains confined per architecture-boundaries rule 1). A future
  Modern/Microsoft Graph model is enabled by adding a second adapter only — core normalization, the
  DTO mapper, and cache repositories must not require a rewrite. Keep the seam minimal and
  purpose-specific per the C# DI-seam guidance.

## Behavior

End-to-end: during inbox/calendar scanning, `OutlookScanner` normalization obtains a message-source
abstraction (via the COM adapter), then builds `MessageDto` with:

- `SenderEmailResolved` — fail-soft SMTP resolution of the sender (D-C).
- `FromEmailAddress` — fail-soft SMTP resolution of the on-behalf-of identity, fallback to resolved
  sender (D-A).
- `ToJson` / `CcJson` — recipient arrays serialized as `[{"name":"...","email":"..."}]` using the
  same serializer/options/DTO shape as the #71 attendee JSON. To = recipient type 1, Cc = type 2.
  Email values are SMTP-resolved via the same fail-soft mechanism.
- `ConversationId` — the source `ConversationID` string, unmodified.
- `MeetingMessageType` — raw `OlMeetingType` int for meeting items; null for ordinary mail (D-B).

Edge/error behavior: any single field that cannot be resolved degrades to null (or to the documented
fallback for the sender) without aborting normalization. COM objects acquired during resolution are
released deterministically in `finally`.

## Inputs / Outputs

- Inputs: Outlook message source objects, accessed only through the new unifying interface.
- Outputs: enriched `MessageDto`; persisted columns in both SQLite caches; mapped
  `NormalizedMeetingContext` fields.
- Backward-compatibility: new `MessageDto` fields are appended as trailing optional parameters with
  defaults (mirrors the #72 `EventDto` additive-evolution pattern). Existing positional construction
  sites remain valid.

## API / CLI Surface

- `MessageDto` gains `SenderEmailResolved` (`string?`), `FromEmailAddress` (`string?`),
  `ConversationId` (`string?`), `MeetingMessageType` (`int?`), all trailing optional with defaults.
  `ToJson` / `CcJson` keep their existing positions and become populated.
- New internal interface (e.g. `IMessageSource`) + COM adapter inside `OpenClaw.MailBridge`. No
  public API on downstream projects changes shape beyond the additive DTO fields.

## Data & State

- `CacheRepository` (bridge) and `CoreCacheRepository` (Core): add `sender_email_resolved`,
  `from_email_address`, `conversation_id`, `meeting_message_type` columns. `to_json` / `cc_json`
  columns already exist. Use the idempotent `PRAGMA table_info` migration guard pattern established by
  the events-schema migration (#71/#72). Update parameter binding, INSERT/UPSERT SQL, and readers.
- `SchedulingDtoMapper`: wire `SenderEmailResolved` -> Sender.Email, `FromEmailAddress` -> From.Email,
  `ConversationId` -> ConversationId (remove hardcoded null), `MeetingMessageType` int -> Graph string
  (replace hardcoded `"meetingRequest"`).

## Constraints & Risks

- COM confinement (architecture-boundaries rule 1): the interface and COM adapter live only in
  `OpenClaw.MailBridge`; `OpenClaw.Core` consumes contract-shaped data only.
- COM fragility: SMTP resolution and `GetExchangeUser()` can throw for non-Exchange accounts — all
  such calls are try/catch fail-soft with fallback.
- Tier classification: `OpenClaw.MailBridge` managed surface T2 / COM-confined surface T3;
  `OpenClaw.Core` T1. Uniform coverage applies: line >= 85%, branch >= 75%; zero regression on
  changed lines.
- File-size limit 500 lines; `CacheRepository.cs` (460) and `CoreCacheRepository.cs` (687, already
  over via partials) — keep additions in partial files; do not push any file over 500 lines.

## Implementation Strategy

- Add the unifying `IMessageSource` interface + COM adapter; route normalization through it.
- Extend `MessageDto`; populate ToJson/CcJson via the reused attendee serializer; resolve sender/from
  SMTP fail-soft; read ConversationID and MeetingType.
- Propagate through `SchedulingDtoMapper`, `CacheRepository`, `CoreCacheRepository` with idempotent
  schema migrations.
- Add unit tests for the meeting-message path and the ordinary-mail path using the reflection-based
  fake message/recipient doubles; extend fakes with the new members.
- No new third-party dependencies.

## Acceptance Criteria

- **AC-01:** `MessageDto` declares `SenderEmailResolved` (`string?`), `FromEmailAddress` (`string?`),
  `ConversationId` (`string?`), `MeetingMessageType` (`int?`) as trailing optional parameters with
  defaults; `ToJson`/`CcJson` retain their positions.
- **AC-02:** Normalization resolves `SenderEmailResolved` to a valid SMTP address for an Exchange
  internal sender (fail-soft per D-C), falling back gracefully when SMTP is unavailable.
- **AC-03:** `FromEmailAddress` reflects the on-behalf-of identity when present, else the resolved
  sender (D-A).
- **AC-04:** `ToJson` and `CcJson` are non-null JSON arrays of `{"name","email"}` for a message with
  recipients, using the same shape/serializer as the #71 attendee JSON; To=type 1, Cc=type 2.
- **AC-05:** `ConversationId` is populated (non-empty) from the source `ConversationID`.
- **AC-06:** `MeetingMessageType` carries the raw `OlMeetingType` int for a meeting-request item and
  is null for ordinary mail (D-B).
- **AC-07:** A meeting-request `MessageDto` simultaneously has `SenderEmailResolved` as valid SMTP,
  `ToJson` as a non-null JSON array, and `ConversationId` non-empty (issue acceptance signal).
- **AC-08:** Unit tests cover both the meeting-message path and the ordinary-mail path.
- **AC-09:** A unifying message-source interface and a COM data-type adapter exist within
  `OpenClaw.MailBridge`; core normalization, `SchedulingDtoMapper`, and both cache repositories depend
  on the abstraction, not on concrete COM types (D-D). COM remains confined to `OpenClaw.MailBridge`.
- **AC-10:** Both SQLite caches persist and read back all six fields via idempotent schema migrations;
  `SchedulingDtoMapper` maps all four scheduling-relevant fields (no hardcoded ConversationId or
  meeting type).
- **AC-11:** Full seven-stage toolchain passes; line coverage >= 85%, branch coverage >= 75%; no
  regression on changed lines; no new analyzer/nullable suppressions; no file exceeds 500 lines.

## Definition of Done

- [x] Acceptance criteria documented and mapped to tests or demos
- [x] Behavior matches acceptance criteria in all documented environments
- [x] Tests updated/added (unit/integration as applicable)
- [x] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [x] Toolchain pass completed (format → lint → type-check → test)
