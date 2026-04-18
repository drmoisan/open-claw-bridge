# 2026-04-16-openclaw-agent-docker - Plan

- **Issue:** #30
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-16T00-10
- **Status:** Draft
- **Version:** 0.2

## Required References

- General Coding Standards: [`.github/instructions/general-code-change.instructions.md`](../../../../.github/instructions/general-code-change.instructions.md)
- General Unit Test Policy: [`.github/instructions/general-unit-test.instructions.md`](../../../../.github/instructions/general-unit-test.instructions.md)
- GitHub Actions: [`.github/instructions/github-actions.instructions.md`](../../../../.github/instructions/github-actions.instructions.md)
- Tonality: [`.github/instructions/tonality.instructions.md`](../../../../.github/instructions/tonality.instructions.md)

**All work must comply with these policies; do not duplicate their content here.**

## Overview

Add an external OpenClaw AI assistant runtime as a separate Docker Compose service (`openclaw-agent`) beside the existing `openclaw-core` service. The assistant consumes the Windows `OpenClaw.HostAdapter` HTTP API via `host.docker.internal` with bearer-token authentication. This feature involves Docker Compose definitions, environment configuration, assistant instruction/tool files, and documentation updates. No C#, PowerShell, or Python source code changes are required.

## Coverage Evidence Contract

Not applicable. This feature changes only YAML, Markdown, and dotenv configuration files. No compiled or interpreted source code is added or modified, so language-specific toolchain baselines, coverage capture, and coverage delta gates do not apply.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Context & Inputs

- [x] [P0-T1] Read repo policies in required order: `.github/copilot-instructions.md`, `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/github-actions.instructions.md`
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/baseline/phase0-instructions-read.md` exists with `Timestamp:`, `Policy Order:`, and explicit list of files read

- [x] [P0-T2] Read feature context files: `docs/features/active/2026-04-16-openclaw-agent-docker-30/issue.md`, `spec.md`, `user-story.md`, and `artifacts/research/20260415-openclaw-agent-docker-integration-research.md`
  - Acceptance: Executor confirms all four files read; key constraints documented: no C# changes, HostAdapter HTTP-only seam, existing Docker security posture preserved, external docs unreachable so image name is placeholder

- [x] [P0-T3] Capture baseline `docker compose config` output for `docker-compose.yml`
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/baseline/baseline-compose-config.md` with `Timestamp:`, `Command: docker compose --env-file .env.example -f docker-compose.yml config`, `EXIT_CODE:`, `Output Summary:`

- [x] [P0-T4] Capture baseline `docker compose config` output for combined dev compose
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/baseline/baseline-compose-dev-config.md` with `Timestamp:`, `Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config`, `EXIT_CODE:`, `Output Summary:`

- [x] [P0-T5] Snapshot the full `openclaw-core` service block from `docker-compose.yml` for regression comparison
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/baseline/baseline-openclaw-core-service.md` with `Timestamp:` and the complete `openclaw-core` service YAML as-is (lines 3–51 of `docker-compose.yml` at time of capture)

### Phase 1 — Environment Configuration

- [x] [P1-T1] Append three new environment variables to `.env.example` with documentation comments
  - Preconditions: Baseline captured (P0-T3)
  - Acceptance: `.env.example` contains the following new keys below a new `# OpenClaw Agent (external assistant runtime)` comment block: `OPENCLAW_AGENT_IMAGE=<placeholder — verify at docs.openclaw.ai before use>`, `OPENCLAW_AGENT_PORT=8181`, `OPENCLAW_AGENT_WORKSPACE=./deploy/docker/openclaw-assistant`; all existing keys remain unchanged and in their original order

### Phase 2 — Docker Compose Service Definitions

