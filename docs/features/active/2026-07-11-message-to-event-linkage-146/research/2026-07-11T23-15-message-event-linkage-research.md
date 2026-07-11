# Research: Real Message-to-Event Linkage in the Scheduling Pipeline (Issue #146)

Timestamp: 2026-07-11T23-15
Feature: 2026-07-11-message-to-event-linkage-146
Scope: OpenClaw.MailBridge, OpenClaw.MailBridge.Contracts, OpenClaw.HostAdapter, OpenClaw.HostAdapter.Contracts, OpenClaw.Core

## 1. Current State Analysis

### 1.1 The stand-in being replaced

`src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` lines 33-45.
`GetEventForMessageAsync(messageId, ct)` currently forwards the `messageId` straight to
`GetEventAsync(messageId, ct)` (line 44), which decodes the value as an *event* bridge id and
performs a plain event-by-id lookup. Because a message bridge id (`msg:`/`mtg:` prefix, see
below) is never a valid event bridge id (`evt:` prefix), this call effectively always misses and
returns `null`. The inline comment names this "deferred bridge work (#71-#76)". The downstream
consumer `SchedulingWorker.ProcessMessageAsync` (`src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`
lines 25-39) already treats a `null` result as the trigger for the calendar-view fallback
(`ChooseRelatedEventFromWindowAsync`), so today the pipeline *always* falls back to window
matching and never uses a direct link.

### 1.2 End-to-end call path (verified)

The read path is a five-hop chain; the new RPC must be threaded through all of it:

1. `ISchedulingService` (`src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`) — the D6 seam.
2. `HostAdapterSchedulingService` -> `IHostAdapterClient`
   (`src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`). Two implementations exist and both
   must satisfy the interface:
   - `HostAdapterHttpClient` (`src/OpenClaw.Core/HostAdapterHttpClient.cs`) — local Stage-0 backend, real HTTP GET.
   - `GraphHostAdapterClient` (`src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.*.cs`) — Graph backend.
   A contract-parity test (`tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphContractParityTests.cs`)
   enforces that both implement the same surface, so a new interface method obligates both.
3. HostAdapter HTTP route (`src/OpenClaw.HostAdapter/Program.cs`, `SchedulingRoutes.cs`) ->
   `HostAdapterCommandBuilder` -> `IHostAdapterProcessRunner` (`HostAdapterProcessRunner`).
4. The runner spawns the CLI `OpenClaw.MailBridge.Client` (`src/OpenClaw.MailBridge.Client/Program.cs`),
   which opens the named pipe.
5. `PipeRpcWorker` (`src/OpenClaw.MailBridge/PipeRpcWorker.cs`) dispatches to `IBridgeRepository`
   (`CacheRepository`, SQLite). "Host data source" here = the bridge SQLite cache populated by the
   `OutlookScanner`; the RPC worker itself has no Outlook/COM session.

### 1.3 What linkage data exists today (key finding)

There is **no persisted join key between a message and an event** in the current cache.

- `MessageDto` (`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` lines 76-98) carries
  `ConversationId` and `MeetingMessageType` (raw `OlMeetingType`), plus `ItemKind` (`mail`/`meeting`).
  It does **not** carry any global-object-id / appointment key. Confirmed against the `messages`
  table DDL (`src/OpenClaw.MailBridge/CacheRepository.Schema.cs` line 14) and the message reader
  (`CacheRepository.Readers.cs` lines 14-37).
- `EventDto` (BridgeContracts.cs lines 100-128) carries `GlobalAppointmentId` and `ICalUId`
  (`ICalUId` is populated from the same `GlobalAppointmentID`, see `OutlookScanner.GraphFields.cs`
  line 83). The `events` table has a `global_appointment_id` column (Schema.cs line 15), written by
  `UpsertEventAsync` (CacheRepository.cs lines 220-282).
- The message bridge id is `msg:B64(entryId)` or `mtg:B64(entryId)`; the event bridge id is
  `evt:B64(globalAppointmentId||entryId):startUtc` (`BridgeIdCodec`,
  `src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs` lines 6-69). These id spaces do not share a
  value, which is exactly why the stand-in cannot resolve.

