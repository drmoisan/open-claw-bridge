# 2026-07-07-organizer-reschedule — Spec

- **Issue:** #128
- **Parent (optional):** epic openclaw-vision (Epic D — Stage 2 Final Vision), feature F18, wave 4
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature

## Overview

The OpenClaw deterministic agent computes calendar reschedule decisions but has never
performed a real calendar write. Feature F12 (#109, calendar-write-flags) shipped the
policy scaffolding — the global `CalendarWriteEnabled` kill switch, the per-path
`EnableOrganizerReschedule` flag, and the pure `CalendarWritePolicy.OrganizerRescheduleAllowed`
composition predicate — but no production code consumes that gate to issue a Microsoft
Graph write. Feature F8 (#105) shipped the `OneOnOneMoveGuard` / `ISeriesMoveHistory`
move-history seam explicitly for F18, and it likewise has zero consumers today.

F18 delivers the first real calendar-write RPC: an organizer reschedule realized as a
Microsoft Graph `PATCH /users/{principal}/events/{id}` that moves the start/end time of
an organizer-owned event, gated behind the named feature flag `ENABLE_ORGANIZER_RESCHEDULE`
(canonical binding `OpenClaw__AgentPolicy__EnableOrganizerReschedule`, default OFF).
When the flag or the global `CalendarWriteEnabled` kill switch is off, there is no
outbound behavior change: the pipeline computes and logs the intended reschedule, emits a
`reschedule_disabled` audit record, and performs no Graph write, no move-history write,
and no dedupe write (dry-run parity with today).

Primary research input: `research/2026-07-07T07-35-organizer-reschedule.research.md`
(complexity C3, floor C3, signal `cross_module_contract_change`).

## In Scope

- Extend the portability boundary `IHostAdapterClient` with a tenth member,
  `UpdateEventTimesAsync`, and implement it in both existing adapters (Graph = real
  PATCH; local Stage-0 HTTP adapter = synthesized fail-closed `NOT_SUPPORTED` envelope,
  no I/O).
- Extend `ISchedulingService` with `RescheduleEventAsync`, mirroring the existing
  `SendMailAsync` seam shape, implemented by `HostAdapterSchedulingService`.
- A new `SchedulingWorker.Reschedule.cs` partial that orchestrates: pure intent
  computation → move-guard consult (`OneOnOneMoveGuard` / `ISeriesMoveHistory`) →
  `CalendarWritePolicy.OrganizerRescheduleAllowed` gate → dedupe → Graph write →
  post-write bookkeeping (audit `rescheduled`, `RecordMoveAsync`, dedupe record).
- New audit result codes on `ActionAuditResultCode`: `rescheduled`, `reschedule_failed`,
  `reschedule_disabled`, `reschedule_blocked`; new dedupe action type
  `SentActionKey.OrganizerReschedule = "organizer-reschedule"`.
- Mocked-Graph contract tests (established `FakeHttpHandler` pattern) proving the wire
  contract behind the flag; worker unit tests over the gate truth table; property tests
  for new pure functions (T1 obligation).
- A live-tenant verification runbook recorded as a `human_interaction` exception.

## Non-Goals

- **No live Graph write in this feature's automated verification.** No Azure/Exchange
  credentials exist in this environment or CI. The write path ships proven by
  mocked-Graph contract tests only. Live-tenant verification (including the
  `Calendars.ReadWrite` application-permission grant and admin consent) is a recorded
  `human_interaction` requirement with `response: exception` and runbook path
  `docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md`.
- **`SendOnBehalfAuthorizer` (F15/#119) does not apply.** The reschedule PATCH targets
  the principal's own calendar (`/users/{p}/events/{id}`) under app-only
  `Calendars.ReadWrite`; no representation of one mailbox by another occurs, so the
  send-on-behalf allowlist is intentionally not consulted. Reviewers must not flag the
  absence of an allowlist check on this path.
- **No attendee propose-new-time flow.** Responding to a meeting the principal does not
  organize (tentative-accept with proposed time) is F19, not this feature.
- **No general event update.** The adapter member updates only `start`/`end`
  (`UpdateEventTimesAsync`, not `UpdateEventAsync`). The request body structurally cannot
  carry `body`, `subject`, `location`, or `attendees`, which realizes the master §11.1
  guardrail against clobbering the online-meeting blob.
- **No local COM-bridge calendar-write route.** The local HostAdapter/MailBridge
  `PATCH /events/{id}` remains deferred (master line 108); the local adapter fails closed.
- **No schema or audit-contract change.** `ActionAuditRecord` already reserves
  `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc`; the `series_moves` and
  `action_audit` tables already exist. Result codes and action types are free-form
  strings appended without contract change.
- **No production flag rollout.** Enabling the flags in a real deployment is an operator
  decision covered by the runbook, outside this feature's scope.

## Behavior

### Gate semantics (three-flag composition)

The write is allowed only when `CalendarWritePolicy.OrganizerRescheduleAllowed(options)`
returns true, i.e. `options.CalendarWriteEnabled && options.EnableOrganizerReschedule`.
Both flags default to `false`. The four-row truth table:

| `CalendarWriteEnabled` | `EnableOrganizerReschedule` | Outcome |
|---|---|---|
| false | false | dry-run: `reschedule_disabled` audit, no write |
| false | true | dry-run: `reschedule_disabled` audit, no write |
| true | false | dry-run: `reschedule_disabled` audit, no write |
| true | true | write path proceeds (subject to guard and dedupe) |

### Evaluation order (worker orchestration)

1. **Intent computation (pure, internal static helper for property testing).** Eligible
   iff: hydrated `meetingEvent` is non-null, `context.IsOrganizer == true`, the event's
   `Start`/`End` are non-null, `context.EventId` is non-empty, and at least one proposed
   slot exists. Target interval = first proposed slot's start; duration preserved from
   the original event (`End - Start`). No intent → return silently (no audit row;
   identical to today's behavior for non-reschedulable messages).
2. **Move-guard consult — before the flag gate, so the dry-run reports the true
   decision.** `seriesKey = OneOnOneMoveGuard.ResolveSeriesKey(context)`; moved starts
   from `ISeriesMoveHistory.GetMovedOccurrenceStartsAsync`; occurrence starts from
   already-available calendar-view data filtered by `SeriesMasterId` where present, else
   an empty list (conservative per D2); `OneOnOneMoveGuard.CanMove(...)`. Blocked →
   audit `reschedule_blocked`, log the decision, **no write regardless of flags**.
3. **Flag gate.** `!CalendarWritePolicy.OrganizerRescheduleAllowed(options)` → log the
   intended move (old → new times) at Information and audit `reschedule_disabled` with
   the four time columns populated. **No Graph call, no write-path token acquisition, no
   `series_moves` row, no dedupe row.** Dry-runs must never consume move-history budget
   or dedupe slots.
4. **Dedupe.** Key = `SentActionKey.Build(mailbox, messageId, SentActionKey.OrganizerReschedule)`.
   Already recorded → audit `dedupe_skipped`, return.
5. **Write.** `schedulingService.RescheduleEventAsync(eventId, newStartUtc, newEndUtc, correlationId, ct)`.
   On exception: audit `reschedule_failed` with `ErrorDetail` (durable before the
   exception propagates, mirroring the send-failure ordering), then rethrow. A failed
   write records **no** `series_moves` row and **no** dedupe row, so a retry on the next
   cycle remains possible.
6. **Post-write bookkeeping, in this order.** Audit `rescheduled`; then
   `seriesMoveHistory.RecordMoveAsync(seriesKey, originalStartUtc, timeProvider.GetUtcNow(), ct)`;
   then `sentActionStore.RecordAsync(dedupeKey, ...)`. Audit-first matches the send
   path's rule that the audit reflects the actual side effect even if later bookkeeping
   fails.

One correlation id (GUID) per outbound-action evaluation, forwarded to the adapter as
the Graph `client-request-id` (existing #107 rule).

### Fail-closed rules

Null/missing event, non-organizer message, missing original times, zero proposed slots,
guard block, gate off, local-backend `NOT_SUPPORTED`, and any failure envelope or
exception all result in **no write**. Any ambiguity resolves to "no write" (issue #128
constraint).

### Flag-off no-behavior-change scoping

Today's pipeline computes no reschedule intent, so the dry-run log line and the
`reschedule_disabled` audit row are themselves new, additive output. "No behavior
change" is scoped to outbound side effects: no Graph request, no token acquisition for
the write, no `series_moves` row, no dedupe row. The existing send path — including its
persisted audit `ActingFlags` string — must remain byte-identical to pre-F18; the
reschedule path uses its own ActingFlags snapshot
(`CalendarWriteEnabled=<bool>;EnableOrganizerReschedule=<bool>`) built by a new pure
static helper, and the existing `BuildActingFlags` is not widened. The existing
`"CalendarWriteEnabled is false; not writing the calendar"` stub log line is subsumed by
the richer dry-run log.

## Inputs / Outputs

- **Inputs (config):** `AgentPolicyOptions.CalendarWriteEnabled` (default `false`;
  global kill switch) and `AgentPolicyOptions.EnableOrganizerReschedule` (default
  `false`; env binding `OpenClaw__AgentPolicy__EnableOrganizerReschedule`, the concrete
  binding of the master's semantic name `ENABLE_ORGANIZER_RESCHEDULE`). Both bound from
  the `OpenClaw:AgentPolicy` section; no new config keys.
- **Inputs (runtime):** hydrated `SchedulingEventDto` (`Start`/`End`, `EventId`,
  `SeriesMasterId`), `NormalizedMeetingContext` (organizer bit, message identity),
  proposed slots, move history from SQLite `series_moves`.
- **Outputs:** at most one Graph `PATCH` per message evaluation; one `ActionAuditRecord`
  per evaluated intent (`rescheduled` / `reschedule_failed` / `reschedule_disabled` /
  `reschedule_blocked` / `dedupe_skipped`) with `EventId` and the four time columns
  populated whenever an intent exists; one `series_moves` row and one sent-action row
  only after a successful write; structured logs at each decision step.
- **Backward compatibility:** no DTO shape change, no audit schema change, no new DI
  registration (`ISeriesMoveHistory` is already registered), no breaking change to
  existing `IHostAdapterClient` members (additive member 10).

## API / Contract Surface

### `IHostAdapterClient` (portability boundary — cross-module contract change)

```csharp
Task<ApiEnvelope<EventDto>> UpdateEventTimesAsync(
    string bridgeId,
    DateTimeOffset newStartUtc,
    DateTimeOffset newEndUtc,
    string? requestId = null,
    CancellationToken cancellationToken = default);
```

- **`GraphHostAdapterClient`** (new partial `GraphHostAdapterClient.RescheduleEvent.cs`):
  issues `PATCH users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}` through the
  shared `GraphRequestExecutor`, inheriting bearer-token acquisition,
  `client-request-id` propagation, retry/backoff (429/502/503/504 with `Retry-After`
  precedence, all delays via injected `TimeProvider`), and the D5 error matrix
  (400→`INVALID_REQUEST`, 401/403→`UNAUTHORIZED` with Graph `error.code` passthrough to
  `BridgeErrorCode`, 404→`NOT_FOUND`, 429→`THROTTLED`, 502/503/504→`TRANSPORT_FAILURE`).
  A 200 response is mapped via `GraphEventMapper.Map`; an unparseable 2xx body maps to
  `TRANSPORT_FAILURE`; a mapping gap maps to `INTERNAL_ERROR`. No fabricated data.
- **`HostAdapterHttpClient`** (local Stage-0 backend): fails closed with a synthesized
  non-retryable failure envelope — `ApiError` code `NOT_SUPPORTED`, message stating the
  local HostAdapter backend has no calendar-write route — performing **no I/O**. This
  avoids misreporting a permanent capability gap as a transient `TRANSPORT_FAILURE`.

### Graph wire contract (PATCH body)

Exactly two top-level properties, camelCase via `GraphRequestExecutor.JsonOptions`,
UTC instants rendered at seconds precision (invariant `"s"` format):

```json
{
  "start": { "dateTime": "2026-07-09T14:00:00", "timeZone": "UTC" },
  "end":   { "dateTime": "2026-07-09T14:30:00", "timeZone": "UTC" }
}
```

Headers: `Authorization: Bearer <token>`, `client-request-id: <correlationId>`,
`Content-Type: application/json`. No `Prefer` headers. Contract tests assert
structurally (via `JsonDocument`) that no other properties are present — in particular
no `body`, `subject`, `location`, or `attendees`.

### `ISchedulingService` (middleware seam, D6)

```csharp
Task RescheduleEventAsync(
    string eventId,
    DateTimeOffset newStartUtc,
    DateTimeOffset newEndUtc,
    string? correlationId = null,
    CancellationToken ct = default);
```

`HostAdapterSchedulingService` mirrors `SendMailAsync`: guard-clause the id, delegate to
`UpdateEventTimesAsync(..., requestId: correlationId, ...)`, and on a non-`Ok` envelope
throw `InvalidOperationException($"Organizer reschedule failed: {code}: {message}")`.
Returns `Task` (not the updated DTO); the worker already holds the times it needs.

### Audit constants

- `ActionAuditResultCode`: append `Rescheduled = "rescheduled"`,
  `RescheduleFailed = "reschedule_failed"`, `RescheduleDisabled = "reschedule_disabled"`,
  `RescheduleBlocked = "reschedule_blocked"`. `DedupeSkipped` is reused.
- `SentActionKey`: append `OrganizerReschedule = "organizer-reschedule"` (colon-free).
- Optional, non-blocking: an explicit `PurviewActivityLogProjection.MapActionType` case
  for the new action type (the documented fallback covers it otherwise).

## Data & State

- **`series_moves` (existing SQLite table):** one idempotent row per successful write,
  keyed `(seriesKey, occurrenceStartUtc)`; recorded with the **pre-move** occurrence
  start so subsequent `OneOnOneMoveGuard` consults observe the move. Never written on
  dry-run, guard block, dedupe skip, or failure.
- **`action_audit` (existing table):** one row per evaluated intent, populating
  `EventId`, `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc`, correlation
  id, action type `organizer-reschedule`, and the reschedule ActingFlags snapshot.
- **Sent-action dedupe (existing store):** key `{mailbox}:{messageId}:organizer-reschedule`;
  recorded only after a successful write; a restart after success skips with
  `dedupe_skipped`.
- No migrations, no new tables, no backfill.

## Architecture, Determinism, and Quality Obligations

- **Boundaries (No-COM):** pure decision logic (`CalendarWritePolicy`,
  `OneOnOneMoveGuard`, `MovePolicy`, intent computation) stays in host-neutral domain
  code; the domain must not depend on adapters; the Graph HTTP call lives only behind
  the `IHostAdapterClient` adapter seam in the `CloudGraph` namespace. Existing
  NetArchTest architecture-boundary tests must pass unmodified.
- **Determinism:** all time via injected `TimeProvider` (`FakeTimeProvider` in tests);
  no wall-clock reads, no `Task.Delay`/sleeps in tests; retry-exhaustion tests advance
  simulated time.
- **Coverage:** line >= 85% and branch >= 75% maintained; no regression on changed
  lines. `OpenClaw.Core` is T1: >= 1 property test per new pure function; mutation score
  >= 75% in the pre-merge/nightly pipeline (the truth-table and fail-closed branches are
  the mutation-sensitive surface — tests assert both the action taken and the actions
  not taken).
- **Test stack:** MSTest + FluentAssertions + Moq (the repository's actual convention),
  mocked Graph via the shared `FakeHttpHandler`, base address
  `https://graph.example.test/v1.0/`.
- **File size:** every touched file stays under the 500-line cap; the pipeline stub
  replacement delegates to the new `SchedulingWorker.Reschedule.cs` partial.

## Constraints & Risks

- First real calendar write; must fail closed. Any ambiguity in the gate resolves to
  "no write".
- No Azure/Exchange credentials in this environment or CI; the Graph write is exercised
  only through the mocked Graph seam. Live verification is a human-interaction
  exception (runbook required).
- Cross-module contract change (C3 floor signal): the write path threads options, the
  move guard, the audit record, the service seam, and the Graph adapter contract
  together; both `IHostAdapterClient` implementations must be updated in the same
  change.
- Existing `SchedulingWorker` tests construct the worker directly; the new
  `ISeriesMoveHistory` ctor parameter requires a mechanical mock addition in their
  builders.
- The dry-run emits new (additive) log/audit output; reviewers must evaluate
  "no behavior change" against outbound side effects, not log/audit byte-identity.

## Implementation Strategy

- **Add (production):** `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs`;
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs` (intent helper,
  orchestration, reschedule audit-record builder, reschedule ActingFlags snapshot
  builder).
- **Modify (production):** `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`
  (member 10 with XML docs); `src/OpenClaw.Core/HostAdapterHttpClient.cs` (fail-closed
  `NOT_SUPPORTED`); `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`;
  `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`;
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` (ctor gains
  `ISeriesMoveHistory`); `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`
  (thread `meetingEvent` into `ProposeAndActAsync`; replace the trailing
  `!CalendarWriteEnabled` stub); `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs`
  and `src/OpenClaw.Core/Agent/SentActionKey.cs` (new constants). No `Program.cs`
  change required.
- **Add (tests):** `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs`
  (contract suite); `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleTests.cs`
  (truth table, guard block, dry-run, success, failure, dedupe, no-intent rows);
  property tests for the intent helper and flags snapshot.
- **Modify (tests):** `HostAdapterSchedulingServiceTests` (delegation, correlation-id
  forwarding, failure-throw); `HostAdapterHttpClientTests` (`NOT_SUPPORTED`, zero HTTP
  invocations); existing worker test builders (mock `ISeriesMoveHistory`).
- **Add (docs):** `runbooks/organizer-reschedule-live-verification.runbook.md` under this
  feature folder (permission grant + admin consent, flag enablement, live-move
  observation, flag disable; F11 HI-1 record-shape precedent).
- **Dependencies:** none added or removed.
- **Rollout:** ships dark. Both flags default OFF; enabling requires the tenant admin to
  grant `Calendars.ReadWrite` and an operator to set both flags per the runbook. The
  global `CalendarWriteEnabled` kill switch disables the path independently of the named
  flag.
- **Rejected alternatives (from research):** separate `ICalendarWriteClient`; real PATCH
  from the local adapter; widening `NormalizedMeetingContext` with Start/End; returning
  the updated DTO from `RescheduleEventAsync`.

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
- [ ] Unit: move-guard interaction blocks a reschedule that violates move history.
- [ ] Contract: mocked Graph `PATCH /events/{id}` request shape and response handling.
- [ ] Audit: a performed reschedule emits the expected action-audit record.
