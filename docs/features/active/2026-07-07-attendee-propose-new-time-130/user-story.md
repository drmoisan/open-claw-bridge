# `2026-07-07-attendee-propose-new-time` — User Story

- Issue: #130
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Story Statement

- As the invited principal (the knowledge worker whose mailbox OpenClaw manages), I want
  my deterministic assistant to respond to a conflicting meeting I do not organize by
  proposing a better time on my behalf — but only when I have explicitly enabled
  calendar writes — so that conflicts on meetings I merely attend are surfaced to the
  organizer as a concrete counter-proposal without the agent ever modifying anyone's
  calendar directly.
- As the deterministic assistant (the OpenClaw scheduling worker), I want the attendee
  propose-new-time RPC to be gated, deduplicated, and fully audited — with no move-guard
  or `series_moves` interaction, because a proposal moves nothing — so that every
  meeting response I send is explainable after the fact and every ambiguous or
  disallowed situation resolves to "no write".
- As the operator responsible for rollout, I want flag-off behavior to be a faithful
  dry-run of the eventual write (same intent computation, logged and audited as
  `propose_new_time_disabled`), so that I can validate the agent's judgment from audit
  records before turning `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` on.

## Problem / Why

The OpenClaw deterministic agent can now perform an organizer-side calendar write
(F18 organizer-reschedule, #128: Graph `PATCH /users/{p}/events/{id}` behind
`ENABLE_ORGANIZER_RESCHEDULE`). The attendee-side counterpart is still missing: when the
principal is invited to a meeting they do not organize and the computed decision is to
propose a different time, the agent has no way to respond. Feature F12 (#109,
calendar-write-flags) already shipped the flag scaffolding for this path —
`AgentPolicyOptions.EnableAttendeeProposeNewTime` (binding
`OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`, default OFF) and the pure
predicate `CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)` — but no
production code consumes that gate. F19 delivers the attendee-side calendar-write RPC:
a Graph `POST /users/{p}/events/{id}/tentativelyAccept` carrying a `proposedNewTime`
body behind the `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` named feature flag (default OFF),
completing Epic D and the final feature of the openclaw-vision program.

## Personas & Scenarios

- Persona: **Paula, the principal (invited attendee).**
  - A manager whose mailbox the OpenClaw agent operates against with app-only Graph
    permissions; for this feature she is an invitee, not the organizer.
  - Cares about never appearing to accept, decline, or move a colleague's meeting
    without her knowledge; a wrongly sent meeting response damages trust more than a
    missed optimization.
  - Constraint: the organizer's event must allow new-time proposals
    (`allowNewTimeProposals`); if the organizer disabled them, no proposal may be
    attempted. Until she opts in via the flags, no write may be attempted at all.
  - Goal: when a meeting she is invited to conflicts with higher-priority work, the
    organizer receives a tentative-accept with a concrete alternative slot — her
    calendar and the organizer's calendar are never modified by the agent; only the
    organizer can apply the change.

- Persona: **The deterministic assistant (SchedulingWorker).**
  - Host-neutral domain code; all I/O behind the `ISchedulingService` and
    `IHostAdapterClient` seams; all time from an injected `TimeProvider`.
  - Cares about fail-closed semantics: any missing input, organizer-owned message,
    proposals-disallowed event, disabled flag, unsupported backend, or Graph error means
    no write. Mutual exclusivity with the F18 organizer path comes from the intent
    predicates (`IsOrganizer` true vs false), so a message triggers at most one of the
    two calendar-write evaluations.
  - Goal: exactly one auditable outcome per evaluated intent — `proposed_new_time`,
    `propose_new_time_failed`, `propose_new_time_disabled`, or `dedupe_skipped` — with
    original and proposed times recorded, and zero `series_moves` interaction in every
    branch (a proposal is not a move; the move-guard budget must stay untouched).