Conclusion: real linkage requires surfacing a join key on the message. The Outlook mechanism that
ties a meeting-request message (`MeetingItem`) to its calendar `AppointmentItem` is the
Clean Global Object ID: the appointment's `GlobalAppointmentID` (already stored on events) equals
the clean global object id of the associated meeting message. The message side of that key is not
scanned today.

### 1.4 RPC registration/dispatch pattern (verified)

- Method constants and the allow-list live in `BridgeMethods`
  (BridgeContracts.cs lines 20-40): a `public const string` plus an entry in the `All` HashSet.
  `PipeRpcWorker.BuildResponseAsync` rejects any method not in `All` with `INVALID_REQUEST`
  (PipeRpcWorker.cs lines 190-197).
- Dispatch is a `switch` in `PipeRpcWorker.Handle` (lines 202-233). Each handler reads params via
  `RequireParameter`/`RequireIso8601`/`RequireLimit`, calls the repo, and returns
  `RpcResponse.Success(id, payload)` or `RpcResponse.Failure(id, code, message)`.
- The existing `HandleGetMessageAsync`/`HandleGetEventAsync` (lines 337-380) validate the bridge id
  with `BridgeIdCodec.TryDecode*`, call `repo.Get*Async`, and return `NOT_FOUND` when the row is
  absent. The `send_mail` handler returns `RpcResponse.Success(req.Id, null)` on success (line 253) —
  this is the precedent for a **success envelope carrying no data**.
- The CLI (`OpenClaw.MailBridge.Client/Program.cs`) maps a verb to a method in `Build` (lines 127-154)
  and forwards options. A new verb (e.g. `get-event-for-message`) plus a `Req(...)` arm with a
  required `id` param is required, mirroring `get-event`.

### 1.5 HostAdapter routing + envelope pattern (verified)

- Read routes are mapped in `Program.cs` (`/users/{id}/messages/{messageId}` lines 157-206;
  `/users/{id}/events/{eventId}` lines 302-351) and `SchedulingRoutes.cs`. Each route: get request id,
  `RequireReadyBridgeAsync<T>` gate, validate params (`HostAdapterRequestValidation`), build a
  `ProcessStartInfo` via `HostAdapterCommandBuilder`, call
  `processRunner.ExecuteAsync<T>(cmd, requestId, bridge, projector, ct)`, then `ToHttpResult`.
- `HostAdapterCommandBuilder` (`HostAdapterCommandBuilder.cs`) exposes `BuildGetEvent`/`BuildGetMessage`
  (`CreateBaseStartInfo(verb)` + `AddOption("id", bridgeId)`). A new `BuildGetEventForMessage(bridgeId)`
  follows the same shape.
- Envelope helpers are in `HostAdapterResponses.cs`:
  - `Success<T>(data, requestId, adapterVersion, bridge, cliExitCode)` -> `ApiEnvelope<T>(Ok:true, Data:data, ...)`, HTTP 200.
  - `Failure<T>(...)`, `InvalidRequest<T>(...)` (400), `BridgeNotReady<T>` (409), `ConfigurationError<T>` (503), `AcceptedNoContent` (202).
  - `ApiEnvelope<T>` is `record (bool Ok, T? Data, ApiMeta Meta, ApiError? Error)`
    (`src/OpenClaw.HostAdapter.Contracts/ApiEnvelope.cs`). `Data` is already nullable `T?`, so a
    `Success` with a `null` payload is a structurally valid **ok:true / data:null** envelope.
- The failure mapper `HostAdapterResponseMapper.MapFailure` (`HostAdapterResponseMapper.cs`) maps a
  bridge `NOT_FOUND` error to **HTTP 404** (lines 34-47) and `INVALID_REQUEST` to **400** (lines 71-92).
  This is the critical hazard: if the new RPC returned `NOT_FOUND` for an unlinked message, the route
  would emit a 404 error envelope, not the required success/null.

### 1.6 The null-degradation mechanism (critical, verified)

`HostAdapterSchedulingService.GetEventAsync` (lines 66-74) already implements the exact contract Core
needs: `envelope is { Ok: true, Data: not null } ? mapper.MapEvent(envelope.Data) : null`. So Core maps
an **ok:true / data:null** envelope to `null`, and `SchedulingWorker` degrades to the calendar-view
fallback (Pipeline.cs line 31). The mechanism to preserve, end to end:

