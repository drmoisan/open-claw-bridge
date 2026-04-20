# 2026-04-20-cannot-access-agent-in-docker (Plan)

- **Issue:** #38
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-20T09-21
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-bug
- **Budget Path:** large (3 production PowerShell files: extended validation script + new onboarding script + new helpers module)

**Fail-closed evidence rule:** Every evidence-producing task declares its expected artifact path. If any required baseline artifact, QA artifact, or coverage-comparison artifact is missing, the audit verdict is BLOCKED or INCOMPLETE, never PASS.

**Evidence accounting rule:** Record the expected artifact path in each evidence-producing task. Do not mark evidence-backed work complete without the artifact.

**Policy precedence (read in this order):** `CLAUDE.md` (standing) → `.claude/rules/general-code-change.md` → `.claude/rules/general-unit-test.md` → `.claude/rules/powershell.md` → `.claude/rules/tonality.md`.

**Sources of truth:**
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md` (authoritative scope, 14 ACs)
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/issue.md` (operator problem statement)
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/2026-04-20T14-00-cannot-access-agent-in-docker-38-research.md` (four corrections to staged work)

---

### Phase 0 — Stash in-flight work, read policy, capture clean baseline

- [x] [P0-T1] Read `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`, and `.claude/rules/tonality.md`. Record the read order in `artifacts/evidence/2026-04-20T09-21-issue-38/phase0-instructions-read.md` with `Timestamp:` and `Policy Order:` and the explicit list of files read.
- [x] [P0-T2] Verify the working tree represents the staged-but-uncommitted in-flight implementation by running `git status --short` and capturing output to `artifacts/evidence/2026-04-20T09-21-issue-38/phase0-pre-stash-status.txt`. Confirm the listed files in the system-reminder brief match the on-disk state. If they do not match, halt and escalate.
- [x] [P0-T3] Stash all staged and untracked changes with the exact command `git stash push -u -m "issue-38-inflight-before-baseline-2026-04-20T09-21"`. Capture command output, the stash ref printed (expected `stash@{0}`), and the subsequent `git stash list` + `git status` output to `artifacts/evidence/2026-04-20T09-21-issue-38/phase0-stash-confirmation.txt`. Fail the task if the working tree is not clean after the stash.
- [x] [P0-T4] Record clean baseline metadata: branch name (`git rev-parse --abbrev-ref HEAD`), HEAD SHA (`git rev-parse HEAD`), timestamp (ISO-8601), and stash ref from P0-T3. Write to `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/baseline-metadata.txt` with required fields `Timestamp:`, `Branch:`, `HEAD:`, `StashRef:`.
- [x] [P0-T5] Capture clean-tree PowerShell format baseline by running the MCP tool `mcp__drmCopilotExtension__run_poshqc_format`. Store the run output to `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-format.md` with required fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P0-T6] Capture clean-tree PowerShell analyze baseline by running `mcp__drmCopilotExtension__run_poshqc_analyze`. Store the run output to `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-analyze.md` with required fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P0-T7] Capture clean-tree Pester test + coverage baseline by running `mcp__drmCopilotExtension__run_poshqc_test` in coverage mode. Store the run output to `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-test.md` with required fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` where `Output Summary:` records the numeric repository-wide coverage headline as a baseline reference. This baseline predates the new scripts by design; it captures the pre-implementation coverage floor.
- [x] [P0-T8] Record the required environment/fixtures/data for downstream phases: Docker Desktop with functioning `host.docker.internal` networking, Pester v5.x, PSScriptAnalyzer exposed through the MCP toolchain, and an Anthropic API key (manual integration step only — not a unit-test dependency). Write to `artifacts/evidence/2026-04-20T09-21-issue-38/phase0-fixtures.md` with required fields `Timestamp:`, `RequiredFixtures:`, `UnitTestDependencies:` (empty list by design), `ManualDependencies:`.

### Phase 1 — Restore in-flight work for re-delivery as reference material

