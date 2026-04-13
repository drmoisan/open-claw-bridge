# 2026-04-11-open-claw-docker — Spec

- **Issue:** #23
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-12T16-58
- **Status:** Draft
- **Version:** 0.1

## Overview

OpenClaw.MailBridge is now a working Windows-local Outlook bridge with a CLI client, shared contracts, SQLite-backed cached reads, safe versus enhanced response shaping, and operator/install automation. What the repository does not yet have is a safe way to run the rest of an OpenClaw experience outside the Windows/Outlook boundary. The prototype bundle under `artifacts\gpt-web-dev\openclaw-pre-mvp-docker-bundle` defines that missing pre-MVP path: keep Outlook, `OpenClaw.MailBridge`, and `OpenClaw.MailBridge.Client` on Windows, then add a narrow Windows-side HTTP HostAdapter plus a Linux-containerized `OpenClaw.Core` app for local UI, polling, and cached read-only views.

The bundle is useful because it already contains a coherent architectural split, Docker and devcontainer assets, API contracts, deployment notes, and compile-ready project scaffolding. It is not ready to merge blindly into this repository. Several prototype assumptions diverge from the current codebase: it refers to older `EmailBridge`/`EmailClient` names instead of `OpenClaw.MailBridge`/`OpenClaw.MailBridge.Client`, targets .NET 8 while the repo currently targets `net10.0-windows` for Windows projects, and includes xUnit placeholder tests even though this repository standardizes on MSTest plus FluentAssertions for C#. This feature therefore needs to capture the prototype as a guided integration effort, not a copy operation, so the existing bridge contract, scripts, documentation, and regression coverage remain intact.


## Behavior

