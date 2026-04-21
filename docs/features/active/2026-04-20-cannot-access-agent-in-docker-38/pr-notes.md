# PR Notes — issue #38: cannot-access-agent-in-docker

## Summary

The OpenClaw gateway dashboard at `http://127.0.0.1:18789/` now accepts the operator's credential without manual copy-paste. This change:

1. Adds `scripts/Invoke-OpenClawAgentOnboarding.ps1`, a PowerShell wrapper that runs the upstream `openclaw onboard` flow once and writes the generated `OPENCLAW_GATEWAY_TOKEN` to the repository-root `.env`.
2. Extends `scripts/Invoke-OpenClawContainerPathValidation.ps1` with four new probes (`AgentReadyz`, `HostAdapterInContainer`, `GatewayTokenPresence`, `DashboardAuth`) so a single invocation reports `OverallResult: Expected` for a correctly onboarded container stack. Shared helpers moved to `scripts/powershell/modules/OpenClawContainerValidation/` to respect the 500-line cap.
3. Corrects four pre-existing issues called out by the research artifact: validation script `CoreBaseUrl` default (`8081` -> `8080`), hard-coded `openclaw-dev-token` default in `docker-compose.yml` (removed), `openclaw.json` auth stanza (adds explicit `token` reference), and entrypoint seed-file copy behavior (now conditional; onboarding state survives restarts).
4. Reframes `openclaw-agent` as a required peer service across runbook, README, architecture diagrams, and `AGENTS.md`.
5. Splits `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (formerly 742 lines) into five `*.Tests.ps1` files (orchestration + `Readyz` + `HostAdapter` + `TokenPresence` + `DashboardAuth`) plus one shared fixture module at `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`; every resulting file is <= 500 lines and the 17 original tests are preserved without behavioral edits.
6. Adds two additive parameters as non-breaking operator overrides: `scripts/Invoke-OpenClawAgentOnboarding.ps1 -OnboardBinaryPath` (default `dist/index.js`) and `scripts/Invoke-OpenClawContainerPathValidation.ps1 -DashboardAuthPath` (default `/auth/verify`, threaded through to the module's `Invoke-OpenClawDashboardAuthProbe -AuthPath`). Both defaults match the prior hard-coded values, so existing invocations behave identically.

## Breaking changes

The breaking-change scope is unchanged from the original PR: only the two operator-visible changes below. The new `-OnboardBinaryPath` and `-DashboardAuthPath` parameters introduced by the remediation cycle are additive with defaults that match the prior hard-coded values, so they are not operator-visible breaking changes.

- **Removed hard-coded `OPENCLAW_GATEWAY_TOKEN` default in `docker-compose.yml`.** Operators who currently rely on the `openclaw-dev-token` placeholder will see compose fail until they supply a valid token from the onboarding script or their own value in `.env`.
- **Removed `OPENCLAW_AGENT_WORKSPACE` from `.env.example`.** The compose stack uses the named volume `openclaw_agent_workspace`; operators should remove any stale `OPENCLAW_AGENT_WORKSPACE=...` line from their local `.env`.

## Operator upgrade steps

1. Pull the merged branch.
2. If a `.env` file does not exist, copy `.env.example` to `.env`.
3. Remove any `OPENCLAW_AGENT_WORKSPACE=...` line from `.env`. Ensure `OPENCLAW_GATEWAY_TOKEN` is empty or absent.
4. Run the onboarding script once, supplying an Anthropic API key:
   ```powershell
   pwsh -NoProfile -File scripts/Invoke-OpenClawAgentOnboarding.ps1
   ```
   The script accepts an optional `-OnboardBinaryPath` parameter (default `dist/index.js`). Supply an override only when an upstream release renames or relocates the onboarding entry-point binary. See `docs/mailbridge-runbook.md` (Onboarding parameter overrides) for the rationale.
5. Start the stack:
   ```powershell
   docker compose --env-file .env -f docker-compose.yml -f docker-compose.dev.yml up --build -d
   ```
6. Validate:
   ```powershell
   pwsh -NoProfile -File scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru
   ```
   Expect `OverallResult: Expected`.
7. Open `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/`. The dashboard reads the stored token; no paste step required.

## Validation evidence pointers

Original delivery cycle (2026-04-20T09-21):

- Phase 0 baseline (clean tree): `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/`
- Regression-testing evidence (expect-fail before implementation, pass after): `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/`
- Final QA gate: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-qa-gate-summary.md`
- Acceptance-criteria checklist: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-ac-checklist.md`
- Coverage comparison: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`
- Inflight-reference diffs: `artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/`

Remediation cycle (2026-04-21T00-00):

- Remediation plan-of-record: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/plan-remediation.2026-04-21T00-00.md`
- Remediation baseline: `artifacts/evidence/2026-04-21T00-00-issue-38-remediation/baseline/`
- R1 (test-file split) evidence: `artifacts/evidence/2026-04-21T00-00-issue-38-remediation/r1/`
- R2 (onboarding `-OnboardBinaryPath`) evidence: `artifacts/evidence/2026-04-21T00-00-issue-38-remediation/r2/`
- R3 (validation `-DashboardAuthPath`) evidence: `artifacts/evidence/2026-04-21T00-00-issue-38-remediation/r3/`
- Final QA + summary: `artifacts/evidence/2026-04-21T00-00-issue-38-remediation/final/`

## Clean-machine integration gate (manual)

AC-15 in `spec.md` is a manual verification that is not automated by this PR:

1. `docker compose down -v` (drops the `openclaw_agent_workspace` named volume).
2. Remove the local wrapper image if present: `docker image rm openclaw/agent:pre-mvp`.
3. Copy `.env.example` to `.env`.
4. Run `scripts/Invoke-OpenClawAgentOnboarding.ps1` with a real Anthropic API key.
5. `docker compose up --build -d`.
6. Run `scripts/Invoke-OpenClawContainerPathValidation.ps1` and confirm `OverallResult: Expected`.
7. Open the dashboard URL and confirm it accepts the stored token without operator paste.

Attach a screenshot of step 7 plus the `-PassThru` output of step 6 to the PR review before merging.

## Rollback

```powershell
git revert <merge-sha>
docker compose down -v
```

`docker compose down -v` drops the `openclaw_agent_workspace` named volume and any onboarding state. No further cleanup is required on the Windows host.

## Policy compliance

- Tonality: professional, factual, no hyperbole or humor (`.claude/rules/tonality.md`).
- Unit tests: no temp files, no external deps, ≥ 90% new-code coverage, ≥ 80% repo-wide (`.claude/rules/general-unit-test.md`).
- General code change: 500-line cap enforced on both new scripts and module; fail-fast error handling; no silent catches (`.claude/rules/general-code-change.md`).
- PowerShell: advanced functions with `CmdletBinding`, `ShouldProcess` on state-changing actions, `SecureString` for the Anthropic API key parameter (`.claude/rules/powershell.md`).

## Tool-binding note

The plan references `mcp__drmCopilotExtension__run_poshqc_*` MCP tools. Those tools are not exposed in the executor environment used for this PR. The same PoshQC harness module at `.claude/worktrees/agent-a04d81f7/scripts/powershell/PoshQC/` was imported directly and invoked with the same `pssa.settings.psd1` and `pester.runsettings.psd1` that the MCP tools use. All toolchain evidence artifacts record the substitution explicitly.