- [x] [P1-T1] Restore the Phase 0 stash with `git stash pop stash@{0}`. Capture the pop output and the post-pop `git status --short` to `artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/stash-pop.txt`. If any hunk conflicts are reported, halt and escalate to the orchestrator; do not attempt manual conflict resolution in this phase.
- [x] [P1-T2] Save the unstashed diff snapshot for downstream reference. Create `artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/` if absent, then run `git diff --cached > artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/inflight-diff.patch` for staged content and `git diff > artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/inflight-unstaged-diff.patch` for unstaged content. Verify both files exist on disk and are non-empty when the corresponding diff is non-empty.
- [x] [P1-T3] Generate an inventory of every tracked file touched by the in-flight work by running `git diff --name-status HEAD` (after the unstash) and writing the list to `artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/inflight-file-inventory.txt`. Cross-check the list against the 13 paths named in the brief; record any delta.

### Phase 2 — Regression tests (must fail first)

- [x] [P2-T1] [expect-fail] Add an expect-fail Pester regression test `DashboardAuth probe reports Unexpected when OPENCLAW_GATEWAY_TOKEN is empty` inside `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` that invokes the extended validation script against a mocked stack where the mocked `.env` content contains `OPENCLAW_GATEWAY_TOKEN=` (empty). Arrange mocks via the existing `Invoke-FakeDocker` injection pattern and `Mock Invoke-WebRequest`. Act: call the script with `-PassThru`. Assert: `OverallResult -eq 'Unexpected'` and the `DashboardAuth` probe `IsExpected -eq $false`. Tag the test with `-Tag 'ExpectFail-Phase2'`.
- [x] [P2-T2] [expect-fail] Run the regression test via `mcp__drmCopilotExtension__run_poshqc_test` targeting only the expect-fail tag. The test must fail because the validation script has not yet been extended with the `DashboardAuth` probe. Capture the failing run output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-dashboard-auth-expect-fail.md` with required fields `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero expected), `Output Summary:` (must name the missing probe).
- [x] [P2-T3] [expect-fail] Add an expect-fail Pester regression test `GatewayTokenPresence probe reports Unexpected when .env omits OPENCLAW_GATEWAY_TOKEN` in the same test file. Arrange: mock an `.env` with no `OPENCLAW_GATEWAY_TOKEN=` line at all. Act: invoke the script. Assert: `GatewayTokenPresence` probe reports `IsExpected -eq $false` and `SupportingDiagnostics` describes the missing key. Run and capture failure output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-token-presence-expect-fail.md` with the four required fields.

### Phase 3 — Apply research-flagged corrections to already-staged work

- [x] [P3-T1] Unstage the restored in-flight work with `git restore --staged .` so subsequent edits land in the working tree. Capture the post-unstage `git status --short` to `artifacts/evidence/2026-04-20T09-21-issue-38/inflight-reference/post-unstage-status.txt`.
- [x] [P3-T2] In `scripts/Invoke-OpenClawContainerPathValidation.ps1`, change the `CoreBaseUrl` parameter default from `http://127.0.0.1:8081` to `http://127.0.0.1:8080`. Verify by `grep -n '8081' scripts/Invoke-OpenClawContainerPathValidation.ps1` returning no matches. Maps to AC-5.
- [x] [P3-T3] In `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`, replace every hard-coded `8081` URI with `8080`. Verify by `grep -n '8081' tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` returning no matches. Maps to AC-9.
- [x] [P3-T4] In `docker-compose.yml`, remove the `:-openclaw-dev-token` default from the `OPENCLAW_GATEWAY_TOKEN` environment entry on the `openclaw-agent` service. The line must read `OPENCLAW_GATEWAY_TOKEN: ${OPENCLAW_GATEWAY_TOKEN}` with no default. Verify by `grep -n 'openclaw-dev-token' docker-compose.yml` returning no matches. Maps to AC-3.
- [x] [P3-T5] In `deploy/docker/openclaw-assistant/openclaw.json`, retain `"mode": "token"` in the `gateway.auth` object and add an explicit `"token": "${OPENCLAW_GATEWAY_TOKEN}"` field so the config references the operator-supplied token. Verify the file parses as valid JSON via `powershell -c "Get-Content deploy/docker/openclaw-assistant/openclaw.json -Raw | ConvertFrom-Json | Out-Null"`. Capture the verification exit code to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-openclaw-json-parse.md`. Maps to AC-2.
- [x] [P3-T6] In `deploy/docker/openclaw-agent-entrypoint.sh`, replace the unconditional `cp "$seed_dir/<file>" "$target"` lines with an `if [ ! -e "$target" ]; then cp ...; fi` guard around each seed-file copy. Preserve `set -eu` at the top of the script. Verify the change by `grep -c 'if \[ ! -e' deploy/docker/openclaw-agent-entrypoint.sh` returning a count matching the number of seed-file copies. Maps to AC-6.

### Phase 4 — Deliver new onboarding script (`scripts/Invoke-OpenClawAgentOnboarding.ps1`) with expect-fail-first TDD

- [x] [P4-T1] Create the test file `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` with an expect-fail Pester test `Onboarding script fails fast when docker CLI is unavailable`. Arrange: mock `Get-Command docker` returning `$null`. Act: dot-source and invoke the onboarding script. Assert: the call throws with a distinct error category mentioning Docker. Tag `-Tag 'ExpectFail-Phase4'`.
- [x] [P4-T2] Add test `Onboarding script propagates non-zero exit from upstream onboard command` using `Invoke-FakeDocker` injection pattern to simulate an upstream failure. Assert: script exits non-zero and surfaces the underlying exit code.
- [x] [P4-T3] Add test `Onboarding script fails fast when onboard output contains no OPENCLAW_GATEWAY_TOKEN= line`. Arrange a fake docker output with malformed content. Assert: script throws with a distinct error category referencing malformed output.
- [x] [P4-T4] Add test `Onboarding script is idempotent when OPENCLAW_GATEWAY_TOKEN is already present in .env and -Force is not supplied`. Mock a pre-populated `.env` content. Assert: script exits `0`, writes no new value, and emits a verbose message.
- [x] [P4-T5] Add test `Onboarding script overwrites token when OPENCLAW_GATEWAY_TOKEN is present and -Force is supplied`. Assert: the new value replaces the old.
- [x] [P4-T6] Add test `Onboarding script consumes -AnthropicApiKey parameter without prompting`. Mock `Read-Host` and assert it is never called.
- [x] [P4-T7] Add test `Onboarding script prompts via Read-Host -AsSecureString when -AnthropicApiKey is absent`. Mock `Read-Host -AsSecureString` and assert the mock is invoked exactly once.
- [x] [P4-T8] Run the expect-fail test set via `mcp__drmCopilotExtension__run_poshqc_test -Tag ExpectFail-Phase4`. Capture the failing output (no onboarding script exists yet) to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-onboarding-expect-fail.md` with the four required fields.
- [x] [P4-T9] Create `scripts/Invoke-OpenClawAgentOnboarding.ps1` as an advanced function with `[CmdletBinding(SupportsShouldProcess)]`. Parameters per spec §Technical specifications: `-AnthropicApiKey [SecureString]` (prompt if absent via `Read-Host -AsSecureString`), `-EnvFilePath [string]` default `./.env`, `-ComposeFiles [string[]]` default to the project compose files, `-Force [switch]`. Implement the onboarding flow: run `docker compose --file <composeFiles> run --rm --no-deps --entrypoint node openclaw-agent dist/index.js onboard --mode local --no-install-daemon --non-interactive --auth-choice apiKey --anthropic-api-key <plaintext> --secret-input-mode plaintext --gateway-port 18789 --gateway-bind loopback --skip-skills`, parse `OPENCLAW_GATEWAY_TOKEN=<value>` from stdout, write to `-EnvFilePath`. Fail fast with distinct error categories for: missing docker CLI, non-zero onboard exit, malformed onboard output, unable to write `.env`. The file must be <= 500 lines.
- [x] [P4-T10] If implementation pushes the file over 500 lines, extract shared helpers (env-file parsing, docker argument marshalling) into a new module `scripts/powershell/modules/OpenClawOnboarding/OpenClawOnboarding.psm1` with an accompanying `OpenClawOnboarding.psd1` manifest and dot-source / `Import-Module` it from the onboarding script. Verify line count with `(Get-Content scripts/Invoke-OpenClawAgentOnboarding.ps1).Count -le 500`. (N/A — script is 216 lines; extraction not required.)
- [x] [P4-T11] Re-run the onboarding test set. All Phase 4 tests must pass. Capture pass output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-onboarding-pass.md` with the four required fields.

### Phase 5 — Extend validation script (`scripts/Invoke-OpenClawContainerPathValidation.ps1`) with four new probes

- [x] [P5-T1] [expect-fail] Add probe-level Pester tests for `AgentReadyz` to `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`: positive (HTTP 200 from `/readyz`), negative (HTTP 503), unreachable (throw). Tag `-Tag 'ExpectFail-Phase5'`. Run and capture failing output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-agent-readyz-expect-fail.md` with the four required fields.
- [x] [P5-T2] [expect-fail] Add probe-level tests for `HostAdapterInContainer`: positive (`docker compose exec openclaw-agent` curl returns 200 with bearer token), negative (non-200), exec failure (non-zero exit). Run and capture failing output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-hostadapter-incontainer-expect-fail.md`.
- [x] [P5-T3] [expect-fail] Add probe-level tests for `GatewayTokenPresence`: positive (`.env` contains `OPENCLAW_GATEWAY_TOKEN=<non-empty>`), negative-missing (no line), negative-empty (`OPENCLAW_GATEWAY_TOKEN=`). Mock `.env` reads via `Mock Get-Content`. Run and capture failing output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-token-presence-probe-expect-fail.md`.
- [x] [P5-T4] [expect-fail] Add probe-level tests for `DashboardAuth`: positive (POST auth with stored token returns 200), negative (401), malformed (non-JSON body). Run and capture failing output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-dashboard-auth-probe-expect-fail.md`.
- [x] [P5-T5] Before implementing probes, measure the current line count of `scripts/Invoke-OpenClawContainerPathValidation.ps1` with `(Get-Content scripts/Invoke-OpenClawContainerPathValidation.ps1).Count`. Record to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-validation-script-line-count-pre.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. The value is expected to be approximately 497.
- [x] [P5-T6] Create `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` (plus `.psd1` manifest) and move shared helpers (docker marshalling, URL probing, env parsing, JSON output formatting) out of `Invoke-OpenClawContainerPathValidation.ps1` into the module. Update the script to `Import-Module` the new module. This preserves the 500-line cap before new probes are added. Verify the module loads cleanly with `Import-Module scripts/powershell/modules/OpenClawContainerValidation -Force`.
- [x] [P5-T7] Implement `Invoke-OpenClawReadyzProbe` in the validation module. Probe `GET ${AgentBaseUrl}/readyz`. Return `[pscustomobject]@{ Name = 'AgentReadyz'; IsExpected = <bool>; SupportingDiagnostics = <string> }`. Fail fast on unreachable host.
- [x] [P5-T8] Implement `Invoke-OpenClawHostAdapterInContainerProbe` in the module. Use `Invoke-OpenClawDockerCommand` (existing wrapper) to call `docker compose exec openclaw-agent sh -c 'curl -H "Authorization: Bearer $(cat /run/openclaw/hostadapter.token)" http://host.docker.internal:4319/v1/status'`. Return a probe object.
- [x] [P5-T9] Implement `Test-OpenClawGatewayTokenPresence` in the module. Read the configured `.env` path, parse lines, and confirm `OPENCLAW_GATEWAY_TOKEN` exists with a non-empty value. Return a probe object.
- [x] [P5-T10] Implement `Invoke-OpenClawDashboardAuthProbe` in the module. POST the gateway token against the dashboard auth endpoint (path derived from upstream config) and confirm HTTP 200. Return a probe object.
- [x] [P5-T11] Wire the four new probes into the script's top-level orchestration so a single invocation of `scripts/Invoke-OpenClawContainerPathValidation.ps1` aggregates all five probe groups (existing plus the four new probes) into `OverallResult`. Preserve the existing `-PassThru` and `-AsJson` behavior and existing parameter signature.
- [x] [P5-T12] Verify the production script file stays at or below 500 lines: `(Get-Content scripts/Invoke-OpenClawContainerPathValidation.ps1).Count`. Record to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-validation-script-line-count-post.md`. Fail the task if the count exceeds 500.
- [x] [P5-T13] Re-run the Phase 5 probe tests plus the Phase 2 expect-fail regressions. All must now pass. Capture the pass output to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-validation-probes-pass.md` with the four required fields.

### Phase 6 — Documentation updates (runbook, README, architecture diagrams, environment, AGENTS propagation)

- [x] [P6-T1] In `docs/mailbridge-runbook.md`, rename the heading `## Optional OpenClaw Assistant Service` (pre-stash line 475) to `## OpenClaw Agent (Required)`. Verify by `grep -n 'Optional OpenClaw Assistant Service' docs/mailbridge-runbook.md` returning no matches. Maps to AC-7.
- [x] [P6-T2] In `docs/mailbridge-runbook.md`, reframe the purpose block (lines 9-10, 20-21) and "Install Path C is optional" (line 364) so `openclaw-agent` is presented as required. Remove the word "optional" from all four listed locations. Verify by `grep -n 'optional' docs/mailbridge-runbook.md` not returning any of those four locations (acknowledge that unrelated matches may remain). Maps to AC-7.
- [x] [P6-T3] In `docs/mailbridge-runbook.md`, replace the ad-hoc `curl` / `docker compose exec` diagnostic block (pre-stash lines 494-506) with a single invocation of `scripts/Invoke-OpenClawContainerPathValidation.ps1` and a note describing its aggregated pass/fail result. Maps to AC-7.
- [x] [P6-T4] Normalize all port references in `docs/mailbridge-runbook.md` to use `${OPENCLAW_AGENT_PORT:-18789}` for the agent, `${OPENCLAW_HTTP_PORT:-8080}` for the core, and `4319` for the HostAdapter. Verify by `grep -n '8181' docs/mailbridge-runbook.md` returning no matches. Maps to AC-7.
- [x] [P6-T5] Update `README.md` so the assistant-service section (pre-stash lines 267-310) describes `openclaw-agent` as required, removes any "no longer requires a gateway token" prose (the token is now produced by onboarding), and points operators at `scripts/Invoke-OpenClawAgentOnboarding.ps1`. Maps to AC-8.
- [x] [P6-T6] Update `docs/architecture-diagrams.md` so the agent is described as a required peer and the published port remains `18789`. Verify no stale `8181` survives via `grep -n '8181' docs/architecture-diagrams.md`. Maps to AC-8.
- [x] [P6-T7] Update `.env.example`: set `OPENCLAW_GATEWAY_TOKEN=` with an empty value, add a comment line directly above it pointing operators at `scripts/Invoke-OpenClawAgentOnboarding.ps1`, and ensure `OPENCLAW_AGENT_WORKSPACE` is not present. Verify by `grep -n 'OPENCLAW_AGENT_WORKSPACE' .env.example` returning no matches and `grep -n 'OPENCLAW_GATEWAY_TOKEN=' .env.example` returning exactly one match with an empty right-hand side. Maps to AC-4.
- [x] [P6-T8] Edit `AGENTS.md` at the repository root. In the `## Repository Setup (High-Level)` section (lines 20-25), add a new bullet directly after the existing two bullets that reframes `openclaw-agent` as a required service: the bullet must state that `openclaw-agent` is a required peer service in the compose stack and point operators at `scripts/Invoke-OpenClawAgentOnboarding.ps1` for first-run token provisioning. Verify by `grep -n 'openclaw-agent' AGENTS.md` returning at least one match inside the `Repository Setup (High-Level)` section and by confirming the section length has increased by exactly one bullet line. Record the pre-edit and post-edit `grep -n '## Repository Setup' AGENTS.md` line numbers and the inserted bullet text to `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-agents-edit.md` with the four required fields. Maps to AC-8.

