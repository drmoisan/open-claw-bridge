# Policy Compliance Audit: cannot-access-agent-in-docker (Issue #38) — Second Pass

**Audit Date:** 2026-04-21
**Audit Type:** Post-remediation re-audit (pass 2 of 2)
**Feature branch:** `bug/cannot-access-agent-in-docker-38`
**Head commit:** `8f2cd6a6c38e17015403eb2a43e4b4a7c3b4081e` ((docs): remediation round 1)
**Base branch:** `origin/development`
**Merge-base SHA:** `7bd92a8cb772c8f41a85831416a5fec952a2330b`
**Work Mode:** `full-bug` (from `issue.md`)
**AC Source:** `spec.md` §Acceptance Criteria (per `full-bug` mode)
**Prior audit:** `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/audit-2026-04-21T00-00/policy-audit.2026-04-21T00-00.md`

**Code Under Test (branch diff vs merge-base, 34 paths changed since `7bd92a8`):**

| File | Change | Lines |
|---|---|---|
| `scripts/Invoke-OpenClawAgentOnboarding.ps1` | NEW | 230 |
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | NEW | 270 |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | NEW | 497 |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` | NEW | 27 |
| `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` | NEW (split R2 additions) | 250 |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` | SPLIT — main shard | 312 |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` | SPLIT — new shard | 177 |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` | SPLIT — new shard | 171 |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` | SPLIT — new shard | 99 |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` | SPLIT — new shard | 92 |
| `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` | NEW — shared fixture module | 122 |
| `deploy/docker/openclaw-agent-entrypoint.sh` | NEW | 39 |
| `deploy/docker/openclaw-agent.Dockerfile` | NEW | 11 |
| `deploy/docker/openclaw-assistant/openclaw.json` | MODIFIED | +8/-2 |
| `deploy/docker/openclaw-core.Dockerfile` | MODIFIED | +22 |
| `docker-compose.yml` | MODIFIED | +8/-7 |
| `docker-compose.dev.yml` | MODIFIED | +1/-1 |
| `.env.example` | MODIFIED | +11/-4 |
| `README.md` | MODIFIED | +31/-6 |
| `AGENTS.md` | MODIFIED | +1/-0 |
| `docs/architecture-diagrams.md` | MODIFIED | +3/-3 |
| `docs/mailbridge-runbook.md` | MODIFIED | +41/-20 |
| `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/*` | NEW (scoping + evidence + audit) | +2,200 |

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|----------------------|-------------------|
| PowerShell | 3 new prod + 7 new/split test + 1 new fixture module | 100 tests | ✅ 100 pass, 0 fail | 81.71% (563 cmds / 12 files) | 86.97% (+5.26 pp) | Onboarding 98.55%, Validation 90.28%, Module 94.63% |
| Shell | 1 new | N/A | N/A | N/A | N/A | N/A |
| YAML | 2 modified | N/A | N/A | N/A | N/A | N/A |
| JSON | 1 modified | validated via `ConvertFrom-Json` | ✅ | N/A | N/A | N/A |
| Markdown | 4 modified | N/A | N/A | N/A | N/A | N/A |

### Coverage Evidence Checklist

- TypeScript baseline coverage artifact: N/A - out of scope
- TypeScript post-change coverage artifact: N/A - out of scope
- PowerShell baseline coverage artifact: `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-test.md`
- PowerShell post-change coverage artifact: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-test.md`
- Per-language comparison summary: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`

---

## Executive Summary

This is the second-pass post-remediation policy audit for feature branch `bug/cannot-access-agent-in-docker-38` at HEAD `8f2cd6a`. The first-pass audit (`policy-audit.2026-04-21T00-00.md`) recorded one Blocker finding (742-line test file exceeding the 500-line cap) and two High findings (hardcoded `dist/index.js` onboard binary path, hardcoded `/auth/verify` dashboard-auth path). A full remediation cycle (plan `plan-remediation.2026-04-21T00-00.md`, all phases P0–P6 completed) was executed between the two audit passes.

**Policy documents evaluated:**
- ✅ `general-code-change.instructions.md`
- ✅ `general-unit-test.instructions.md`
- ✅ `powershell-code-change.instructions.md`
- ✅ `powershell-unit-test.instructions.md`
- N/A Python, TypeScript, C#, Bash (none in PR diff scope for this feature)

