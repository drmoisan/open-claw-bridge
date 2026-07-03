# `graph-backed-adapter` — User Story

- Issue: #115
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-02

## Story Statement

- As the platform owner running OpenClaw against my principal mailbox, I want to swap the COM-backed local bridge for a Microsoft Graph backend by changing configuration only, so that I can move from the Local MVP to Product Increment 1 without modifying or re-validating the deterministic agent.
- As a developer maintaining the deterministic agent, I want a second `IHostAdapterClient` implementation whose parity with the local adapter is proven by contract tests against recorded Graph payloads, so that backend choice is a deployment decision rather than a code change and the agent's test suite remains the single source of behavioral truth.
- As the operator of the existing local Docker deployment, I want the default composition unchanged, so that nothing about my running system moves until I explicitly opt in.

## Problem / Why

The entire vision architecture rests on contract parity: the agent calls a Graph-shaped surface via `IHostAdapterClient`, and moving from the Local MVP to Product Increment 1 means swapping the COM-backed implementation for a Microsoft Graph-backed one with the agent unchanged (`docs/open-claw-approach.master.md` Delivery Stages "Migration path", §3). No Graph-backed implementation exists — `HostAdapterHttpClient` (which posts to the local HostAdapter) is the only implementation of `IHostAdapterClient`. This is the contract-parity payoff feature. Identified as gap F13 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Personas & Scenarios

- Persona: **Dan — platform owner and operator.**
  - Runs the Local MVP (Docker Core + Windows HostAdapter + COM MailBridge) against his own mailbox today.
  - Cares about the migration promise the architecture was designed around: the agent must not change when the backend does.
  - Constraints: the tenant work (app registration, RBAC scoping, assistant mailbox) is handled by the F11/F12 track; he cannot run live-tenant tests from CI.
  - Goal: flip one configuration flag and have the same deterministic scheduling behavior served by Graph. Frustration to avoid: a "migration" that turns into an agent rewrite or a silent behavior drift.
- Persona: **Agent developer.**
  - Maintains the deterministic core (`OpenClaw.Core.Agent`) and its runtime seam; relies on the architecture-boundary tests to keep partitions honest.
  - Cares about mapping fidelity: the triage and slot-proposal logic consumes specific DTO fields (sensitivity, meeting-message type, attendees, series identity), and a Graph mapping that drops one of them fails silently at the behavior level.
  - Goal: recorded-payload contract tests that prove the Graph client feeds the runtime the same shapes the local adapter does.

- Scenario: **Opt-in cutover to the Graph backend.**
  - Dan has completed the F11 admin handoff and configured F12 CloudAuth (`OpenClaw:CloudAuth:*`) for his tenant.
  - He sets `OpenClaw__GraphAdapter__Enabled=true`, `OpenClaw__GraphAdapter__PrincipalMailboxUpn=executive@contoso.com`, and `OpenClaw__GraphAdapter__AssistantMailboxUpn=assistant@contoso.com`, then restarts Core.
  - At startup the options validator runs fail-closed: a missing UPN or a non-https base URL stops the service with a clear error instead of limping into runtime failures.
  - The composition root registers `GraphHostAdapterClient` instead of `HostAdapterHttpClient`. The pollers, the scheduling service, and the dashboard keep calling the same `IHostAdapterClient` members; message lists, calendar windows, mailbox settings, free/busy, and outbound sends now flow to `graph.microsoft.com`.
  - When Graph throttles (429), the client waits per `Retry-After` (or bounded exponential backoff) and recovers; when Graph is unreachable, the status route reports a failure instead of a fabricated healthy snapshot, so `/health/ready` and the dashboard tell the truth.
  - Outcome: the agent's decisions and proposals are indistinguishable from the local-backend runs; removing the flag returns him to the local bridge.
- Scenario: **Developer proves parity without a tenant.**
  - A developer adds a field to the scheduling logic and wants confidence it works on both backends.
  - She runs the test suite: handler-level tests pin every Graph request shape, and the recorded-payload contract suite drives `HostAdapterSchedulingService` through `GraphHostAdapterClient` with a mocked handler — no network, no tenant, deterministic time via `FakeTimeProvider`.
  - Obstacle handled: if her change had required a new DTO field that the Graph mapping does not populate, the parity tests would name the missing field rather than letting behavior drift.
  - Outcome: parity holds in CI on every commit; live-tenant confirmation stays where it belongs (F17 smoke test).

