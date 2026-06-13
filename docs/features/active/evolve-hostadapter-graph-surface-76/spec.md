# evolve-hostadapter-graph-surface — Spec

- **Issue:** #76
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-12
- **Status:** Ready
- **Version:** 1.0

## Overview

This is a breaking change to the `OpenClaw.HostAdapter` HTTP surface and the
`IHostAdapterClient` contract (a T2 boundary in `OpenClaw.HostAdapter.Contracts`).
The bespoke `/v1/*` route table is replaced with a Microsoft Graph-shaped surface
(`/users/{id}/messages`, `/users/{id}/messages/{messageId}`, `/users/{id}/calendarView`,
`/users/{id}/events/{eventId}`, and a messages-filtered meeting-requests form). The goal
is that the OpenClaw agent calls the same URL shapes against the local adapter in Stage 0
that it will use against the real Microsoft Graph in Product Increment 1.

This change replaces the OR-4 mapper-shim relationship introduced by #70. Under #70 the
`HostAdapterSchedulingService` shim (in `OpenClaw.Core`) wrapped a bespoke
`IHostAdapterClient` and translated to the agent's scheduling contract. After #76 the
`IHostAdapterClient` wire routes are natively Graph-shaped, so the adapter no longer needs
a bespoke-to-Graph translation at the route layer; the shim updates its call-sites to the
revised client and otherwise continues to operate on unchanged DTO fields.

- Target users/personas and primary use cases: the PI-1 OpenClaw agent that natively
  speaks Microsoft Graph route shapes against the local HostAdapter during Stage 0
  development, so the agent's request construction is portable to real Graph without
  rework.
- Success metrics or expected impact: the HostAdapter no longer serves any `/v1/*` route
  (the `/status` operational probe excepted); the in-repo callers (`HostAdapterHttpClient`,
  `HostAdapterSchedulingService`) compile and pass against the Graph-shaped routes; the
  adapter reports version `1.0.0` to signal the breaking contract change; DTO/envelope
  schema is unchanged.

## Behavior

The HostAdapter route table is reshaped from bespoke `/v1/*` paths to Graph-shaped paths.
The change is path-and-query only: request/response envelopes and DTO shapes are unchanged.
The `{id}` path segment is sourced from the new `HostAdapterOptions.MailboxId` configuration
property (default `"me"`), so routes render as `/users/me/...` by default.

- Main user flow (happy path): the agent (or `HostAdapterHttpClient`) issues a Graph-shaped
  request against the local adapter (for example
  `GET /users/me/messages?$filter=receivedDateTime ge 2026-06-12T00:00:00Z&$top=50`),
  the adapter resolves the bridge-cached data through the existing RPC chain, and returns
  an `ApiEnvelope<ItemsResponse<MessageDto>>` exactly as before.

- Per-route old -> new mapping:

  | Operation | Current bespoke route | New Graph-shaped route |
  |---|---|---|
  | Status probe | `GET /v1/status` | `GET /status` (operational probe; no Graph equivalent, kept as a plain non-Graph path) |
  | Messages list | `GET /v1/messages?since={iso}&limit={n}` | `GET /users/{id}/messages?$filter=receivedDateTime ge {iso8601}&$top={limit}` |
  | Single message | `GET /v1/messages/{bridgeId}` | `GET /users/{id}/messages/{messageId}` |
  | Meeting requests | `GET /v1/meeting-requests?since={iso}&limit={n}` | `GET /users/{id}/messages?$filter=meetingMessageType ne null and receivedDateTime ge {iso8601}&$top={limit}` (messages-filtered; see below) |
  | Calendar window | `GET /v1/calendar?start={iso}&end={iso}&limit={n}` | `GET /users/{id}/calendarView?startDateTime={iso8601}&endDateTime={iso8601}&$top={limit}` |
  | Single event | `GET /v1/events/{bridgeId}` | `GET /users/{id}/events/{eventId}` |

