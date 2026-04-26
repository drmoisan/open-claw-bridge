# 2026-04-16-openclaw-agent-docker — Spec

- **Issue:** #30
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-16T00-10
- **Status:** Draft
- **Version:** 0.1

## Overview

This feature adds an external OpenClaw AI assistant runtime as a new Docker Compose service alongside the existing `openclaw-core` service. The assistant consumes the Windows `OpenClaw.HostAdapter` HTTP API (`http://host.docker.internal:4319/v1`) using bearer-token authentication via a read-only bind-mounted token file. The scope is limited to Docker Compose definitions, environment configuration, assistant tool/skill instruction files, and documentation updates. No C# code changes are required.


## Behavior

The feature introduces the following end-to-end flow:

**Service startup.** When the operator runs `docker compose up`, the new assistant service starts alongside `openclaw-core`. Both services independently resolve `host.docker.internal` to reach the Windows-host `OpenClaw.HostAdapter` on port 4319. The assistant reads its bearer token from the bind-mounted file at `/run/openclaw/hostadapter.token`.

**Data access.** The assistant issues authenticated HTTP GET requests to the six HostAdapter endpoints to retrieve mail and calendar data:

| Endpoint | Purpose |
|---|---|
| `GET /v1/status` | Health check and bridge status |
| `GET /v1/messages?since=<utc>&limit=<n>` | List recent messages |
| `GET /v1/messages/{bridgeId}` | Retrieve a single message by bridge ID |
| `GET /v1/meeting-requests?since=<utc>&limit=<n>` | List recent meeting requests |
| `GET /v1/calendar?start=<utc>&end=<utc>&limit=<n>` | List calendar events in a time window |
| `GET /v1/events/{bridgeId}` | Retrieve a single calendar event by bridge ID |

Each request includes an `Authorization: Bearer <token>` header. The HostAdapter validates the token and returns normalized JSON responses or a `401` error for invalid/missing tokens.

**Assistant behavior.** The assistant runs as the OpenClaw agent identity `admin-assistant`. It operates in read-only, safe-mode-first mode. It can triage, summarize, analyze scheduling conflicts, and generate draft responses. It does not claim to write, send, reply, or modify any mail or calendar data. It is redaction-aware and surfaces unknowns and human-approval points explicitly.

**Session-start protocol.** On each new session the assistant must:

1. Read `SOUL.md`, `USER.md`, and `TOOLS.md` from its workspace.
2. Call `GET /v1/status` and stop/report if the bridge is not ready.
3. Pull a baseline window: meeting requests from the last 7 days, recent messages from the last 24 hours, and calendar events for the next 14 days.
4. Expand individual items (`GET /v1/messages/{bridgeId}`, `GET /v1/events/{bridgeId}`) only for entries that warrant it.

**Decision labels.** When triaging items the assistant uses one of: `IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`. `AUTO_COORDINATE` means "safe to recommend a coordination action" — never permission to send or reschedule, since the HostAdapter API is read-only.

**Output format.** Every triage or summary response is structured as: (1) Executive summary, (2) Items needing action, (3) Proposed drafts / next steps, (4) Unknowns / missing data.

**Safe vs enhanced mode.** The HostAdapter (and underlying MailBridge) expose `safe` and `enhanced` modes. In `safe` mode, sender/body/attendee/organizer details are redacted; in `enhanced` mode they are present. This deployment starts in `safe`. Switching to `enhanced` is out of scope for this feature and requires a separate operator decision once the assistant has demonstrated correct behavior.

**Tool/skill definitions.** The assistant's tool and skill instruction files define each HostAdapter endpoint as an HTTP-based tool, replacing the original CLI `exec` approach from the external OpenClaw prompt. Examples use `curl`-style HTTP calls rather than `OpenClaw.MailBridge.Client.exe` command lines. The skill is exposed as `skills/mailbridge_admin/SKILL.md` with OpenClaw-native YAML frontmatter (`name`, `description`, `metadata.openclaw.os`).

**Existing services unchanged.** The `openclaw-core` service definition, its volumes, its environment, and its healthcheck remain unmodified. The Windows-host `HostAdapter` and `MailBridge` are also unchanged.

**Dev-mode support.** `docker-compose.dev.yml` includes a corresponding dev-mode definition for the assistant service with `extra_hosts` for `host.docker.internal:host-gateway` resolution.


## Out of Scope / Future Direction

Captured up front so the day-1 scope is explicit and the longer-term shape is not lost:

