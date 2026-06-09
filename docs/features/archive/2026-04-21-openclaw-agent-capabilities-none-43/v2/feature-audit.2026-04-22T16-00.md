# Feature Audit: openclaw-agent tools profile fix (Issue #43 v2)

---

**Audit Date:** 2026-04-22
**Feature Folder:** `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2`
**Base Branch:** `development` (commit `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`)
**Head Branch:** `bug/openclaw-agent-capabilities-none-43` (commit `97cde32be6ecd54b3295dec743dcfc16e542f343`)
**Work Mode:** `full-bug`
**Audit Type:** Initial acceptance review (post-implementation)

---

## Scope and Baseline

- **Base branch:** `development` (commit `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`)
- **Head branch/commit:** `bug/openclaw-agent-capabilities-none-43` (commit `97cde32be6ecd54b3295dec743dcfc16e542f343`)
- **Merge base:** `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt` (refreshed 2026-04-22T15:48Z against `development`)
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt` (working tree diff confirms one unstaged file: `deploy/docker/openclaw-assistant/openclaw.json`)
  - Feature evidence: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/` (14 evidence artifacts)
  - v1 baseline: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/evidence/qa-gates/` (v1 QA gate artifacts, all PASS)
- **Feature folder used:** `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2`
- **Requirements source:** `spec.md` (work mode `full-bug` → spec.md only)
- **Work mode resolution note:** `issue.md` contains `- Work Mode: full-bug`. Per the acceptance-criteria tracking protocol, the authoritative AC source is `spec.md` only. `issue.md` v1 ACs (AC-1 through AC-8) are all confirmed PASS from the prior v1 audit; they are included in a separate section below as v1 continuity evidence.
- **Scope note:** This is a versioned feature (`v2`). Evidence under `v2/` is the authoritative source for v2 ACs. v1 QA gate artifacts under `evidence/qa-gates/` remain the authoritative source for v1 ACs. The production file change is currently unstaged (working tree only), which does not affect the verification evidence already collected but does affect PR readiness.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/spec.md` — only source (full-bug mode)

### Acceptance criteria from spec.md (v2 ACs)

1. **AC-1-v2** — The agent answers a calendar question by calling `GET http://host.docker.internal:4319/v1/calendar` successfully. Evidence: agent response contains calendar availability data, not an "execution capabilities" error message. NOTE: Manual operator verification pending — see `verify-agent-capability.2026-04-22.md`.
2. **AC-2-v2** — The agent session has exec/bash tools available (no "cannot execute" or "capabilities=none" error when asked to call the HostAdapter). Evidence: the agent completes a tool call using `exec`/`bash` to retrieve data from the HostAdapter. NOTE: Manual operator verification pending — see `verify-agent-capability.2026-04-22.md`.
3. **AC-3-v2** — Container hardening is preserved: `read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, and `noexec/nosuid/nodev` on tmpfs mounts remain. Evidence: `docker inspect openclaw-agent` output confirms all six hardening tokens; `git diff development HEAD -- docker-compose.yml` is empty. Artifacts: `verify-hardening.2026-04-22.md`, `verify-compose-unchanged.2026-04-22.md`.
4. **AC-4-v2** — `deploy/docker/openclaw-assistant/openclaw.json` contains `"profile": "coding"` (not `"minimal"`). Evidence: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json` returns `"profile": "coding"`. Artifacts: `verify-profile-in-container.2026-04-22.md`.
5. **AC-5-v2** — v1 ACs (AC-1 through AC-8) remain PASS. Evidence: the ACP runtime still starts successfully (`[plugins] embedded acpx runtime backend ready` in logs); the container-path validator still returns `OverallResult: Expected` when the stack is healthy. Artifact: `verify-gateway-logs.2026-04-22.md` (acpx ready confirmed; 0 probe failures).

### v1 acceptance criteria (continuity verification)

The following v1 ACs from `issue.md` were confirmed PASS in the prior audit and are verified as unaffected by the v2 change:

