# F18 organizer-reschedule (#128) — Deep Research

- **Issue:** #128 (epic openclaw-vision, Epic D, feature F18, wave 4)
- **Date:** 2026-07-07T07-35
- **Author:** task-researcher
- **Complexity:** C3 (floor C3, signal `cross_module_contract_change`)
- **Scope:** First real calendar-write RPC — Microsoft Graph `PATCH /events/{id}` behind `ENABLE_ORGANIZER_RESCHEDULE` (default OFF; flag-off = dry-run parity, no Graph write).

All findings below were verified by reading the named files in this worktree on 2026-07-07.

---

## 1. Current-State Seam Map (verified)

### 1.1 Gate scaffolding (F12 / #109) — merged, unconsumed by any write path

| Element | Location | Verified detail |
|---|---|---|
| `CalendarWritePolicy.OrganizerRescheduleAllowed(AgentPolicyOptions)` | `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` | Pure static predicate: `options.CalendarWriteEnabled && options.EnableOrganizerReschedule`; throws `ArgumentNullException` on null options. Already covered by `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs` + property tests. |
| `AgentPolicyOptions.CalendarWriteEnabled` | `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` (line 94) | Global kill switch, default `false` (master §7.5). |
| `AgentPolicyOptions.EnableOrganizerReschedule` | same file (line 104) | Per-path flag, default `false`; canonical env var `OpenClaw__AgentPolicy__EnableOrganizerReschedule`; bound from `OpenClaw:AgentPolicy` in `src/OpenClaw.Core/Program.cs` (line 84–86). |

The three-flag composition is therefore two booleans on one options bag composed by one pure predicate; "three-flag" counts the path-specific predicate as the third element. No production code currently calls `OrganizerRescheduleAllowed`.

### 1.2 Move-guard seam (F8 / #105) — merged, built for F18, currently has zero consumers

| Element | Location | Verified detail |
|---|---|---|
| `OneOnOneMoveGuard.ComputeAnswers(movedStarts, occurrenceStarts, candidateStart)` | `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` | Pure; anchors timestamps to UTC calendar dates; rolling window = six greatest distinct anchors ≤ candidate. Incomplete occurrence lists are conservative by design (D2): fewer anchors shrink the window, so the guard blocks more, never less. An empty occurrence list is valid input. |
| `OneOnOneMoveGuard.CanMove(meeting, ownerEmail, requesterEmail, priority, policy, history)` | same file | For `ONE_ON_ONE`: allowed iff `MovesInLastSixOccurrences < 2 && !MovedPreviousWeek`. Every other `RecurringMeetingKind` delegates to `MovePolicy.CanMove` unchanged. |
| `OneOnOneMoveGuard.ResolveSeriesKey(meeting)` | same file | `SeriesMasterId` when present, else `EventId`; throws `ArgumentException` when both are absent — a fail-closed input validation the worker must guard before calling. |
| `ISeriesMoveHistory` | `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs` | `RecordMoveAsync(seriesKey, occurrenceStartUtc, movedAtUtc, ct)` — idempotent on `(seriesKey, occurrenceStartUtc)`; clock-free (timestamps caller-supplied). `GetMovedOccurrenceStartsAsync(seriesKey, ct)` — distinct starts, most recent first. |
| `CoreCacheRepository : ISeriesMoveHistory` | `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs` (note: repo root of `OpenClaw.Core`, not a `Persistence/` folder) | SQLite `series_moves(series_key, occurrence_start_utc, moved_at_utc)` with `ON CONFLICT DO NOTHING`; lazy once-per-instance schema ensure. |
| DI registration | `src/OpenClaw.Core/Program.cs` (line 91–93) | `ISeriesMoveHistory` singleton bound to `CoreCacheRepository`. Verified by repo-wide grep: **no production code consumes `ISeriesMoveHistory` or calls `OneOnOneMoveGuard.CanMove` today.** F18 is the first consumer. |

Guard-rejection surfacing: `CanMove` returns `false` (no exception). F18 surfaces a rejection as a logged decision plus an audit record (Section 2.5) and performs no write.

### 1.3 Scheduling runtime — the plug point

