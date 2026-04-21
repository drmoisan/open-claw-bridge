# Code Review: cannot-access-agent-in-docker (Issue #38) — Second Pass

**Review Date:** 2026-04-21
**Reviewer:** GitHub Copilot (feature_code_review_agent)
**Feature Folder:** `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/`
**Base Branch:** `origin/development` (merge-base `7bd92a8cb772c8f41a85831416a5fec952a2330b`)
**Head Branch:** `bug/cannot-access-agent-in-docker-38` (commit `8f2cd6a6c38e17015403eb2a43e4b4a7c3b4081e`)
**Review Type:** Post-remediation re-review

---

## Executive Summary

This review re-inspects the three scopes addressed in the remediation cycle that followed the first-pass code review (`audit-2026-04-21T00-00/code-review.2026-04-21T00-00.md`). The three remediation items were:
- **R1 (Blocker):** 742-line test file exceeds the 500-line cap.
- **R2 (High):** Hard-coded `dist/index.js` onboard binary path not parameterised.
- **R3 (High):** Hard-coded `/auth/verify` dashboard-auth path not parameterised.

Non-remediation findings from the first pass (Minor/Nit/Info severity) were not assigned remediation items and are carried forward in §Carried-Forward Findings below. The overall implementation scope has not changed.

**What changed (since first-pass audit):**
The three remediation commits (`578fa0e`, `8f2cd6a`) applied the following concrete changes:
- Replaced the 742-line monolithic test file with 5 shards (max 312 lines) and one shared fixture module (122 lines). Total It-block count grew from 17 to 18.
- Added `-OnboardBinaryPath [string]` (default `'dist/index.js'`) as an explicit parameter of `Invoke-OpenClawAgentOnboarding.ps1`, with `.PARAMETER` doc, README + runbook updates, and two new Pester `It` blocks.
- Added `-DashboardAuthPath [string]` (default `'/auth/verify'`) as an explicit parameter of `Invoke-OpenClawContainerPathValidation.ps1`, threaded to `Invoke-OpenClawDashboardAuthProbe -AuthPath`, with code comment, runbook update, and one new Pester `It` block.

