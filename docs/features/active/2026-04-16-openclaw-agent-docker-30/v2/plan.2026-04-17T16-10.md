# 2026-04-16-openclaw-agent-docker — Implementation Plan (v2)

- **Issue:** #30
- **Owner:** drmoisan
- **Last Updated:** 2026-04-18
- **Status:** Executed
- **Version:** 2.0

---

## Required References

- General Code Change Policy: [`.claude/rules/general-code-change.md`](../../../../.claude/rules/general-code-change.md)
- General Unit Test Policy: [`.claude/rules/general-unit-test.md`](../../../../.claude/rules/general-unit-test.md)
- Tonality Policy: [`.claude/rules/tonality.md`](../../../../.claude/rules/tonality.md)

All work must comply with these policies. Do not duplicate their content here.

---

## Overview

v1 of this feature (committed to `feature/openclaw-agent-docker-30`) delivered the `openclaw-agent` Docker Compose service, the combined dev compose override, updated documentation, and an HTTP-based `TOOLS.md`. v2 completes the remaining items: credential infrastructure (`secrets/` gitignore pattern, `secrets/.env.anthropic`, missing `.env.example` entries, missing `env_file` stanza in `docker-compose.yml`), the OpenClaw-native workspace layout (`IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`, `skills/mailbridge_admin/SKILL.md`, `openclaw.json`), and retirement of the v1 placeholder files (`SYSTEM.md`, `config.yaml`).

No C#, PowerShell, or Python source code is added or modified.

---

## Coverage Evidence Contract

Not applicable. This feature changes only YAML, Markdown, and dotenv configuration files. No compiled or interpreted source code is added or modified. Language-specific toolchain baselines, coverage capture, and coverage delta gates do not apply.

---

## Test Plan

- **Compose YAML validation:** `docker compose config` on the production compose file and on the combined dev compose file validates syntax and variable resolution after all changes (Phase 3, P3-T1 and P3-T2).
- **Security posture verification:** Parsed config confirms `read_only: true`, `cap_drop: [ALL]`, non-root user, loopback-only port, read-only token mount, and the `env_file` entry on the `openclaw-agent` service (P3-T3).
- **Regression:** The `openclaw-core` service block is diffed against the Phase 0 baseline snapshot to confirm zero semantic change (P3-T4).
- **Acceptance criteria:** All 19 ACs from `v2/user-story.md` are checked with file-level evidence (P3-T5).
- **Unit and integration tests:** Not applicable — no source code changes.
- **Live stack validation:** Out of scope for this plan. Requires a running HostAdapter, the verified external OpenClaw image, and a real `ANTHROPIC_API_KEY`. These are environment-dependent prerequisites.

---

## Open Questions / Notes

1. **OpenClaw image schema unverified.** `docs.openclaw.ai` was unreachable during research. The `OPENCLAW_AGENT_IMAGE` placeholder value and the `openclaw.json` configuration shape are based on the integration-research artifact (`20260417-integration-prompt.md`). Both must be validated against official documentation before production use.
2. **`openclaw.json` schema is a placeholder.** The JSON5-style config shape in the integration research (keys: `gateway.mode`, `gateway.port`, `gateway.bind`, `gateway.auth`, `session.dmScope`, `tools.profile`, `agents.list[].id`, `agents.list[].workspace`, `agents.list[].tools`, `agents.list[].skills`) is unverified. Flag every placeholder key in the file with an inline comment.
3. **Non-root UID for the external image.** The `openclaw-agent` service currently uses `user: "1654:1654"` matching `openclaw-core`. The external OpenClaw image may require a different UID. Verify after image documentation is confirmed.
4. **`SYSTEM.md` and `config.yaml` retirement.** These v1 placeholder files are superseded by the OpenClaw-native workspace layout. They are deleted in Phase 2. No references to them remain in the committed workspace after deletion.
5. **`TOOLS.md` is kept.** The existing `deploy/docker/openclaw-assistant/TOOLS.md` already uses HTTP-based tool definitions and satisfies AC-7. No modification is required unless content alignment is needed after verifying the actual image.
6. **Port value in `.env.example`.** The v1 `.env.example` `OPENCLAW_AGENT_PORT` value must be verified. The current `docker-compose.yml` uses `18789`; the `.env.example` default should match. Phase 0 baseline capture will confirm the committed value before any edits.
7. **`secrets/.env.anthropic` is gitignored and never committed.** The file created in Phase 1 contains only a placeholder `ANTHROPIC_API_KEY=` line. Operators must populate it with a real key obtained from the Anthropic console. The file must not be committed, even accidentally, because `.gitignore` is updated before the file is created.

