# `2026-04-11-open-claw-docker` — User Story

- Issue: #23
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-12T16-58

## Story Statement

- As a Windows bridge operator, I want a minimal authenticated `OpenClaw.HostAdapter` that reuses `OpenClaw.MailBridge.Client`, so that a Linux container can read Outlook-derived data without gaining direct access to Outlook, the named pipe, or broader host capabilities.
- As a local OpenClaw user, I want a containerized `OpenClaw.Core` that shows recent mail, meeting requests, and calendar data with freshness and redaction indicators, so that I can use a local read-only OpenClaw experience without weakening the repository’s current privacy and safety boundary.

## Problem / Why

OpenClaw.MailBridge is now a working Windows-local Outlook bridge with a CLI client, shared contracts, SQLite-backed cached reads, safe versus enhanced response shaping, and operator/install automation. What the repository does not yet have is a safe way to run the rest of an OpenClaw experience outside the Windows/Outlook boundary. The prototype bundle under `artifacts\gpt-web-dev\openclaw-pre-mvp-docker-bundle` defines that missing pre-MVP path: keep Outlook, `OpenClaw.MailBridge`, and `OpenClaw.MailBridge.Client` on Windows, then add a narrow Windows-side HTTP HostAdapter plus a Linux-containerized `OpenClaw.Core` app for local UI, polling, and cached read-only views.

The bundle is useful because it already contains a coherent architectural split, Docker and devcontainer assets, API contracts, deployment notes, and compile-ready project scaffolding. It is not ready to merge blindly into this repository. Several prototype assumptions diverge from the current codebase: it refers to older `EmailBridge`/`EmailClient` names instead of `OpenClaw.MailBridge`/`OpenClaw.MailBridge.Client`, targets .NET 8 while the repo currently targets `net10.0-windows` for Windows projects, and includes xUnit placeholder tests even though this repository standardizes on MSTest plus FluentAssertions for C#. This feature therefore needs to capture the prototype as a guided integration effort, not a copy operation, so the existing bridge contract, scripts, documentation, and regression coverage remain intact.


## Personas & Scenarios

- Persona: Windows bridge operator / repository maintainer
  - Maintains the existing Windows-local Outlook bridge and is accountable for not breaking the current named-pipe client, scripts, and safe-mode behavior.
  - Cares about compatibility-first changes, deterministic diagnostics, and keeping Outlook access confined to the Windows host.
  - Works within Windows and Docker Desktop constraints, including local firewall rules, token-file management, and the repo’s `.NET 10`, MSTest, and FluentAssertions standards.
  - Wants to introduce a pre-MVP OpenClaw experience without replacing the existing bridge deployment or creating a second incompatible contract surface.
  - Is frustrated by prototype scaffolding that is architecturally useful but mechanically mismatched with the repo’s names, framework targets, and test conventions.
- Persona: Local OpenClaw reviewer
  - Uses the pre-MVP OpenClaw UI/API on a development machine to review recent mailbox and calendar context locally.
  - Cares about knowing whether data is fresh, stale, or redacted before acting on it.
  - Accepts a read-only experience for pre-MVP, but needs predictable local startup and cached-data behavior when Outlook is slow or temporarily unavailable.
  - Wants local-only exposure and clear health/readiness signals rather than a broadly reachable network service.
- Scenario: Operator introduces the host/container split without disrupting the current bridge
  - The Windows bridge operator has already validated the current `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client` flow and wants to add the pre-MVP container path.
  - They generate or provision the HostAdapter token file, start `OpenClaw.HostAdapter` on the Windows host, and launch `docker compose up` for `OpenClaw.Core` using the loopback-only port binding.
  - `OpenClaw.Core` calls `http://host.docker.internal:4319/v1`, the HostAdapter validates the token, shells out to the existing CLI client, and the Core poller stores the results in SQLite.
  - If the bridge reports `degraded`, the operator expects the container to continue serving cached data while clearly showing freshness warnings; if the token is missing or invalid, they expect deterministic `401` behavior instead of silent retries or leaked secrets.
  - The expected outcome is that the existing Windows bridge flow still works unchanged, while the containerized path becomes available as an additive local feature.
- Scenario: Local user checks recent information and understands when it is stale or redacted
  - A local OpenClaw user opens the containerized UI on `http://localhost:8080` to review recent mail and calendar entries before planning work.
  - The UI reads from the Core cache, shows recent items, and displays badges or status details that indicate whether protected fields were redacted and whether the bridge cache is stale.
  - The user drills into a cached message or event by `bridgeId` and expects the DTO fields to match the current bridge contract rather than a newly invented translation model.
  - If Outlook is unavailable, the user expects cached data and readiness warnings rather than a broken page or a hidden failure.
  - The expected outcome is a stable, local, read-only experience that is explicit about freshness and privacy boundaries.


## Acceptance Criteria

- [x] The feature is implemented as an additive architecture extension: `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`, `OpenClaw.MailBridge.Contracts`, existing scripts, and the existing named-pipe contract continue to work without breaking changes.
- [x] A Windows-side `OpenClaw.HostAdapter` is introduced as the only network seam for the container, and it maps exactly to the current six read-only bridge/client operations (`status`, recent messages, message by ID, recent meeting requests, calendar window, event by ID) using the existing CLI client before any direct pipe integration is considered.
- [x] A containerized `OpenClaw.Core` is introduced for the pre-MVP, with local-only published ports, non-root execution, read-only root filesystem, SQLite persistence, health endpoints, and UI/API behavior that surfaces bridge freshness and redaction state.
- [x] The implementation preserves current privacy and safety behavior: safe mode remains the default, redacted fields stay redacted when required, and no token values, message bodies, or attendee details are logged.
- [x] Prototype assets from `artifacts\gpt-web-dev\openclaw-pre-mvp-docker-bundle` are normalized to current repo reality before use, including project names, solution/workspace references, framework targets, docs, scripts, and C# test framework conventions.
- [x] Docker, devcontainer, and deployment assets are merged carefully with existing repository files rather than overwriting them, and they remain consistent with the current solution structure and operational guidance.
- [x] New automated tests cover HostAdapter contract/error behavior and Core polling/cache behavior, while existing bridge tests and current repository quality gates continue to pass without regression.
- [x] Invalid and boundary inputs are handled deterministically: missing or invalid bearer tokens return `401`, malformed or non-UTC timestamps return `400`, `end <= start` returns `400`, missing items return `404`, and `limit` never exceeds `250` at the adapter boundary.
- [x] Degraded bridge reads remain explicit rather than hidden: when the bridge serves cached data in a degraded state, HostAdapter responses still return `200` with freshness metadata, and Core surfaces stale-cache warnings through health/status APIs and the UI.


## Non-Goals

- Sending mail, replying to mail, accepting or declining meetings, writing calendar changes, or exposing any other Outlook-mutating action.
- Direct container access to the Windows named pipe, Outlook COM APIs, or a generic pass-through RPC surface that bypasses the current bridge/client boundary.
- Replacing, renaming, or replatforming the existing `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`, or named-pipe transport as part of the pre-MVP path.
- Internet-facing deployment, multi-user tenancy, cloud synchronization, or any network exposure broader than the documented local Docker Desktop plus Windows-host path.
- Attachment extraction, arbitrary search DSLs, vector databases, Redis, or a broader microservice decomposition beyond one Windows HostAdapter and one containerized Core app.