### Phase 7 — Final QA gate

- [x] [P7-T1] Run `mcp__drmCopilotExtension__run_poshqc_format` against the full repository. If the step modifies any file, restart the loop from this task. Store output to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-format.md` with required fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P7-T2] Run `mcp__drmCopilotExtension__run_poshqc_analyze`. If violations are reported, fix them and restart from P7-T1. Store output to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-analyze.md` with the four required fields.
- [x] [P7-T3] Run `mcp__drmCopilotExtension__run_poshqc_test` in coverage mode. All tests must pass. Store output to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-test.md` with the four required fields. `Output Summary:` must include numeric values for repository-wide coverage and per-file coverage for `scripts/Invoke-OpenClawContainerPathValidation.ps1` and `scripts/Invoke-OpenClawAgentOnboarding.ps1`.
- [x] [P7-T4] Compare Phase 0 baseline coverage (`artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-test.md`) against Phase 7 final coverage (`artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-test.md`). Write the comparison to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md` with fields: `BaselineRepoCoverage:`, `FinalRepoCoverage:`, `NewCodeCoveragePerFile:`, `ThresholdRepo: >= 80%`, `ThresholdNewCode: >= 90%`, `Verdict:`. Fail the task if repo-wide coverage drops below 80% or either new-code file drops below 90%. Maps to AC-11.
- [x] [P7-T5] Verify file-size cap on the extended validation script: `(Get-Content scripts/Invoke-OpenClawContainerPathValidation.ps1).Count -le 500`. Record outcome to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-validation-script-line-count-final.md`.
- [x] [P7-T6] Verify file-size cap on the onboarding script: `(Get-Content scripts/Invoke-OpenClawAgentOnboarding.ps1).Count -le 500`. Record outcome to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-onboarding-script-line-count-final.md`.
- [x] [P7-T7] Acceptance-criteria check-off: for each of the 14 AC items in `spec.md §Acceptance Criteria`, mark the checkbox `[x]` and append a short evidence pointer (relative file path, Pester test name, or artifact path). Commit the spec update as part of the feature delivery. Record the sweep result to `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-ac-checklist.md` with a per-AC table.
- [x] [P7-T8] Store an end-state baseline-vs-final summary at `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-qa-gate-summary.md` that links the four Phase 7 artifacts, states `EXIT_CODE: 0` for each, and lists all AC IDs as checked.