---

### Phase 0 — Context and Baseline

**Purpose:** Read required policies, read feature context, and capture pre-change baseline evidence. No files other than evidence artifacts are written in this phase.

- [x] [P0-T1] Read policy files in required order: (1) `CLAUDE.md` if present, (2) `.claude/rules/general-code-change.md`, (3) `.claude/rules/general-unit-test.md`
  - Acceptance: Evidence artifact written to `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/baseline/phase0-instructions-read.md` containing:
    - `Timestamp:` (ISO-8601)
    - `Policy Order:` listing each file attempted in order
    - Explicit list of files actually read (note if `CLAUDE.md` does not exist)

- [x] [P0-T2] Read feature context files: `docs/features/active/2026-04-16-openclaw-agent-docker-30/issue.md`, `docs/features/active/2026-04-16-openclaw-agent-docker-30/v2/spec.md`, `docs/features/active/2026-04-16-openclaw-agent-docker-30/v2/user-story.md`, and `docs/features/active/2026-04-16-openclaw-agent-docker-30/20260417-integration-prompt.md`
  - Acceptance: Executor confirms all four files read. Key constraints noted: no source code changes, HostAdapter HTTP-only access, Docker security posture preserved, external OpenClaw docs unverified, `secrets/` pattern missing from `.gitignore`, `env_file` stanza missing from `docker-compose.yml openclaw-agent` service, v1 workspace files `SYSTEM.md` and `config.yaml` to be retired.

- [x] [P0-T3] Capture baseline `docker compose config` output for `docker-compose.yml`
  - Preconditions: None
  - Command: `docker compose --env-file .env.example -f docker-compose.yml config`
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/baseline/baseline-compose-config.md` containing:
    - `Timestamp:`
    - `Command: docker compose --env-file .env.example -f docker-compose.yml config`
    - `EXIT_CODE:`
    - `Output Summary:` (full rendered YAML or key excerpts for both services)

- [x] [P0-T4] Capture baseline `docker compose config` output for combined dev compose
  - Preconditions: None
  - Command: `docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config`
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/baseline/baseline-compose-dev-config.md` containing:
    - `Timestamp:`
    - `Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config`
    - `EXIT_CODE:`
    - `Output Summary:`

- [x] [P0-T5] Snapshot the full `openclaw-core` service block from `docker-compose.yml` for regression comparison
  - Preconditions: None
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/baseline/baseline-openclaw-core-service.md` containing:
    - `Timestamp:`
    - The complete `openclaw-core` service YAML block as-is from `docker-compose.yml` at time of capture, verbatim

---

### Phase 1 — Credential and Secret Infrastructure

**Purpose:** Establish the gitignore pattern, the gitignored env file, the missing `.env.example` entries, and the missing `env_file` stanza in `docker-compose.yml`. These are the four gaps between v1 and AC-18 and AC-19.

- [x] [P1-T1] Add `secrets/` pattern to `.gitignore`
  - Preconditions: P0-T5 complete (baseline captured)
  - File: `.gitignore`
  - Change: Append `secrets/` on a new line under the existing `# Local environment files` block (after the `.env.*` / `!.env.example` lines). The existing `.env` and `.env.*` patterns are preserved unchanged.
  - Acceptance: `.gitignore` contains the line `secrets/` and does not contain any modification to existing entries. Running `git check-ignore -v secrets/.env.anthropic` would resolve to the `secrets/` pattern (verified by inspection after the edit).

- [x] [P1-T2] Create `secrets/.env.anthropic` with `ANTHROPIC_API_KEY=` placeholder
  - Preconditions: P1-T1 complete (`.gitignore` updated so the file cannot be accidentally staged)
  - File: `secrets/.env.anthropic` (new file, gitignored)
  - Content: Exactly the following two lines:
    ```
    # Obtain your Anthropic API key from https://console.anthropic.com
    ANTHROPIC_API_KEY=
    ```
  - Acceptance: File exists at `secrets/.env.anthropic`. The value for `ANTHROPIC_API_KEY` is empty (placeholder only). No actual key is populated. The file does not appear in `git status` as a tracked or staged file.

