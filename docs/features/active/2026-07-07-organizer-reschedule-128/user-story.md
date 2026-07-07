# `2026-07-07-organizer-reschedule` — User Story

- Issue: #128
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Story Statement

- As the meeting owner (the principal whose mailbox OpenClaw manages), I want my
  deterministic assistant to execute the reschedule it computed for an event I organize
  as a real calendar write — but only when I have explicitly enabled calendar writes —
  so that low-priority conflicts are resolved on my actual calendar without me granting
  the agent standing, ungated write authority.
- As the deterministic assistant (the OpenClaw scheduling worker), I want the first
  calendar-write RPC to be gated, guard-checked, deduplicated, and fully audited, so
  that every write I perform is explainable after the fact and every ambiguous or
  disallowed situation resolves to "no write".
- As the operator responsible for rollout, I want flag-off behavior to be a faithful
  dry-run of the eventual write (same intent computation, same guard decision, logged
  and audited), so that I can validate the agent's judgment from audit records before
  turning `ENABLE_ORGANIZER_RESCHEDULE` on.

## Problem / Why

The OpenClaw deterministic agent computes calendar reschedule decisions but has never
performed a real calendar write. Feature F12 (#109, calendar-write-flags) shipped the
policy scaffolding — the global `CalendarWriteEnabled` kill switch, the per-path
`EnableOrganizerReschedule` flag, and the pure `CalendarWritePolicy.OrganizerRescheduleAllowed`
composition predicate — and Feature F8 (#105) shipped the `OneOnOneMoveGuard` /
`ISeriesMoveHistory` move-history seam explicitly for this feature, but no production
code consumes either. F18 delivers the first real calendar-write RPC: an organizer
reschedule realized as a Microsoft Graph `PATCH /users/{p}/events/{id}` behind the
`ENABLE_ORGANIZER_RESCHEDULE` named feature flag (default OFF).

## Personas & Scenarios

- Persona: **Paula, the principal (meeting owner).**
  - A manager whose mailbox the OpenClaw agent operates against with app-only Graph
    permissions.
  - Cares about her calendar never being modified silently or incorrectly; a wrong move
    of a customer meeting costs more than a missed optimization.
  - Constraint: she has not yet granted `Calendars.ReadWrite`; until an admin does, no
    write can succeed, and until she opts in via the flags, no write may be attempted.
  - Goal: recurring one-on-ones she organizes get moved out of the way of higher-priority
    meetings automatically — but a given one-on-one is not shuffled repeatedly (the
    move-history guard limits moves to fewer than two in the last six occurrences and
    none in the previous week).

- Persona: **The deterministic assistant (SchedulingWorker).**
  - Host-neutral domain code; all I/O behind the `ISchedulingService` and
    `IHostAdapterClient` seams; all time from an injected `TimeProvider`.
  - Cares about fail-closed semantics: any missing input, guard rejection, disabled
    flag, unsupported backend, or Graph error means no write.
  - Goal: exactly one auditable outcome per evaluated intent — `rescheduled`,
    `reschedule_failed`, `reschedule_disabled`, `reschedule_blocked`, or
    `dedupe_skipped` — with original and new times recorded.

- Persona: **Dan, the operator.**
  - Owns flag rollout and the live-tenant verification runbook.
  - Constraint: no Azure/Exchange credentials exist in the development environment or
    CI; live verification is a documented human-interaction exception.
  - Goal: review `reschedule_disabled` dry-run audit rows in production, confirm the
    agent's intended moves are correct, then enable the write per the runbook — with the
    global `CalendarWriteEnabled` kill switch available to shut the path off instantly.

- Scenario: **Dry-run today (flags off, the default).**
  - A scheduling message arrives for a one-on-one Paula organizes; the pipeline computes
    a reschedule intent (new start from the first proposed slot, duration preserved).
  - The move guard is consulted first and allows the move; the flag gate then finds
    `OrganizerRescheduleAllowed == false`.
  - The worker logs the intended move (old → new times) and writes a
    `reschedule_disabled` audit record with the four time columns populated.
  - No Graph request is sent, no write-path token is acquired, no move-history row and
    no dedupe row are written. Outbound behavior is identical to today.

- Scenario: **First real write (flags on).**
  - Dan has followed the runbook: the tenant admin granted `Calendars.ReadWrite`, and
    both `CalendarWriteEnabled` and `EnableOrganizerReschedule` are set.
  - The same intent passes the guard and the gate; the dedupe store has no prior
    `organizer-reschedule` record for this message.
  - The worker calls `RescheduleEventAsync`; the Graph adapter issues
    `PATCH /users/{paula}/events/{id}` with exactly `start` and `end` dateTimeTimeZone
    pairs (UTC) — the body structurally cannot touch the online-meeting blob.
  - On 200, the worker audits `rescheduled`, records the pre-move occurrence start in
    move history, and records the dedupe key. If the same message is re-evaluated after
    a restart, the outcome is `dedupe_skipped` with no second write.

- Scenario: **Guard block despite flags on.**
  - Paula's weekly one-on-one has already been moved twice within the last six
    occurrences. Both flags are on.
  - The guard rejects the move; the worker audits `reschedule_blocked` and performs no
    write. The flags cannot override the guard.

- Scenario: **Graph error / unsupported backend.**
  - Graph returns 403 (`ErrorAccessDenied`, e.g. consent revoked): the adapter maps it
    to a non-retryable `UNAUTHORIZED` envelope, the service throws, and the worker
    audits `reschedule_failed` — with no move-history or dedupe row, so a later retry is
    possible. On the local Stage-0 adapter, the call fails closed with `NOT_SUPPORTED`
    and no HTTP I/O at all.

## Acceptance Criteria

- [x] AC-1 (gate truth table): With `CalendarWriteEnabled=true` and
      `EnableOrganizerReschedule=true`, an eligible organizer-owned reschedule intent
      produces exactly one Graph `PATCH /users/{p}/events/{id}`; each of the other three
      flag combinations produces zero Graph write requests and zero write-path token
      acquisitions. Proven by worker unit tests over the four-row truth table.
- [x] AC-2 (flag-off no-behavior-change): With defaults (both flags false), an eligible
      intent produces no Graph request, no `series_moves` row, and no sent-action row;
      the intended reschedule is logged and audited as `reschedule_disabled` with
      `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc` populated; the
      existing send path and its persisted audit `ActingFlags` string are unchanged from
      pre-F18.
- [x] AC-3 (move-guard block): A `ONE_ON_ONE` intent whose move history violates the
      rolling-six/previous-week rule is blocked with a `reschedule_blocked` audit record
      and no Graph write, even when both flags are on; the guard is consulted before the
      flag gate so dry-run audits reflect the true guard decision.
- [x] AC-4 (successful write): A permitted write issues Graph
      `PATCH /users/{p}/events/{id}` with bearer auth, `client-request-id` equal to the
      evaluation's correlation id, and a body containing exactly `start` and `end`
      dateTimeTimeZone pairs (UTC, seconds precision) and no other properties; on 200 it
      emits exactly one `rescheduled` audit record (action type `organizer-reschedule`,
      four time columns populated), records the pre-move occurrence start via
      `ISeriesMoveHistory.RecordMoveAsync`, and records the dedupe key.
- [x] AC-5 (fail-closed on adapter/Graph error): Error responses map per the D5 matrix
      (400/401/403/404 non-retryable with Graph `error.code` passthrough; 429 and
      502/503/504 retried then `THROTTLED`/`TRANSPORT_FAILURE`); any failure envelope or
      exception yields a `reschedule_failed` audit record and no move-history or dedupe
      bookkeeping; the local Stage-0 adapter returns a non-retryable `NOT_SUPPORTED`
      envelope with zero HTTP I/O.
- [x] AC-6 (idempotency): A second evaluation of the same message after a successful
      write audits `dedupe_skipped` and issues no Graph request.
- [x] AC-7 (mocked-Graph contract tests): The contract suite
      (`GraphHostAdapterClientRescheduleEventTests`) exists using the established
      `FakeHttpHandler` pattern and covers method/URL/headers, exact body shape
      including absent-property guardrail assertions, 200→`EventDto` mapping, D5 error
      samples, and 429 retry exhaustion under `FakeTimeProvider`.
- [x] AC-8 (quality gates): Line coverage >= 85% and branch coverage >= 75% are
      maintained; >= 1 property test per new pure function (intent computation, flags
      snapshot); architecture-boundary tests pass with no domain→adapter reference
      introduced.
- [x] AC-9 (live-verification runbook): The runbook
      `docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md`
      exists, and live-tenant verification is recorded in orchestrator state as a
      `human_interaction` requirement with `response: exception` and that
      `runbook_path`.

## Non-Goals

- No live Graph write in automated verification; no Azure/Exchange credentials exist in
  this environment or CI. Live-tenant verification (permission grant, admin consent,
  observed move) is a recorded `human_interaction` exception with the runbook above.
- `SendOnBehalfAuthorizer` (F15/#119) is not consulted: the PATCH targets the
  principal's own calendar, so no mailbox representation occurs.
- No attendee propose-new-time flow for meetings the principal does not organize — that
  is F19.
- No general event update: only `start`/`end` are writable; `body`, `subject`,
  `location`, and `attendees` are structurally excluded (online-meeting-blob guardrail).
- No local COM-bridge calendar-write route; the Stage-0 adapter fails closed.
- No audit-contract or database schema change; no production flag rollout decision.