Incorporate the bundle as an architectural reference and starter scaffold for a new read-only OpenClaw pre-MVP deployment model. The implementation should add `OpenClaw.HostAdapter`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.Core`, container/deployment assets, and matching tests alongside the existing bridge projects without renaming, replacing, or destabilizing the current named-pipe bridge stack.

At a high level, the resulting design should work as follows:

- Windows host remains the only place that talks to Outlook and the named pipe.
- `OpenClaw.HostAdapter` exposes a tiny authenticated HTTP surface that maps directly to the existing six read-only bridge operations by shelling out to `OpenClaw.MailBridge.Client` first.
- `OpenClaw.Core` runs in a Linux container, calls only the HostAdapter over HTTP, persists a local SQLite cache, surfaces freshness and redaction status, and provides a simple local UI plus internal API.
- Docker and devcontainer assets are added in a way that is compatible with the current solution, scripts, docs, and repo policies.

The prototype material should be leveraged selectively: keep the boundary, runtime hardening posture, OpenAPI contracts, and staged implementation intent, but reconcile all paths, project names, frameworks, test libraries, docs, and operational commands with the current repository before any code is promoted into the main architecture.


## Inputs / Outputs

- Inputs (CLI flags, files, env vars)
	- Existing `OpenClaw.MailBridge.Client` commands remain the pre-MVP transport and are invoked unchanged by the HostAdapter: `status`, `list-messages --since <utc> --limit <n>`, `get-message --id <bridgeId>`, `list-meeting-requests --since <utc> --limit <n>`, `list-calendar --start <utc> --end <utc> --limit <n>`, and `get-event --id <bridgeId>`.
	- HostAdapter request inputs are limited to one bearer token header (`Authorization: Bearer <token>`), an optional correlation header (`X-Request-Id`), query parameters (`since`, `start`, `end`, `limit`), and the opaque path parameter `bridgeId`.
	- Host-side configuration inputs come from `%ProgramData%\OpenClaw\HostAdapter\appsettings.json` and `%ProgramData%\OpenClaw\HostAdapter\adapter.token`; the token file is ACL-restricted and is the only secret the container receives.
	- Container runtime inputs are defined through Compose and `.env.example` values evidenced by the bundle: `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__AppDb`, `OpenClaw__HostAdapter__BaseUrl`, `OpenClaw__HostAdapter__TokenFile`, `OpenClaw__Polling__MessagesIntervalSeconds`, `OpenClaw__Polling__MeetingRequestsIntervalSeconds`, `OpenClaw__Polling__CalendarIntervalSeconds`, `OpenClaw__Polling__MessageLookbackHours`, `OpenClaw__Polling__CalendarPastDays`, `OpenClaw__Polling__CalendarFutureDays`, `OpenClaw__Defaults__Limit`, `OpenClaw__Defaults__MaxLimit`, `OPENCLAW_HTTP_PORT`, and `HOSTADAPTER_TOKEN_FILE`.
	- Repo-level inputs added by this feature are expected to include merged `docker-compose.yml`, `docker-compose.dev.yml`, `.dockerignore`, `.env.example`, and selective `.devcontainer/` updates aligned to the current solution and workspace layout.
- Outputs (artifacts, logs, telemetry)
	- `OpenClaw.HostAdapter` returns an `ApiEnvelope<T>` success/error wrapper for every HTTP call, always including `meta.requestId`, `meta.adapterVersion`, and `meta.bridge` on successful data responses.
	- `OpenClaw.Core` emits local-only UI and internal API responses for `/health/live`, `/health/ready`, `/api/status`, `/api/messages/recent`, `/api/messages/{bridgeId}`, `/api/events/window`, and `/api/events/{bridgeId}` using cached SQLite-backed data.
	- The container persists its own database at `/data/openclaw.db`; this is separate from any bridge-side SQLite cache and should be backed by a named Docker volume.
	- Structured logs are emitted from both the HostAdapter and Core to standard application logging sinks. Required fields are request ID, route/operation, duration, bridge state, bridge error code, poll outcome, and cache freshness; prohibited fields are token values, message bodies, and attendee details.
	- Health and readiness signals become operational outputs: HostAdapter uses `/v1/status` as its health signal, while Core exposes `200` for live, `200` for ready only when SQLite and HostAdapter connectivity checks pass, and `503` when readiness checks fail.
- Config keys and defaults:
	- HostAdapter base URL defaults to `http://host.docker.internal:4319/v1` from the container and may use `http://localhost:4319/v1` only in the optional Docker Desktop host-networking mode documented by the bundle.
	- Container UI publishing defaults to `127.0.0.1:${OPENCLAW_HTTP_PORT:-8080}:8080`; loopback binding is required so the pre-MVP UI is not exposed beyond the Windows host.
	- Polling defaults remain environment-driven rather than hardcoded in code paths: message and meeting-request polling default to `60` seconds in the bundle compose file, calendar polling defaults to `300` seconds, message lookback defaults to `48` hours, and calendar windows default to `14` days past and `30` days future.
	- The request `limit` default is `100` and must never exceed `250`; invalid values are rejected or clamped according to the endpoint contract instead of being passed through to the bridge unchecked.
	- HostAdapter status caching uses an in-memory TTL of `5` seconds so `meta.bridge` can be attached consistently without issuing a second bridge status call for every data request.
- Versioning or backward-compatibility constraints:
	- The existing named-pipe RPC contract, `OpenClaw.MailBridge.Client` CLI surface, bridge error codes, DTO property names, and safe-mode semantics remain canonical and must not change as part of this feature.
	- `OpenClaw.HostAdapter.Contracts` may add HTTP envelope types and a typed client abstraction, but it must not fork `BridgeStatusDto`, `MessageDto`, or `EventDto` into competing models when `OpenClaw.MailBridge.Contracts` can stay the shared source of truth.
	- If `OpenClaw.MailBridge.Contracts` is retargeted from `net10.0-windows` to `net10.0`, namespaces and public type names must stay stable so current Windows projects do not require caller-visible API changes.
	- Existing install, run, and test scripts continue to describe the Windows bridge baseline even after Docker assets are added; the containerized path is additive rather than a replacement deployment model.

## API / CLI Surface

List commands, flags, request/response shapes, and examples.

The implementation keeps the current CLI contract intact and layers a narrow HTTP contract on top of it.