**Top 3 risks (post-remediation):**
1. The `dist/index.js` entry-point is an unverified assumption about the upstream `openclaw-agent` image layer layout. The parameter now exists for override, but a clean-machine integration test has not yet been performed. Tracked in `followups.md §4`.
2. The `/auth/verify` endpoint path is similarly unverified against the upstream gateway. The comment at `OpenClawContainerValidation.psm1:~351` documents the manual verification gate.
3. The pre-existing PSScriptAnalyzer warnings in `.claude/hooks/` and `.tmp-tools/` (outside this PR's scope) will continue to surface in workspace-wide analyzer runs until addressed in a follow-up task.

**PR readiness recommendation:** **Go** — All Blocker and High findings are resolved; no new Blocker or High findings were introduced by the remediation changes. The remaining risks are acknowledged, documented, and deferred per spec.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Info | `.claude/hooks/enforce-python-batch-budget.ps1` | n/a (pre-existing) | 2 × `PSAvoidUsingEmptyCatchBlock` PSScriptAnalyzer warnings in a pre-existing file outside the PR diff. | Address in a follow-up maintenance task targeting `.claude/` hook hygiene. | Does not affect the correctness of this PR but will continue to cause workspace-wide analyzer exit code 1. | `mcp_drmcopilotext_run_poshqc_analyze` output, §G1 of policy-audit.2026-04-21T01-00.md |
| Info | `.tmp-tools/capture-phase4-lifecycle.ps1` | n/a (pre-existing) | 3 PSScriptAnalyzer warnings (`PSAvoidUsingEmptyCatchBlock`, `PSUseShouldProcessForStateChangingFunctions`, `PSUseDeclaredVarsMoreThanAssignments`) in a pre-existing file outside the PR diff. | Address in a follow-up maintenance task; delete or fix this file. | Same root as above — not a merge blocker for this PR. | `mcp_drmcopilotext_run_poshqc_analyze` output, §G1 of policy-audit.2026-04-21T01-00.md |
| Info | `scripts/Invoke-OpenClawContainerPathValidation.ps1` | Line 225 | The `-AuthPath` threading comment could be made more precise by referencing the parameter default directly. Non-blocking. | Consider adding a note near the `Invoke-OpenClawDashboardAuthProbe` call that explicitly states the value flows from `-DashboardAuthPath` to aid future maintainers. | Cosmetic; the threading is correct and verified by test. | `read_file` of line 220–230. |

No Blockers or Major findings in PR-scoped files.

---

## Implementation Audit

### PowerShell implementation audit

#### R1: Test file split

The split was performed cleanly. Each shard has a single `Describe` block aligned with the probe area it covers, and all five shards import the shared `OpenClawContainerValidation.Fixtures.psm1` fixture module via `BeforeAll`. The fixture module itself is well-structured: it exports two helpers (`New-DockerInspectFixture`, `Invoke-FakeDocker`) and carries a header docstring. No duplication was observed between shards.

The `It` block count increased from 17 to 18 due to the addition of an explicit `-DashboardAuthPath` override test in `DashboardAuth.Tests.ps1`. This is expected and additive.

One minor structural observation: the main shard (`Invoke-OpenClawContainerPathValidation.Tests.ps1`, 312 lines) is the largest and now also hosts the `CoreBaseUrl` override and overall result aggregation tests. If the file grows further, consider splitting the aggregation tests into a dedicated `Aggregation.Tests.ps1` shard. This is not required at current size.

#### R2: `-OnboardBinaryPath` parameter

The parameter is correctly added at line 61 of `scripts/Invoke-OpenClawAgentOnboarding.ps1`:
```powershell
[string]$OnboardBinaryPath = 'dist/index.js'
```
The `.PARAMETER OnboardBinaryPath` docstring at line 30 is accurate and explains the override semantics. The parameter is injected into the docker-run command string and is covered by both the default path test (lines 208–218) and the override path test (lines 232–242) in `Invoke-OpenClawAgentOnboarding.Tests.ps1`.

The documentation updates in `README.md:281` and `docs/mailbridge-runbook.md:281,524` correctly describe the parameter and its default.

One existing Minor from the first-pass review is worth repeating: if the upstream `openclaw-agent` image ever changes its layout, the `dist/index.js` default will silently produce a misleading error from the docker exec rather than a descriptive parameter validation error. The parameter now exists, which is the remediation target; runtime verification of the path is the deferred follow-up.

#### R3: `-DashboardAuthPath` parameter

The parameter is correctly added at line 33 of `scripts/Invoke-OpenClawContainerPathValidation.ps1`:
```powershell
[string]$DashboardAuthPath = '/auth/verify'
```
It is threaded to `Invoke-OpenClawDashboardAuthProbe -AuthPath $DashboardAuthPath` at line 225. The `Invoke-OpenClawDashboardAuthProbe` function in `OpenClawContainerValidation.psm1` accepts the `[string]$AuthPath` parameter with an accurate code comment at ~line 351:

> `# Default '/auth/verify' is unverified against upstream config; tracked as a manual pre-release verification gate in docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/followups.md`

The threading from the top-level script through the module to the HTTP call is end-to-end correct. The Pester test in `DashboardAuth.Tests.ps1` at lines 149–171 invokes the script with `-DashboardAuthPath '/custom/auth'` and asserts that `Invoke-WebRequest` is called with the overridden URL, confirming that the parameter reaches the HTTP call site.

#### Typing and API notes

All parameters use explicit PowerShell type annotations (`[string]`, `[switch]`, `[SecureString]`). The public parameter surface for both scripts is stable and additive relative to the base branch. No parameters were removed or renamed.

#### Error handling and logging

No changes to error handling patterns in the remediation commits. The `$ErrorActionPreference = 'Stop'` scope is preserved.

---

## Carried-Forward Findings (from first-pass, not assigned remediation)

These findings were documented in the first-pass code review (`audit-2026-04-21T00-00/code-review.2026-04-21T00-00.md`) at Minor or lower severity. They remain unresolved but are not required for merge per the remediation plan scope.

| Severity | File | Location | Finding | Status |
|---|---|---|---|---|
| Minor | `scripts/Invoke-OpenClawAgentOnboarding.ps1` | Param block | `dist/index.js` default is unverified at runtime. Override exists; verification deferred to `followups.md §4`. | Carried Forward — deferred by design |
| Minor | `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | ~line 351 | `/auth/verify` endpoint unverified against upstream gateway. Parameter exists; deferred to `followups.md §4`. | Carried Forward — deferred by design |
| Nit | `deploy/docker/openclaw-agent-entrypoint.sh` | Lines 12–18 | `cp -r` without `-n` flag means re-runs will overwrite any pre-existing workspace files. The idempotency comment does not mention this. Consider `cp -rn` or an explicit check. | Carried Forward — low risk for current usage |
| Info | Various | n/a | `.claude/hooks/` and `.tmp-tools/` PSScriptAnalyzer warnings (pre-existing, 5 total). | Maintenance task, not this PR |
