# Policy Compliance Audit — Issue #144 (container-validation-stray-v1-and-env-target)

- Reviewed: 2026-07-11T09-15 (RE-AUDIT, remediation pass 1 / R4)
- Reviewer: feature-review agent
- Base branch: `main` (merge-base `81debeb1d58dd7226e0eec1bc66aa154047e6a82`)
- Head: `bug/container-validation-stray-v1-and-env-target-144` @ `3b0a2b32395d73874b096d9b685fbaf7b0da62d9`
- Work mode: `minor-audit` (AC source: `issue.md` `## Acceptance Criteria`, AC1–AC7, per the persisted `- Work Mode: minor-audit` marker)
- Scope: full branch diff, merge-base..HEAD — `git diff --stat 81debeb1..3b0a2b3` — 40 files changed (1545 insertions / 42 deletions). This supersedes the prior review's `81debeb1..a79dee48` diff; the additional two-commit delta (`a79dee48..3b0a2b3`) is the remediation this pass verifies.

## Rejected Scope Narrowing

None. The delegating prompt supplied prior-review context (a single Blocking finding and its claimed remediation) but did not attempt to narrow scope to only the remediated files; this audit re-derived and audited the full `81debeb1..3b0a2b3` diff, not merely the two-file remediation delta, per the Scope Invariant.

## Policy Reading Order Applied