1. Bridge RPC returns `RpcResponse.Success(req.Id, null)` when the message has no linked event
   (mirror of `send_mail`), **not** `Failure(NOT_FOUND)`.
2. `HostAdapterProcessRunner.ExecuteAsync` sees `response.Ok == true`, calls `ConvertPayload(null, projector)`;
   `ConvertPayload` serializes a JSON null element (HostAdapterProcessRunner.cs lines 149-159). The
   default `DeserializePayload<EventDto>` (lines 143-147) **throws `JsonException` on a null element**,
   which the runner turns into a 502 TRANSPORT_FAILURE. Therefore a **null-tolerant projector** is
   required for this route, e.g. `element.ValueKind == JsonValueKind.Null ? null : DeserializePayload<EventDto>(element)`.
   With that projector, `HostAdapterResponses.Success((EventDto?)null, ...)` yields ok:true / data:null / 200.
3. `HostAdapterHttpClient` deserializes the 200 body into `ApiEnvelope<EventDto>` with `Data == null`.
4. `HostAdapterSchedulingService.GetEventForMessageAsync` applies the same
   `{ Ok:true, Data:not null }` guard and returns `null` when unlinked.

Note: even a non-ok envelope from the client degrades to `null` in Core, but the feature contract in
issue #146 explicitly requires the "no linked event" case to be a *success* envelope (so it is
distinguishable from a real failure and does not pollute error telemetry). Reserve error envelopes for
genuine faults: malformed bridge id -> `INVALID_REQUEST`/400; bridge unavailable -> existing gates.

## 2. Candidate Approaches (linkage resolution)

### 2.1 Recommended: cache-join on a scanned linked-event key

Add a message linkage column populated at inbox-scan time; resolve the RPC by joining to the
already-stored `events.global_appointment_id`.

- Scan: in `OutlookScanner.NormalizeMessage` (OutlookScanner.cs lines 353-417), for meeting items
  (`isMeeting == true`, `IsMeetingItem` in `OutlookScanner.Redaction.cs` line 163), resolve the
  associated appointment's `GlobalAppointmentID` and store it on the message. Route the COM read
  through the existing model-agnostic seam `IMessageSource` (`src/OpenClaw.MailBridge/IMessageSource.cs`)
  by adding one member (e.g. `string? LinkedGlobalAppointmentId`), implemented in `ComMessageSource`
  (`src/OpenClaw.MailBridge/ComMessageSource.cs`) fail-soft via
  `InvokeMember(item, "GetAssociatedAppointment", false)` then `GetOptionalString(appt, "GlobalAppointmentID")`,
  releasing the wrapper in `finally` (the file already uses `InvokeMember` with args for
  `PropertyAccessor.GetProperty` and no-arg for `GetExchangeUser`, so the idiom is established). Ordinary
  mail yields `null`.