- [x] [P1-T3] Append `ANTHROPIC_API_KEY` and `OPENCLAW_AGENT_MODEL` entries to `.env.example`
  - Preconditions: P0-T3 complete (baseline captured)
  - File: `.env.example`
  - Change: Append a new comment block and two variable entries below the existing `OPENCLAW_AGENT_WORKSPACE` line. Existing entries remain unchanged and in their original order.
  - Required new content (exact wording):
    ```
    # Anthropic credential for the openclaw-agent assistant runtime.
    # Obtain your API key from https://console.anthropic.com.
    # Supply the real value in secrets/.env.anthropic (gitignored). Never commit the real key here.
    ANTHROPIC_API_KEY=

    # Model used by the admin-assistant agent. Verify the current model ID at docs.openclaw.ai/providers/anthropic.
    OPENCLAW_AGENT_MODEL=anthropic/claude-opus-4-6
    ```
  - Acceptance: `.env.example` contains both `ANTHROPIC_API_KEY=` (empty) and `OPENCLAW_AGENT_MODEL=anthropic/claude-opus-4-6` as new entries. All pre-existing entries are unchanged.

- [x] [P1-T4] Add `env_file: ./secrets/.env.anthropic` to the `openclaw-agent` service in `docker-compose.yml`
  - Preconditions: P1-T1 complete, P1-T2 complete
  - File: `docker-compose.yml`
  - Change: In the `openclaw-agent` service block, add an `env_file` key after the `image:` line (or after `restart: unless-stopped`) pointing to `./secrets/.env.anthropic`. The `openclaw-core` service block and the `volumes:` section are not modified.
  - Acceptance: `docker-compose.yml` `openclaw-agent` service contains `env_file: ./secrets/.env.anthropic`. The `openclaw-core` service block is byte-identical to the P0-T5 baseline snapshot.

---

### Phase 2 — OpenClaw Workspace Files

**Purpose:** Create the OpenClaw-native workspace layout files (`IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`, `skills/mailbridge_admin/SKILL.md`, `openclaw.json`) and delete the v1 placeholder files (`SYSTEM.md`, `config.yaml`).

- [x] [P2-T1] Create `deploy/docker/openclaw-assistant/IDENTITY.md`
  - Preconditions: None (independent of Phase 1)
  - File: `deploy/docker/openclaw-assistant/IDENTITY.md` (new file)
  - Required content elements:
    - Agent name: `Admin Assistant` (display name) with internal id `admin-assistant`
    - Role: Administrative assistant for the operator; triages mail and calendar data from the OpenClaw MailBridge
    - Tone: Calm, precise, conservative
  - Acceptance: File exists at the specified path. It states the name `Admin Assistant`, the id `admin-assistant`, and the role and tone. File does not exceed 500 lines (trivially satisfied).

- [x] [P2-T2] Create `deploy/docker/openclaw-assistant/SOUL.md`
  - Preconditions: None
  - File: `deploy/docker/openclaw-assistant/SOUL.md` (new file)
  - Required content elements (must appear in this priority order):
    1. Avoid false claims
    2. Protect private calendar and email details
    3. Surface urgent items and scheduling conflicts
    4. Draft concise proposed replies and decisions
    5. Never claim to have sent an email, changed a calendar event, or taken any write action unless a future write plane explicitly confirms success
  - Additional required element: If data is redacted or missing, state that plainly. Do not infer or fabricate missing fields.
  - Acceptance: File exists at the specified path. All five priorities are present in stated order. The no-write-claim contract is explicit. File does not exceed 500 lines.

- [x] [P2-T3] Create `deploy/docker/openclaw-assistant/USER.md`
  - Preconditions: None
  - File: `deploy/docker/openclaw-assistant/USER.md` (new file)
  - Required content elements:
    - Operator role description: the operator runs the Docker Desktop deployment and needs actionable visibility into mail and calendar data
    - Context of use: daily standup prep or end-of-day triage; expects summaries and flagged anomalies, not autonomous action
    - Constraint: HostAdapter API is read-only; the assistant cannot modify, reply to, or delete items
    - Placeholder note: actual operator-specific details (name, organization) should be filled in by the deploying operator
  - Acceptance: File exists at the specified path. Contains an operator profile section covering role, context, and constraint. Includes a placeholder notice directing the operator to customize the file. File does not exceed 500 lines.

