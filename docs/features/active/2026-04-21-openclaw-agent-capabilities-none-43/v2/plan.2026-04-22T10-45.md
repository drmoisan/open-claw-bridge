# 2026-04-21-openclaw-agent-capabilities-none (Plan)

- **Issue:** #43
- **Parent:** v1 (`plan.2026-04-21T14-00.md`, all 8 ACs PASS)
- **Owner:** drmoisan
- **Last Updated:** 2026-04-22T10-45
- **Status:** Approved
- **Version:** 2.0
- **Work Mode:** full-bug
- **Feature Folder:** `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2`

## Overview

The v1 fix resolved the ACP runtime startup failure and removed the `DashboardAuth` probe false-positive. One root cause remains: `"tools": { "profile": "minimal" }` in `deploy/docker/openclaw-assistant/openclaw.json` restricts every agent session to `session_status` only, preventing the agent from executing `exec`/`bash` calls to the HostAdapter. The fix is a single-field change (`"minimal"` → `"coding"`), followed by an image rebuild and container recreation. No PowerShell, C#, or `docker-compose.yml` files are modified.

**Fail-closed evidence rule:** Evidence artifacts are required for every baseline and verification command step. An artifact is incomplete unless it contains `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`. Do not mark any evidence-backed task complete without the artifact present at its specified path.

**Scope constraint:** The only production file change is `deploy/docker/openclaw-assistant/openclaw.json` line 15. No PowerShell toolchain pass and no C# toolchain pass are required or permitted. `docker-compose.yml` must not be modified. Dockerfile v1 changes (`@zed-industries/codex-acp` global install, `CODEX_HOME`, `NPM_CONFIG_CACHE`) must be preserved.

---

### Phase 0 — Baseline Capture

- [x] [P0-T1] Create `phase0-instructions-read.md` in the v2 feature folder recording all mandatory policy files read in compliance order
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/phase0-instructions-read.md`
  - Acceptance: File exists and contains `Timestamp:` (ISO-8601), `Policy Order:`, and an explicit list of every file read — at minimum `.github/copilot-instructions.md`, `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`.

- [x] [P0-T2] Read `.github/copilot-instructions.md` per policy compliance order
  - Acceptance: File content has been read in full; its path appears in `phase0-instructions-read.md` under `Policy Order:`.

- [x] [P0-T3] Read `.github/instructions/general-code-change.instructions.md`
  - Acceptance: File content has been read in full; its path appears in `phase0-instructions-read.md` under `Policy Order:`.

- [x] [P0-T4] Read `.github/instructions/general-unit-test.instructions.md`
  - Acceptance: File content has been read in full; its path appears in `phase0-instructions-read.md` under `Policy Order:`.

- [x] [P0-T5] Capture `git status` pre-fix baseline to artifact
  - Command: `git status`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/baseline-git-status.2026-04-22.md`
  - Acceptance: Artifact exists; contains `Timestamp:`, `Command: git status`, `EXIT_CODE: 0`, `Output Summary:` with working-tree state (clean or list of modified files).

- [x] [P0-T6] Capture `git log -1` pre-fix commit reference to artifact
  - Command: `git log -1`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/baseline-git-log.2026-04-22.md`
  - Acceptance: Artifact exists; contains `Timestamp:`, `Command: git log -1`, `EXIT_CODE: 0`, `Output Summary:` with commit hash and message.

- [x] [P0-T7] Confirm the current tools profile value is `"minimal"` via grep (pre-fix state)
  - Command: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/baseline-profile-grep.2026-04-22.md`
  - Acceptance: Command output contains `"profile": "minimal"` (exit code 0); artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary: "profile": "minimal" confirmed on line 15`.

- [x] [P0-T8] Capture `docker inspect openclaw-agent` pre-fix container baseline to artifact
  - Command: `docker inspect openclaw-agent`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/baseline-docker-inspect.2026-04-22.md`
  - Acceptance: Artifact exists; contains `Timestamp:`, `Command: docker inspect openclaw-agent`, `EXIT_CODE:` (0 if container is running, non-zero if not), `Output Summary:` stating container running state and noting the hardening fields (`ReadonlyRootfs`, `CapDrop`, `SecurityOpt`) as observed in the pre-fix state.