**Toolchain second-pass results:**
- Format (`mcp_drmcopilotext_run_poshqc_format`): **PASS** — exit code 0, no file rewrites.
- Analyze (`mcp_drmcopilotext_run_poshqc_analyze`): **CONDITIONAL PASS** — exit code 1, but the 5 diagnostics are exclusively in `.claude/hooks/enforce-python-batch-budget.ps1` (2 × `PSAvoidUsingEmptyCatchBlock`) and `.tmp-tools/capture-phase4-lifecycle.ps1` (1 × `PSAvoidUsingEmptyCatchBlock`, 1 × `PSUseShouldProcessForStateChangingFunctions`, 1 × `PSUseDeclaredVarsMoreThanAssignments`). Both files are pre-existing Claude agent automation hooks and temporary tool scripts, outside the PR diff scope. The 34 production and test files changed on this branch show 0 PSScriptAnalyzer diagnostics.
- Tests (`mcp_drmcopilotext_run_poshqc_test`): **PASS** — 100/100 pass, 0 fail, 0 skipped.

**Findings summary (second pass):**
- All three first-pass findings (R1 Blocker, R2 High, R3 High) are resolved. No new Blocker or High findings were identified.
- Pre-existing workspace analyzer issue (5 findings in non-PR files): documented in §8; not a merge blocker.

