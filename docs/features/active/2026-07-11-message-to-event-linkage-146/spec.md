# message-to-event-linkage — Spec

- **Issue:** #146
- **Parent (optional):** openclaw-runtime-remediation (child A)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-11T23-15
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature

## Overview

The OpenClaw scheduling pipeline does not resolve the calendar event that a message is
linked to. `OpenClaw.Core.Agent.Runtime.HostAdapterSchedulingService.GetEventForMessageAsync`
is a stand-in: it forwards the supplied `messageId` to `GetEventAsync` (a plain event lookup
by id), which cannot succeed because a message bridge id (`msg:`/`mtg:` prefix) is never a
valid event bridge id (`evt:` prefix). The inline comment names the real linkage as "deferred
bridge work (#71-#76)". As a result the direct lookup always misses and the pipeline depends
entirely on the heuristic calendar-view fallback (`ChooseRelatedEventFromWindowAsync`), which
is lossy and only runs when `CalendarViewFallbackDays > 0`.

No MailBridge RPC and no HostAdapter route answers the question "which calendar event is this
message linked to?" This feature introduces that wire contract end to end and rewires Core to
use it, while preserving the graceful-degradation contract the deterministic pipeline relies
on: a genuinely unlinked message must resolve to a clean `null`, not an error.

## Behavior

Introduce a real message-to-event linkage path across the bridge stack. Linkage joins a
meeting message's Clean Global Object ID (the appointment `GlobalAppointmentID`) to the
already-stored `events.global_appointment_id`.

1. A new MailBridge RPC (`BridgeMethods.GetEventForMessage`) that, given a message bridge id,
   loads the message row, reads its stored linked appointment key, and resolves the matching
   event from the bridge SQLite cache. It returns the linked event payload or a clean
   not-linked result.
2. The MailBridge wire contract is extended: the RPC reuses the existing flat `{ "id": ... }`
   request param shape of `get_message`, and its response payload is the existing `EventDto`
   (or JSON null). `MessageDto` gains a nullable `LinkedGlobalAppointmentId` field (appended
   positional-last with a `null` default) so the linkage key is carried and persisted.
3. A new HostAdapter route (`GET /users/{id}/messages/{messageId}/event`) and a new
   `IHostAdapterClient.GetEventForMessageAsync` method that a Core consumer calls to resolve
   the linked event. The route uses a null-tolerant `EventDto` projector so an ok/null RPC
   result becomes an `ok:true` / `data:null` / HTTP 200 envelope rather than a 502.
4. `HostAdapterSchedulingService.GetEventForMessageAsync` is rewired to call the new client
   method instead of the messageId-as-eventId stand-in, applying its existing
   `{ Ok:true, Data:not null }` guard to return the mapped event or `null`.

The graceful-degradation contract is preserved end to end: an unlinked message (ordinary
mail, no matching event row, or absent message row) yields a success envelope carrying no
data, which Core maps to `null`, so `SchedulingWorker` degrades to the calendar-view fallback
exactly as it does today. Error envelopes are reserved for genuine faults: a malformed message
bridge id yields `INVALID_REQUEST` / HTTP 400; bridge-not-ready and transport faults use the
existing gates unchanged.

## Inputs / Outputs

- **Inputs:**
  - RPC request: flat JSON `{ "id": "<messageBridgeId>" }` (reuses the `get_message` shape).
  - HostAdapter route: `GET /users/{id}/messages/{messageId}/event`; `messageId` is a message
    bridge id (`msg:`/`mtg:` prefix, Base64 entry id).
  - Client method: `GetEventForMessageAsync(string bridgeId, string? requestId = null,
    CancellationToken cancellationToken = default)`.
  - Scan-time input: for meeting items, the associated appointment's `GlobalAppointmentID`
    read fail-soft through the `IMessageSource` seam.
- **Outputs:**
  - RPC response: `RpcResponse.Success(id, EventDto)` when linked; `RpcResponse.Success(id,
    null)` when unlinked; `RpcResponse.Failure(id, INVALID_REQUEST, ...)` on malformed id.
  - HostAdapter envelope: `ApiEnvelope<EventDto>` with `Ok:true` and either `Data:event`
    (HTTP 200) or `Data:null` (HTTP 200); `INVALID_REQUEST` -> HTTP 400; bridge-not-ready ->
    HTTP 409.
  - Core: a mapped `SchedulingEventDto` on a linked hit, or `null` on unlinked.
- **Config keys and defaults:** No new config keys. The existing `CalendarViewFallbackDays`
  gate that governs the fallback path is unchanged.
- **Versioning / backward-compatibility constraints:** The appended `MessageDto` field is
  positional-last with a `null` default and the new RPC method is additive, so both changes
  are non-breaking, consistent with the #72/#73 additions. No contract major bump is required.

## API / CLI Surface

- **MailBridge RPC:** `BridgeMethods.GetEventForMessage` const added plus an `All` allow-list
  entry so `PipeRpcWorker.BuildResponseAsync` accepts it. Dispatched by a new `Handle` switch
  arm to `HandleGetEventForMessageAsync`, which decodes the id via
  `BridgeIdCodec.TryDecodeMessageId`, returns `INVALID_REQUEST` on a malformed id, calls the
  repository, and returns `Success(id, event-or-null)`.
- **MailBridge.Client verb:** `get-event-for-message` mapped in `Build` to
  `Req(id, BridgeMethods.GetEventForMessage, opts, "id")`, mirroring `get-event`.
- **HostAdapter route:** `GET /users/{id}/messages/{messageId}/event` following the existing
  `GET /users/{id}/messages/{messageId}` pattern (request id, `RequireReadyBridgeAsync<EventDto>`
  gate, `TryGetBridgeId` validation, `HostAdapterCommandBuilder.BuildGetEventForMessage`,
  `processRunner.ExecuteAsync<EventDto>(cmd, requestId, bridge, nullTolerantProjector, ct)`,
  then `ToHttpResult`).
- **Client method:** `IHostAdapterClient.GetEventForMessageAsync` with keyword-style optional
  params matching `GetEventAsync`, implemented by both `HostAdapterHttpClient` and
  `GraphHostAdapterClient`.
- **Contracts and validation rules:** Message bridge id must decode via
  `BridgeIdCodec.TryDecodeMessageId`; a decode failure is `INVALID_REQUEST` / 400. A decoded
  id that has no linked event is not an error — it is `Success(null)` / 200 / `data:null`.

## Data & State

- **Data transformations and invariants:** Linkage key = the meeting message's Clean Global
  Object ID, equal to the associated appointment's `GlobalAppointmentID`. Resolution is an
  exact-string equality join against `events.global_appointment_id`. For a recurring series,
  the newest instance is selected (`ORDER BY start_utc DESC LIMIT 1`), matching
  `ListCalendarWindow` ordering.
- **Caching / persistence details:** A `linked_global_appointment_id TEXT NULL` column is
  added to the `messages` table and written on INSERT/UPSERT and read back. The RPC resolves
  entirely from the bridge SQLite cache; no COM/Outlook session runs in the RPC path.
- **Migration / backfill requirements:** The column is added via the guarded-ALTER migration
  idiom (`MessageFieldColumns` array + `MigrateMessagesSchemaAsync`) so the migration is
  idempotent. Existing rows carry `NULL` (treated as unlinked) until the next scan repopulates
  them; no explicit backfill is performed. Linkage is only as fresh as the last inbox+calendar
  scan.
- **Scan-time population:** `OutlookScanner.NormalizeMessage` populates the field for meeting
  items via a new `IMessageSource` member read fail-soft in `ComMessageSource`
  (`GetAssociatedAppointment` then `GlobalAppointmentID`, wrapper released in `finally`).
  Ordinary mail yields `null`. The sensitive-message path
  (`OutlookScanner.Redaction.cs`) is reviewed; the linked key is a mechanical identifier, but
  if the never-ingest ordering (issue #18) is uncertain the safer default sets it to `null`.

## Constraints & Risks

- Spans five C# projects — `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Contracts`,
  `OpenClaw.HostAdapter`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.Core` — and adds a new
  cross-module wire contract.
- Must follow the existing envelope/route patterns (`ApiEnvelope`, `HostAdapterResponses`,
  `IHostAdapterProcessRunner`, `HostAdapterCommandBuilder`) rather than inventing a new
  transport shape.
- `OpenClaw.Core` and `OpenClaw.HostAdapter` are T1 (critical); `OpenClaw.MailBridge`,
  `OpenClaw.MailBridge.Contracts`, and `OpenClaw.HostAdapter.Contracts` are T2. The
  cross-module contract change forces a higher complexity band.
- The unlinked-message `null` contract must not regress; the existing deterministic pipeline
  relies on it. The specific hazard: if the RPC returned `NOT_FOUND` for an unlinked message,
  `HostAdapterResponseMapper.MapFailure` would emit HTTP 404, breaking degradation. The RPC
  must return `Success(null)` (mirroring `send_mail`), and the route must use the
  null-tolerant projector to avoid the default `DeserializePayload<EventDto>` throwing on a
  JSON null element (which would surface as a 502 TRANSPORT_FAILURE).
- Both cloud client implementations (`HostAdapterHttpClient` and `GraphHostAdapterClient`) are
  bound by `CloudGraphContractParityTests`; the new method must be represented in both and
  behave consistently with the null contract (no `NOT_SUPPORTED` error for this read path).
- Test framework is MSTest + Moq + FluentAssertions (authoritative per research); xUnit and
  NSubstitute must not be referenced.
- File-size cap (500 lines) constrains where new code lands: `OutlookScanner.cs` is at its cap
  and `PipeRpcWorker.cs` (~438) and `CacheRepository.cs` (~480) are near it — add to partials
  or `.Readers.cs`, not to the capped files.

## Implementation Strategy

- **Implementation scope:** Add the RPC method const + dispatch + handler; add the repository
  resolution method, the linkage column, the guarded-ALTER migration, and INSERT/UPSERT/read
  wiring; surface the linked key through `IMessageSource`/`ComMessageSource`/`OutlookScanner`;
  add the CLI verb; add the HostAdapter route, command builder, and null-tolerant projector;
  add the `IHostAdapterClient` method and both implementations; rewire Core's
  `GetEventForMessageAsync`.
- **New classes/functions/commands to add or update:**
  `BridgeMethods.GetEventForMessage`, `MessageDto.LinkedGlobalAppointmentId`,
  `HandleGetEventForMessageAsync`, `IBridgeRepository.GetEventForMessageAsync` + impl,
  `MigrateMessagesSchemaAsync` column entry, `IMessageSource` linked-key member,
  `get-event-for-message` CLI verb, `HostAdapterCommandBuilder.BuildGetEventForMessage`,
  a null-tolerant `EventDto` projector helper, `IHostAdapterClient.GetEventForMessageAsync`
  (both impls), and the `HostAdapterSchedulingService.GetEventForMessageAsync` rewire.
- **Dependency changes:** None. All work uses libraries already present.
- **Logging/telemetry additions:** No new telemetry required; the null-success path is used
  precisely so an unlinked message does not pollute error telemetry. Existing route/RPC
  logging patterns are reused.
- **Rollout plan:** No feature flag. The change is additive and backward-compatible; the
  fallback path (`ChooseRelatedEventFromWindowAsync`) remains the safety net for any message
  that resolves to `null`.

## Acceptance Criteria

- [ ] `BridgeMethods.GetEventForMessage` is added as a `public const string` and included in
      the `BridgeMethods.All` allow-list, so `PipeRpcWorker.BuildResponseAsync` accepts the
      method and rejects it only when absent from `All`.
- [ ] `MessageDto` carries a new nullable `LinkedGlobalAppointmentId` field appended
      positional-last with a `null` default; the addition is non-breaking and covered by the
      contract coverage test.
- [ ] The `messages` table gains a `linked_global_appointment_id TEXT NULL` column via the
      guarded-ALTER migration idiom, and the migration is idempotent (re-running it does not
      error or duplicate the column).
- [ ] `IBridgeRepository.GetEventForMessageAsync` decodes the message bridge id, loads the
      message row, and resolves the matching event by exact-string join on
      `events.global_appointment_id`, selecting the newest instance
      (`ORDER BY start_utc DESC LIMIT 1`) for a recurring series.
- [ ] The RPC handler returns `RpcResponse.Success(id, EventDto)` for a linked message and
      `RpcResponse.Success(id, null)` for an unlinked message (ordinary mail, no matching
      event, or absent message row) — never `Failure(NOT_FOUND)` for those cases.
- [ ] The RPC handler returns `RpcResponse.Failure(id, INVALID_REQUEST, ...)` for a malformed
      message bridge id (decode failure).
- [ ] The MailBridge.Client exposes a `get-event-for-message` verb that forwards the required
      `id` option to `BridgeMethods.GetEventForMessage`.
- [ ] A HostAdapter route `GET /users/{id}/messages/{messageId}/event` is registered following
      the existing message route pattern, gated by `RequireReadyBridgeAsync<EventDto>` and
      `TryGetBridgeId` validation.
- [ ] `HostAdapterCommandBuilder.BuildGetEventForMessage(bridgeId)` builds the CLI command for
      the new verb, mirroring `BuildGetEvent`/`BuildGetMessage`.
- [ ] The route uses a null-tolerant `EventDto` projector so an `ok`/JSON-null RPC result
      produces an `ok:true` / `data:null` / HTTP 200 envelope (not a 502 TRANSPORT_FAILURE),
      and an `ok`/event result produces `ok:true` / `data:event` / HTTP 200.
- [ ] `IHostAdapterClient.GetEventForMessageAsync(string bridgeId, string? requestId = null,
      CancellationToken cancellationToken = default)` is declared, with keyword-style optional
      params matching `GetEventAsync`.
- [ ] Both `HostAdapterHttpClient` and `GraphHostAdapterClient` implement
      `GetEventForMessageAsync`, satisfy `CloudGraphContractParityTests`, and behave
      consistently with the null contract (no `NOT_SUPPORTED` error for this read path).
- [ ] `HostAdapterSchedulingService.GetEventForMessageAsync` invokes
      `hostAdapterClient.GetEventForMessageAsync` (verified to be the method called, not
      `GetEventAsync`) and applies the `{ Ok:true, Data:not null }` guard to return the mapped
      event on a linked hit.
- [ ] An unlinked message resolves to a clean `null` in Core (via `ok:true`/`data:null`) with
      no HTTP 400 and no HTTP 404, and `SchedulingWorker` degrades to the calendar-view
      fallback exactly as it does today; a linked hit skips the window fallback.
- [ ] A malformed message bridge id surfaces as HTTP 400 (`INVALID_REQUEST`), distinct from
      the null-degradation path; bridge-not-ready remains HTTP 409 via the existing gate.
- [ ] Line coverage on changed C# code is >= 85% and branch coverage >= 75%, with no
      regression on changed lines, and no production file is excluded from coverage.
- [ ] Every changed or added source and test file remains under the 500-line cap (new logic
      placed in partials / `.Readers.cs` rather than the capped `OutlookScanner.cs`,
      `PipeRpcWorker.cs`, or `CacheRepository.cs`).
- [ ] Tests are authored with MSTest + Moq + FluentAssertions, use an injected `TimeProvider`
      (`FakeTimeProvider`) with no wall-clock reads or sleeps, and use in-memory SQLite / seam
      fakes with no temporary files.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/contract/integration as applicable)
- [ ] Edge cases and error handling covered by tests (linked hit, unlinked null, absent-row
      null, recurring newest-instance, malformed-id 400)
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging reviewed (no new error telemetry for the unlinked path)
- [ ] Seven-stage toolchain pass completed (format -> lint -> type-check -> arch -> unit ->
      contract -> integration)

## Seeded Test Conditions

- [ ] Unit: RPC handler returns event payload for a linked message.
- [ ] Unit: RPC handler returns clean not-linked (`Success(null)`) result for an unlinked
      message and for an absent message row.
- [ ] Unit: RPC handler returns `INVALID_REQUEST` for a malformed message bridge id.
- [ ] Unit: repository selects the newest instance for a recurring series.
- [ ] Unit: HostAdapter route maps success-event, success-null, malformed-id (400), and
      not-ready (409) to the correct envelope shapes; the null-tolerant projector is exercised.
- [ ] Unit: `GetEventForMessageAsync` returns a mapped event on success and `null` on
      not-linked without throwing, calling the new client method.
- [ ] Unit: `SchedulingWorker` skips the window fallback on a linked hit and uses it on null.
- [ ] Contract: the `MessageDto` field addition and both client-impl surfaces satisfy the
      contract-coverage and `CloudGraphContractParityTests` gates.