---

### Phase 1 — Implementation

- [x] [P1-T1] Change `"profile": "minimal"` to `"profile": "coding"` on line 15 of `deploy/docker/openclaw-assistant/openclaw.json`
  - Acceptance: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json` returns `"profile": "coding"` (exit code 0); `git diff deploy/docker/openclaw-assistant/openclaw.json` shows exactly one line changed — the `"minimal"` value replaced by `"coding"` — with no other modifications to that file.

- [x] [P1-T2] Confirm that only `deploy/docker/openclaw-assistant/openclaw.json` is modified and no other source files changed
  - Command: `git status`
  - Acceptance: `git status` output lists only `deploy/docker/openclaw-assistant/openclaw.json` as modified; no PowerShell (`.ps1`/`.psm1`), C# (`.cs`/`.csproj`), Dockerfile, or `docker-compose.yml` files appear as changed.

---

### Phase 2 — Docker Build and Recreation

- [x] [P2-T1] Rebuild the `openclaw-agent` Docker image with the updated seed configuration
  - Command: `docker compose build openclaw-agent`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/docker-build.2026-04-22.md`
  - Acceptance: Command exits with code 0; artifact contains `Timestamp:`, `Command: docker compose build openclaw-agent`, `EXIT_CODE: 0`, `Output Summary:` confirming the build completed without errors.

- [x] [P2-T2] Recreate the `openclaw-agent` container from the rebuilt image using force-recreate
  - Command: `docker compose up -d --force-recreate openclaw-agent`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/docker-recreate.2026-04-22.md`
  - Acceptance: Command exits with code 0; artifact contains `Timestamp:`, `Command: docker compose up -d --force-recreate openclaw-agent`, `EXIT_CODE: 0`, `Output Summary:` confirming the container was recreated.

---

### Phase 3 — Verification

- [x] [P3-T1] Verify the openclaw gateway started cleanly via container log inspection
  - Command: `docker compose logs openclaw-agent`
  - Secondary command: `docker compose logs openclaw-agent | grep -c "embedded acpx runtime backend probe failed"`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/verify-gateway-logs.2026-04-22.md`
  - Acceptance: Primary log output contains a `gateway` ready or `plugins ... ready` line; secondary command returns `0`; artifact contains `Timestamp:`, both commands, `EXIT_CODE: 0` for each, `Output Summary:` with the ready-line excerpt and the zero-failure-count value.

- [x] [P3-T2] Confirm all six container hardening tokens are preserved via `docker inspect`
  - Command: `docker inspect openclaw-agent`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/verify-hardening.2026-04-22.md`
  - Acceptance: Inspect output contains `"ReadonlyRootfs": true`, `"CapDrop": ["ALL"]`, and `"SecurityOpt": ["no-new-privileges:true"]`; artifact records each of the three fields by name with their confirmed values.

- [x] [P3-T3] Confirm the runtime configuration at `/.openclaw/openclaw.json` inside the container has `"profile": "coding"`
  - Command: `docker compose exec openclaw-agent grep '"profile"' /.openclaw/openclaw.json`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/verify-profile-in-container.2026-04-22.md`
  - Acceptance: Command returns `"profile": "coding"` (exit code 0); artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary: "profile": "coding" confirmed inside container at /.openclaw/openclaw.json`.

- [x] [P3-T4] Confirm `docker-compose.yml` is unchanged relative to the development branch
  - Command: `git diff development HEAD -- docker-compose.yml`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/verify-compose-unchanged.2026-04-22.md`
  - Acceptance: Command produces empty output (exit code 0, zero diff lines); artifact contains `Timestamp:`, `Command: git diff development HEAD -- docker-compose.yml`, `EXIT_CODE: 0`, `Output Summary: diff is empty — docker-compose.yml unchanged`.