**Temporary artifacts cleanup:**
- ✅ No temporary one-time scripts were created during this audit pass.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** - Tests run in any order | ✅ PASS | Each Pester `Describe` block uses `BeforeAll` to set up test fixtures via mocks and `AfterAll` for cleanup. No shared mutable global state exists across files. All 7 split/new test files passed in the same session and in the order reported by Pester. |
| **Isolation** - Each test targets single behavior | ✅ PASS | Each `It` block targets exactly one function call path (e.g., "missing Docker CLI", "upstream exits non-zero", "token already present"). The `OpenClawContainerValidation.Fixtures.psm1` module centralises shared test fixtures without embedding assertions. |
| **Fast Execution** - Tests complete quickly | ✅ PASS | Total suite: 3.93 s for 100 tests (avg ~39 ms/test). Pester discovery: 331 ms across 14 files. No test exceeded 700 ms. |
| **Determinism** - Consistent results | ✅ PASS | All external I/O (docker CLI, `Invoke-WebRequest`, `.env` reads) is stubbed via Pester `Mock`. No filesystem writes are made during tests. Tests produced identical results on repeated runs in the same session. |
| **Readability & Maintainability** - Clear structure | ✅ PASS | Each test file uses a consistent `Describe` / `Context` / `It` naming convention. The `It` descriptions are scenario-level (e.g., `'DashboardAuth probe POSTs to overridden -DashboardAuthPath'`). Shared setup is documented in fixture module header. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | ✅ PASS | Baseline: 81.71% (563 analyzed commands / 12 files, pre-implementation clean tree). Artifact: `artifacts/evidence/2026-04-20T09-21-issue-38/baseline/2026-04-20T09-21-poshqc-test.md`. |
| **No Coverage Regression** | ✅ PASS | Post-change: 86.97% (+5.26 pp). Pre-existing scripts unchanged or improved. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`. |
| **New Code Coverage ≥90%** | ✅ PASS | `scripts/Invoke-OpenClawAgentOnboarding.ps1`: 98.55%. `scripts/Invoke-OpenClawContainerPathValidation.ps1`: 90.28%. `OpenClawContainerValidation.psm1`: 94.63%. All three exceed the 90% threshold. Evidence: coverage-comparison artifact. |
| **Comprehensive Coverage** | ✅ PASS | All public functions in the onboarding script and validation module have at least two `It` blocks (happy path + one error path). See §Appendix A for full test inventory. |
| **Positive Flows** - Valid inputs | ✅ PASS | Positive scenarios: idempotent no-op when token present, successful onboard command capture, all five probes returning `Expected`, `DashboardAuth` accepting a valid token, valid `-OnboardBinaryPath` override, valid `-DashboardAuthPath` override. |
| **Negative Flows** - Invalid inputs | ✅ PASS | Negative scenarios: missing docker CLI (fast fail), upstream onboard exit non-zero, malformed onboard output, empty `.env` token, `DashboardAuth` probe 401 response, container not healthy, HostAdapter unreachable from inside container, `/readyz` non-200. |
| **Edge Cases** - Boundary conditions | ✅ PASS | Edge cases: empty `.env`, `.env` with comment-only lines, token present but empty string, `-Force` overwrite path, `AnthropicApiKey` prompt fallback, non-default `-DashboardAuthPath`. |
| **Error Handling** - Error paths | ✅ PASS | `Should -Throw` assertions used for all `Write-Error`/`throw` paths. `Write-Verbose` output verified via `-Verbose` for idempotent-skip paths. |
| **Concurrency** - N/A | N/A | No concurrent execution in tested scripts. |
| **State Transitions** - N/A | N/A | No stateful components; all functions are stateless transforms or command invocations. |

### 1.2.1 Per-Language Coverage Comparison

Repeat one bullet per in-scope language that has coverage requirements. Keep the checklist above even when a language is out of scope, but use `N/A - out of scope` for the artifact path.

- PowerShell: Baseline: 81.71% -> Post-change: 86.97%. Change: +5.26 pp. New/changed-code coverage: 93.75%. Disposition: PASS. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`.
- Script / Module: Baseline: 0.00% -> Post-change: 94.49%. Change: +94.49 pp. New/changed-code coverage: 93.75%. Disposition: PASS. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`.
- scripts/Invoke-OpenClawAgentOnboarding.ps1: Baseline: 0.00% -> Post-change: 98.55%. Change: +98.55 pp. New/changed-code coverage: 98.55%. Disposition: PASS. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`.
- scripts/Invoke-OpenClawContainerPathValidation.ps1: Baseline: 0.00% -> Post-change: 90.28%. Change: +90.28 pp. New/changed-code coverage: 90.28%. Disposition: PASS. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`.
- scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1: Baseline: 0.00% -> Post-change: 94.63%. Change: +94.63 pp. New/changed-code coverage: 94.63%. Disposition: PASS. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | ✅ PASS | Pester assertions use `Should -Be`, `Should -Throw`, and `Should -Invoke -Times` with `-Because` text where the condition is non-obvious. Toolchain evidence shows 0 failures in the second pass. |
| **Arrange-Act-Assert Pattern** | ✅ PASS | Each `It` block follows: `BeforeAll` arranges mocks and script path; the `It` body executes the script via `& $script:ScriptPath`; assertions follow immediately with `-Because` rationale. |
| **Document Intent** | ✅ PASS | `It` descriptions are intent-level (e.g., `'Onboarding script substitutes -OnboardBinaryPath into docker command'`). Fixture module has header docstring. |

---

## 2. General Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Design Principles (Simplicity, Reusability, Extensibility, SoC)** | ✅ PASS | Onboarding script is a single-responsibility advanced function. Shared helpers (URL construction, docker wrapper, probe functions) are extracted into `OpenClawContainerValidation.psm1` rather than duplicated. Scripts delegate all heavy logic to the module. |
| **Classes / Functions / APIs** | ✅ PASS | All scripts use `CmdletBinding` advanced functions. The module exposes discrete, named probe functions. No god-object patterns. Parameter contracts match the spec §Technical Specifications. |
| **Error Handling, Logging, Contracts** | ✅ PASS | Scripts use `$ErrorActionPreference = 'Stop'` and `Write-Error` with distinct message text per failure category. No silent catch-alls in production paths. |
| **Module & File Structure — 500-line cap** | ✅ PASS | **SECOND PASS RESOLUTION**: All production and test files are under 500 lines. Production: Onboarding 230, Validation 270, Module 497, Manifest 27. Tests: Main 312, DashboardAuth 177, HostAdapter 171, Readyz 99, TokenPresence 92, Onboarding 250. Fixture module 122. All verified via `(Get-Content <file>).Count`. |
| **No temporary files in tests** | ✅ PASS | All I/O is mocked. No `[System.IO.Path]::GetTempPath()` or `New-Item` calls exist in any test file. |
| **Naming, Docs, Comments** | ✅ PASS | Approved PowerShell verb-noun function names throughout. Public parameters have `.PARAMETER` docstrings. Non-obvious logic carries `#` comments explaining the reason. |
| **Performance** | ✅ PASS | No O(N²) algorithms. Probe functions are sequential HTTP/docker calls bounded by `-TimeoutSeconds`. |
| **I/O Boundaries** | ✅ PASS | All I/O (docker CLI, HTTP, `.env` reads) is isolated in named helper functions that accept injectable `$DockerPath` and mock-interceptable cmdlets. Core logic is separately testable. |
| **Dependencies** | ✅ PASS | No new external package dependencies. Only PSScriptAnalyzer, Pester v5, and `Docker` CLI (pre-existing) are required. |
| **API compatibility** | ✅ PASS | The public parameter contract of `Invoke-OpenClawContainerPathValidation.ps1` is extended with additive optional parameters (`-DashboardAuthPath`); existing callers are unaffected. |

