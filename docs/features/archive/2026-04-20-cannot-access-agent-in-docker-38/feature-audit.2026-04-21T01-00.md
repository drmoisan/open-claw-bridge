# Feature Audit: cannot-access-agent-in-docker (Issue #38) — Second Pass

**Audit Date:** 2026-04-21
**Feature Folder:** `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/`
**Base Branch:** `origin/development` (merge-base `7bd92a8cb772c8f41a85831416a5fec952a2330b`)
**Head Branch:** `bug/cannot-access-agent-in-docker-38` (commit `8f2cd6a6c38e17015403eb2a43e4b4a7c3b4081e`)
**Work Mode:** `full-bug` (from `issue.md`)
**Audit Type:** Post-remediation acceptance verification

---

## Scope and Baseline

- **Base branch:** `origin/development` (commit `6ed593f68700de22967d0c051c58395b3199f85e`)
- **Head branch/commit:** `bug/cannot-access-agent-in-docker-38` (commit `8f2cd6a6c38e17015403eb2a43e4b4a7c3b4081e`)
- **Merge base:** `7bd92a8cb772c8f41a85831416a5fec952a2330b`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt` (refreshed 2026-04-21 against `origin/development`)
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/evidence/`
  - Coverage artifact: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`
  - Test results: `mcp_drmcopilotext_run_poshqc_test` run at second-pass timestamp (100/100 pass)
- **Feature folder used:** `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/`
- **Requirements source:** `spec.md §Acceptance Criteria` only (per `full-bug` work mode)
- **Work mode resolution note:** `issue.md` declares `Work Mode: full-bug`, which uses `spec.md` as the exclusive AC source.
- **Scope note:** PR context was re-collected against `origin/development` base branch using `mcp_drmcopilotext_collect_pr_context` before this audit pass. The resulting diff spans 34 files, 4307 insertions, and 58 deletions.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md` — only source (full-bug mode)

### Acceptance criteria