### Phase 8 — PR notes and rollout

- [x] [P8-T1] Draft PR notes at `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/pr-notes.md`. Required sections: Summary, Breaking Changes (removed hard-coded `OPENCLAW_GATEWAY_TOKEN` default in `docker-compose.yml`), Operator Upgrade Steps (run `scripts/Invoke-OpenClawAgentOnboarding.ps1` once), Validation Evidence Pointers (link Phase 7 artifacts and AC checklist), and Rollback (`git revert` + `docker compose down -v`).
- [x] [P8-T2] Record follow-up items in `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/followups.md`: audit of stale `8181` references in `docs/features/active/2026-04-16-openclaw-agent-docker-30/v1/` and `v2/` (research §2.9) — explicitly out of scope for this bug, open as a separate tracking item.
- [x] [P8-T3] Mirror the issue update to `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/evidence/issue-updates/issue-38.2026-04-20T09-21.md` with fields `Timestamp:`, the exact text posted/intended, `PostedAs:`, and the issue or comment URL if posted.

---

## Acceptance-Criteria Coverage Matrix

Mapping the 14 ACs from `spec.md §Acceptance Criteria` (in source order) to plan tasks:

| # | AC summary | Delivering / verifying task(s) |
|---|---|---|
| 1 | `Invoke-OpenClawAgentOnboarding.ps1` exists, runs upstream onboard, writes `OPENCLAW_GATEWAY_TOKEN` to `.env`, idempotent unless `-Force` | P4-T1..P4-T11 |
| 2 | `openclaw.json` `gateway.auth.mode` consistent with runbook prose; no contradiction | P3-T5, P6-T3, P6-T5 |
| 3 | `docker-compose.yml` does not hard-code `OPENCLAW_GATEWAY_TOKEN` default | P3-T4 |
| 4 | `.env.example` lists empty `OPENCLAW_GATEWAY_TOKEN=` with comment; `OPENCLAW_AGENT_WORKSPACE` absent | P6-T7 |
| 5 | Validation script `CoreBaseUrl` default is `http://127.0.0.1:8080` | P3-T2 |
| 6 | Entrypoint script does not unconditionally overwrite `/workspace` | P3-T6 |
| 7 | Runbook heading renamed; "optional" framing removed; verification references `18789` not `8181` | P6-T1, P6-T2, P6-T3, P6-T4 |
| 8 | README, architecture diagrams, AGENTS describe agent as required | P6-T5, P6-T6, P6-T8 |
| 9 | Validation test file replaces `8081` with `8080` and adds branch coverage for the four new probes | P3-T3, P5-T1, P5-T2, P5-T3, P5-T4 |
| 10 | Onboarding test file covers missing Docker, upstream failure, malformed output, idempotency, parameter-vs-prompt | P4-T1..P4-T7 |
| 11 | Pester passes with >= 90% on both new/extended scripts; repo-wide >= 80% | P7-T3, P7-T4 |
| 12 | Baseline, post-change, and comparison evidence artifacts under `artifacts/evidence/` with ISO-8601 timestamps | P0-T5, P0-T6, P0-T7, P7-T1, P7-T2, P7-T3, P7-T4 |
| 13 | Full toolchain: format → analyze → test in a single pass without auto-fix file changes | P7-T1, P7-T2, P7-T3 |
| 14 | Clean-machine integration: `down -v` → onboard → `up --build -d` → validation `Expected` → dashboard accepts token | P8-T1 (documented); manual verification is the integration gate called out in AC-14 and tracked in PR notes |