- [x] [P2-T4] Create `deploy/docker/openclaw-assistant/AGENTS.md`
  - Preconditions: None
  - File: `deploy/docker/openclaw-assistant/AGENTS.md` (new file)
  - Required content elements:
    **Session-start protocol** (must appear as a numbered list):
    1. Read `SOUL.md`, `USER.md`, and `TOOLS.md` from the workspace
    2. Call `GET /v1/status` and stop and report if the bridge is not ready
    3. Pull baseline window: meeting requests from the last 7 days, recent messages from the last 24 hours, calendar events for the next 14 days
    4. Expand individual items (`GET /v1/messages/{bridgeId}`, `GET /v1/events/{bridgeId}`) only for entries that warrant closer review
    **Primary jobs** (must be listed):
    - Triage meeting requests
    - Summarize urgent inbox items
    - Identify scheduling conflicts and unanswered scheduling items
    - Propose reply drafts and scheduling recommendations
    **Decision labels** (must appear as a labeled list with description):
    - `IGNORE` — item requires no action
    - `PRIVATE_BUSY_ONLY` — item is private; block time as busy, no detail shared
    - `PROTECTED_MEETING` — meeting must not be moved or declined without explicit approval
    - `HUMAN_APPROVAL` — item requires explicit operator decision before any action
    - `AUTO_COORDINATE` — safe to recommend a coordination action; never permission to send, reschedule, or take any write action, because the HostAdapter API is read-only
    **Output format** (must be stated as a required structure for every triage or summary response):
    1. Executive summary
    2. Items needing action
    3. Proposed drafts / next steps
    4. Unknowns / missing data
  - Acceptance: File exists at the specified path. Session-start protocol, decision labels (all five), and 4-part output format are all present with the required wording. `AUTO_COORDINATE` explicitly states recommendation-only semantics. File does not exceed 500 lines.

- [x] [P2-T5] Create `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`
  - Preconditions: None
  - File: `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` (new file; parent directory `skills/mailbridge_admin/` must be created)
  - Required content elements:
    - YAML frontmatter block (between `---` delimiters) containing:
      - `name: mailbridge_admin`
      - `description: Read Outlook inbox, meeting requests, and calendar from the OpenClaw HostAdapter HTTP API.`
      - `metadata.openclaw.os: ["windows"]`
    - Body section: trigger condition (when the user asks about inbox, scheduling, calendar conflicts, meeting requests, or drafting an admin response)
    - Required workflow (numbered):
      1. Call `GET /v1/status` on the HostAdapter. If not ready, stop and report.
      2. Use list endpoints first (`/v1/messages`, `/v1/meeting-requests`, `/v1/calendar`).
      3. Use get endpoints only for items identified as relevant (`/v1/messages/{bridgeId}`, `/v1/events/{bridgeId}`).
      4. Respect redaction: if `isRedacted=true` or mode is `safe`, state that details are unavailable; never fabricate sender, body, or attendee details.
      5. The bridge is read-only: never claim to have replied, sent, created, updated, accepted, declined, or rescheduled anything.
    - Example HTTP patterns for each tool (referencing `http://host.docker.internal:4319/v1` and `Authorization: Bearer` header)
  - Acceptance: File exists at the specified path. YAML frontmatter is syntactically valid and contains all three required fields. The body includes the trigger condition, the numbered workflow, and HTTP example patterns. No references to `OpenClaw.MailBridge.Client.exe` or named-pipe access. File does not exceed 500 lines.