- [x] [P2-T1] Add `openclaw-agent` service definition to `docker-compose.yml`
  - Preconditions: `.env.example` updated (P1-T1)
  - Acceptance: `docker-compose.yml` contains a new `openclaw-agent` service with all of the following properties:
    - `image: ${OPENCLAW_AGENT_IMAGE}` (variable-driven, no hardcoded image name)
    - `container_name: openclaw-agent`
    - `init: true`
    - `restart: unless-stopped`
    - `user:` set to a non-root UID (e.g., `"1654:1654"`)
    - `read_only: true`
    - `cap_drop: [ALL]`
    - `security_opt: [no-new-privileges:true]`
    - `tmpfs: ["/tmp:size=64m,noexec,nosuid,nodev"]`
    - `ports: ["127.0.0.1:${OPENCLAW_AGENT_PORT:-8181}:8181"]` (loopback only)
    - Token file bind mount: source `${HOSTADAPTER_TOKEN_FILE}`, target `/run/openclaw/hostadapter.token`, `read_only: true`
    - Workspace bind mount: source `${OPENCLAW_AGENT_WORKSPACE:-./deploy/docker/openclaw-assistant}`, target `/workspace`
    - Environment: `OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}`, `OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token`
    - The existing `openclaw-core` service block and `volumes:` section are unchanged

- [x] [P2-T2] Add dev-mode `openclaw-agent` service definition to `docker-compose.dev.yml`
  - Preconditions: Production compose updated (P2-T1)
  - Acceptance: `docker-compose.dev.yml` contains an `openclaw-agent` service with:
    - `extra_hosts: ["host.docker.internal:host-gateway"]` for Docker Desktop host resolution
    - Token file bind mount: source `${HOSTADAPTER_TOKEN_FILE}`, target `/run/openclaw/hostadapter.token`, `read_only: true`
    - Workspace bind mount: source `${OPENCLAW_AGENT_WORKSPACE:-./deploy/docker/openclaw-assistant}`, target `/workspace`
    - Port: `"127.0.0.1:${OPENCLAW_AGENT_PORT:-8181}:8181"`
    - The existing `openclaw-dev` service, its volumes, and its configuration are unchanged

- [x] [P2-T3] Verify `openclaw-core` service definition in `docker-compose.yml` remains unchanged after P2-T1
  - Preconditions: P2-T1 complete
  - Acceptance: Line-by-line comparison of the `openclaw-core` service block against the baseline snapshot (P0-T5) shows zero semantic changes

### Phase 3 — Assistant Instruction & Configuration Files

- [x] [P3-T1] Create `deploy/docker/openclaw-assistant/TOOLS.md` with HTTP-based tool definitions for all six HostAdapter endpoints
  - Acceptance: File exists at `deploy/docker/openclaw-assistant/TOOLS.md` and defines a tool section for each of the following endpoints:
    - `GET /v1/status` — health check; no query parameters
    - `GET /v1/messages?since=<utc>&limit=<n>` — list recent messages
    - `GET /v1/messages/{bridgeId}` — retrieve single message by bridge ID
    - `GET /v1/meeting-requests?since=<utc>&limit=<n>` — list recent meeting requests
    - `GET /v1/calendar?start=<utc>&end=<utc>&limit=<n>` — list calendar events in window
    - `GET /v1/events/{bridgeId}` — retrieve single calendar event by bridge ID
    - Each tool specifies: HTTP method, full URL pattern using `http://host.docker.internal:4319/v1`, required `Authorization: Bearer <token>` header read from `/run/openclaw/hostadapter.token`, query parameter constraints (ISO-8601 UTC dates, positive integer limits), and expected response shape
    - No references to CLI `exec`, `OpenClaw.MailBridge.Client.exe`, or named-pipe access

- [x] [P3-T2] Create `deploy/docker/openclaw-assistant/SYSTEM.md` with system instructions enforcing safe, read-only assistant behavior
  - Acceptance: File exists at `deploy/docker/openclaw-assistant/SYSTEM.md` and explicitly documents all five behavioral constraints:
    1. Read-only operation — the assistant retrieves data only; it does not write, send, reply, modify, or delete any mail or calendar item
    2. No-write claims — the assistant never states or implies that it has written, sent, or modified data
    3. Redaction awareness — when the bridge operates in `safe` mode, certain fields (sender details, body previews) are redacted; the assistant surfaces this to the operator and does not hallucinate redacted content
    4. Human-approval gating — any action beyond triage, summarization, and scheduling analysis requires explicit operator approval before proceeding
    5. Safe-mode-first — the assistant assumes `safe` mode unless the operator explicitly confirms `enhanced` mode is active