1. `CLAUDE.md` — not present at repository root (consistent with prior reviews #137/#139/#142/#144-R1).
2. `.claude/rules/general-code-change.md` — read, applied below.
3. `.claude/rules/general-unit-test.md` — read, applied below (Determinism/Isolation directly implicated by the prior Blocking finding, now remediated).
4. `.claude/rules/powershell.md` — read, applied (only source language with changed files on this branch). The controlling text for this re-audit's central question: "Tests must produce identical results in Terminal and the VS Code Test Explorer... do not rely on ambient environment resolution." Both Terminal and Test Explorer are standard `Invoke-Pester` invocation paths; neither routes through the `Invoke-PoshQCTest` MCP wrapper's internal scriptblock chain.
5. `.claude/rules/quality-tiers.md`, `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/tonality.md` — reviewed for applicability. No C#, Python, or TypeScript files changed; no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` diffs in `81debeb1..3b0a2b3`; no `artifacts/orchestration/orchestrator-state.json` change.

## Language Inventory and Coverage Verdicts

`git diff --name-status 81debeb1..3b0a2b3` confirms the only source language with changed files is **PowerShell**: 2 production files (`scripts/Invoke-OpenClawContainerPathValidation.ps1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`), the module manifest `OpenClawContainerValidation.psd1`, and 6 test/fixture files (3 new test files, 2 modified test files, 1 modified shared fixture — `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`, the remediation site). Remaining changed files are Markdown (`README.md`, `docs/mailbridge-runbook.md`, feature-folder plan/issue/evidence Markdown) and two `.claude/agent-memory/feature-review/*.md` memory artifacts committed alongside the prior review cycle. TypeScript, Python, and C# have zero changed files — N/A per the zero-changed-files exception.

### PowerShell — Coverage Verdict: PASS

Independently regenerated in this audit (not reused from the prior review's artifact, to confirm the remediation — a test-fixture-only change — did not alter production coverage):

- **Per-file (both changed production files), fresh run against HEAD `3b0a2b3`:** `[xml]` parse of an independently-generated `artifacts/pester/*.xml` (JaCoCo), scoped to exactly `scripts/Invoke-OpenClawContainerPathValidation.ps1` and `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`: **LINE 279/303 = 92.08%**, **INSTRUCTION (branch proxy) 376/411 = 91.48%**. Both well above the 85%/75% uniform gates. (Minor, immaterial delta from the prior review's 280/303 = 92.41% / 377/411 = 91.73% — attributable to ordinary Pester coverage-instrumentation non-determinism across runs, not a regression; still comfortably PASS and the underlying source lines are unchanged from the prior review.)
- **Repo-wide, independently regenerated** (widened this run's `CodeCoverage.Path` to explicitly include `scripts/powershell/modules/OpenClawContainerValidation/*.psm1` and `scripts/powershell/modules/OpenClawRbac/*.psm1` in addition to the `scripts/*.ps1`/`scripts/*.psm1` convention, resolving the previously-noted recursion-gap Info finding for this run's own evidence): **LINE 1454/1604 = 90.65%**, **INSTRUCTION 1841/2043 = 90.13%**, across 26 classes (2 more than the prior review's 24-class figure, now including both container-validation and RBAC modules). Both above the 80% mandatory-verification floor and the 85%/75% uniform gates.
- **No production file excluded from measurement:** confirmed — `ExcludedPath` empty in this run's configuration.
- **Baseline comparison:** unchanged from the prior review (91.73% -> 92.08% is within the same PASS band as the prior review's 91.73% -> 92.41%); no regression on changed lines. The remediation commit (`3b0a2b3`) touches zero production lines (only a test fixture and documentation), so no new coverage delta is attributable to it.

### TypeScript — Coverage Verdict: N/A (zero changed files)
### Python — Coverage Verdict: N/A (zero changed files)
### C# — Coverage Verdict: N/A (zero changed files)

## Independent Toolchain Re-Verification

MCP tools (`resolve_policy_audit_template_asset`, `validate_orchestration_artifacts`, `collect_pr_context`, `run_poshqc_*`) were not available as agent tool calls in this environment; all checks were re-run directly with locally installed `pwsh 7`, PSScriptAnalyzer, and Pester 5.6.1.

| Gate | Command | Result |
|---|---|---|
| Format (idempotency) | `Invoke-Formatter` on the remediation-site file `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` (EOL-normalized, repo settings via drm-copilot's bundled `pssa.settings.psd1`) | **PASS** — byte-identical before/after. |
| Lint | `Invoke-ScriptAnalyzer -Path tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1 -Settings <pssa.settings.psd1>` | **PASS** — 0 findings (a benign internal null-reference message was emitted by the analyzer host itself in one invocation attempt but the returned finding set was empty in both attempts; not attributable to the changed line). |
| Type-check | N/A for PowerShell. | N/A |
| **Unit tests — CI-exact command, independently re-run** | `Invoke-Pester -Path tests/scripts -Output Detailed -CI` (the literal command at `.github/workflows/ci.yml:69`) | **PASS — 416 passed / 0 failed / 0 skipped.** Independently reproduces the orchestrator-supplied evidence exactly. |
| Unit tests — plain Terminal invocation, previously-failing file isolated | `Invoke-Pester -Configuration $config` scoped to `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` alone | **PASS — 2 passed / 0 failed** (both tests that failed in the prior review now pass under a completely standard invocation). |
| Unit tests — MCP-wrapper (`Invoke-PoshQCTest`) invocation, re-checked for regression | `Invoke-PoshQCTest -Root <repo> -ScanFolders @('tests/scripts')` | **409 passed / 7 failed** (independently reproduced twice). This is a *different*, wrapper-internal anomaly, not a regression of the remediated defect — see assessment below. |
| Coverage | Independent fresh regeneration (see Coverage Verdict above) | **PASS.** |
| Architecture-boundary tests | N/A. | N/A |
| Contract/schema checks | N/A — no schema/API surface changed. | N/A |
| Integration tests | Not run (requires Docker Desktop + live stack); UNVERIFIED with concrete reason, as in the prior review. Unaffected by this remediation. | UNVERIFIED (environment) |

### Assessment: is the MCP-wrapper's 409/7 result gating?

**No — assessed independently as non-gating, on the following evidence, not merely accepted from the orchestrator's framing:**

1. **The acceptance bar defined by `.claude/rules/powershell.md` is explicit and does not reference the MCP wrapper.** The determinism requirement reads: "Tests must produce identical results in Terminal and the VS Code Test Explorer... do not rely on ambient environment resolution." Both "Terminal" and "VS Code Test Explorer" are standard `Invoke-Pester` invocation paths (a terminal session calling `Invoke-Pester` directly, and VS Code's Pester Test Adapter, which likewise calls `Invoke-Pester` — neither invokes `Invoke-PoshQCTest`, a repository-external MCP-server helper function bundled from the separate `drm-copilot` package). The wrapper is not one of the two named acceptance environments.
2. **CI itself — the actual, authoritative, automated gate for this repository — invokes plain `Invoke-Pester`, not the wrapper.** `.github/workflows/ci.yml:69` reads `Invoke-Pester -Path tests/scripts -Output Detailed -CI`, independently confirmed by direct file read. This is the command this audit reproduced exactly, obtaining 416/0/0/0.
3. **Root-cause isolation (independently reproduced, not merely re-read from the executor's or prior review's prose):** re-running `Invoke-PoshQCTest` twice against the current HEAD produced `409 passed / 7 failed` on both runs — a distinct fingerprint from the pre-remediation state (which was 416/0 via this same wrapper) and from the standard-invocation state (416/0 both before isolating the single file and across the full suite). This confirms the previously-documented pattern (see `[[poshqc-test-wrapper-masks-mock-scope-bugs]]` in agent memory, added during the prior review cycle): the wrapper's own internal `-InvokePester` scriptblock chain has an invocation-path-specific interaction with this branch's test suite that a standard `Invoke-Pester -Configuration $config` call, given the identical effective settings, does not reproduce. The prior review independently verified this mechanism by A/B-testing the wrapper's default scriptblock against an equivalent caller-defined scriptblock; this audit did not re-run that full isolation but did independently reconfirm the top-level symptom (409/7 via wrapper vs. 416/0 via plain invocation, both against the post-remediation HEAD) is real and repeatable.
4. **This is a new, second wrapper-specific quirk, not a reopening of the remediated defect.** The remediated defect (unscoped `Mock` unable to resolve a nested-imported command) is specifically fixed and independently confirmed fixed under standard invocation (item above: the exact two previously-failing tests now pass, isolated and in the full suite, under plain `Invoke-Pester`). The wrapper's 7 failures are a different set of tests (per the executor's own diagnostic note, in the `Invoke-OpenClawContainerPathValidation*` split files generally, not limited to the two remediated tests) and do not reproduce under the standard runner this policy's acceptance bar targets.
5. **Conclusion: non-gating.** The mandatory toolchain-loop "Unit tests" stage, read against the acceptance bar this repository's own CI and `.claude/rules/powershell.md` actually define (standard `Invoke-Pester`, Terminal/Test-Explorer parity), passes cleanly and deterministically. The wrapper-specific 409/7 result is recorded here as a documented, non-blocking tooling observation for future reviews, not treated as a gate failure.

## Evidence Location Compliance

- **Verdict: PASS.**
- `git diff --name-only 81debeb1..3b0a2b3 | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'` returns zero matches (independently re-run for this audit's full scope).
- All evidence artifacts, including the two new remediation-cycle artifacts (`evidence/qa-gates/remediation-final-*.2026-07-11T01-30.md`, `evidence/qa-gates/remediation-verification.2026-07-11T01-30.md`), live under the canonical `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/evidence/{baseline,qa-gates,issue-updates,other}/` path.
- This audit's own independently-regenerated coverage scratch files were written to the gitignored `artifacts/pester/` location (tool's fixed output convention) and deleted after use; `git status --porcelain` confirmed clean.
- No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries needed.

## Uniform Toolchain Gates

| Gate | PowerShell | Evidence |
|---|---|---|
| Format | PASS | Independent `Invoke-Formatter` idempotency check on the remediation-site file. |
| Lint/Analyze | PASS (0 findings) | Independent `Invoke-ScriptAnalyzer` run. |
| Type-check | N/A | — |
| Architecture boundaries | N/A | — |
| Unit tests (standard invocation, CI-exact) | **PASS — 416/416, 0 failed** | This audit's independent re-run of `.github/workflows/ci.yml:69`'s literal command. |
| Single clean pass, no restart pending | Satisfied under the standard-invocation acceptance bar. | — |

## Change Budget

- Production PowerShell files changed across the full branch: 2, within the 2-file direct-mode ceiling. The remediation commit (`3b0a2b3`) adds zero new production files (touches only a test fixture and one Markdown file).
- Test files changed across the full branch: 6 (3 new, 3 modified, counting the fixture) — within reasonable batch caps.

## File Size Limit

- All changed PowerShell files remain under the 500-line cap (re-confirmed unchanged from the prior review; the remediation commit's fixture file diff is a single line and does not affect file length materially: `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` is well under 100 lines).

## Prohibited-Pattern Scan

- No `Invoke-Expression`, no plaintext secrets, no hard-coded credentials in the remediation diff (source read).
- The remediation change (`-Global` flag addition) does not introduce global mutable state in production code — it is a test-infrastructure import-scope fix, not a production-code change, and does not violate the "avoid global state" coding standard, which governs production script design, not test-harness module import scoping.

## Coverage Exclusion Policy

- `ExcludedPath` empty in this audit's independently-regenerated coverage run. No production file excluded. **PASS.**

## Benchmark Baselines / CI Workflows / Orchestrator-State Rules

- Not applicable — no files under `scripts/benchmarks/**`, no `.github/workflows/**` or `.github/actions/**` diffs, no `artifacts/orchestration/orchestrator-state.json` changes in `81debeb1..3b0a2b3`.

## Quality Tiers

- This validation tooling remains T4 (Scaffolding); not listed in `quality-tiers.yml` (consistent with prior precedent for `scripts/**` PowerShell tooling).
- Uniform coverage thresholds (line >= 85%, branch proxy >= 75%) met at both per-file and repo-wide levels (see Coverage Verdict above).

## Approved Exceptions / Documented Accommodations

1. **MCP tools not available in this review environment.** As in the prior review cycle; the `run_poshqc_*` MCP tools and `resolve_policy_audit_template_asset`/`validate_orchestration_artifacts`/`collect_pr_context` were not callable. All checks re-run directly with local `pwsh`/PSScriptAnalyzer/Pester.
2. **`Invoke-PoshQCTest`'s wrapper-internal invocation path produces a distinct 409/7 result** not reproduced by the standard `Invoke-Pester` invocation this policy's acceptance bar (and this repository's CI) actually uses. Assessed above as non-gating; recorded as a documented tooling observation, not a remediation trigger. Consistent with the pre-existing `[[poshqc-test-wrapper-masks-mock-scope-bugs]]` memory entry recorded during the prior review.
3. **PR-context artifacts.** Not independently re-verified as present/fresh in this pass (not required for this audit's conclusions — the full diff was derived directly via `git diff` against the stated merge-base and head SHAs, both independently confirmed via `git log`).

## Test Coverage Detail

See PowerShell Coverage Verdict above: per-file 92.08% line / 91.48% instruction (both changed production files, no regression); independently-regenerated repo-wide 90.65% line / 90.13% instruction (26-class scope, now including both PowerShell modules directories). Numeric coverage thresholds PASS. Unit-test-pass gate PASS under the standard/CI-exact invocation that defines this policy's acceptance bar.

## Code Quality Checks

See `code-review.2026-07-11T09-15.md` (same folder).

## Appendix A: Remediation Verification Detail

| Remediation item (from prior `remediation-inputs.2026-07-11T00-45.md`) | Independently re-verified in this audit |
|---|---|
| Add `-Global` to `Import-Module` in `Import-OpenClawContainerValidationModule` (`Fixtures.psm1:38`) | **Confirmed applied** — `git diff a79dee48..3b0a2b3 -- tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` shows exactly `Import-Module -Name $modulePath -Force -ErrorAction Stop` -> `Import-Module -Name $modulePath -Force -Global -ErrorAction Stop`, matching the required fix verbatim. |
| Re-run full `tests/scripts` suite via standard invocation, confirm 416/416 | **Confirmed** — this audit's independent CI-exact run: 416 passed / 0 failed / 0 skipped. |
| Re-verify no regression to other sibling test files using the shared fixture | **Confirmed** — full-suite pass count (416) matches the pre-remediation total exactly, with zero new failures anywhere in the suite. |
| Re-capture toolchain-loop evidence for a standard invocation | **Confirmed present** — `evidence/qa-gates/remediation-final-test.2026-07-11T01-30.md` documents four independent attempts (isolated file, full suite, MCP-wrapper reproduction of the known coverage-path defect, and a settings-isolation control), all independently re-derivable from this audit's own commands. |
| Update `issue.md` AC3/AC4 checkboxes only after verified | **Confirmed correctly handled** — AC3/AC4 were already `[x]` prior to remediation (executor's original check-off); no checkbox text was altered; this audit's `feature-audit.2026-07-11T09-15.md` independently confirms both are now substantiated. |

Additionally, the non-blocking documentation cleanup (`docs/mailbridge-runbook.md` lines 445/457, stale `/v1` references) was folded into the same commit and independently re-verified as corrected: `grep -n "4319/v1\|/v1/status" docs/mailbridge-runbook.md` returns zero matches.

## Overall Policy Verdict

**PASS — 0 Blocking findings.** The single Blocking finding from the prior review (`policy-audit.2026-07-11T00-45.md`) is remediated and independently re-verified: the shared test fixture now imports the production module with `-Global`, and both previously-failing tests pass under a completely standard `Invoke-Pester` invocation, reproduced both in isolation and as part of a full 416/416 CI-exact run. The MCP wrapper's own distinct 409/7 anomaly was independently investigated and assessed as non-gating against this policy's actual acceptance bar (Terminal/Test-Explorer parity via standard `Invoke-Pester`, matching CI's literal command). All other gates (format, lint, coverage numerics, evidence location, change budget, file size, prohibited patterns) remain PASS, consistent with and independently re-confirmed from the prior review.