- **Channel reachability.** Dedicated WhatsApp/Telegram/Discord bindings via `openclaw agents bind --agent admin-assistant --bind <channel>`, `channels.*.dmPolicy=allowlist`, `allowFrom` numbers, and `session.dmScope=per-channel-peer`. Day-1 exposure is local UI/API only.
- **Enhanced mode.** Switching the HostAdapter/MailBridge from `safe` to `enhanced` (unredacted sender/body/attendees/organizer) requires a separate operator decision once the assistant is behaving correctly.
- **Write plane.** Send/reply, accept/decline, create/update/move calendar items are out of scope until the HostAdapter surface is extended. The current `AUTO_COORDINATE` label means "recommend a coordination action," never "execute it."
- **Typed plugin tools.** The long-term end state is an OpenClaw plugin registering typed tools (`mailbridge_status`, `mailbridge_list_messages`, `mailbridge_list_calendar`, `mailbridge_get_message`, `mailbridge_get_event`) instead of shelling through an `exec` path or generic HTTP tool. Day-1 uses HTTP-based tool definitions; the plugin path is a follow-on feature.
- **Onboarding automation.** Exact `openclaw onboard`, `openclaw agents add`, `openclaw approvals allowlist add`, and `openclaw skills list` invocations are unverified against current docs and not delivered here. The placeholder `openclaw.json` is a starting point only.
- **Claude subscription reuse (Claude CLI backend).** OpenClaw does not implement OAuth against a Claude.ai Pro/Max subscription directly. Its subscription-backed path is `agents.defaults.cliBackends.claude-cli.command` pointing at a local `claude` binary that already holds the OAuth session. Using it from a container would require installing the `claude` CLI in the image, bind-mounting host Claude credentials read-only, accepting that OpenClaw drops CLI session reuse on token rotation, and a separate operator decision on Anthropic subscription terms for containerized use. Day-1 uses the Anthropic API key path.


## Inputs / Outputs

### Environment variables (new, added to `.env.example`)

| Variable | Purpose | Default / Example |
|---|---|---|
| `OPENCLAW_AGENT_IMAGE` | Docker image for the external OpenClaw assistant runtime | `ghcr.io/openclaw/openclaw:latest` (verified at docs.openclaw.ai) |
| `OPENCLAW_AGENT_PORT` | Loopback-only published host port for the assistant UI/API | `18789` |
| `OPENCLAW_AGENT_WORKSPACE` | Host-side path bind-mounted as the assistant workspace | `./deploy/docker/openclaw-assistant` |
| `ANTHROPIC_API_KEY` | Anthropic API credential for the `admin-assistant` agent | empty in `.env.example`; real value supplied via gitignored `env_file` (see Credential delivery below) |
| `OPENCLAW_AGENT_MODEL` | Optional override for `agents.defaults.model.primary` without editing `openclaw.json` | `anthropic/claude-opus-4-6` |

### Environment variables (reused from existing stack)

| Variable | Purpose | Current Default |
|---|---|---|
| `OpenClaw__HostAdapter__BaseUrl` | HostAdapter base URL for container HTTP access | `http://host.docker.internal:4319/v1` |
| `OpenClaw__HostAdapter__TokenFile` | In-container path to the bearer-token file | `/run/openclaw/hostadapter.token` |
| `HOSTADAPTER_TOKEN_FILE` | Windows-host path to the bearer-token file (bind-mount source) | `C:\ProgramData\OpenClaw\HostAdapter\adapter.token` |

### Bind mounts

| Source | Target | Mode | Purpose |
|---|---|---|---|
| `${HOSTADAPTER_TOKEN_FILE}` | `/run/openclaw/hostadapter.token` | `read_only: true` | Bearer-token for HostAdapter authentication |
| `${OPENCLAW_AGENT_WORKSPACE}` | `/workspace` | read-write | Assistant workspace for config, instructions, and tool definitions |

### Outputs

- The assistant produces user-facing triage summaries, scheduling analyses, and draft responses via its UI/API on `127.0.0.1:${OPENCLAW_AGENT_PORT}`.
- No persistent artifacts are written outside the assistant workspace volume.
- Healthcheck status is reported via the assistant's container healthcheck (if supported by the image).

### Configuration files (new)

The assistant workspace follows OpenClaw's native workspace layout so the runtime picks up the files without adapter shims:

| File | Purpose |
|---|---|
| `.env.example` (updated) | Documents the three new environment variables above |
| `deploy/docker/openclaw-assistant/IDENTITY.md` (new) | Agent name (`admin-assistant`), role, tone |
| `deploy/docker/openclaw-assistant/SOUL.md` (new) | Priorities: avoid false claims, protect private details, surface urgent items, draft conservatively, never claim sent/modified |
| `deploy/docker/openclaw-assistant/USER.md` (new) | Operator profile and context the assistant should orient around |
| `deploy/docker/openclaw-assistant/AGENTS.md` (new) | Session-start protocol, primary jobs, decision labels, required output format |
| `deploy/docker/openclaw-assistant/TOOLS.md` (new) | HTTP-based tool definitions for the six HostAdapter endpoints |
| `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` (new) | OpenClaw skill definition with YAML frontmatter (`name`, `description`, `metadata.openclaw.os`) describing when to invoke and the required workflow |
| `deploy/docker/openclaw-assistant/openclaw.json` (placeholder, new) | Gateway/agent config shape: `gateway.mode=local`, loopback bind, token auth, `session.dmScope=per-channel-peer`, `tools.profile=minimal`, `agents.defaults.model.primary="anthropic/claude-opus-4-6"` with a Sonnet fallback, agent entry for `admin-assistant` with `skills: ["mailbridge_admin"]`. Marked placeholder pending verification against current OpenClaw docs. |
| `secrets/.env.anthropic` (new, gitignored) | Holds the real `ANTHROPIC_API_KEY` value; referenced by the `openclaw-agent` service as an `env_file`. Never committed. |
| `.gitignore` (updated) | Adds `secrets/` so the Anthropic credential file cannot be committed. |

### Credential delivery

The `admin-assistant` uses Anthropic Claude via API-key authentication — OpenClaw's documented, production-oriented path for a persistent gateway host.

- `ANTHROPIC_API_KEY` is supplied to the `openclaw-agent` container through a Docker Compose `env_file` pointed at a gitignored file (default path: `./secrets/.env.anthropic`).
- `.env.example` references `ANTHROPIC_API_KEY` with an empty value and a comment directing operators to their Anthropic console and to `docs.openclaw.ai/providers/anthropic` for the current model ID.
- The key must not appear in `docker-compose.yml`, the committed `.env`, image layers, or container logs. `secrets/` is added to `.gitignore`.
- Model selection lives in `agents.defaults.model.*` inside the placeholder `openclaw.json`:

```json5
{
  agents: {
    defaults: {
      model: {
        primary: "anthropic/claude-opus-4-6",
        fallbacks: ["anthropic/claude-sonnet-4-6"]
      }
    }
  }
}
```

Operator reference (unverified): `openclaw onboard --anthropic-api-key "$ANTHROPIC_API_KEY"`.

### Versioning / backward compatibility

- The existing `.env.example` keys are preserved; new keys are appended.
- The existing `docker-compose.yml` service `openclaw-core` definition is unchanged.
- The new service is additive and does not affect the existing stack when disabled or removed.

## API / CLI Surface

The assistant service does not introduce new APIs into the repository. It consumes the existing HostAdapter HTTP API as a client.

### HostAdapter endpoints consumed by the assistant

All requests include `Authorization: Bearer <token>` from the mounted token file.

```
GET /v1/status
  → 200: { "status": "ok", ... }
  → 401: missing or invalid token

GET /v1/messages?since=2026-04-15T00:00:00Z&limit=50
  → 200: { "items": [ { "bridgeId": "...", "subject": "...", ... } ] }

GET /v1/messages/{bridgeId}
  → 200: { "bridgeId": "...", "subject": "...", "body": "...", ... }

GET /v1/meeting-requests?since=2026-04-15T00:00:00Z&limit=50
  → 200: { "items": [ { "bridgeId": "...", "subject": "...", ... } ] }

GET /v1/calendar?start=2026-04-01T00:00:00Z&end=2026-04-30T00:00:00Z&limit=100
  → 200: { "items": [ { "bridgeId": "...", "subject": "...", ... } ] }

GET /v1/events/{bridgeId}
  → 200: { "bridgeId": "...", "subject": "...", "start": "...", ... }
```

### Operator CLI interactions

```bash
# Start the full stack including the assistant
docker compose up -d

# Check assistant service health
docker compose ps openclaw-agent

# View assistant logs
docker compose logs openclaw-agent

# Stop only the assistant
docker compose stop openclaw-agent
```

### Contracts and validation rules