- Meeting-requests handling (decision D1, explicit): the meeting-requests capability is
  retained in full. `ListMeetingRequestsAsync` stays on `IHostAdapterClient`, the HostAdapter
  meeting-requests route stays, the bridge RPC chain (`list_recent_meeting_requests`) stays,
  the `MessagePollingWorker.PollMeetingRequestsAsync` path stays, and the Core UI
  meeting-requests surface stays. Only the wire route string and query parameters change.
  Microsoft Graph has no dedicated meeting-requests collection, so the Graph-shaped form is
  a messages-filtered query: `GET /users/{id}/messages?$filter=meetingMessageType ne null`
  combined with the existing `since` lower bound expressed Graph-style as
  `receivedDateTime ge {iso8601}`, with `$top={limit}`.

- Query-parameter renames (decision D4): `since` -> `$filter=receivedDateTime ge {iso8601}`;
  `start` -> `startDateTime`; `end` -> `endDateTime`; `limit` -> `$top`. The `$filter` and
  `$top` parameters carry the OData `$` prefix; `startDateTime` and `endDateTime` do not
  carry a `$` prefix (they are Graph calendarView required parameters). The timestamp value
  format remains ISO 8601 (the adapter already emits round-trip `O` format).

- Alternate/edge flows: the `/status` operation has no Graph equivalent and is retained as a
  plain operational probe at `GET /status`. The validation behavior for missing or malformed
  timestamps and over-limit `$top` values is preserved; only the parameter names read by the
  request validation change.

- Error handling and recovery behavior: error responses continue to use the existing
  `ApiError`/`ApiEnvelope<T>` envelope. Request validation rejects malformed Graph-shaped
  query parameters with the same status codes and error codes used today; only the parameter
  names that validation inspects change (`$filter` receivedDateTime bound, `startDateTime`,
  `endDateTime`, `$top`).

## Inputs / Outputs

- Inputs (CLI flags, files, env vars): no new CLI flags. The new configuration input is the
  `HostAdapterOptions.MailboxId` key (HostAdapter side) and the revised
  `OpenClawOptions.HostAdapter.BaseUrl` default (Core side).
- Outputs (artifacts, logs, telemetry): response envelopes are unchanged. The
  `meta.adapterVersion` field reports `1.0.0` after the version bump.
- Config keys and defaults:
  - `HostAdapterOptions.MailboxId` (new, in `src/OpenClaw.HostAdapter/HostAdapterOptions.cs`),
    default `"me"`. Sources the `{id}` path segment so routes render as `/users/me/...`.
  - `OpenClawOptions.HostAdapter.BaseUrl` (in `src/OpenClaw.Core/CoreOptions.cs`), default
    changes from `http://host.docker.internal:4319/v1/` to
    `http://host.docker.internal:4319/` (decision D6 — the `/v1/` segment is dropped).
  - Adapter version: `<Version>1.0.0</Version>` is added to
    `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj` (decision D5). This surfaces
    through `HostAdapterOptions.DefaultAdapterVersion` -> `AdapterVersion` into `ApiMeta`.
    The current effective version is the assembly fallback `0.1.0`; this is the first major
    bump to `1.0.0`.
- Versioning or backward-compatibility constraints: this is explicitly a breaking change to
  the HostAdapter HTTP surface and the `IHostAdapterClient` contract. It requires a major
  version bump (`1.0.0`). Backward compatibility with the `/v1/*` routes is not retained;
  callers must use the Graph-shaped routes. All in-repo callers are updated as part of this
  change: `HostAdapterHttpClient` (relative paths) and `HostAdapterSchedulingService`
  (call-site review against the revised client). Operators with a pinned `BaseUrl` containing
  `/v1/` must update their configuration; new deployments work without intervention because
  the default changes.

## API / CLI Surface

New route table served by `OpenClaw.HostAdapter` (`Program.cs`):

