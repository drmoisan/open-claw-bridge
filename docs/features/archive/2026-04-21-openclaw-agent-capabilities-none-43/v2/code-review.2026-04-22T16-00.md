# Code Review: openclaw-agent tools profile fix (Issue #43 v2)

---

**Review Date:** 2026-04-22
**Reviewer:** GitHub Copilot (feature_code_review_agent)
**Feature Folder:** `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2`
**Base Branch:** `development` (commit `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`)
**Head Branch:** `bug/openclaw-agent-capabilities-none-43` (commit `97cde32be6ecd54b3295dec743dcfc16e542f343`)
**Review Type:** Initial review (post-implementation)

---

## Executive Summary

This review covers the v2 change for Issue #43: a single JSON seed file value update from `"profile": "minimal"` to `"profile": "coding"` in `deploy/docker/openclaw-assistant/openclaw.json`. The change is minimal and directly targeted at the confirmed root cause — the `minimal` profile restricts all agent sessions to `session_status` only, preventing the agent from executing bash/curl calls to the HostAdapter.

The implementation is clean. No Dockerfile, no `docker-compose.yml`, no PowerShell or C# files were modified. The Docker image was rebuilt and the container was recreated. All automated post-fix checks passed with EXIT_CODE 0. One critical operational gap exists: the production file change is unstaged (the diff is in the working tree, not committed to the branch), which means no PR can be opened in the current state.

**What changed:**
One line in `deploy/docker/openclaw-assistant/openclaw.json` (line 14): `"profile": "minimal"` → `"profile": "coding"`. The v1 Dockerfile tokens (`codex-acp`, `CODEX_HOME`, `NPM_CONFIG_CACHE`) and all container hardening configuration in `docker-compose.yml` are preserved and confirmed by dedicated verification artifacts.

**Top 3 risks:**
1. The production file is unstaged. If the working tree is cleaned or reset before committing, the change is lost.
2. AC-1-v2 and AC-2-v2 (agent executing live HostAdapter calls) have not yet been manually verified. The `coding` profile enables exec/bash tools; whether the end-to-end HostAdapter call succeeds depends on the token being available at `/run/openclaw/hostadapter.token` in the running container.
3. The `coding` profile grants a substantially expanded tool surface (exec, process, code_execution, fs read/write, web) relative to `minimal`. The container security model (`cap_drop: ALL`, `read_only: true`, `no-new-privileges: true`) mitigates privilege escalation risk, but the expanded surface is an intentional trade-off that should be acknowledged in the PR description.

**PR readiness recommendation:** **Conditional Go** — commit the unstaged production file change, then this branch is ready for PR review pending manual operator confirmation of AC-1-v2 and AC-2-v2.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Major | `deploy/docker/openclaw-assistant/openclaw.json` | Working tree | Production file change is unstaged. The diff (`"profile": "minimal"` → `"profile": "coding"`) exists only in the working tree, not in any commit on the branch. | Run `git add deploy/docker/openclaw-assistant/openclaw.json && git commit -m "fix(openclaw-agent): change tools profile from minimal to coding"` before opening a PR. | A PR cannot be opened for an uncommitted working-tree change. If the working tree is reset, the change is lost. | `artifacts/pr_context.appendix.txt` — status line: `M deploy/docker/openclaw-assistant/openclaw.json`; diff shows change in `===== Working tree diff (unstaged) =====` section. |
| Info | `deploy/docker/openclaw-assistant/openclaw.json` | Line 14 | The `coding` profile grants exec/bash/fs-write/web tools. This is a materially expanded tool surface relative to `minimal`. | Document the security trade-off explicitly in the PR description, noting that container hardening (`cap_drop: ALL`, `read_only: true`, `no-new-privileges: true`) mitigates privilege escalation risk. | The expanded surface is intentional and correct for the bug fix, but reviewers should be aware of what the `coding` profile enables. | `spec.md` Section "Risks & Mitigations" (Risk 1); `verify-hardening.2026-04-22.md` (hardening confirmed); openclaw config reference: `coding` = `group:runtime + group:fs + group:web + group:sessions + group:memory`. |
| Info | `deploy/docker/openclaw-assistant/openclaw.json` | n/a | No `$schema` property is present. | No action required. `openclaw.json` is a vendor JSON5 configuration file and no official schema URI is published for this vendor format. Absence of `$schema` is consistent with the existing file structure and is not a policy violation for this file type. | Noted for completeness; does not affect functionality or policy compliance. | File inspection; `spec.md`: "JSON5 document (comments and trailing commas allowed)." |
| Info | `verify-agent-capability.2026-04-22.md` | Entire file | Manual agent capability verification (AC-1-v2 and AC-2-v2) is pending. Artifact has EXIT_CODE: pending. | Operator to connect to the gateway at `http://127.0.0.1:18789`, ask "When is my next available 60-minute window?", confirm the agent responds with calendar data (not a capabilities error), and update the artifact with EXIT_CODE: 0 and Result: PASS. | AC-1-v2 and AC-2-v2 are the primary acceptance criteria for this bug fix. The change is correct per all automated signals, but the feature is not complete until the agent's live behavior is confirmed. | `verify-agent-capability.2026-04-22.md` — EXIT_CODE: pending; instructions for operator provided in the artifact. |

No Blockers found.

---

## Implementation Audit

### JSON seed file change

#### What changed well

