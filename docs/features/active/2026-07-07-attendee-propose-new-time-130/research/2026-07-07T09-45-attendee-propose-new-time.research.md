# F19 attendee-propose-new-time (#130) — Deep Research

- **Issue:** #130 (epic openclaw-vision, Epic D, feature F19, wave 5; depends on F18 #128, merged at branch base 273c7df)
- **Date:** 2026-07-07T09-45
- **Author:** task-researcher
- **Scope:** Attendee-side calendar-write RPC — Microsoft Graph `POST /users/{p}/events/{id}/tentativelyAccept` with a `proposedNewTime` body, behind `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` (canonical binding `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`, default OFF; flag-off = dry-run parity, no Graph write).

All codebase findings below were verified by reading the named files in this worktree on 2026-07-07. F18 organizer-reschedule is the authoritative precedent and is already merged into this branch.

---

## 1. Current-State Seam Map (verified)

### 1.1 F19 scaffolding already merged (do not re-add)

| Element | Location | Verified detail |
|---|---|---|
| `AgentPolicyOptions.EnableAttendeeProposeNewTime` | `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` lines 106–115 | `bool`, default `false`; XML doc names the canonical flag `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` and the env binding `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`. |
| `CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)` | `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` lines 36–40 | Pure predicate: `options.CalendarWriteEnabled && options.EnableAttendeeProposeNewTime`; throws `ArgumentNullException` on null options. No production consumer exists today. |
| Default configuration | `src/OpenClaw.Core/appsettings.json` line 48 | `"EnableAttendeeProposeNewTime": false` under `OpenClaw:AgentPolicy`. |
| Eligibility inputs on the event DTO | `src/OpenClaw.Core/Agent/Contracts/SchedulingEventDto.cs` lines 45 (`IsOrganizer`), 47 (`AllowNewTimeProposals`), 49/51 (`Start`/`End`, nullable) | Already carried end-to-end from the Graph adapter (`GraphEventMapper.cs` lines 56/58 map `isOrganizer`/`allowNewTimeProposals`, both defaulting `false` when absent — fail-closed). |
| Eligibility inputs on the normalized context | `src/OpenClaw.Core/Agent/Models/NormalizedMeetingContext.cs` lines 39 (`EventId`), 51 (`IsOrganizer`), 54 (`AllowNewTimeProposals`) | `MeetingContextNormalizer.cs` lines 64/68 populate both booleans as `meetingEvent?.X ?? false` — a missing event yields `false` for both, so the context values are fail-closed by construction. |
| Audit time columns | `src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs` (used with named args in `SchedulingWorker.Reschedule.cs` lines 95–108) | `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc` were reserved for "Stage 2 (F18/F19)"; no contract or schema change is needed. |

### 1.2 F18 precedent surfaces to mirror (all merged, all verified)