```
GET /status
GET /users/{id}/messages?$filter=receivedDateTime ge {iso8601}&$top={limit}
GET /users/{id}/messages/{messageId}
GET /users/{id}/messages?$filter=meetingMessageType ne null and receivedDateTime ge {iso8601}&$top={limit}
GET /users/{id}/calendarView?startDateTime={iso8601}&endDateTime={iso8601}&$top={limit}
GET /users/{id}/events/{eventId}
```

Example invocations with expected outputs (concise):

- `GET /users/me/messages?$filter=receivedDateTime ge 2026-06-12T00:00:00Z&$top=50`
  -> `200` with `ApiEnvelope<ItemsResponse<MessageDto>>`.
- `GET /users/me/messages/AAMkAD...` -> `200` with `ApiEnvelope<MessageDto>`.
- `GET /users/me/calendarView?startDateTime=2026-06-01T00:00:00Z&endDateTime=2026-06-30T00:00:00Z&$top=100`
  -> `200` with `ApiEnvelope<ItemsResponse<EventDto>>`.
- `GET /users/me/events/AAMkAD...` -> `200` with `ApiEnvelope<EventDto>`.
- `GET /status` -> `200` with `ApiEnvelope<BridgeStatusDto>`.

`IHostAdapterClient` method signatures: the C# shape is unchanged. The strongly-typed
parameters (`sinceUtc`, `limit`, `bridgeId`, `startUtc`/`endUtc`, `requestId`,
`cancellationToken`) are kept; only the wire routes the implementation emits change. The
methods retained are:

- `GetStatusAsync(requestId, cancellationToken)` -> `ApiEnvelope<BridgeStatusDto>`
- `ListMessagesAsync(sinceUtc, limit, requestId, cancellationToken)` -> `ApiEnvelope<ItemsResponse<MessageDto>>`
- `GetMessageAsync(bridgeId, requestId, cancellationToken)` -> `ApiEnvelope<MessageDto>`
- `ListMeetingRequestsAsync(sinceUtc, limit, requestId, cancellationToken)` -> `ApiEnvelope<ItemsResponse<MessageDto>>` (retained per D1)
- `ListCalendarWindowAsync(startUtc, endUtc, limit, requestId, cancellationToken)` -> `ApiEnvelope<ItemsResponse<EventDto>>`
- `GetEventAsync(bridgeId, requestId, cancellationToken)` -> `ApiEnvelope<EventDto>`

Contracts and validation rules: request validation reads the new query-parameter names
(`$filter` receivedDateTime lower bound, `startDateTime`, `endDateTime`, `$top`) and enforces
the existing `MaxLimit` ceiling on `$top`. The DTO and envelope contracts
(`ApiEnvelope<T>`, `ItemsResponse<T>`, `MessageDto`, `EventDto`, `ApiMeta`, `ApiError`,
`BridgeStatusDto`) are unchanged.

## Data & State

- Data transformations and invariants: none. The route reshaping is path-and-query only.
  No DTO field is added, removed, or renamed. The `SchedulingDtoMapper` continues to operate
  on unchanged DTO fields and is not modified by this change.
- Caching or persistence details: no change. The bridge-cache and RPC chain that backs each
  operation is unchanged, including `list_recent_meeting_requests` for the meeting-requests
  route.
- Migration or backfill requirements (if any): none for stored data. The only operator-facing
  migration is the `BaseUrl` configuration change (drop `/v1/`), which the new default handles
  for fresh deployments.

## Constraints & Risks

- Limits and acceptable trade-offs: `$top` remains bounded by the existing `MaxLimit`
  (default 250). No latency/throughput change is introduced; the work is route-string and
  parameter-name reshaping.
- T1/T2 contract boundary: `IHostAdapterClient` lives in `OpenClaw.HostAdapter.Contracts`
  (T2). Renaming query-parameter semantics on a public HTTP contract is a breaking change.
  Per `.claude/rules/quality-tiers.md`, a contract breaking change at a T1/T2 boundary
  requires a major version bump and a contract/schema compatibility check. The major bump is
  `1.0.0` (decision D5). The compatibility check must confirm the new interface is correctly
  implemented by `HostAdapterHttpClient` and that all in-repo callers are updated.