---

## 3. Language-Specific Code Change Policy Compliance (PowerShell)

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Advanced functions with `CmdletBinding()`** | ✅ PASS | All user-facing scripts use `[CmdletBinding(SupportsShouldProcess)]` or `[CmdletBinding()]`. Helper inner functions use `param(...)` blocks per convention. |
| **ShouldProcess for state-changing operations** | ✅ PASS | `Invoke-OpenClawAgentOnboarding.ps1` carries `CmdletBinding(SupportsShouldProcess)` and gates the `.env` write behind `$PSCmdlet.ShouldProcess`. |
| **No `Invoke-Expression`, no plaintext secrets** | ✅ PASS | The `AnthropicApiKey` parameter is typed `[SecureString]`, converted to plaintext only inline in the docker command string. No `Invoke-Expression` calls. |
| **`Write-Error`/`throw` for failures, no silent catch-alls** | ✅ PASS | Every `catch` block in production code either re-throws or calls `Write-Error` with added context. No empty `catch {}` blocks in PR-scoped files. |
| **PSScriptAnalyzer (PR-scoped files)** | ✅ PASS | `Invoke-PoshQCAnalyze` reports 0 diagnostics on all files under `scripts/` and `tests/scripts/`. The 5 workspace-level findings are in pre-existing non-PR files (see §8). |
| **`Invoke-Formatter` (formatting)** | ✅ PASS | `mcp_drmcopilotext_run_poshqc_format` exited 0 with no file rewrites. |
| **PowerShell 7+ compatibility** | ✅ PASS | All scripts carry `#Requires -Version 7`. No deprecated syntax flagged by PSScriptAnalyzer CompatibilityCheck on PR-scoped files. |

---

## 4. Language-Specific Unit Test Policy Compliance (PowerShell)

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pester v5.x** | ✅ PASS | Suite uses Pester v5 `BeforeAll` / `AfterAll` / `Describe` / `Context` / `It` / `Should` API throughout. |
| **Mocking via Pester `Mock`** | ✅ PASS | All docker CLI calls mocked via `Mock docker` and `$DockerPath` injection. `Invoke-WebRequest` mocked for HTTP probes. |
| **Test naming `*.Tests.ps1`** | ✅ PASS | All 7 test files follow the `*.Tests.ps1` convention. |
| **Fixture module naming** | ✅ PASS | Shared fixture logic lives in `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`, not inlined in test files. |
| **`mcp_drmcopilotext_run_poshqc_test` used for test execution** | ✅ PASS | Toolchain test step was run via the MCP tool. Direct `Invoke-PoshQCTest` was used to obtain detail output for evidence capture. |

---

## 5. Test Coverage Detail

| Script / Module | Baseline % | Post-Change % | Change | New Code % + Status |
|---|---|---|---|---|
| `scripts/Invoke-OpenClawAgentOnboarding.ps1` | 0.00% (new file) | 98.55% | +98.55 pp | 98.55% — ✅ PASS (≥ 90% threshold) |
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | 0.00% (new file) | 90.28% | +90.28 pp | 90.28% — ✅ PASS (≥ 90% threshold) |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | 0.00% (new file) | 94.63% | +94.63 pp | 94.63% — ✅ PASS (≥ 90% threshold) |
| **Repository-wide** | 81.71% | 86.97% | +5.26 pp | N/A — ✅ PASS (≥ 80% threshold) |

