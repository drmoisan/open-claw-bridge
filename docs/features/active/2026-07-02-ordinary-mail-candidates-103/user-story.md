# `ordinary-mail-candidates` — User Story

- Issue: #103
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-02

## Story Statement

- As a mailbox owner, I want ordinary scheduling emails ("can we move this?", "what works next week?") to be triaged by the assistant, so that reschedule requests sent as plain mail are handled instead of being handled only when they arrive as formal meeting invites.
- As a mailbox owner, I want the assistant to connect a plain email to the calendar event it is talking about, so that its decisions use the real meeting context — attendees, recurrence, sensitivity, and protected status — rather than the email text alone.
- As a mailbox owner, I want scheduling mail that matches no calendar event to still be triaged safely from the message alone, so that the assistant neither errors out nor acts on the wrong meeting.

## Problem / Why

The master specification defines two input classes (`docs/open-claw-approach.master.md` §2.2): formal meeting traffic and **ordinary scheduling mail** ("can we move this?", "what works next week?"). Today only the first class is handled: `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` (`src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`) selects only `item_kind = 'meeting'` messages, and `HostAdapterSchedulingService.GetEventForMessageAsync` does a direct event-by-id lookup returning null on a miss — there is no `calendarView`-window fallback with the subject/attendee-overlap matching heuristic the master specifies (§9.2 `chooseMostLikelyRelatedEvent`, §9.3 step 3). Ordinary scheduling threads are never triaged. Identified as gap F7 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

From the mailbox owner's perspective the effect is simple: most real-world rescheduling conversations happen in plain email, and the assistant currently ignores every one of them.

## Personas & Scenarios

- Persona: **The mailbox owner** (an executive or manager whose calendar the assistant coordinates)
  - Receives a steady stream of scheduling-related plain email alongside formal invites.
  - Cares that reschedule requests are noticed promptly and answered consistently, without personally re-reading every thread.
  - Constraint: trusts the assistant only if it is deterministic and conservative — it must never confuse one meeting with another, act on private meetings, or send anything while the send kill switch is off.
  - Frustration today: a colleague's "can we push Thursday's budget review?" email produces no assistant activity at all, while the same request sent as a formal proposal would be triaged.

- Scenario: **Reschedule request as plain mail, matching event found**
  - A direct report emails the owner: subject "Budget review Thursday", body "Can we move this to later in the week?". It is ordinary mail — no invite attached, no linked event.
  - On the next scheduling cycle the assistant now includes this message as a candidate (previously it was filtered out as non-meeting mail).
  - The direct event lookup misses, so the assistant reads the next 14 days of the calendar and scores each event: the "Budget Review" event shares two long subject tokens with the message and the sender is a required attendee, comfortably clearing the acceptance threshold.
  - The assistant hydrates the meeting context from that event — exactly as it would for a formal invite — then runs the standard triage, priority, and slot-proposal pipeline. With sends enabled, the requester receives a proposal reply; with sends disabled, the decision is computed and logged only.
  - Obstacle handled deterministically: if two events tie on score, the assistant always picks the same one (earliest start, then id), so repeated cycles never flip-flop.

- Scenario: **Scheduling mail with no matching event**
  - A partner emails "what works next week for a first sync?" — there is no existing calendar event to match, so the calendar-window search finds nothing above the threshold.
  - The assistant proceeds with message-only context: triage still classifies the item (for example `HUMAN_APPROVAL` for an external sender), and nothing errors or silently stalls.
  - Outcome the owner expects: no wrong-meeting guesses — a below-threshold match is treated as no match.

- Scenario: **Adapter hiccup during the calendar read**
  - The calendar-window read fails or returns nothing. The assistant degrades to message-only triage for that message and continues the cycle; one bad item never halts the loop.

## Acceptance Criteria

- [x] `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` returns ordinary (`item_kind = 'mail'`) messages alongside meeting messages (repository kind `"all"`), preserving the existing lookback window, limit, and ordering; formal meeting-traffic handling is unchanged; covered by a new unit test against an in-memory SQLite `CoreCacheRepository`.
- [x] A new pure static `RelatedEventMatcher` in `OpenClaw.Core.Agent` ports §9.2 `chooseMostLikelyRelatedEvent` exactly: distinct lowercase subject tokens (split on non-alphanumeric runs, length >= 4) score +2 per token also present in the event subject; distinct normalized participant emails (from, sender, to, cc) score +3 per email present in the event attendee set (required + optional + resource); the best event is returned only when its score >= 4; ties resolve deterministically by earliest `Start` (null last) then ordinal `Id`; unit tests cover subject-only, attendee-only, combined, sub-threshold, empty-input, and tie-break rows.
- [x] CsCheck property/invariant tests cover the matcher: the result is null or scores >= 4; the selected event is invariant under permutation of the input event list; scoring is case-insensitive; duplicate tokens/emails do not double-count (set semantics).
- [x] When `GetEventForMessageAsync` returns null, `SchedulingWorker.ProcessMessageAsync` fetches `ISchedulingService.GetCalendarViewAsync(now, now + CalendarViewFallbackDays)` and applies the matcher; `CalendarViewFallbackDays` is a new `AgentPolicyOptions` value defaulting to 14; `now` comes from the injected `TimeProvider` (no wall-clock APIs); a non-positive configured value skips the fallback; verified with mocked `ISchedulingService` and `FakeTimeProvider`.
- [x] No-match (empty window, all scores < 4, or a failure envelope mapped to an empty window) falls through to message-only triage (`MeetingContextNormalizer.Normalize(mailboxUpn, message, null)`) without a new exception path; a matched event hydrates the context through the same `Normalize` call formal traffic uses.
- [x] Widened candidates are re-listed every cycle (no candidate cursor exists); outbound proposal sends remain deduplicated across cycles by the #101 sent-action store with the unchanged key shape — verified by extending the existing dedupe tests with an ordinary-mail scenario.
- [x] Full C# toolchain passes (CSharpier, analyzers, build, architecture, tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; all touched files remain <= 500 lines.

## Non-Goals

- No natural-language or model-based matching; the matcher is the fixed-weight, fixed-threshold deterministic heuristic from master §9.2. "Timing clues" mentioned in §2.2 are not scored — the §9.2 reference implementation scores subject and participants only, and this feature ports it exactly.
- No calendar writes and no changes to the send path, kill switches, or the #101 dedupe key shape.
- No `Prefer: outlook.body-content-type="text"` header work: the Local MVP event surface already returns plain-text previews only; the header is a Product Increment 1 (Graph client) concern.
- No message-to-event linkage for formal traffic (deferred bridge work #71–#76) — the fallback compensates for misses but does not implement linkage.
- No candidate cursor, backpressure, or per-kind quota redesign; re-listing the lookback window each cycle remains the candidate model.
- No changes to the Graph-shaped HostAdapter surface or MailBridge RPC methods (`list_calendar_window` is consumed as-is).