- Existing CLI commands reused by the HostAdapter with no pre-MVP renames:
	- `OpenClaw.MailBridge.Client.exe status`
	- `OpenClaw.MailBridge.Client.exe list-messages --since <utc> --limit <n>`
	- `OpenClaw.MailBridge.Client.exe get-message --id <bridgeId>`
	- `OpenClaw.MailBridge.Client.exe list-meeting-requests --since <utc> --limit <n>`
	- `OpenClaw.MailBridge.Client.exe list-calendar --start <utc> --end <utc> --limit <n>`
	- `OpenClaw.MailBridge.Client.exe get-event --id <bridgeId>`
- HostAdapter HTTP surface mapped one-to-one to those commands:
	- `GET /v1/status` -> returns `ApiEnvelope<BridgeStatusDto>`
	- `GET /v1/messages?since=<utc>&limit=<n>` -> returns `ApiEnvelope<ItemsResponse<MessageDto>>`
	- `GET /v1/messages/{bridgeId}` -> returns `ApiEnvelope<MessageDto>`
	- `GET /v1/meeting-requests?since=<utc>&limit=<n>` -> returns `ApiEnvelope<ItemsResponse<MessageDto>>`
	- `GET /v1/calendar?start=<utc>&end=<utc>&limit=<n>` -> returns `ApiEnvelope<ItemsResponse<EventDto>>`
	- `GET /v1/events/{bridgeId}` -> returns `ApiEnvelope<EventDto>`
- Core internal API and UI surface, served from the Linux container and backed by SQLite cache:
	- `GET /health/live`
	- `GET /health/ready`
	- `GET /api/status`
	- `GET /api/messages/recent?since=<utc>&kind=<all|mail|meeting>&limit=<n>`
	- `GET /api/messages/{bridgeId}`
	- `GET /api/events/window?start=<utc>&end=<utc>&limit=<n>`
	- `GET /api/events/{bridgeId}`
- Example invocations with expected outputs (concise):
	- `curl -H "Authorization: Bearer <token>" http://host.docker.internal:4319/v1/status` -> `200` with `ApiEnvelope<BridgeStatusDto>` containing bridge state, mode, cache freshness, and last scan timestamps.
	- `curl -H "Authorization: Bearer <token>" "http://host.docker.internal:4319/v1/messages?since=2026-04-10T00:00:00Z&limit=100"` -> `200` with redacted or enhanced `MessageDto` items exactly as produced by the bridge, wrapped in `items` and `meta.bridge`.
	- `curl -H "Authorization: Bearer <token>" "http://host.docker.internal:4319/v1/calendar?start=2026-04-11T00:00:00Z&end=2026-04-18T00:00:00Z&limit=250"` -> `200` with `EventDto` items or an empty `items` array when the request is outside the bridge cache window.
	- `curl http://localhost:8080/api/status` -> `200` with app status, last poll timestamps, database path, cache item counts, and current bridge freshness.
	- `curl "http://localhost:8080/api/messages/recent?kind=meeting&limit=25"` -> `200` with cached meeting-request records already persisted by the Core poller.
- Contracts and validation rules:
	- `since`, `start`, and `end` must be ISO-8601 UTC timestamps; non-UTC values are rejected with `400 INVALID_REQUEST` rather than normalized silently.
	- `end` must be strictly greater than `start`; equal or reversed windows are invalid.
	- `limit` defaults to `100`, is constrained to `1..250`, and must not be allowed to fan out bridge load beyond the published ceiling.
	- `bridgeId` is opaque. The HostAdapter URL-decodes it once and passes it through unchanged without reparsing or synthesizing alternate identifiers.
	- Missing or invalid bearer tokens return `401 UNAUTHORIZED`; missing items return `404 NOT_FOUND`; `starting` or `waiting_for_outlook` bridge state returns `409 BRIDGE_NOT_READY`; bridge transport failures map to `502`; `OUTLOOK_UNAVAILABLE` maps to `503`; degraded cached reads still return `200` with `meta.bridge.cacheStale = true`.
	- The only shape added by the HostAdapter is the outer HTTP envelope plus `meta.bridge`; DTO field names from `OpenClaw.MailBridge.Contracts` remain unchanged.

