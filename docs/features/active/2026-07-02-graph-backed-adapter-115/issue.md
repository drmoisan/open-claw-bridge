# graph-backed-adapter (Issue #115)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/graph-backed-adapter/ (Issue #115)

- Issue: #115
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/115
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

The entire vision architecture rests on contract parity: the agent calls a Graph-shaped surface via `IHostAdapterClient`, and moving from the Local MVP to Product Increment 1 means swapping the COM-backed implementation for a Microsoft Graph-backed one with the agent unchanged (`docs/open-claw-approach.master.md` Delivery Stages "Migration path", §3). No Graph-backed implementation exists — `HostAdapterHttpClient` (which posts to the local HostAdapter) is the only implementation of `IHostAdapterClient`. This is the contract-parity payoff feature. Identified as gap F13 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Proposed Behavior

- New `OpenClaw.Core.CloudGraph` namespace: `GraphHostAdapterClient : IHostAdapterClient` implementing every interface member against Microsoft Graph REST v1.0 endpoints (master §3): messages list/get, calendarView window, event get, mailboxSettings, `calendar/getSchedule`, `sendMail` (assistant-mailbox submission with `from` = principal — the full Send-on-behalf semantics including the allowlist land in F15; this feature implements the plain request shape), plus status (synthesized locally — Graph has no bridge-status analog; document the substitute).
- Auth via the F12 `IAppTokenProvider` (bearer header per request); HTTP via `HttpClient`/`IHttpClientFactory` with a mockable handler seam; deterministic time rendering headers (`Prefer: outlook.timezone`, `Prefer: outlook.body-content-type="text"`) per master §3.1.
- 429/`Retry-After` handling with bounded exponential backoff per master §6.3 (deterministic: TimeProvider-driven delays, seedless).
- Response mapping: Graph JSON -> the existing wire DTOs (`ApiEnvelope<T>`, `MessageSummaryDto`, `EventDto` wire shapes in `OpenClaw.HostAdapter.Contracts`) so `OpenClaw.Core.Agent`/Runtime code runs unchanged.
- Contract-parity proof: a shared test suite (or contract tests against a mocked Graph handler returning recorded Graph-shaped payloads) demonstrating the Agent/Runtime test expectations hold against the new client; no live Graph calls anywhere in tests.
- Opt-in DI (`AddGraphHostAdapterClient`) selecting the backend by configuration; local Docker default remains the HTTP client (no behavior change until explicitly configured).

## Acceptance Criteria

- [x] `GraphHostAdapterClient` (namespace `OpenClaw.Core.CloudGraph`) implements all nine `IHostAdapterClient` members; handler-level tests against a mocked `HttpMessageHandler` verify each endpoint's request shape: URL and query composition (`$select`/`$filter`/`$top`/paging), HTTP method, `Authorization: Bearer` sourced from `IAppTokenProvider`, `client-request-id`, the `Prefer: outlook.timezone` and `Prefer: outlook.body-content-type="text"` headers, and the `getSchedule` and `sendMail` JSON bodies (with `from` = principal mailbox when principal != assistant).
- [x] Response mapping from recorded Graph v1.0 payloads populates every wire-DTO field in the parity minimum set (spec "Data & State"), including sensitivity (`private` -> 2), `iCalUId`/`seriesMasterId`, attendee-type partitioning into the OR-5 attendee-JSON shape, importance, and `meetingMessageType`; mappers are pure static functions with CsCheck property tests for the enum and attendee-JSON mappings.
- [x] 429/`Retry-After` handling is deterministic: `Retry-After` (delta-seconds or HTTP-date) takes precedence over the exponential fallback, attempts are bounded by configuration, all delays flow through the injected `TimeProvider` (verified with `FakeTimeProvider`; no wall-clock sleeps), and exhaustion returns a failure envelope whose `ApiError` is retryable and carries the request id in `ApiMeta`.
- [x] Contract parity is demonstrated: representative Agent/Runtime expectations (`HostAdapterSchedulingService` flows) pass against `GraphHostAdapterClient` backed by a mocked handler returning recorded Graph payloads; production code under `OpenClaw.Core.Agent` is unchanged; namespace-scoped NetArchTest rules assert `OpenClaw.Core.CloudGraph` depends on no `OpenClaw.MailBridge.*` namespace other than `OpenClaw.MailBridge.Contracts`, no COM interop (`Microsoft.Office.Interop.Outlook`, `System.Runtime.InteropServices`), and that `OpenClaw.Core.Agent` (including `Runtime`) does not depend on `OpenClaw.Core.CloudGraph`.
- [x] Backend selection is opt-in: `AddGraphHostAdapterClient` takes effect only when `OpenClaw:GraphAdapter:Enabled` is `true`; with the flag absent or false the composition root registers `HostAdapterHttpClient` exactly as today (untouched-surface verification for the Program.cs default path and docker-compose).
- [x] The full C# toolchain passes (CSharpier, analyzers, nullable, architecture tests, MSTest + FluentAssertions + Moq + CsCheck) and coverage holds at line >= 85% / branch >= 75% with changed lines covered; no live Graph calls and no temporary files in any test; every new file <= 500 lines.

## Constraints & Risks

- No live Graph calls in any test (mocked HttpMessageHandler with recorded Graph-shaped JSON); live verification is tenant-dependent and covered by runbooks (F11 handoff + later F17 smoke test).
- No new NuGet dependency expected (raw REST via HttpClient + System.Text.Json, consistent with master §9.2's direct-REST reference implementation; avoid the Graph SDK unless the spec finds a decisive reason — record the decision).
- MSTest + FluentAssertions + Moq + CsCheck; FakeTimeProvider; no temp files; 500-line cap (mapping code will need multiple partials/files).
- COM stays in MailBridge; CloudGraph must have zero MailBridge/COM references (architecture-boundary test).

## Test Conditions to Consider

- [ ] Handler-level request-shape tests per endpoint; error mapping (401/403/404/429/5xx) to ApiEnvelope errors.
- [ ] Mapping tests from recorded Graph payloads incl. edge fields (sensitivity private, recurring seriesMasterId, attendees types).
- [ ] Backoff: 429 then success; Retry-After honored; exhaustion propagates.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create active feature folder from the template
