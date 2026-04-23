# Research: OpenClaw Agent Capabilities — v2 Root Cause (tools.profile)

- **Issue:** #43
- **Feature Folder:** docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2
- **Author:** orchestrator
- **Date:** 2026-04-22
- **Status:** Complete

---

## Executive Summary

v1 of this bug fix (plan.2026-04-21T14-00.md) resolved two of the three root causes that prevented the openclaw agent from functioning:

1. **ACP runtime startup failure** — `npx @zed-industries/codex-acp@^0.11.1` could not write its cache to `/.npm` on a read-only container filesystem. Fixed by pre-installing `@zed-industries/codex-acp@0.11.1` globally in the Dockerfile and adding `ENV CODEX_HOME=/workspace/.codex` and `ENV NPM_CONFIG_CACHE=/workspace/.npm-cache`.
2. **DashboardAuth probe false Unexpected** — The validator's `DashboardAuth` probe POST'd to `/auth/verify` which returns HTTP 404 (not implemented). Fixed by removing the probe entirely.

Both v1 fixes are confirmed PASS in the v1 feature audit (AC-1 through AC-8 all green).

However, the agent is still not functional. The agent's own error message from post-v1 testing reads:

> "I'd like to help find your next available 60-minute window, but I'm unable to query your calendar right now — my current session doesn't have execution capabilities to call the HostAdapter API (GET /v1/calendar). What I can do: 1. Grant exec capability to this session so I can call the HostAdapter directly..."

This message reveals the v2 root cause: **the tools profile in `openclaw.json` does not grant the agent bash/exec capabilities.**

---

## v2 Root Cause Analysis

### Affected File

`deploy/docker/openclaw-assistant/openclaw.json` — line 15:

```json
"tools": { "profile": "minimal" }
```

### How the Tools Profile Works

The `tools.profile` field in `openclaw.json` sets the base tool allowlist for agent sessions before per-agent `allow`/`deny` adjustments. Valid profiles (confirmed from `docs.openclaw.ai/gateway/configuration-reference`):

| Profile | Included Tools |
|---|---|
| `minimal` | `session_status` only |
| `coding` | `group:fs`, `group:runtime`, `group:web`, `group:sessions`, `group:memory`, `cron`, `image`, `image_generate`, `video_generate` |
| `messaging` | `group:messaging`, `sessions_list`, `sessions_history`, `sessions_send`, `session_status` |
| `full` | No restriction (same as unset) |

Relevant tool groups:

| Group | Tools |
|---|---|
| `group:runtime` | `exec`, `process`, `code_execution` (`bash` is an alias for `exec`) |
| `group:fs` | `read`, `write`, `edit`, `apply_patch` |
| `group:web` | `web_search`, `x_search`, `web_fetch` |

### Impact of `minimal` Profile

With `"profile": "minimal"`, the agent session has **only `session_status`**. It cannot:

- Execute bash/curl to call the HostAdapter API (`exec` / `bash`)
- Read files, including the HostAdapter token at `/run/openclaw/hostadapter.token` (`read`)
- Make web fetches or searches (`web_fetch`, `web_search`)

The SKILL.md (`deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`) and TOOLS.md (`deploy/docker/openclaw-assistant/TOOLS.md`) both define HTTP call patterns against `http://host.docker.internal:4319/v1/...` using the token from `/run/openclaw/hostadapter.token`. Without `group:runtime`, none of these calls can execute.

The agent's error message `"my current session doesn't have execution capabilities"` directly confirms this diagnosis.

### Why `coding` Is the Correct Profile (not `exec`)

There is no `exec` profile in openclaw. The correct profile that unlocks exec/bash tools is `coding`, which includes:
- `group:runtime` → provides `exec` (= `bash`) needed to run `curl` against the HostAdapter
- `group:fs` → provides `read` needed to read `/run/openclaw/hostadapter.token`
- `group:web` → provides `web_fetch` and `web_search` as alternative tool paths
- `group:sessions` → provides session management tools
- `group:memory` → provides memory tools

This is also the profile recommended by openclaw documentation for local onboarding: "Local onboarding defaults new local configs to `tools.profile: 'coding'` when unset."

---

## Entrypoint Seeding Behavior

`deploy/docker/openclaw-agent-entrypoint.sh` contains:

```sh
cp "$seed_dir/openclaw.json" "$runtime_dir/openclaw.json"
```

This line runs unconditionally on every container start, overwriting `/.openclaw/openclaw.json` from the seed image. This means:

1. The tools profile fix must be made in the **seed file** (`deploy/docker/openclaw-assistant/openclaw.json`) and baked into the image.
2. A Docker image rebuild is required. An `exec` into the running container and file edit would be overwritten on the next container start.
3. The fix requires: `docker compose build openclaw-agent` followed by `docker compose up -d --force-recreate openclaw-agent`.

---

## Security Analysis

Changing from `minimal` to `coding` enables exec/bash tools within the container session. The container's own hardening is unchanged and still applies:

- `read_only: true` — the container root filesystem is read-only; exec tools can only write to `tmpfs` mounts or the workspace volume
- `cap_drop: ALL` — no Linux capabilities are available; exec tools cannot escalate privileges
- `no-new-privileges: true` — processes spawned by exec tools cannot gain additional privileges via setuid/setgid
- `tmpfs` with `noexec,nosuid,nodev` on `/tmp` and `/.openclaw` — bash tools cannot execute binaries from these paths
- User `1654` is non-root with no additional groups

The `coding` profile enables `bash` (exec) to run installed binaries such as `curl`, which is the exact capability needed to call `http://host.docker.internal:4319/v1/...`. This is an appropriate tool for an assistant container designed for this purpose.

No changes to `docker-compose.yml` are required or warranted.

---

## Minimal Change Surface

Only one production file requires modification:

| File | Change |
|---|---|
| `deploy/docker/openclaw-assistant/openclaw.json` | Line 15: `"profile": "minimal"` → `"profile": "coding"` |

Post-change actions required (not source file changes):

1. `docker compose build openclaw-agent` — rebuild image with new seed file
2. `docker compose up -d --force-recreate openclaw-agent` — recreate container from new image

---

## Verification Strategy

After the image rebuild and container recreation:

1. Ask the agent a calendar question: "When is my next available 60-minute window?" (or equivalent)
2. Confirm the agent calls `GET /v1/calendar` against the HostAdapter instead of reporting no execution capabilities
3. Confirm container hardening is preserved: `docker inspect openclaw-agent` should show `ReadonlyRootfs: true`, `CapDrop: ["ALL"]`, and `SecurityOpt: ["no-new-privileges:true"]`

---

## v1 Evidence (for Reference)

All eight v1 ACs confirmed PASS:
- `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v1/audit-2026-04-21T15-34/feature-audit.2026-04-21T15-34.md`

The ACP runtime backend starts cleanly (`[plugins] embedded acpx runtime backend ready`). The remaining blocker is strictly the tools profile.

---

## External Sources Consulted

- `https://docs.openclaw.ai/gateway/configuration-reference` — Tool profiles table, tool groups table, per-agent tools override schema
- `https://docs.openclaw.ai/gateway/configuration` — Config hot-reload behavior, strict validation rules
- `https://github.com/openclaw/openclaw` — Security model, default session access, typical sandbox tool allowlist
