# Remediation Inputs: cannot-access-agent-in-docker (Issue #38)

**Timestamp:** 2026-04-21T00-00
**Feature branch:** `bug/cannot-access-agent-in-docker-38`
**Head commit:** `92d7ee69dcd66f126d7a957e3071f00dd5373ed3`
**Base branch:** `origin/development`
**Merge-base SHA:** `7bd92a8cb772c8f41a85831416a5fec952a2330b`

## Review Artifacts (Inputs)

- Policy audit: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/policy-audit.2026-04-21T00-00.md`
- Code review: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/code-review.2026-04-21T00-00.md`
- Feature audit: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/feature-audit.2026-04-21T00-00.md`
- Executor evidence root: `artifacts/evidence/2026-04-20T09-21-issue-38/`
- Spec: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md`
- Plan: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/plan.2026-04-20T09-21.md`

## Remediation Triggers

Remediation is required because:

- The policy audit contains one FAIL result against the general-code-change 500-line file cap.
- The code review contains one Blocker and two High-severity findings.
- Acceptance criteria are all PASS or DEFERRED (no AC-driven remediation).
- Toolchain checks pass (no toolchain-driven remediation).
- Coverage thresholds all pass (no coverage-driven remediation).

## Explicit Remediation Items

### R1. (Blocker) Split `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`

- **Source:** Policy audit §2.4 and §7; code review table row 1.
- **Root cause:** The test file is 742 lines; `.claude/rules/general-code-change.md:40` caps test code at 500 lines, and no exception applies.
- **Required outcome:** The file is split into multiple `*.Tests.ps1` modules such that each file is <= 500 lines and the Pester test count (currently 17) is preserved with identical pass/fail outcomes.
- **Suggested split (indicative, not prescriptive):**
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` — top-level orchestration and aggregate `OverallResult` scenarios only.
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` — `AgentReadyz` probe tests.
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` — `HostAdapterInContainer` probe tests.
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` — `GatewayTokenPresence` probe tests.
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` — `DashboardAuth` probe tests.
  - `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` — shared `Invoke-FakeDocker` and `Invoke-WebRequest` mocks dot-sourced or imported from each test file.
- **Verification:**
  - `(Get-Content <each split file>).Count -le 500` for every resulting file.
  - Pester count remains 17 across the split files (or the count changes only by additions, not deletions).
  - Toolchain rerun: format → analyze → test all PASS in a single loop without rewrites.
  - Coverage for the validation script and module remains >= 90% (recompute via koverage XML).
- **Evidence artifact paths to produce under `artifacts/evidence/<timestamp>-issue-38-remediation/`:**
  - `remediation-test-file-split.md` — list of resulting files with line counts and test counts.
  - `remediation-final-poshqc-format.md`, `...-poshqc-analyze.md`, `...-poshqc-test.md` — toolchain reruns with `EXIT_CODE: 0`.
  - `remediation-coverage-comparison.md` — before-vs-after coverage with thresholds preserved.

### R2. (High) Resolve onboarding binary path or record manual verification gate

- **Source:** Code review table row 2.
- **Root cause:** `scripts/Invoke-OpenClawAgentOnboarding.ps1:131` invokes `dist/index.js` on the `openclaw-agent` service, but `deploy/docker/openclaw-agent.Dockerfile:12` CMDs `node openclaw.mjs gateway --allow-unconfigured`. No runtime evidence exists that `dist/index.js` is present in `${OPENCLAW_AGENT_IMAGE}`. The Pester mocks cover docker entirely, so the mismatch will not surface at CI time.
- **Required outcome — choose one:**
  - **Option A (preferred):** Add a real-docker smoke-run evidence artifact that runs `docker compose --file docker-compose.yml run --rm --no-deps --entrypoint node openclaw-agent dist/index.js --version` (or equivalent non-mutating probe) against `${OPENCLAW_AGENT_IMAGE}`. Capture stdout and exit code. If exit code is non-zero or `dist/index.js` is not found, switch the onboarding script's binary path to `openclaw.mjs` or whatever the upstream CMD uses, update Pester fixtures, rerun the toolchain, and re-record the smoke artifact.
  - **Option B:** Add a parameterized `-OnboardBinaryPath` parameter to `Invoke-OpenClawAgentOnboarding.ps1` (default `dist/index.js`) so operators can override per upstream release, and document the override in `README.md` and `docs/mailbridge-runbook.md`.
  - **Option C:** Record a `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/followups.md` item explicitly marking this as a manual pre-release verification gate, including the exact command the release engineer must run and the expected result.
- **Verification:**
  - If Option A: evidence artifact exists under `artifacts/evidence/<timestamp>-issue-38-remediation/` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` fields.
  - If Option B: parameter documented in README, runbook, and `pr-notes.md`; Pester tests cover both the default and an overridden path.
  - If Option C: `followups.md` updated with the explicit verification gate.
- **Reference:** `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md:118,132,165,355` (flags the pending verification); `2026-04-20T14-00-cannot-access-agent-in-docker-38-research.md` §3.1.

### R3. (High) Verify or parameterize dashboard-auth endpoint path

- **Source:** Code review table row 3.
- **Root cause:** `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:351` hard-codes `$AuthPath = '/auth/verify'` as the default. The path is not verified against upstream configuration reference docs. If upstream uses a different path, the probe will false-negative on a correctly onboarded stack.
- **Required outcome — choose one:**
  - **Option A:** Confirm the upstream path against `docs.openclaw.ai/gateway/configuration-reference` and record the citation in a code comment next to the default. If the path differs, update the default.
  - **Option B:** Expose `-AuthPath` on `Invoke-OpenClawContainerPathValidation.ps1` (currently only exposed on the probe function) so operators can override without code changes.
  - **Option C:** Record a followup item in `followups.md` tracking upstream path verification as a pre-release task.
- **Verification:** Updated code comment or parameter or followup entry. If option A resolves to a different path, Pester fixtures in the existing `DashboardAuth` tests must be updated and re-recorded.

## Non-Remediation Items (Noted but Not Blocking)

The following findings from the code review are medium or lower severity and are recommended but not required for merge. They may be tracked in `followups.md`:

- Anthropic API key plaintext window on docker CLI argv (upstream constraint; not introduced by this branch).
- `Get-OpenClawContentPreview` whitespace-only body semantics.
- `Test-OpenClawGatewayTokenPresence` quote-stripping for `.env` values written with surrounding quotes.
- Test-file `SuppressMessageAttribute` scope tightening.
- `Invoke-OpenClawAgentOnboarding.ps1` `SupportsShouldProcess` advertised at script level but not honored for the primary `docker compose run` side effect.
- `Invoke-OpenClawHostAdapterInContainerProbe` `curl` format-string robustness.
- `Invoke-OpenClawContainerPathValidation.ps1` stale-module-version warning path.
- `.env.example` "do not quote" comment.
- `Invoke-OpenClawAgentOnboarding.ps1` token regex quoted-value robustness.

## Handoff to Atomic Planner

Use this document as the input to `remediation-handoff-atomic-planner` when creating the remediation plan file. The resulting plan should:

- Treat R1 as the single must-fix item required for merge.
- Treat R2 and R3 as strong-recommend items; the release engineer and feature owner may defer one or both to `followups.md` if an explicit manual verification gate is agreed.
- Preserve the existing test count (17) unless an explicit additive test is introduced.
- Rerun the full PowerShell toolchain loop (format → analyze → test + coverage) and record fresh evidence artifacts under a new timestamped folder in `artifacts/evidence/`.