## Data & State

Data flow, storage, or state changes introduced by this feature.

The data path stays intentionally narrow and sequential:

1. Outlook remains on the Windows host and continues to be queried only by `OpenClaw.MailBridge`.
2. `OpenClaw.MailBridge.Client` remains the pre-MVP transport adapter that resolves the named pipe, serializes the JSON-RPC request, parses the bridge response, and maps bridge failures to deterministic exit codes.
3. `OpenClaw.HostAdapter` shells out to the client with allowlisted arguments only, converts the process result into an `ApiEnvelope<T>`, and attaches a short-lived cached `meta.bridge` snapshot.
4. `OpenClaw.Core` calls the HostAdapter over HTTP from the Linux container, writes normalized records into its own SQLite database, and serves UI/API reads from that local cache.
5. Operators and local users consume the cached UI/API endpoints on `localhost` while the Windows host remains the only system that can reach Outlook or the named pipe.

- Data transformations and invariants:
	- `BridgeStatusDto`, `MessageDto`, `EventDto`, `BridgeMethods`, `BridgeErrorCodes`, and helper types remain canonical in `OpenClaw.MailBridge.Contracts`; the feature should reuse them rather than introducing a second DTO taxonomy.
	- Safe-mode redaction rules remain owned by the existing bridge. The HostAdapter and Core may surface redaction state (`isRedacted`, `protectedFieldsAvailable`, freshness badges) but must not attempt to rehydrate redacted data.
	- Message and event IDs remain opaque `bridgeId` values end-to-end. No layer in the HostAdapter or Core is allowed to parse or reinterpret them into a new identifier scheme.
	- HostAdapter request validation enforces UTC-only timestamps, `limit <= 250`, and `end > start`. Core validation mirrors those invariants on its internal APIs so cached reads and live reads behave consistently.
	- Sequential bridge access guidance remains in force. Polling and on-demand reads must avoid broad parallel fan-out against the Windows bridge.
- Caching or persistence details:
	- HostAdapter keeps only an in-memory status cache with a `5` second TTL; it does not persist its own durable data store in pre-MVP.
	- Core persists its own SQLite state under `/data/openclaw.db` using the bundle’s evidenced table set: `bridge_status_snapshots`, `messages`, `events`, `poll_cursors`, and `ingest_runs`.
	- Suggested natural keys are `bridge_id` for messages and events; each persisted record also tracks `observed_at_utc`, `adapter_request_id`, `bridge_mode`, `cache_stale`, `stale_reason`, and `is_redacted` so the UI and API can explain data freshness.
	- Poll cadence stays environment-driven. Message and meeting-request ingestion run on the shorter configured interval, calendar ingestion runs on the longer configured interval, and each ingestion pass records success/failure timing for readiness and troubleshooting.
	- When the bridge is degraded or temporarily unavailable, Core continues serving cached records and surfaces freshness warnings instead of fabricating live success.
- Migration or backfill requirements (if any):
	- No destructive migration of the existing bridge-side storage is required because `OpenClaw.Core` owns a separate SQLite database and read model.
	- Initial deployment can start with an empty Core database; the first successful poll seeds the cache from current bridge data.
	- If `OpenClaw.MailBridge.Contracts` is retargeted to `net10.0`, solution and project references must be updated so both Windows-only and cross-platform projects consume the same source library without duplicate DTO definitions.
	- There is no requirement to backfill historical Outlook data beyond the configured polling windows in pre-MVP. Calendar queries outside the cached bridge window may return empty results and should do so transparently.

## Constraints & Risks

