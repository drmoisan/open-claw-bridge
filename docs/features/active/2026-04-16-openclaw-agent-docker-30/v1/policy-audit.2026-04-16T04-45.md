# Policy Compliance Audit: openclaw-agent-docker (Issue #30)

---

**Audit Date:** 2026-04-16  
**Code Under Test:** `docker-compose.yml`, `docker-compose.dev.yml`, `.env.example`, `deploy/docker/openclaw-assistant/TOOLS.md`, `deploy/docker/openclaw-assistant/SYSTEM.md`, `deploy/docker/openclaw-assistant/config.yaml`, `README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| YAML (Docker Compose) | 2 files | N/A | ✅ `docker compose config` validated | N/A | N/A | N/A |
| Markdown (docs) | 6 files | N/A | N/A | N/A | N/A | N/A |
| YAML (config) | 1 file | N/A | N/A | N/A | N/A | N/A |
| Env config | 1 file | N/A | N/A | N/A | N/A | N/A |

---

## Executive Summary

This feature adds an external OpenClaw assistant runtime (`openclaw-agent`) as a new Docker Compose service alongside the existing `openclaw-core` service. No C#, Python, PowerShell, or other programmatic code was changed. The change scope is limited to Docker Compose definitions, environment configuration, assistant instruction/tool files, and documentation updates.

**Policy documents evaluated:**
- ✅ `general-code-change.instructions.md` — applicable to file structure, design principles, naming, and documentation
- N/A `general-unit-test.instructions.md` — no testable code was changed or added

**Language-specific policies evaluated:**
- N/A `python-code-change.instructions.md` + `python-unit-test.instructions.md` — no Python files changed
- N/A `powershell-code-change.instructions.md` + `powershell-unit-test.instructions.md` — no PowerShell files changed
- N/A `csharp-code-change.instructions.md` + `csharp-unit-test.instructions.md` — no C# files changed
- N/A Bash — no Bash scripts changed
- N/A JSON — no JSON files changed

**Toolchain summary:** The standard language-specific toolchain (format, lint, type-check, test) is not applicable to this change because no programmatic code was modified. Docker Compose validation (`docker compose config`) was run against both compose files and passed (evidence: `evidence/qa-gates/qa-compose-config.md`, `evidence/qa-gates/qa-compose-dev-config.md`). The existing `openclaw-core` service was confirmed semantically unchanged (evidence: `evidence/qa-gates/qa-core-regression.md`).

**Temporary artifacts cleanup:**
- ✅ No temporary or one-time scripts were created during development
- N/A No ongoing tooling scripts added

---

## 1. General Unit Test Policy Compliance

**Section status: N/A**

No testable programmatic code (C#, Python, PowerShell, Bash) was added or modified. This change is limited to Docker Compose YAML, environment configuration, Markdown documentation, and YAML configuration templates. The general unit test policy does not apply to this change scope.

The existing C# test suite was not re-run because `openclaw-core` is semantically unchanged, as verified by the QA regression evidence at `evidence/qa-gates/qa-core-regression.md`.

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | ✅ PASS | Objective documented in `issue.md` (Issue #30): add external OpenClaw assistant runtime as a Docker Compose service alongside `openclaw-core`. |
| **Read existing change plans** | ✅ PASS | `plan.2026-04-16T00-10.md` exists in the active feature folder and was used to guide implementation. |
| **Document the plan** | ✅ PASS | Spec (`spec.md`), user story (`user-story.md`), and plan file present in the feature folder. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | The new service mirrors the existing `openclaw-core` service structure in Docker Compose. Configuration uses the same patterns (bind-mount token file, loopback-only ports, env-var substitution). No complex indirection introduced. |
| **Reusability** | ✅ PASS | Reuses existing patterns: same token-file mount convention, same `host.docker.internal` networking, same security posture settings. Environment variables follow the same naming conventions as the existing stack. |
| **Extensibility** | ✅ PASS | The new service is additive and independent. It can be disabled or removed without affecting `openclaw-core`. Environment variables are parameterized with safe defaults. |
| **Separation of concerns** | ✅ PASS | The assistant service is isolated as a separate Docker Compose service. It does not share volumes, state, or configuration with `openclaw-core` beyond the read-only token file. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | ✅ PASS | New files are organized under `deploy/docker/openclaw-assistant/` with clear purposes: `TOOLS.md` (tool definitions), `SYSTEM.md` (behavioral constraints), `config.yaml` (placeholder configuration). |
| **Under 500 lines** | ✅ PASS | `docker-compose.yml`: ~82 lines. `docker-compose.dev.yml`: ~48 lines. `TOOLS.md`: ~100 lines. `SYSTEM.md`: ~54 lines. `config.yaml`: ~20 lines. All documentation files remain under 500 lines of added content. |
| **Public vs internal** | ✅ PASS | Public surface is intentional: compose files define the service contract; `.env.example` documents required configuration; documentation describes usage and operational procedures. |
| **No circular dependencies** | ✅ PASS | The `openclaw-agent` service has no dependency on `openclaw-core`. Both independently consume the HostAdapter API. No `depends_on` relationship exists between them. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | ✅ PASS | Service named `openclaw-agent` is distinct from `openclaw-core`. Environment variables follow the `OPENCLAW_AGENT_*` prefix convention. Documentation explicitly distinguishes between the repo-owned `OpenClaw.Core` and the external assistant runtime. |
| **Docs/docstrings** | ✅ PASS | `README.md` updated with usage section. `docs/architecture-diagrams.md` updated with topology diagram. `docs/mailbridge-runbook.md` updated with operational guidance including prerequisites, start/stop, connectivity verification, and troubleshooting. |
| **Comment why, not what** | ✅ PASS | `.env.example` includes section comment `# OpenClaw Agent (external assistant runtime)`. `config.yaml` includes a warning comment explaining that values are placeholders requiring verification against official documentation. |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | N/A | No programmatic code changed. Docker Compose YAML formatting is validated by `docker compose config`. |
| **2. Linting** | N/A | No programmatic code changed. Compose YAML validity confirmed via `docker compose config` (EXIT_CODE: 0). |
| **3. Type checking** | N/A | No programmatic code changed. |
| **4. Testing** | N/A | No programmatic code changed. `openclaw-core` regression verified via diff comparison (evidence: `qa-core-regression.md`). |
| **Full toolchain loop** | ✅ PASS | Applicable validation (`docker compose config` for both files) completed in a single pass with no errors. Evidence: `qa-compose-config.md`, `qa-compose-dev-config.md`. |
| **Explicit reporting** | ✅ PASS | Commands and results documented in QA gate evidence files. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | ✅ PASS | Changes are summarized in the issue, spec, and user-story documents. This audit provides additional summary. |
| **Design choices explained** | ✅ PASS | The spec documents the architecture, service topology, and security posture decisions. The choice of HTTP-based tools over CLI exec is documented in the spec and TOOLS.md. |
| **Update supporting documents** | ✅ PASS | `README.md`, `docs/architecture-diagrams.md`, and `docs/mailbridge-runbook.md` all updated. |
| **Provide next steps** | ✅ PASS | Constraints section in issue.md and spec.md documents known risks and next actions (verify agent image against official docs). |