- [x] [P3-T3] Create `deploy/docker/openclaw-assistant/config.yaml` as a placeholder configuration template for the external OpenClaw runtime
  - Acceptance: File exists at `deploy/docker/openclaw-assistant/config.yaml` with:
    - A header comment block stating that the exact schema must be verified against official documentation at `docs.openclaw.ai` before production use
    - Placeholder keys for: assistant identity/name, HostAdapter base URL, token file path, workspace path, and enabled tool/skill references
    - All values are clearly marked as placeholders (not production-ready defaults)

### Phase 4 — Documentation Updates

- [x] [P4-T1] Update `README.md` to document the `openclaw-agent` service in the existing Docker deployment section
  - Acceptance: The "Optional HostAdapter And Docker Core Path" section (or a new subsection within it) includes:
    - Description of `openclaw-agent` as an external OpenClaw assistant runtime that sits beside `openclaw-core`
    - Clear naming distinction: `OpenClaw.Core` is the repo-owned UI/cache container; `openclaw-agent` is the external assistant runtime
    - Operator commands: `docker compose up` for the full stack, `docker compose ps openclaw-agent`, `docker compose logs openclaw-agent`, `docker compose stop openclaw-agent`
    - Validation command for assistant-to-HostAdapter connectivity via `curl`
    - Note that `OPENCLAW_AGENT_IMAGE` must be set in `.env` before use

- [x] [P4-T2] Update `docs/architecture-diagrams.md` Section 0 Mermaid diagram to include `openclaw-agent`
  - Acceptance: The "Additive Deployment Topology" diagram contains:
    - A new `Agent` node in the `DockerDesktop` subgraph labeled `openclaw-agent` with port `127.0.0.1:8181`
    - An edge from `Agent` to `HostAdapter` via `host.docker.internal`
    - A `Browser` edge to `Agent` for operator access on loopback
    - The `TokenFile`/`TokenMount` connection extended to the `Agent` node
    - All existing diagram nodes and edges preserved unchanged
    - Accompanying text updated to mention both container services

- [x] [P4-T3] Add an operational section to `docs/mailbridge-runbook.md` for the assistant service
  - Acceptance: `docs/mailbridge-runbook.md` contains a new section (e.g., "Optional OpenClaw Assistant Service") covering:
    - Prerequisites: Docker Desktop, working HostAdapter with token file, `OPENCLAW_AGENT_IMAGE` configured in `.env`
    - Start/stop: `docker compose up -d openclaw-agent`, `docker compose stop openclaw-agent`
    - Connectivity verification: `curl` command from the host to the assistant port, and from-container `curl` to HostAdapter status endpoint
    - Troubleshooting: token authentication failures (401), container startup failures, `host.docker.internal` resolution failures
    - Independence: stopping `openclaw-agent` does not affect `openclaw-core` and vice versa

### Phase 5 — QA Validation

- [x] [P5-T1] Run `docker compose --env-file .env.example -f docker-compose.yml config` and verify clean output with both services
  - Acceptance: Command exits with code 0; parsed output contains both `openclaw-core` and `openclaw-agent` service definitions; evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/qa-gates/qa-compose-config.md` with `Timestamp:`, `Command: docker compose --env-file .env.example -f docker-compose.yml config`, `EXIT_CODE: 0`, `Output Summary:`

- [x] [P5-T2] Run `docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config` and verify clean dev-compose output
  - Acceptance: Command exits with code 0; parsed output contains both services with dev overrides applied (including `extra_hosts` on `openclaw-agent`); evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/qa-gates/qa-compose-dev-config.md` with `Timestamp:`, `Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config`, `EXIT_CODE: 0`, `Output Summary:`

