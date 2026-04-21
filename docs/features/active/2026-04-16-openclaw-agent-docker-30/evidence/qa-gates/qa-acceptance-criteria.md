# QA Gate — Acceptance Criteria Verification

Timestamp: 2026-04-16T00-23

## Acceptance Criteria from `issue.md`

| # | Criterion | Status | Evidence |
|---|---|---|---|
| 1 | `docker-compose.yml` defines a new service (distinct from `openclaw-core`) for the external OpenClaw assistant runtime | PASS | `docker-compose.yml` lines 56–82: `openclaw-agent` service defined separately from `openclaw-core` |
| 2 | `docker-compose.dev.yml` includes a corresponding dev-mode service definition for the assistant | PASS | `docker-compose.dev.yml` lines 29–42: `openclaw-agent` dev override with `extra_hosts` |
| 3 | `.env.example` documents new configuration keys required by the assistant service (image, port, workspace path) | PASS | `.env.example` lines 17–20: `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, `OPENCLAW_AGENT_WORKSPACE` |
| 4 | The new service mounts the HostAdapter token file read-only at `/run/openclaw/hostadapter.token` | PASS | `docker-compose.yml` agent volumes section: bind mount with `read_only: true` at target `/run/openclaw/hostadapter.token`; confirmed in compose config output |
| 5 | The new service uses `host.docker.internal` to reach `HostAdapter` on the Windows host | PASS | `docker-compose.yml` agent environment: `OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}` |
| 6 | The new service follows the existing Docker security posture: loopback-only port publishing, non-root user, read-only root filesystem | PASS | Compose config confirms: `127.0.0.1:8181:8181`, `user: 1654:1654`, `read_only: true`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]` — see `qa-compose-config.md` |
| 7 | Assistant tool/skill definitions use HTTP calls to HostAdapter endpoints instead of CLI `exec` against `OpenClaw.MailBridge.Client.exe` | PASS | `deploy/docker/openclaw-assistant/TOOLS.md`: all six tools use `http://host.docker.internal:4319/v1/*` HTTP endpoints; no references to CLI exec or `OpenClaw.MailBridge.Client.exe` |
| 8 | Existing `openclaw-core` service definition and its functionality remain unchanged after the addition | PASS | `qa-core-regression.md`: line-by-line comparison shows zero semantic changes from baseline |
| 9 | Documentation (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`) updated to reflect the new service and its relationship to the existing topology | PASS | `README.md`: new subsection "4. Optional: Start the OpenClaw Assistant Service"; `docs/architecture-diagrams.md`: Agent node added to Section 0 Mermaid diagram; `docs/mailbridge-runbook.md`: new "Optional OpenClaw Assistant Service" section |
| 10 | The assistant's system instructions enforce read-only behavior, no-write claims, and redaction awareness | PASS | `deploy/docker/openclaw-assistant/SYSTEM.md`: five behavioral constraints documented (read-only operation, no-write claims, redaction awareness, human-approval gating, safe-mode-first) |

## Summary

- Total ACs: 10
- PASS: 10
- FAIL: 0