---

## 3. Language-Specific Code Change Policy Compliance

**Section status: N/A**

No C#, Python, PowerShell, Bash, or JSON programmatic files were changed. All changed files are Docker Compose YAML, environment configuration, Markdown documentation, or YAML configuration templates. Language-specific code change policies do not apply.

---

## 4. Language-Specific Unit Test Policy Compliance

**Section status: N/A**

No unit tests were added, modified, or required. No testable programmatic code was changed.

---

## 5. Test Coverage Detail

**Section status: N/A**

No testable programmatic code was changed. Coverage metrics are not applicable to Docker Compose YAML, Markdown, or configuration files.

The existing C# test suite coverage is unaffected because no C# code was modified.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | N/A | N/A |
| Tests Passing | N/A | N/A |
| Tests Failing | N/A | N/A |
| Execution Time | N/A | N/A |
| Docker Compose Validation (production) | EXIT_CODE: 0 | ✅ |
| Docker Compose Validation (dev combined) | EXIT_CODE: 0 | ✅ |
| openclaw-core Regression Check | IDENTICAL | ✅ |

---

## 7. Code Quality Checks

### Docker Compose Validation

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Production config | `docker compose --env-file .env.example -f docker-compose.yml config` | EXIT_CODE: 0, clean output with two services | ✅ |
| Dev combined config | `docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config` | EXIT_CODE: 0, clean output with three services | ✅ |
| openclaw-core regression | Diff comparison of parsed compose config against baseline snapshot | IDENTICAL — zero semantic changes | ✅ |

### Security Posture Validation (openclaw-agent)

| Property | Expected | Actual | Status |
|----------|----------|--------|--------|
| `read_only` | `true` | `true` | ✅ |
| `cap_drop` | `[ALL]` | `[ALL]` | ✅ |
| `security_opt` | `[no-new-privileges:true]` | `[no-new-privileges:true]` | ✅ |
| `user` | non-root | `1654:1654` | ✅ |
| `ports` host_ip | `127.0.0.1` (loopback) | `127.0.0.1` | ✅ |
| token mount `read_only` | `true` | `true` | ✅ |

Evidence: `evidence/qa-gates/qa-compose-config.md`

---

## 8. Gaps and Exceptions

### Identified Gaps