- Persona: **Dan, the operator.**
  - Owns flag rollout and the live-tenant verification runbook.
  - Constraint: no Azure/Exchange credentials exist in the development environment or
    CI; live verification is a documented human-interaction exception. The
    `Calendars.ReadWrite` grant is shared with F18 and may already be in place.
  - Goal: review `propose_new_time_disabled` dry-run audit rows in production, confirm
    the agent's intended proposals are correct, then enable the write per the runbook —
    with the global `CalendarWriteEnabled` kill switch available to shut the path off
    instantly.

- Scenario: **Dry-run today (flags off, the default).**
  - A scheduling message arrives for a meeting Paula is invited to but does not
    organize; the event allows new-time proposals; the pipeline computes a
    propose-new-time intent (proposed start from the first proposed slot, duration
    preserved from the original event).
  - The flag gate finds `AttendeeProposeNewTimeAllowed == false`.
  - The worker logs the intended proposal (original → proposed times) and writes a
    `propose_new_time_disabled` audit record with the four time columns populated.
  - No Graph request is sent, no write-path token is acquired, and no dedupe row is
    written. Outbound behavior is identical to today; the existing send path's and F18
    path's `ActingFlags` audit strings remain byte-identical.

- Scenario: **First real proposal (flags on).**
  - Dan has followed the runbook: `Calendars.ReadWrite` is granted, and both
    `CalendarWriteEnabled` and `EnableAttendeeProposeNewTime` are set.
  - The same intent passes the gate; the dedupe store has no prior
    `attendee-propose-new-time` record for this message.
  - The worker calls `ProposeNewMeetingTimeAsync`; the Graph adapter issues
    `POST /users/{paula}/events/{id}/tentativelyAccept` with exactly
    `sendResponse: true` and a `proposedNewTime` of UTC `start`/`end` pairs — the body
    structurally cannot rewrite the event, and no comment is sent.
  - Graph returns `202 Accepted` with no body; the worker audits `proposed_new_time`
    and records the dedupe key. Nothing is written to `series_moves`. The organizer
    receives the proposal on their copy of the response; applying it remains the
    organizer's decision. If the same message is re-evaluated after a restart, the
    outcome is `dedupe_skipped` with no second write.

- Scenario: **Ineligible message (fail closed, silent).**
  - The message concerns a meeting Paula organizes (`IsOrganizer == true`), or the
    organizer disabled new-time proposals (`AllowNewTimeProposals == false`), or the
    event lacks original times, an event id, or any proposed slot.
  - `ComputeProposeNewTimeIntent` yields no intent; the evaluation returns silently
    with no audit row and no write — identical to today's behavior. Organizer-owned
    messages are handled exclusively by the F18 reschedule path.

- Scenario: **Graph error / unsupported backend.**
  - Graph returns 400 (server-side `allowNewTimeProposals` is false despite the client
    check) or 403 (`ErrorAccessDenied`, e.g. consent revoked): the adapter maps per the
    D5 matrix, the service throws, and the worker audits `propose_new_time_failed` —
    with no dedupe row, so a later retry is possible. On the local Stage-0 adapter, the
    call fails closed with `NOT_SUPPORTED` and no HTTP I/O at all.

## Acceptance Criteria

- [x] AC-1 (gate truth table): With `CalendarWriteEnabled=true` and
      `EnableAttendeeProposeNewTime=true`, an eligible attendee-side propose-new-time intent
      produces exactly one Graph `POST /users/{p}/events/{id}/tentativelyAccept`; each of the
      other three flag combinations produces zero Graph write requests and zero write-path
      token acquisitions. Proven by worker unit tests over the four-row truth table.
- [x] AC-2 (flag-off no-behavior-change): With defaults (both flags false), an eligible
      intent produces no Graph request and no sent-action (dedupe) row; the intended proposal
      is logged and audited as `propose_new_time_disabled` with
      `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc` populated; the existing
      send path's and the F18 reschedule path's persisted audit `ActingFlags` strings are
      byte-identical to pre-F19.
- [x] AC-3 (eligibility fail-closed matrix): An organizer-owned message
      (`IsOrganizer == true`), `AllowNewTimeProposals == false`, missing original
      `Start`/`End`, an empty event id, or zero proposed slots each yields no intent — silent
      return with no audit row and no Graph write — proven by a worker-test matrix.
