# Feature Audit — openclaw-agent-docker v2 (Issue #30)

- **Timestamp:** 2026-04-18T00-00
- **Branch:** `feature/openclaw-agent-docker-30`
- **Auditor:** Feature Review Agent
- **Work Mode:** full-feature
- **AC Sources:** `v2/spec.md` and `v2/user-story.md`
- **Evidence base:** `docs/features/active/2026-04-16-openclaw-agent-docker-30/evidence/v2/qa-gates/qa-acceptance-criteria.md`

---

## Summary

All 19 acceptance criteria from `v2/user-story.md` evaluate as PASS. All spec Definition of Done items evaluate as PASS. No acceptance criteria are FAIL or UNVERIFIED.

Overall verdict: **PASS**

No remediation is required.

---

## Assumptions and Scope Notes

The following assumptions were applied during this audit:

1. **No source code changes.** This feature is limited to Docker Compose files, environment configuration, assistant instruction files, and documentation. Policy requirements that apply only to source code (toolchain loop, coverage, unit tests) are documented as not applicable.

2. **`secrets/.env.anthropic` absence from git is correct.** The file exists in the working tree but is gitignored by design. Its absence from git tracking is not a defect (AC-18 explicitly requires this behavior).

3. **`openclaw.json` placeholder values are intentional.** The `_placeholder` key and explicit schema-unverified notice are required by AC-17 and documented in the plan's Open Questions. The presence of placeholder values does not constitute a failing AC.

4. **Live stack validation is out of scope.** The plan explicitly defers live `docker compose up`, HostAdapter connectivity, token authentication, and OpenClaw runtime verification to environment-dependent post-merge validation. `docker compose config` (EXIT_CODE 0) is the applicable automated QA gate.

5. **v1 deliverables (already committed).** AC items 1–9 and AC-10 partially depend on v1 deliverables. This audit evaluates the complete feature as of the current working tree state, including both v1 (committed) and v2 (working tree) changes.

---

## AC Evaluation — `v2/user-story.md`