- **No healthcheck defined for `openclaw-agent`:** The existing `openclaw-core` service has a healthcheck (`/app/healthcheck.sh`), but the new `openclaw-agent` service does not define one. The spec notes this is contingent on the external image's healthcheck support. This is acceptable as the exact healthcheck mechanism depends on the unverified external image.
- **Spec path discrepancy:** The spec references `deploy/agent-workspace/` as the default workspace path and `deploy/agent-workspace/INSTRUCTIONS.md` as the instruction file name. The implementation uses `deploy/docker/openclaw-assistant/` and `SYSTEM.md` respectively. These are minor naming divergences between spec and implementation.

### Approved Exceptions

**None.** No policy exceptions were required.

### Removed/Skipped Tests

**None.** No unit tests were applicable to this change scope.

---

## 9. Summary of Changes

### Files Modified

1. **`docker-compose.yml`** (MODIFIED)
   - Added `openclaw-agent` service definition with security posture matching `openclaw-core`
   - Existing `openclaw-core` service block unchanged

2. **`docker-compose.dev.yml`** (MODIFIED)
   - Added dev-mode `openclaw-agent` override with `extra_hosts`, ports, and volumes

3. **`.env.example`** (MODIFIED)
   - Appended `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, `OPENCLAW_AGENT_WORKSPACE`

4. **`deploy/docker/openclaw-assistant/TOOLS.md`** (NEW)
   - HTTP-based tool definitions for all six HostAdapter endpoints

5. **`deploy/docker/openclaw-assistant/SYSTEM.md`** (NEW)
   - Five behavioral constraints: read-only operation, no-write claims, redaction awareness, human-approval gating, safe-mode-first

6. **`deploy/docker/openclaw-assistant/config.yaml`** (NEW)
   - Placeholder configuration template with verification warning

7. **`README.md`** (MODIFIED)
   - Added "4. Optional: Start the OpenClaw Assistant Service" section

8. **`docs/architecture-diagrams.md`** (MODIFIED)
   - Updated Section 0 Mermaid diagram with `openclaw-agent` node and connections

9. **`docs/mailbridge-runbook.md`** (MODIFIED)
   - Added "Optional OpenClaw Assistant Service" section with prerequisites, start/stop, connectivity verification, and troubleshooting table

---

## 10. Compliance Verdict

### Overall Status: ✅ FULLY COMPLIANT

All applicable general code change policy requirements are met. Language-specific policies and unit test policies are not applicable because no programmatic code was changed. Docker Compose validation passed for both production and dev combined configurations. The existing `openclaw-core` service is confirmed unchanged.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Before Making Changes: Objective, plan, and spec documented
- ✅ Design Principles: Simple, reusable, extensible, separated concerns
- ✅ Module & File Structure: Cohesive, under 500 lines, no circular deps
- ✅ Naming, Docs, Comments: Descriptive names, updated documentation, rationale comments
- ✅ Toolchain Execution: Docker Compose validation passed (applicable subset)
- ✅ Summarize & Document: Changes summarized, docs updated, next steps provided

#### Language-Specific Code Change Policy (Section 3)
- N/A — no programmatic code changed

#### General Unit Test Policy (Section 1)
- N/A — no testable code changed

#### Language-Specific Unit Test Policy (Section 4)
- N/A — no tests added or modified

---

### Metrics Summary

- ✅ Docker Compose config validation: 2/2 files passed
- ✅ Security posture verification: 6/6 properties matched
- ✅ Regression check: `openclaw-core` service unchanged
- ✅ File structure: all new files under 500 lines
- ✅ Documentation: 3/3 required docs updated

---

### Recommendation

**Ready for merge** — pending verification of `OPENCLAW_AGENT_IMAGE` against official OpenClaw platform documentation, which is an acknowledged prerequisite documented in the spec and issue.

---

## Appendix A: Test Inventory

N/A — no unit tests were added or modified. This change is limited to Docker Compose, configuration, and documentation files.

---

## Appendix B: Toolchain Commands Reference

**Docker Compose validation:**
```bash
# Validate production compose config
docker compose --env-file .env.example -f docker-compose.yml config

