# 2026-07-07-attendee-propose-new-time — Spec

- **Issue:** #130
- **Parent (optional):** epic openclaw-vision (Epic D — Stage 2 Final Vision), feature F19, wave 5; depends on organizer-reschedule (F18, #128)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature

## Overview

The OpenClaw deterministic agent can now perform an organizer-side calendar write
(F18 organizer-reschedule, #128, merged at this branch's base: Graph
`PATCH /users/{p}/events/{id}` behind `ENABLE_ORGANIZER_RESCHEDULE`). The attendee-side
counterpart is still missing: when the principal is invited to a meeting they do not
organize and the computed decision is to propose a different time, the agent has no way
to respond. Feature F12 (#109, calendar-write-flags) shipped the flag scaffolding for
this path — `AgentPolicyOptions.EnableAttendeeProposeNewTime` (canonical flag
`ENABLE_ATTENDEE_PROPOSE_NEW_TIME`, binding
`OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`, default OFF) and the pure
predicate `CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)` — but no
production code consumes that gate.

F19 delivers the attendee-side calendar-write RPC, the mirror of F18: a Microsoft Graph
`POST /users/{principal}/events/{id}/tentativelyAccept` carrying a `proposedNewTime`
body, gated behind the named feature flag `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`. When the
flag or the global `CalendarWriteEnabled` kill switch is off, there is no outbound
behavior change: the pipeline computes and logs the intended proposal, emits a
`propose_new_time_disabled` audit record, and performs no Graph write, no write-path
token acquisition, and no dedupe write (dry-run parity with today). F19 completes
Epic D and is the final feature of the openclaw-vision program.

Primary research input: `research/2026-07-07T09-45-attendee-propose-new-time.research.md`
(floor signal `cross_module_contract_change`).

## In Scope

- Extend the portability boundary `IHostAdapterClient` with an eleventh member,
  `ProposeNewMeetingTimeAsync`, and implement it in both existing adapters (Graph = real
  POST to `tentativelyAccept`; local Stage-0 HTTP adapter = synthesized fail-closed
  `NOT_SUPPORTED` envelope, no I/O).
- Extend `ISchedulingService` with `ProposeNewMeetingTimeAsync`, mirroring the existing
  `RescheduleEventAsync` seam shape, implemented by `HostAdapterSchedulingService`.
- A new `SchedulingWorker.ProposeNewTime.cs` partial that orchestrates: pure intent
  computation → `CalendarWritePolicy.AttendeeProposeNewTimeAllowed` gate → dedupe →
  Graph write → post-write bookkeeping (audit `proposed_new_time`, dedupe record). No
  move-guard step and no `series_moves` interaction on this path.
- New audit result codes on `ActionAuditResultCode`: `proposed_new_time`,
  `propose_new_time_failed`, `propose_new_time_disabled`; new dedupe action type
  `SentActionKey.AttendeeProposeNewTime = "attendee-propose-new-time"`; new pure
  ActingFlags helper `BuildProposeNewTimeActingFlags`.
- Mocked-Graph contract tests (established `FakeHttpHandler` pattern) proving the wire
  contract behind the flag; worker unit tests over the gate truth table, eligibility
  fail-closed matrix, and mutual exclusivity with the F18 organizer path; property tests
  for new pure functions (T1 obligation).
- A live-tenant verification runbook recorded as a `human_interaction` exception.

## Non-Goals

- **No live Graph write in this feature's automated verification.** No Azure/Exchange
  credentials exist in this environment or CI. The write path ships proven by
  mocked-Graph contract tests only. Live-tenant verification (including the
  `Calendars.ReadWrite` application-permission grant, shared with F18 and possibly
  already granted) is a recorded `human_interaction` requirement with
  `response: exception` and runbook path
  `docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`.
- **`SendOnBehalfAuthorizer` (F15/#119) does not apply.** The tentativelyAccept POST
  targets the principal's own event (`/users/{p}/events/{id}/tentativelyAccept`) under
  app-only `Calendars.ReadWrite`; no representation of one mailbox by another occurs, so
  the send-on-behalf allowlist is intentionally not consulted. Reviewers must not flag
  the absence of an allowlist check on this path.
- **No decline path.** Graph also supports proposals on `decline`, but decline removes
  the meeting from the attendee's calendar and signals non-attendance; the agent's
  deterministic decision is "can attend, at a different time," which tentative-accept
  represents. Decline is out of scope.
- **No general event update.** The attendee path does not patch the attendee's event
  copy (`UpdateEventTimesAsync` is not used here — patching the copy would not implement
  the meeting-response protocol and would silently diverge the attendee's calendar). The
  request body structurally cannot carry `comment`, `body`, `subject`, `attendees`, or
  top-level `start`/`end`.
- **No local COM-bridge meeting-response route.** The local HostAdapter/MailBridge has
  no meeting-response route; the local adapter fails closed with `NOT_SUPPORTED`.
- **No schema or audit-contract change.** `ActionAuditRecord` already reserves
  `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc` for F18/F19; the
  `action_audit` table already exists. Result codes and action types are free-form
  strings appended without contract change. No `series_moves` interaction of any kind.
- **No worker constructor change.** Unlike F18, no new dependency is added to
  `SchedulingWorker`; existing worker-test builders need no changes.
- **No production flag rollout.** Enabling the flags in a real deployment is an operator
  decision covered by the runbook, outside this feature's scope.

## Behavior

### Gate semantics (flag composition)

The write is allowed only when `CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)`
returns true, i.e. `options.CalendarWriteEnabled && options.EnableAttendeeProposeNewTime`.
Both flags default to `false`. The four-row truth table:

| `CalendarWriteEnabled` | `EnableAttendeeProposeNewTime` | Outcome |
|---|---|---|
| false | false | dry-run: `propose_new_time_disabled` audit, no write |
| false | true | dry-run: `propose_new_time_disabled` audit, no write |
| true | false | dry-run: `propose_new_time_disabled` audit, no write |
| true | true | write path proceeds (subject to dedupe) |

### Evaluation order (worker orchestration)

1. **Intent computation (pure, internal static helper for property testing).** Eligible
   iff **all** of: hydrated `meetingEvent` is non-null, `context.IsOrganizer == false`
   (the exact mirror of F18's organizer check), `context.AllowNewTimeProposals == true`
   (fail-closed by construction: the normalizer and the Graph mapper both default it
   `false` when the event or field is absent), the event's `Start`/`End` are non-null,
   `context.EventId` is non-empty, and at least one proposed slot exists. Proposed start
   = first proposed slot's start; duration preserved from the original event
   (`End - Start`), identical to F18's target computation. No intent → return silently
   (no audit row; identical to today's behavior for non-proposable messages).
2. **Flag gate.** `!CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)` → log
   the intended proposal (original → proposed times) at Information and audit
   `propose_new_time_disabled` with the four time columns populated. **No Graph call, no
   write-path token acquisition, no dedupe row.** Dry-runs must never consume dedupe
   slots.
3. **Dedupe.** Key = `SentActionKey.Build(mailbox, messageId, SentActionKey.AttendeeProposeNewTime)`.
   Already recorded → audit `dedupe_skipped`, return.
4. **Write.** `schedulingService.ProposeNewMeetingTimeAsync(eventId, proposedStartUtc, proposedEndUtc, correlationId, ct)`.
   On exception (excluding `OperationCanceledException`): audit `propose_new_time_failed`
   with `ErrorDetail` (durable before the exception propagates, mirroring the F18
   failure ordering), then rethrow. A failed write records **no** dedupe row, so a retry
   on the next cycle remains possible.
5. **Post-write bookkeeping, in this order.** Audit `proposed_new_time`; then
   `sentActionStore.RecordAsync(dedupeKey, timeProvider.GetUtcNow(), ct)`. Audit-first
   matches the send path's rule that the audit reflects the actual side effect even if
   later bookkeeping fails. **No `ISeriesMoveHistory.RecordMoveAsync` call.**

One correlation id (GUID) per outbound-action evaluation, forwarded to the adapter as
the Graph `client-request-id` (existing #107 rule). The evaluation
(`EvaluateProposeNewTimeAsync(messageId, context, meetingEvent, slots, ct)`) takes no
`priority` parameter; nothing consumes it.

### No move guard on the attendee path (explicit decision)

F18's move-guard step (`OneOnOneMoveGuard`/`ISeriesMoveHistory`) budgets how often the
owner *actually moves* an organizer-owned recurring series. The attendee propose-new-time
performs no calendar move: it sends a meeting-response message to the organizer, and the
event's times change only if the organizer separately accepts the proposal. Consulting
the move budget here would spend or read a budget for a move that never happened, and
recording into `series_moves` would corrupt the guard's history for any future
organizer-side reschedule of the same series. Repetition on the attendee path is bounded
instead by the per-message dedupe key, which is the correct idempotency scope for a
response action. Consequently there is also **no blocked state and no blocked result
code**: the F19 state machine is a strict subset of F18's
(intent → gate → dedupe → write → bookkeeping).

### Mutual exclusivity with the F18 organizer path

The pipeline calls `EvaluateProposeNewTimeAsync` sequentially, immediately after the F18
`EvaluateRescheduleAsync` call, with no pipeline-level branching. Exclusivity is
guaranteed by the intent predicates: `ComputeRescheduleIntent` requires
`context.IsOrganizer == true` and `ComputeProposeNewTimeIntent` requires
`context.IsOrganizer == false`, so for any single message at most one of the two
evaluations produces an intent and the other returns silently. Worker unit tests must
assert the exclusivity in both directions.

### Fail-closed rules

Null/missing event, organizer-owned message (`IsOrganizer == true`),
`AllowNewTimeProposals == false`, missing original times, empty event id, zero proposed
slots, gate off, local-backend `NOT_SUPPORTED`, and any failure envelope or exception
all result in **no write**. Any ambiguity resolves to "no write" (issue #130
constraint).

### Flag-off no-behavior-change scoping

Today's pipeline computes no propose-new-time intent, so the dry-run log line and the
`propose_new_time_disabled` audit row are themselves new, additive output. "No behavior
change" is scoped to outbound side effects: no Graph request, no token acquisition for
the write, no dedupe row. The existing send path and the F18 reschedule path — including
their persisted audit `ActingFlags` strings — must remain byte-identical to pre-F19; the
propose path uses its own ActingFlags snapshot
(`CalendarWriteEnabled=<bool>;EnableAttendeeProposeNewTime=<bool>`) built by a new pure
static helper `BuildProposeNewTimeActingFlags`, and neither the existing
`BuildActingFlags` nor `BuildRescheduleActingFlags` is widened.

## Inputs / Outputs

- **Inputs (config):** `AgentPolicyOptions.CalendarWriteEnabled` (default `false`;
  global kill switch) and `AgentPolicyOptions.EnableAttendeeProposeNewTime` (default
  `false`; env binding `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`, the
  concrete binding of the master's semantic name `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`).
  Both bound from the `OpenClaw:AgentPolicy` section; no new config keys.
- **Inputs (runtime):** hydrated `SchedulingEventDto` (`Start`/`End`, `EventId`,
  `IsOrganizer`, `AllowNewTimeProposals`), `NormalizedMeetingContext` (organizer bit,
  `AllowNewTimeProposals`, message identity), proposed slots.
- **Outputs:** at most one Graph `POST .../tentativelyAccept` per message evaluation;
  one `ActionAuditRecord` per evaluated intent (`proposed_new_time` /
  `propose_new_time_failed` / `propose_new_time_disabled` / `dedupe_skipped`) with
  `EventId` and the four time columns populated whenever an intent exists; one
  sent-action row only after a successful write; structured logs at each decision step.
- **Backward compatibility:** no DTO shape change (`SchedulingEventDto` and
  `NormalizedMeetingContext` already carry `IsOrganizer` and `AllowNewTimeProposals`),
  no audit schema change, no new DI registration, no worker constructor change, no
  breaking change to existing `IHostAdapterClient` members (additive member 11).

## API / Contract Surface

### `IHostAdapterClient` (portability boundary — cross-module contract change)

```csharp
Task<ApiEnvelope<object?>> ProposeNewMeetingTimeAsync(
    string bridgeId,
    DateTimeOffset proposedStartUtc,
    DateTimeOffset proposedEndUtc,
    string? requestId = null,
    CancellationToken cancellationToken = default);
```

- **Envelope payload `ApiEnvelope<object?>`:** the wire response is `202 Accepted` with
  no body; the repository already uses exactly this shape for exactly this case
  (`SendMailAsync`, parsed with `_ => null`, producing `ok: true, data: null`). Mapping
  to `EventDto` would fabricate data; a new marker type would add a cross-module
  contract type with no consumer.
- **`GraphHostAdapterClient`** (new partial `GraphHostAdapterClient.ProposeNewTime.cs`):
  issues `POST users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}/tentativelyAccept`
  through the shared `GraphRequestExecutor`, inheriting bearer-token acquisition,
  `client-request-id` propagation, retry/backoff (429/502/503/504 with `Retry-After`
  precedence, all delays via injected `TimeProvider`), and the D5 error matrix
  (400→`INVALID_REQUEST`, 401/403→`UNAUTHORIZED` with Graph `error.code` passthrough to
  `BridgeErrorCode`, 404→`NOT_FOUND`, 429→`THROTTLED`, 502/503/504→`TRANSPORT_FAILURE`).
  A 202 response with an empty body maps to `ok: true, data: null`. No fabricated data.
- **`HostAdapterHttpClient`** (local Stage-0 backend): fails closed with a synthesized
  non-retryable failure envelope — `ApiError` code `NOT_SUPPORTED`, message stating the
  local HostAdapter backend has no meeting-response route and the attendee
  propose-new-time requires the Graph adapter — performing **no I/O**. This avoids
  misreporting a permanent capability gap as a transient `TRANSPORT_FAILURE`.

### Graph wire contract (tentativelyAccept POST body)

Route (app-only form): `POST /users/{id | userPrincipalName}/events/{id}/tentativelyAccept`.
Application permission: `Calendars.ReadWrite` (the same grant as F18). Success:
`202 Accepted` with **no response body**. `sendResponse` must be `true` whenever
`proposedNewTime` is set (Graph returns 400 otherwise), so it is hardcoded `true`.

Exactly two top-level properties, camelCase via `GraphRequestExecutor.JsonOptions`,
UTC instants rendered at seconds precision (invariant `"s"` format):

```json
{
  "sendResponse": true,
  "proposedNewTime": {
    "start": { "dateTime": "2026-07-09T14:00:00", "timeZone": "UTC" },
    "end":   { "dateTime": "2026-07-09T14:30:00", "timeZone": "UTC" }
  }
}
```

Headers: `Authorization: Bearer <token>`, `client-request-id: <correlationId>`,
`Content-Type: application/json`. No `Prefer` headers. Contract tests assert
structurally (via `JsonDocument`) that the top level contains exactly `sendResponse` and
`proposedNewTime`, that `proposedNewTime` contains exactly `start` and `end`
dateTimeTimeZone pairs with `timeZone == "UTC"`, and that no other properties are
present — in particular no `comment` and no top-level
`start`/`end`/`body`/`subject`/`attendees` (the F19 analogue of F18's absent-property
guardrail: the body structurally cannot rewrite the event).

### `ISchedulingService` (middleware seam, D6)

```csharp
Task ProposeNewMeetingTimeAsync(
    string eventId,
    DateTimeOffset proposedStartUtc,
    DateTimeOffset proposedEndUtc,
    string? correlationId = null,
    CancellationToken ct = default);
```

`HostAdapterSchedulingService` mirrors `RescheduleEventAsync`: guard-clause the id
(`ArgumentException.ThrowIfNullOrWhiteSpace`), delegate to
`ProposeNewMeetingTimeAsync(..., requestId: correlationId, ...)`, and on a non-`Ok`
envelope throw
`InvalidOperationException($"Attendee propose-new-time failed: {code}: {message}")`.
Returns `Task` (not a DTO); the worker already holds the times it audits, and there is
no response body anyway.

### Audit constants

- `ActionAuditResultCode`: append `ProposedNewTime = "proposed_new_time"`,
  `ProposeNewTimeFailed = "propose_new_time_failed"`,
  `ProposeNewTimeDisabled = "propose_new_time_disabled"`. `DedupeSkipped` is reused.
  **No blocked code** (`reschedule_blocked` was move-guard-specific; the attendee path
  has no guard).
- `SentActionKey`: append `AttendeeProposeNewTime = "attendee-propose-new-time"`
  (colon-free, satisfying the `Build` distinctness remark).
- ActingFlags: new pure static helper `BuildProposeNewTimeActingFlags(options)`
  returning `CalendarWriteEnabled=<bool>;EnableAttendeeProposeNewTime=<bool>`, beside
  (not widening) `BuildRescheduleActingFlags`, so existing persisted `ActingFlags`
  strings stay byte-identical.
- Audit record builder: private `BuildProposeNewTimeAuditRecord(...)` mirroring the F18
  builder with `ActionType: SentActionKey.AttendeeProposeNewTime`, the four time columns
  from the intent (`Original*` = event's current times, `New*` = proposed times), and
  the propose ActingFlags snapshot.
- Optional, non-blocking: an explicit `PurviewActivityLogProjection.MapActionType` case
  for the new action type (the documented fallback covers it otherwise).

## Data & State

- **No `series_moves` interaction.** The attendee path never reads or writes the
  `series_moves` table in any branch — a proposal moves nothing, and writing rows here
  would corrupt the move-guard history for future organizer-side reschedules. There is
  no move-guard consult and no blocked state.
- **`action_audit` (existing table):** one row per evaluated intent, populating
  `EventId`, `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc`, correlation
  id, action type `attendee-propose-new-time`, and the propose ActingFlags snapshot.
- **Sent-action dedupe (existing store):** key
  `{mailbox}:{messageId}:attendee-propose-new-time`; recorded only after a successful
  write; a restart after success skips with `dedupe_skipped`.
- No migrations, no new tables, no backfill.

## Architecture, Determinism, and Quality Obligations

- **Boundaries (No-COM):** pure decision logic (`CalendarWritePolicy`, intent
  computation, ActingFlags snapshot) stays in host-neutral domain code; the domain must
  not depend on adapters; the Graph HTTP call lives only behind the `IHostAdapterClient`
  adapter seam in the `CloudGraph` namespace; the worker partial references no
  `CloudGraph` type and reaches Graph only through `ISchedulingService`. Existing
  NetArchTest architecture-boundary tests must pass unmodified.
- **Determinism:** all time via injected `TimeProvider` (`FakeTimeProvider` in tests);
  no wall-clock reads, no `Task.Delay`/sleeps in tests; retry-exhaustion tests advance
  simulated time.
- **Coverage:** line >= 85% and branch >= 75% maintained; no regression on changed
  lines. `OpenClaw.Core` is T1: >= 1 CsCheck property test per new pure function
  (`ComputeProposeNewTimeIntent`, `BuildProposeNewTimeActingFlags`); mutation score
  >= 75% in the pre-merge/nightly pipeline (the truth-table and fail-closed branches are
  the mutation-sensitive surface — tests assert both the action taken and the actions
  not taken: zero Graph calls, zero token acquisitions, zero `RecordMoveAsync` calls).
- **Test stack:** MSTest + FluentAssertions + Moq (the repository's actual convention),
  mocked Graph via the shared `FakeHttpHandler`, base address
  `https://graph.example.test/v1.0/`.
- **File size:** every touched file stays under the 500-line cap; split a
  `...EdgeTests.cs` sibling if the worker test file approaches the cap (F18 precedent).

## Constraints & Risks

- Attendee-side calendar write; must fail closed. Any ambiguity in the gate or
  eligibility resolves to "no write".
- No Azure/Exchange credentials in this environment or CI; the Graph write is exercised
  only through the mocked Graph seam. Live verification is a human-interaction
  exception (runbook required).
- Cross-module contract change (floor signal `cross_module_contract_change`): the write
  path threads options, the audit record, the service seam, and the Graph adapter
  contract together; both `IHostAdapterClient` implementations must be updated in the
  same change.
- Touching `series_moves` from this path would corrupt the F18 move-guard history; the
  design and tests must prove the attendee path never calls `RecordMoveAsync`.
- The dry-run emits new (additive) log/audit output; reviewers must evaluate
  "no behavior change" against outbound side effects, not log/audit byte-identity.
- Server-side authority: Graph returns 400 if the event's `allowNewTimeProposals` is
  `false` despite the client-side eligibility check; the D5 mapping
  (400→`INVALID_REQUEST`, non-retryable) covers this and the worker fails closed with
  `propose_new_time_failed`.

## Implementation Strategy

- **Add (production):** `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs`
  (tentativelyAccept POST member, mirrors `.RescheduleEvent.cs`);
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs` (intent record +
  pure intent helper, propose ActingFlags snapshot helper, audit-record builder,
  `EvaluateProposeNewTimeAsync` orchestration).
- **Modify (production):** `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`
  (member 11 with XML docs: wire route, 202-no-body semantics, local-backend
  `NOT_SUPPORTED`); `src/OpenClaw.Core/HostAdapterHttpClient.cs` (fail-closed
  `NOT_SUPPORTED`, no I/O); `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`
  (add `ProposeNewMeetingTimeAsync`);
  `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (implement it);
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` (one
  `EvaluateProposeNewTimeAsync` call immediately after the F18 evaluation);
  `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` (three new consts) and
  `src/OpenClaw.Core/Agent/SentActionKey.cs` (one new const). Optional:
  `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs` (explicit
  action-type case). No `SchedulingWorker.cs` ctor change, no `Program.cs`/DI change, no
  `AgentPolicyOptions`/`CalendarWritePolicy`/`appsettings.json` change (F12 shipped
  them), no `NormalizedMeetingContext` widening.
- **Add (tests):** `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientProposeNewTimeTests.cs`
  (contract suite);
  `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeTests.cs`
  (truth table, dry-run, success, failure, dedupe, eligibility fail-closed matrix,
  mutual-exclusivity rows; split an `...EdgeTests.cs` sibling if approaching 500 lines);
  `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeIntentPropertyTests.cs`
  (CsCheck properties: duration preservation, eligibility fail-closed monotonicity,
  ActingFlags round-trip);
  `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceProposeNewTimeTests.cs`
  (delegation, correlation-id forwarding, failure-envelope throw message).
- **Modify (tests):** `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`
  (member-11 `NOT_SUPPORTED` fail-closed with zero HTTP invocations). Existing loose
  Moq mocks of `IHostAdapterClient`/`ISchedulingService` absorb the new members
  automatically; no other test churn.
- **Add (docs):** `runbooks/attendee-propose-new-time-live-verification.runbook.md`
  under this feature folder (permission-grant confirmation, flag enablement,
  two-mailbox live-proposal observation, flag disable; F18/F11 HI-1 record-shape
  precedent).
- **Dependencies:** none added or removed.
- **Rollout:** ships dark. Both flags default OFF; enabling requires the tenant admin's
  `Calendars.ReadWrite` grant (shared with F18) and an operator to set both flags per
  the runbook. The global `CalendarWriteEnabled` kill switch disables the path
  independently of the named flag.
- **Rejected alternatives (from research):** `decline` + `proposedNewTime`;
  `ApiEnvelope<EventDto>` or a new `Unit` marker payload; patching the attendee's event
  copy via `UpdateEventTimesAsync`; consulting `OneOnOneMoveGuard`/`ISeriesMoveHistory`
  (with a blocked code); including a `comment` in the wire body; a pipeline-level
  `if (context.IsOrganizer)` branch; widening `NormalizedMeetingContext` or
  `SchedulingEventDto`.

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

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/property/contract as applicable)
- [ ] Edge cases and error handling covered by tests (fail-closed paths asserted)
- [ ] Docs updated (README, docs/features/active/... links, runbook)
- [ ] Telemetry/logging added or updated (dry-run and decision logging)
- [ ] Toolchain pass completed (format → lint → type-check → arch → test → contract → integration)

## Seeded Test Conditions (from potential)

- [ ] Unit: gate truth table (both flags on/off combinations) yields write / no-write.
- [ ] Unit: eligibility fail-closed matrix (organizer-owned, proposals disallowed, missing
      times, empty event id, no proposed slot).
- [ ] Unit: mutual exclusivity with the F18 organizer path (intent predicates, not branching).
- [ ] Contract: mocked Graph `POST /events/{id}/tentativelyAccept` request shape and
      response handling (202-empty-body success, D5 error matrix, retry exhaustion).
- [ ] Audit: a performed proposal emits the expected action-audit record; idempotency dedupe.
