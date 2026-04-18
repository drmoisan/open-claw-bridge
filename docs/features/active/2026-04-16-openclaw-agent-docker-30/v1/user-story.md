# `2026-04-16-openclaw-agent-docker` — User Story

- Issue: #30
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-16T00-10

## Story Statement

- As an operator running the OpenClaw Docker deployment, I want an AI assistant service that can triage, summarize, and analyze my mail and calendar data through the existing HostAdapter API, so that I can act on high-priority items without manually reviewing each message and event.
- As a MailBridge administrator, I want the new assistant service to authenticate via the existing bearer-token file and operate in read-only mode, so that no new credential management or write-path risk is introduced into the deployment.

## Problem / Why

The repository has a complete Docker deployment for `OpenClaw.Core` and an authenticated HTTP bridge (`OpenClaw.HostAdapter`) exposing read-only mail and calendar data. However, there is no AI assistant or agent that can triage, summarize, and analyze that data on behalf of the operator. The external OpenClaw platform provides this capability but requires Docker-native integration rather than the Windows-native `exec` approach described in the original prompt. Adding a Dockerized assistant runtime beside the existing `openclaw-core` service would close this gap without altering the validated Windows-host topology.


## Personas & Scenarios

- Persona: Operator / Developer
  - Runs the Docker Desktop deployment of `OpenClaw.Core` and the new assistant service on a local workstation.
  - Cares about actionable visibility into mail and calendar data without context-switching to Outlook or manually browsing the MailBridge cache.
  - Constrained by a read-only HostAdapter API; cannot modify, reply to, or delete mail and calendar items through the assistant.
  - Goals: quickly surface high-priority messages, summarize meeting requests, and identify scheduling conflicts. Frustrated by the current gap between raw MailBridge data and triage decisions.
  - Context: typically interacts with the assistant during daily standup prep or end-of-day triage. Expects the assistant to produce summaries and flag anomalies, not take autonomous action.

- Persona: MailBridge Administrator
  - Maintains the Windows-side `OpenClaw.HostAdapter` and the bearer-token file that gates container access to mail and calendar data.
  - Cares about security posture: loopback-only network exposure, read-only token mounts, no credential embedding in images or environment variables, and non-root container execution.
  - Constrained by organizational security policy; must validate that the new service does not widen the attack surface or introduce write-path risk.
  - Goals: approve the new service for deployment without requiring changes to the existing `HostAdapter` or `MailBridge` configuration. Frustrated when new services introduce new credential-management requirements.
  - Context: reviews the Docker Compose definitions and security configuration before the operator deploys updates.

- Scenario: Daily mail triage via the assistant
  - The operator starts the Docker Compose stack with `docker compose up`, bringing both `openclaw-core` and the new assistant service to healthy status.
  - The operator opens the assistant UI or CLI and asks for a summary of messages received in the last 24 hours.
  - The assistant authenticates to `OpenClaw.HostAdapter` using the mounted bearer-token file at `/run/openclaw/hostadapter.token`.
  - The assistant issues `GET /v1/messages?since=<24h-ago>&limit=100` to the HostAdapter via `http://host.docker.internal:4319/v1`.
  - The assistant receives the message list, applies triage logic (sender importance, subject keywords, meeting-request presence), and presents a prioritized summary to the operator.
  - If the operator asks for detail on a specific message, the assistant issues `GET /v1/messages/{bridgeId}` and returns the full content, respecting redaction caveats.
  - The operator decides to manually reply to two flagged messages using Outlook; the assistant does not claim to write or send on the operator's behalf.

- Scenario: Administrator validates security posture of the new service
  - The MailBridge administrator reviews the updated `docker-compose.yml` before approving deployment.
  - They confirm the new service uses `127.0.0.1` for port publishing, runs as a non-root user, specifies `read_only: true` for the root filesystem, drops all capabilities, and mounts the token file as read-only.
  - They verify that the new service reaches `HostAdapter` through `host.docker.internal` and does not expose any new network paths.
  - They confirm the assistant's system instructions enforce read-only behavior and no-write claims.


## Acceptance Criteria

- [x] `docker-compose.yml` defines a new service (distinct from `openclaw-core`) for the external OpenClaw assistant runtime
- [x] `docker-compose.dev.yml` includes a corresponding dev-mode service definition for the assistant
- [x] `.env.example` documents new configuration keys required by the assistant service (image, port, workspace path)
- [x] The new service mounts the HostAdapter token file read-only at `/run/openclaw/hostadapter.token`
- [x] The new service uses `host.docker.internal` to reach `HostAdapter` on the Windows host
- [x] The new service follows the existing Docker security posture: loopback-only port publishing, non-root user, read-only root filesystem
- [x] Assistant tool/skill definitions use HTTP calls to HostAdapter endpoints instead of CLI `exec` against `OpenClaw.MailBridge.Client.exe`
- [x] Existing `openclaw-core` service definition and its functionality remain unchanged after the addition
- [x] Documentation (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`) updated to reflect the new service and its relationship to the existing topology
- [x] The assistant's system instructions enforce read-only behavior, no-write claims, and redaction awareness


## Non-Goals

- **No C# code changes.** This feature is limited to Docker Compose definitions, configuration files, assistant instructions, and documentation. No modifications to `OpenClaw.Core`, `OpenClaw.HostAdapter`, or any other C# project.
- **No write operations.** The assistant operates read-only against the HostAdapter API. Sending replies, modifying calendar events, deleting messages, or any other mutation is explicitly out of scope.
- **No replacement of `openclaw-core`.** The new assistant service is additive; it does not replace or modify the existing `openclaw-core` Docker service.
- **No direct access to named pipes or Windows executables.** The assistant must not call `OpenClaw.MailBridge.Client.exe` directly or access the named pipe. All data access flows through `OpenClaw.HostAdapter` HTTP endpoints.
- **No headless Docker Engine or remote Docker host support.** The feature targets Docker Desktop with `host.docker.internal` networking only.
- **No external OpenClaw platform onboarding automation.** Exact `openclaw onboard`, `openclaw agents add`, and related CLI commands are unverified and are not delivered as part of this feature. Placeholder configuration is provided instead.