# Validate dev combined compose config
docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config
```

**No programmatic toolchain commands were applicable to this change.**

---

**Audit Completed By:** GitHub Copilot (feature_code_review_agent)
# Policy Compliance Audit: openclaw-agent-docker (Issue #30)

---

**Audit Date:** 2026-04-16  
**Code Under Test:** `docker-compose.yml`, `docker-compose.dev.yml`, `.env.example`, `deploy/docker/openclaw-assistant/TOOLS.md`, `deploy/docker/openclaw-assistant/SYSTEM.md`, `deploy/docker/openclaw-assistant/config.yaml`, `README.md`, `docs/architecture-diagrams.md`, `docs/mailbridge-runbook.md`

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| YAML (Docker Compose) | 2 files | N/A | ✅ `docker compose config` validated | N/A | N/A | N/A |
| Markdown (docs) | 6 files | N/A | N/A | N/A | N/A | N/A |
| YAML (config) | 1 file | N/A | N/A | N/A | N/A | N/A |
| Env config | 1 file | N/A | N/A | N/A | N/A | N/A |

---

## Executive Summary

This feature adds an external OpenClaw assistant runtime (`openclaw-agent`) as a new Docker Compose service alongside the existing `openclaw-core` service. No C#, Python, PowerShell, or other programmatic code was changed. The change scope is limited to Docker Compose definitions, environment configuration, assistant instruction/tool files, and documentation updates.

**Policy documents evaluated:**
- ✅ `general-code-change.instructions.md` — applicable to file structure, design principles, naming, and documentation
- N/A `general-unit-test.instructions.md` — no testable code was changed or added

**Language-specific policies evaluated:**
- N/A `python-code-change.instructions.md` + `python-unit-test.instructions.md` — no Python files changed
- N/A `powershell-code-change.instructions.md` + `powershell-unit-test.instructions.md` — no PowerShell files changed
- N/A `csharp-code-change.instructions.md` + `csharp-unit-test.instructions.md` — no C# files changed
- N/A Bash: shfmt + shellcheck + bats — no Bash scripts changed
- N/A JSON: format_json + validate_json — no JSON files changed

**Toolchain summary:** The standard language-specific toolchain (format, lint, type-check, test) is not applicable to this change because no programmatic code was modified. Docker Compose validation (`docker compose config`) was run against both compose files and passed. The existing C# test suite was not re-run because `openclaw-core` is semantically unchanged (verified via QA gate regression evidence).

**Temporary artifacts cleanup:**
- ✅ No temporary or one-time scripts were created during development
- N/A No ongoing tooling scripts added

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** - Tests run in any order | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how tests demonstrate independence. Do they share state? Can they run in parallel? Do they use proper setup/teardown?] |
| **Isolation** - Each test targets single behavior | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how each test targets a single unit. List test organization structure. Explain how tests are grouped.] |
| **Fast Execution** - Tests complete quickly | [✅/❌/N/A] [PASS/FAIL/N/A] | [Report total execution time, average per test, discovery time. Identify any slow tests.] |
| **Determinism** - Consistent results | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how tests avoid randomness, time dependencies, and external I/O. List mocking strategy.] |
| **Readability & Maintainability** - Clear structure | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe test naming conventions, organization patterns, and documentation approach.] |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Baseline (pre-development):** [N]% lines, [N]% functions<br>**Command:** `[coverage command]`<br>**Timestamp:** [YYYY-MM-DD HH:MM]<br>**Note:** Document baseline BEFORE making changes to avoid re-computation during audit. |
| **No Coverage Regression** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Post-change coverage:** [N]% lines, [N]% functions<br>**Change:** [+/-N]% lines, [+/-N]% functions<br>**Status:** [No regression / Regression detected: justification]<br>**Example:** "Baseline: 85.2% → Post-change: 87.1% (+1.9%) ✅ PASS" |
| **New Code Coverage ≥90%** | [✅/❌/N/A] [PASS/FAIL/N/A] | **New/modified files:** [list files]<br>**New code coverage:** [N]% (must be ≥90%)<br>**Calculation method:** [describe how new code was isolated for measurement]<br>**Example:** "New module `foo.py`: 127/135 lines covered = 94.1% ✅ PASS" |
| **Comprehensive Coverage** | [✅/❌/N/A] [PASS/FAIL/N/A] | **All functions/classes tested:**<br>- `function_name()` (lines X-Y): [N] tests<br>- `ClassName` (lines A-B): [N] tests<br>**Untested code:** [list with justification]<br>**Example:**<br>- `parse_config()` (lines 45-67): 3 tests<br>- `ConfigLoader` (lines 100-150): 5 tests<br>- Untested: `__repr__()` (trivial representation, no business logic) |
| **Positive Flows** - Valid inputs | [✅/❌/N/A] [PASS/FAIL/N/A] | **All positive scenarios tested:**<br>- `test_function_with_valid_string`: Tests normal string input → returns expected output<br>- `test_function_with_valid_number`: Tests numeric input in valid range → performs calculation<br>- `test_function_with_default_params`: Tests function with default parameters → uses correct defaults<br>**Total positive tests:** [N] |
| **Negative Flows** - Invalid inputs | [✅/❌/N/A] [PASS/FAIL/N/A] | **All negative scenarios tested:**<br>- `test_function_rejects_none`: Input `None` → raises `ValueError` with message "Input cannot be None"<br>- `test_function_rejects_empty_string`: Input `""` → raises `ValueError` with message "Input cannot be empty"<br>- `test_function_rejects_negative_number`: Input `-5` → raises `ValueError` with message "Value must be positive"<br>- `test_function_rejects_wrong_type`: Input `123` (expected str) → raises `TypeError` with message "Expected string, got int"<br>**Total negative tests:** [N] |
| **Edge Cases** - Boundary conditions | [✅/❌/N/A] [PASS/FAIL/N/A] | **All edge cases tested:**<br>- `test_function_with_empty_list`: Input `[]` → returns empty result<br>- `test_function_with_max_length_string`: Input 255-char string (boundary) → processes correctly<br>- `test_function_with_unicode_chars`: Input `"café ☕"` → handles Unicode correctly<br>- `test_function_with_whitespace`: Input `"  spaces  "` → strips/preserves as designed<br>**Total edge case tests:** [N] |
| **Error Handling** - Error paths | [✅/❌/N/A] [PASS/FAIL/N/A] | **All error scenarios tested:**<br>- `test_function_handles_network_timeout`: Mock network timeout → raises `TimeoutError` after retry<br>- `test_function_handles_file_not_found`: Missing file → raises `FileNotFoundError` with filepath<br>- `test_function_handles_json_parse_error`: Invalid JSON → raises `JSONDecodeError` with line number<br>**Total error handling tests:** [N] |
| **Concurrency** - If applicable | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe concurrency testing or explain why it's not applicable. If applicable, describe how race conditions and thread safety are tested.] |
| **State Transitions** - If applicable | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe state transition testing or explain why it's not applicable. If applicable, show that all state transitions are tested.] |

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe assertion strategy and failure message quality. Show examples of diagnostic output from failing tests.] |
| **Arrange-Act-Assert Pattern** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how tests follow AAA pattern. Explain setup, execution, and assertion phases.] |
| **Document Intent** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Show test naming conventions and documentation approach. Provide examples of test names and describe how tests are grouped.] |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | [✅/❌/N/A] [PASS/FAIL/N/A] | [List any external dependencies (databases, networks, APIs, processes, filesystem). If none, explicitly state this.] |
| **Use Mocks/Stubs** | [✅/❌/N/A] [PASS/FAIL/N/A] | [List all mocked/stubbed components and why they were mocked. Explain mocking strategy.] |
| **Environment Stability** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how tests avoid environment dependencies. Note any global state, config files, or temporary files. Confirm no prohibited temporary file creation.] |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm that this audit document serves as the required policy review. Note any outstanding review items.] |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe what objective was being pursued. Reference issue numbers or feature specs if applicable.] |
| **Read existing change plans** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Note whether existing change plans were reviewed. List any relevant planning documents.] |
| **Document the plan** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Reference where the plan was documented (commit messages, planning docs, etc.).] |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how the code demonstrates simplicity. Note any complexity and its justification.] |
| **Reusability** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe what was reused and how. Note any shared patterns or helpers used.] |
| **Extensibility** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how the design supports future extension. Note any extension points.] |
| **Separation of concerns** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how different concerns are separated. Note the layering strategy.] |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe the purpose and cohesion of each module/file. Note organization strategy.] |
| **Under 500 lines** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Report line counts for all files. Note any exceptions and justifications.] |
| **Public vs internal** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe the public API surface. Note how internals are hidden.] |
| **No circular dependencies** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe the dependency graph. Confirm no circular dependencies exist.] |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe naming conventions used. Provide examples of well-named elements.] |
| **Docs/docstrings** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe documentation approach. Note coverage of public APIs.] |
| **Comment why, not what** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe commenting approach. Provide examples of good comments explaining rationale.] |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `[formatting command]`<br>**Result:** [Describe result - no changes needed, or changes applied] |
| **2. Linting** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `[linting command]`<br>**Result:** [Describe result - no findings, or findings fixed] |
| **3. Type checking** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `[type checking command]`<br>**Result:** [Describe result - no errors, or errors fixed, or N/A for PowerShell] |
| **4. Testing** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `[test command]`<br>**Result:** [Describe result - all tests passing] |
| **Full toolchain loop** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm all steps completed in single pass, or describe how many iterations were needed.] |
| **Explicit reporting** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm commands and results are documented in this audit and/or commit messages.] |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Summarize what was changed. Reference PR description and commit messages.] |
| **Design choices explained** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Explain key design decisions. Note any alternatives considered.] |
| **Update supporting documents** | [✅/❌/N/A] [PASS/FAIL/N/A] | [List any documentation updated (README, specs, runbooks, etc.).] |
| **Provide next steps** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe what should happen next. Note completion status and readiness for merge/deployment.] |

---

## 3. Language-Specific Code Change Policy Compliance

**Instructions:** Complete one section per language involved in this change. Delete sections for languages not used.

---

### Section 3A: Python Code Change Policy Compliance (if applicable)

#### 3A.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with Black** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run black .`<br>**Result:** [Describe result] |
| **Linting with Ruff** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run ruff check`<br>**Result:** [Describe result] |
| **Type checking with Pyright** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run pyright`<br>**Result:** [Describe result] |
| **Testing with Pytest** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run pytest`<br>**Result:** [Describe result] |

#### 3A.2 Python Design & Typing

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Strong typing** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe type annotation coverage. Note any use of `Any` and justification.] |
| **Dataclasses for value objects** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe use of dataclasses. Note any value objects.] |
| **Protocols/ABCs for interfaces** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe use of protocols or abstract base classes. Note any interfaces.] |
| **Avoid utility classes** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm no static-method-only utility classes. Note use of modules with functions instead.] |

#### 3A.3 Python Error Handling

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Specific exceptions** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe exception handling approach. Confirm no broad catches.] |
| **Logging over print** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm use of logging module. Note any print statements and justification.] |
| **Invariants at construction** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how invariants are enforced in `__init__` or `__post_init__`.] |

---

### Section 3B: PowerShell Code Change Policy Compliance (if applicable)

#### 3B.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with Invoke-Formatter** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `Invoke-PoshQCFormat -Root .`<br>**Result:** [Describe result] |
| **Linting with PSScriptAnalyzer** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `Invoke-PoshQCAnalyze -Root .`<br>**Result:** [Describe result] |
| **Fix all findings** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe any findings and how they were resolved.] |
| **PowerShell 5.1 & 7.6+ compatible** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe compatibility testing. Note any version-specific features.] |

#### 3B.2 PowerShell Design & Safety

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Advanced functions** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe use of CmdletBinding and parameter attributes. N/A for test files.] |
| **Parameter validation** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe parameter validation attributes used. N/A for test files.] |
| **Avoid global state** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how global state is avoided. Note any legitimate global usage.] |
| **Error handling** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe error handling approach. Note use of try/catch, -ErrorAction, etc.] |

#### 3B.3 Structure, Naming, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive and under 500 lines** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Report file line counts. Confirm all under limit.] |
| **Approved verbs** | [✅/❌/N/A] [PASS/FAIL/N/A] | [List all function names and confirm verbs are approved.] |
| **Comment why** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe commenting approach. Confirm focus on rationale.] |

#### 3B.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Step 1: Format** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe execution and result.] |
| **Step 2: Analyze** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe execution and result.] |
| **Step 3: Type check** | N/A | Not applicable for PowerShell. |
| **Step 4: Test** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe execution and result.] |
| **Rerun loop if needed** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how many iterations were needed.] |

---

### Section 3C: Bash Script Policy Compliance (if applicable)

#### 3C.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with shfmt** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run shell-qc format`<br>**Result:** [Describe result] |
| **Linting with shellcheck** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run shell-qc check`<br>**Result:** [Describe result] |
| **Testing with bats** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run shell-qc test`<br>**Result:** [Describe result or N/A if no tests] |

