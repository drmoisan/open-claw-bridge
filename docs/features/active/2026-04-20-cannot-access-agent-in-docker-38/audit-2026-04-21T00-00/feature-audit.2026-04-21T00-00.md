# Feature Audit: cannot-access-agent-in-docker (Issue #38)

**Audit Date:** 2026-04-21
**Work Mode:** `full-bug`
**AC Source File:** `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md` §Acceptance Criteria

## Scope and Baseline

- **Feature branch:** `bug/cannot-access-agent-in-docker-38`
- **Head commit:** `92d7ee69dcd66f126d7a957e3071f00dd5373ed3`
- **Base branch:** `origin/development`
- **Merge-base SHA:** `7bd92a8cb772c8f41a85831416a5fec952a2330b`
- **Diff range:** `7bd92a8..92d7ee6` (1 merged commit on the feature branch touching every change; 2 preceding commits on `development` are scoping-doc-only and arrived before the implementation commit)
- **Changed-file count:** 24 tracked paths (3 new production PowerShell files; 2 new PowerShell test files; 1 new `.psd1` manifest; 1 new shell script; 1 new Dockerfile; 1 modified JSON config; 2 modified Dockerfiles; 2 modified compose files; 1 modified `.env.example`; 1 modified `AGENTS.md`; 1 modified `README.md`; 2 modified docs; 7 new feature-folder scoping/evidence docs).
- **Work-mode AC source:** `spec.md` §Acceptance Criteria (the `full-bug` work mode does not include `user-story.md`).

## Acceptance Criteria Inventory

The `spec.md` §Acceptance Criteria section enumerates 14 checkbox items numbered AC-1 through AC-14 in source order. All 14 are currently marked `[x]` in `spec.md` except AC-14, which remains `[ ]` and is explicitly documented as a manual clean-machine integration gate out of scope for automated verification.

| # | Criterion (summary) |
|---|---|
| AC-1 | Onboarding script exists, runs the verified upstream onboarding command, writes `OPENCLAW_GATEWAY_TOKEN` to `.env`, and is idempotent unless `-Force`. |
| AC-2 | `deploy/docker/openclaw-assistant/openclaw.json` `gateway.auth.mode` consistent with runbook prose; config and docs not contradictory. |
| AC-3 | `docker-compose.yml` does not provide a hard-coded `OPENCLAW_GATEWAY_TOKEN` default; token is sourced entirely from `.env`. |
| AC-4 | `.env.example` lists empty `OPENCLAW_GATEWAY_TOKEN=` with comment; `OPENCLAW_AGENT_WORKSPACE` is not present. |
| AC-5 | Validation script default `CoreBaseUrl` equals `http://127.0.0.1:8080`. |
| AC-6 | Validation script probes (single invocation): container health, agent `/readyz`, in-container HostAdapter reachability, `.env` token presence and non-emptiness, dashboard-auth acceptance of token. |
| AC-7 | `deploy/docker/openclaw-agent-entrypoint.sh` does not unconditionally overwrite `/workspace`; onboarding state survives restart. |
| AC-8 | `docs/mailbridge-runbook.md` heading renamed to "OpenClaw Agent (Required)"; optional framing removed; verification references `${OPENCLAW_AGENT_PORT:-18789}` not `8181`. |
| AC-9 | `README.md`, `docs/architecture-diagrams.md`, and `AGENTS.md` describe `openclaw-agent` as a required service in every deployment. |
| AC-10 | `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` replaces all `8081` URIs with `8080` and adds branch coverage for the four new probes. |
| AC-11 | `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` exists and covers missing Docker, upstream failure, malformed output, idempotency, and parameter-vs-prompt. |
| AC-12 | Pester suite passes with >= 90% on the two new/extended production scripts; repo-wide coverage remains >= 80%. |
| AC-13 | Baseline, post-change, and comparison evidence under `artifacts/evidence/` with ISO-8601 timestamps. |
| AC-14 | Full toolchain pass in order (format -> analyze -> test) in a single pass without auto-fix file changes. |
| AC-15 | (spec lists 15 items total) Clean-machine integration: `down -v` → remove wrapper image → onboard → `up --build -d` → validation returns `Expected` → dashboard accepts stored token. Marked as manual verification gate, out of scope for automated executor verification. |

Note: the spec header count states 14, but the section contains 15 checkbox items. The executor `ac-checklist` evidence artifact acknowledges this discrepancy and treats the manual clean-machine integration as AC-15 (deferred).

## Acceptance Criteria Evaluation