Validation script line-count cap (supporting policy for AC-6 risk mitigation): P5-T5, P5-T6, P5-T12, P7-T5.
Onboarding script line-count cap (supporting policy): P4-T10, P7-T6.

---

## Preflight

- **Spec coverage:** All 14 ACs addressed per the matrix above; every AC maps to at least one P#-T# task.
- **Toolchain (PowerShell):** Baseline capture at P0-T5 (format), P0-T6 (analyze), P0-T7 (test+coverage). Final QA loop at P7-T1 (format), P7-T2 (analyze), P7-T3 (test+coverage). Coverage comparison at P7-T4. Type-check step intentionally omitted per `.claude/rules/powershell.md`.
- **Evidence:** Baseline artifacts declared under `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/`. Regression-testing evidence under `.../regression-testing/`. Final artifacts under `.../final/`. Inflight reference diff under `.../inflight-reference/`. Every evidence-producing task names its artifact path and the four required fields (`Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`) are required on every command-step artifact.
- **Budget:** `large` path selected. Production-file count: 3 (extended `Invoke-OpenClawContainerPathValidation.ps1`, new `Invoke-OpenClawAgentOnboarding.ps1`, new module `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` plus conditional `OpenClawOnboarding.psm1`). Routed per `powershell-change-budget-router` contract.
- **File-size cap (500 lines):** Pre-extension baseline at P5-T5, post-extension verification at P5-T12, final verification at P7-T5 (validation script) and P7-T6 (onboarding script). Helper extraction to modules is the mandated mitigation when a file approaches the cap.
- **Policy compliance:**
  - Tonality — plan prose is factual and measured; no humor, hyperbole, or decorative metaphor.
  - Unit-test policy — tests use Pester mocks; no temporary files; no external services; deterministic inputs; Arrange-Act-Assert structure.
  - General-code-change — fail-fast error handling in both new scripts (distinct error categories), no silent catches, 500-line cap enforced, advanced functions with `CmdletBinding`, ShouldProcess on state-changing calls.
  - Evidence-and-timestamp — ISO-8601 `yyyy-MM-ddTHH-mm` applied throughout; canonical `artifacts/evidence/` rooting with `baseline/`, `regression-testing/`, `final/`, and `issue-updates/` subpaths.
- **Plan-path continuity:** This plan is written to the exact path `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/plan.2026-04-20T09-21.md` and will be updated in place across any revision cycles.
- **Expect-fail tagging:** All Phase 2, Phase 4 (pre-implementation), and Phase 5 (pre-implementation) regression tests carry the `[expect-fail]` marker with explicit failing-run artifact paths.

PREFLIGHT: ALL CLEAR
