# 2026-04-21-openclaw-agent-capabilities-none (Spec)

- **Issue:** #43
- **Parent (optional):** v1 (plan.2026-04-21T14-00.md, all 8 ACs PASS)
- **Owner:** drmoisan
- **Last Updated:** 2026-04-22T10-45
- **Status:** Approved
- **Version:** 2.0

## Context

v1 of this bug fix resolved the ACP runtime startup failure and the DashboardAuth probe false-positive (8 ACs, all PASS). The agent container now starts cleanly with the embedded `acpx` runtime backend ready. However, the agent is still not functional when answering data questions.

After v1, when asked "When is my next available 60-minute window?" the agent responds:

> "I'd like to help find your next available 60-minute window, but I'm unable to query your calendar right now — my current session doesn't have execution capabilities to call the HostAdapter API (GET /v1/calendar). What I can do: 1. Grant exec capability to this session so I can call the HostAdapter directly..."

This confirms a remaining defect: the agent's tool profile prevents it from executing bash or curl to call the HostAdapter.

## Repro & Evidence

**Steps to reproduce:**
1. Start the full Docker Compose stack (`docker compose up -d`)
2. Connect to the openclaw agent via its gateway interface
3. Ask: "When is my next available 60-minute window?" (or any question requiring HostAdapter data)

**Expected behavior:**
The agent issues `GET http://host.docker.internal:4319/v1/calendar` using the token from `/run/openclaw/hostadapter.token` and returns calendar availability information.

**Actual behavior:**
The agent responds with a message indicating it has no execution capabilities and cannot call the HostAdapter API. The agent explicitly reports `capabilities=none` and refuses to issue HTTP requests.

**Root cause signal:**
`deploy/docker/openclaw-assistant/openclaw.json` line 15 contains `"tools": { "profile": "minimal" }`. According to the openclaw configuration reference (docs.openclaw.ai/gateway/configuration-reference), the `minimal` profile allows only `session_status`. The `exec` (bash) tool required to run `curl` is part of `group:runtime`, which is only included in the `coding` or `full` profiles.

**Frequency:** Deterministic — reproducible on every question that requires HostAdapter data.

## Scope & Non-Goals

**In scope:**
- Change `"profile": "minimal"` to `"profile": "coding"` in `deploy/docker/openclaw-assistant/openclaw.json`
- Rebuild the Docker image and recreate the container
- Verify the agent can call the HostAdapter using exec/bash tools

**Out of scope / non-goals:**
- Changes to `docker-compose.yml` (container hardening must be preserved as-is)
- Changes to the Dockerfile beyond what is needed for the openclaw.json seed
- Changes to PowerShell scripts or C# code
- Changes to the HostAdapter, Core, or bridge services
- Upstream openclaw gateway changes

**Explicitly excluded:**
- Dashboard authentication architecture
- ACP runtime configuration (v1 confirmed working)
- Any openclaw features unrelated to the tools profile

## Root Cause Analysis

**Confirmed root cause:**
`"tools": { "profile": "minimal" }` in `deploy/docker/openclaw-assistant/openclaw.json` restricts all agent sessions to `session_status` only. The agent cannot execute bash or read files, so it cannot call `curl http://host.docker.internal:4319/v1/...` or read `/run/openclaw/hostadapter.token`.

**Signals/evidence:**
1. Agent error message explicitly states no execution capabilities
2. `grep` confirms `"profile": "minimal"` is on line 15 of `openclaw.json` (the only profile configuration in the repo)
3. openclaw configuration reference table: `minimal` = `session_status only`
4. openclaw configuration reference table: `group:runtime` = `exec, process, code_execution` — only in `coding` or `full` profiles
5. SKILL.md and TOOLS.md both require `bash`/`exec` to call `http://host.docker.internal:4319/v1/...`

**Affected components:**
- `deploy/docker/openclaw-assistant/openclaw.json` — the seed configuration baked into the image

**How the seed reaches the runtime:**
`deploy/docker/openclaw-agent-entrypoint.sh` unconditionally copies `$seed_dir/openclaw.json` to `$runtime_dir/openclaw.json` (`/.openclaw/openclaw.json`) on every container start. The openclaw gateway reads its configuration from `/.openclaw/openclaw.json`. Any runtime change to this file is overwritten on restart. The fix must be in the seed file and requires an image rebuild.

## Proposed Fix

### Design summary (what changes where)

Change `"profile": "minimal"` to `"profile": "coding"` in `deploy/docker/openclaw-assistant/openclaw.json` (line 15). Rebuild the Docker image and recreate the container. No other source files change.

### Boundaries and invariants to preserve