## Acceptance Criteria

- [x] `GraphHostAdapterClient` (namespace `OpenClaw.Core.CloudGraph`) implements all nine `IHostAdapterClient` members; handler-level tests against a mocked `HttpMessageHandler` verify each endpoint's request shape: URL and query composition (`$select`/`$filter`/`$top`/paging), HTTP method, `Authorization: Bearer` sourced from `IAppTokenProvider`, `client-request-id`, the `Prefer: outlook.timezone` and `Prefer: outlook.body-content-type="text"` headers, and the `getSchedule` and `sendMail` JSON bodies (with `from` = principal mailbox when principal != assistant).
- [x] Response mapping from recorded Graph v1.0 payloads populates every wire-DTO field in the parity minimum set (spec "Data & State"), including sensitivity (`private` -> 2), `iCalUId`/`seriesMasterId`, attendee-type partitioning into the OR-5 attendee-JSON shape, importance, and `meetingMessageType`; mappers are pure static functions with CsCheck property tests for the enum and attendee-JSON mappings.
- [x] 429/`Retry-After` handling is deterministic: `Retry-After` (delta-seconds or HTTP-date) takes precedence over the exponential fallback, attempts are bounded by configuration, all delays flow through the injected `TimeProvider` (verified with `FakeTimeProvider`; no wall-clock sleeps), and exhaustion returns a failure envelope whose `ApiError` is retryable and carries the request id in `ApiMeta`.
- [x] Contract parity is demonstrated: representative Agent/Runtime expectations (`HostAdapterSchedulingService` flows) pass against `GraphHostAdapterClient` backed by a mocked handler returning recorded Graph payloads; production code under `OpenClaw.Core.Agent` is unchanged; namespace-scoped NetArchTest rules assert `OpenClaw.Core.CloudGraph` depends on no `OpenClaw.MailBridge.*` namespace other than `OpenClaw.MailBridge.Contracts`, no COM interop (`Microsoft.Office.Interop.Outlook`, `System.Runtime.InteropServices`), and that `OpenClaw.Core.Agent` (including `Runtime`) does not depend on `OpenClaw.Core.CloudGraph`.
- [x] Backend selection is opt-in: `AddGraphHostAdapterClient` takes effect only when `OpenClaw:GraphAdapter:Enabled` is `true`; with the flag absent or false the composition root registers `HostAdapterHttpClient` exactly as today (untouched-surface verification for the Program.cs default path and docker-compose).
- [x] The full C# toolchain passes (CSharpier, analyzers, nullable, architecture tests, MSTest + FluentAssertions + Moq + CsCheck) and coverage holds at line >= 85% / branch >= 75% with changed lines covered; no live Graph calls and no temporary files in any test; every new file <= 500 lines.

## Non-Goals

- **Send-on-behalf allowlist and rendered-appearance validation** — this feature ships the plain Graph `sendMail` shape with `from` = principal (master §5.3); the recipient allowlist and tenant-rendering validation land in F15.
- **Live-tenant verification** — no live Graph calls anywhere in this feature; tenant smoke testing is F17, admin handoff is F11.
- **Delta sync and change notifications** (master §6.1/§6.2 `/delta`, subscriptions, webhooks) — the existing polling cadence is unchanged.
- **Calendar writes** (`PATCH /events`, `tentativelyAccept`, event creation) — read-only plus `sendMail`, matching the current interface surface.
- **Reply routes** (`/reply`, `/replyAll`, `/createReply`) — not part of `IHostAdapterClient`.
- **Retiring or changing the local backend** — `HostAdapterHttpClient`, the HostAdapter service, and the MailBridge remain the default deployment; no removal, no default flip.
- **Send idempotency/dedupe keys and dead-lettering** (master §6.3) — bounded retry only; the durable send queue is later work.
- **Changes to `IHostAdapterClient` or the wire DTOs** — the contract is frozen; this feature is purely a second implementation behind it.
