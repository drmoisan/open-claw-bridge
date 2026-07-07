# attendee-propose-new-time (Issue #130)

- Date captured: 2026-07-07
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-07-07-attendee-propose-new-time-130/ (Issue #130)
- Epic: openclaw-vision (Epic D — Stage 2 Final Vision), feature F19, wave 5
- Depends on: organizer-reschedule (F18, #128)

- Issue: #130
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/130
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Problem / Why

The OpenClaw deterministic agent can now perform an organizer-side calendar write
(F18 organizer-reschedule: Graph `PATCH /events/{id}` behind `ENABLE_ORGANIZER_RESCHEDULE`).
The attendee-side counterpart is still missing: when the principal is invited to a meeting
they do not organize and the computed decision is to propose a different time, the agent has
no way to respond. Feature F12 (`#109`, calendar-write-flags) already shipped the flag
scaffolding for this path — `AgentPolicyOptions.EnableAttendeeProposeNewTime` (binding
`OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`, default OFF) and the pure predicate
`CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)` — but no production code consumes
that gate to issue a Microsoft Graph meeting-response write. F19 delivers the attendee-side
calendar-write RPC behind the named flag `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`, completing Epic D
and the final feature of the openclaw-vision program.

## Proposed Behavior

- Add an attendee propose-new-time calendar-write operation that, when gated on by
  `CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)`, issues a Graph meeting-response
  write (`POST /users/{principal}/events/{id}/tentativelyAccept` with body exactly
  `{ sendResponse: true, proposedNewTime: { start, end } }`, UTC dateTimeTimeZone pairs) to
  propose an alternative start/end time for a meeting the principal is invited to but does not
  organize. Success is `202 Accepted` with no response body.
- The flag defaults OFF. When `EnableAttendeeProposeNewTime` (or the global
  `CalendarWriteEnabled` kill switch) is false, there is no outbound behavior change: the
  deterministic pipeline still computes and logs the intended proposal and audits
  `propose_new_time_disabled`, but performs no Graph write, no write-path token acquisition,
  and no dedupe write (dry-run parity with today's behavior).
- Eligibility is the attendee-side mirror of F18: the hydrated event is present, the principal
  is NOT the organizer (`IsOrganizer == false`), the event allows new-time proposals
  (`AllowNewTimeProposals == true`), original `Start`/`End` are present, the event id is
  non-empty, and at least one proposed slot exists. Proposed start = first proposed slot's
  start; duration preserved from the original event. Any ambiguity resolves to "no write"
  (fail closed).
- Mutual exclusivity with the F18 organizer path is guaranteed by the intent predicates
  (`IsOrganizer` true vs false), not by pipeline branching. The attendee path has NO move
  guard and NO blocked result code: a proposal moves nothing, and `series_moves` must not be
  touched in any branch.
- Emit the proposal as an auditable action (new result codes `proposed_new_time`,
  `propose_new_time_failed`, `propose_new_time_disabled`; new dedupe action type
  `attendee-propose-new-time`; four time columns populated whenever an intent exists).
- No live tenant is available; the Graph write path ships as mocked-Graph contract tests behind
  the flag. Live verification is recorded as a human-interaction exception runbook (F11 HI-1 /
  F18 HI-1 precedent).

## Acceptance Criteria

- [ ] AC-1 (gate truth table): With `CalendarWriteEnabled=true` and
      `EnableAttendeeProposeNewTime=true`, an eligible attendee-side propose-new-time intent
      produces exactly one Graph `POST /users/{p}/events/{id}/tentativelyAccept`; each of the
      other three flag combinations produces zero Graph write requests and zero write-path
      token acquisitions. Proven by worker unit tests over the four-row truth table.
- [ ] AC-2 (flag-off no-behavior-change): With defaults (both flags false), an eligible
      intent produces no Graph request and no sent-action (dedupe) row; the intended proposal
      is logged and audited as `propose_new_time_disabled` with
      `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc` populated; the existing
      send path's and the F18 reschedule path's persisted audit `ActingFlags` strings are
      byte-identical to pre-F19.
- [ ] AC-3 (eligibility fail-closed matrix): An organizer-owned message
      (`IsOrganizer == true`), `AllowNewTimeProposals == false`, missing original
      `Start`/`End`, an empty event id, or zero proposed slots each yields no intent — silent
      return with no audit row and no Graph write — proven by a worker-test matrix.
- [ ] AC-4 (successful write): A permitted write issues Graph
      `POST /users/{p}/events/{id}/tentativelyAccept` (URL-escaped principal and event id)
      with bearer auth, `client-request-id` equal to the evaluation's correlation id, and a
      body containing exactly `sendResponse: true` and a `proposedNewTime` with `start`/`end`
      dateTimeTimeZone pairs (UTC, seconds precision) and no other properties; on
      `202 Accepted` (empty body) it emits exactly one `proposed_new_time` audit record
      (action type `attendee-propose-new-time`, four time columns populated) and records the
      dedupe key, with zero `ISeriesMoveHistory.RecordMoveAsync` calls and no `series_moves`
      row.
- [ ] AC-5 (fail-closed on adapter/Graph error): Error responses map per the D5 matrix
      (400/401/403/404 non-retryable with Graph `error.code` passthrough; 429 and 502/503/504
      retried then `THROTTLED`/`TRANSPORT_FAILURE`); any failure envelope or exception yields
      a `propose_new_time_failed` audit record (durable before the exception rethrows) and no
      dedupe row; the local Stage-0 adapter returns a non-retryable `NOT_SUPPORTED` envelope
      with zero HTTP I/O and zero token acquisitions.
- [ ] AC-6 (idempotency): A second evaluation of the same message after a successful write
      audits `dedupe_skipped` and issues no Graph request (dedupe key
      `{mailbox}:{messageId}:attendee-propose-new-time`).
- [ ] AC-7 (mocked-Graph contract tests): The contract suite
      (`GraphHostAdapterClientProposeNewTimeTests`) exists using the established
      `FakeHttpHandler` pattern (base `https://graph.example.test/v1.0/`) and covers
      method/URL/headers, exact body shape including absent-property guardrail assertions
      (only `sendResponse` and `proposedNewTime` at top level; no `comment` and no top-level
      `start`/`end`/`body`/`subject`/`attendees`), 202-empty-body → `ok: true, data: null`,
      D5 error samples, and 429 retry exhaustion under `FakeTimeProvider`.
- [ ] AC-8 (quality gates): Line coverage >= 85% and branch coverage >= 75% are maintained;
      >= 1 CsCheck property test per new pure function (`ComputeProposeNewTimeIntent`,
      `BuildProposeNewTimeActingFlags`); architecture-boundary tests pass unmodified; worker
      tests assert mutual exclusivity with the F18 organizer path (an organizer-owned message
      never triggers the propose path and an attendee message never triggers the reschedule
      path).
- [ ] AC-9 (live-verification runbook): The runbook
      `docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`
      exists, and live-tenant verification is recorded in orchestrator state as a
      `human_interaction` requirement with `response: exception` and that `runbook_path`.

## Constraints & Risks

- Attendee-side calendar write; must fail closed. Any ambiguity in the gate or eligibility
  resolves to "no write".
- No Azure/Exchange credentials in this environment or CI; the Graph write is exercised only
  through the mocked Graph seam. Live verification is a human-interaction exception (runbook
  required).
- Architecture boundaries (No-COM, layer boundaries) must hold; the write path lives in
  host-neutral domain code with the Graph call behind the `IHostAdapterClient` adapter seam in
  the `CloudGraph` namespace. Existing NetArchTest architecture-boundary tests must pass
  unmodified.
- Cross-module contract change (floor signal `cross_module_contract_change`): the write path
  threads options, the audit record, the service seam, and the Graph adapter contract together;
  both `IHostAdapterClient` implementations must be updated in the same change (Graph = real
  POST; local Stage-0 adapter = fail-closed `NOT_SUPPORTED`, no I/O), mirroring F18.
- The attendee path must not read or write `series_moves`; recording into it would corrupt the
  move-guard history for future organizer-side reschedules of the same series.

## Test Conditions to Consider

- [ ] Unit: gate truth table (both flags on/off combinations) yields write / no-write.
- [ ] Unit: eligibility fail-closed matrix (organizer-owned, proposals disallowed, missing
      times, empty event id, no proposed slot).
- [ ] Unit: mutual exclusivity with the F18 organizer path (intent predicates, not branching).
- [ ] Contract: mocked Graph `POST /events/{id}/tentativelyAccept` request shape (method, URL,
      headers, exact `sendResponse` + `proposedNewTime` body) and response handling
      (202-empty-body success, D5 error matrix, retry exhaustion).
- [ ] Audit: a performed proposal emits the expected action-audit record; idempotency dedupe.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create `docs/features/active/2026-07-07-attendee-propose-new-time-130/` folder from the template
