# graph-backed-adapter — Spec

- **Issue:** #115
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02
- **Status:** Ready
- **Version:** 1.0

## Overview

The vision architecture rests on contract parity: the agent calls a Graph-shaped surface via `IHostAdapterClient`, and moving from the Local MVP to Product Increment 1 means swapping the COM-backed implementation for a Microsoft Graph-backed one with the agent unchanged (`docs/open-claw-approach.master.md` Delivery Stages "Migration path", §3). Today `HostAdapterHttpClient` (`src/OpenClaw.Core/HostAdapterHttpClient.cs`), which posts to the local HostAdapter, is the only implementation of `IHostAdapterClient`. This feature adds `GraphHostAdapterClient` in the new `OpenClaw.Core.CloudGraph` namespace: a second implementation that speaks directly to Microsoft Graph REST v1.0. Identified as gap F13 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

**Delta from the promoted draft (re-derived from source):** the draft references a `MessageSummaryDto`; no such type exists. The wire DTOs are `MessageDto`, `EventDto`, and `BridgeStatusDto` in `OpenClaw.MailBridge.Contracts.Models` (`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`), wrapped by `ApiEnvelope<T>`/`ApiMeta`/`ApiError`/`ItemsResponse<T>` in `OpenClaw.HostAdapter.Contracts`. All mapping targets below use the real types.

## Behavior

### Interface-to-endpoint mapping

`GraphHostAdapterClient : IHostAdapterClient` implements all nine interface members (`src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`). `{p}` is the configured principal mailbox UPN; `{a}` is the assistant mailbox UPN.

| # | Interface member | Graph v1.0 request | Notes |
|---|---|---|---|
| 1 | `GetStatusAsync(requestId?, ct)` -> `ApiEnvelope<BridgeStatusDto>` | `GET /users/{p}/mailboxSettings?$select=timeZone` (liveness probe) | No Graph analog for bridge status; synthesized locally — see D2. |
| 2 | `ListMessagesAsync(sinceUtc, limit, requestId?, ct)` -> `ApiEnvelope<ItemsResponse<MessageDto>>` | `GET /users/{p}/messages?$filter=receivedDateTime ge {iso8601}&$orderby=receivedDateTime desc&$top={pageSize}&$select={message field list}` | Paged via `@odata.nextLink` — see D3. |
| 3 | `GetMessageAsync(bridgeId, requestId?, ct)` -> `ApiEnvelope<MessageDto>` | `GET /users/{p}/messages/{id}?$select={message field list}` | `bridgeId` carries the Graph message id (URL-escaped). |
| 4 | `ListMeetingRequestsAsync(sinceUtc, limit, requestId?, ct)` -> `ApiEnvelope<ItemsResponse<MessageDto>>` | `GET /users/{p}/messages?$filter=receivedDateTime ge {iso8601}&$orderby=receivedDateTime desc&$top={pageSize}&$select={message field list}` + client-side filter `@odata.type == "#microsoft.graph.eventMessage"` | Graph has no dedicated meeting-requests collection — see D10. |
| 5 | `ListCalendarWindowAsync(startUtc, endUtc, limit, requestId?, ct)` -> `ApiEnvelope<ItemsResponse<EventDto>>` | `GET /users/{p}/calendarView?startDateTime={iso8601}&endDateTime={iso8601}&$top={pageSize}&$select={event field list}` | Returns occurrences, exceptions, and single instances (master §3.1). Paged — see D3. |
| 6 | `GetEventAsync(bridgeId, requestId?, ct)` -> `ApiEnvelope<EventDto>` | `GET /users/{p}/events/{id}?$select={event field list}` | `bridgeId` carries the Graph event id (URL-escaped). |
| 7 | `GetMailboxSettingsAsync(requestId?, ct)` -> `ApiEnvelope<MailboxSettingsDto>` | `GET /users/{p}/mailboxSettings?$select=timeZone,workingHours` | Master §3.2. |
| 8 | `GetFreeBusyAsync(startUtc, endUtc, requestId?, ct)` -> `ApiEnvelope<FreeBusyScheduleDto>` | `POST /users/{p}/calendar/getSchedule` with JSON body | Wire form changes GET -> POST-with-body exactly as anticipated by the D2 portability note in the interface XML doc; the signature and all callers are unchanged. |
| 9 | `SendMailAsync(request, requestId?, ct)` -> `ApiEnvelope<object?>` | `POST /users/{a}/sendMail` with JSON body | `from` = principal when principal != assistant (master §5.3) — see D7. |