- Container hardening in `docker-compose.yml` must remain exactly as in v1: `read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, `tmpfs` with `noexec,nosuid,nodev` on `/tmp` and `/.openclaw`
- The `coding` profile grants exec/bash within the hardened container. The container security model prevents privilege escalation regardless of which tools are enabled.
- v1 Dockerfile changes (global `@zed-industries/codex-acp` install, `CODEX_HOME`, `NPM_CONFIG_CACHE`) must be preserved

### Dependencies or blocked work

None. The change is a single JSON field value. Image rebuild depends only on the modified seed file.

### Implementation strategy (what changes, not sequencing)

#### Files/modules to change

| File | Change |
|---|---|
| `deploy/docker/openclaw-assistant/openclaw.json` | Line 15: `"profile": "minimal"` → `"profile": "coding"` |

No other source files change.

#### Functions/classes/CLI commands impacted

None — this is a configuration-only change to a JSON seed file.

#### Data flow and validation changes

None. The openclaw gateway reads the tools profile at startup and applies it to new agent sessions. No data schema or pipeline changes.

#### Error handling and logging updates

None required. If the profile value is invalid, the openclaw gateway will refuse to start and log a validation error. `coding` is a valid, documented profile value.

#### Rollback/feature-flag considerations

Rollback: change `"profile": "coding"` back to `"profile": "minimal"` in `openclaw.json`, rebuild, and recreate. This restores the previous (broken) behavior.

No feature flags. The change is immediate and in effect on the next container start after image rebuild.

### Technical specifications (interfaces/contracts)

#### Inputs/outputs and formats

`openclaw.json` is a JSON5 document (comments and trailing commas allowed). The `tools.profile` field accepts exactly one of: `"minimal"`, `"coding"`, `"messaging"`, `"full"`. The value `"coding"` enables:
- `group:runtime` → `exec`, `process`, `code_execution` (bash is an alias for exec)
- `group:fs` → `read`, `write`, `edit`, `apply_patch`
- `group:web` → `web_search`, `x_search`, `web_fetch`
- `group:sessions` → `sessions_list`, `sessions_history`, `sessions_send`, `sessions_spawn`, `sessions_yield`, `subagents`, `session_status`
- `group:memory` → `memory_search`, `memory_get`
- `cron`, `image`, `image_generate`, `video_generate`

#### Required configuration keys and defaults

```json
{
  "tools": { "profile": "coding" }
}
```

No other configuration keys change.

#### Backward-compatibility expectations

Fully backward-compatible. The `tools.profile` field has been `minimal` since the container was first configured. Changing it to `coding` does not alter any data, API, or configuration schema. The change only affects what tools are available in agent sessions.

#### Performance constraints (latency/throughput/memory)

None. The tools profile is applied at session initialization and has no runtime performance impact.

## Assumptions, Constraints, Dependencies

**Assumptions:**
- The Docker daemon is available and the `openclaw-agent` service is running
- The operator has access to run `docker compose build` and `docker compose up`
- The HostAdapter is listening on `host.docker.internal:4319` (unchanged from v1)
- The `hostadapter.token` file is available at `/run/openclaw/hostadapter.token` inside the container (unchanged from v1)

**Constraints:**
- The `docker-compose.yml` hardening configuration must not change (AC-3-v2)
- The Dockerfile v1 changes (codex-acp install, CODEX_HOME, NPM_CONFIG_CACHE) must be preserved
- No PowerShell or C# changes are in scope; no PowerShell toolchain pass is required for this change

**External dependencies:**
- `deploy/docker/openclaw-agent.Dockerfile` is the build context for the image
- `deploy/docker/openclaw-agent-entrypoint.sh` seeds `openclaw.json` to `/.openclaw/` on container start
- openclaw gateway reads `/.openclaw/openclaw.json` at startup

## Data / API / Config Impact

**User-facing or API changes:**
None to the OpenClaw MailBridge API surface. The agent's HTTP API (`http://host.docker.internal:4319/v1/...`) is unchanged.

**Data or migration considerations:**
None. `openclaw.json` is a seed file; any existing `/workspace/openclaw.json` or `/.openclaw/openclaw.json` in the container volume will be overwritten on the next container start after image rebuild.

**Logging/telemetry updates:**
None required. The openclaw gateway logs tool-profile configuration at startup; `coding` is a known-good value and will not produce any warnings.

**Compatibility notes:**
The `tools.profile: "coding"` value is documented in the openclaw configuration reference as the local onboarding default. No special compatibility handling is needed.

## Test Strategy

**Regression tests to add or update:**
No automated regression test is added for this change. The change is a JSON configuration value in a Docker seed file; automated verification would require a running Docker environment, which falls outside the repo's unit test surface. Manual validation is the appropriate gate.