- Architecture boundaries preserved (per `.claude/rules/architecture-boundaries.md`): no new
  `ProjectReference` edges. `OpenClaw.Core` continues to depend only on
  `OpenClaw.HostAdapter.Contracts`; `OpenClaw.HostAdapter` continues to depend only on
  `OpenClaw.HostAdapter.Contracts` and `OpenClaw.MailBridge.Contracts`;
  `OpenClaw.HostAdapter.Contracts` continues to depend only on
  `OpenClaw.MailBridge.Contracts`. No COM boundary is crossed.
- Security/privacy considerations: no change to authentication or token handling. The
  `{id}` segment defaults to the non-identifying literal `"me"`; it does not place a real
  UPN into URLs unless an operator configures one.
- Operational/rollout risks and mitigations: operators with a pinned `BaseUrl` that includes
  `/v1/` must update configuration; mitigated by changing the default so new deployments work
  without intervention. The breaking route change is signaled by the `1.0.0` adapter version
  reported in `meta.adapterVersion`.

## Implementation Strategy

- Implementation scope (what changes, not sequencing): reshape the HostAdapter route table,
  update query-parameter names in request validation, update the typed client's relative
  paths, add the `MailboxId` option, change the Core `BaseUrl` default, bump the adapter
  version, and review the scheduling-service call-sites. No DTO/envelope change.

- Production files to change:
  - `src/OpenClaw.HostAdapter/Program.cs` — reshape the six route registrations to the
    Graph-shaped paths (status -> `/status`; messages, single message, meeting-requests
    messages-filtered, calendarView, single event under `/users/{id}/...`).
  - `src/OpenClaw.HostAdapter/HostAdapterRequestValidation.cs` — read the new query-parameter
    names (`$filter` receivedDateTime lower bound, `startDateTime`, `endDateTime`, `$top`)
    in place of `since`, `start`, `end`, `limit`.
  - `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` — update XML-doc text to
    describe Graph-shaped routes; method signatures keep the same C# shape (no member
    removal; `ListMeetingRequestsAsync` retained per D1).
  - `src/OpenClaw.Core/HostAdapterHttpClient.cs` — update the six relative-path constructions
    to the Graph-shaped paths, sourcing the `{id}` segment.
  - `src/OpenClaw.HostAdapter/HostAdapterOptions.cs` — add the `MailboxId` property (default
    `"me"`) and use it to render the `{id}` path segment.
  - `src/OpenClaw.Core/CoreOptions.cs` — change the `HostAdapter.BaseUrl` default to
    `http://host.docker.internal:4319/` (drop `/v1/`).
  - `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj` — add `<Version>1.0.0</Version>`.
  - `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — review call-sites
    against the revised `IHostAdapterClient`; update only if a call shape changed (signatures
    are unchanged, so this is expected to be a review with no functional edit).

- Test files to update:
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs` — route URL strings.
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterAuthTests.cs` — `/status` route string.
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterMappingTests.cs` — route and query-param strings.
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterValidationTests.cs` — route and query-param strings.
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterEnvelopeTests.cs` — `/status` route string.
  - `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — `BaseUrl`, path assertions,
    query-param-name assertions (`$filter=receivedDateTime`, `startDateTime`, `endDateTime`,
    `$top`).
  - `tests/OpenClaw.Core.Tests/MessagePollingWorkerTests.cs` and
    `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — review
    mock setups; `ListMeetingRequestsAsync` is retained, so mocks for it remain valid.

- Dependency changes (new/removed packages) and rationale: none.
- Logging/telemetry additions and locations: none beyond the `1.0.0` value surfacing in
  `meta.adapterVersion`.
- Rollout plan (feature flags, staged deploys, fallback path): no feature flag. The breaking
  change is gated by the major version bump and the contract/schema compatibility check.
  Operators update `BaseUrl` if pinned; the changed default covers fresh deployments.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)