#### 3C.2 Bash Script Design

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Portable shebang** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm `#!/usr/bin/env bash` or `#!/bin/bash` used.] |
| **Error handling** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe use of `set -e`, `set -u`, `set -o pipefail`, error traps.] |
| **Under 500 lines** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Report file line counts.] |

---

### Section 3D: JSON Configuration Policy Compliance (if applicable)

#### 3D.1 JSON Tooling

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with jq** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run python -m scripts.dev_tools.format_json`<br>**Result:** [Describe result - files formatted or already formatted] |
| **Schema validation** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run python -m scripts.dev_tools.validate_json`<br>**Result:** [Describe result - all schemas valid] |
| **Required $schema** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm all governed JSON files have `$schema` property.] |

#### 3D.2 JSON Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Strict JSON only** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm no comments, trailing commas, or other JSON5 features.] |
| **Deterministic key order** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm keys are sorted via `jq --sort-keys`.] |

---

## 4. Language-Specific Unit Test Policy Compliance

**Instructions:** Complete one section per language with tests. Delete sections for languages not used or not tested.

---

### Section 4A: Python Unit Test Policy Compliance (if applicable)

#### 4A.1 Framework and Scope

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use Pytest** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe use of Pytest. Note any fixtures or plugins used.] |
| **Coverage expectation** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Report coverage metrics. Confirm new code has >= 90% coverage and repo-wide >= 80%.] |