- [x] AC-4 (successful write): A permitted write issues Graph
      `POST /users/{p}/events/{id}/tentativelyAccept` (URL-escaped principal and event id)
      with bearer auth, `client-request-id` equal to the evaluation's correlation id, and a
      body containing exactly `sendResponse: true` and a `proposedNewTime` with `start`/`end`
      dateTimeTimeZone pairs (UTC, seconds precision) and no other properties; on
      `202 Accepted` (empty body) it emits exactly one `proposed_new_time` audit record
      (action type `attendee-propose-new-time`, four time columns populated) and records the
      dedupe key, with zero `ISeriesMoveHistory.RecordMoveAsync` calls and no `series_moves`
      row.
- [x] AC-5 (fail-closed on adapter/Graph error): Error responses map per the D5 matrix
      (400/401/403/404 non-retryable with Graph `error.code` passthrough; 429 and 502/503/504
      retried then `THROTTLED`/`TRANSPORT_FAILURE`); any failure envelope or exception yields
      a `propose_new_time_failed` audit record (durable before the exception rethrows) and no
      dedupe row; the local Stage-0 adapter returns a non-retryable `NOT_SUPPORTED` envelope
      with zero HTTP I/O and zero token acquisitions.
- [x] AC-6 (idempotency): A second evaluation of the same message after a successful write
      audits `dedupe_skipped` and issues no Graph request (dedupe key
      `{mailbox}:{messageId}:attendee-propose-new-time`).
- [x] AC-7 (mocked-Graph contract tests): The contract suite
      (`GraphHostAdapterClientProposeNewTimeTests`) exists using the established
      `FakeHttpHandler` pattern (base `https://graph.example.test/v1.0/`) and covers
      method/URL/headers, exact body shape including absent-property guardrail assertions
      (only `sendResponse` and `proposedNewTime` at top level; no `comment` and no top-level
      `start`/`end`/`body`/`subject`/`attendees`), 202-empty-body → `ok: true, data: null`,
      D5 error samples, and 429 retry exhaustion under `FakeTimeProvider`.
- [x] AC-8 (quality gates): Line coverage >= 85% and branch coverage >= 75% are maintained;
      >= 1 CsCheck property test per new pure function (`ComputeProposeNewTimeIntent`,
      `BuildProposeNewTimeActingFlags`); architecture-boundary tests pass unmodified; worker
      tests assert mutual exclusivity with the F18 organizer path (an organizer-owned message
      never triggers the propose path and an attendee message never triggers the reschedule
      path).
- [x] AC-9 (live-verification runbook): The runbook
      `docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`
      exists, and live-tenant verification is recorded in orchestrator state as a
      `human_interaction` requirement with `response: exception` and that `runbook_path`.

## Non-Goals

- No live Graph write in automated verification; no Azure/Exchange credentials exist in
  this environment or CI. Live-tenant verification (permission-grant confirmation,
  two-mailbox observed proposal) is a recorded `human_interaction` exception with the
  runbook above.
- `SendOnBehalfAuthorizer` (F15/#119) is not consulted: the POST targets the
  principal's own event under app-only `Calendars.ReadWrite`, so no mailbox
  representation occurs.
- No decline path: the agent's decision is "can attend, at a different time," which
  tentative-accept represents; decline would remove the meeting from the attendee's
  calendar and signal non-attendance.
- No general event update: the attendee path never patches the event; the body carries
  only `sendResponse` and `proposedNewTime` — `comment`, `body`, `subject`,
  `attendees`, and top-level `start`/`end` are structurally excluded.
- No move guard, no blocked result code, and no `series_moves` interaction: a proposal
  moves nothing; idempotency is bounded by the per-message dedupe key.
- No local COM-bridge meeting-response route; the Stage-0 adapter fails closed.
- No audit-contract or database schema change; no worker constructor change; no
  production flag rollout decision.
