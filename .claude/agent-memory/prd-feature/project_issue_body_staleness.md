---
name: issue-body-staleness
description: April 2026 issue bodies predate the #71/#72/#73 DTO field additions; specs must reconcile field lists against current BridgeContracts.cs
metadata:
  type: project
---

GitHub issue bodies drafted 2026-04-11 (e.g. #18, #20) enumerate DTO field sets that are stale: issues #71/#72/#73 later added fields to `MessageDto` (SenderEmailResolved, FromEmailAddress, ConversationId, MeetingMessageType) and `EventDto` (ResponseStatus, Categories, IsOrganizer, IsOnlineMeeting, AllowNewTimeProposals, ICalUId, SeriesMasterId, LastModifiedDateTime, BodyFull, SensitivityLabel).

**Why:** Specs written verbatim from issue bodies would omit new sensitive fields (e.g. BodyFull) from redaction/suppression sets — a privacy gap. The sensitivity-redaction-18 spec records the field-by-field delta as the template for this.

**How to apply:** When writing spec.md from any pre-May-2026 issue body that enumerates DTO fields, re-derive the field list from `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` and record the delta explicitly in the spec. Also note: parts of old proposals may already be implemented (ShapeEvent already nulled BodyFull/attendees before #20 landed) — verify current code before restating requirements.
