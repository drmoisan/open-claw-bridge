# Feature (Acceptance Criteria) Audit — Issue #144 (container-validation-stray-v1-and-env-target)

- Reviewed: 2026-07-11T09-15 (RE-AUDIT, remediation pass 1 / R4)
- Work mode: `minor-audit`
- AC source (authoritative): `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/issue.md`, `## Acceptance Criteria` (AC1–AC7).

## Scope and Baseline

- Base branch: `main` (merge-base `81debeb1d58dd7226e0eec1bc66aa154047e6a82`)
- Head: `bug/container-validation-stray-v1-and-env-target-144` @ `3b0a2b32395d73874b096d9b685fbaf7b0da62d9`
- Diff scope: `git diff --stat 81debeb1..3b0a2b3` — 40 files changed (1545 insertions / 42 deletions); the prior review's `81debeb1..a79dee48` diff plus one remediation commit (`3b0a2b3`).

## Acceptance Criteria Inventory

`issue.md` `## Acceptance Criteria` carries 7 checkbox items, all marked `- [x]`:

| # | Criterion (abbreviated) |
|---|---|
| AC1 | In-container HostAdapter probe requests root `/status` (no `/v1`) |
| AC2 | `HostAdapterInContainer` result's `ExpectedCondition` text references `/status` (no `/v1`) |
| AC3 | Default `-EnvFilePath` resolves to the deployed operator `.env` when present, falls back to `./.env` otherwise; pure, testable helper |
| AC4 | No regression: full `tests/scripts` Pester suite passes; PoshQC format + analyze clean, in a single pass |
| AC5 | No `src/OpenClaw.HostAdapter/**` change |
| AC6 | Dashboard-access documentation corrected |
| AC7 | Dashboard validation reports auth accurately; new in-container gateway-token check added; no WebSocket/device-pairing handshake |

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|---|---|---|
| AC1 | **PASS** (unchanged from prior review) | Independently re-confirmed unaffected by the remediation commit: `OpenClawContainerValidation.psm1:282` still reads `http://host.docker.internal:4319/status`; `HostAdapter.Tests.ps1` assertions pass in this audit's fresh CI-exact re-run (416/416, including this test). |
| AC2 | **PASS** (unchanged) | `ExpectedCondition` text unaffected by remediation; assertion independently re-run and passing. |
| AC3 | **PASS** (upgraded from PARTIAL) | The prior review found the pure-helper unit tests reliably passing but the end-to-end entry-script integration tests (`tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1`, 2 Its) failing under standard `Invoke-Pester` due to the now-remediated fixture defect. This audit independently re-ran those exact two tests in isolation under a completely standard, non-MCP-wrapper `Invoke-Pester -Configuration $config` invocation: **2 passed / 0 failed.** Both the present-operator-file and absent-operator-file (fallback to `./.env`) cases are now reliably verified end-to-end under a standard invocation. AC3 is fully satisfied. |
| AC4 | **PASS** (upgraded from FAIL) | The criterion's literal text ("the full `tests/scripts` Pester suite passes... in a single pass") is now substantiated under a standard invocation: this audit's independent re-run of the exact CI command (`Invoke-Pester -Path tests/scripts -Output Detailed -CI`, `.github/workflows/ci.yml:69`) produced **416 passed / 0 failed / 0 skipped**. Format and analyze remain independently confirmed clean on the remediation-site file. AC4 is fully satisfied. (The MCP wrapper's own distinct 409/7 result, investigated separately, is assessed as a wrapper-internal tooling anomaly outside this criterion's "single, standard pass" scope — see `policy-audit.2026-07-11T09-15.md` for the full assessment.) |
| AC5 | **PASS** (unchanged) | `git diff --name-only 81debeb1..3b0a2b3 -- src/OpenClaw.HostAdapter` returns zero output, independently re-confirmed for the full re-audit scope (the remediation commit touches neither). |
| AC6 | **PASS** (unchanged, and the previously-noted out-of-scope Minor documentation issue is now also resolved) | README/runbook dashboard-section corrections unaffected by remediation. Additionally, the prior review's non-blocking Minor finding (stale `/v1` references at `docs/mailbridge-runbook.md` lines 445/457, explicitly outside AC6's literal scope) is now also corrected by the same remediation commit — independently re-confirmed via `grep`, zero remaining matches. |
| AC7 | **PASS** (unchanged) | `AgentDashboard` wording and `Test-OpenClawGatewayTokenInContainer` unaffected by remediation; `GatewayTokenInContainer.Tests.ps1` assertions independently re-run and passing in the fresh CI-exact run. No WebSocket/device-pairing code present (re-confirmed unaffected). |

## Root-Cause / Non-Goal Verification

Unchanged from the prior review and independently re-confirmed for the full `81debeb1..3b0a2b3` scope:

- Root cause (stray `/v1` segment; wrong `-EnvFilePath` default) matches the issue and is correctly fixed.
- Non-goals honored: no `src/OpenClaw.HostAdapter/**` change; no WebSocket/device-pairing handshake; tracked docker-compose files, Dockerfiles, and `Install.Helpers.psm1` untouched (all re-confirmed via `git diff --name-only` against the full re-audit scope).

## Summary

All 7 acceptance criteria are independently verified **PASS**. AC3 and AC4, found PARTIAL and FAIL respectively in the prior review due to a single root-caused, now-remediated test-fixture import-scope defect, are both now fully substantiated: the previously-failing tests pass under a completely standard `Invoke-Pester` invocation (isolated and full-suite), and the CI-exact command (`.github/workflows/ci.yml:69`, independently re-run in this audit) reports 416 passed / 0 failed / 0 skipped.

## Acceptance Criteria Check-off

All 7 AC items were already marked `- [x]` in `issue.md` prior to this re-audit (the executor's original check-off from the initial delivery). No checkbox text or state required a change in this pass — AC3 and AC4, which the prior review found were not yet fully substantiated despite being marked `[x]`, are now independently confirmed to be correctly substantiated by this audit's fresh evidence. No reviewer-side edits to `issue.md` were made (its checkbox state was already correct; this audit's role was to independently verify the substantiating evidence, which it did).

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/issue.md`
- Total AC items: 7
- Checked off (delivered, independently verified PASS): 7 (AC1–AC7)
- Remaining (unchecked): 0
- Items remaining: none

## Overall Feature Audit Verdict

**PASS.** All 7 acceptance criteria are independently verified and fully substantiated as of `3b0a2b32395d73874b096d9b685fbaf7b0da62d9`. The prior review's remediation requirement (AC3 end-to-end test reliability; AC4 full-suite-single-pass) is resolved and independently confirmed via a direct re-run of the exact command CI uses, not solely via the executor's or prior review's committed evidence.