- All date/time parameters use ISO-8601 UTC format.
- The `limit` parameter must be a positive integer; the HostAdapter enforces a maximum (currently 250).
- The HostAdapter returns `401` for missing, empty, or invalid bearer tokens.
- The HostAdapter returns normalized error responses for invalid parameters or downstream CLI failures.

## Data & State

### Data flow

1. Outlook (Windows host) → `OpenClaw.MailBridge` (COM automation, SQLite cache, named-pipe server)
2. `OpenClaw.MailBridge.Client` (Windows host CLI) → `OpenClaw.HostAdapter` (Windows host HTTP bridge on `127.0.0.1:4319`)
3. `OpenClaw.HostAdapter` → Docker containers via `host.docker.internal:4319`
4. `openclaw-core` (existing) and `openclaw-agent` (new) both consume the HostAdapter HTTP API independently

### State ownership

- The assistant does not own or persist mail/calendar data. It reads from HostAdapter on demand.
- The assistant workspace volume (`/workspace`) holds configuration, tool definitions, and system instructions. This is operator-managed and not a data cache.
- `openclaw-core` continues to own its `/data/openclaw.db` SQLite cache independently.

### Caching and persistence

- The assistant may maintain in-memory conversation state for triage sessions; no durable cache is introduced.
- No migration or backfill is required. The assistant reads live data from HostAdapter on each request.

## Constraints & Risks

- External OpenClaw platform documentation (`docs.openclaw.ai`) may be partially unreachable during research; the exact image name, configuration schema, onboarding CLI commands, and skill frontmatter format are unverified and must be revalidated against official docs before implementation.
- The current HostAdapter API is read-only; assistant scope is limited to triage, summarization, scheduling analysis, and draft generation. Write operations are out of scope until the API is extended.
- Docker Desktop with `host.docker.internal` networking is required; this feature does not support headless Docker Engine or remote Docker hosts.
- The token-file bind-mount approach must follow the existing security posture: read-only mount, loopback-only port exposure, and no credential embedding in environment variables or image layers.
- The naming distinction between the repo's `OpenClaw.Core` application and the external OpenClaw assistant runtime must be clearly documented to avoid operator confusion.
- Leaking `ANTHROPIC_API_KEY` through committed files, image layers, or `docker inspect` output. Mitigation: gitignored `env_file`, no baking into image, no logging of env at startup.
- Model-name drift between the value in `openclaw.json` / `OPENCLAW_AGENT_MODEL` and what the provider currently supports. Mitigation: confirm the model ID at `docs.openclaw.ai/providers/anthropic` before setting it, and keep it in a single env-var-backed location.
- Must not break existing `openclaw-core` or `HostAdapter` functionality.


## Implementation Strategy

### Implementation scope

This feature changes only Docker Compose definitions, environment configuration, assistant instruction/tool files, and repository documentation. No C# source files are modified.

### Files to add or update

| File | Change |
|---|---|
| `docker-compose.yml` | Add `openclaw-agent` service definition |
| `docker-compose.dev.yml` | Add dev-mode `openclaw-agent` service definition with `extra_hosts` |
| `.env.example` | Append `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, `OPENCLAW_AGENT_WORKSPACE` |
| `deploy/docker/openclaw-assistant/IDENTITY.md` | New file: agent name (`admin-assistant`), role, tone |
| `deploy/docker/openclaw-assistant/SOUL.md` | New file: conservative-assistant priorities and no-write-claim contract |
| `deploy/docker/openclaw-assistant/USER.md` | New file: operator profile |
| `deploy/docker/openclaw-assistant/AGENTS.md` | New file: session-start protocol, primary jobs, decision labels (`IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`), required 4-part output format |
| `deploy/docker/openclaw-assistant/TOOLS.md` | New file: HTTP-based tool definitions for the six HostAdapter endpoints |
| `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` | New file: OpenClaw skill with YAML frontmatter and required workflow |
| `deploy/docker/openclaw-assistant/openclaw.json` | New file (placeholder): gateway+agent config shape including `agents.defaults.model.*`, flagged for verification |
| `secrets/.env.anthropic` | New file (gitignored): holds the real `ANTHROPIC_API_KEY` value; referenced by the `openclaw-agent` service as an `env_file` |
| `.gitignore` | Add `secrets/` so the Anthropic credential file cannot be committed |
| `README.md` | Update Docker deployment section to describe both services |
| `docs/architecture-diagrams.md` | Update topology diagram to show the assistant service |
| `docs/mailbridge-runbook.md` | Add operational guidance for the assistant service |

### New service definition shape

The `openclaw-agent` service in `docker-compose.yml` follows the existing `openclaw-core` security posture:

- `user`: non-root UID (e.g., `1654:1654` or equivalent for the external image)
- `read_only: true`
- `cap_drop: [ALL]`
- `security_opt: [no-new-privileges:true]`
- `ports: ["127.0.0.1:${OPENCLAW_AGENT_PORT:-8181}:8181"]` (loopback only)
- Token file bind-mounted read-only at `/run/openclaw/hostadapter.token`
- Workspace bind-mounted at `/workspace`
- Environment: `OpenClaw__HostAdapter__BaseUrl`, `OpenClaw__HostAdapter__TokenFile`, `ANTHROPIC_API_KEY`, optional `OPENCLAW_AGENT_MODEL`
- `env_file: ./secrets/.env.anthropic` (gitignored) supplies `ANTHROPIC_API_KEY`

### Architecture / service topology

```
┌─────────────────────────────────────────────────────┐
│  Windows Host                                       │
│  ┌──────────────────┐   ┌────────────────────────┐  │
│  │ OpenClaw.MailBridge│──│ OpenClaw.MailBridge     │  │
│  │ (Outlook COM,     │  │ .Client (CLI adapter)   │  │
│  │  SQLite, pipe)    │  └──────────┬─────────────┘  │
│  └──────────────────┘              │                │
│                         ┌──────────▼─────────────┐  │
│                         │ OpenClaw.HostAdapter    │  │
│                         │ 127.0.0.1:4319         │  │
│                         │ Bearer-token auth      │  │
│                         └──────────┬─────────────┘  │
│                                    │                │
│              host.docker.internal:4319              │
└────────────────────────┬───────────┬────────────────┘
                         │           │