- [x] [P5-T3] Verify `openclaw-agent` security posture in parsed compose config output
  - Preconditions: P5-T1 output available
  - Acceptance: Parsed config confirms all security properties: `read_only: true`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`, non-root `user` value, port published as `127.0.0.1:<port>:8181` (loopback only), token file mount at `/run/openclaw/hostadapter.token` with `read_only: true`; evidence recorded inline in `qa-compose-config.md` (P5-T1 artifact)

- [x] [P5-T4] Verify `openclaw-core` service definition is unchanged from baseline
  - Acceptance: Diff between baseline snapshot (P0-T5 artifact) and current `openclaw-core` block from `docker compose config` output shows zero semantic changes; evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/qa-gates/qa-core-regression.md` with `Timestamp:` and diff output or explicit "identical" confirmation

- [x] [P5-T5] Verify all ten acceptance criteria from `issue.md` are satisfied
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/qa-gates/qa-acceptance-criteria.md` listing each AC from the issue's "Acceptance Criteria" section with PASS/FAIL status and the specific file + evidence supporting the determination:
    1. `docker-compose.yml` defines `openclaw-agent` service distinct from `openclaw-core`
    2. `docker-compose.dev.yml` includes dev-mode `openclaw-agent` definition
    3. `.env.example` documents `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, `OPENCLAW_AGENT_WORKSPACE`
    4. Token file mounted read-only at `/run/openclaw/hostadapter.token`
    5. Service uses `host.docker.internal` to reach HostAdapter
    6. Security posture: loopback-only ports, non-root user, read-only root FS, cap_drop ALL
    7. Tool definitions use HTTP calls, not CLI exec
    8. `openclaw-core` unchanged
    9. Documentation updated (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`)
    10. System instructions enforce read-only behavior, no-write claims, redaction awareness

- [x] [P5-T6] Verify documentation completeness across all three updated doc files
  - Acceptance: Each of `README.md`, `docs/architecture-diagrams.md`, and `docs/mailbridge-runbook.md` references the `openclaw-agent` service; the naming distinction between the repo's `OpenClaw.Core` application and the external OpenClaw assistant runtime is documented in at least one location; no broken internal links introduced

## Test Plan

- **Compose YAML validation**: `docker compose config` on production and combined dev compose files validates syntax and variable resolution (P5-T1, P5-T2)
- **Security posture verification**: Parsed config output confirms read-only FS, non-root user, loopback-only ports, cap_drop ALL, no-new-privileges, read-only token mount (P5-T3)
- **Regression**: `openclaw-core` service definition unchanged from baseline (P5-T4)
- **Acceptance criteria**: All ten ACs from `issue.md` checked and evidenced (P5-T5)
- **Documentation**: Three updated doc files reference the new service and the naming distinction is documented (P5-T6)
- **Unit/integration tests**: Not applicable — this feature does not add or modify C#, PowerShell, or Python source code
- **Live stack validation**: Out of scope for this plan; requires a running HostAdapter and the verified external OpenClaw image, both of which are environment-dependent prerequisites

## Open Questions / Notes

- The exact external OpenClaw assistant Docker image name is unverified because `docs.openclaw.ai` was unreachable during research. The `OPENCLAW_AGENT_IMAGE` value in `.env.example` and `docker-compose.yml` uses a placeholder variable that the operator must populate after verifying official documentation.
- The `config.yaml` schema is a best-effort placeholder based on research findings. The actual configuration format and required fields must be validated against official platform documentation before production use.
- The `openclaw-agent` service definition omits a `healthcheck:` block because the external image's health endpoint is unverified. A healthcheck should be added once the image documentation confirms the endpoint path and expected response.
- The non-root user UID in the compose definition (`1654:1654`) matches the existing `openclaw-core` convention. If the external image requires a different UID, the value must be adjusted after image verification.
- No GitHub Actions workflow changes are required for this feature. If CI validation of the compose files is desired in the future, that would be a separate follow-up.
