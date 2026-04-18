# QA Gate — Acceptance Criteria Verification

Timestamp: 2026-04-18T00-00

Source: `docs/features/active/2026-04-16-openclaw-agent-docker-30/v2/user-story.md ## Acceptance Criteria`

Total ACs: 19

---

## AC Evaluation Table

| # | AC Text | Status | Supporting Evidence |
|---|---|---|---|
| 1 | `docker-compose.yml` defines a new service (distinct from `openclaw-core`) for the external OpenClaw assistant runtime | PASS | `docker-compose.yml` lines 52–85 define `openclaw-agent` as a distinct service. Both services appear in P3-T1 config output. |
| 2 | `docker-compose.dev.yml` includes a corresponding dev-mode service definition for the assistant | PASS | `docker-compose.dev.yml` includes the `openclaw-agent` dev override with `extra_hosts: host.docker.internal=host-gateway`. Confirmed in P3-T2 config output. |
| 3 | `.env.example` documents new configuration keys required by the assistant service (image, port, workspace path) | PASS | `.env.example` contains `OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest`, `OPENCLAW_AGENT_PORT=18789`, `OPENCLAW_AGENT_WORKSPACE=./deploy/docker/openclaw-assistant`. |
| 4 | The new service mounts the HostAdapter token file read-only at `/run/openclaw/hostadapter.token` | PASS | P3-T1 config output: `target: /run/openclaw/hostadapter.token`, `read_only: true`. |
| 5 | The new service uses `host.docker.internal` to reach `HostAdapter` on the Windows host | PASS | P3-T1 config output: `OpenClaw__HostAdapter__BaseUrl: http://host.docker.internal:4319/v1`. |
| 6 | The new service follows the existing Docker security posture: loopback-only port publishing, non-root user, read-only root filesystem | PASS | P3-T3 security posture verification: `host_ip: 127.0.0.1`, `user: 1654:1654`, `read_only: true`, `cap_drop: ALL`, `security_opt: no-new-privileges:true`. All PASS. |
| 7 | Assistant tool/skill definitions use HTTP calls to HostAdapter endpoints instead of CLI `exec` against `OpenClaw.MailBridge.Client.exe` | PASS | `deploy/docker/openclaw-assistant/TOOLS.md` defines six HTTP-based tools using `http://host.docker.internal:4319/v1` endpoints. `skills/mailbridge_admin/SKILL.md` also uses HTTP patterns. No reference to `OpenClaw.MailBridge.Client.exe`. |
| 8 | Existing `openclaw-core` service definition and its functionality remain unchanged after the addition | PASS | P3-T4 regression check: `openclaw-core` block is semantically identical to P0-T5 baseline. |
| 9 | Documentation (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`) updated to reflect the new service and its relationship to the existing topology | PASS | All three files contain references to `openclaw-agent` (verified via grep returning all three paths). |
| 10 | The assistant's system instructions enforce read-only behavior, no-write claims, and redaction awareness | PASS | `SOUL.md` priority 5 states no write claims; states "Do not infer or fabricate missing fields" for redacted data. `AGENTS.md` decision label `AUTO_COORDINATE` explicitly states "never permission to send, reschedule, or take any write action". |
| 11 | The assistant is identified as `admin-assistant` across its workspace and config | PASS | `IDENTITY.md`: `Agent ID: admin-assistant`. `AGENTS.md` heading: "Administrative Assistant". `openclaw.json`: `"id": "admin-assistant"`. Consistent across all three. |
| 12 | The assistant workspace uses OpenClaw-native layout: `IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`, `TOOLS.md`, and `skills/mailbridge_admin/SKILL.md` | PASS | All six files exist: `IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`, `TOOLS.md` (kept from v1), `skills/mailbridge_admin/SKILL.md`. Verified via `ls deploy/docker/openclaw-assistant/`. |
| 13 | `AGENTS.md` defines a session-start protocol (status check, then 7-day meeting-request / 24-hour message / 14-day calendar baseline pulls) | PASS | `AGENTS.md` Session-Start Protocol lists: (1) read workspace files, (2) `GET /v1/status` with stop-on-not-ready, (3) pull meeting requests last 7 days, messages last 24 hours, calendar next 14 days, (4) expand individual items only for relevant entries. |
| 14 | `AGENTS.md` defines the decision labels `IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`, with `AUTO_COORDINATE` scoped to recommendation only | PASS | `AGENTS.md` Decision Labels section lists all five labels. `AUTO_COORDINATE` description: "this label is never permission to send, reschedule, or take any write action, because the HostAdapter API is read-only". |
| 15 | `AGENTS.md` requires every triage/summary response to follow a 4-part output format: Executive summary, Items needing action, Proposed drafts / next steps, Unknowns / missing data | PASS | `AGENTS.md` Required Output Format section lists all four parts in stated order. |
| 16 | The skill file uses OpenClaw YAML frontmatter (`name`, `description`, `metadata.openclaw.os`) and encodes the required workflow | PASS | `skills/mailbridge_admin/SKILL.md` YAML frontmatter contains `name: mailbridge_admin`, `description: Read Outlook inbox, meeting requests, and calendar from the OpenClaw HostAdapter HTTP API.`, `metadata.openclaw.os: ["windows"]`. Required workflow numbered 1–5 is present. |
| 17 | A placeholder `openclaw.json` documents the intended gateway/agent config (`gateway.mode=local`, loopback bind, token auth, `session.dmScope=per-channel-peer`, `tools.profile=minimal`, `admin-assistant` agent entry) and is flagged for verification | PASS | `openclaw.json` contains `_placeholder` key with verification warning. All required keys present: `gateway.mode=local`, `gateway.bind=loopback`, `gateway.auth.mode=token`, `session.dmScope=per-channel-peer`, `tools.profile=minimal`, agent entry `id=admin-assistant` with `skills: ["mailbridge_admin"]`. |
| 18 | The assistant's Anthropic credential (`ANTHROPIC_API_KEY`) is supplied via a gitignored `env_file`, never committed, and never baked into image layers | PASS | `.gitignore` contains `secrets/` (line 70). `docker-compose.yml` `openclaw-agent` service has `env_file: ./secrets/.env.anthropic`. `secrets/.env.anthropic` contains only `ANTHROPIC_API_KEY=` (empty). File does not appear in `git status` (gitignored). No key in committed `.env.example`, `docker-compose.yml`, or workspace files. |
| 19 | `.env.example` documents `ANTHROPIC_API_KEY` (empty placeholder) and `OPENCLAW_AGENT_MODEL`, pointing operators to the official OpenClaw Anthropic provider docs for the current model ID | PASS | `.env.example` contains `ANTHROPIC_API_KEY=` (empty) with comment "Supply the real value in secrets/.env.anthropic (gitignored). Never commit the real key here." and `OPENCLAW_AGENT_MODEL=anthropic/claude-opus-4-6` with comment referencing `docs.openclaw.ai/providers/anthropic`. |

---

## Summary

- Total AC items: 19
- PASS: 19
- FAIL: 0
- Remaining (unchecked): 0

All 19 acceptance criteria from `v2/user-story.md` are satisfied.