┌────────────────────────▼───────────▼────────────────┐
│  Docker Desktop                                     │
│  ┌──────────────────┐   ┌────────────────────────┐  │
│  │ openclaw-core     │   │ openclaw-agent         │  │
│  │ (repo UI/cache)   │   │ (assistant runtime)    │  │
│  │ 127.0.0.1:8080    │   │ 127.0.0.1:8181         │  │
│  └──────────────────┘   └────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

Both Docker services independently consume the HostAdapter HTTP API. They do not communicate with each other directly.

### Dependency changes

- No new C# package dependencies.
- The external OpenClaw assistant Docker image is a runtime dependency pulled from the external platform's registry. The exact image name must be verified against official docs before implementation.

### Logging / telemetry

- The assistant service logs to stdout/stderr as per Docker convention.
- No changes to `openclaw-core` logging.

### Rollout plan

1. Verify the external OpenClaw image name and configuration schema against official documentation.
2. Add the Compose service definitions and configuration files.
3. Validate with `docker compose config` and a local `docker compose up`.
4. Update documentation.
5. The assistant service can be disabled without affecting `openclaw-core` by removing or commenting out its service block.

## Definition of Done

- [x] `docker-compose.yml` defines `openclaw-agent` service with security posture matching `openclaw-core` (loopback-only publish, non-root user, read-only root FS, cap_drop ALL)
- [x] `docker-compose.dev.yml` includes dev-mode definition for `openclaw-agent` with `extra_hosts` for `host.docker.internal`
- [x] `.env.example` documents `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, and `OPENCLAW_AGENT_WORKSPACE`
- [x] Token file bind-mounted read-only at `/run/openclaw/hostadapter.token` in the new service
- [x] `deploy/docker/openclaw-assistant/TOOLS.md` defines HTTP-based tools for all six HostAdapter endpoints
- [x] `deploy/docker/openclaw-assistant/IDENTITY.md`, `SOUL.md`, `USER.md`, and `AGENTS.md` are present and collectively enforce: read-only behavior, no-write claims, redaction awareness, the session-start protocol, and the 4-part output format
- [x] `deploy/docker/openclaw-assistant/AGENTS.md` defines the decision labels `IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE` and binds `AUTO_COORDINATE` to recommendation-only semantics
- [x] `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` exists with YAML frontmatter (`name`, `description`, `metadata.openclaw.os`) and the required workflow
- [x] `deploy/docker/openclaw-assistant/openclaw.json` placeholder exists with `gateway.mode=local`, loopback bind, token auth, `session.dmScope=per-channel-peer`, `tools.profile=minimal`, and an `admin-assistant` agent entry
- [x] The agent is named `admin-assistant` consistently across workspace files and config
- [x] `openclaw-agent` service consumes `ANTHROPIC_API_KEY` via a gitignored `env_file`; the key is not present in the committed `.env`, `.env.example`, `docker-compose.yml`, or image layers
- [x] The placeholder `openclaw.json` sets `agents.defaults.model.primary` to a valid Anthropic model ID (or wires it through `OPENCLAW_AGENT_MODEL`)
- [x] `.env.example` documents `ANTHROPIC_API_KEY` (empty) and `OPENCLAW_AGENT_MODEL` with guidance pointing operators to `docs.openclaw.ai/providers/anthropic`
- [x] `docker compose config` validates both compose files without errors
- [x] Existing `openclaw-core` service definition is byte-identical before and after the change (excluding trailing newlines or comments)
- [x] `README.md`, `docs/architecture-diagrams.md`, and `docs/mailbridge-runbook.md` updated to reflect the new service
- [x] Naming distinction between repo `OpenClaw.Core` and the external OpenClaw assistant runtime is documented

## Testing Strategy

### Compose validation

- Run `docker compose config` against both `docker-compose.yml` and `docker-compose.dev.yml` to confirm YAML validity and variable resolution.
- Validate that the new service inherits the security posture: verify `read_only`, `cap_drop`, `user`, `security_opt`, and loopback-only `ports` values in the parsed config output.

### HostAdapter connectivity

- Start the full stack with `docker compose up` and confirm both `openclaw-core` and `openclaw-agent` reach healthy status.
- From the assistant container, issue `curl http://host.docker.internal:4319/v1/status -H "Authorization: Bearer $(cat /run/openclaw/hostadapter.token)"` and verify a `200` response.