**Manual validation steps (required):**
1. Apply the change to `deploy/docker/openclaw-assistant/openclaw.json`
2. Run `docker compose build openclaw-agent` to rebuild the image
3. Run `docker compose up -d --force-recreate openclaw-agent` to recreate the container
4. Ask the agent: "When is my next available 60-minute window?" (or any calendar question)
5. Confirm the agent executes a call to `GET http://host.docker.internal:4319/v1/calendar` and returns calendar data
6. Confirm the agent does NOT respond with "no execution capabilities" or "cannot call the HostAdapter"
7. Run `docker inspect openclaw-agent` and confirm `ReadonlyRootfs: true`, `CapDrop: ["ALL"]`, `SecurityOpt: ["no-new-privileges:true"]` remain in place

**Edge cases and negative scenarios:**
- If the profile value is invalid (e.g., a typo), the openclaw gateway will refuse to start and log a schema validation error. Run `docker compose logs openclaw-agent` to diagnose.
- If the HostAdapter is not running on `host.docker.internal:4319`, the agent will report connection errors — this is expected behavior unrelated to the tools profile.

**Toolchain commands (this change is Docker-only; no PS/C# toolchain needed):**
```sh
docker compose build openclaw-agent
docker compose up -d --force-recreate openclaw-agent
docker compose logs openclaw-agent | grep -E "profile|tools|ready|error"
```

## Acceptance Criteria

- [ ] AC-1-v2 — The agent answers a calendar question by calling `GET http://host.docker.internal:4319/v1/calendar` successfully. Evidence: agent response contains calendar availability data, not an "execution capabilities" error message. NOTE: Manual operator verification pending — see `verify-agent-capability.2026-04-22.md`.
- [ ] AC-2-v2 — The agent session has exec/bash tools available (no "cannot execute" or "capabilities=none" error when asked to call the HostAdapter). Evidence: the agent completes a tool call using `exec`/`bash` to retrieve data from the HostAdapter. NOTE: Manual operator verification pending — see `verify-agent-capability.2026-04-22.md`.
- [x] AC-3-v2 — Container hardening is preserved: `read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, and `noexec/nosuid/nodev` on tmpfs mounts remain. Evidence: `docker inspect openclaw-agent` output confirms all six hardening tokens; `git diff development HEAD -- docker-compose.yml` is empty. Artifacts: `verify-hardening.2026-04-22.md`, `verify-compose-unchanged.2026-04-22.md`.
- [x] AC-4-v2 — `deploy/docker/openclaw-assistant/openclaw.json` contains `"profile": "coding"` (not `"minimal"`). Evidence: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json` returns `"profile": "coding"`. Artifacts: `verify-profile-in-container.2026-04-22.md`.
- [x] AC-5-v2 — v1 ACs (AC-1 through AC-8) remain PASS. Evidence: the ACP runtime still starts successfully (`[plugins] embedded acpx runtime backend ready` in logs); the container-path validator still returns `OverallResult: Expected` when the stack is healthy. Artifact: `verify-gateway-logs.2026-04-22.md` (acpx ready confirmed; 0 probe failures).

## Risks & Mitigations

**Risk 1 — Expanded tool surface inside container**
Granting exec/bash tools means the agent can now run any installed binary inside the container. Mitigation: the container's Linux security model (`cap_drop: ALL`, `no-new-privileges: true`, `read_only` root FS) prevents privilege escalation or host filesystem access from any executed command.

**Risk 2 — Invalid profile value breaks gateway startup**
The openclaw gateway will refuse to start on invalid config. Mitigation: `"coding"` is a documented, valid profile value. Verified against the configuration reference schema.

**Risk 3 — Volume-cached openclaw.json persists old profile**
The entrypoint always overwrites `/.openclaw/openclaw.json` from the seed, so the volume cannot retain a stale profile. Mitigation: none needed beyond the image rebuild.

## Rollout & Follow-up

**Release/rollout steps:**
1. Apply the one-line change to `deploy/docker/openclaw-assistant/openclaw.json`
2. `docker compose build openclaw-agent`
3. `docker compose up -d --force-recreate openclaw-agent`
4. Perform manual validation per Test Strategy above

**Post-fix monitoring:**
- Monitor `docker compose logs openclaw-agent` for any gateway startup errors
- Confirm agent responses include HostAdapter data on the first calendar/message question after container recreation

**Links:**
- Issue: #43
- v1 feature folder: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v1/`
- v2 research: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/2026-04-22-openclaw-agent-capabilities-none-v2-research.md`
- openclaw config reference: https://docs.openclaw.ai/gateway/configuration-reference