#### 4A.2 Test Style and Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Focused unit tests** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe test focus. Confirm each test exercises single behavior.] |
| **Mocking sparingly** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe mocking strategy. List what is mocked and why.] |
| **Organization** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe test organization. Confirm tests mirror code structure.] |

#### 4A.3 Naming and Readability

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Naming conventions** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe test naming. Provide examples of descriptive test names.] |
| **Docstrings/comments** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe documentation approach for tests.] |

#### 4A.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use Pytest** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `poetry run pytest`<br>**Result:** [Describe test results] |
| **No Alternative Test Runners** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm only Pytest is used.] |

---

### Section 4B: PowerShell Unit Test Policy Compliance (if applicable)

#### 4B.1 Framework and Scope

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use Pester v5.x** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe Pester v5 features used (BeforeAll, BeforeEach, Describe/Context/It, modern Should syntax).] |
| **Use PoshQC Configuration** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `Invoke-PoshQCTest -Root .`<br>**Config:** `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`<br>[Describe configuration updates if any.] |
| **PowerShell 5.1 & 7.6+ Compatible** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe compatibility testing. Note any version-specific features.] |

#### 4B.2 Test Style and Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Focused Unit Tests** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe test focus. Report test distribution across functions.] |
| **Test Behavior Over Implementation** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe what behaviors are tested vs. implementation details.] |
| **Mocking Used Sparingly** | [✅/❌/N/A] [PASS/FAIL/N/A] | [List all mocked components and justification for each.] |
| **Organization** | [✅/❌/N/A] [PASS/FAIL/N/A] | **CRITICAL:** Test file location must mirror code file location.<br>**Test file:** `[path]`<br>**Code file:** `[path]`<br>[Confirm structure mirrors code location.] |

