# Requirements Read Digest

Timestamp: 2026-04-12T17:20:00

## Issue Findings

- The feature must remain additive and must not break `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`, `OpenClaw.MailBridge.Contracts`, existing scripts, or the named-pipe contract.
- `OpenClaw.HostAdapter` is the only permitted network seam for the container and must map exactly to the six existing read-only CLI operations.
- `OpenClaw.Core` must remain local-only, read-only, SQLite-backed, and explicit about freshness and redaction state.
- Prototype assets are inputs only; they must be normalized to current repo names, .NET 10 targets, and MSTest plus FluentAssertions.

## Spec Findings

- `OpenClaw.MailBridge.Contracts` should remain the canonical DTO and error-code source, with HostAdapter-specific HTTP envelope types layered separately.
- HostAdapter must enforce bearer-token authentication, UTC-only request validation, `limit` defaults and bounds, deterministic HTTP mappings, and a 5-second status cache.
- Core must poll HostAdapter sequentially, persist cached records in SQLite, expose health/status and cached read APIs, and render a local UI with stale/redacted indicators.
- Docker and devcontainer assets must preserve localhost-only exposure, `host.docker.internal` connectivity, non-root execution, and read-only root filesystem expectations.

## User Story Findings

- Operators need the host/container split without disrupting the current Windows bridge workflow.
- Local users need a stable cached UI/API experience that is explicit about freshness and redaction.
- Invalid or boundary inputs must fail deterministically at the HostAdapter boundary.
- Degraded bridge reads must stay visible through metadata, status endpoints, and UI indicators rather than being hidden.

## Research Findings

- The prototype bundle is architecturally useful but mechanically mismatched: it still uses older naming, .NET 8 targets, and xUnit placeholders.
- The current repo already has the exact six CLI operations needed, and `OpenClaw.MailBridge.Client` is the lowest-risk pre-MVP integration seam.
- `OpenClaw.MailBridge.Contracts` appears portable enough to retarget to `net10.0`, which avoids duplicate DTO definitions across Windows and Linux-targeted projects.
- Docker Desktop guidance supports `host.docker.internal` for container-to-host access and `127.0.0.1` binding for local-only exposure.

## Planned Non-Scope Items

- Outlook-mutating operations such as send, reply, accept, decline, or arbitrary pass-through RPC.
- Direct container access to Outlook, COM APIs, or the Windows named pipe.
- Internet-facing deployment, multi-user tenancy, or broader microservice decomposition.
- PowerShell or GitHub Actions changes unless implementation scope later proves they are required and the plan is formally amended first.