1. **AC-1** — `scripts/Invoke-OpenClawAgentOnboarding.ps1` exists and is a PowerShell advanced function (`CmdletBinding`), executes the upstream OpenClaw onboarding command via docker exec, captures the emitted gateway token, and writes it as `OPENCLAW_GATEWAY_TOKEN` to the `.env` file.
2. **AC-2** — `deploy/docker/openclaw-assistant/openclaw.json` is updated so the `mode` field is consistent with the documented compose setup and does not produce a startup conflict.
3. **AC-3** — `docker-compose.yml` no longer hard-codes a dummy token value for the `openclaw-agent` service. The token value is supplied via the `.env` file or environment variable injection.
4. **AC-4** — `.env.example` contains an empty or placeholder `OPENCLAW_GATEWAY_TOKEN` entry with a descriptive comment explaining that it must be populated by running the onboarding script.
5. **AC-5** — `scripts/Invoke-OpenClawContainerPathValidation.ps1` defaults `CoreBaseUrl` to `http://127.0.0.1:8080` (matching the container's exposed port) rather than the prior 8081.
6. **AC-6** — `scripts/Invoke-OpenClawContainerPathValidation.ps1` covers all five probe areas: Core readyz, HostAdapter connectivity (via docker exec), token presence in running container, dashboard auth, and overall health report.
7. **AC-7** — `deploy/docker/openclaw-agent-entrypoint.sh` is idempotent: the workspace seed operation is skipped if the target already exists, so re-running the container does not fail.
8. **AC-8** — `docs/mailbridge-runbook.md` heading for the onboarding section is renamed to reflect the new onboarding approach and does not reference the removed prior manual steps.
9. **AC-9** — `README.md`, `docs/architecture-diagrams.md`, and `AGENTS.md` each carry the required framing text and updated compose or onboarding references per the spec.
10. **AC-10** — Pester tests for `Invoke-OpenClawContainerPathValidation.ps1` cover the updated 8080 default port and all five probes. No test file exceeds 500 lines.
11. **AC-11** — Pester tests for `Invoke-OpenClawAgentOnboarding.ps1` exist and cover the happy path (token captured and written), idempotent no-op (token already present), and at least one error path (docker exec failure).
12. **AC-12** — PowerShell coverage for new/changed scripts meets the ≥ 90% threshold for each script. Repository-wide coverage meets ≥ 80%.
13. **AC-13** — Evidence artifacts (baseline coverage, final coverage, comparison) are stored under `artifacts/evidence/`.
14. **AC-14** — The full PoshQC toolchain (format → analyze → test) passes with exit code 0 for all PR-scoped files.
15. **AC-15** — A first-run clean-machine integration test confirms the full onboarding → validation sequence completes without manual intervention.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification | Notes |
|---|-----------|--------|----------|--------------|-------|
| AC-1 | `Invoke-OpenClawAgentOnboarding.ps1` exists, CmdletBinding, executes docker exec, captures token, writes to `.env` | PASS | `read_file scripts/Invoke-OpenClawAgentOnboarding.ps1:1-70` — `CmdletBinding(SupportsShouldProcess)` present; token captured from stdout; `.env` write gated by `ShouldProcess`. Test at `Invoke-OpenClawAgentOnboarding.Tests.ps1:100-135` confirms token write path. | `read_file` line inspection + Pester tests | |
| AC-2 | `openclaw.json` mode consistent, no startup conflict | PASS | `read_file deploy/docker/openclaw-assistant/openclaw.json` — `mode` field updated to match compose docs. No conflicting mode values detected. | `read_file` inspection of modified file | |
| AC-3 | `docker-compose.yml` removes hard-coded dummy token | PASS | PR diff shows the hard-coded `OPENCLAW_GATEWAY_TOKEN=change-me` replaced with `${OPENCLAW_GATEWAY_TOKEN}`. Confirmed via `pr_context.summary.txt` diff entry. | PR context diff | |
| AC-4 | `.env.example` has empty placeholder with descriptive comment | PASS | `read_file .env.example` — `OPENCLAW_GATEWAY_TOKEN=` entry present with comment directing users to run `scripts/Invoke-OpenClawAgentOnboarding.ps1`. | `read_file` inspection | |
| AC-5 | Validation script `CoreBaseUrl` defaults to `http://127.0.0.1:8080` | PASS | `read_file scripts/Invoke-OpenClawContainerPathValidation.ps1:20-40` — default is `'http://127.0.0.1:8080'`. Test at `TokenPresence.Tests.ps1` and main shard confirm 8080 is the mock base URL. | `read_file` line inspection | |
| AC-6 | Validation script covers all five probe areas | PASS | `read_file scripts/Invoke-OpenClawContainerPathValidation.ps1` — function calls to Core readyz, HostAdapter exec, token presence, dashboard auth, and overall report all present. All five shards in the split test files map one-to-one to these probe areas. | `read_file` inspection + test file enumeration | |
| AC-7 | `entrypoint.sh` idempotent workspace seed | PASS | `read_file deploy/docker/openclaw-agent-entrypoint.sh` — seed block guarded by `[ -d "/workspace/.openclaw" ]` check. Re-running the container skips the copy. | `read_file` line inspection | Cosmetic nit: `cp -r` without `-n` does not protect pre-existing files on overwrite path; tracked as Nit in code review carried-forward list. |
| AC-8 | Runbook section heading renamed | PASS | `read_file docs/mailbridge-runbook.md:270-290` — section heading updated. Prior "Manual steps" heading no longer present. | `read_file` heading inspection | |
| AC-9 | README, arch-diagrams, AGENTS.md updated with required framing | PASS | PR context diff entries for `README.md`, `docs/architecture-diagrams.md`, `AGENTS.md` confirm additions per spec §Required framing. Content verified via `read_file` on each document. | PR context diff + `read_file` per file | |
| AC-10 | Container validation tests cover updated defaults and all probes; no test file > 500 lines | PASS | **R1 RESOLVED**: 742-line file split into 5 shards (max 312 lines) + fixture module (122 lines). All 18 It-blocks verified. All five probe areas covered (DashboardAuth.Tests, HostAdapter.Tests, Readyz.Tests, TokenPresence.Tests, main Tests). `(Get-Content <file>).Count` for each confirmed all under 500. | `read_file` line counts + `mcp_drmcopilotext_run_poshqc_test` | First-pass Blocker R1 resolved. |
| AC-11 | Onboarding tests exist; happy path + idempotent no-op + error path | PASS | `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1:9` It-blocks confirmed. Happy path at line 100–135. No-op (token present) at lines 136–165. Docker exec failure at lines 55–80. R2 additions: default path (208–218), override path (232–242). | `read_file` inspection + `mcp_drmcopilotext_run_poshqc_test` | R2 resolved: `-OnboardBinaryPath` tests added. |
| AC-12 | Per-script coverage ≥ 90%; repo-wide ≥ 80% | PASS | Onboarding: 98.55%. Validation: 90.28%. Module: 94.63%. Repo-wide: 86.97%. All thresholds satisfied. | `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md` | |
| AC-13 | Evidence artifacts stored under `artifacts/evidence/` | PASS | `artifacts/evidence/2026-04-20T09-21-issue-38/` contains `baseline/`, `final/`, and comparison artifacts. Verified via `list_dir`. | `list_dir artifacts/evidence/2026-04-20T09-21-issue-38/` | |
| AC-14 | Full PoshQC toolchain passes for PR-scoped files | PASS | Format: exit 0. Test: exit 0, 100/100. Analyze: exit 1 only due to 5 pre-existing issues in `.claude/hooks/` and `.tmp-tools/` (outside PR diff). PR-scoped files show 0 diagnostics. | `mcp_drmcopilotext_run_poshqc_format`, `mcp_drmcopilotext_run_poshqc_analyze`, `mcp_drmcopilotext_run_poshqc_test` | Analyzer non-zero exit is pre-existing and documented in policy-audit §G1; not a merge blocker. |
| AC-15 | Clean-machine integration test confirms full sequence | DEFERRED | Not performed. No clean environment was available. Tracked in `followups.md §3`. | N/A | Deferred by explicit spec risk-acceptance note. Not a merge blocker per spec. |

---

## AC Status Summary

| Status | Count | AC Numbers |
|--------|-------|------------|
| PASS | 14 | AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-12, AC-13, AC-14 |
| PARTIAL | 0 | — |
| FAIL | 0 | — |
| DEFERRED | 1 | AC-15 |
| UNVERIFIED | 0 | — |

---

## Summary

All 14 verifiable acceptance criteria are satisfied at HEAD `8f2cd6a`. The single deferred criterion (AC-15: clean-machine integration test) is explicitly accepted as deferred in the spec and is tracked in `followups.md §3`. The first-pass Blocker (R1: 500-line cap) and both High findings (R2: hard-coded binary path, R3: hard-coded auth path) are fully resolved.

**Overall readiness verdict:** The branch is ready for merge to `development`.

---

## Overall Readiness Verdict

**PASS — Branch is ready for merge to `development`.**

All 14 verifiable acceptance criteria are satisfied. The single deferred criterion (AC-15: clean-machine integration test) is explicitly accepted as deferred in the spec and is tracked in `followups.md §3`. No new Blocker or High findings were introduced by the remediation changes. The first-pass Blocker (R1: 500-line cap) and both High findings (R2: hard-coded binary path, R3: hard-coded auth path) are fully resolved.

Merge should proceed after the three second-pass audit artifacts (`policy-audit.2026-04-21T01-00.md`, `code-review.2026-04-21T01-00.md`, `feature-audit.2026-04-21T01-00.md`) are validated against their schemas.

Post-merge follow-ups (non-blocking):
1. `followups.md §3` — Clean-machine integration test (AC-15).
2. `followups.md §4` — Verify `dist/index.js` entry-point path against actual `openclaw-agent` image layer.
3. `followups.md §4` — Verify `/auth/verify` path against upstream gateway routing config.
4. Maintenance task — address 5 pre-existing PSScriptAnalyzer warnings in `.claude/hooks/` and `.tmp-tools/`.

---

## Acceptance Criteria Check-off

The authoritative check-off state lives in `spec.md §Acceptance Criteria`. The check-off below reflects the verified state at HEAD `8f2cd6a6`:

- [x] AC-1 — `Invoke-OpenClawAgentOnboarding.ps1` exists, CmdletBinding, captures token, writes to `.env` — PASS
- [x] AC-2 — `openclaw.json` mode consistent — PASS
- [x] AC-3 — `docker-compose.yml` no hard-coded dummy token — PASS
- [x] AC-4 — `.env.example` has empty placeholder with descriptive comment — PASS
- [x] AC-5 — Validation script `CoreBaseUrl` defaults to `http://127.0.0.1:8080` — PASS
- [x] AC-6 — Validation script covers all five probe areas — PASS
- [x] AC-7 — `entrypoint.sh` workspace seed is idempotent — PASS
- [x] AC-8 — Runbook section heading renamed — PASS
- [x] AC-9 — README, arch-diagrams, AGENTS.md updated with required framing — PASS
- [x] AC-10 — Container validation tests cover 8080 default and all probes; no test file > 500 lines — PASS (R1 resolved)
- [x] AC-11 — Onboarding tests: happy path + idempotent no-op + error path — PASS (R2 resolved)
- [x] AC-12 — Per-script coverage ≥ 90%; repo-wide ≥ 80% — PASS
- [x] AC-13 — Evidence artifacts stored under `artifacts/evidence/` — PASS
- [x] AC-14 — Full PoshQC toolchain passes for PR-scoped files — PASS
- [ ] AC-15 — Clean-machine integration test — DEFERRED (no clean environment available; tracked in `followups.md §3`)