- The feature boundary is still pre-MVP and read-only: no Outlook writes, no send/reply/accept/decline actions, no arbitrary pass-through RPC, and no direct container access to the named pipe.
- The current repository is Windows-bridge-first and already has working contracts, scripts, and acceptance evidence; integration work must preserve those established behaviors and avoid accidental renames or transport changes.
- The prototype bundle is partly scaffold-level material rather than production-ready code. Several files are placeholders, including `Program.cs` stubs and placeholder tests, so the bundle should be treated as early design evidence and scaffolding rather than proof of a completed implementation.
- The prototype's assumptions do not fully match this repo today: older project names, .NET/runtime differences, and xUnit placeholders all create compatibility risk if copied without adaptation.
- The host/container split adds new operational risk around token handling, host-to-container connectivity, Docker Desktop networking, Windows Firewall scope, health checks, and preserving sequential bridge access guidance.
- There is a regression risk if the HostAdapter reinterprets DTOs or error behavior instead of preserving the current bridge/client contract semantics and safe-mode data minimization rules.


## Implementation Strategy

- Implementation scope (what changes, not sequencing):
	- Add `src/OpenClaw.HostAdapter/`, `src/OpenClaw.HostAdapter.Contracts/`, `src/OpenClaw.Core/`, `tests/OpenClaw.HostAdapter.Tests/`, and `tests/OpenClaw.Core.Tests/` to the existing solution as additive projects.
	- Update `OpenClaw.MailBridge.sln` so the new projects participate in repo-standard builds and tests without displacing `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`, `OpenClaw.MailBridge.Contracts`, or `tests/OpenClaw.MailBridge.Tests`.
	- Normalize the prototype bundle to current repository reality before any merge: replace `EmailBridge`/`EmailClient` naming with current `OpenClaw.MailBridge` names, align all C# targets to the repo’s `.NET 10` baseline, convert bundle xUnit placeholders to MSTest plus FluentAssertions, and merge docker/devcontainer assets into the existing root and `.devcontainer/` structure instead of overwriting files.
	- Reuse the current contracts project as the single DTO/error-code source wherever possible; if portability remains confirmed during implementation, retarget `OpenClaw.MailBridge.Contracts` to `net10.0` so new cross-platform projects can reference it directly.
- New classes/functions/commands to add or update:
	- In `OpenClaw.HostAdapter`, implement the bundle-defined modules `AuthMiddleware`, `RequestValidation`, `BridgeClient`, `StatusCache`, and structured logging around ASP.NET Core Minimal API endpoints.
	- In `OpenClaw.HostAdapter.Contracts`, implement the HTTP envelope and typed-client surface evidenced by the bundle: `ApiEnvelope`, `ApiMeta`, `ApiError`, `ItemsResponse`, and `IHostAdapterClient`.
	- In `OpenClaw.Core`, implement a typed HostAdapter client, a background poller, persistence models/migrations for the SQLite cache, readiness/liveness endpoints, and Razor Pages or equivalent server-rendered UI endpoints that present recent items plus freshness/redaction badges.
	- Update repo automation and operator guidance so token generation, HostAdapter startup, and Docker startup use the current repository scripts/docs conventions instead of bundle-only examples.
	- Preserve the existing `OpenClaw.MailBridge.Client` command verbs and argument shapes exactly; the HostAdapter command builder must use `ProcessStartInfo.ArgumentList` or an equivalent API rather than concatenated shell strings.
- Dependency changes (new/removed packages) and rationale:
	- New application projects should use ASP.NET Core on the repo’s `.NET 10` baseline because HostAdapter and Core are cross-platform HTTP workloads rather than Windows-only COM workloads.
	- `OpenClaw.Core` requires SQLite persistence; the implementation may use the bundle’s EF Core + SQLite approach or an equivalently simple SQLite access layer, but the dependency must stay local to Core and must not leak Windows-specific dependencies into the container.
	- `OpenClaw.HostAdapter.Contracts` should remain thin and reference `OpenClaw.MailBridge.Contracts` rather than redefining message/event/status DTOs.
	- Test dependencies must follow repository policy: MSTest plus FluentAssertions, with Moq available when isolation is necessary. Bundle xUnit dependencies are explicitly out of scope.
	- No dependency addition in this feature should weaken the current Windows bridge packaging or introduce a container-side dependency on Outlook, COM, named pipes, or arbitrary command execution libraries.