- The fix is minimal and precisely targets the root cause. Only the field value changed; all surrounding configuration (gateway, session, agents) is untouched.
- The pre-fix and post-fix states are both captured in evidence artifacts (`baseline-profile-grep.2026-04-22.md` for pre-fix, `verify-profile-in-container.2026-04-22.md` for post-fix), making the change fully auditable.
- The v1 Dockerfile changes (`codex-acp` global install, `CODEX_HOME`, `NPM_CONFIG_CACHE`) are preserved. `verify-dockerfile-v1.2026-04-22.md` confirms all three tokens are present with their expected counts.
- The `coding` profile value is documented as the recommended local onboarding default in the openclaw configuration reference. Using a documented default reduces future maintenance risk.

#### Configuration correctness

- `"profile": "coding"` is a valid, documented value. The openclaw gateway validates the profile at startup and will produce a structured error if an invalid value is provided — no silent misconfiguration risk.
- The seed file is unconditionally applied by `openclaw-agent-entrypoint.sh` on every container start (confirmed in spec.md). No volume-cached stale profile can persist after the image rebuild.
- The gateway logs confirm the new seed hash was applied at container startup (`Config overwrite: /.openclaw/openclaw.json (sha256 ... -> f3c93211...)` in `verify-gateway-logs.2026-04-22.md`).

#### Hardening boundary

- `docker-compose.yml` was not modified (confirmed by `verify-compose-unchanged.2026-04-22.md` — empty git diff).
- `docker inspect openclaw-agent` confirms ReadonlyRootfs: true, CapDrop: ALL, SecurityOpt: no-new-privileges:true remain in place (`verify-hardening.2026-04-22.md`).
- The tmpfs mount options (`noexec`, `nosuid`, `nodev`) are not visible in the inspect snippet but are preserved in `docker-compose.yml` which is confirmed unchanged. The v1 audit (`compose-hardening.2026-04-21T14-00.md`) confirmed all six hardening tokens.

---

## Test Quality Audit

The v2 change has no automated regression tests. The spec.md Test Strategy explicitly acknowledges this and designates manual Docker validation as the appropriate gate. The following verification artifacts were reviewed:

### Reviewed test and QA artifacts

- `docker-build.2026-04-22.md` — confirms image rebuilt cleanly; key build step [3/4] COPY of `openclaw-assistant` seed with updated `openclaw.json` captured. EXIT_CODE 0.
- `docker-recreate.2026-04-22.md` — confirms container recreated from the new image. EXIT_CODE 0.
- `verify-gateway-logs.2026-04-22.md` — confirms `[gateway] ready`, `[plugins] embedded acpx runtime backend ready`, 0 probe failures, and config overwrite with new seed hash. EXIT_CODE 0.
- `verify-profile-in-container.2026-04-22.md` — confirms `"profile": "coding"` at `/.openclaw/openclaw.json` inside the running container. EXIT_CODE 0.
- `verify-hardening.2026-04-22.md` — confirms ReadonlyRootfs, CapDrop, SecurityOpt preserved. EXIT_CODE 0.
- `verify-compose-unchanged.2026-04-22.md` — confirms `git diff development HEAD -- docker-compose.yml` is empty. EXIT_CODE 0.
- `verify-dockerfile-v1.2026-04-22.md` — confirms codex-acp, CODEX_HOME, NPM_CONFIG_CACHE tokens present in Dockerfile. EXIT_CODE 0.
- `verify-agent-capability.2026-04-22.md` — **STUB**. EXIT_CODE: pending. Manual operator verification required.

### Quality assessment prompts

- **Determinism:** All automated verification commands are deterministic (grep, git diff, docker inspect). They do not depend on network calls or time-sensitive state.
- **Isolation:** Each verification artifact targets a specific concern (profile value, hardening, compose state, gateway readiness). No single artifact conflates multiple concerns.
- **Speed:** Docker build took 74.2s (most steps cached). Container recreate took 0.9s. All other checks completed immediately.
- **Diagnostics:** Failure in any check would produce a concrete, actionable output (non-zero exit code + specific field missing or wrong value).

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | ✅ PASS | `openclaw.json` contains `"token": "${OPENCLAW_GATEWAY_TOKEN}"` — an environment variable reference, not a hardcoded credential. No secrets are embedded in the file. |
| No unsafe subprocess or command construction | ✅ PASS | The changed file is a JSON seed configuration. No subprocess invocations are present in this file. |
| Input validation at boundaries | ✅ PASS | The `"coding"` profile value is validated by the openclaw gateway at startup. An invalid value produces a structured startup error. No user input flows into this config path at runtime. |
| Error handling remains explicit | ✅ PASS | Gateway validation behavior on invalid profile is documented in spec.md Risk 2. The change uses a validated, documented profile value. |
| Configuration / path handling is safe | ✅ PASS | The seed file path is fixed (`/opt/openclaw-assistant-seed/openclaw.json` → `/.openclaw/openclaw.json`). No dynamic path construction or injection surface. The expanded `coding` profile grants exec/bash tools within the container; container security controls prevent host-level privilege escalation. |

---

## Research Log

No external research was required during this review. The openclaw configuration reference profile values are documented in `spec.md` Section "Technical specifications." Evidence for all automated checks was collected directly from the v2 feature folder artifacts.

---

## Verdict

The change is technically correct, minimal, and well-documented. It directly addresses the confirmed root cause (tools profile `minimal` → `coding`) with a single field value change, preserves all v1 Dockerfile work, and leaves container hardening intact. All seven automated verification checks passed with EXIT_CODE 0.

The change is not yet ready for PR in its current state because the production file is unstaged. Once `deploy/docker/openclaw-assistant/openclaw.json` is staged and committed to the branch, the automated evidence is complete and the branch is ready for PR review. Final feature sign-off requires manual operator confirmation of AC-1-v2 (agent calls HostAdapter successfully) and AC-2-v2 (exec/bash tools available in session), which should be completed per the instructions in `verify-agent-capability.2026-04-22.md`.
