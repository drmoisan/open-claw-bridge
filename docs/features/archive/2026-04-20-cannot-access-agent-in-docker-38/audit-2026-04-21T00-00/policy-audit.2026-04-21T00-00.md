# Policy Compliance Audit: cannot-access-agent-in-docker (Issue #38)

**Audit Date:** 2026-04-21
**Feature branch:** `bug/cannot-access-agent-in-docker-38`
**Head commit:** `92d7ee69dcd66f126d7a957e3071f00dd5373ed3` (fix(openclaw-agent): close dashboard-auth gap and deliver verified install flow (#38))
**Base branch:** `origin/development`
**Merge-base SHA:** `7bd92a8cb772c8f41a85831416a5fec952a2330b`
**Work Mode:** `full-bug` (from `issue.md`)
**AC Source:** `spec.md` §Acceptance Criteria (per `full-bug` mode)

**Code Under Test (branch diff vs merge-base):**

| File | Change | Lines |
|---|---|---|
| `scripts/Invoke-OpenClawAgentOnboarding.ps1` | NEW | 221 |
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | NEW | 264 |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | NEW | 410 |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` | NEW | 27 |
| `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` | NEW | 207 |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` | NEW | 742 |
| `deploy/docker/openclaw-agent.Dockerfile` | NEW | 11 |
| `deploy/docker/openclaw-agent-entrypoint.sh` | NEW | 39 |
| `deploy/docker/openclaw-assistant/openclaw.json` | MODIFIED | +8/-2 |
| `deploy/docker/openclaw-core.Dockerfile` | MODIFIED | +22 |
| `docker-compose.yml` | MODIFIED | +8/-7 |
| `docker-compose.dev.yml` | MODIFIED | +1/-1 |
| `.env.example` | MODIFIED | +11/-4 |
| `README.md` | MODIFIED | +31/-6 |
| `AGENTS.md` | MODIFIED | +1/-0 |
| `docs/architecture-diagrams.md` | MODIFIED | +3/-3 |
| `docs/mailbridge-runbook.md` | MODIFIED | +41/-20 |
| `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/*` | NEW (scoping + evidence mirror) | +1,171 |

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|----------------------|-------------------|
| PowerShell | 3 new prod + 2 new test + 1 new module | 97 tests total (24 new) | PASS (97/97) | 81.71% | 86.97% (+5.26pp) | 90.28% (validation), 98.55% (onboarding), 94.63% (module) |
| Shell (entrypoint) | 1 new | N/A (no unit-test harness for .sh) | N/A | N/A | N/A | N/A |
| YAML (compose) | 2 modified | N/A | N/A | N/A | N/A | N/A |
| JSON (config) | 1 modified | validated via `ConvertFrom-Json` | PASS | N/A | N/A | N/A |
| Markdown | 4 modified + scoping docs | N/A | N/A | N/A | N/A | N/A |
| Python | 0 | N/A | N/A | N/A | N/A | N/A |
| C# | 0 | N/A | N/A | N/A | N/A | N/A |
| TypeScript | 0 | N/A | N/A | N/A | N/A | N/A |

---

## Executive Summary

The feature branch introduces a token-based onboarding flow for the OpenClaw agent dashboard and a consolidated diagnostic script that returns a single `Expected`/`Unexpected` verdict covering Docker engine, container health, core endpoints, agent readyz, in-container HostAdapter reachability, `.env` token presence, and dashboard auth. Shared helpers are extracted into a `OpenClawContainerValidation` module. Documentation (`README.md`, `docs/mailbridge-runbook.md`, `docs/architecture-diagrams.md`, `AGENTS.md`) reframes the agent as a required peer service, and port references are normalized to `${OPENCLAW_AGENT_PORT:-18789}`. `docker-compose.yml` removes the `:-openclaw-dev-token` default so the stack fails fast when `.env` is not onboarded. `deploy/docker/openclaw-agent-entrypoint.sh` uses `if [ ! -e ]` guards so operator onboarding state under `/workspace` survives container restarts.

Executor toolchain evidence (PoshQC format, PSScriptAnalyzer, Pester + coverage):
- Format: PASS, 27 files checked, zero rewrites.
- Analyze: PASS, 0 diagnostics after initial remediation (help blocks, OutputType attributes, singular-noun rename, justified suppressions on tests only).
- Pester: PASS 97/97, 0 failures.
- Coverage: repo-wide 86.97% (+5.26pp vs baseline 81.71%); `Invoke-OpenClawContainerPathValidation.ps1` 90.28%, `Invoke-OpenClawAgentOnboarding.ps1` 98.55%, module 94.63%. All meet thresholds.

Findings summary:
- **FAIL** — `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` is 742 lines; the repository 500-line file cap applies explicitly to test code and has no test-file exception. The spec §Risks & Mitigations anticipated this pressure for the production file only; the same mitigation (module split or file split) must apply to the test file.
- **PARTIAL** — The onboarding script wires the upstream `onboard` call to the service name `openclaw-agent` with binary path `dist/index.js`, but the baked image `CMD` in `deploy/docker/openclaw-agent.Dockerfile` is `node openclaw.mjs gateway --allow-unconfigured`. The spec flagged `dist/index.js` as "pending upstream verification per research §3.1". No runtime verification is recorded against the real `${OPENCLAW_AGENT_IMAGE}` layer. AC-1 evidence is a Pester pass-through with mocked docker and will not detect a binary-name mismatch at deploy time.
- **PASS** — All other policy sections including unit test policy, general code change policy, PowerShell-specific policy, and tonality.

Policy documents evaluated:
- PASS `CLAUDE.md` (repo standing instructions)
- PASS `.claude/rules/general-code-change.md` (except FAIL on test file size below)
- PASS `.claude/rules/general-unit-test.md`
- PASS `.claude/rules/powershell.md`
- PASS `.claude/rules/tonality.md`

Language-specific policies evaluated:
- PASS PowerShell (`.claude/rules/powershell.md`) — 3 production `.ps1/.psm1` + 2 Pester test files + 1 `.psd1` manifest.
- N/A Python — zero Python files in branch diff.
- N/A C# — zero C# files in branch diff.
- N/A TypeScript — zero TypeScript files in branch diff.

Temporary artifacts cleanup: PASS — no throwaway scripts in the change.

## Rejected Scope Narrowing

No caller narrowing detected. The orchestrator prompt explicitly instructed "Execute the full `feature-review-workflow` SKILL contract end-to-end" and "Determine scope and required toolchain steps per the skill contract." Scope is the full branch diff against `origin/development` at merge-base `7bd92a8cb772c8f41a85831416a5fec952a2330b`. No language with changed files in the diff has been marked N/A or UNVERIFIED — PowerShell carries an explicit PASS verdict; Python, C#, and TypeScript carry explicit N/A verdicts tied to zero changed files.

The prompt noted: "PR context artifacts: Not generated by this orchestrator run — the `pr-context-artifacts` skill's documented collector does not exist in this repository. Use the git-native base/merge-base SHAs." This is not a scope narrowing — it is a base-and-evidence source selection. The on-disk `artifacts/pr_context.summary.txt` references a prior branch (`feature/bundle-install-script-36`) and was not regenerated for this review because no collector is present in the repo; evidence was taken from git-native diffs, the executor-produced artifacts under `artifacts/evidence/2026-04-20T09-21-issue-38/`, and the feature scoping docs.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|---|---|---|
| Independence | PASS | Each `It` block sets up its own mocks in `BeforeEach`; `$script:DockerRequests`, `$script:WrittenContent`, `$script:RequestedUris` are freshly allocated per test. `AfterEach` tears down the fake `Invoke-FakeDocker` function and globals. |
| Isolation | PASS | Tests target individual probes and the top-level orchestration. Failures in one test do not cascade. `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` organizes probes per `It`. |
| Fast Execution | PASS | `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-test.md` reports 97 tests in 5.69s. |
| Determinism | PASS | External `docker` CLI replaced by the `Invoke-FakeDocker` function-shim pattern. `Invoke-WebRequest` replaced via `Mock -ModuleName OpenClawContainerValidation`. `Read-Host` mocked. No wallclock dependence. |
| Readability & Maintainability | PASS | Test names describe behavior (`'returns expected when all container endpoints match their validation contracts'`, `'Onboarding script is idempotent when OPENCLAW_GATEWAY_TOKEN is already present in .env and -Force is not supplied'`). Arrange-Act-Assert structure observable in each `It`. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|---|---|---|
| Baseline Coverage Documented | PASS | `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-test.md` captures pre-implementation 81.71%. |
| No Coverage Regression | PASS | `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`: +5.26pp repo-wide. No pre-existing file regressed. |
| New Code Coverage >= 90% | PASS | `Invoke-OpenClawAgentOnboarding.ps1` 98.55%; `Invoke-OpenClawContainerPathValidation.ps1` 90.28%; `OpenClawContainerValidation.psm1` 94.63%. |
| Comprehensive Coverage | PASS | 17 tests cover the validation script (all five probe groups + docker-engine + container + endpoint mixes + unhealthy/missing container paths). 7 tests cover onboarding (missing Docker, upstream non-zero, malformed output, idempotency, `-Force`, explicit key, prompt path). |
| Positive Flows | PASS | e.g., `'returns expected when all container endpoints match their validation contracts'`. |
| Negative Flows | PASS | e.g., `'returns unexpected when readiness diagnostics report a degraded dependency'`, `'Onboarding script propagates non-zero exit from upstream onboard command'`. |
| Edge Cases | PASS | Empty `.env`, missing key, empty value, malformed onboard output, unhealthy container, unreachable URI, non-JSON body on `/auth/verify`. |
| Error Handling | PASS | `Should -Throw -ExpectedMessage '*Docker*'`, `'*onboard*'`, `'*malformed*'` explicit. |
| Concurrency | N/A | No concurrent code paths. |
| State Transitions | N/A | No state machine. |

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|---|---|---|
| Clear Failure Messages | PASS | Pester `Should -Throw -ExpectedMessage` uses wildcard patterns that match the exact category terms in the error strings. |
| Arrange-Act-Assert Pattern | PASS | Tests show clear AAA boundaries with `BeforeEach` arrange + in-test arrange + `& $ScriptPath` act + `Should` assert. |
| Document Intent | PASS | `Describe`/`It` names communicate scenario and outcome. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|---|---|---|
| Avoid External Dependencies | PASS | No network, no real docker, no real filesystem writes. `Mock -ModuleName OpenClawContainerValidation` injects `Get-Content`, `Test-Path`, `Invoke-WebRequest`, `Set-Content`. |
| Use Mocks/Stubs | PASS | Explicit module-scoped mocks and function-shim pattern. |
| No Temporary Files | PASS | Inspection of both test files shows no `New-TemporaryFile`, no `GetTempFileName`, no `System.IO.Path]::GetTempPath()` calls. The `$Global:__OpenClawOnboardingTestWrittenContent` list captures writes in memory and is torn down in `AfterEach`. Global variables are documented with justified `SuppressMessageAttribute` entries. |
| Stable Environment | PASS | No reliance on host `.env`, host docker, or time. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|---|---|---|
| Pre-submission Review | PASS | This audit. |

---

## 2. General Code Change Policy Compliance

### 2.1 Design Principles

| Requirement | Status | Evidence |
|---|---|---|
| Simplicity first | PASS | Scripts use advanced functions with a small set of probe helpers. Orchestration is a flat sequence of probe invocations followed by aggregation. |
| Reusability | PASS | Shared helpers (URL composition, docker wrapper, HTTP wrapper, `.env` parsing, JSON parsing) extracted into `OpenClawContainerValidation.psm1`. The onboarding script keeps its own small helper set because no other caller needs them yet. |
| Extensibility | PASS | Both scripts use `CmdletBinding` with keyword parameters and sensible defaults. New probes can be added without disrupting callers. |
| Separation of concerns | PASS | Probe logic (side effects) lives in helper functions. Orchestration is the only place that aggregates `supportingDiagnostics` and computes `OverallResult`. |

### 2.2 Classes, Functions, and APIs

| Requirement | Status | Evidence |
|---|---|---|
| Advanced functions | PASS | Every new function declares `[CmdletBinding()]`. State-changing functions (`Set-OpenClawEnvEntry`, script-level `Invoke-OpenClawAgentOnboarding`) declare `SupportsShouldProcess`. |
| Small, focused methods | PASS | Each probe function is < 50 lines. |
| Interfaces for multiple implementations | N/A | No plurality of implementations required. |

### 2.3 Mandatory Toolchain Loop

| Step | Status | Evidence |
|---|---|---|
| Formatting (Invoke-Formatter / PoshQC format) | PASS | `final/2026-04-20T09-21-final-poshqc-format.md`: "All files reported 'Already formatted'; no file was rewritten." |
| Linting (PSScriptAnalyzer / PoshQC analyze) | PASS | `final/2026-04-20T09-21-final-poshqc-analyze.md`: 0 diagnostics after remediation. |
| Type checking | N/A | PowerShell has no type-check step per `.claude/rules/powershell.md`. |
| Testing (Pester) | PASS | `final/2026-04-20T09-21-final-poshqc-test.md`: 97/97 pass. |

The loop completed without auto-fix rewrites in the final pass.

### 2.4 File Size Limit

| Requirement | Status | Evidence |
|---|---|---|
| Production code <= 500 lines | PASS | `Invoke-OpenClawAgentOnboarding.ps1` 221 lines; `Invoke-OpenClawContainerPathValidation.ps1` 264 lines; `OpenClawContainerValidation.psm1` 410 lines. |
| Test code <= 500 lines | **FAIL** | `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` is 742 lines. The rule `.claude/rules/general-code-change.md` line 40 states: "No production code, test code, or reusable script file may exceed 500 lines. Exceptions: temporary throwaway scripts created and deleted within an agent session; raw text fixtures for language-processing test data; Markdown documentation files." This test file qualifies for none of the listed exceptions. The file is 242 lines over the cap (48% over). The spec §Risks at line 358 and the research artifact §7 both explicitly warned that the test file would need to stay under 500 lines after additions. |
| Reusable script files <= 500 lines | PASS | `OpenClawContainerValidation.psm1` 410 lines; `.psd1` manifest 27 lines; other tests (`Invoke-OpenClawAgentOnboarding.Tests.ps1` 207 lines) are within cap. |

Remediation guidance for the FAIL:
- Split `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` into multiple Pester files organized by probe group (e.g., `Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1`, `...TokenPresence.Tests.ps1`, `...DashboardAuth.Tests.ps1`, `...HostAdapterInContainer.Tests.ps1`, plus a base `...Tests.ps1` retaining the top-level orchestration and container/endpoint scenarios). Shared `BeforeAll`/`BeforeEach` setup can live in a test-helpers script dot-sourced into each file.
- Alternatively, extract the fixtures (docker fake output strings, `Invoke-WebRequest` URL->response switch tables) into a `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` and import from the test file. Fixture data is not obviously "raw text fixtures for language-processing test data," so a split is preferred.

### 2.5 Error Handling and Logging

| Requirement | Status | Evidence |
|---|---|---|
| Fail fast and explicit | PASS | `Invoke-OpenClawAgentOnboarding.ps1`: `throw` for missing Docker, non-zero onboard exit, malformed output. `Invoke-OpenClawContainerPathValidation.ps1`: `$ErrorActionPreference = 'Stop'`; probe helpers return `IsExpected = $false` with a specific `Summary` instead of silent failure. |
| No silent catch-alls | PASS | The only `try/catch` blocks in the module and scripts (`ConvertFrom-OpenClawJsonContent`, `Invoke-OpenClawEndpointRequest`, `Invoke-OpenClawDockerCommand`, `Get-OpenClawContainerInspection`) convert the failure into a structured diagnostic with an error-message field. This is intentional non-fatal probing, not silent swallowing. |
| Project logging pattern | PASS | Scripts use `Write-Verbose` for idempotent-skip notices and `Write-Output` for the human-readable table path. |
| Invariants at init | PASS | `Assert-OpenClawDockerAvailable` is called before any state-changing work. |
| Assertions only for sanity | PASS | No `Assert-*` pattern used for user-facing errors. |

### 2.6 Naming

| Requirement | Status | Evidence |
|---|---|---|
| Descriptive names | PASS | `Invoke-OpenClawReadyzProbe`, `Test-OpenClawGatewayTokenPresence`, `Invoke-OpenClawDashboardAuthProbe` are unambiguous. |
| Language conventions | PASS | PowerShell verb-noun pattern used throughout. `PSUseSingularNouns` remediation already applied (`Get-OpenClawEnvEntries` → `Get-OpenClawEnvEntryMap`). |

### 2.7 Public APIs and Compatibility

| Requirement | Status | Evidence |
|---|---|---|
| Keyword-style parameters with defaults | PASS | Both scripts declare named parameters with sensible defaults and `[Parameter]` attributes. |
| No break in existing parameter contract | PASS | Validation script preserves `-CoreBaseUrl`, `-AgentBaseUrl`, `-CoreContainerName`, `-AgentContainerName`, `-DockerPath`, `-TimeoutSeconds`, `-PassThru`, `-AsJson`; adds `-EnvFilePath` as an additive optional parameter. |
| Breaking change callouts | PASS | `docker-compose.yml` removal of the `:-openclaw-dev-token` default is called out in `pr-notes.md` §Breaking Changes. |

### 2.8 Dependencies

| Requirement | Status | Evidence |
|---|---|---|
| Only approved libraries | PASS | No new NuGet / npm / PyPI packages. PowerShell modules are all in-repo. |
| Pester/PSScriptAnalyzer usage | PASS | No change to existing tool versions. |

### 2.9 I/O Boundaries

| Requirement | Status | Evidence |
|---|---|---|
| Isolated I/O | PASS | `Invoke-WebRequest`, docker CLI invocations, `Get-Content`/`Set-Content` live in small wrappers that return structured objects. The orchestrator composes probe objects from these. |
| Core logic testable without net/FS | PASS | Tests demonstrate this by mocking each I/O boundary. |
| No temp files in tests | PASS | Inspection confirmed. |

---

## 3. Language-Specific Code Change Policy Compliance

### PowerShell (`.claude/rules/powershell.md`)

| Requirement | Status | Evidence |
|---|---|---|
| PowerShell 7+ compatible | PASS | Both scripts declare `#Requires -Version 7`. |
| Advanced functions with `CmdletBinding` | PASS | Verified in source. |
| `[Parameter(Mandatory = ...)]` and validators | PASS | `Invoke-OpenClawContainerPathValidation.ps1` uses `[ValidateRange(1, 300)]` on `-TimeoutSeconds`. Onboarding script uses explicit `[Parameter]` attributes. |
| `SupportsShouldProcess` for state-changing | PASS | `Invoke-OpenClawAgentOnboarding.ps1` declares it and `Set-OpenClawEnvEntry` calls `$PSCmdlet.ShouldProcess`. |
| No global state / mutable script-scoped | PARTIAL | Production code does not use globals. The tests use `$Global:__OpenClawOnboardingTestWrittenContent` as a documented mock-scope bridge with a justified `PSAvoidGlobalVars` suppression on the test file (not on production code). The `$global:LASTEXITCODE` write inside `Invoke-OpenClawOnboardCommand` and `Invoke-OpenClawDockerCommand` follows the standard PowerShell pattern for native-command exit tracking. PASS in effect. |
| No `Invoke-Expression` / plaintext secrets / hard-coded creds | PARTIAL | `Invoke-OpenClawAgentOnboarding.ps1` accepts `-AnthropicApiKey` as a `[SecureString]`, then calls `ConvertFrom-OpenClawSecureString` to marshal the key into a plaintext argument for `docker compose run ... --anthropic-api-key <value>`. This is necessary because the upstream `onboard` CLI consumes `--anthropic-api-key` as plaintext; the script clears the plaintext variable in a `finally` block. This is the minimum-exposure path the upstream contract allows. Accepting SecureString at the boundary is correct; the in-memory plaintext window is scoped and documented. PASS in effect with a note: the plaintext key is visible in the `docker ps` / process-list scrape window on the host until the container exits. The script cannot avoid this without an upstream change to accept the key on stdin. Remediation recommendation: capture this residual exposure in `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/followups.md` as an upstream tracking item. |
| `Write-Error` / `throw` for failures | PASS | Every failure path uses `throw` with a distinct message. |
| Approved verbs | PASS | Verbs used: `Assert`, `Get`, `Set`, `Test`, `Invoke`, `ConvertFrom`. All approved. |
| Cohesive, under 500 lines | PASS for production (see 2.4); FAIL for test file (see 2.4). |
| Pester v5.x | PASS | Test files use `Describe`/`Context`/`It`, `BeforeAll`/`BeforeEach`/`AfterEach`, `Mock -ModuleName`, `Should -Invoke`, `Should -Throw -ExpectedMessage` — all Pester 5.x idioms. |
| Coverage >= 80% repo-wide, >= 90% new code | PASS | See §1.2. |
| Coverage regression on changed lines | PASS | No regression; see §1.2. |

---

## 4. Language-Specific Unit Test Policy Compliance

### PowerShell

| Requirement | Status | Evidence |
|---|---|---|
| Pester v5.x framework | PASS | Confirmed. |
| Tests mirror code structure | PASS | `tests/scripts/Invoke-OpenClaw*.Tests.ps1` mirror `scripts/Invoke-OpenClaw*.ps1`. |
| `*.Tests.ps1` naming | PASS | Both files use the naming. |
| `Describe`/`Context`/`It` blocks; one behavior per `It` | PASS | Confirmed by inspection. |
| Focused tests | PASS | Each `It` exercises a single scenario. |
| Mock sparingly; prefer real code paths | PASS | Mocks limited to I/O boundaries. Probe helpers are executed as real code. |
| No external dependencies | PASS | See §1.4. |

---

## 5. Test Coverage Detail

Source: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`, computed from `artifacts/pester/powershell-coverage.koverage.xml`.

| File | Status | Line Coverage |
|---|---|---|
| `scripts/Invoke-OpenClawAgentOnboarding.ps1` | NEW | 98.55% (68 covered / 1 missed) |
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | NEW | 90.28% (130 covered / 14 missed) |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | NEW | 94.63% (141 covered / 8 missed) |
| Repo-wide PowerShell | (rollup) | 86.97% (722 covered / 830 analyzable across 15 files) |
| Baseline (pre-implementation, clean tree) | — | 81.71% |
| Delta | — | +5.26pp |

Thresholds:
- Repo-wide >= 80%: PASS (86.97%).
- New files >= 90%: PASS (all three new/extended files >= 90%).
- Modified files no regression: PASS — pre-existing PowerShell files were not modified by this feature.

## 6. Test Execution Metrics

Source: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-test.md`.

- Tests Passed: 97
- Tests Failed: 0
- Tests Skipped: 0
- Inconclusive: 0
- NotRun: 0
- Duration: 5.69s
- New tests added by this feature: 24 (17 validation-script, 7 onboarding).

## 7. Code Quality Checks

| Check | Status | Evidence |
|---|---|---|
| PoshQC format | PASS | `final/2026-04-20T09-21-final-poshqc-format.md` |
| PSScriptAnalyzer | PASS | `final/2026-04-20T09-21-final-poshqc-analyze.md` |
| Line count — production scripts | PASS | `final/2026-04-20T09-21-validation-script-line-count-final.md` (264), `final/2026-04-20T09-21-onboarding-script-line-count-final.md` (221) |
| Line count — test files | **FAIL** | `Invoke-OpenClawContainerPathValidation.Tests.ps1` = 742 (see §2.4) |
| JSON validity | PASS | `regression-testing/2026-04-20T09-21-openclaw-json-parse.md` confirms `openclaw.json` parses. |
| Shell script `set -eu` | PASS | `deploy/docker/openclaw-agent-entrypoint.sh` line 2. Conditional-copy guards (`if [ ! -e "$target" ]`) are in place for each seed file (6 workspace files + 1 skills subdir file). Note: `/.openclaw/openclaw.json` copy is intentionally unconditional per inline comment because `/.openclaw` is a tmpfs that is always re-seeded. |

## 8. Gaps and Exceptions

1. **FAIL — Test file size cap.** `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` at 742 lines violates the 500-line cap in `.claude/rules/general-code-change.md`. No exception in the rule set applies. Remediation: split per §2.4 guidance.

2. **PARTIAL — Upstream onboard binary path.** `scripts/Invoke-OpenClawAgentOnboarding.ps1` line 131 invokes `docker compose ... --entrypoint node openclaw-agent dist/index.js onboard ...`, but `deploy/docker/openclaw-agent.Dockerfile` line 12 specifies `CMD ["node","openclaw.mjs","gateway","--allow-unconfigured"]`. The spec explicitly flags `dist/index.js` as "pending upstream verification per research §3.1," and no evidence artifact demonstrates a real container run against `ghcr.io/openclaw/openclaw:latest` resolved the path. The tests mock docker entirely, so Pester cannot detect a runtime mismatch. This is a known risk called out in `spec.md` Risks. Remediation: either (a) record a single real-docker smoke-run artifact under `artifacts/evidence/` that exercises the onboard path, or (b) document the residual risk in `followups.md` as a manual verification gate that must be run before any release.

3. **Note — Plaintext Anthropic key on process-list.** The onboarding script marshals the SecureString key into a plaintext CLI argument because the upstream `onboard` CLI requires plaintext. The key appears in the `docker ps` / native-process argv window for the lifetime of the onboarding container run. This is an upstream constraint, not a change introduced by this branch. Remediation: track in `followups.md` as an upstream tracking item.

4. **Note — Stale PR context artifacts on disk.** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` at the start of this review referenced a prior branch (`feature/bundle-install-script-36`). The repository does not contain a PR-context collector script, and the orchestrator prompt explicitly instructed using git-native base/merge-base SHAs. This review uses git-native diffs and the executor evidence as authoritative. No remediation requested.

## 9. Summary of Changes

- New scripted onboarding path: `scripts/Invoke-OpenClawAgentOnboarding.ps1` wraps the upstream OpenClaw `onboard` subcommand and persists the generated `OPENCLAW_GATEWAY_TOKEN` to `.env`. Idempotent unless `-Force`. Secrets accepted as `SecureString`.
- Consolidated diagnostic: `scripts/Invoke-OpenClawContainerPathValidation.ps1` aggregates Docker engine, container health (core + agent), core HTTP endpoints (`/health/live`, `/health/ready`, `/api/status`), agent `/` and `/readyz`, in-container HostAdapter reachability via `docker compose exec`, `.env` token presence, and dashboard auth into a single `OverallResult`. The `CoreBaseUrl` default is corrected to `http://127.0.0.1:8080`.
- Shared helpers extracted to `scripts/powershell/modules/OpenClawContainerValidation/` to keep the validation script under the 500-line cap while adding four new probes.
- Dockerfile wrapper (`deploy/docker/openclaw-agent.Dockerfile`) bakes the assistant workspace into the image and sets an entrypoint script that uses `if [ ! -e ]` guards so operator state under `/workspace` survives container restarts.
- `docker-compose.yml` drops the `:-openclaw-dev-token` default so the stack fails fast when `.env` is not onboarded.
- `.env.example` exposes `OPENCLAW_GATEWAY_TOKEN=` (empty) with a comment pointing at the onboarding script; the legacy `OPENCLAW_AGENT_WORKSPACE` key is removed.
- `deploy/docker/openclaw-assistant/openclaw.json` switches `gateway.auth` to `{ mode: token, token: "${OPENCLAW_GATEWAY_TOKEN}" }`.
- Documentation reframes `openclaw-agent` as a required peer service in `README.md`, `docs/mailbridge-runbook.md`, `docs/architecture-diagrams.md`, and `AGENTS.md`. All port references are normalized to `${OPENCLAW_AGENT_PORT:-18789}` (agent), `${OPENCLAW_HTTP_PORT:-8080}` (core), and `4319` (HostAdapter).

## 10. Compliance Verdict

**Overall:** REMEDIATION REQUIRED.

- One FAIL: 500-line test file cap on `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`.
- One PARTIAL: upstream `dist/index.js` vs baked `openclaw.mjs` unresolved; flagged by spec as pending.
- No blockers on toolchain, coverage, or acceptance-criteria evidence.

Recommended remediation:
1. Split the oversized test file into multiple `*.Tests.ps1` modules organized by probe group.
2. Either add a real-docker smoke-run evidence artifact that proves the onboard binary path resolves against `${OPENCLAW_AGENT_IMAGE}`, or document the residual risk in `followups.md` as a manual verification gate required before release.

---

## Appendix A: Test Inventory

**`tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1`** (207 lines, 7 tests):
- Onboarding script fails fast when docker CLI is unavailable
- Onboarding script propagates non-zero exit from upstream onboard command
- Onboarding script fails fast when onboard output contains no OPENCLAW_GATEWAY_TOKEN= line
- Onboarding script is idempotent when OPENCLAW_GATEWAY_TOKEN is already present in .env and -Force is not supplied
- Onboarding script overwrites token when OPENCLAW_GATEWAY_TOKEN is present and -Force is supplied
- Onboarding script consumes -AnthropicApiKey parameter without prompting
- Onboarding script prompts via Read-Host -AsSecureString when -AnthropicApiKey is absent

**`tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`** (742 lines, 17 tests):
- returns expected when all container endpoints match their validation contracts
- returns unexpected when readiness diagnostics report a degraded dependency
- plus 15 additional `It` blocks covering: missing container, unhealthy health status, malformed docker inspect JSON, unreachable URIs, probe-level tests for `AgentReadyz` (positive / HTTP 503 / unreachable), `HostAdapterInContainer` (positive / non-200 / exec failure), `GatewayTokenPresence` (present / missing / empty), `DashboardAuth` (positive / 401 / non-JSON body), and the expect-fail regression tests from Phase 2.

## Appendix B: Toolchain Commands Reference

Commands actually run by the executor (evidence artifacts under `artifacts/evidence/2026-04-20T09-21-issue-38/`):

| Step | Command | Evidence |
|---|---|---|
| Format (baseline) | `Invoke-PoshQCFormat -Root <repo> -ScanFolders scripts, tests/scripts` | `baseline/2026-04-20T09-21-poshqc-format.md` |
| Analyze (baseline) | `Invoke-PoshQCAnalyze -Root <repo> -ScanFolders scripts, tests/scripts` | `baseline/2026-04-20T09-21-poshqc-analyze.md` |
| Test (baseline) | `Invoke-PoshQCTest -Root <repo> -ScanFolders scripts, tests/scripts` | `baseline/2026-04-20T09-21-poshqc-test.md` |
| Format (final) | `Invoke-PoshQCFormat -Root <repo> -ScanFolders scripts, tests/scripts` | `final/2026-04-20T09-21-final-poshqc-format.md` |
| Analyze (final) | `Invoke-PoshQCAnalyze -Root <repo> -ScanFolders scripts, tests/scripts` | `final/2026-04-20T09-21-final-poshqc-analyze.md` |
| Test + Coverage (final) | `Invoke-PoshQCTest -Root <repo> -ScanFolders scripts, tests/scripts` | `final/2026-04-20T09-21-final-poshqc-test.md` |
| Coverage comparison | Compare koverage XML (baseline vs final) | `final/2026-04-20T09-21-coverage-comparison.md` |
| Line-count check (validation script) | `(Get-Content scripts/Invoke-OpenClawContainerPathValidation.ps1).Count` | `final/2026-04-20T09-21-validation-script-line-count-final.md` |
| Line-count check (onboarding script) | `(Get-Content scripts/Invoke-OpenClawAgentOnboarding.ps1).Count` | `final/2026-04-20T09-21-onboarding-script-line-count-final.md` |
| JSON parse check | `Get-Content deploy/docker/openclaw-assistant/openclaw.json -Raw | ConvertFrom-Json` | `regression-testing/2026-04-20T09-21-openclaw-json-parse.md` |

Note on MCP tool binding: the executor evidence records that the `mcp__drmCopilotExtension__run_poshqc_*` MCP tools were not exposed in the executor environment and were substituted by direct invocation of the same `Invoke-PoshQC*` harness module with the same settings files (`.claude/worktrees/agent-a04d81f7/scripts/powershell/PoshQC/settings/pssa.settings.psd1`). This is an equivalent harness substitution, not a skipped step. No further remediation required.