- Contract: add a nullable field to `MessageDto` (default `null`, appended as the last positional
  parameter to preserve the existing record's compatibility, consistent with how #73 fields were added).
- Persistence: add a `linked_global_appointment_id TEXT NULL` column via the guarded-ALTER migration
  idiom in `CacheRepository.Schema.cs` (`MessageFieldColumns` array + `MigrateMessagesSchemaAsync`),
  plus INSERT/UPSERT/read wiring in `CacheRepository.cs` and `CacheRepository.Readers.cs`.
- Resolution: a new `IBridgeRepository.GetEventForMessageAsync(string messageBridgeId)` that decodes the
  message id, loads the message row, reads its linked key, and runs
  `SELECT * FROM events WHERE global_appointment_id = $key ORDER BY start_utc DESC LIMIT 1`
  (most-recent instance for recurring series); returns `EventDto?`.
- Advantages: keeps resolution in the "host data source" (cache), reuses the migration/COM-confinement
  patterns, no COM in the RPC path, deterministic and unit-testable via the in-memory SQLite repo.
- Limitations: touches the scanner and schema (a bounded, well-precedented change); the linkage is only
  as fresh as the last inbox+calendar scan; recurring-series master vs occurrence resolution needs a
  decision (recommend newest instance in-window, matching `ListCalendarWindow` ordering).
- Sensitivity interaction: `NormalizeSensitiveMessage` (OutlookScanner.Redaction.cs) must be considered.
  The linked key is a mechanical identifier, not protected content; recommend it may be retained for
  sensitive messages, but the planner should confirm against the issue-#18 never-ingest ordering and
  set it to `null` in the sensitive path if in doubt (safer default).

### 2.2 Rejected alternatives (brief)

- **Live COM at RPC time** (re-open the item by `EntryID` in `PipeRpcWorker` and call
  `GetAssociatedAppointment`): rejected. The RPC worker holds no Outlook session; only `OutlookScanner`
  attaches to Outlook. Adding COM to the RPC path breaks the scanner/reader separation and the
  cache-read architecture, and is not unit-testable without live Outlook.
- **`ConversationId` join**: rejected. Events do not store a conversation id, and conversation id is not
  a reliable 1:1 meeting-to-appointment key.
- **Core-side compute** (resolve linkage inside `OpenClaw.Core` from cached data): rejected per the task
  constraint; the prior scheduling features (#74/#75/#76) use a Graph-shaped HostAdapter route that calls
  the CLI process runner, and this feature must follow that pattern rather than a Core-cache compute.

## 3. Behavior Semantics

- Success (linked): message has a stored linked key that matches an event row ->
  RPC `Success(id, EventDto)` -> route 200 ok:true/data:event -> Core returns mapped `SchedulingEventDto`.
- Success (unlinked / ordinary mail / no matching event / message row absent): RPC `Success(id, null)`
  -> route 200 ok:true/data:null -> Core returns `null` -> `SchedulingWorker` runs the calendar-view
  fallback exactly as today. Treat "message row absent" as unlinked (success/null) for graceful
  degradation, not as a hard 404.
- Failure (malformed message bridge id): RPC `Failure(INVALID_REQUEST)` -> route 400. This is a caller
  error, distinct from graceful degradation.
- Failure (bridge not ready / transport): existing `RequireReadyBridgeAsync` (409) and process-runner
  502 paths are unchanged.
- Ordering/edge cases: recurring series -> pick newest instance (`ORDER BY start_utc DESC LIMIT 1`);
  cancelled meeting (`MeetingMessageType == 1`) still resolves to the underlying appointment if present;
  case sensitivity of the key must match how it is stored (exact string equality on the hex
  `GlobalAppointmentID` as written by the scanner).

## 4. Requirements Mapping (files to create/modify)

### OpenClaw.MailBridge.Contracts (T2)
- `Models/BridgeContracts.cs`: add `BridgeMethods.GetEventForMessage` const + `All` entry; append a
  nullable `LinkedGlobalAppointmentId` (or similarly named) field to `MessageDto`.
- No new request/response wire record is strictly required: the request reuses the flat
  `{ "id": <messageBridgeId> }` param shape of `get_message`, and the response payload is the existing
  `EventDto` (or JSON null). If the planner prefers an explicit contract, a thin
  request/response record can be added here mirroring the existing DTO style, but the minimal design
  reuses `EventDto` + null.

### OpenClaw.MailBridge (T2)
- `PipeRpcWorker.cs`: add `BridgeMethods.GetEventForMessage => await HandleGetEventForMessageAsync(req)`
  to the `Handle` switch; add the handler (decode message id via `BridgeIdCodec.TryDecodeMessageId`,
  400 on malformed; call the repo; `Success(id, evt-or-null)`).
- `CacheRepository.cs` (+`.Readers.cs`, `.Schema.cs`): add `GetEventForMessageAsync` to
  `IBridgeRepository` and the implementation; add the message linkage column + migration; wire
  INSERT/UPSERT/read of the new column.
- `OutlookScanner.cs` (`NormalizeMessage`), `IMessageSource.cs`, `ComMessageSource.cs`: surface and read
  the linked-appointment key fail-soft for meeting items.
- Consider `OutlookScanner.Redaction.cs` (`NormalizeSensitiveMessage`) for the sensitive-message default.

### OpenClaw.MailBridge.Client (T3)
- `Program.cs`: add a `get-event-for-message` verb in `Build` -> `Req(id, BridgeMethods.GetEventForMessage, opts, "id")`.

### OpenClaw.HostAdapter (T1)
- Route registration: add `GET /users/{id}/messages/{messageId}/event` (recommended Graph-shaped path;
  the Graph `eventMessage` has an `event` navigation property) in `Program.cs` or a scheduling-routes
  partial, following the `GET /users/{id}/messages/{messageId}` pattern with `RequireReadyBridgeAsync`
  and `TryGetBridgeId`.
- `HostAdapterCommandBuilder.cs`: add `BuildGetEventForMessage(bridgeId)`.
- Provide a **null-tolerant `EventDto` projector** for `ExecuteAsync` (new static helper), so an ok/null
  RPC result becomes an ok:true/data:null 200 envelope rather than a 502.

### OpenClaw.HostAdapter.Contracts (T2)
- `IHostAdapterClient.cs`: add `Task<ApiEnvelope<EventDto>> GetEventForMessageAsync(string bridgeId, string? requestId = null, CancellationToken cancellationToken = default)`
  (keyword-style optional params, matching `GetEventAsync`).

### OpenClaw.Core (T1)
- `HostAdapterHttpClient.cs`: implement `GetEventForMessageAsync` as a real
  `SendAsync<EventDto>($"users/{id}/messages/{Uri.EscapeDataString(bridgeId)}/event", ...)`.
- `CloudGraph/GraphHostAdapterClient.Messages.cs` (or a new partial): implement the same method.
  Options: a real Graph lookup (`GET /users/{p}/messages/{id}/?$expand=microsoft.graph.eventMessage/event`
  or the `event` navigation) mapped through `GraphEventMapper`, or — if out of scope for this feature —
  a clean **ok:true/data:null** envelope to preserve degradation. Do **not** use a `NOT_SUPPORTED`
  error for this read path if a null-success is representable; contract parity only requires the method
  to exist and behave consistently with the null contract.
- `Agent/Runtime/HostAdapterSchedulingService.cs` lines 33-45: replace the `GetEventAsync` forward with
  `hostAdapterClient.GetEventForMessageAsync(messageId, cancellationToken: ct)` and the existing
  `{ Ok:true, Data:not null } ? mapper.MapEvent(...) : null` guard.

## 5. Testing Implications

Authoritative frameworks (see section 6): **MSTest + Moq + FluentAssertions** (CsCheck for
property tests, NetArchTest for architecture boundaries, `Microsoft.Extensions.TimeProvider.Testing`
for time). Tests mirror production under `tests/<Project>.Tests/...`.

- OpenClaw.MailBridge.Tests (T2): repository test using in-memory SQLite
  (`CacheRepository("Data Source=...;Mode=Memory;Cache=Shared")`, as existing repo tests do) for
  `GetEventForMessageAsync`: linked hit, ordinary-mail null, no-matching-event null, absent message
  null, recurring newest-instance selection. `PipeRpcWorker` handler test (via `BuildResponseAsync`)
  for success-with-event, success-null, and malformed-id -> INVALID_REQUEST. Scanner/`ComMessageSource`
  fail-soft tests follow `ComMessageSourceTests`/`ComMessageSourceResolutionTests` doubles. Contract
  coverage in `BridgeContractsCoverageTests`.
- OpenClaw.HostAdapter.Tests (T1): route test with a fake `IHostAdapterProcessRunner`
  (`HostAdapterEndpointTests`/`HostAdapterProcessRunnerTests` pattern) asserting: ok RPC-null ->
  200 ok:true/data:null; ok RPC-event -> 200 ok:true/data:event; malformed id -> 400; not-ready ->
  409. Add a projector null-tolerance test.
- OpenClaw.Core.Tests (T1): `HostAdapterSchedulingServiceTests` (Moq `IHostAdapterClient`) asserting
  data:null -> `null`, data:event -> mapped DTO, and that `GetEventForMessageAsync` is the method
  invoked (not `GetEventAsync`). `HostAdapterHttpClientSchedulingTests` for URL construction. A
  `SchedulingWorker` fallback test (`SchedulingWorkerFallbackTests` pattern) confirming a linked hit
  skips the window fallback and a null uses it. Both client impls covered per `CloudGraphContractParityTests`.
- Property tests (T1/T2 pure functions): if any pure transform is introduced (e.g. key-normalization),
  add a CsCheck property test per `.claude/rules/general-unit-test.md`.

## 6. Test Framework Resolution (Authoritative)

Inspected the actual test projects. The repository uses **MSTest**, **Moq**, and **FluentAssertions**.
The generated AGENTS.md is correct; `.claude/rules/csharp.md`'s xUnit + NSubstitute naming does **not**
match the code and must not be followed.

Evidence:
- `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj`: `MSTest.TestAdapter` 3.6.4,
  `MSTest.TestFramework` 3.6.4, `Moq` 4.20.72, `FluentAssertions` 6.12.0, `CsCheck` 4.7.0,
  `NetArchTest.Rules` 1.3.2, `Microsoft.Extensions.TimeProvider.Testing` 10.6.0,
  `Microsoft.AspNetCore.Mvc.Testing` 10.0.5, `coverlet.collector` 6.0.2. No xUnit, no NSubstitute.
- `tests/OpenClaw.HostAdapter.Tests/OpenClaw.HostAdapter.Tests.csproj`: MSTest 3.6.4 + Moq 4.20.72 +
  FluentAssertions 6.12.0.
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`: MSTest 3.6.4 + FluentAssertions
  6.12.0 (no Moq — bridge tests use hand-written doubles, e.g. `MailBridgeRuntimeTestDoubles.cs`).
- Using-directives confirm MSTest attributes: `HostAdapterSchedulingServiceTests.cs` lines 6-9
  (`FluentAssertions`, `Microsoft.VisualStudio.TestTools.UnitTesting`, `Moq`), `[TestClass]` line 22;
  `HostAdapterEndpointTests.cs` lines 3-6 with `[TestMethod]`. Across `tests/**` the pattern is uniform
  (`Microsoft.VisualStudio.TestTools.UnitTesting` everywhere).

## 7. Quality / Tier Constraints in Scope

Tier map (`quality-tiers.yml`): OpenClaw.Core = **T1**, OpenClaw.HostAdapter = **T1**;
OpenClaw.MailBridge.Contracts = **T2**, OpenClaw.HostAdapter.Contracts = **T2**,
OpenClaw.MailBridge = **T2**; OpenClaw.MailBridge.Client = **T3**.

- Coverage (uniform, `.claude/rules/general-unit-test.md`): line >= 85%, branch >= 75% across all tiers;
  no regression on changed lines. No production file may be `exclude`d from coverage.
- Untyped escape hatches: T1 modules 0 `dynamic`/`any`. Note the COM read in `ComMessageSource` uses
  late-bound `object`/`InvokeMember` (not `dynamic`) and lives in T2 `OpenClaw.MailBridge`, confined per
  the architecture-boundary rule (no COM types on the `IMessageSource` surface).
- Property tests: >= 1 per pure function for T1/T2 modules. Mutation score >= 75% for T1 (pre-merge/nightly).
- Contract / schema tests: required at host-service boundaries; the new wire method crosses the
  MailBridge and HostAdapter boundaries. Contract-breaking changes to T1/T2 require a major bump — the
  new method and the appended nullable `MessageDto` field are additive (non-breaking) if the field is
  positional-last with a `null` default, mirroring the #72/#73 additions.
- Determinism (`.claude/rules/general-unit-test.md`): use the injected `TimeProvider`
  (`FakeTimeProvider` in tests); no `DateTime.Now`/`UtcNow` in code under test, no `Task.Delay`/
  `Thread.Sleep`/`setTimeout`/real waits in tests. Caveat: `CacheRepository` currently stamps
  `last_seen_utc` with `DateTimeOffset.UtcNow` (CacheRepository.cs lines 393-396, 453-456) — this is
  pre-existing persistence behavior, not newly introduced; do not add new wall-clock reads.
- File size: no source/test file may exceed 500 lines. `OutlookScanner.cs` is already at its cap
  (partial-class split); add scanner logic to a partial file, not to `OutlookScanner.cs`. `PipeRpcWorker.cs`
  (~438 lines) and `CacheRepository.cs` (~480 lines) are near the cap — prefer adding the new repo method
  and handler in a way that keeps files under 500 (e.g. a `CacheRepository` partial or the existing
  `.Readers.cs`).

## 8. Concrete File Change List (planner budget)

Production (create `*` / modify):
- `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (BridgeMethods const + All; MessageDto field)
- `src/OpenClaw.MailBridge/PipeRpcWorker.cs` (switch arm + handler)
- `src/OpenClaw.MailBridge/CacheRepository.cs` (IBridgeRepository method + impl + upsert/insert wiring)
- `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` (read new column)
- `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` (DDL column + guarded-ALTER migration)
- `src/OpenClaw.MailBridge/IMessageSource.cs` (new linked-key member)
- `src/OpenClaw.MailBridge/ComMessageSource.cs` (fail-soft COM read)
- `src/OpenClaw.MailBridge/OutlookScanner.cs` (NormalizeMessage populates the field) and/or a scanner partial
- `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` (sensitive-path default, if chosen)
- `src/OpenClaw.MailBridge.Client/Program.cs` (new verb)
- `src/OpenClaw.HostAdapter/Program.cs` or a scheduling-routes partial (new route)
- `src/OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs` (BuildGetEventForMessage)
- `src/OpenClaw.HostAdapter/*` (null-tolerant projector helper; e.g. in Program partial or the runner)
- `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` (new client method)
- `src/OpenClaw.Core/HostAdapterHttpClient.cs` (impl)
- `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Messages.cs` or new partial (impl)
- `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (rewire lines 33-45)

Tests (mirror layout):
- `tests/OpenClaw.MailBridge.Tests/` — new repo/handler/scanner/ComMessageSource tests; extend
  `BridgeContractsCoverageTests.cs`, `CacheRepositoryMessageFieldsTests.cs`, `CacheRepositoryMigrationIdempotencyTests.cs`.
- `tests/OpenClaw.HostAdapter.Tests/` — new route + projector tests; extend `HostAdapterEndpointTests.cs`,
  `HostAdapterProcessRunnerTests.cs`, `HostAdapterCommandBuilder`-adjacent tests, `HostAdapterMappingTests.cs`.
- `tests/OpenClaw.Core.Tests/` — extend `Agent/Runtime/HostAdapterSchedulingServiceTests.cs`,
  `HostAdapterHttpClientSchedulingTests.cs`, `Agent/Runtime/SchedulingWorkerFallbackTests.cs`,
  `CloudGraph/GraphHostAdapterClientMessagesTests.cs`, `CloudGraph/CloudGraphContractParityTests.cs`.

## 9. Automation Feasibility

The full code / build / test loop is automatable without human interaction.

- The RPC, contract, route, client, and Core-rewire changes are exercised through seams that the
  existing tests already use: in-memory SQLite `CacheRepository`, `PipeRpcWorker.BuildResponseAsync`
  (no real named pipe needed), the `HostAdapterProcessRunner.ProcessExecutor` func seam (no child
  process spawned), the `HostAdapterHttpClient.TokenReader` seam, and Moq `IHostAdapterClient`. No live
  HostAdapter process, pipe, or HTTP server is required for unit tests.
- The **only** genuinely host-bound code is the Outlook COM read in `ComMessageSource`
  (`GetAssociatedAppointment` / `GlobalAppointmentID`). This is not automatable against a live Outlook
  desktop in CI, which is precisely why the repo confines COM behind `IMessageSource` and tests
  `ComMessageSource` with in-process fakes (see `ComMessageSourceTests`, `ComMessageSourceResolutionTests`,
  and the `MailBridgeRuntimeTestDoubles`). The new COM member is tested the same way: fake COM objects
  exercising the fail-soft path, and pure/DB tests for the resolution join. No Azure portal, Graph
  tenant, or interactive Outlook session is required for the build/test loop.
- The `[ExcludeFromCodeCoverage]` pipe/ACL plumbing in `PipeRpcWorker` (CreateServer/BuildPipeSecurity/
  HandleClientAsync) is unchanged by this feature; the new handler is reached via `BuildResponseAsync`,
  which is covered without OS pipe access.

Conclusion: fully automatable via seams/fakes. No unautomatable dependency blocks implementation,
build, or the seven-stage toolchain loop.
