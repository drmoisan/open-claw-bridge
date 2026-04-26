# Code Review — openclaw-agent-docker v2 (Issue #30)

- **Timestamp:** 2026-04-18T00-00
- **Branch:** `feature/openclaw-agent-docker-30`
- **Reviewer:** Feature Review Agent
- **Work Mode:** full-feature
- **Scope:** v2 deliverables — credential infrastructure, OpenClaw workspace layout files, `.gitignore` and `.env.example` additions, `docker-compose.yml` `env_file` stanza, deletion of `SYSTEM.md` and `config.yaml`

---

## Summary

| Area | Verdict | Notes |
|---|---|---|
| `docker-compose.yml` — `openclaw-agent` service | PASS | Security posture correct; env_file wiring correct |
| `docker-compose.dev.yml` — `openclaw-agent` override | PASS | Minimal and correct |
| `.gitignore` — `secrets/` pattern | PASS | Correctly placed; precedes credential file creation |
| `.env.example` — new credential entries | PASS | Empty placeholder; operator guidance clear |
| `IDENTITY.md` | PASS | Complete and minimal |
| `SOUL.md` | PASS | All five priorities present in required order |
| `USER.md` | PASS | Operator profile with placeholder notice |
| `AGENTS.md` | PASS | Session-start protocol, labels, and output format all correct |
| `TOOLS.md` (v1, unchanged in v2) | PASS | Six endpoints defined; no CLI exec references |
| `skills/mailbridge_admin/SKILL.md` | PASS | YAML frontmatter valid; workflow complete |
| `openclaw.json` | PASS WITH NOTE | Placeholder correctly flagged; schema unverified |
| `SYSTEM.md` deletion | PASS | Correctly retired |
| `config.yaml` deletion | PASS | Correctly retired |

Overall verdict: **PASS**

---

## Detailed Findings

### `docker-compose.yml` — `openclaw-agent` service block

The service block (lines 52–85) is well-structured. All security-relevant properties are present and correct:

- `read_only: true` — root filesystem is read-only.
- `cap_drop: [ALL]` — all Linux capabilities dropped.
- `security_opt: [no-new-privileges:true]` — privilege escalation blocked.
- `user: "1654:1654"` — non-root user. (Note: UID for the external image is unverified; flagged as an open item in the plan.)
- `ports: ["127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}:18789"]` — loopback-only binding; no `0.0.0.0` exposure.
- Token file bind-mounted at `/run/openclaw/hostadapter.token` with `read_only: true`.
- Workspace bind-mounted at `/workspace` (no `read_only`; the runtime requires write access to its workspace for session state).
- `tmpfs` entries for `/tmp` and `/.openclaw` with `noexec,nosuid,nodev` flags — mutable scratch space without persistent state.
- `env_file: ./secrets/.env.anthropic` — credential injected via gitignored file, not baked into the image or the compose file directly.
- `init: true` — correct for signal handling in a long-running agent process.
- `restart: unless-stopped` — consistent with `openclaw-core`.

One structural observation: `env_file` is positioned after `restart: unless-stopped` and before `user:`. The position is valid YAML and Docker Compose processes `env_file` independently of key order. No defect.

The `healthcheck` uses `curl -fsS http://127.0.0.1:18789/healthz`. The `-f` flag causes `curl` to return a non-zero exit code on HTTP error responses, which is correct for a health check. The endpoint path `/healthz` is a standard convention; whether the external image actually exposes this path is unverified (noted in plan Open Questions).

**No defects identified.**

### `docker-compose.dev.yml` — `openclaw-agent` override

The dev override is minimal and correct: it adds only `extra_hosts: - "host.docker.internal:host-gateway"` to the `openclaw-agent` service. This is the standard pattern for resolving `host.docker.internal` on Linux-based Docker Engine hosts (as opposed to Docker Desktop on Windows/macOS where it resolves natively). The override is consistent with how `openclaw-dev` is handled in the same file.

**No defects identified.**

### `.gitignore` — `secrets/` pattern

The `secrets/` pattern is at line 70, placed under the `# Local environment files` block immediately after the existing `.env.*` / `!.env.example` entries. The placement is semantically correct: the secrets directory is excluded from all git tracking, which prevents any file under `secrets/` from being accidentally committed regardless of name.

The plan documents that `.gitignore` is updated before the credential file is created (Phase 1, P1-T1 before P1-T2). The current working tree state confirms the pattern is present.

**No defects identified.**

### `.env.example` — new credential entries

The `.env.example` additions document `ANTHROPIC_API_KEY=` with an empty value and a three-line comment block:
- Source for obtaining the key (Anthropic console URL).
- Where the real value must be supplied (`secrets/.env.anthropic`, gitignored).
- Explicit prohibition on committing the real key here.

`OPENCLAW_AGENT_MODEL=anthropic/claude-opus-4-6` is documented with a comment directing operators to `docs.openclaw.ai/providers/anthropic` for the current model ID. This is important because the model ID may change as OpenClaw updates its Anthropic provider support.

The existing entries are preserved unchanged. The new entries are additive.

**No defects identified.**

### `IDENTITY.md`

Correct and minimal. Contains agent name (`Admin Assistant`), agent ID (`admin-assistant`), role description, and tone. The agent ID matches `openclaw.json` and `AGENTS.md` heading.