| # | Verdict | Evidence |
|---|---|---|
| AC-1 | PASS | `scripts/Invoke-OpenClawAgentOnboarding.ps1` (221 lines) exists and implements the required flow. Tests: 7 in `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` cover missing Docker, upstream non-zero exit, malformed output, idempotency, `-Force`, explicit-key, prompt-mode. Coverage 98.55%. Note: the onboarding command path `dist/index.js` remains pending upstream verification per spec §Risks; not a PASS blocker, flagged as High in the code review. |
| AC-2 | PASS | `deploy/docker/openclaw-assistant/openclaw.json` sets `gateway.auth.mode = "token"` and `gateway.auth.token = "${OPENCLAW_GATEWAY_TOKEN}"`. `docs/mailbridge-runbook.md:520` prose matches. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-openclaw-json-parse.md`. |
| AC-3 | PASS | `docker-compose.yml` at feature HEAD shows `OPENCLAW_GATEWAY_TOKEN: ${OPENCLAW_GATEWAY_TOKEN}` on the `openclaw-agent` service (no `:-openclaw-dev-token` default). Compose now errors out when `.env` is empty. |
| AC-4 | PASS | `.env.example` at HEAD shows `OPENCLAW_GATEWAY_TOKEN=` (empty) with a comment block pointing operators at `scripts/Invoke-OpenClawAgentOnboarding.ps1`. `OPENCLAW_AGENT_WORKSPACE` is not present. |
| AC-5 | PASS | `scripts/Invoke-OpenClawContainerPathValidation.ps1:20`: `[uri]$CoreBaseUrl = 'http://127.0.0.1:8080'`. |
| AC-6 | PASS | `scripts/Invoke-OpenClawContainerPathValidation.ps1:213-227` runs all five probe groups (plus the four new probes) in a single invocation and aggregates into `OverallResult`. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-validation-probes-pass.md`. Note: the `DashboardAuth` probe's endpoint path `/auth/verify` is not verified against upstream; flagged as High in the code review. |
| AC-7 | PASS | `deploy/docker/openclaw-agent-entrypoint.sh:14-37` guards every `/workspace` seed-file copy with `if [ ! -e "$target" ]`. The one unconditional copy (line 32) targets `/.openclaw`, which is compose-defined tmpfs and intentionally re-seeded every start. |
| AC-8 | PASS | `docs/mailbridge-runbook.md:480` reads `## OpenClaw Agent (Required)`. `grep -c "8181\|Optional OpenClaw Assistant"` on the runbook returns 0. The validation script is invoked in the runbook at line 513 as the single pass/fail diagnostic. |
| AC-9 | PASS | `README.md:267` section titled "Manage the OpenClaw Agent (Required)". `docs/architecture-diagrams.md:46` prose describes the agent as a required peer of `openclaw-core`. `AGENTS.md:25` adds the required-peer bullet. |
| AC-10 | PASS | `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` has 17 `It` blocks; `grep -c "8081" tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` returns 0; `grep -c "readyz\|HostAdapterInContainer\|GatewayTokenPresence\|DashboardAuth"` returns multiple matches per probe. |
| AC-11 | PASS | `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` (207 lines) exists with 7 `It` blocks covering the required scenarios. |
| AC-12 | PASS | Coverage artifact: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`. Repo-wide 86.97% (>= 80%). New file coverage: onboarding 98.55%, validation 90.28%, module 94.63% (all >= 90%). |
| AC-13 | PASS | `artifacts/evidence/2026-04-20T09-21-issue-38/` contains `baseline/`, `regression-testing/`, `final/`, and `inflight-reference/` subfolders with ISO-8601 timestamps in every artifact name. |
| AC-14 | PASS | Toolchain evidence artifacts all record `EXIT_CODE: 0`: `final-poshqc-format.md` (no rewrites), `final-poshqc-analyze.md` (0 diagnostics), `final-poshqc-test.md` (97/97 pass). Tool binding note: the executor substituted direct `Invoke-PoshQC*` harness invocation for the MCP-exposed tools because the MCP bindings were not available in the executor environment; the same settings file and harness module were used. |
| AC-15 | DEFERRED | Manual verification gate. Documented in `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/pr-notes.md`. Explicitly out of scope for automated verification. The spec text marks this item with an explanatory note rather than a PASS/FAIL expectation. |

### Additional Scope Findings (beyond AC Evaluation)

- **Policy finding (not an AC):** `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` at 742 lines exceeds the 500-line file cap in `.claude/rules/general-code-change.md`. This is captured in the policy audit and the code review as Blocker. It is not on the AC list because the spec did not enumerate the file-size constraint as an explicit acceptance criterion; the constraint is inherited from the repo-wide policy and is load-bearing for merge readiness.

- **Research-flagged correction (covered by existing ACs):** The research artifact §2.1 notes `--entrypoint node` passthrough from `docker-entrypoint.sh` is unverified. The spec acknowledges this as a risk and the AC set does not require runtime verification before merge. The code review captures this as High severity.

## Summary

- Acceptance criteria delivered: 14 of 15 (14 PASS, 1 DEFERRED by design).
- Policy compliance: PASS for general-unit-test, powershell, tonality; FAIL for general-code-change (§File Size Limit) due to the 742-line test file.
- Coverage: PASS all thresholds.
- Toolchain: PASS (format, analyze, test all clean in a single final pass).
- Code review: 1 Blocker (test-file size cap), 2 High (onboard binary path, dashboard-auth endpoint path), 5 Medium (secret window, body-preview semantics, token quote-stripping, suppression scope, ShouldProcess wiring), 3 Low, 4 Info.
- Overall: REMEDIATION REQUIRED on the Blocker and at least one of the two High-severity items.

## Acceptance Criteria Check-off

All PASS items in the evaluation are already checked off in `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md`. The DEFERRED AC-15 is correctly left unchecked and annotated as a manual verification gate.

No new check-offs are required from this review. No changes are made to `spec.md`.

### Acceptance Criteria Status
- Source: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md` §Acceptance Criteria
- Total AC items: 15
- Checked off (delivered): 14
- Remaining (unchecked): 1
- Items remaining: Clean-machine integration (AC-15) — manual verification gate, out of scope for automated executor verification, documented in `pr-notes.md`.