#### 4B.3 Naming and Readability

| Requirement | Status | Evidence |
|------------|--------|----------|
| **File Naming** - *.Tests.ps1 | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm file named correctly: `[name].Tests.ps1`] |
| **Describe/Context/It Structure** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe test structure: # Describe blocks, # Context blocks, # It blocks.] |
| **Logical Grouping** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe how tests are grouped. Show grouping strategy.] |
| **Docstrings/Comments** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Describe documentation approach. Note that test names should be self-documenting.] |

#### 4B.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use PoshQCTest Command** | [✅/❌/N/A] [PASS/FAIL/N/A] | **Command:** `Invoke-PoshQCTest -Root .`<br>**Result:** [Describe test results] |
| **No Alternative Test Runners** | [✅/❌/N/A] [PASS/FAIL/N/A] | [Confirm only Pester is used through PoshQC.] |

---

## 5. Test Coverage Detail

**Instructions:** Create one subsection for each function/class/module under test. Use tables to show test names, scenario types, lines covered, and status.

### [Function/Class/Module Name] ([N] tests)

| Test Name | Scenario Type | Lines Covered | Status |
|-----------|--------------|---------------|--------|
| [test name] | [Positive/Negative/Edge Case/Error Handling] | [line ranges] | [✅/❌] |
| [test name] | [Positive/Negative/Edge Case/Error Handling] | [line ranges] | [✅/❌] |
| [test name] | [Positive/Negative/Edge Case/Error Handling] | [line ranges] | [✅/❌] |

**Coverage:** [X]% of [function/class] (lines [start]-[end])

