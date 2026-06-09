# Policy Compliance Audit: openclaw-agent tools profile (Issue #43 v2)

---

**Audit Date:** 2026-04-22  
**Code Under Test:** `deploy/docker/openclaw-assistant/openclaw.json` (1 production file, 1 line changed)

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| JSON | 1 file | N/A | ✅ validation — vendor JSON5 config | N/A (config file) | N/A (config file) | N/A |

### Coverage Evidence Checklist

- Python baseline coverage artifact: N/A — out of scope
- Python post-change coverage artifact: N/A — out of scope
- TypeScript baseline coverage artifact: N/A — out of scope
- TypeScript post-change coverage artifact: N/A — out of scope
- PowerShell baseline coverage artifact: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/evidence/qa-gates/coverage-delta.2026-04-21T14-00.md` (v1 baseline, unchanged — no PS code changed in v2)
- PowerShell post-change coverage artifact: same v1 artifact (no PS code changed in v2; baseline remains authoritative)
- Per-language comparison summary: PowerShell coverage unchanged from v1 (88.58% repo-wide, 90.80% `OpenClawContainerValidation.psm1`). No new code was added.

**Fail-closed note:** No automated coverage toolchain runs are required for this change. The sole production change is a JSON seed file value (`"profile": "minimal"` → `"profile": "coding"`). No testable code was added or modified. The v1 coverage baseline is the authoritative post-change baseline for this audit.

---

## Executive Summary

This audit covers the v2 fix for Issue #43 on branch `bug/openclaw-agent-capabilities-none-43` relative to base branch `development` (merge-base `2397e6d0`). The single production change is a one-line value update in `deploy/docker/openclaw-assistant/openclaw.json`, replacing `"profile": "minimal"` with `"profile": "coding"` in the `tools` section. No C#, PowerShell, Python, Dockerfile, or `docker-compose.yml` files were modified in v2.

**Policy documents evaluated:**
- ✅ `general-code-change.instructions.md` — applies; evaluated in Section 2
- ✅ `general-unit-test.instructions.md` — applies; all N/A given no automated tests added or changed; exception documented in Section 8
- N/A `python-code-change.instructions.md` / `python-unit-test.instructions.md` — no Python files changed
- N/A `powershell-code-change.instructions.md` / `powershell-unit-test.instructions.md` — no PowerShell files changed
- N/A `csharp-code-change.instructions.md` / `csharp-unit-test.instructions.md` — no C# files changed
- N/A `github-actions.instructions.md` — no workflow files changed

**Language-specific policies evaluated:**
- N/A `python-code-change.instructions.md` + `python-unit-test.instructions.md`
- N/A `powershell-code-change.instructions.md` + `powershell-unit-test.instructions.md`
- N/A Bash: not in scope for this change (entrypoint.sh was not modified)
- ✅ JSON: Section 3D applies; see below

**Toolchain summary:** No formatting, linting, or type-checking tools apply to the changed file. Docker toolchain (`docker compose build`, `docker compose up --force-recreate`) ran clean (both EXIT_CODE 0). Manual agent capability verification (AC-1-v2, AC-2-v2) is pending.

**Key gap:** The production file `deploy/docker/openclaw-assistant/openclaw.json` is unstaged (modified but not committed) as confirmed by the refreshed PR context appendix. The change has been applied and verified at runtime, but it has not been committed to the branch. This must be resolved before a PR can be created.

**Temporary artifacts cleanup:**
- ✅ No temporary or one-time scripts were created during this change
- ✅ All development artifacts are retained as evidence under the v2 feature folder

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** - Tests run in any order | N/A | No automated tests were added or modified in v2. The change is a Docker seed file value; no testable unit exists. |
| **Isolation** - Each test targets single behavior | N/A | No automated tests were added or modified in v2. |
| **Fast Execution** - Tests complete quickly | N/A | No automated tests were added or modified in v2. |
| **Determinism** - Consistent results | N/A | No automated tests were added or modified in v2. |
| **Readability & Maintainability** - Clear structure | N/A | No automated tests were added or modified in v2. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | ✅ PASS | PowerShell baseline from v1: 88.58% repo-wide, 90.80% `OpenClawContainerValidation.psm1`. Artifact: `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`. No code changed in v2; this baseline is the unchanged post-change baseline. |
| **No Coverage Regression** | ✅ PASS | No testable code was added or modified. Coverage metrics are identical to the v1 post-change baseline (88.58% repo-wide). |
| **New Code Coverage ≥90%** | N/A | No new code added. JSON seed file value change has no coverage surface. |
| **Comprehensive Coverage** | N/A | No code functions, classes, or methods were added. |
| **Positive Flows** - Valid inputs | N/A | No automated tests applicable. Manual validation is the approved gate per `spec.md` Test Strategy section. |
| **Negative Flows** - Invalid inputs | N/A | No automated tests applicable. |
| **Edge Cases** - Boundary conditions | N/A | No automated tests applicable. |
| **Error Handling** - Error paths | N/A | No automated tests applicable. Gateway behavior on invalid profile is documented in spec.md (validation error at startup). |
| **Concurrency** - If applicable | N/A | Not applicable. |
| **State Transitions** - If applicable | N/A | Not applicable. |

### 1.2.1 Per-Language Coverage Comparison

- PowerShell: Baseline (v1): 88.58% commands repo-wide, 90.80% `OpenClawContainerValidation.psm1` → Post-change (v2): unchanged (no PS code modified). Change: 0% delta. New/changed-code coverage: N/A — out of scope. Disposition: PASS. Evidence: `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`.
- JSON: N/A — out of scope (no coverage tooling for JSON config files).

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | N/A | No automated tests applicable. |
| **Arrange-Act-Assert Pattern** | N/A | No automated tests applicable. |
| **Document Intent** | N/A | No automated tests applicable. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | N/A | No automated tests applicable. |
| **Use Mocks/Stubs** | N/A | No automated tests applicable. |
| **Environment Stability** | N/A | No automated tests applicable. No temporary files created. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | ✅ PASS | This document constitutes the required policy review. Outstanding items: unstaged production file (Section 8, Gap 1); manual verification pending for AC-1-v2 and AC-2-v2 (Section 8, Gap 2). |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | ✅ PASS | Objective is documented in `spec.md` Section "Context" and "Root Cause Analysis": change `"profile": "minimal"` to `"profile": "coding"` so the agent can execute bash/curl calls against the HostAdapter. Issue #43 v2. |
| **Read existing change plans** | ✅ PASS | `plan.2026-04-22T10-45.md` is the active change plan. `phase0-instructions-read.md` confirms policy documents were read before work began. Baseline artifacts captured before the fix was applied. |
| **Document the plan** | ✅ PASS | `plan.2026-04-22T10-45.md` documents the full plan with phase/task IDs. `spec.md` documents design, scope, and assumptions. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | The change is a single JSON field value. No indirection, no structural change, no new abstractions. The minimal change needed to address the root cause. |
| **Reusability** | N/A | No new reusable logic introduced. |
| **Extensibility** | N/A | The `tools.profile` field accepts any valid openclaw profile value; the change does not restrict future profile changes. |
| **Separation of concerns** | ✅ PASS | The seed file is the sole configuration source; the entrypoint and gateway layers are not modified. Concern boundaries preserved. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | ✅ PASS | `openclaw.json` is the seed configuration for the openclaw gateway. The change stays within this cohesive file. |
| **Under 500 lines** | ✅ PASS | `deploy/docker/openclaw-assistant/openclaw.json` is approximately 35 lines. |
| **Public vs internal** | N/A | JSON config file; no public/internal API surface distinction applies. |
| **No circular dependencies** | N/A | JSON config file; no import dependencies. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | ✅ PASS | Field names (`tools`, `profile`, `"coding"`) are vendor-defined and documented in the openclaw configuration reference. |
| **Docs/docstrings** | ✅ PASS | `spec.md` documents the root cause, the proposed fix, the technical specification, and all constraints. `issue.md` documents the v2 ACs and their evidence mapping. |
| **Comment why, not what** | N/A | JSON config files do not carry inline comments in this file (JSON5 supports comments but none are present). The rationale is documented in `spec.md`. |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | N/A | No repo-standard formatter applies to this vendor JSON5 config file. `jq` would not process JSON5 correctly (JSON5 allows comments and trailing commas). |
| **2. Linting** | N/A | No repo-standard linter applies to this vendor JSON5 config file. |
| **3. Type checking** | N/A | Not applicable for JSON configuration. |
| **4. Testing** | ⚠️ PARTIAL | **Commands:** `docker compose build openclaw-agent` (EXIT_CODE: 0, `docker-build.2026-04-22.md`); `docker compose up -d --force-recreate openclaw-agent` (EXIT_CODE: 0, `docker-recreate.2026-04-22.md`); runtime profile verification (EXIT_CODE: 0, `verify-profile-in-container.2026-04-22.md`); hardening verification (EXIT_CODE: 0, `verify-hardening.2026-04-22.md`); compose unchanged check (EXIT_CODE: 0, `verify-compose-unchanged.2026-04-22.md`); gateway log verification (EXIT_CODE: 0, `verify-gateway-logs.2026-04-22.md`). Manual agent capability verification (AC-1-v2, AC-2-v2): EXIT_CODE pending (`verify-agent-capability.2026-04-22.md`). |
| **Full toolchain loop** | ⚠️ PARTIAL | Formatting, linting, and type-check are N/A. Docker toolchain completed in a single pass. Manual verification is pending. |
| **Explicit reporting** | ✅ PASS | All evidence commands and exit codes are documented in the v2 feature folder evidence artifacts. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | ✅ PASS | Change documented in `spec.md` Section "Proposed Fix" and in `issue.md` v2 ACs. |
| **Design choices explained** | ✅ PASS | `spec.md` documents the chosen fix, alternatives (profile values), rollback procedure, and rationale. |
| **Update supporting documents** | ✅ PASS | `issue.md` updated with v2 AC status; `spec.md` is the complete v2 specification. No runbook update required (no new operator surface added). |
| **Provide next steps** | ⚠️ PARTIAL | `spec.md` Rollout section documents next steps. Outstanding: commit the unstaged production file change; complete manual operator verification (AC-1-v2, AC-2-v2). |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3D: JSON Configuration Policy Compliance

#### 3D.1 JSON Tooling

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with jq** | N/A | `openclaw.json` is a JSON5 document (comments and trailing commas permitted per spec.md). `jq` cannot parse JSON5 reliably. No repo-standard JSON formatter applies to this vendor config file. |
| **Schema validation** | N/A | No repo-managed JSON schema exists for `openclaw.json`. The schema is owned by the openclaw gateway vendor. The valid profile values are documented in the openclaw configuration reference. |
| **Required $schema** | N/A | `openclaw.json` is a vendor JSON5 seed file. No `$schema` property is present or required by the repo policy for vendor config files. |

#### 3D.2 JSON Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Strict JSON only** | N/A | `openclaw.json` is intentionally JSON5 (per spec.md: "JSON5 document (comments and trailing commas allowed)"). This is an approved format for openclaw configuration files. |
| **Deterministic key order** | N/A | Vendor configuration file. Key ordering is as-defined in the openclaw reference. `jq --sort-keys` would not be applied to a JSON5 file. |

---

## 4. Language-Specific Unit Test Policy Compliance

All language-specific test policy sections are N/A for this change. No Python, PowerShell, C#, or Bash test files were added or modified. See Section 1 and Section 8 for the approved exception.

---

## 5. Test Coverage Detail

No code functions, classes, or modules under test. The sole production change is a JSON seed file value. No coverage detail applicable.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Docker build | EXIT_CODE 0, 74.2s | ✅ |
| Docker recreate | EXIT_CODE 0, 0.9s | ✅ |
| Profile in container (grep) | EXIT_CODE 0 — `"profile": "coding"` confirmed | ✅ |
| Container hardening check | EXIT_CODE 0 — ReadonlyRootfs, CapDrop ALL, no-new-privileges confirmed | ✅ |
| compose unchanged check (git diff) | EXIT_CODE 0 — empty diff | ✅ |
| Gateway log check | EXIT_CODE 0 — `[plugins] embedded acpx runtime backend ready`, 0 probe failures | ✅ |
| Dockerfile v1 token check | EXIT_CODE 0 — codex-acp (3), CODEX_HOME (1), NPM_CONFIG_CACHE (1) confirmed | ✅ |
| Manual agent capability (AC-1-v2, AC-2-v2) | EXIT_CODE pending — stub artifact `verify-agent-capability.2026-04-22.md` | ⚠️ PENDING |
| Pester tests | Not re-run (no PS code changed) | N/A |

---

## 7. Code Quality Checks

No Python, PowerShell, or C# toolchain steps apply to this change. All changed lines are in a JSON seed file.

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Docker build | `docker compose build openclaw-agent` | EXIT_CODE 0 | ✅ |
| Docker recreate | `docker compose up -d --force-recreate openclaw-agent` | EXIT_CODE 0 | ✅ |
| Profile runtime verification | `docker compose exec openclaw-agent grep '"profile"' /.openclaw/openclaw.json` | `"profile": "coding"` | ✅ |
| Hardening verification | `docker inspect openclaw-agent` | ReadonlyRootfs: true, CapDrop: ALL, SecurityOpt: no-new-privileges:true | ✅ |
| Compose unchanged | `git diff development HEAD -- docker-compose.yml` | Empty diff | ✅ |

---

## 8. Gaps and Exceptions

### Identified Gaps

1. **Unstaged production file:** `deploy/docker/openclaw-assistant/openclaw.json` is modified but unstaged. The PR context appendix (refreshed 2026-04-22T15:48Z) confirms `M deploy/docker/openclaw-assistant/openclaw.json` in the working tree. The change has been verified at runtime but is not committed to the branch. This must be staged and committed before a PR can be created.

2. **Manual verification pending:** AC-1-v2 (agent calls HostAdapter via GET /v1/calendar) and AC-2-v2 (exec/bash tools available in session) require manual operator verification. The stub artifact `verify-agent-capability.2026-04-22.md` has EXIT_CODE: pending. These ACs cannot be rated PASS until the operator confirms the agent behavior.

### Approved Exceptions

1. **No automated unit tests added:** The sole production change is a Docker JSON seed file value. Automated verification would require a running Docker environment, which falls outside the repository's unit test surface. Manual validation is the explicitly approved gate for this change per `spec.md` Section "Test Strategy." This exception is documented in the spec and is consistent with the approved out-of-scope statement for this work.

### Removed/Skipped Tests

**None.** No tests were planned that were subsequently removed or skipped.

---

## 9. Summary of Changes

### Commits in This PR/Branch

1. **97cde32** — `(feat): put documentation into v1 folder because it did not deliver on the feature`
2. **7051d4e** — `(fix(openclaw-agent)): restore ACP runtime and remove dashboard auth probe`
3. **925869f** — `(fix(openclaw-agent)): restore ACP runtime and remove dashboard auth probe`

Note: The production `openclaw.json` change is currently unstaged (working tree modification, not committed). See Gap 1.

### Files Modified

1. **`deploy/docker/openclaw-assistant/openclaw.json`** (MODIFIED — unstaged)
   - Line 14 (post-change): `"profile": "coding"` (was `"profile": "minimal"`)
   - This is the only production source file changed in v2.

---

## 10. Compliance Verdict

### Overall Status: ⚠️ PARTIALLY COMPLIANT

The change is minimal, targeted, and correctly implements the documented root cause fix. All automated verification steps (Docker build, container recreation, profile confirmation, hardening preservation, ACP runtime continuity) passed with EXIT_CODE 0. No regressions were introduced. The change is technically sound and policy-compliant in all respects where automated toolchain steps apply.

Two gaps prevent a FULLY COMPLIANT rating:

1. The production file is unstaged (not committed). The PR cannot be opened until this is resolved.
2. AC-1-v2 and AC-2-v2 require manual operator verification. These are expected per the spec and are not blockers to committing the change, but they must be resolved before the feature is considered complete.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Before Making Changes: objective clarified, plan documented, baseline captured
- ✅ Design Principles: simplicity, targeted scope, no over-engineering
- ✅ Module & File Structure: single cohesive file, 35 lines
- ✅ Naming, Docs, Comments: vendor field names, spec.md documents rationale
- ⚠️ Toolchain Execution: Docker steps PASS; manual verification pending (AC-1-v2, AC-2-v2)
- ⚠️ Summarize & Document: change documented; production file unstaged

#### Language-Specific Code Change Policy (Section 3)
- N/A Python, PowerShell, C#, Bash
- N/A JSON: vendor JSON5 config; no repo-managed schema or formatter applies

#### General Unit Test Policy (Section 1)
- N/A Core Principles: no automated tests applicable
- ✅ Coverage & Scenarios: no regression (no code changed); approved exception documented
- N/A Test Structure, External Dependencies
- ✅ Policy Audit: this document serves as the required pre-submission review

#### Language-Specific Unit Test Policy (Section 4)
- N/A all languages: no test files changed

---

### Metrics Summary

- N/A automated test pass rate (no automated tests applicable)
- ✅ Docker toolchain (build + recreate): 2/2 steps, EXIT_CODE 0
- ✅ Runtime verification: 5/5 automated checks passed (profile, hardening, compose, gateway logs, Dockerfile v1 tokens)
- ⚠️ Manual verification: 0/2 ACs confirmed (pending operator execution)
- ✅ PowerShell coverage baseline (v1): 88.58% repo-wide, 90.80% changed module — no regression (no code changed in v2)

---

### Recommendation

**Needs revision — commit the unstaged production file change, then Conditional Go pending manual verification.**

Before creating a PR: stage and commit `deploy/docker/openclaw-assistant/openclaw.json`. After the commit, the automated verification evidence is complete. The branch may proceed to PR once the production file is committed. Final feature sign-off requires manual operator confirmation of AC-1-v2 and AC-2-v2 per `verify-agent-capability.2026-04-22.md`.

---

## Appendix A: Test Inventory

No automated tests applicable to this change. Manual verification steps are documented in `spec.md` Test Strategy and `verify-agent-capability.2026-04-22.md`.

---

## Appendix B: Toolchain Commands Reference

| Step | Command | Exit Code | Artifact |
|------|---------|-----------|----------|
| Docker build | `docker compose build openclaw-agent` | 0 | `docker-build.2026-04-22.md` |
| Docker recreate | `docker compose up -d --force-recreate openclaw-agent` | 0 | `docker-recreate.2026-04-22.md` |
| Profile runtime verification | `docker compose exec openclaw-agent grep '"profile"' /.openclaw/openclaw.json` | 0 | `verify-profile-in-container.2026-04-22.md` |
| Hardening check | `docker inspect openclaw-agent` | 0 | `verify-hardening.2026-04-22.md` |
| Compose unchanged | `git diff development HEAD -- docker-compose.yml` | 0 | `verify-compose-unchanged.2026-04-22.md` |
| Gateway logs check | `docker compose logs openclaw-agent` | 0 | `verify-gateway-logs.2026-04-22.md` |
| Dockerfile v1 tokens | `Select-String "codex-acp|CODEX_HOME|NPM_CONFIG_CACHE" deploy/docker/openclaw-agent.Dockerfile` | 0 | `verify-dockerfile-v1.2026-04-22.md` |
| Agent capability (manual) | manual operator query | pending | `verify-agent-capability.2026-04-22.md` |