- [x] [P2-T6] Create `deploy/docker/openclaw-assistant/openclaw.json` as a placeholder configuration file
  - Preconditions: None
  - File: `deploy/docker/openclaw-assistant/openclaw.json` (new file)
  - Required content elements (all keys flagged as placeholder with inline comments):
    - `gateway.mode: "local"` — required for local Gateway operation
    - `gateway.port: 18789` — matches `OPENCLAW_AGENT_PORT` default
    - `gateway.bind: "loopback"` — loopback-only per security posture
    - `gateway.auth.mode: "token"` — token auth required even on loopback
    - `session.dmScope: "per-channel-peer"` — secure DM isolation
    - `tools.profile: "minimal"` — smallest default tool surface
    - `agents.defaults.model.primary: "anthropic/claude-opus-4-6"` — primary model
    - `agents.defaults.model.fallbacks: ["anthropic/claude-sonnet-4-6"]` — fallback model
    - `agents.list` entry for `admin-assistant` with:
      - `id: "admin-assistant"`
      - `workspace: "/workspace"` (container path)
      - `skills: ["mailbridge_admin"]`
    - A header comment block stating that this file is a placeholder, the exact schema must be verified against `https://docs.openclaw.ai/gateway/configuration-reference` before production use, and all values are unverified defaults
  - Acceptance: File exists at the specified path. All required keys are present. Header comment clearly marks the file as a placeholder requiring verification. The `admin-assistant` agent entry is present with `skills: ["mailbridge_admin"]`. File does not exceed 500 lines.

- [x] [P2-T7] Delete `deploy/docker/openclaw-assistant/SYSTEM.md`
  - Preconditions: P2-T2 complete (SOUL.md and AGENTS.md provide the replacement content)
  - File to delete: `deploy/docker/openclaw-assistant/SYSTEM.md`
  - Rationale: `SYSTEM.md` was the v1 monolithic system instructions file. v2 replaces it with the OpenClaw-native split layout (`IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`). Keeping both creates a conflict in what the runtime loads.
  - Acceptance: `deploy/docker/openclaw-assistant/SYSTEM.md` does not exist in the working tree. No other committed file references this path in a load or import context.

- [x] [P2-T8] Delete `deploy/docker/openclaw-assistant/config.yaml`
  - Preconditions: P2-T6 complete (`openclaw.json` provides the replacement)
  - File to delete: `deploy/docker/openclaw-assistant/config.yaml`
  - Rationale: `config.yaml` was the v1 placeholder configuration file. v2 replaces it with `openclaw.json`, which matches the OpenClaw native config format. Keeping both creates operator confusion.
  - Acceptance: `deploy/docker/openclaw-assistant/config.yaml` does not exist in the working tree. No other committed file references this path in a load or import context.

---

### Phase 3 — QA Validation

**Purpose:** Run all compose validation commands unconditionally, verify security posture, confirm regression safety for `openclaw-core`, and check all 19 ACs from `v2/user-story.md`.

- [x] [P3-T1] Run `docker compose --env-file .env.example -f docker-compose.yml config` and verify clean output
  - Preconditions: All Phase 1 and Phase 2 tasks complete
  - Command: `docker compose --env-file .env.example -f docker-compose.yml config`
  - Acceptance: Command exits with code 0. Parsed output contains both `openclaw-core` and `openclaw-agent` service definitions. Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/qa-gates/qa-compose-config.md` containing:
    - `Timestamp:`
    - `Command: docker compose --env-file .env.example -f docker-compose.yml config`
    - `EXIT_CODE: 0`
    - `Output Summary:` (full rendered output or key service excerpts confirming both services present)

- [x] [P3-T2] Run `docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config` and verify clean dev compose output
  - Preconditions: P3-T1 complete
  - Command: `docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config`
  - Acceptance: Command exits with code 0. Parsed output contains both services with dev overrides applied (including `extra_hosts: host.docker.internal:host-gateway` on `openclaw-agent`). Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/qa-gates/qa-compose-dev-config.md` containing:
    - `Timestamp:`
    - `Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config`
    - `EXIT_CODE: 0`
    - `Output Summary:`

- [x] [P3-T3] Verify `openclaw-agent` security posture in parsed compose config output
  - Preconditions: P3-T1 complete (P3-T1 artifact available)
  - Acceptance: The `openclaw-agent` service block in the P3-T1 output confirms all of the following are present:
    - `read_only: true`
    - `cap_drop: [ALL]` (or equivalent list form)
    - `security_opt: [no-new-privileges:true]`
    - `user:` set to a non-root value (e.g., `1654:1654`)
    - `ports:` published as `127.0.0.1:<port>:18789` (loopback only; no `0.0.0.0` binding)
    - Token file bind mount at `/run/openclaw/hostadapter.token` with `read_only: true`
    - `env_file:` entry referencing `./secrets/.env.anthropic`
  - Evidence: Record verification findings inline within the `qa-compose-config.md` artifact from P3-T1 under a `## Security Posture Verification` section.