| # | AC Text | Verdict | Evidence |
|---|---|---|---|
| 1 | `docker-compose.yml` defines a new service (distinct from `openclaw-core`) for the external OpenClaw assistant runtime | PASS | `docker-compose.yml` lines 52–85 define `openclaw-agent` as a distinct service alongside `openclaw-core`. Both services appear in compose config output (EXIT_CODE 0). |
| 2 | `docker-compose.dev.yml` includes a corresponding dev-mode service definition for the assistant | PASS | `docker-compose.dev.yml` lines 29–31 define `openclaw-agent` override with `extra_hosts: host.docker.internal:host-gateway`. Confirmed in QA dev config evidence. |
| 3 | `.env.example` documents new configuration keys required by the assistant service (image, port, workspace path) | PASS | `.env.example` (working tree) contains `OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest`, `OPENCLAW_AGENT_PORT=18789`, `OPENCLAW_AGENT_WORKSPACE=./deploy/docker/openclaw-assistant`. |
| 4 | The new service mounts the HostAdapter token file read-only at `/run/openclaw/hostadapter.token` | PASS | `docker-compose.yml` `openclaw-agent` volumes: `target: /run/openclaw/hostadapter.token`, `read_only: true`. Confirmed in compose config evidence. |
| 5 | The new service uses `host.docker.internal` to reach `HostAdapter` on the Windows host | PASS | `docker-compose.yml` `openclaw-agent` environment: `OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}`. Resolved in compose config evidence. |
| 6 | The new service follows the existing Docker security posture: loopback-only port publishing, non-root user, read-only root filesystem | PASS | Security posture verification in `qa-compose-config.md`: `host_ip: 127.0.0.1`, `user: 1654:1654`, `read_only: true`, `cap_drop: ALL`, `security_opt: no-new-privileges:true` — all PASS. |
| 7 | Assistant tool/skill definitions use HTTP calls to HostAdapter endpoints instead of CLI `exec` against `OpenClaw.MailBridge.Client.exe` | PASS | `TOOLS.md` defines six HTTP tools using `http://host.docker.internal:4319/v1`. `SKILL.md` uses the same HTTP patterns. No reference to `OpenClaw.MailBridge.Client.exe` in either file. |
| 8 | Existing `openclaw-core` service definition and its functionality remain unchanged after the addition | PASS | QA regression evidence (`qa-core-regression.md`) confirms `openclaw-core` block is semantically identical to the Phase 0 baseline snapshot. Confirmed via direct diff of baseline vs post-change compose config output. |
| 9 | Documentation (`README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`) updated to reflect the new service and its relationship to the existing topology | PASS | All three files contain references to `openclaw-agent`. Verified via grep in QA acceptance criteria evidence. |
| 10 | The assistant's system instructions enforce read-only behavior, no-write claims, and redaction awareness | PASS | `SOUL.md` priority 5 prohibits write claims; states plainly to report redacted data without inference. `AGENTS.md` `AUTO_COORDINATE` label explicitly states "never permission to send, reschedule, or take any write action". `SKILL.md` step 5 restates the no-write contract; step 4 states redaction handling. |
| 11 | The assistant is identified as `admin-assistant` across its workspace and config | PASS | `IDENTITY.md`: `Agent ID: admin-assistant`. `AGENTS.md` heading: "Administrative Assistant" (display) with internal id consistent via `openclaw.json`. `openclaw.json`: `"id": "admin-assistant"`. Consistent across all three. |
| 12 | The assistant workspace uses OpenClaw-native layout: `IDENTITY.md`, `SOUL.md`, `USER.md`, `AGENTS.md`, `TOOLS.md`, and `skills/mailbridge_admin/SKILL.md` | PASS | All six files are present in the working tree under `deploy/docker/openclaw-assistant/`. Confirmed via directory listing. |
| 13 | `AGENTS.md` defines a session-start protocol (status check, then 7-day meeting-request / 24-hour message / 14-day calendar baseline pulls) | PASS | `AGENTS.md` Session-Start Protocol: step 2 calls `GET /v1/status` with stop-on-not-ready; step 3 specifies "Meeting requests from the last 7 days", "Recent messages from the last 24 hours", "Calendar events for the next 14 days". All three windows are explicitly stated. |
| 14 | `AGENTS.md` defines the decision labels `IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`, with `AUTO_COORDINATE` scoped to recommendation only | PASS | All five labels are defined in the Decision Labels section. `AUTO_COORDINATE`: "this label is never permission to send, reschedule, or take any write action, because the HostAdapter API is read-only." Recommendation-only scoping is explicit. |
| 15 | `AGENTS.md` requires every triage/summary response to follow a 4-part output format: Executive summary, Items needing action, Proposed drafts / next steps, Unknowns / missing data | PASS | Required Output Format section lists all four parts in the stated order. The section is labeled "Required Output Format" and the preamble states "Every triage or summary response must follow this four-part structure." |
| 16 | The skill file uses OpenClaw YAML frontmatter (`name`, `description`, `metadata.openclaw.os`) and encodes the required workflow | PASS | `SKILL.md` YAML frontmatter (lines 1–7) contains `name: mailbridge_admin`, `description: Read Outlook inbox, meeting requests, and calendar from the OpenClaw HostAdapter HTTP API.`, `metadata.openclaw.os: ["windows"]`. Required workflow steps 1–5 are present in body. |
| 17 | A placeholder `openclaw.json` documents the intended gateway/agent config (`gateway.mode=local`, loopback bind, token auth, `session.dmScope=per-channel-peer`, `tools.profile=minimal`, `admin-assistant` agent entry) and is flagged for verification | PASS | `openclaw.json` contains `_placeholder` verification warning. All required keys present: `gateway.mode=local`, `gateway.bind=loopback`, `gateway.auth.mode=token`, `session.dmScope=per-channel-peer`, `tools.profile=minimal`, `agents.list[0].id=admin-assistant`, `skills: ["mailbridge_admin"]`. |
| 18 | The assistant's Anthropic credential (`ANTHROPIC_API_KEY`) is supplied via a gitignored `env_file`, never committed, and never baked into image layers | PASS | `.gitignore` line 70: `secrets/`. `docker-compose.yml` `openclaw-agent` service: `env_file: ./secrets/.env.anthropic`. `secrets/.env.anthropic` contains only `ANTHROPIC_API_KEY=` (empty). File does not appear in `git status` as staged or tracked. No key in committed `.env.example`, `docker-compose.yml`, or workspace files. |
| 19 | `.env.example` documents `ANTHROPIC_API_KEY` (empty placeholder) and `OPENCLAW_AGENT_MODEL`, pointing operators to the official OpenClaw Anthropic provider docs for the current model ID | PASS | `.env.example` (working tree) contains `ANTHROPIC_API_KEY=` with comment "Supply the real value in secrets/.env.anthropic (gitignored). Never commit the real key here." and `OPENCLAW_AGENT_MODEL=anthropic/claude-opus-4-6` with comment referencing `docs.openclaw.ai/providers/anthropic`. |