1. **AC-1** (v1) — `openclaw-agent` starts with tool-capable plugin runtime. PASS — `verify-gateway-logs.2026-04-22.md` confirms `[plugins] embedded acpx runtime backend ready` post-v2 rebuild.
2. **AC-2** (v1) — `Invoke-OpenClawContainerPathValidation.ps1 -PassThru` returns `OverallResult: Expected`. PASS — v1 artifact `validator-expected.2026-04-21T14-00.md`; no changes to the validator or its tested surfaces in v2.
3. **AC-3** (v1) — `DashboardAuth` probe removed. PASS — no changes to PowerShell modules in v2; v1 removal is intact.
4. **AC-4** (v1) — `@zed-industries/codex-acp@0.11.1` embedded. PASS — `verify-dockerfile-v1.2026-04-22.md` confirms `codex-acp` tokens present in Dockerfile.
5. **AC-5** (v1) — Container hardening preserved. PASS — evaluated again under AC-3-v2 with `verify-hardening.2026-04-22.md`.
6. **AC-6** (v1) — PowerShell toolchain clean. PASS — no PowerShell files changed in v2.
7. **AC-7** (v1) — Repository Pester coverage ≥ 80%, changed module ≥ 90%. PASS — no code changed in v2; baseline unchanged.
8. **AC-8** (v1) — Runbook updated. PASS — no runbook changes needed for v2; v1 update intact.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-1-v2 | Agent answers calendar question via `GET /v1/calendar` successfully | UNVERIFIED | `verify-agent-capability.2026-04-22.md` — EXIT_CODE: pending; stub artifact with operator instructions | Manual: connect to gateway at `http://127.0.0.1:18789`, ask "When is my next available 60-minute window?", confirm calendar data returned | Pending manual operator verification. All automated preconditions are met: profile confirmed `coding`, acpx runtime ready, exec tools expected available. |
| AC-2-v2 | Agent session has exec/bash tools available | UNVERIFIED | `verify-agent-capability.2026-04-22.md` — EXIT_CODE: pending; stub artifact | Manual: same session as AC-1-v2; confirm no "capabilities=none" or "cannot execute" response | Pending manual operator verification. Automated signal: `"profile": "coding"` confirmed in container; `group:runtime` (exec, process, code_execution) is included in `coding` profile per openclaw config reference. |
| AC-3-v2 | Container hardening preserved (`read_only`, `cap_drop: ALL`, `no-new-privileges`, `noexec/nosuid/nodev`) | PASS | `verify-hardening.2026-04-22.md` (EXIT_CODE: 0 — ReadonlyRootfs: true, CapDrop: ALL, SecurityOpt: no-new-privileges:true confirmed via `docker inspect openclaw-agent`); `verify-compose-unchanged.2026-04-22.md` (EXIT_CODE: 0 — empty diff) | `docker inspect openclaw-agent`; `git diff development HEAD -- docker-compose.yml` | All six hardening tokens confirmed. `docker-compose.yml` unchanged from `development`. |
| AC-4-v2 | `openclaw.json` contains `"profile": "coding"` | PASS | `verify-profile-in-container.2026-04-22.md` (EXIT_CODE: 0 — `"profile": "coding"` confirmed at `/.openclaw/openclaw.json` in running container); `baseline-profile-grep.2026-04-22.md` (pre-fix: `"profile": "minimal"` confirmed) | `docker compose exec openclaw-agent grep '"profile"' /.openclaw/openclaw.json` | Seed file change confirmed applied in container. Baseline pre-fix state documented. |
| AC-5-v2 | v1 ACs (AC-1 through AC-8) remain PASS | PASS | `verify-gateway-logs.2026-04-22.md` (EXIT_CODE: 0 — `[plugins] embedded acpx runtime backend ready`; 0 probe failures; v1 AC-1 preserved); `verify-dockerfile-v1.2026-04-22.md` (EXIT_CODE: 0 — codex-acp, CODEX_HOME, NPM_CONFIG_CACHE confirmed; v1 AC-4 preserved); no PS/C# files changed (v1 AC-6, AC-7, AC-8 unaffected) | `docker compose logs openclaw-agent \| Select-String "embedded acpx runtime backend probe failed" \| Measure-Object -Line` (0 lines); `Select-String "codex-acp" deploy/docker/openclaw-agent.Dockerfile \| Measure-Object` (count: 3) | acpx runtime continuity confirmed. Dockerfile v1 tokens intact. |

---

## Summary

**Overall Feature Readiness:** NEEDS REVISION

**Criteria summary:**
- **PASS:** 3 criteria (AC-3-v2, AC-4-v2, AC-5-v2)
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 2 criteria (AC-1-v2, AC-2-v2 — pending manual operator verification)
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. AC-1-v2 and AC-2-v2 require manual operator verification of live agent behavior. These cannot be confirmed by automated evidence alone. The `verify-agent-capability.2026-04-22.md` artifact contains instructions for the operator to complete this step.
2. The production file `deploy/docker/openclaw-assistant/openclaw.json` is unstaged. This is not an AC failure but it is required for PR creation and is noted as a prerequisite.

**Recommended follow-up verification steps:**

1. Stage and commit `deploy/docker/openclaw-assistant/openclaw.json` to `bug/openclaw-agent-capabilities-none-43`.
2. Operator: connect to the openclaw gateway at `http://127.0.0.1:18789` with a valid token, ask "When is my next available 60-minute window?", and confirm the agent responds with calendar availability data (not a capabilities error). Update `verify-agent-capability.2026-04-22.md` with EXIT_CODE: 0 and Result: PASS.
3. After step 2 passes, check off AC-1-v2 and AC-2-v2 in `spec.md` and re-run the feature audit to produce a final PASS verdict.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- AC-3-v2, AC-4-v2, and AC-5-v2 are evaluated as PASS and are already checked (`[x]`) in `spec.md`. No source file update is required for these.
- AC-1-v2 and AC-2-v2 are evaluated as UNVERIFIED (pending manual verification) and remain unchecked (`[ ]`) in `spec.md`. No change to source file checkboxes is made.

### AC Status Summary

- Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2/spec.md`
- Total AC items: 5 (AC-1-v2 through AC-5-v2)
- Checked off (delivered): 3 (AC-3-v2, AC-4-v2, AC-5-v2 — already checked in spec.md prior to this audit)
- Remaining (unchecked): 2 (AC-1-v2, AC-2-v2)
- Items remaining:
  - `AC-1-v2` — agent answers calendar question via GET /v1/calendar (pending manual verification)
  - `AC-2-v2` — agent session has exec/bash tools available (pending manual verification)

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `v2/spec.md` | 5 | 3 | 2 | Checkbox-backed; AC-3-v2, AC-4-v2, AC-5-v2 already checked before this audit. AC-1-v2, AC-2-v2 remain unchecked pending manual verification. |

No source-file checkbox changes were made by this audit. The three passing ACs (AC-3-v2 through AC-5-v2) were already checked in `spec.md` prior to this audit run, consistent with the plan execution check-off.