**Detailed line-by-line coverage (optional):**
- Line [N]: [✅/❌] Covered ([describe what's tested])
- Lines [N-M]: [✅/❌] Covered ([describe what's tested])
- Line [N]: ❌ Not covered ([explain why or plan to cover])

**Not covered:** [List any untested code and justification, or state "None"]

---

### [Additional Function/Class/Module] ([N] tests)

[Repeat structure above for each component tested]

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | [N] | [✅/❌] |
| Tests Passed | [N] ([X]%) | [✅/❌] |
| Tests Failed | [N] | [✅/❌] |
| Execution Time | [X]s total | [✅/❌] Fast/Slow |
| Average Time per Test | [X]ms | [✅/❌] Fast/Slow |
| Discovery Time | [X]ms | [✅/❌] |
| Functions/Classes Tested | [N]/[M] ([X]%) | [✅/❌] |
| Test File Size | [N] lines | [✅/❌] Maintainable |
| Code Coverage (if applicable) | [X]% lines, [Y]% branches | [✅/❌] |

---

## 7. Code Quality Checks

**For Python:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Black Formatting | `poetry run black .` | [result] | [✅/❌] |
| Ruff Linting | `poetry run ruff check` | [result] | [✅/❌] |
| Pyright Type Checking | `poetry run pyright` | [result] | [✅/❌] |
| Pytest Tests | `poetry run pytest` | [result] | [✅/❌] |

**For PowerShell:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Invoke-Formatter | `Invoke-PoshQCFormat -Root .` | [result] | [✅/❌] |
| PSScriptAnalyzer | `Invoke-PoshQCAnalyze -Root .` | [result] | [✅/❌] |
| Pester Tests | `Invoke-PoshQCTest -Root .` | [result] | [✅/❌] |

**Notes:**
[Document any pre-existing failures unrelated to this work. Explain any deviations from clean runs.]

---

## 8. Gaps and Exceptions

### Identified Gaps
[List any policy requirements that are not fully met. If none, state "**None.** All policy requirements are met."]

**Example:**
- [Requirement name]: [Description of gap and plan to address]

### Approved Exceptions
[List any approved exceptions to policy requirements with justification. If none, state "**None.** No exceptions needed."]

**Example:**
- [Requirement name]: [Justification for exception and approval source]

### Removed/Skipped Tests
[List any tests that were planned but removed or skipped, with justification. If none, state "**None.** All planned tests implemented."]

**Example:**
1. **"[test name]"** - Removed in commit [hash]
   - **Reason:** [Why was it removed?]
   - **Impact:** [What coverage is lost?]
   - **Justification:** [Why is this acceptable?]

---

## 9. Summary of Changes

### Commits in This PR/Branch

[List all commits with hashes and brief descriptions]

**Example:**
1. **[hash]** - [commit message]
2. **[hash]** - [commit message]
3. **[hash]** - [commit message]

### Files Modified

[List all files that were created, modified, or deleted]

**Example:**

1. **[path/to/file]** (NEW/MODIFIED/DELETED)
   - [Description of changes]
   - [Key points]

2. **[path/to/file]** (NEW/MODIFIED/DELETED)
   - [Description of changes]
   - [Key points]

3. **[path/to/file]** (NEW/MODIFIED/DELETED)
   - [Description of changes]
   - [Key points]

---

## 10. Compliance Verdict

### Overall Status: [✅ FULLY COMPLIANT / ⚠️ PARTIALLY COMPLIANT / ❌ NON-COMPLIANT]

[Provide a brief summary of overall compliance status and any major findings.]

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- [✅/⚠️/❌] Before Making Changes: [Brief status]
- [✅/⚠️/❌] Design Principles: [Brief status]
- [✅/⚠️/❌] Module & File Structure: [Brief status]
- [✅/⚠️/❌] Naming, Docs, Comments: [Brief status]
- [✅/⚠️/❌] Toolchain Execution: [Brief status]
- [✅/⚠️/❌] Summarize & Document: [Brief status]

#### Language-Specific Code Change Policy (Section 3)

**For Python:**
- [✅/⚠️/❌] Tooling & Baseline: [Brief status]
- [✅/⚠️/❌] Python Design & Typing: [Brief status]
- [✅/⚠️/❌] Error Handling: [Brief status]

**For PowerShell:**
- [✅/⚠️/❌] Tooling & Baseline: [Brief status]
- [✅/⚠️/❌] PowerShell Design & Safety: [Brief status]
- [✅/⚠️/❌] Structure & Naming: [Brief status]
- [✅/⚠️/❌] Toolchain: [Brief status]

#### General Unit Test Policy (Section 1)
- [✅/⚠️/❌] Core Principles: [Brief status]
- [✅/⚠️/❌] Coverage & Scenarios: [Brief status]
- [✅/⚠️/❌] Test Structure: [Brief status]
- [✅/⚠️/❌] External Dependencies: [Brief status]
- [✅/⚠️/❌] Policy Audit: [Brief status]

#### Language-Specific Unit Test Policy (Section 4)

**For Python:**
- [✅/⚠️/❌] Framework & Scope: [Brief status]
- [✅/⚠️/❌] Test Style & Structure: [Brief status]
- [✅/⚠️/❌] Naming & Readability: [Brief status]
- [✅/⚠️/❌] Toolchain: [Brief status]

**For PowerShell:**
- [✅/⚠️/❌] Framework & Scope: [Brief status]
- [✅/⚠️/❌] Test Style & Structure: [Brief status]
- [✅/⚠️/❌] Naming & Readability: [Brief status]
- [✅/⚠️/❌] Toolchain: [Brief status]

---

### Metrics Summary

[Provide a bulleted summary of key metrics from Section 6]

**Example:**
- [✅/❌] [N]/[M] tests passing ([X]%)
- [✅/❌] [N]/[M] functions/classes tested ([X]%)
- [✅/❌] [X]% line coverage
- [✅/❌] Proper file organization: [describe]
- [✅/❌] All code quality checks passing
- [✅/❌] Test execution time: [X] seconds ([fast/slow])

---

### Recommendation

**[Ready for merge / Needs revision / Blocked]**

[Provide clear recommendation and any next steps. If not ready for merge, list specific items that must be addressed.]

---

## Appendix A: Test Inventory

### Complete Test List

[List all tests in a hierarchical structure that matches the test organization (Describe/Context/It or test class hierarchy)]

**Example format:**

1. [Describe/Class] › [Context/Method] › [test name]
2. [Describe/Class] › [Context/Method] › [test name]
3. [Describe/Class] › [Context/Method] › [test name]
...

**Alternative flat format:**

- [test_module.py::TestClassName::test_method_name]
- [test_module.py::TestClassName::test_method_name]
- [test_module.py::test_function_name]
...

---

## Appendix B: Toolchain Commands Reference

[Provide quick reference of all commands used in this audit]

**For Python:**
```bash
# Formatting
poetry run black .

# Linting
poetry run ruff check
poetry run ruff check --fix  # auto-fix

# Type checking
poetry run pyright

# Testing
poetry run pytest
poetry run pytest --cov=src/[package] --cov-report=term-missing
```

**For PowerShell:**
```powershell
# Formatting
Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCFormat -Root .

# Linting
Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCAnalyze -Root .

# Testing
Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root .
```

---

**Audit Completed By:** [Agent Name / Human Name]  
**Audit Date:** [YYYY-MM-DD]  
**Policy Version:** Current (as of audit date)