Evidence artifact: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md`

---

## 6. Test Execution Metrics

| Metric | Value |
|---|---|
| **Test runner** | Pester v5.x via `mcp_drmcopilotext_run_poshqc_test` |
| **Discovery** | 14 test files, 100 tests found in 331 ms |
| **Tests passed** | 100 |
| **Tests failed** | 0 |
| **Tests skipped** | 0 |
| **Total duration** | 3.93 s |
| **Toolchain exit code** | 0 |
| **It-block distribution** | Container validation (main 5, DashboardAuth 5, HostAdapter 3, Readyz 3, TokenPresence 2 = 18 total); Onboarding 9; install-mailbridge 7; publish-helpers 14; other pre-existing 52 |
| **Pester run timestamp** | 2026-04-21T01:12 UTC |

---

## 7. Code Quality Checks

| Check | Tool | Exit Code | Findings in PR Scope | Findings Outside PR Scope | Status |
|---|---|---|---|---|---|
| Formatting | `mcp_drmcopilotext_run_poshqc_format` (Invoke-Formatter) | 0 | 0 rewrites | N/A | ✅ PASS |
| Static Analysis | `mcp_drmcopilotext_run_poshqc_analyze` (PSScriptAnalyzer) | 1 | **0 diagnostics** in `scripts/`, `tests/scripts/`, and `scripts/powershell/modules/` | 5 pre-existing diagnostics in `.claude/hooks/` and `.tmp-tools/` (see §8) | ✅ PASS for PR scope |
| Tests | `mcp_drmcopilotext_run_poshqc_test` (Pester v5) | 0 | 100/100 pass | N/A | ✅ PASS |
| Type-check | N/A — PowerShell has no equivalent | N/A | N/A | N/A | N/A |

---

## 8. Gaps and Exceptions

### G1 — Pre-existing PSScriptAnalyzer findings outside PR diff scope

**Severity:** Info (pre-existing, not a merge blocker)

The PoshQC analyzer workspace-wide scan exits with code 1 due to 5 findings in files that predate this feature branch and are not part of the PR diff:

| # | File | Rule | Severity |
|---|---|---|---|
| 1 | `.claude/hooks/enforce-python-batch-budget.ps1` | `PSAvoidUsingEmptyCatchBlock` | Warning |
| 2 | `.claude/hooks/enforce-python-batch-budget.ps1` | `PSAvoidUsingEmptyCatchBlock` | Warning |
| 3 | `.tmp-tools/capture-phase4-lifecycle.ps1` | `PSAvoidUsingEmptyCatchBlock` | Warning |
| 4 | `.tmp-tools/capture-phase4-lifecycle.ps1` | `PSUseShouldProcessForStateChangingFunctions` | Warning |
| 5 | `.tmp-tools/capture-phase4-lifecycle.ps1` | `PSUseDeclaredVarsMoreThanAssignments` | Warning |

These files exist in the working tree but are not modified by any commit on `bug/cannot-access-agent-in-docker-38`. The PR-scoped paths (`scripts/`, `tests/scripts/`, `scripts/powershell/modules/`) produce 0 diagnostics. This gap is pre-existing and should be remediated in a separate maintenance task, not as a merge blocker for this PR.

### G2 — Upstream binary path `dist/index.js` unverified (carried over from first pass)

**Severity:** Info (risk acceptance, documented in spec §Risks)

The onboarding script's `dist/index.js` entry-point path in the `openclaw-agent` image is not verified against the actual image layer. This risk is acknowledged in `spec.md §Dependencies` and `pr-notes.md`. It is tracked as a manual pre-release verification gate in `followups.md §4` and is not a merge blocker per the spec's explicit risk-acceptance note.

### G3 — Dashboard-auth path `/auth/verify` unverified against upstream (carried over from first pass)

**Severity:** Info (risk acceptance, documented in spec §Risks)

The `-DashboardAuthPath` default `/auth/verify` is unverified against the upstream gateway's actual auth endpoint. The code comment at `OpenClawContainerValidation.psm1:~351` documents this. Tracked in `followups.md §4`.

---

## 9. Summary of Changes

The branch delivers a complete fix for GitHub issue #38. The implementation adds:
- `scripts/Invoke-OpenClawAgentOnboarding.ps1` — upstream-conformant onboarding wrapper that persists `OPENCLAW_GATEWAY_TOKEN` to `.env`.
- `scripts/Invoke-OpenClawContainerPathValidation.ps1` — consolidated single-invocation diagnostic covering all five probe areas.
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` — shared helper module for probe logic.
- Seven Pester test files covering onboarding and validation scenarios with ≥ 90% coverage.
- `deploy/docker/openclaw-agent-entrypoint.sh` with idempotent `/workspace` seed handling.
- Documentation updates across `README.md`, `docs/mailbridge-runbook.md`, `docs/architecture-diagrams.md`, `AGENTS.md`.
- `docker-compose.yml` hardcoded token default removed; `.env.example` aligned.