- [x] [P3-T4] Verify `openclaw-core` service definition is unchanged from baseline
  - Preconditions: P0-T5 complete (baseline snapshot available), P3-T1 complete
  - Acceptance: The `openclaw-core` service block extracted from the P3-T1 rendered config output is semantically identical to the baseline snapshot captured in P0-T5. Any diff must show zero semantic changes. Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/qa-gates/qa-core-regression.md` containing:
    - `Timestamp:`
    - Diff output or explicit "identical" confirmation
    - `EXIT_CODE: 0` (or `IDENTICAL` notation if no diff command is used)

- [x] [P3-T5] Verify all 19 acceptance criteria from `v2/user-story.md` are satisfied with file-level evidence
  - Preconditions: All Phase 1, Phase 2, and P3-T1 through P3-T4 tasks complete
  - Acceptance: Evidence artifact at `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/qa-gates/qa-acceptance-criteria.md` listing each of the 19 ACs with `PASS` or `FAIL` status and the supporting file path and specific element for each:
    1. `docker-compose.yml` defines a new `openclaw-agent` service distinct from `openclaw-core` — verified against `docker-compose.yml` service list
    2. `docker-compose.dev.yml` includes dev-mode `openclaw-agent` definition — verified against `docker-compose.dev.yml`
    3. `.env.example` documents `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, `OPENCLAW_AGENT_WORKSPACE` — verified against `.env.example`
    4. New service mounts token file read-only at `/run/openclaw/hostadapter.token` — verified in compose config output
    5. New service uses `host.docker.internal` to reach HostAdapter — verified in `OpenClaw__HostAdapter__BaseUrl` environment variable value
    6. New service follows security posture: loopback-only ports, non-root user, read-only root FS, `cap_drop: ALL` — verified in P3-T3
    7. Tool/skill definitions use HTTP calls, not CLI exec — verified against `deploy/docker/openclaw-assistant/TOOLS.md` and `skills/mailbridge_admin/SKILL.md`
    8. Existing `openclaw-core` service definition unchanged — verified in P3-T4
    9. Documentation updated (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`) — file existence and content check for the `openclaw-agent` references
    10. System instructions enforce read-only behavior, no-write claims, redaction awareness — verified against `SOUL.md` and `AGENTS.md` content
    11. Assistant identified as `admin-assistant` across workspace and config — verified across `IDENTITY.md`, `AGENTS.md`, `openclaw.json`
    12. Workspace uses OpenClaw-native layout: `IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`, `TOOLS.md`, and `skills/mailbridge_admin/SKILL.md` — file existence check
    13. `AGENTS.md` defines session-start protocol (status check, 7d meeting requests, 24h messages, 14d calendar) — verified against `AGENTS.md` content
    14. `AGENTS.md` defines all five decision labels with `AUTO_COORDINATE` scoped to recommendation only — verified against `AGENTS.md` content
    15. `AGENTS.md` requires 4-part output format for every triage/summary response — verified against `AGENTS.md` content
    16. Skill file uses OpenClaw YAML frontmatter (`name`, `description`, `metadata.openclaw.os`) — verified against `skills/mailbridge_admin/SKILL.md` frontmatter
    17. Placeholder `openclaw.json` documents intended gateway/agent config and is flagged for verification — verified against `openclaw.json` header comment and key structure
    18. `ANTHROPIC_API_KEY` supplied via gitignored `env_file`, never committed, never baked into image layers — verified: `.gitignore` contains `secrets/`, `docker-compose.yml` contains `env_file: ./secrets/.env.anthropic`, `secrets/.env.anthropic` is gitignored and contains only a placeholder value
    19. `.env.example` documents `ANTHROPIC_API_KEY` (empty) and `OPENCLAW_AGENT_MODEL` — verified against `.env.example` content
  - If any AC is `FAIL`, the artifact must include a specific remediation step before the task can be marked complete.