| Element | Location | Verified detail |
|---|---|---|
| `SchedulingWorker` (ctor) | `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` | Primary-ctor deps: `ISchedulingService`, `ISentActionStore`, `IActionAuditLog`, `ISchedulingCandidateSource`, `IOptions<AgentPolicyOptions>`, `TimeProvider`, `ILogger`. Does **not** yet take `ISeriesMoveHistory`. |
| Pipeline | `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` | `ProcessMessageAsync` hydrates message + `meetingEvent` (`SchedulingEventDto?`, direct lookup then calendar-view fallback), normalizes (D1), triages (D2), classifies priority/recurrence (D3), then calls `ProposeAndActAsync(messageId, context, priority, ct)`. **`meetingEvent` is not threaded into `ProposeAndActAsync`**, and `NormalizedMeetingContext` (`src/OpenClaw.Core/Agent/Models/NormalizedMeetingContext.cs`) carries no event Start/End — the original times needed for the PATCH and the audit record live only on the hydrated `SchedulingEventDto` (`Start`/`End`, lines 49–52 of `SchedulingEventDto.cs`). |
| The exact plug point | `SchedulingWorker.Pipeline.cs` lines 288–294 | `ProposeAndActAsync` ends with `if (!options.CalendarWriteEnabled) { log "not writing the calendar" }` — a stub with no else-branch. F18 replaces this block with the reschedule evaluation. |
| `ISchedulingService` | `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` (contracts folder, not `Runtime/`) | Middleware seam (D6): six reads + `SendMailAsync(request, correlationId, ct)`. The `correlationId` flows to the adapter as the request id so audit rows correlate with `client-request-id` (#107 precedent to replicate for the write). |
| `HostAdapterSchedulingService` | `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` | Wraps `IHostAdapterClient` + `SchedulingDtoMapper`. `SendMailAsync` pattern to mirror: map request → call client → on non-`Ok` envelope throw `InvalidOperationException($"Outbound sendMail failed: {code}: {message}")`; client exceptions propagate unwrapped. |
| Audit partial | `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs` | `BuildActingFlags` = `SendEnabled=<bool>;CalendarWriteEnabled=<bool>`. `BuildAuditRecord` hardcodes `ActionType: SentActionKey.ProposalReply` and null time columns — a reschedule needs its own record builder. `WriteAuditSafelyAsync` is the one sanctioned catch-and-log boundary (reuse as-is). |
| Dedupe | `src/OpenClaw.Core/Agent/SentActionKey.cs`, `ISentActionStore` | Key = `{mailbox}:{messageId}:{actionType}`, colon-free components required. Single constant today: `ProposalReply = "proposal-reply"`. Worker consults before send, records after success. |
| Mailbox identity | `SchedulingWorker.Pipeline.cs` line 297 | `MailboxUpn()` is synthetic: `owner@{InternalDomain}`. Pre-existing; reschedule audit/dedupe rows inherit this value. |

### 1.4 Graph-backed adapter (F13 / #115) — the pattern to replicate

| Element | Location | Verified detail |
|---|---|---|
| `IHostAdapterClient` | `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` | Nine members (status, 5 reads, mailboxSettings, freeBusy, sendMail). This is the cross-module portability boundary; the write member added here is the `cross_module_contract_change` floor signal. Two implementations exist (verified by grep): `GraphHostAdapterClient` and `HostAdapterHttpClient`. |
| `GraphHostAdapterClient` | `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.cs` + `.Messages`/`.Calendar`/`.SendMail` partials | `internal sealed partial`, speaks Graph REST v1.0 directly through `GraphRequestExecutor`. Target mailbox is fixed by `GraphAdapterOptions.PrincipalMailboxUpn` (`Principal` property, URL-escaped). `EventSelect` `$select` list and `GraphEventMapper.Map` produce `EventDto`. |
| `GraphRequestExecutor` | `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs` | Shared pipeline: per-attempt request factory, bearer token via `IAppTokenProvider`, `client-request-id` header, retries only 429/502/503/504 with `Retry-After` precedence over exponential backoff (all delays via injected `TimeProvider`), D5 error matrix: 400→`INVALID_REQUEST`, 401/403→`UNAUTHORIZED`, 404→`NOT_FOUND`, 429→`THROTTLED`, 502/503/504→`TRANSPORT_FAILURE`, else `INTERNAL_ERROR`; Graph `error.code` passes through to `ApiError.BridgeErrorCode`. `JsonException` on success body → `TRANSPORT_FAILURE`; `GraphMappingException` → `INTERNAL_ERROR`. A new PATCH member gets all of this for free. |
| Send-write precedent | `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs` | POST body serialized camelCase via `GraphRequestExecutor.JsonOptions`; success parsed with a trivial `_ => null`. This is the write-member shape to copy. |
| `HostAdapterHttpClient` | `src/OpenClaw.Core/HostAdapterHttpClient.cs` | Stage-0 local adapter (GET/POST only). The local HostAdapter/MailBridge has **no calendar-write route** — master line 108 lists `PATCH /events/{id}` as "deferred; behind feature flags" for the COM bridge. |
| Mocked-Graph test doubles | `tests/OpenClaw.Core.Tests/CloudGraph/*` | Pattern (verified in `GraphHostAdapterClientSendMailTests.cs`): `FakeHttpHandler` (defined in `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`, shared across the test project) wrapping a per-test `Func<HttpRequestMessage, Task<HttpResponseMessage>>`; `Mock<IAppTokenProvider>` (Moq, `MockBehavior.Strict`); `FakeTimeProvider`; base address `https://graph.example.test/v1.0/`; structural `JsonDocument` assertions on captured request bodies; retry-exhaustion driven by `timeProvider.Advance` loops. Test framework is MSTest (`[TestClass]`/`[TestMethod]`/`[DataTestMethod]`) + FluentAssertions + Moq — follow this actual repo convention, not the xUnit/NSubstitute text in `.claude/rules/csharp.md` (known pre-existing rule-vs-repo mismatch). |

### 1.5 Audit path (F9 / #107) — pre-provisioned for F18

| Element | Location | Verified detail |
|---|---|---|
| `ActionAuditRecord` | `src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs` | Already carries `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc`, documented as "for Stage 2 (F18/F19) reschedules". No contract change needed to record a reschedule. |
| `ActionAuditResultCode` | `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` | Const strings `sent`/`send_failed`/`dedupe_skipped`/`send_disabled`; XML doc states Stage 2 "can append reschedule codes without a contract or schema change". Store does not validate membership. |
| `IActionAuditLog` | `src/OpenClaw.Core/Agent/Contracts/IActionAuditLog.cs`; impl `CoreCacheRepository.AuditLog.cs` | Clock-free; worker wraps writes in `WriteAuditSafelyAsync`. |
| Purview projection (F20 groundwork) | `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs` | Total mapping with documented fallback (`UnknownActivity`/`Unknown`) for unrecognized action types — a new action type does not break it; an explicit case is optional polish. |

### 1.6 Send-on-behalf (F15 / #119) — no interaction required

`SendOnBehalfAuthorizer` (`src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`, consumed only in `GraphHostAdapterClient.SendMail.cs`) gates **assistant-mailbox representation** on `POST /users/{a}/sendMail` — whether the assistant may stamp `from = principal`. The organizer-reschedule PATCH targets the principal's own calendar (`/users/{p}/events/{id}`) under app-only `Calendars.ReadWrite`; no representation of one mailbox by another occurs, so the allowlist does not apply. Tenant-side write authorization is the F11 RBAC management scope plus the F17 startup scope validation. The spec should state this explicitly as a non-goal so review does not flag the absence of an allowlist check.

### 1.7 Master/roadmap anchors

- Master §11.1 (lines 1197–1231): `PATCH /users/{id}/events/{event-id}`, update at least `start`/`end`; guardrail — do not touch `body` on online meetings (preserving the online-meeting blob); log old and new times. Example body uses `dateTimeTimeZone` pairs.
- Master line 413 / 621 / 1577: `ENABLE_ORGANIZER_RESCHEDULE=true` is the first calendar write enabled, only after admin grants `Calendars.ReadWrite`.
- Gap analysis (`docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, Epic D item 18, Stage-2 table line 72): organizer reschedule not started; depends on Epic C + flag naming (F12), which are merged.
- Epic plan (`docs/features/epics/openclaw-vision/epic-plan.md`): F18 row — C3, floor C3, `cross_module_contract_change`, wave 4; `depends_on: [2026-07-02-calendar-write-flags-109, send-on-behalf-allowlist, azure-bicep-iac, negative-scope-smoke-test]`.

---

## 2. Recommended Design

### 2.1 Placement summary

Pure decision logic stays in existing host-neutral domain surfaces (`CalendarWritePolicy`, `OneOnOneMoveGuard`, `MovePolicy` — all already pure and tested). Orchestration lives in a new `SchedulingWorker.Reschedule.cs` partial. I/O crosses exactly two existing seams: `ISchedulingService` (runtime→service) and `IHostAdapterClient` (service→adapter). The Graph HTTP call lives only in a new `GraphHostAdapterClient` partial. This satisfies `.claude/rules/architecture-boundaries.md`: domain does not depend on adapters; mailbox data flows only through Graph; business behavior stays host-neutral.

### 2.2 Adapter seam: new `IHostAdapterClient` member (member 10)

```csharp
Task<ApiEnvelope<EventDto>> UpdateEventTimesAsync(
    string bridgeId,
    DateTimeOffset newStartUtc,
    DateTimeOffset newEndUtc,
    string? requestId = null,
    CancellationToken cancellationToken = default
);
```

- **`GraphHostAdapterClient`** (new partial `GraphHostAdapterClient.RescheduleEvent.cs`): `PATCH users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}` through `executor.ExecuteAsync`, body per Section 3, success body mapped with the existing `GraphEventMapper.Map(DeserializeWire<GraphEvent>(body))` (Graph returns the full updated event on 200). All retry/backoff/error mapping is inherited from `GraphRequestExecutor` unchanged.
- **`HostAdapterHttpClient`** (local Stage-0 backend): fail-closed synthesized failure envelope with **no I/O** — `ApiEnvelope<EventDto>(false, null, meta, new ApiError("NOT_SUPPORTED", "The local HostAdapter backend has no calendar-write route; organizer reschedule requires the Graph adapter.", null, Retryable: false))`. Rationale: the COM bridge write is explicitly deferred (master line 108); issuing a real PATCH to a nonexistent route would produce a misleading `TRANSPORT_FAILURE`. `ApiError.Code` is a free-form string, so the new literal is not a contract change.

Narrow scope note: the member intentionally updates only `start`/`end` (name `UpdateEventTimesAsync`, not a general `UpdateEventAsync`), which makes the online-meeting-blob guardrail structural — the request body cannot carry `body`/`subject`/`attendees`.

### 2.3 Service seam: new `ISchedulingService` member

```csharp
Task RescheduleEventAsync(
    string eventId,
    DateTimeOffset newStartUtc,
    DateTimeOffset newEndUtc,
    string? correlationId = null,
    CancellationToken ct = default
);
```

`HostAdapterSchedulingService` implements it mirroring `SendMailAsync` exactly: guard-clause the id, delegate to `hostAdapterClient.UpdateEventTimesAsync(..., requestId: correlationId, ...)`, and on a non-`Ok` envelope throw `InvalidOperationException($"Organizer reschedule failed: {code}: {message}")`. Returning `Task` (not the updated DTO) keeps the seam minimal; the worker already holds the original and target times it needs for the audit record, and response-shape verification belongs to the adapter contract tests.

### 2.4 Worker orchestration: `SchedulingWorker.Reschedule.cs`

Constructor gains `ISeriesMoveHistory seriesMoveHistory` (already registered in `Program.cs`; no DI change needed beyond the ctor parameter). `ProcessMessageAsync` threads the hydrated `meetingEvent` (`SchedulingEventDto?`) into `ProposeAndActAsync`, whose trailing `!CalendarWriteEnabled` stub is replaced by a call to the new evaluation method. Threading the DTO is preferred over widening `NormalizedMeetingContext` (see rejected alternatives).

Evaluation order (each step logs its outcome; audit rows carry the four time columns whenever an intent exists):

1. **Intent computation (pure, extract as an internal static helper for property testing).** Eligible iff: `meetingEvent is not null`, `context.IsOrganizer == true`, `meetingEvent.Start`/`End` are non-null, `context.EventId` non-empty, and `slots.Count > 0`. Target interval = first proposed slot's start; duration preserved from the original event (`End - Start`). No intent → return (no audit row; identical to today's behavior for non-reschedulable messages).
2. **Move-guard consult (before the flag gate, so the dry-run reports the true decision).** `seriesKey = OneOnOneMoveGuard.ResolveSeriesKey(context)` (guarded — step 1 assures `EventId`); `movedStarts = await seriesMoveHistory.GetMovedOccurrenceStartsAsync(seriesKey, ct)` (local SQLite read, cheap); occurrence starts from the already-available calendar-view data filtered by `SeriesMasterId` where present, else empty list (conservative per D2); `answers = ComputeAnswers(...)`; `allowed = OneOnOneMoveGuard.CanMove(context, MailboxUpn(), context.MessageFrom, priority, ownerPolicy, answers)`. Blocked → audit `reschedule_blocked` + log, **no write, regardless of flags**.
3. **Three-flag gate.** `!CalendarWritePolicy.OrganizerRescheduleAllowed(options)` → log the intended move (old→new times) and audit `reschedule_disabled` with the time columns populated; **no Graph call, no `series_moves` write, no dedupe write** (dry-runs must never consume move-history budget or dedupe slots).
4. **Dedupe.** Key = `SentActionKey.Build(mailbox, messageId, SentActionKey.OrganizerReschedule)` (new const `"organizer-reschedule"`, colon-free). Already recorded → audit `dedupe_skipped` (action type distinguishes it from send rows), return.
5. **Write.** `schedulingService.RescheduleEventAsync(eventId, newStart, newEnd, correlationId, ct)`. On exception: audit `reschedule_failed` with `ErrorDetail` (mirroring the send-failure ordering — durable before the exception propagates to `ProcessMessageSafelyAsync`), rethrow.
6. **Post-write bookkeeping (in this order).** Audit `rescheduled`; `seriesMoveHistory.RecordMoveAsync(seriesKey, originalStartUtc, timeProvider.GetUtcNow(), ct)`; `sentActionStore.RecordAsync(dedupeKey, ...)`. Audit-first matches the send path's "audit reflects the actual side effect even if later bookkeeping fails" rule.

One correlation id per outbound-action evaluation (existing #107 rule); the reschedule evaluation generates its own GUID, forwarded to the adapter as `client-request-id`.

### 2.5 Audit composition

- New result codes appended to `ActionAuditResultCode`: `Rescheduled = "rescheduled"`, `RescheduleFailed = "reschedule_failed"`, `RescheduleDisabled = "reschedule_disabled"`, `RescheduleBlocked = "reschedule_blocked"`. `DedupeSkipped` is reused.
- New action type on `SentActionKey`: `OrganizerReschedule = "organizer-reschedule"`.
- Reschedule records populate `EventId`, `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc` (master §13 step 12: "original times / proposed times").
- **ActingFlags:** use a reschedule-specific snapshot `CalendarWriteEnabled=<bool>;EnableOrganizerReschedule=<bool>` (new pure static helper beside `BuildActingFlags`). Do **not** widen the existing `BuildActingFlags` — that would change the persisted `ActingFlags` string on every send record, violating strict flag-off no-behavior-change for the existing path.
- Optional polish: add a `SentActionKey.OrganizerReschedule => ("Reschedule organizer event", "Update", SendCategory-or-new-CalendarCategory)` case to `PurviewActivityLogProjection.MapActionType`; the documented fallback makes this non-blocking.

### 2.6 Behavior semantics (for the spec to formalize)

- **Flag-off (either flag false, the default):** intent is computed and guard-consulted, the intended move is logged at Information and audited as `reschedule_disabled` — and no Graph request, token acquisition for the write, `series_moves` row, or dedupe row is produced. This is the "computes and logs the intended reschedule but performs no Graph write" mandate. Note for the spec: today's pipeline computes no reschedule intent at all, so the dry-run log/audit rows are themselves new (additive) output; "no behavior change" is scoped to outbound side effects. The existing `"CalendarWriteEnabled is false; not writing the calendar"` log line is subsumed by the richer dry-run log.
- **Fail-closed rules:** null/missing event, non-organizer, missing original times, zero slots, guard block, gate off, `NOT_SUPPORTED` local backend, or any failure envelope/exception → no write. Ambiguity always resolves to "no write" (issue #128 constraint).
- **Ordering:** guard before gate (dry-run fidelity); audit before move-history/dedupe records after a successful write; move-history recorded only for actual writes.
- **Idempotency:** dedupe key per `(mailbox, messageId, organizer-reschedule)`; a restart after a successful write skips with `dedupe_skipped`.

### 2.7 Rejected alternatives (brief)

- **Separate `ICalendarWriteClient` registered only with the Graph adapter:** avoids touching `IHostAdapterClient`, but diverges from the established sendMail seam pattern the delegation mandates, adds DI-optionality branching in the worker, and hides the write capability from the portability boundary. Rejected.
- **`HostAdapterHttpClient` issues a real PATCH to the local adapter:** the route does not exist; a 404-without-envelope would surface as `TRANSPORT_FAILURE`, misreporting a permanent capability gap as a transient fault. Rejected in favor of the synthesized `NOT_SUPPORTED` envelope.
- **Widening `NormalizedMeetingContext` with Start/End:** touches a heavily tested pure contract consumed by triage/priority/move layers for a value only the reschedule path needs. Threading the already-hydrated `SchedulingEventDto` through one internal method signature is smaller. Rejected.
- **Returning the updated `SchedulingEventDto` from `RescheduleEventAsync`:** the worker does not need it; keeps parity with `SendMailAsync`'s `Task` shape. Rejected.

---

## 3. Graph `PATCH /events/{id}` Contract (mocked-Graph contract-test assertions)

### 3.1 Request

- **Method/URL:** `PATCH` `https://graph.example.test/v1.0/users/{principal-upn-url-escaped}/events/{event-id-url-escaped}` — assert `AbsolutePath == "/v1.0/users/paula%40contoso.com/events/evt-1"` per the sendMail test precedent. The principal (not assistant) mailbox is the target.
- **Headers:** `Authorization: Bearer <token>` (from `IAppTokenProvider`), `client-request-id: <correlationId>` (assert passthrough), `Content-Type: application/json`. No `Prefer` headers (write path; the read-path timezone/body preferences do not apply).
- **Body (exactly two top-level properties; camelCase via `GraphRequestExecutor.JsonOptions`):**

```json
{
  "start": { "dateTime": "2026-07-09T14:00:00", "timeZone": "UTC" },
  "end":   { "dateTime": "2026-07-09T14:30:00", "timeZone": "UTC" }
}
```

  - `dateTime` rendered from the UTC instant with the invariant seconds-precision `"s"` format — reuse/mirror the existing `SchedulingDateTime` helper precedent in `GraphHostAdapterClient.Calendar.cs` (getSchedule body).
  - Contract tests must assert (structurally, via `JsonDocument`) that **no other properties are present** — in particular no `body`, `subject`, `location`, or `attendees` — which is the online-meeting-blob guardrail from master §11.1 made testable.

### 3.2 Response handling

- **200 OK + updated event JSON** → parse via `GraphEventMapper.Map` → `ApiEnvelope<EventDto>(ok: true)`; assert the mapped `Start`/`End` reflect the response payload.
- **2xx with unparseable body** → `TRANSPORT_FAILURE` (executor's `JsonException` path).
- **2xx with missing required event fields** → `INTERNAL_ERROR` (`GraphMappingException` path) — no fabricated data.

### 3.3 Error handling / fail-closed (inherited D5 matrix — sample per class in contract tests)

| HTTP | `ApiError.Code` | Retryable | Note |
|---|---|---|---|
| 400 | `INVALID_REQUEST` | false | e.g. malformed dateTimeTimeZone. |
| 401 / 403 | `UNAUTHORIZED` | false | The key negative case: app lacks `Calendars.ReadWrite` or event outside the RBAC scope. Assert Graph `error.code` (e.g. `ErrorAccessDenied`) passes through to `BridgeErrorCode`. |
| 404 | `NOT_FOUND` | false | Event deleted/moved. |
| 429 | retry, then `THROTTLED` | true | `Retry-After` precedence; exhaustion test driven by `FakeTimeProvider.Advance`. |
| 502/503/504 | retry, then `TRANSPORT_FAILURE` | true | |

Worker-side fail-closed consequence to assert: any non-`Ok` envelope → `HostAdapterSchedulingService` throws → worker audits `reschedule_failed` → **no** `series_moves` row and **no** dedupe row (a failed write must not consume move budget, so a retry on the next cycle is possible).

---

## 4. File Change List

### Production — add (2)

1. `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs` — the PATCH member (mirrors the `.SendMail.cs` partial).
2. `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs` — intent computation (pure internal static helper), guard consult, gate, dedupe, write, bookkeeping, reschedule audit-record builder + reschedule ActingFlags snapshot builder (kept here, not in `.Audit.cs`, to respect the 500-line cap and keep the write path cohesive).

### Production — modify (7 + 2 optional)

3. `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` — add `UpdateEventTimesAsync` (member 10; XML-doc the wire route and the local-backend `NOT_SUPPORTED` behavior).
4. `src/OpenClaw.Core/HostAdapterHttpClient.cs` — fail-closed `NOT_SUPPORTED` implementation (no I/O).
5. `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` — add `RescheduleEventAsync`.
6. `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — implement it (SendMailAsync-mirrored fail-fast).
7. `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` — ctor gains `ISeriesMoveHistory`.
8. `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` — thread `meetingEvent` into `ProposeAndActAsync`; replace the trailing `!CalendarWriteEnabled` stub with the reschedule-evaluation call.
9. `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` — four new consts; `src/OpenClaw.Core/Agent/SentActionKey.cs` — `OrganizerReschedule` const.
10. *(Optional)* `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs` — explicit case for the new action type (fallback covers it otherwise).
11. *(Optional)* `docs/open-claw-approach.master.md` untouched; feature docs updated per Definition of Done.

No `Program.cs` change is required (`ISeriesMoveHistory` already registered). No schema change (`series_moves` and `action_audit` tables already exist).

### Tests — add (3)

- `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs` — mocked-Graph contract tests (Section 3): method/URL/headers, exact body shape incl. absent-property guardrail assertions, 200→`EventDto` mapping, D5 samples (400, 401/403 with `ErrorAccessDenied` passthrough, 404), 429 exhaustion under `FakeTimeProvider`, unparseable/mapping-gap bodies.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleTests.cs` — gate truth table (4 flag rows → exactly one writes), guard-block row (blocked despite both flags on; audit `reschedule_blocked`), dry-run row asserts audit `reschedule_disabled` with populated time columns and zero `RescheduleEventAsync`/`RecordMoveAsync`/dedupe calls, success row asserts write + `rescheduled` audit + `RecordMoveAsync(seriesKey, originalStart)` + dedupe record, failure row asserts `reschedule_failed` audit then rethrow and no bookkeeping, dedupe-hit row asserts `dedupe_skipped`, no-intent rows (no event / not organizer / no slots / missing times) assert silence.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleIntentPropertyTests.cs` (or fold into an existing property-test file) — >= 1 property test per new pure function (intent computation; reschedule flags snapshot), per the T1 property-density gate (`OpenClaw.Core` is T1 in `quality-tiers.yml`).

### Tests — modify (2–3)

- `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — `RescheduleEventAsync` delegation, correlation-id forwarding, failure-envelope throw message.
- `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — `UpdateEventTimesAsync` returns `NOT_SUPPORTED` fail-closed with zero HTTP invocations.
- Existing `SchedulingWorker*Tests` construct the worker directly; the new ctor parameter requires a mechanical `Mock<ISeriesMoveHistory>` addition in their builders (Moq mocks of `IHostAdapterClient`/`ISchedulingService` absorb the new interface members automatically).

Estimated added production surface: ~250–350 lines across the files above; every touched file stays under the 500-line cap (largest today: `SchedulingWorker.Pipeline.cs` at 323 lines — the stub replacement delegates to the new partial to keep it flat).

---

## 5. Candidate Acceptance Criteria

- **AC-1 (gate truth table):** With `CalendarWriteEnabled=true` and `EnableOrganizerReschedule=true`, an eligible organizer-owned reschedule intent produces exactly one Graph `PATCH /users/{p}/events/{id}`; each of the other three flag combinations produces zero Graph write requests and zero write-path token acquisitions. Proven by worker unit tests over the four-row truth table.
- **AC-2 (flag-off dry-run parity):** Defaults (both flags false) produce no Graph request, no `series_moves` row, and no sent-action row; the intended reschedule is logged and audited as `reschedule_disabled` with `OriginalStartUtc/OriginalEndUtc/NewStartUtc/NewEndUtc` populated. Existing send-path behavior and its audit `ActingFlags` string are byte-identical to pre-F18.
- **AC-3 (wire contract):** Mocked-Graph contract tests assert the PATCH method, principal-mailbox route, bearer + `client-request-id` headers, and a body containing exactly `start` and `end` `dateTimeTimeZone` pairs (UTC, seconds precision) and no other properties; a 200 response maps to `ApiEnvelope<EventDto>` with the updated times.
- **AC-4 (fail-closed errors):** 400/401/403/404 map per the D5 matrix with Graph `error.code` passthrough; 429 and 502/503/504 retry with `Retry-After` precedence then map to `THROTTLED`/`TRANSPORT_FAILURE`; any failure surfaces as a thrown `InvalidOperationException` from the scheduling service, a `reschedule_failed` audit record, and no move-history or dedupe bookkeeping.
- **AC-5 (move-guard composition):** A `ONE_ON_ONE` intent whose history answers violate the rolling-six/previous-week rule is blocked (`reschedule_blocked` audit, no write) even with both flags on; a successful write records the pre-move occurrence start via `ISeriesMoveHistory.RecordMoveAsync` so subsequent guard consults see it.
- **AC-6 (audit):** A performed reschedule emits exactly one `ActionAuditRecord` with `ActionType == "organizer-reschedule"`, `ResultCode == "rescheduled"`, the four time columns populated, the correlation id equal to the adapter `client-request-id`, and a reschedule ActingFlags snapshot naming both gate flags.
- **AC-7 (idempotency):** A second evaluation of the same message after a successful write skips with `dedupe_skipped` and issues no Graph request.
- **AC-8 (local backend fail-closed):** On the Stage-0 local adapter, `UpdateEventTimesAsync` returns a non-retryable `NOT_SUPPORTED` failure envelope without performing any HTTP I/O.
- **AC-9 (quality gates):** Line >= 85% / branch >= 75% maintained; >= 1 property test per new pure function; architecture-boundary tests pass (no domain→adapter reference introduced).
- **AC-10 (human exception):** Live-tenant verification is recorded in orchestrator state as a `human_interaction` requirement with `response: exception` and a `runbook_path` pointing at `docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md`, and the runbook file exists (F11 HI-1 precedent).

---

## Automation Feasibility

| # | Step | Automatable? | Classification | Recommended `human_interaction` response |
|---|---|---|---|---|
| 1 | All production code changes (adapter member, service member, worker partial, constants) | Yes | Code authoring | — |
| 2 | Gate truth-table, move-guard, audit, dedupe, and fail-closed unit/property tests | Yes | Deterministic unit tests (Moq + FakeTimeProvider; SQLite in-memory per existing repo patterns) | — |
| 3 | Graph `PATCH /events/{id}` request/response/error contract verification | Yes — via the established `FakeHttpHandler` mocked-Graph pattern (`tests/OpenClaw.Core.Tests/CloudGraph/`) | Mocked contract tests | — |
| 4 | Coverage, mutation (T1), formatting, lint, architecture-boundary gates | Yes | Standard toolchain loop | — |
| 5 | Granting `Calendars.ReadWrite` application permission + admin consent in Azure AD, scoping it via the F11 RBAC management scope | **No** — no Azure/Exchange credentials exist in this environment or CI; tenant-admin action | Live-tenant privileged administration | **exception** + runbook (combined into #6's runbook) |
| 6 | Live verification that the PATCH actually moves a real organizer-owned event (create test event → enable `OpenClaw__AgentPolicy__CalendarWriteEnabled` and `...EnableOrganizerReschedule` in a real deployment → observe the move, the audit row, and the `series_moves` row → disable flags) | **No** — requires a live tenant, a real mailbox, and a human flipping production-affecting flags | Live-tenant end-to-end verification | **exception**, `runbook_path: docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md` (to be authored during implementation; F11 HI-1 precedent for record shape) |
| 7 | Production flag rollout decision (when to turn the flag on for real) | **No** — an operator/business decision by design (the flag exists precisely to defer this to a human) | Operational rollout | Covered by the same runbook; no separate requirement |

Everything except live-tenant permission grant, live write verification, and the rollout decision is automatable with the mocked Graph seam. Expected orchestrator-state outcome: one `human_interaction` requirement (HI-1 for this feature) with `response: "exception"` and the runbook path above, satisfying the `.claude/rules/orchestrator-state.md` invariant that an `exception` carries a non-empty `runbook_path`.

---

## Testing Implications (strategy, no test code)

- **Unit (worker):** MSTest + FluentAssertions + Moq, mirroring `SchedulingWorkerAuditTests`/`SchedulingWorkerDedupeTests` construction. All time via `FakeTimeProvider`; no `Task.Delay`/sleeps (banned).
- **Property (T1 obligation):** intent computation (e.g., duration preservation: for all valid original intervals and slots, `newEnd - newStart == originalEnd - originalStart`; eligibility monotonicity: removing the event or organizer bit never yields an intent) and the flags-snapshot builder (round-trip parse).
- **Contract (host-service boundary, required per `general-unit-test.md`):** the mocked-Graph tests of Section 3 are the contract suite; `SchedulingDtoContractTests`-style assertions are not needed because no DTO shape changes.
- **Architecture:** existing `AgentArchitectureBoundaryTests`/`CloudGraphArchitectureBoundaryTests` should pass unmodified; the new partial stays inside `CloudGraph`.
- **Integration:** deliberately none in CI (live tenant is the recorded human exception); the local-backend `NOT_SUPPORTED` test doubles as the adapter-smoke negative.
- **Mutation:** `OpenClaw.Core` is T1 — Stryker in the pre-merge/nightly pipeline; the truth-table and fail-closed branches are the mutation-sensitive surface, so assert both the action taken and the actions *not* taken (no-write assertions kill "negate condition" mutants).