Remediation changes since the first pass:
- **R1**: `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (was 742 lines) split into 5 shards (312/177/171/99/92 lines) + shared fixture module (122 lines). All 17 original `It` blocks preserved; 1 `It` block added for the `-DashboardAuthPath` override path = 18 total.
- **R2**: `-OnboardBinaryPath [string]` parameter (default `'dist/index.js'`) added to `scripts/Invoke-OpenClawAgentOnboarding.ps1:61`. Documented in `README.md:281`, `docs/mailbridge-runbook.md:281,524`, and `pr-notes.md`. Two new Pester `It` blocks added (default path, override path).
- **R3**: `-DashboardAuthPath [string]` parameter (default `'/auth/verify'`) added to `scripts/Invoke-OpenClawContainerPathValidation.ps1:33`. Threaded to `Invoke-OpenClawDashboardAuthProbe -AuthPath $DashboardAuthPath` at line 225. Code comment added at `OpenClawContainerValidation.psm1:~351`. Documented in `docs/mailbridge-runbook.md`. One new Pester `It` block added.

---

## 10. Compliance Verdict

| Domain | Verdict | Notes |
|---|---|---|
| General Code Change Policy | ✅ PASS | All §4 file-size violations (R1) resolved in remediation. |
| General Unit Test Policy | ✅ PASS | 100/100 tests pass; coverage thresholds met. |
| PowerShell Code Change Policy | ✅ PASS | 0 diagnostics in PR-scope files; formatting clean. |
| PowerShell Unit Test Policy | ✅ PASS | Pester v5, correct naming, fixture module, mock-only I/O. |
| Toolchain (format → analyze → test) | ✅ PASS | Format and test exit 0. Analyze exit 1 only due to pre-existing non-PR-scope files (§G1). |

**Overall Compliance Verdict: PASS**

The Blocker (R1) and both High findings (R2, R3) from the first-pass audit are fully resolved. The 5 PSScriptAnalyzer diagnostics are in pre-existing files outside the PR diff and do not affect this branch's compliance posture. The branch satisfies all policy requirements for merge to `development`.

---

## Appendix A: Test Inventory

| Test File | It-Blocks | Scenarios Covered |
|---|---|---|
| `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` | 9 | Missing Docker CLI; upstream non-zero exit; malformed output; token present no-op; token present with `-Force`; explicit API key param; prompt fallback; default binary path; override `-OnboardBinaryPath` |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` | 5 | Core health probe; overall result aggregation; container not found; JSON parse; `CoreBaseUrl` override |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` | 5 | Token absent; token empty; probe 401 rejection; probe 200 acceptance; override `-DashboardAuthPath` |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` | 3 | HostAdapter exec success; exec failure; malformed exec output |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` | 3 | `/readyz` 200 expected; `/readyz` non-200; timeout |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` | 2 | Token present and non-empty; token absent from `.env` |
| `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` | 0 (shared fixture module) | Provides `Invoke-FakeDocker` and shared mock setup helpers |

Container validation subtotal: **18 It-blocks** across 5 test shards.
Onboarding subtotal: **9 It-blocks** in 1 test file.

---

## Appendix B: Toolchain Commands Reference

| Step | Tool / Command | Exit Code |
|---|---|---|
| Format | `mcp_drmcopilotext_run_poshqc_format` | 0 |
| Analyze | `mcp_drmcopilotext_run_poshqc_analyze` | 1 (pre-existing non-PR files only) |
| Test | `mcp_drmcopilotext_run_poshqc_test` | 0 |
| Direct analyze detail | `Invoke-PoshQCAnalyze -Root (Resolve-Path ".").Path` | Confirms 5 findings in `.claude/hooks/` and `.tmp-tools/` |
| Direct test detail | `Invoke-PoshQCTest -Root (Resolve-Path ".").Path` | 100/100, 3.93 s |
| Line count verification (R1) | `(Get-Content <file>).Count` per split shard | All ≤ 500 |
| It-block count (R1) | `Select-String -Pattern "^\s+It\s" | Measure-Object` | 18 across 5 shards |
| Parameter presence (R2) | `read_file scripts/Invoke-OpenClawAgentOnboarding.ps1:47-70` | Line 61: `[string]$OnboardBinaryPath = 'dist/index.js'` |
| Parameter presence (R3) | `read_file scripts/Invoke-OpenClawContainerPathValidation.ps1:1-45` | Line 33: `[string]$DashboardAuthPath = '/auth/verify'` |