- Logging/telemetry additions and locations:
	- HostAdapter logs must record request ID, route, validated query shape, duration, CLI exit code, mapped bridge error code, and bridge readiness state. Sensitive data such as bearer tokens, message bodies, attendee JSON, and raw response payloads must never be logged.
	- Core logs must record poll start/finish, rows inserted/updated, last successful poll time, stale-cache conditions, readiness failures, and downstream HostAdapter response status.
	- Readiness and freshness information must also be observable through `/health/ready`, `/api/status`, and the local UI so operators can diagnose stale or degraded reads without inspecting raw logs first.
	- If service or container manifests add event-log or structured JSON sinks later, they must preserve the same no-sensitive-data rule and correlation via request or ingest IDs.
- Rollout plan (feature flags, staged deploys, fallback path):
	- Roll out as an additive local deployment path: existing Windows-only bridge install/run flows remain the baseline, while HostAdapter and Core are introduced as an optional pre-MVP operator path.
	- Sequence the delivery so the contracts/library normalization and HostAdapter are verified before the containerized Core relies on them; this keeps the fallback path simple because the current bridge/client stack continues to work throughout.
	- Keep a compatibility-first fallback: if HostAdapter or Core is unavailable, operators can still use `OpenClaw.MailBridge.Client` directly with the existing scripts and docs.
	- Reserve direct named-pipe integration, TLS/mTLS hardening, token rotation automation, and any wider deployment model for later phases after the CLI-backed adapter path is stable.

## Definition of Done

- [ ] Acceptance criteria in `issue.md`, `spec.md`, and `user-story.md` are traceable to named automated tests and/or explicit manual demo commands.
- [x] Windows host plus Docker Desktop validation demonstrates that the current bridge/client stack still works and that HostAdapter/Core behavior matches this spec in safe and degraded bridge states.
- [x] MSTest coverage exists for HostAdapter auth, UTC and range validation, `limit` handling, CLI exit-code mapping, response-envelope consistency, Core polling, and SQLite persistence/readback behavior.
- [x] Negative and boundary scenarios are covered by tests or demos, including missing token, invalid UTC input, `end <= start`, `limit > 250`, `NOT_FOUND`, `OUTLOOK_UNAVAILABLE`, and stale-cache serving behavior.
- [x] `README.md`, `docs/api-reference.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`, and the feature-doc folder describe the host/container split, local-only exposure, and fallback path.
- [x] Structured logging and health/status endpoints are updated so operators can diagnose readiness, stale cache, and bridge unavailability without exposing sensitive content.
- [x] Final repo-standard toolchain pass succeeds for the touched C# assets: `csharpier .`, analyzer build, nullable/treat-warnings-as-errors build, and the relevant MSTest assemblies or solution test pass.

## Seeded Test Conditions (from potential)
- [x] Unit coverage for HostAdapter token validation, UTC-only timestamp parsing, `bridgeId` pass-through handling, `limit` clamp/default behavior, status-cache TTL behavior, CLI exit-code/error-code mapping, and response-envelope consistency.
- [x] Unit coverage for Core polling cursors, SQLite upsert behavior for `messages` and `events`, stale-cache flag propagation, readiness-status computation, and cached read APIs filtering by `kind`, `start`, `end`, and `limit`.
- [x] Integration scenarios covering Windows HostAdapter to current CLI client wiring, container-to-host HTTP access through `host.docker.internal`, SQLite-backed stale-cache serving during HostAdapter outages, local-only port exposure on `127.0.0.1`, and preservation of sequential bridge access patterns.
- [x] Manual and automated contract checks for `GET /v1/status`, `GET /v1/messages`, `GET /v1/messages/{bridgeId}`, `GET /v1/meeting-requests`, `GET /v1/calendar`, `GET /v1/events/{bridgeId}`, `/health/live`, `/health/ready`, `/api/status`, `/api/messages/recent`, and `/api/events/window`.
- [ ] Operator troubleshooting coverage for missing token files, invalid bearer tokens, unavailable Outlook, bridge `waiting_for_outlook` or `starting` states, empty calendar-window results outside cache range, stale bridge cache, and degraded readiness.