### Request conventions (mirrors `HostAdapterHttpClient` where applicable)

- **Request id:** caller-supplied `requestId` when non-blank, else `Guid.NewGuid().ToString()` (identical to the local client). Sent to Graph as the `client-request-id` header and echoed back in `ApiMeta.RequestId`.
- **Auth:** `Authorization: Bearer {token}` with the token obtained per attempt from `IAppTokenProvider.GetTokenAsync(ct)` (`src/OpenClaw.Core/CloudAuth/IAppTokenProvider.cs`, F12). The token value is never logged (`AppAccessToken` redacts it).
- **Prefer headers (read routes returning event/message bodies):** `Prefer: outlook.timezone="{GraphAdapterOptions.PreferredTimeZone}"` (default `UTC`) and `Prefer: outlook.body-content-type="text"` per master §3.1, so time rendering is deterministic and downstream logic receives text, not HTML.
- **Envelope synthesis:** Graph returns raw resource JSON, not `ApiEnvelope<T>`. `GraphHostAdapterClient` synthesizes the envelope: success -> `ApiEnvelope<T>(true, mapped, new ApiMeta(requestId, "cloudgraph", null), null)`; failure -> `ApiEnvelope<T>(false, default, new ApiMeta(requestId, "cloudgraph", null), mappedError)`. `ApiMeta.Bridge` is always `null` (there is no bridge). The local client uses `"hostadapter"` as its synthesized `AdapterVersion`; this client uses `"cloudgraph"` for symmetric identification.
- **Requests are rebuilt per attempt** (an `HttpRequestMessage` is single-use), so the retry pipeline accepts a request factory, not a request instance.

### Paging (D3)