### Token authentication

- Valid token: confirm `GET /v1/status` returns `200` from the assistant container with the mounted token.
- Missing token: confirm `GET /v1/status` without the `Authorization` header returns `401`.
- Invalid token: confirm `GET /v1/status` with a fabricated token returns `401`.

### Service health

- `docker compose ps` shows both services as `running` / `healthy`.
- Stopping `openclaw-agent` does not affect `openclaw-core` health or functionality.
- Stopping `openclaw-core` does not prevent `openclaw-agent` from reaching HostAdapter.

### Tool definition validation

- Manually invoke each of the six HostAdapter endpoints from the assistant container using the tool definitions to confirm correct URL construction, header inclusion, and response parsing.

### OpenClaw runtime verification

Inside the assistant container (or via `docker compose exec openclaw-agent ...`), confirm the runtime is healthy and sees the workspace:

- `openclaw --version` returns a version string
- `openclaw doctor` reports no critical issues
- `openclaw gateway status` reports the local gateway running on the configured loopback port
- `openclaw skills list` includes `mailbridge_admin`
- `openclaw logs --follow` surfaces agent session activity when the assistant is invoked

These commands are flagged as tentative — they must match whatever the external image actually exposes. If any command name differs, record the actual invocation in `docs/mailbridge-runbook.md`.

### Assistant behavior smoke test

- Start a session and confirm the assistant performs the session-start protocol (status check, then baseline pulls for the 7d / 24h / 14d windows) before producing output.
- Confirm the assistant's response uses the 4-part output format (Executive summary / Items needing action / Proposed drafts / Unknowns).
- Confirm triage output uses the decision labels (`IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`) and never claims a write occurred.

### Existing stack regression

- After adding the new service, run the existing `openclaw-core` container tests (if applicable) to verify no regression.
- Confirm the `openclaw-core` service definition in `docker-compose.yml` is unchanged by diffing before and after.

## Seeded Test Conditions (from potential)
- [ ] Existing `openclaw-core` container tests continue to pass after the new service is added
- [ ] New service starts successfully and reaches the HostAdapter health endpoint (`GET /v1/status`)
- [ ] Token authentication works from the new container (valid token returns 200, missing/invalid token returns 401)
- [ ] `docker compose up` brings both `openclaw-core` and the new assistant service to healthy status
- [ ] Assistant tool definitions map correctly to the six HostAdapter HTTP endpoints and produce valid responses
- [ ] Compose file validation confirms the new service maintains loopback-only publishing, read-only root filesystem, and non-root user constraints
- [ ] `openclaw skills list` inside the assistant container includes `mailbridge_admin`
- [ ] The assistant performs the session-start protocol (status check, then 7d/24h/14d baseline pulls) before emitting output
- [ ] Assistant responses follow the 4-part output format (Executive summary / Items needing action / Proposed drafts / Unknowns)
- [ ] Triage output uses only the approved decision labels and never asserts a send/accept/decline/create/update occurred