---

## Spec Definition of Done — `v2/spec.md`

The spec `## Definition of Done` section contains 17 items (checkbox list). All are checked `[x]` in the source document. This audit independently verified each against working tree evidence. All 17 items evaluate as PASS, consistent with the source document state.

Items that overlap with `user-story.md` ACs (1–19 above) are not repeated here in detail. The additional DoD items not directly mapped to user-story ACs are:

| DoD Item | Verdict | Evidence |
|---|---|---|
| `docker compose config` validates both compose files without errors | PASS | QA compose config evidence: EXIT_CODE 0 for both production and dev configs. |
| Naming distinction between repo `OpenClaw.Core` and external OpenClaw assistant runtime is documented | PASS | `README.md`, `docs/architecture-diagrams.md`, and `docs/mailbridge-runbook.md` all updated. The distinction is addressed in the spec overview and referenced in the plan. |

---

## Acceptance Criteria Status

- Source: `docs/features/active/2026-04-16-openclaw-agent-docker-30/v2/user-story.md`
- Total AC items: 19
- Checked off (delivered): 19
- Remaining (unchecked): 0
- Items remaining: none

All AC items in `v2/user-story.md` were already marked `[x]` prior to this audit (checked off during plan execution). This audit independently verified each item and found no discrepancies. No new check-offs were required.

---

## Outstanding Items (Non-Blocking)

These items do not block a PASS verdict but must be addressed by the operator before production use:

1. **Verify `openclaw.json` schema.** All values in `openclaw.json` are unverified placeholders. The schema must be validated against `https://docs.openclaw.ai/gateway/configuration-reference` before the stack is run in production.

2. **Verify `OPENCLAW_AGENT_IMAGE`.** The placeholder value `ghcr.io/openclaw/openclaw:latest` must be confirmed against official OpenClaw documentation. Pin a specific version tag rather than `latest` for production deployments.

3. **Verify non-root UID compatibility.** The `user: "1654:1654"` assignment in `docker-compose.yml` must be verified against the external OpenClaw image's expected runtime user. If the image requires a different UID, update the service definition accordingly.

4. **Commit all v2 changes.** The v2 deliverables are present in the working tree but not all changes are committed. The `.gitignore` patch, `.env.example` additions, `docker-compose.yml` `env_file` stanza, and all workspace files should be committed to the feature branch before merge.

5. **Live stack validation.** `docker compose config` (EXIT_CODE 0) is the only automated gate that has run. Live validation (container start, HostAdapter connectivity, token authentication, OpenClaw runtime health) requires a running HostAdapter and a real `ANTHROPIC_API_KEY`. This is deferred to post-merge operator verification.
