# openclaw-agent-docker (Potential)

- Date captured: 2026-04-16
- Author: drmoisan
- Status: Draft

## Problem / Why

The repository has a complete Docker deployment for `OpenClaw.Core` and an authenticated HTTP bridge (`OpenClaw.HostAdapter`) exposing read-only mail and calendar data. However, there is no AI assistant or agent that can triage, summarize, and analyze that data on behalf of the operator. The external OpenClaw platform provides this capability but requires Docker-native integration rather than the Windows-native `exec` approach described in the original prompt. Adding a Dockerized assistant runtime beside the existing `openclaw-core` service would close this gap without altering the validated Windows-host topology.

## Proposed Behavior

Add a new Docker Compose service for the external OpenClaw assistant runtime. This service sits beside `openclaw-core` as a separate consumer of the Windows `OpenClaw.HostAdapter` HTTP API (`http://host.docker.internal:4319/v1`). It authenticates using the same bearer-token file (bind-mounted read-only) and operates in read-only mode with safe-mode-first behavior.

Tool and skill definitions for the assistant translate the original CLI command examples into authenticated HTTP calls against the six HostAdapter endpoints (`GET /v1/status`, `/v1/messages`, `/v1/messages/{bridgeId}`, `/v1/meeting-requests`, `/v1/calendar`, `/v1/events/{bridgeId}`). The assistant's instructions enforce read-only behavior, no-write claims, redaction awareness, and explicit human-approval gating for any action beyond triage and summarization.

The existing `openclaw-core` service, `HostAdapter`, and the Windows-host `MailBridge` remain unchanged.

## Acceptance Criteria (early draft)

- [ ] `docker-compose.yml` defines a new service (distinct from `openclaw-core`) for the external OpenClaw assistant runtime
- [ ] `docker-compose.dev.yml` includes a corresponding dev-mode service definition for the assistant
- [ ] `.env.example` documents new configuration keys required by the assistant service (image, port, workspace path)
- [ ] The new service mounts the HostAdapter token file read-only at `/run/openclaw/hostadapter.token`
- [ ] The new service uses `host.docker.internal` to reach `HostAdapter` on the Windows host
- [ ] The new service follows the existing Docker security posture: loopback-only port publishing, non-root user, read-only root filesystem
- [ ] Assistant tool/skill definitions use HTTP calls to HostAdapter endpoints instead of CLI `exec` against `OpenClaw.MailBridge.Client.exe`
- [ ] Existing `openclaw-core` service definition and its functionality remain unchanged after the addition
- [ ] Documentation (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`) updated to reflect the new service and its relationship to the existing topology
- [ ] The assistant's system instructions enforce read-only behavior, no-write claims, and redaction awareness

## Constraints & Risks

- External OpenClaw platform documentation (`docs.openclaw.ai`) was unreachable during research; the exact image name, configuration schema, onboarding CLI commands, and skill frontmatter format are unverified and must be revalidated against official docs before implementation.
- The current HostAdapter API is read-only; assistant scope is limited to triage, summarization, scheduling analysis, and draft generation. Write operations are out of scope until the API is extended.
- Docker Desktop with `host.docker.internal` networking is required; this feature does not support headless Docker Engine or remote Docker hosts.
- The token-file bind-mount approach must follow the existing security posture: read-only mount, loopback-only port exposure, and no credential embedding in environment variables or image layers.
- The naming distinction between the repo's `OpenClaw.Core` application and the external OpenClaw assistant runtime must be clearly documented to avoid operator confusion.
- Must not break existing `openclaw-core` or `HostAdapter` functionality.

## Test Conditions to Consider

- [ ] Existing `openclaw-core` container tests continue to pass after the new service is added
- [ ] New service starts successfully and reaches the HostAdapter health endpoint (`GET /v1/status`)
- [ ] Token authentication works from the new container (valid token returns 200, missing/invalid token returns 401)
- [ ] `docker compose up` brings both `openclaw-core` and the new assistant service to healthy status
- [ ] Assistant tool definitions map correctly to the six HostAdapter HTTP endpoints and produce valid responses
- [ ] Compose file validation confirms the new service maintains loopback-only publishing, read-only root filesystem, and non-root user constraints

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/openclaw-agent-docker/` folder from the template

