# `message-to-event-linkage` — User Story

- Issue: #146
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-11T23-15
- Work Mode: full-feature

## Story Statement

- As the OpenClaw scheduling pipeline (the `SchedulingWorker` triage path), I want to resolve
  the exact calendar event a meeting-related message is linked to, so that triage decisions
  are based on the real linked appointment instead of only the lossy calendar-view heuristic.
- As a maintainer of the deterministic scheduling pipeline, I want an unlinked message to
  resolve to a clean `null` rather than an error, so that ordinary mail and unmatched messages
  degrade to the existing fallback without polluting error telemetry or emitting spurious
  4xx/5xx responses.

## Problem / Why

The scheduling pipeline cannot answer "which calendar event is this message linked to?"
`HostAdapterSchedulingService.GetEventForMessageAsync` is a stand-in that forwards the
`messageId` to `GetEventAsync`, a plain event-by-id lookup. Because a message bridge id
(`msg:`/`mtg:` prefix) is never a valid event bridge id (`evt:` prefix), the direct lookup
always misses, and the pipeline depends entirely on the heuristic calendar-view fallback
(`ChooseRelatedEventFromWindowAsync`) — which is lossy and only runs when
`CalendarViewFallbackDays > 0`. No MailBridge RPC and no HostAdapter route exist to perform
real linkage; the wire contract for that operation does not exist.

The value of closing this gap is more accurate triage: when a meeting-related message is
directly linked to its appointment, the pipeline can resolve that appointment deterministically
instead of guessing from a time window, improving the correctness of scheduling decisions.

## Personas & Scenarios

- **Persona: the scheduling pipeline (automated consumer).**
  - Who: the `SchedulingWorker.ProcessMessageAsync` path that classifies incoming messages and
    associates meeting-related mail with calendar events.
  - What it cares about: correctly identifying the linked event for a meeting message;
    deterministic, non-flaky behavior; graceful degradation when no link exists.
  - Constraints: must not throw or emit errors for the common unlinked case; must continue to
    honor the existing fallback; must run without a live Outlook/COM session in the RPC path.
  - Goals and frustrations: today it always falls back to window matching and never uses a
    direct link, so a correct link is silently unavailable.
  - Context and motivations: part of the openclaw-runtime-remediation epic (child A); the
    stand-in was explicitly deferred bridge work (#71-#76).

- **Scenario: a meeting invite arrives and is linked to its appointment.**
  - Who is acting: the scheduling pipeline processing a newly scanned meeting message.
  - What triggered it: the inbox+calendar scan stored the message's Clean Global Object ID
    (`GlobalAppointmentID`) as its linkage key, alongside the already-stored event.
  - Steps: the pipeline calls `GetEventForMessageAsync`; Core calls the new HostAdapter route;
    the route runs the new RPC; the RPC joins the message's linked key to
    `events.global_appointment_id` and returns the matching event.
  - Obstacles/decisions: for a recurring series the newest instance is chosen; a cancelled
    meeting still resolves to the underlying appointment if present.
  - Expected outcome: the pipeline receives the mapped event and skips the window fallback.

- **Scenario: an ordinary (non-meeting) or unmatched message is processed.**
  - Who is acting: the same pipeline processing a message with no linked appointment.
  - What triggered it: ordinary mail (no linkage key), or a meeting message whose linked key
    matches no stored event, or a message row not present in the cache.
  - Steps: the pipeline calls `GetEventForMessageAsync`; the RPC returns a clean not-linked
    success (`Success(null)`); the route returns `ok:true` / `data:null` / HTTP 200; Core maps
    it to `null`.
  - Obstacles/decisions: this must not be a 400 or 404 — it is normal degradation.
  - Expected outcome: the pipeline receives `null` and runs the existing calendar-view
    fallback exactly as it does today, with no error telemetry.

## Acceptance Criteria

- [ ] The scheduling pipeline resolves the linked calendar event for a meeting message through
      a new MailBridge RPC (`BridgeMethods.GetEventForMessage`), a new HostAdapter route
      (`GET /users/{id}/messages/{messageId}/event`), and a new
      `IHostAdapterClient.GetEventForMessageAsync` method.
- [ ] Linkage is resolved by joining the message's stored `LinkedGlobalAppointmentId` (the
      appointment `GlobalAppointmentID`) to `events.global_appointment_id`, selecting the
      newest instance for a recurring series.
- [ ] `HostAdapterSchedulingService.GetEventForMessageAsync` calls the new client method (not
      `GetEventAsync`) and returns the mapped event for a linked message.
- [ ] A linked hit causes `SchedulingWorker` to use the resolved event and skip the
      calendar-view window fallback.
- [ ] A genuinely unlinked message (ordinary mail, no matching event, or absent message row)
      yields a clean `null` result via an `ok:true` / `data:null` / HTTP 200 envelope — no
      HTTP 400 and no HTTP 404 — and the pipeline continues via its existing calendar-view
      fallback, adding no error telemetry.
- [ ] A malformed message bridge id surfaces as HTTP 400 (`INVALID_REQUEST`), distinct from
      the null-degradation path; bridge-not-ready remains HTTP 409.
- [ ] Both `HostAdapterHttpClient` and `GraphHostAdapterClient` implement the new method
      consistently with the null contract and satisfy `CloudGraphContractParityTests`.
- [ ] Coverage on changed C# code meets repo thresholds (line >= 85%, branch >= 75%) with no
      regression on changed lines, using MSTest + Moq + FluentAssertions.

## Non-Goals

- No live COM lookup at RPC time; the RPC resolves only from the bridge SQLite cache (linkage
  freshness is bounded by the last inbox+calendar scan).
- No `ConversationId`-based join (events store no conversation id and it is not a reliable 1:1
  key).
- No Core-side cache compute for linkage; the feature follows the Graph-shaped HostAdapter
  route + CLI process-runner pattern used by #74/#75/#76.
- No change to the calendar-view fallback logic or to `CalendarViewFallbackDays`; the fallback
  remains the safety net for any `null` result.
- No explicit backfill of the new linkage column for pre-existing rows; they repopulate on the
  next scan.