| Element | Location | Verified detail |
|---|---|---|
| Adapter member 10 | `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` lines 199–205 (`UpdateEventTimesAsync`, `ApiEnvelope<EventDto>`) | The additive-member pattern: keyword defaults for `requestId`/`cancellationToken`, XML docs stating the wire route and the local-backend fail-closed behavior. |
| 202-no-body write precedent | `IHostAdapterClient.cs` lines 166–170 (`SendMailAsync` returns `ApiEnvelope<object?>`); `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs` lines 29–33 and 71–80 (`executor.ExecuteAsync<object?>(…, _ => null, …)`) | Graph's `202 Accepted` with an empty body is already handled: `GraphRequestExecutor.cs` line 122 treats any `IsSuccessStatusCode` as success and the `_ => null` parser produces `ok: true, data: null`. This is the envelope shape F19 should reuse. |
| Graph write partial | `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs` lines 28–56 | Body serialized with anonymous objects through `GraphRequestExecutor.JsonOptions` (camelCase, `GraphRequestExecutor.cs` line 39); `Principal` route helper (`GraphHostAdapterClient.cs` line 98); `SchedulingDateTime` seconds-precision UTC renderer (`GraphHostAdapterClient.Calendar.cs` lines 128–130). |
| Shared request pipeline | `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs` | Bearer token via `IAppTokenProvider`, `client-request-id` header (line 93), retry only on 429/502/503/504 with `Retry-After` precedence (lines 191–223, all delays via injected `TimeProvider`), D5 matrix 400→`INVALID_REQUEST`, 401/403→`UNAUTHORIZED` (+ Graph `error.code` passthrough to `BridgeErrorCode`), 404→`NOT_FOUND`, 429→`THROTTLED`, 502/503/504→`TRANSPORT_FAILURE` (lines 234–244). A new POST member inherits all of it. |
| Local adapter fail-closed | `src/OpenClaw.Core/HostAdapterHttpClient.cs` lines 153–184 | `UpdateEventTimesAsync` synthesizes a non-retryable `NOT_SUPPORTED` envelope with **no I/O and no token acquisition**; the comment documents why a real request would misreport a capability gap as `TRANSPORT_FAILURE`. |
| Service seam | `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` lines 98–104; `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` lines 149–178 | `RescheduleEventAsync` returns `Task`, guard-clauses the id, delegates with `requestId: correlationId`, throws `InvalidOperationException($"Organizer reschedule failed: {code}: {message}")` on a non-`Ok` envelope. |
| Worker orchestration partial | `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs` | `RescheduleIntent` record (lines 21–27); pure `ComputeRescheduleIntent` (lines 38–71; line 47 requires `context.IsOrganizer == true`); pure `BuildRescheduleActingFlags` (lines 79–80); private audit-record builder (lines 87–109); `EvaluateRescheduleAsync` running intent → guard → gate → dedupe → write → bookkeeping (lines 118–301). |
| Pipeline wiring point | `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` lines 125–131 (`ProposeAndActAsync` already receives `meetingEvent`) and lines 289–303 (the trailing `EvaluateRescheduleAsync(messageId, context, meetingEvent, priority, slots, ct)` call) | F18 already threaded `SchedulingEventDto? meetingEvent` and the proposed `slots` to the exact place F19 needs them. The worker ctor (`SchedulingWorker.cs` lines 21–30) already has every dependency F19 needs — **no new ctor parameter** (F19 uses no `ISeriesMoveHistory`). |
| Constants | `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` lines 24–41 (four F18 consts appended); `src/OpenClaw.Core/Agent/SentActionKey.cs` lines 18–19 (`OrganizerReschedule = "organizer-reschedule"`, colon-free) and 32–59 (`Build`) | Result codes and action types are free-form appended strings; no contract change. |
| Contract-test template | `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs` (348 lines) | Client factory with `Mock<IAppTokenProvider>` + base address `https://graph.example.test/v1.0/` (lines 60–88); structural `JsonDocument` body assertions with absent-property guardrail (lines 149–190); D5 samples (lines 217–268); 429 exhaustion driven by `FakeTimeProvider.Advance` (lines 272–311). `FakeHttpHandler` is defined in `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` line 606 and shared project-wide. The 202 helper precedent is `GraphHostAdapterClientSendMailTests.cs` line 149 (`Accepted() => new(HttpStatusCode.Accepted)`, no content). |
| Worker-test harness | `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleTests.cs` (shared internal helpers: `Options`, `Message`, `OneOnOneEvent(isOrganizer: …)`, `Service`, `CandidateSource`) + `SchedulingWorkerRescheduleEdgeTests.cs` | MSTest + FluentAssertions + Moq (the repository's actual convention; the xUnit/NSubstitute text in `.claude/rules/csharp.md` is a known pre-existing rule-vs-repo mismatch, acknowledged in the F18 spec lines 268–270). The `OneOnOneEvent` factory already exposes `isOrganizer` as a parameter and sets `AllowNewTimeProposals: true` (line 95), so it is directly reusable for attendee-side rows. |
| Property tests | `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleIntentPropertyTests.cs` line 2 (`using CsCheck;`) | CsCheck is the property framework for the two pure F18 helpers; F19 mirrors this for its two pure helpers (T1 obligation). |

### 1.3 Feature docs

- F19 spec draft: `docs/features/active/2026-07-07-attendee-propose-new-time-130/spec.md` (behavior/eligibility already stated at lines 27–44; several sections still template placeholders — this research fills them).
- F19 issue: `docs/features/active/2026-07-07-attendee-propose-new-time-130/issue.md` (draft ACs lines 49–62 name `propose_new_time_disabled` and the tentativelyAccept route).
- F18 spec (template for F19's spec): `docs/features/active/2026-07-07-organizer-reschedule-128/spec.md`.
- F18 research: `docs/features/active/2026-07-07-organizer-reschedule-128/research/2026-07-07T07-35-organizer-reschedule.research.md`.

---

## 2. Graph API Contract (design question 1 — verified against Microsoft Graph v1.0 docs)

**Source of truth:**
- `event: tentativelyAccept` — https://learn.microsoft.com/en-us/graph/api/event-tentativelyaccept?view=graph-rest-1.0
- Propose new meeting times (concept) — https://learn.microsoft.com/en-us/graph/outlook-calendar-meeting-proposals
- `timeSlot` resource — https://learn.microsoft.com/en-us/graph/api/resources/timeslot?view=graph-rest-1.0

**Verified facts from the API doc (fetched 2026-07-07):**

- Route (app-only form): `POST /users/{id | userPrincipalName}/events/{id}/tentativelyAccept`.
- Permissions: Application **`Calendars.ReadWrite`** is the least-privileged application permission — the same permission F18 requires, so the F18 runbook's grant covers F19.
- Request body parameters: `comment` (String, optional); `sendResponse` (Boolean, optional, default `true`); `proposedNewTime` (`timeSlot`) — quoted: "An alternate date/time proposed by an invitee for a meeting request to start and end. **Valid only for events that allow new time proposals. Setting this parameter requires setting sendResponse to `true`.** Optional."
- Success: quoted — "If successful, this method returns `202 Accepted` response code. **It doesn't return anything in the response body.**" (Contrast with F18: `PATCH /events/{id}` returns `200` + the updated event.)
- Documented 400 conditions: `proposedNewTime` included while the event's `allowNewTimeProposals` is `false`, or `proposedNewTime` included while `sendResponse` is `false`.
- The concept doc confirms `tentativelyAccept` is the canonical propose-new-time response ("accept tentatively or decline" are the two response actions that may carry a proposal) and shows the organizer receiving the proposal on the `eventMessageResponse.proposedNewTime` and `attendee.proposedNewTime` properties. The organizer's calendar is **not** modified by this call; only the response message is sent.

### 2.1 Recommended wire request (mocked-Graph contract-test assertions)

- **Method/URL:** `POST` `users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}/tentativelyAccept` — assert `AbsolutePath == "/v1.0/users/paula%40contoso.com/events/evt-1/tentativelyAccept"` per the F18 test precedent. The principal (the invited attendee) mailbox is the target.
- **Headers:** `Authorization: Bearer <token>`, `client-request-id: <correlationId>`, `Content-Type: application/json`. No `Prefer` headers (write path).
- **Body — exactly two top-level properties** (camelCase via `GraphRequestExecutor.JsonOptions`; `dateTime` rendered with the invariant seconds-precision `"s"` format from the UTC instant, reusing the `SchedulingDateTime` helper precedent):

```json
{
  "sendResponse": true,
  "proposedNewTime": {
    "start": { "dateTime": "2026-07-09T14:00:00", "timeZone": "UTC" },
    "end":   { "dateTime": "2026-07-09T14:30:00", "timeZone": "UTC" }
  }
}
```

- `sendResponse` is hardcoded `true` because the docs make it a precondition of `proposedNewTime` (400 otherwise).
- `comment` is deliberately omitted (see Rejected alternatives).
- Contract tests must assert structurally (via `JsonDocument`) that the top level contains exactly `sendResponse` and `proposedNewTime`, that `proposedNewTime` contains exactly `start` and `end` `dateTimeTimeZone` pairs with `timeZone == "UTC"`, and that **no other properties are present** — in particular no `comment` and no top-level `start`/`end`/`body`/`subject`/`attendees` (the F19 analogue of F18's absent-property guardrail: the body structurally cannot rewrite the event).

### 2.2 Response handling

- **202 Accepted, empty body** → `ApiEnvelope<object?>(ok: true, data: null)` via the `_ => null` parser (the exact `SendMailAsync` shape; `GraphRequestExecutor` already treats 202 as success and an empty body never reaches a deserializer).
- **Error matrix (inherited D5, sample per class in contract tests):** 400→`INVALID_REQUEST` non-retryable (the key semantic negative: server-side `allowNewTimeProposals == false` — the client-side eligibility check makes this unreachable in normal operation, but the server remains authoritative); 401/403→`UNAUTHORIZED` with Graph `error.code` (e.g. `ErrorAccessDenied`) passthrough to `BridgeErrorCode`; 404→`NOT_FOUND`; 429→retries then `THROTTLED` (exhaustion under `FakeTimeProvider`); 502/503/504→retries then `TRANSPORT_FAILURE`.

---

## 3. Recommended Design

### 3.1 Adapter seam: new `IHostAdapterClient` member 11 (design question 2)

```csharp
Task<ApiEnvelope<object?>> ProposeNewMeetingTimeAsync(
    string bridgeId,
    DateTimeOffset proposedStartUtc,
    DateTimeOffset proposedEndUtc,
    string? requestId = null,
    CancellationToken cancellationToken = default
);
```

- **Envelope payload: `ApiEnvelope<object?>`** — the simplest consistent choice. The wire response is 202 with no body, and the repository already has exactly this shape for exactly this case: `SendMailAsync` (`IHostAdapterClient.cs` line 166; `GraphHostAdapterClient.SendMail.cs` line 77 parses with `_ => null`). Reusing it means zero new contract types, zero mapper work, and no fabricated data. Rejected payloads: `ApiEnvelope<EventDto>` (nothing to map — would require fabricating an event, violating the no-fabricated-data rule) and a new `Unit`/no-content marker type (a new cross-module contract type for no benefit).
- **`GraphHostAdapterClient`** (new partial `GraphHostAdapterClient.ProposeNewTime.cs`, mirroring `.RescheduleEvent.cs`): `POST users/{Principal}/events/{escaped id}/tentativelyAccept` through `executor.ExecuteAsync<object?>(…, _ => null, requestId, ct)` with the Section 2.1 body. All auth/retry/error mapping inherited.
- **`HostAdapterHttpClient`** (local Stage-0 backend): fail-closed synthesized non-retryable `NOT_SUPPORTED` envelope with **no I/O**, copying the member-10 implementation at `HostAdapterHttpClient.cs` lines 153–184 verbatim in shape; the message states the local backend has no meeting-response route and the attendee propose-new-time requires the Graph adapter.

### 3.2 Service seam: new `ISchedulingService` member

```csharp
Task ProposeNewMeetingTimeAsync(
    string eventId,
    DateTimeOffset proposedStartUtc,
    DateTimeOffset proposedEndUtc,
    string? correlationId = null,
    CancellationToken ct = default
);
```

`HostAdapterSchedulingService` implements it mirroring `RescheduleEventAsync` (lines 149–178) exactly: `ArgumentException.ThrowIfNullOrWhiteSpace(eventId)`, delegate with `requestId: correlationId`, and on a non-`Ok` envelope throw `InvalidOperationException($"Attendee propose-new-time failed: {code}: {message}")`. Returns `Task`, not a DTO — the worker already holds the times it audits, and there is no response body anyway.

### 3.3 Eligibility and evaluation order (design questions 3 and 5)

New partial `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs`, structured exactly like `SchedulingWorker.Reschedule.cs`:

**Pure intent helper** (internal static, property-tested):

```csharp
internal readonly record struct ProposeNewTimeIntent(
    string EventId,
    DateTimeOffset OriginalStartUtc,
    DateTimeOffset OriginalEndUtc,
    DateTimeOffset ProposedStartUtc,
    DateTimeOffset ProposedEndUtc);

internal static ProposeNewTimeIntent? ComputeProposeNewTimeIntent(
    NormalizedMeetingContext context,
    SchedulingEventDto? meetingEvent,
    IReadOnlyList<CandidateSlot> slots);
```

An intent exists iff **all** of: `meetingEvent is not null`; `context.IsOrganizer == false` (the exact mirror of `ComputeRescheduleIntent`'s line-47 check); `context.AllowNewTimeProposals == true` (fail-closed: the normalizer defaults it `false` when the event is missing, `MeetingContextNormalizer.cs` line 68, and the Graph mapper defaults it `false` when the field is absent, `GraphEventMapper.cs` line 58); `meetingEvent.Start`/`End` non-null; `context.EventId` non-empty; `slots.Count > 0`. Proposed start = `slots[0].Start`; duration preserved from the original event (`End - Start`), identical to F18's target computation. Any missing precondition → `null` (silent return, no audit row — identical to today's behavior for non-proposable messages).

**Evaluation order** (`EvaluateProposeNewTimeAsync(messageId, context, meetingEvent, slots, ct)` — note: no `priority` parameter; nothing consumes it):

1. **Intent computation (pure).** No intent → return silently.
2. **Flag gate.** `!CalendarWritePolicy.AttendeeProposeNewTimeAllowed(options)` → log the intended proposal (original → proposed times) at Information and audit `propose_new_time_disabled` with all four time columns populated. **No Graph call, no write-path token acquisition, no dedupe row.** This is the flag-off dry-run-parity mandate; as with F18, the dry-run log/audit rows are themselves new additive output and "no behavior change" is scoped to outbound side effects.
3. **Dedupe.** Key = `SentActionKey.Build(mailbox, messageId, SentActionKey.AttendeeProposeNewTime)`. Already recorded → audit `dedupe_skipped`, return.
4. **Write.** `schedulingService.ProposeNewMeetingTimeAsync(intent.EventId, intent.ProposedStartUtc, intent.ProposedEndUtc, correlationId, ct)`. On exception (excluding `OperationCanceledException`): audit `propose_new_time_failed` with `ErrorDetail`, durable before the exception propagates (F18 step-5 ordering), then rethrow. No dedupe row on failure, so a retry on the next cycle remains possible.
5. **Post-write bookkeeping.** Audit `proposed_new_time`; then `sentActionStore.RecordAsync(dedupeKey, timeProvider.GetUtcNow(), ct)`. Audit-first per the established rule. **No `ISeriesMoveHistory.RecordMoveAsync` call** (see below).

One correlation id (GUID) per outbound-action evaluation, forwarded as the Graph `client-request-id` (issue #107 rule), generated inside the evaluation exactly as `EvaluateRescheduleAsync` does (line 136).

**No move guard on the attendee path — explicit decision with reasoning.** F18's step 2 (`OneOnOneMoveGuard`/`ISeriesMoveHistory`, `SchedulingWorker.Reschedule.cs` lines 141–179) exists to budget how often the owner *actually moves* an organizer-owned recurring series (`series_moves` rows are recorded per performed move and consulted before the next one). The attendee propose-new-time performs no calendar move: it sends a meeting-response message to the organizer, and the event's times change only if the organizer separately accepts the proposal (verified in the concept doc — the organizer applies the change with their own `PATCH`). Consulting the move budget here would spend or read a budget for a move that never happened, and recording into `series_moves` would corrupt the guard's history for any future organizer-side reschedule of the same series. Repetition on the attendee path is bounded instead by the per-message dedupe key (step 3), which is the correct idempotency scope for a response action. Consequently there is also **no blocked state and no blocked result code**: the F19 state machine is a strict subset of F18's (intent → gate → dedupe → write → bookkeeping).

**Pipeline wiring (design question 5).** Add one call in `ProposeAndActAsync` immediately after the F18 evaluation (`SchedulingWorker.Pipeline.cs` lines 294–302):

```csharp
await EvaluateProposeNewTimeAsync(messageId, context, meetingEvent, slots, cancellationToken)
    .ConfigureAwait(false);
```

Mutual exclusivity with the organizer path is guaranteed by the intent predicates, not by pipeline branching: `ComputeRescheduleIntent` requires `context.IsOrganizer == true` (`Reschedule.cs` line 47) and `ComputeProposeNewTimeIntent` requires `context.IsOrganizer == false`, so for any single message at most one of the two evaluations produces an intent and the other returns silently. An explicit `if (context.IsOrganizer) … else …` branch in the pipeline was rejected because it duplicates the predicate logic in a second place and creates a divergence risk; sequential calls keep the pipeline flat and each partial self-contained. Worker unit tests must still assert the exclusivity (an organizer-owned message never triggers the propose path and vice versa).

### 3.4 New constants (design question 4)

Append, following the F18 pattern exactly (`ActionAuditResultCode.cs` lines 24–41; `SentActionKey.cs` lines 18–19):

- `ActionAuditResultCode.ProposedNewTime = "proposed_new_time"` — the tentativelyAccept write completed (202).
- `ActionAuditResultCode.ProposeNewTimeFailed = "propose_new_time_failed"` — the write threw; the original exception still propagates.
- `ActionAuditResultCode.ProposeNewTimeDisabled = "propose_new_time_disabled"` — dry-run: `CalendarWritePolicy.AttendeeProposeNewTimeAllowed` is off.
- `SentActionKey.AttendeeProposeNewTime = "attendee-propose-new-time"` (colon-free, satisfying the `Build` distinctness remark at `SentActionKey.cs` lines 8–12).
- **No blocked code** (`reschedule_blocked` was move-guard-specific; the attendee path has no guard — see 3.3). `DedupeSkipped` is reused.
- ActingFlags: new pure static helper `BuildProposeNewTimeActingFlags(options)` returning `CalendarWriteEnabled=<bool>;EnableAttendeeProposeNewTime=<bool>`, beside (not widening) `BuildRescheduleActingFlags` (`Reschedule.cs` lines 79–80), so both the send path's and the F18 path's persisted `ActingFlags` strings stay byte-identical to pre-F19.
- Audit record builder: private `BuildProposeNewTimeAuditRecord(...)` mirroring `BuildRescheduleAuditRecord` (lines 87–109) with `ActionType: SentActionKey.AttendeeProposeNewTime`, the four time columns from the intent (`Original*` = event's current times, `New*` = proposed times), and the propose ActingFlags snapshot.
- Optional, non-blocking: an explicit `PurviewActivityLogProjection.MapActionType` case for the new action type (the documented fallback covers it otherwise, per the F18 precedent).

---

## 4. File Change List (design question 7)

### Production — add (2)

1. `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs` — the tentativelyAccept POST member (mirrors `.RescheduleEvent.cs`, 57 lines; expected ~60 lines).
2. `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs` — intent record + pure intent helper, ActingFlags snapshot helper, audit-record builder, `EvaluateProposeNewTimeAsync` (mirrors `.Reschedule.cs`, 303 lines, minus the ~60-line guard step; expected ~220 lines).

### Production — modify (6 + 1 optional)

3. `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` (207 lines) — add member 11 with XML docs (wire route, 202-no-body semantics, local-backend `NOT_SUPPORTED`); stays well under 500.
4. `src/OpenClaw.Core/HostAdapterHttpClient.cs` — fail-closed `NOT_SUPPORTED` implementation, no I/O (copy lines 153–184 pattern).
5. `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` (106 lines) — add `ProposeNewMeetingTimeAsync`.
6. `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (180 lines) — implement it (fail-fast mirror of lines 149–178).
7. `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` (332 lines) — one call after line 302.
8. `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` (43 lines) — three new consts; `src/OpenClaw.Core/Agent/SentActionKey.cs` (61 lines) — one new const.
9. *(Optional)* `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs` — explicit action-type case.

**Not modified:** `SchedulingWorker.cs` (no new ctor dependency — unlike F18, existing worker-test builders need no changes), `AgentPolicyOptions.cs`, `CalendarWritePolicy.cs`, `appsettings.json`, `Program.cs` (no DI change), no schema/migration, no `NormalizedMeetingContext` widening (it already carries `AllowNewTimeProposals`).

### Tests — add (3–4)

- `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientProposeNewTimeTests.cs` — contract suite per Section 2: method/route (escaped principal + `/tentativelyAccept` suffix), headers (bearer, `client-request-id`, content-type, no `Prefer`), exact body shape incl. absent-property assertions, 202-empty-body → `ok: true, data: null`, D5 samples (400, 403 `ErrorAccessDenied` passthrough, 404), 429 exhaustion under `FakeTimeProvider`.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeTests.cs` — gate truth table (4 rows, exactly one writes; zero write-path token acquisitions on the other three), dry-run row (audit `propose_new_time_disabled` with populated time columns; zero `ProposeNewMeetingTimeAsync`/dedupe calls), success row (write + `proposed_new_time` audit + dedupe record + **zero `RecordMoveAsync` calls**), failure row (`propose_new_time_failed` audit then rethrow, no dedupe row), dedupe-hit row (`dedupe_skipped`, no Graph request), eligibility fail-closed matrix (organizer-owned event, `AllowNewTimeProposals == false`, missing Start/End, empty EventId, zero slots → silence), mutual-exclusivity rows (organizer message triggers only the F18 path; attendee message triggers only the F19 path). Split an `…EdgeTests.cs` sibling if the file approaches 500 lines (F18 precedent).
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeIntentPropertyTests.cs` — CsCheck properties for the two new pure functions (T1 obligation, `OpenClaw.Core` is T1): duration preservation (`ProposedEnd - ProposedStart == OriginalEnd - OriginalStart` for all valid inputs), eligibility monotonicity/fail-closed (setting `IsOrganizer` true, `AllowNewTimeProposals` false, or removing the event/slots never yields an intent), ActingFlags round-trip.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceProposeNewTimeTests.cs` — delegation, correlation-id forwarding, failure-envelope throw message (mirrors `HostAdapterSchedulingServiceRescheduleTests.cs`).

### Tests — modify (1)

- `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — member-11 `NOT_SUPPORTED` fail-closed with zero HTTP invocations. Existing Moq mocks of `IHostAdapterClient`/`ISchedulingService` absorb the new interface members automatically (loose mocks); no other test churn.

### Docs — add

- `docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md` (authored during implementation; F18/F11 HI-1 record-shape precedent).

---

## 5. Behavior Semantics Summary (for the spec to formalize)

- **Gate truth table:** identical shape to F18 — write allowed only when `CalendarWriteEnabled && EnableAttendeeProposeNewTime`; the other three rows are dry-runs auditing `propose_new_time_disabled`.
- **Fail-closed rules:** null/missing event, organizer-owned message (`IsOrganizer == true`), `AllowNewTimeProposals == false`, missing original times, empty event id, zero proposed slots, gate off, local-backend `NOT_SUPPORTED`, and any failure envelope or exception all result in **no write**. Any ambiguity resolves to "no write" (issue #130 constraint).
- **Ordering:** intent → gate → dedupe → write → audit-then-dedupe-record. No guard step; no `series_moves` interaction in any branch.
- **Idempotency:** dedupe key `{mailbox}:{messageId}:attendee-propose-new-time`; a restart after a successful write skips with `dedupe_skipped`.
- **Flag-off no-behavior-change scoping:** as with F18, the dry-run log line and disabled audit row are new additive output; "no outbound behavior change" means no Graph request, no write-path token acquisition, no dedupe row. Existing send-path and F18-path audit `ActingFlags` strings remain byte-identical.
- **Non-goal (carry into the spec):** `SendOnBehalfAuthorizer` (F15) does not apply — the POST targets the principal's own event under app-only `Calendars.ReadWrite`; no mailbox representation occurs (same reasoning as F18 spec lines 61–65).

---

## Automation Feasibility

No Azure/Exchange tenant credentials exist in this environment or in CI. The live Graph `POST /events/{id}/tentativelyAccept` write therefore **cannot be exercised automatically**; the write path ships proven by mocked-Graph contract tests behind the flag, and live-tenant verification is a `human_interaction` requirement with `response: exception` and a runbook, following the F18 HI-1 / F11 HI-1 precedent.

| # | Step | Automatable? | Classification | Recommended `human_interaction` response |
|---|---|---|---|---|
| 1 | All production code changes (adapter member 11, service member, worker partial, constants) | Yes | Code authoring | — |
| 2 | Gate truth-table, eligibility fail-closed matrix, audit, dedupe, and mutual-exclusivity unit/property tests | Yes | Deterministic unit tests (Moq + `FakeTimeProvider`) | — |
| 3 | Graph `POST /events/{id}/tentativelyAccept` request/response/error contract verification | Yes — established `FakeHttpHandler` mocked-Graph pattern, base `https://graph.example.test/v1.0/` | Mocked contract tests | — |
| 4 | Coverage, mutation (T1), formatting, lint, architecture-boundary gates | Yes | Standard toolchain loop | — |
| 5 | `Calendars.ReadWrite` application-permission grant + admin consent in Azure AD (shared with F18; may already be granted if F18's runbook was executed) | **No** — tenant-admin action; no credentials here or in CI | Live-tenant privileged administration | **exception** (combined into #6's runbook) |
| 6 | Live verification that the tentativelyAccept actually delivers a proposal (second mailbox organizes a test meeting inviting the principal with `allowNewTimeProposals` on → operator enables `OpenClaw__AgentPolicy__CalendarWriteEnabled` and `…EnableAttendeeProposeNewTime` → observe the 202, the `proposed_new_time` audit row, the dedupe row, and the proposal arriving on the organizer's `attendee.proposedNewTime` → disable flags) | **No** — requires a live tenant, two real mailboxes, and a human flipping production-affecting flags | Live-tenant end-to-end verification | **exception**, `runbook_path: docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md` |
| 7 | Production flag rollout decision | **No** — operator/business decision by design | Operational rollout | Covered by the same runbook; no separate requirement |

Everything except the permission grant, the live two-mailbox verification, and the rollout decision is automatable with the mocked Graph seam. Expected orchestrator-state outcome: one `human_interaction` requirement (HI-1 for this feature) with `response: "exception"` and the runbook path above, satisfying the `.claude/rules/orchestrator-state.md` invariant that an `exception` carries a non-empty `runbook_path`.

---

## Testing Implications (strategy, no test code)

- **Unit (worker):** MSTest + FluentAssertions + Moq, reusing the `SchedulingWorkerRescheduleTests` internal harness (`Options`/`Message`/`OneOnOneEvent(isOrganizer: false)`/`Service`/`CandidateSource`). All time via `FakeTimeProvider`; no `Task.Delay`/sleeps (banned in tests).
- **Property (T1 obligation):** >= 1 CsCheck property per new pure function — `ComputeProposeNewTimeIntent` (duration preservation; fail-closed monotonicity over `IsOrganizer`/`AllowNewTimeProposals`/event/slots) and `BuildProposeNewTimeActingFlags` (round-trip parse), mirroring `SchedulingWorkerRescheduleIntentPropertyTests`.
- **Contract (host-service boundary, required per `general-unit-test.md`):** the Section 2 mocked-Graph suite; the key F19-specific assertions are the 202-empty-body success mapping and the `sendResponse`/`proposedNewTime`-only body with absent-property checks. No DTO shape changes, so no DTO contract tests.
- **Architecture:** existing NetArchTest boundary tests must pass unmodified — the new partial stays inside `CloudGraph`; the worker partial references no `CloudGraph` type and reaches Graph only through `ISchedulingService`.
- **Integration:** deliberately none in CI (live tenant is the recorded human exception); the local-backend `NOT_SUPPORTED` test doubles as the adapter-smoke negative.
- **Mutation (T1, pre-merge/nightly):** the truth-table and fail-closed branches are the mutation-sensitive surface — assert both the action taken and the actions **not** taken (zero Graph calls, zero token acquisitions, zero `RecordMoveAsync` calls) so condition-negation mutants die.
- **Coverage:** line >= 85% / branch >= 75% maintained; no regression on changed lines.

---

## Rejected alternatives

- **`decline` + `proposedNewTime` instead of `tentativelyAccept`:** Graph supports proposals on both response actions (concept doc), but decline removes the meeting from the attendee's calendar and signals non-attendance; the agent's deterministic decision is "can attend, at a different time," which tentative-accept represents. The F19 issue and spec drafts also name tentativelyAccept. Rejected.
- **`ApiEnvelope<EventDto>` (F18's envelope) or a new `Unit`/no-content marker payload for member 11:** tentativelyAccept returns 202 with no body; mapping to `EventDto` would fabricate data, and a marker type adds a new cross-module contract type with no consumer. `ApiEnvelope<object?>` already exists for exactly this case (`SendMailAsync`). Rejected.
- **Reusing/patching the attendee's event copy via `UpdateEventTimesAsync`:** patching the attendee's copy does not implement the meeting-response protocol — the organizer would receive no proposal, and the attendee's calendar would silently diverge from the organizer's. The propose-new-time semantics exist only on the response actions. Rejected.
- **Consulting `OneOnOneMoveGuard`/`ISeriesMoveHistory` (with a `propose_new_time_blocked` code):** the move budget governs performed calendar moves on organizer-owned series; a proposal moves nothing, and writing `series_moves` rows here would corrupt the guard history for future organizer reschedules. Idempotency is already bounded by the per-message dedupe key. Rejected — and therefore no blocked result code exists on this path.
- **Including a `comment` in the wire body:** optional per Graph, but its content would either be hardcoded prose or derived message text, adding a nondeterministic/localizable surface to an otherwise fully structural body and complicating the exact-body contract assertions. The proposal is fully expressed by `proposedNewTime`; a comment can be added later as an additive body property without a contract change. Rejected for this feature.
- **Pipeline-level `if (context.IsOrganizer)` branch to select the F18 vs F19 evaluation:** duplicates the intent predicates' organizer checks in a second location and risks divergence; sequential evaluation calls with self-contained intent predicates are flatter and match the F18 wiring style. Rejected.
- **Widening `NormalizedMeetingContext` or `SchedulingEventDto`:** not needed — both already carry `IsOrganizer` and `AllowNewTimeProposals` (Section 1.1). No change proposed.