List routes request `$top = min(limit, PageSize)` and follow `@odata.nextLink` until (a) `limit` items are accumulated, (b) the server stops returning a `nextLink`, or (c) `MaxPages` pages have been fetched — whichever comes first. Results are truncated to `limit`. `MaxPages` (default 10) is the determinism/runaway bound; hitting it is logged at `warning` and returns the truncated set as a success (matching the local adapter's best-effort list semantics).

### Retry policy (D6)

Applies to HTTP 429, 502, 503, and 504 only. All other statuses and all deserialization failures are terminal for the attempt.

- **Attempts:** `MaxAttempts` total attempts (default 4 = 1 initial + 3 retries).
- **Delay:** when the response carries `Retry-After` (delta-seconds or HTTP-date form), that value wins. Otherwise exponential backoff: `BaseDelay * 2^(attempt-1)` (default base 1 s -> 1 s, 2 s, 4 s), capped at `MaxDelay` (default 30 s).
- **Determinism:** every delay flows through `Task.Delay(delay, timeProvider, ct)` on the injected `TimeProvider`; the HTTP-date form of `Retry-After` is evaluated against `timeProvider.GetUtcNow()`. Tests advance a `FakeTimeProvider`; no wall-clock waits anywhere (banned-API rules in `.claude/rules/csharp.md`).
- **Exhaustion:** the final failure maps through the error matrix below with `Retryable = true`, and the message records the attempt count and last status.

### Error mapping matrix (D5)

Codes reuse the local adapter's vocabulary (`BridgeErrorCodes` plus the `HostAdapterHttpClient` synthesized codes) so `ApiError`-consuming callers see one vocabulary regardless of backend. The Graph `error.code` string (for example `ErrorItemNotFound`, `TooManyRequests`) is preserved in `ApiError.BridgeErrorCode` — repurposed as the backend-error passthrough field.

| Condition | `ApiError.Code` | `Retryable` |
|---|---|---|
| `TokenAcquisitionException` from `IAppTokenProvider` | `CONFIGURATION_ERROR` | `false` |
| HTTP 400 | `INVALID_REQUEST` | `false` |
| HTTP 401 / 403 | `UNAUTHORIZED` | `false` |
| HTTP 404 | `NOT_FOUND` | `false` |
| HTTP 429 after retry exhaustion | `THROTTLED` | `true` |
| HTTP 500 | `INTERNAL_ERROR` | `false` |
| HTTP 502 / 503 / 504 after retry exhaustion | `TRANSPORT_FAILURE` | `true` |
| `HttpRequestException` (network) | `TRANSPORT_FAILURE` | `true` |
| 2xx with unparseable/missing body (non-202 routes) | `TRANSPORT_FAILURE` | `false` |
| Any other status | `INTERNAL_ERROR` | `false` |

`THROTTLED` is the one new code (the local adapter never surfaces throttling); it is additive and no existing caller switches on an exhaustive code set (verified: `HostAdapterSchedulingService` only reads `Code`/`Message` into an exception string).

### Status substitute (D2)

Graph has no bridge-status analog. `GetStatusAsync` issues the cheapest stable read — `GET /users/{p}/mailboxSettings?$select=timeZone` (single resource, no paging) — through the same auth/retry pipeline and synthesizes:

- **Probe succeeds:** `BridgeStatusDto(State: "ready", Mode: "graph", OutlookConnected: true, CacheStale: false, StaleReason: null, LastInboxScanUtc: null, LastCalendarScanUtc: null)` in a success envelope. Graph reads are live, so there is no cache and no scan timestamps; `OutlookConnected` is interpreted as "backend reachable".
- **Probe fails:** a failure envelope with the mapped `ApiError` (no fabricated healthy status).

Rationale: a static capability descriptor was considered and rejected because it would report healthy while Graph is unreachable, corrupting `/health/ready`, `/api/status`, and the dashboard staleness badge. Consumer audit (verified): `BridgeStatusDto` fields are persisted (`CoreCacheRepository`) and displayed (`Program.cs` status endpoints, `Index.cshtml` badge); nothing parses `Mode` into `BridgeMode`, so the novel `"graph"` mode string is safe and honest, and `CacheStale: false` correctly renders "Fresh Cache".

## Design Decisions

- **D1 — Raw REST + System.Text.Json; no Graph SDK.** Matches master §9.2's direct-REST reference implementation; zero new NuGet dependencies (dependency-minimization rule in `.claude/rules/general-code-change.md`); the SDK would add a large transitive surface to the analyzer-strict build for six endpoints whose shapes are already pinned by this spec. Internal `GraphWireModels` records deserialize only the selected fields.
- **D2 — Status substitute:** probe-derived liveness (above), not a static descriptor.
- **D3 — Paging:** `@odata.nextLink` following bounded by `limit` and `MaxPages` (above).
- **D4 — Bounded `$select` per endpoint** (master §3.1). Field lists are exactly the mapping sources in "Data & State"; nothing else is requested.
- **D5 — Error matrix:** local-adapter code vocabulary plus `THROTTLED`; Graph `error.code` passes through `BridgeErrorCode`.
- **D6 — Retry:** `Retry-After` precedence, exponential fallback, bounded attempts, `TimeProvider`-driven (above).
- **D7 — SendMail:** plain Graph `sendMail` submitted through the assistant mailbox; `message.from.emailAddress.address = {p}` is set only when `{p} != {a}` (master §5.3). Success is Graph's `202 Accepted` (empty body) -> `ApiEnvelope<object?>(true, null, meta, null)`, matching the local adapter's D-A contract. Full send-on-behalf semantics including the recipient allowlist land in F15 — explicitly out of scope here.
- **D8 — Config/DI:** `GraphAdapterOptions` bound from `OpenClaw:GraphAdapter` with a fail-closed startup validator (mirroring `CloudAuthOptions`/`CloudAuthOptionsValidator`); `AddGraphHostAdapterClient(services, configuration)` is the single opt-in entry point and internally calls `AddCloudAuth` (whose XML doc already names F13 as its consumer), so one call wires token acquisition plus the typed client. The composition root selects the backend on `OpenClaw:GraphAdapter:Enabled` (default `false`); the default path registers `HostAdapterHttpClient` exactly as today.
- **D9 — File layout:** client core + endpoint partials + pure mapper files, each <= 500 lines (see Implementation Strategy).
- **D10 — Meeting-request identification is client-side.** Server-side filtering on `meetingMessageType` (an `eventMessage`-derived property) has under-documented support on the base `/messages` collection; a rejected `$filter` would be a runtime 400. The deterministic choice: filter server-side on `receivedDateTime` only and classify client-side from `@odata.type == "#microsoft.graph.eventMessage"`, paging (D3) until `limit` meeting messages are found or bounds are hit. `meetingMessageType` is included in the message `$select`; if implementation verification shows Graph v1.0 rejects the derived-type property in `$select` on the base collection, it is dropped from `$select` and `MeetingMessageType` is populated only on `GetMessageAsync` of an `eventMessage` — recorded as a verification item in Risks, with handler tests pinning whichever form ships.
- **D11 — Free/busy busy-statuses are conservative.** `getSchedule` `scheduleItems` with status `busy`, `oof`, or `tentative` map to `BusyIntervalDto`; `free` and `workingElsewhere` do not. Rationale: the slot proposer must not propose over tentative holds; over-blocking degrades to fewer proposals, under-blocking double-books the principal.
- **D12 — Architecture boundary (namespace-scoped NetArchTest, per the #74/#113 namespace-partition convention):**
  1. `OpenClaw.Core.CloudGraph` must not depend on any `OpenClaw.MailBridge.*` namespace **except** `OpenClaw.MailBridge.Contracts` — the carve-out is required because the wire DTOs (`MessageDto`, `EventDto`, `BridgeStatusDto`) live in `OpenClaw.MailBridge.Contracts.Models`; a blanket ban is unsatisfiable. Enforced with the dependency-inspection technique already used by `AgentArchitectureBoundaryTests.DeterministicSurface_DoesNotDependOnHostAdapterHostImplementation` (NetArchTest 1.3.2 cannot express prefix-ban-with-exception directly).
  2. `OpenClaw.Core.CloudGraph` must not depend on `Microsoft.Office.Interop.Outlook` or `System.Runtime.InteropServices` (COM stays in MailBridge).
  3. The entire `OpenClaw.Core.Agent` partition **including** `Runtime` must not depend on `OpenClaw.Core.CloudGraph`. Direction verified: `Runtime` consumes only `IHostAdapterClient` and has no need for the concrete client; backend wiring happens solely in the composition root (Program.cs), mirroring the CloudAuth-boundary precedent that bans Azure/MSAL for the whole Agent partition.

## Inputs / Outputs

- **Inputs:** configuration only; no CLI flags or files. Requires the F12 CloudAuth configuration (`OpenClaw:CloudAuth:*`) when enabled.
- **Outputs:** HTTP calls to Microsoft Graph; `ILogger<GraphHostAdapterClient>` logging — `debug` for request composition (never tokens, never message bodies), `warning` for retries and `MaxPages` truncation, `error` for exhausted retries and terminal failures.
- **Config keys and defaults** (section `OpenClaw:GraphAdapter`, env form `OpenClaw__GraphAdapter__*`):

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `false` | Backend selector; `false` keeps the local HTTP client. |
| `PrincipalMailboxUpn` | *(empty; required when enabled)* | `{p}` in all read routes; `from` on send. |
| `AssistantMailboxUpn` | *(empty; required when enabled)* | `{a}`; `sendMail` submits through it. |
| `BaseUrl` | `https://graph.microsoft.com/v1.0/` | Absolute https URI; override for national clouds only. |
| `PreferredTimeZone` | `UTC` | Value of `Prefer: outlook.timezone`. |
| `PageSize` | `50` | Per-page `$top` (1–1000). |
| `MaxPages` | `10` | `nextLink` follow bound (>= 1). |
| `MaxAttempts` | `4` | Total attempts incl. the first (1–10). |
| `BaseDelaySeconds` | `1` | Exponential base (> 0). |
| `MaxDelaySeconds` | `30` | Backoff cap (>= base). |
| `AvailabilityViewIntervalMinutes` | `30` | `getSchedule` interval (5–1440). |

`GraphAdapterOptionsValidator` enforces (fail-closed, `ValidateOnStart()`, only when `Enabled`): non-whitespace UPNs, absolute https `BaseUrl`, and the numeric bounds above.

- **Versioning / backward compatibility:** no contract change. `IHostAdapterClient`, all wire DTOs, and every existing caller are untouched. The only new public surface is `GraphAdapterOptions`, `AddGraphHostAdapterClient`, and the `THROTTLED` error code (additive).

## API / CLI Surface

No new HTTP routes or CLI commands are exposed; this feature is a client implementation behind an existing interface. Representative outbound wire shapes (contract for handler-level tests):

`GetFreeBusyAsync(start, end)` ->

```json
POST {BaseUrl}users/{p}/calendar/getSchedule
Prefer: outlook.timezone="UTC"
{
  "schedules": ["{p}"],
  "startTime": { "dateTime": "2026-07-06T00:00:00", "timeZone": "UTC" },
  "endTime":   { "dateTime": "2026-07-10T00:00:00", "timeZone": "UTC" },
  "availabilityViewInterval": 30
}
```

`SendMailAsync(request)` with principal != assistant (master §5.3 shape) ->

```json
POST {BaseUrl}users/{a}/sendMail
{
  "message": {
    "subject": "...",
    "from": { "emailAddress": { "address": "{p}" } },
    "toRecipients": [ { "emailAddress": { "address": "...", "name": "..." } } ],
    "body": { "contentType": "Text", "content": "..." }
  },
  "saveToSentItems": true
}
```

Validation rules: `SendMailRequest` passes through the existing wire shape (`MailContracts.cs`); the client adds only `from` and serializes with camelCase (`JsonSerializerDefaults.Web`). `Retry-After` is honored in both delta-seconds and HTTP-date forms.

## Data & State

No storage or persistence is introduced; the client is stateless (options + injected `HttpClient`/`TimeProvider`/`IAppTokenProvider`). All mapping is pure: Graph wire records in, wire DTOs out, no I/O, no mutation.

### Parity minimum set

The deterministic core consumes DTO fields through `SchedulingDtoMapper` (`src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`); the polling/cache path persists the full DTOs. Fields consumed by `SchedulingDtoMapper` are the **parity minimum set** (bold below); the mapper must populate them from Graph whenever Graph supplies a source. Attendee JSON uses the OR-5 shape `[{"name":"...","email":"..."}]` that `ParseAttendees` expects.

**`MessageDto` mapping** (`$select`: `id,subject,bodyPreview,receivedDateTime,sentDateTime,importance,sensitivity,isRead,hasAttachments,conversationId,from,sender,toRecipients,ccRecipients` + `meetingMessageType` per D10):

| DTO field | Graph source |
|---|---|
| **BridgeId** | `id` |
| ItemKind | `"meeting"` when `@odata.type == "#microsoft.graph.eventMessage"`, else `"mail"` |
| **Subject** | `subject` |
| **ReceivedUtc** | `receivedDateTime` |
| **SentUtc** | `sentDateTime` |
| **Importance** | `importance`: `low`->0, `normal`->1, `high`->2 (inverse of `MapImportance`) |
| Sensitivity | `sensitivity`: `normal`->0, `personal`->1, `private`->2, `confidential`->3 (inverse of `MapSensitivity`) |
| Unread | `!isRead` |
| HasAttachments | `hasAttachments` |
| MessageClass | `null` (no Graph analog; kind is carried by ItemKind) |
| **SenderName** | `sender.emailAddress.name` |
| SenderEmail | `sender.emailAddress.address` |
| **ToJson** / **CcJson** | `toRecipients` / `ccRecipients` -> OR-5 JSON |
| **BodyPreview** | `bodyPreview` |
| ProtectedFieldsAvailable / IsRedacted | `true` / `false` (app-only Graph reads full fields; no COM redaction) |
| **SenderEmailResolved** | `sender.emailAddress.address` |
| **FromEmailAddress** | `from.emailAddress.address` |
| **ConversationId** | `conversationId` |
| **MeetingMessageType** | `meetingMessageType`: `meetingRequest`->0, `meetingCancelled`->1, `meetingDeclined`->2, `meetingAccepted`->3, `meetingTentativelyAccepted`->4, `none`/absent->`null` (inverse of `MapMeetingMessageType`) |

**`EventDto` mapping** (`$select`: `id,iCalUId,seriesMasterId,subject,bodyPreview,body,organizer,attendees,categories,isOrganizer,isOnlineMeeting,allowNewTimeProposals,sensitivity,showAs,responseStatus,location,start,end,type,lastModifiedDateTime`):

| DTO field | Graph source |
|---|---|
| **BridgeId** | `id` |
| GlobalAppointmentId | `null` (COM-specific; `ICalUId` is the portable identity) |
| **Subject** | `subject` |
| **StartUtc** / **EndUtc** | `start` / `end` (`dateTime` + `timeZone`; `Prefer: outlook.timezone="UTC"` yields UTC) |
| Location | `location.displayName` |
| BusyStatus | `showAs`: `free`->0, `tentative`->1, `busy`->2, `oof`->3, `workingElsewhere`->4 |
| MeetingStatus | `null` (no direct Graph analog to `OlMeetingStatus`) |
| **IsRecurring** | `type != "singleInstance"` (i.e. `occurrence`, `exception`, or `seriesMaster`) |
| **Sensitivity** | `sensitivity` string -> 0–3 as above (`private` -> 2 is the private-meeting-rule signal) |
| **Organizer** | `organizer.emailAddress.address` |
| **RequiredAttendeesJson** / **OptionalAttendeesJson** / **ResourcesJson** | `attendees` partitioned by `type` (`required`/`optional`/`resource`) -> OR-5 JSON |
| **BodyPreview** | `bodyPreview` |
| ProtectedFieldsAvailable / IsRedacted | `true` / `false` |
| ResponseStatus | `responseStatus.response`: `none`->0, `organizer`->1, `tentativelyAccepted`->2, `accepted`->3, `declined`->4, `notResponded`->5 |
| **Categories** | `categories` |
| **IsOrganizer** | `isOrganizer` |
| **IsOnlineMeeting** | `isOnlineMeeting` |
| **AllowNewTimeProposals** | `allowNewTimeProposals` |
| **ICalUId** | `iCalUId` |
| **SeriesMasterId** | `seriesMasterId` |
| **LastModifiedDateTime** | `lastModifiedDateTime` |
| BodyFull | `body.content` (text via Prefer header) |
| SensitivityLabel | `null` (Purview label names are not on the v1.0 event; the int `Sensitivity` carries the signal) |

**`MailboxSettingsDto`:** `TimeZoneId` <- `timeZone`; `WorkingDays` <- `workingHours.daysOfWeek`; `WorkingHoursStart`/`WorkingHoursEnd` <- `workingHours.startTime`/`endTime`.

**`FreeBusyScheduleDto`:** `MailboxUpn` <- `{p}`; `BusyIntervals` <- `value[0].scheduleItems` where `status` is `busy`/`oof`/`tentative` (D11), mapped to `BusyIntervalDto(Start, End)` in UTC. An empty window yields an empty list, not an error (interface contract).

### Invariants

- Mappers are pure static functions; property-based tests (CsCheck) cover the four enum mappings (round-trip against `SchedulingDtoMapper`'s inverse maps) and attendee-JSON generation (generated attendee lists survive `ParseAttendees` round-trip).
- Missing optional Graph fields map to `null`/empty deterministically; a missing **required** field (`id`, event `start`/`end`) fails the item's envelope with `INTERNAL_ERROR` rather than fabricating data (fail-fast rule).
- No migration or backfill: no schema or storage changes.

## Constraints & Risks

- **No live Graph calls in any test** (mocked `HttpMessageHandler` with recorded Graph-shaped JSON held as in-repo raw-string fixtures; no temp files). Live verification is tenant-dependent and covered by runbooks (F11 handoff + later F17 smoke test).
- **No new NuGet dependency** (D1: raw REST via `HttpClient` + System.Text.Json). `Microsoft.Extensions.TimeProvider.Testing` is already in the test stack.
- **Test stack is MSTest + FluentAssertions + Moq + CsCheck with `FakeTimeProvider`** — the repository's actual stack (see `tests/OpenClaw.Core.Tests/`, e.g. `HostAdapterHttpClientTests.cs`, `HostAdapterHttpClientSendMailTests.cs`), notwithstanding `.claude/rules/csharp.md`'s xUnit/NSubstitute wording. Handler mocking follows the existing `FakeHttpHandler` pattern.
- **COM stays in MailBridge**; CloudGraph has zero COM references and no `OpenClaw.MailBridge.*` reference beyond `OpenClaw.MailBridge.Contracts` (D12 carve-out — the wire DTOs live there).
- **500-line cap** forces the partial/mapper file split (D9).
- **Verification item (D10):** whether Graph v1.0 accepts `meetingMessageType` in `$select` on the base `/messages` collection. Both outcomes are specified; handler tests pin the shipped form. This is the only externally unverifiable shape in the spec.
- **Rendered send-on-behalf appearance** ("Assistant on behalf of Executive") is tenant configuration, not code (master §5.3); out of scope, validated by F15/F17.
- **Throughput risk:** client-side meeting-request filtering (D10) reads more items than a server-side filter would; bounded by `PageSize * MaxPages`.

## Implementation Strategy

- **Scope:** new `OpenClaw.Core.CloudGraph` namespace inside the existing `OpenClaw.Core` project (namespace-not-project convention per the F12/#74 precedent); one conditional block in `Program.cs`; new tests. No changes to `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.*`, `OpenClaw.Core.Agent.*` production code, or `HostAdapterHttpClient`.
- **New files** (all under `src/OpenClaw.Core/CloudGraph/`, each <= 500 lines):
  - `GraphAdapterOptions.cs`, `GraphAdapterOptionsValidator.cs` — options bag + fail-closed validator (CloudAuth pattern).
  - `GraphRequestExecutor.cs` — shared pipeline: request factory execution, bearer header from `IAppTokenProvider`, `client-request-id`, retry/backoff (D6), error mapping (D5), envelope synthesis.
  - `GraphHostAdapterClient.cs` — constructor, DI seams, `GetStatusAsync` (D2).
  - `GraphHostAdapterClient.Messages.cs` — members 2–4 (list paging + client-side meeting filter).
  - `GraphHostAdapterClient.Calendar.cs` — members 5–8.
  - `GraphHostAdapterClient.SendMail.cs` — member 9 (D7 body composition).
  - `GraphWireModels.cs` — internal System.Text.Json records for the selected Graph fields.
  - `GraphMessageMapper.cs`, `GraphEventMapper.cs`, `GraphSchedulingMapper.cs` — pure static mappers (Data & State tables).
  - `GraphServiceCollectionExtensions.cs` — `AddGraphHostAdapterClient` (D8; internally calls `AddCloudAuth`, binds/validates options, registers `AddHttpClient<IHostAdapterClient, GraphHostAdapterClient>` with `BaseAddress` from options).
- **Changed file:** `src/OpenClaw.Core/Program.cs` — backend selection: when `OpenClaw:GraphAdapter:Enabled` is `true` call `AddGraphHostAdapterClient`, else the existing `AddHttpClient<IHostAdapterClient, HostAdapterHttpClient>` registration verbatim.
- **New tests** (under `tests/OpenClaw.Core.Tests/CloudGraph/`, mirroring source layout): per-endpoint request-shape tests, mapper unit + CsCheck property tests, retry/backoff tests with `FakeTimeProvider`, error-matrix tests, recorded-payload contract-parity tests driving `HostAdapterSchedulingService` against the Graph client, options-validator tests, DI opt-in/default-path tests, and `CloudGraphArchitectureBoundaryTests.cs` (D12).
- **Dependency changes:** none (D1).
- **Logging:** `ILogger<GraphHostAdapterClient>` per the Inputs/Outputs contract; token values and message bodies are never logged.
- **Rollout:** dark by default (`Enabled: false`); the local Docker deployment and docker-compose are untouched. Fallback is configuration-only: unset the flag and the local client returns. No staged deploy needed until F15/F17 exercise a live tenant.

## Acceptance Criteria

- [x] `GraphHostAdapterClient` (namespace `OpenClaw.Core.CloudGraph`) implements all nine `IHostAdapterClient` members; handler-level tests against a mocked `HttpMessageHandler` verify each endpoint's request shape: URL and query composition (`$select`/`$filter`/`$top`/paging), HTTP method, `Authorization: Bearer` sourced from `IAppTokenProvider`, `client-request-id`, the `Prefer: outlook.timezone` and `Prefer: outlook.body-content-type="text"` headers, and the `getSchedule` and `sendMail` JSON bodies (with `from` = principal mailbox when principal != assistant).
- [x] Response mapping from recorded Graph v1.0 payloads populates every wire-DTO field in the parity minimum set (spec "Data & State"), including sensitivity (`private` -> 2), `iCalUId`/`seriesMasterId`, attendee-type partitioning into the OR-5 attendee-JSON shape, importance, and `meetingMessageType`; mappers are pure static functions with CsCheck property tests for the enum and attendee-JSON mappings.
- [x] 429/`Retry-After` handling is deterministic: `Retry-After` (delta-seconds or HTTP-date) takes precedence over the exponential fallback, attempts are bounded by configuration, all delays flow through the injected `TimeProvider` (verified with `FakeTimeProvider`; no wall-clock sleeps), and exhaustion returns a failure envelope whose `ApiError` is retryable and carries the request id in `ApiMeta`.
- [x] Contract parity is demonstrated: representative Agent/Runtime expectations (`HostAdapterSchedulingService` flows) pass against `GraphHostAdapterClient` backed by a mocked handler returning recorded Graph payloads; production code under `OpenClaw.Core.Agent` is unchanged; namespace-scoped NetArchTest rules assert `OpenClaw.Core.CloudGraph` depends on no `OpenClaw.MailBridge.*` namespace other than `OpenClaw.MailBridge.Contracts`, no COM interop (`Microsoft.Office.Interop.Outlook`, `System.Runtime.InteropServices`), and that `OpenClaw.Core.Agent` (including `Runtime`) does not depend on `OpenClaw.Core.CloudGraph`.
- [x] Backend selection is opt-in: `AddGraphHostAdapterClient` takes effect only when `OpenClaw:GraphAdapter:Enabled` is `true`; with the flag absent or false the composition root registers `HostAdapterHttpClient` exactly as today (untouched-surface verification for the Program.cs default path and docker-compose).
- [x] The full C# toolchain passes (CSharpier, analyzers, nullable, architecture tests, MSTest + FluentAssertions + Moq + CsCheck) and coverage holds at line >= 85% / branch >= 75% with changed lines covered; no live Graph calls and no temporary files in any test; every new file <= 500 lines.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Handler-level request-shape tests per endpoint; error mapping (401/403/404/429/5xx) to ApiEnvelope errors per the D5 matrix, including Graph `error.code` passthrough into `BridgeErrorCode`.
- [ ] Mapping tests from recorded Graph payloads incl. edge fields (sensitivity `private`, recurring `seriesMasterId`/`type`, attendee `type` partitioning incl. `resource`, `meetingMessageType` vocabulary, missing-optional-field defaults, missing-required-field fail-fast).
- [ ] Backoff: 429 then success; `Retry-After` honored in both delta-seconds and HTTP-date forms; exhaustion propagates a retryable failure envelope; `FakeTimeProvider` advancement only.
- [ ] Paging: multi-page `@odata.nextLink` accumulation, truncation at `limit`, `MaxPages` bound with warning log.
- [ ] Status substitute: probe success -> `ready`/`graph`/fresh snapshot; probe failure -> failure envelope (no fabricated health).
- [ ] SendMail: `from` present iff principal != assistant; 202 -> `ok: true, data: null`.
- [ ] DI: `Enabled: false`/absent leaves the local registration; `Enabled: true` resolves `GraphHostAdapterClient`; validator rejects missing UPNs and non-https base URL fail-closed.
- [ ] Architecture: D12 rules 1–3.