**No defects identified.**

### `SOUL.md`

All five priorities are present in the required order as numbered prose items. Priority 5 explicitly states the no-write-claim contract: "Never claim to have sent an email, changed a calendar event, or taken any write action unless a future write plane explicitly confirms success." The redaction guidance ("Do not infer or fabricate missing fields") is present.

The file is 11 lines. The content is appropriately concise for a behavioral contract document.

**No defects identified.**

### `USER.md`

The operator profile covers role (Docker Desktop deployment, need for actionable visibility), context of use (daily standup prep, end-of-day triage), and constraints (HostAdapter is read-only). A placeholder notice at the end explicitly directs the deploying operator to replace the section with their actual profile (name, organization, timezone).

**No defects identified.**

### `AGENTS.md`

The session-start protocol is a four-step numbered list matching the spec:
1. Read workspace files.
2. Call `GET /v1/status`; stop and report if not ready.
3. Pull baseline windows (7d meeting requests, 24h messages, 14d calendar).
4. Expand individual items only for relevant entries.

All five decision labels are defined with descriptions. `AUTO_COORDINATE` is explicitly scoped to recommendation-only: "this label is never permission to send, reschedule, or take any write action, because the HostAdapter API is read-only." This wording is precise and leaves no ambiguity.

The required output format lists all four parts in the required order: Executive summary, Items needing action, Proposed drafts / next steps, Unknowns / missing data.

The file is 40 lines and well-organized with clear heading hierarchy.

**No defects identified.**

### `TOOLS.md` (v1, unchanged in v2)

All six HostAdapter endpoints are defined with correct URL patterns, HTTP methods, header requirements, query and path parameter documentation, expected response shapes, and error responses. Authentication is specified consistently: `Authorization: Bearer <token>` read from `/run/openclaw/hostadapter.token`. The ISO-8601 UTC format requirement for date/time parameters is stated. The maximum `limit` of 250 is documented.

No references to `OpenClaw.MailBridge.Client.exe`, named pipes, or Windows-only CLI tools appear anywhere in the file.

**No defects identified.**

### `skills/mailbridge_admin/SKILL.md`

The YAML frontmatter block is syntactically valid (delimited by `---`):
- `name: mailbridge_admin`
- `description: Read Outlook inbox, meeting requests, and calendar from the OpenClaw HostAdapter HTTP API.`
- `metadata.openclaw.os: ["windows"]`

The body defines a trigger condition (when the operator asks about inbox items, meeting requests, scheduling conflicts, calendar analysis, or drafting an administrative response) and a five-step required workflow:
1. Check bridge status; stop if not ready.
2. Use list endpoints first.
3. Use get endpoints only for relevant items.
4. Respect redaction; never fabricate fields.
5. Bridge is read-only; never claim a write occurred.

An HTTP patterns table summarizes all six endpoints with example values. The base URL and authentication header requirement are stated.

No references to `OpenClaw.MailBridge.Client.exe` or named pipes appear.

**No defects identified.**

### `openclaw.json`

The file opens with a `_placeholder` key containing an explicit human-readable warning: "VERIFY ALL VALUES against https://docs.openclaw.ai/gateway/configuration-reference before production use. This file was generated from unverified integration research."

All required keys are present:
- `gateway.mode: "local"`, `gateway.port: 18789`, `gateway.bind: "loopback"`, `gateway.auth.mode: "token"`
- `session.dmScope: "per-channel-peer"`
- `tools.profile: "minimal"`
- `agents.defaults.model.primary: "anthropic/claude-opus-4-6"` with fallback `"anthropic/claude-sonnet-4-6"`
- `agents.list[0]`: `id: "admin-assistant"`, `workspace: "/workspace"`, `skills: ["mailbridge_admin"]`

The `gateway.port: 18789` matches the `OPENCLAW_AGENT_PORT` default in `.env.example` and the port published in `docker-compose.yml`.

**Note (non-blocking):** The configuration schema is unverified against official OpenClaw documentation. This is intentional and flagged in the file itself and in the plan. The file must not be used in production without verification. This is an operator responsibility, not a code defect.

**No blocking defects identified.**

### `SYSTEM.md` and `config.yaml` deletions

Both files are deleted from the working tree (visible in `git status` as staged deletions: `D deploy/docker/openclaw-assistant/SYSTEM.md` and `D deploy/docker/openclaw-assistant/config.yaml`). The replacements (`SOUL.md` + `AGENTS.md` for `SYSTEM.md`, `openclaw.json` for `config.yaml`) are present. No remaining files in the workspace reference the deleted paths.

**No defects identified.**

---

## Uncommitted State Observation

The v2 deliverables are present in the working tree but the `secrets/` gitignore pattern, `.env.example` additions, `docker-compose.yml` `env_file` stanza, and all workspace files are currently unstaged or in working-directory-modified state (visible in `git status`). The feature is functionally complete but not yet committed.

This does not affect the review verdict. The changes are auditable from the working tree. The operator should commit all v2 changes together in a single commit or as a logically grouped set before merging to the target branch.

---

## Items Requiring No Further Action

- No file exceeds 500 lines.
- No source code changes require toolchain loop execution.
- No coverage delta analysis is required.
- No breaking API changes were introduced.
- No new library dependencies were added.