- [x] [P3-T5] Verify agent exec/bash tool availability by performing a manual calendar query and capturing the full response as evidence
  - Action: With the full Docker Compose stack running, ask the agent "When is my next available 60-minute window?" via the gateway interface
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/verify-agent-capability.2026-04-22.md`
  - Acceptance: Artifact exists at the above path; artifact records `Timestamp:`, the exact question asked, the agent response excerpt, and a `Result:` field whose value is `PASS`; the response excerpt does NOT contain the strings `"no execution capabilities"`, `"capabilities=none"`, or `"cannot call the HostAdapter"`; the response contains calendar availability data confirming the agent called `GET http://host.docker.internal:4319/v1/calendar`.

---

### Phase 4 — Final QA

- [x] [P4-T1] Update `orchestrator-state.json` in the v2 feature folder with v2 completion status and evidence artifact paths
  - Acceptance: File at `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/orchestrator-state.json` exists (or is updated if already present) and contains `"phase": "complete"`, `"version": "2.0"`, `"status": "PASS"`, and the relative paths of all Phase 3 evidence artifacts (`verify-gateway-logs.2026-04-22.md`, `verify-hardening.2026-04-22.md`, `verify-profile-in-container.2026-04-22.md`, `verify-compose-unchanged.2026-04-22.md`, `verify-agent-capability.2026-04-22.md`).

- [x] [P4-T2] Check off AC-1-v2 through AC-5-v2 in `spec.md` and reference the corresponding evidence artifact for each
  - Acceptance: All five `- [ ]` checkboxes in the `## Acceptance Criteria` section of `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/spec.md` are updated to `- [x]`; each checked AC references its Phase 3 evidence artifact by filename.

- [x] [P4-T3] Confirm `issue.md` v1 AC history (AC-1 through AC-8) remains intact and unmodified
  - Command: `grep -c "\- \[x\]" docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/issue.md`
  - Acceptance: Command returns `8`; no v1 AC checkbox has been altered or unchecked.

- [x] [P4-T4] Confirm Dockerfile v1 changes are intact — `codex-acp` install, `CODEX_HOME`, and `NPM_CONFIG_CACHE` env vars all present
  - Commands:
    - `grep -c "codex-acp" deploy/docker/openclaw-agent.Dockerfile`
    - `grep -c "CODEX_HOME" deploy/docker/openclaw-agent.Dockerfile`
    - `grep -c "NPM_CONFIG_CACHE" deploy/docker/openclaw-agent.Dockerfile`
  - Evidence artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/verify-dockerfile-v1.2026-04-22.md`
  - Acceptance: All three grep commands return a count ≥ 1 (exit code 0 for each); artifact contains `Timestamp:`, each command, each `EXIT_CODE: 0`, `Output Summary:` confirming all three v1 Dockerfile tokens are present.

---

## Requirements Traceability

| AC | Phase | Task(s) | Evidence Artifact(s) |
|---|---|---|---|
| AC-1-v2 | Phase 3 | P3-T5 | `verify-agent-capability.2026-04-22.md` |
| AC-2-v2 | Phase 3 | P3-T5 | `verify-agent-capability.2026-04-22.md` |
| AC-3-v2 | Phase 3 | P3-T2, P3-T4 | `verify-hardening.2026-04-22.md`, `verify-compose-unchanged.2026-04-22.md` |
| AC-4-v2 | Phase 1, Phase 3 | P1-T1, P3-T3 | `verify-profile-in-container.2026-04-22.md` |
| AC-5-v2 | Phase 3, Phase 4 | P3-T1, P4-T4 | `verify-gateway-logs.2026-04-22.md`, `verify-dockerfile-v1.2026-04-22.md` |

---

## Implementation Notes

- No PowerShell toolchain pass is required. No `.ps1` or `.psm1` files are modified.
- No C# toolchain pass is required. No `.cs` or `.csproj` files are modified.
- `docker-compose.yml` must not be modified at any point during execution. P3-T4 verifies this.
- Dockerfile v1 changes (`@zed-industries/codex-acp` global install, `ENV CODEX_HOME=/workspace/.codex`, `ENV NPM_CONFIG_CACHE=/workspace/.npm-cache`) must not be removed or altered.
- If `docker compose build` or `docker compose up` exits non-zero, stop and record the error in the evidence artifact before proceeding. Do not force-pass any task.
- If `docker compose logs` shows `embedded acpx runtime backend probe failed`, stop and diagnose before marking Phase 3 complete.
