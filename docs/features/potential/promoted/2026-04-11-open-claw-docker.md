# open-claw-docker (Issue #23)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Promoted -> docs/features/active/open-claw-docker/ (Issue #23)

- Issue: #23
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/23
- Last Updated: 2026-04-12
## Problem / Why

OpenClaw.MailBridge is now a working Windows-local Outlook bridge with a CLI client, shared contracts, SQLite-backed cached reads, safe versus enhanced response shaping, and operator/install automation. What the repository does not yet have is a safe way to run the rest of an OpenClaw experience outside the Windows/Outlook boundary. The prototype bundle under `artifacts\gpt-web-dev\openclaw-pre-mvp-docker-bundle` defines that missing pre-MVP path: keep Outlook, `OpenClaw.MailBridge`, and `OpenClaw.MailBridge.Client` on Windows, then add a narrow Windows-side HTTP HostAdapter plus a Linux-containerized `OpenClaw.Core` app for local UI, polling, and cached read-only views.

The bundle is useful because it already contains a coherent architectural split, Docker and devcontainer assets, API contracts, deployment notes, and compile-ready project scaffolding. It is not ready to merge blindly into this repository. Several prototype assumptions diverge from the current codebase: it refers to older `EmailBridge`/`EmailClient` names instead of `OpenClaw.MailBridge`/`OpenClaw.MailBridge.Client`, targets .NET 8 while the repo currently targets `net10.0-windows` for Windows projects, and includes xUnit placeholder tests even though this repository standardizes on MSTest plus FluentAssertions for C#. This feature therefore needs to capture the prototype as a guided integration effort, not a copy operation, so the existing bridge contract, scripts, documentation, and regression coverage remain intact.

## Proposed Behavior

Incorporate the bundle as an architectural reference and starter scaffold for a new read-only OpenClaw pre-MVP deployment model. The implementation should add `OpenClaw.HostAdapter`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.Core`, container/deployment assets, and matching tests alongside the existing bridge projects without renaming, replacing, or destabilizing the current named-pipe bridge stack.

At a high level, the resulting design should work as follows:

- Windows host remains the only place that talks to Outlook and the named pipe.
- `OpenClaw.HostAdapter` exposes a tiny authenticated HTTP surface that maps directly to the existing six read-only bridge operations by shelling out to `OpenClaw.MailBridge.Client` first.
- `OpenClaw.Core` runs in a Linux container, calls only the HostAdapter over HTTP, persists a local SQLite cache, surfaces freshness and redaction status, and provides a simple local UI plus internal API.
- Docker and devcontainer assets are added in a way that is compatible with the current solution, scripts, docs, and repo policies.

The prototype material should be leveraged selectively: keep the boundary, runtime hardening posture, OpenAPI contracts, and staged implementation intent, but reconcile all paths, project names, frameworks, test libraries, docs, and operational commands with the current repository before any code is promoted into the main architecture.

## Acceptance Criteria (early draft)

- [ ] The feature is implemented as an additive architecture extension: `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`, `OpenClaw.MailBridge.Contracts`, existing scripts, and the existing named-pipe contract continue to work without breaking changes.
- [ ] A Windows-side `OpenClaw.HostAdapter` is introduced as the only network seam for the container, and it maps exactly to the current six read-only bridge/client operations (`status`, recent messages, message by ID, recent meeting requests, calendar window, event by ID) using the existing CLI client before any direct pipe integration is considered.
- [ ] A containerized `OpenClaw.Core` is introduced for the pre-MVP, with local-only published ports, non-root execution, read-only root filesystem, SQLite persistence, health endpoints, and UI/API behavior that surfaces bridge freshness and redaction state.
- [ ] The implementation preserves current privacy and safety behavior: safe mode remains the default, redacted fields stay redacted when required, and no token values, message bodies, or attendee details are logged.
- [ ] Prototype assets from `artifacts\gpt-web-dev\openclaw-pre-mvp-docker-bundle` are normalized to current repo reality before use, including project names, solution/workspace references, framework targets, docs, scripts, and C# test framework conventions.
- [ ] Docker, devcontainer, and deployment assets are merged carefully with existing repository files rather than overwriting them, and they remain consistent with the current solution structure and operational guidance.
- [ ] New automated tests cover HostAdapter contract/error behavior and Core polling/cache behavior, while existing bridge tests and current repository quality gates continue to pass without regression.

## Constraints & Risks

- The feature boundary is still pre-MVP and read-only: no Outlook writes, no send/reply/accept/decline actions, no arbitrary pass-through RPC, and no direct container access to the named pipe.
- The current repository is Windows-bridge-first and already has working contracts, scripts, and acceptance evidence; integration work must preserve those established behaviors and avoid accidental renames or transport changes.
- The prototype bundle is partly scaffold-level material rather than production-ready code. Several files are placeholders, including `Program.cs` stubs and placeholder tests, so the bundle should be treated as early design evidence and scaffolding rather than proof of a completed implementation.
- The prototype's assumptions do not fully match this repo today: older project names, .NET/runtime differences, and xUnit placeholders all create compatibility risk if copied without adaptation.
- The host/container split adds new operational risk around token handling, host-to-container connectivity, Docker Desktop networking, Windows Firewall scope, health checks, and preserving sequential bridge access guidance.
- There is a regression risk if the HostAdapter reinterprets DTOs or error behavior instead of preserving the current bridge/client contract semantics and safe-mode data minimization rules.

## Test Conditions to Consider

- [ ] Unit coverage for HostAdapter token validation, UTC validation, limit handling, error mapping from `OpenClaw.MailBridge.Client` exit codes/results, response envelope consistency, and Core cache/poller persistence behavior
- [ ] Integration scenarios covering Windows HostAdapter to current CLI client wiring, container-to-host HTTP access through the expected Docker Desktop path, SQLite-backed stale-cache behavior, local-only port exposure, and preservation of sequential read access patterns
- [ ] CLI/API examples for HostAdapter status and read endpoints, Core health endpoints and cached-data APIs, Docker/devcontainer startup flows, and operator troubleshooting paths for missing token files, unavailable Outlook, stale bridge cache, and degraded readiness

## Next Step

- [ ] Promote this into a GitHub feature issue that treats the bundle as prototype input and defines a compatibility-first integration plan for the existing `OpenClaw.MailBridge` architecture
- [ ] Create `docs/features/active/open-claw-docker/` from the template and break the work into discrete phases: repo reconciliation, HostAdapter implementation, Core implementation, Docker/devcontainer integration, and regression/hardening validation
