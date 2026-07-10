# Policy Audit — Issue #135 (env-array-wrap-corruption)

- Feature folder: `docs/features/active/2026-07-07-env-array-wrap-corruption-135`
- Base branch (resolved): `main`
- Merge-base SHA: `5c7e4e154b8f6791bfff868e41bc02b3c28f8c10`
- Feature branch / head: `bug/env-array-wrap-corruption-135` @ `34dfc7ef3dd48c56bfd78f6394311ae55f1ace75`
- Work mode: `minor-audit` (per `issue.md` `- Work Mode: minor-audit`)
- AC source (minor-audit): `issue.md` `## Acceptance Criteria` (AC-1..AC-6); `spec.md`/`user-story.md` intentionally absent, consistent with mode
- Reviewer: feature-review agent
- Audit timestamp: 2026-07-07T16-05

## Environment Note (Accepted Exceptions)

The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. This artifact is authored directly against the canonical major-heading structure from `.claude/skills/policy-audit-template-usage/SKILL.md` rather than the MCP-resolved template asset. This accommodation matches the accepted pattern recorded for prior reviews in this repository (#80, #99, #111, and subsequent PowerShell-only reviews). No MCP-side automated structural validation was run against this file; the structural check below is manual.

`dotnet tool restore`, `mcp__drm-copilot__run_poshqc_*`, and `mcp__drm-copilot__collect_pr_context` are likewise not available to this reviewer directly. All toolchain evidence below is drawn from the executor's committed evidence artifacts under `FEATURE/evidence/` and independently corroborated using locally installed `pwsh` 7.6.0, `PSScriptAnalyzer` 1.24.0, and `Pester` 5.6.1 (see Section 6/7 for the independent re-run commands and results).

## Rejected Scope Narrowing

No caller instruction in this review's delegation prompt attempted to narrow scope to a plan/task/phase subset, to a subset of changed files, or to mark any language's coverage as out-of-scope/informational-only. The orchestrator prompt explicitly instructed: "Determine review scope yourself from the branch diff against the base per your scope invariant." No narrowing text to record.

## Executive Summary

This is a two-line production fix (one line each in `scripts/Publish.ps1` and `scripts/New-MsixDevCert.ps1`) removing a redundant `@(...)` wrapper around `Read-EnvFileContent` calls, which previously nested the callee's already-array-safe `string[]` return inside a one-element array and caused `$OFS`-space-joining corruption of the repository `.env` file when bound to `[string[]]` parameters. The fix is accompanied by mock-shape parity corrections and two new multi-line `.env` regression tests in the two corresponding Pester test files. `scripts/Publish.Env.psm1` and `README.md` are confirmed unchanged, per the issue's explicit out-of-scope constraint (AC-3).

All six acceptance criteria (AC-1 through AC-6) are independently verified PASS. The full branch diff against `main` touches exactly one language (PowerShell); no other language has changed files on this branch. Toolchain evidence (format, analyze, test, coverage) is present, internally consistent, and independently re-verified by this reviewer. No Blocking or Major findings were identified. **Overall verdict: PASS.**

## 1. General Unit Test Policy Compliance

Reference: `.claude/rules/general-unit-test.md`.

| Requirement | Status | Evidence |
|---|---|---|
| Independence | PASS | New `It` blocks in `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` each set up their own fixture in `BeforeEach`/inline and do not depend on execution order within their own file. |
| Isolation | PASS | Each new test targets a single behavior (multi-line `.env` preservation through one call site) and mocks only the file-I/O seam (`Read-EnvFileContent`, `Write-EnvFileContent`); `Set-EnvFileValue`/`Get-EnvFileMap`/`Step-PackageVersion` run for real per the documented seam pattern. |
| Fast execution | PASS | Full targeted run (`Publish.Msix.Tests.ps1` + `Publish.Tests.ps1` + `New-MsixDevCert.Tests.ps1`, 54 tests) completes in ~3.6–4.1s in this reviewer's independent re-run (see Section 6). |
| Determinism | PASS | No `Start-Sleep`, `Thread.Sleep`, or wall-clock dependency introduced. Mocks return fixed literal fixtures. |
| Readability | PASS | New tests carry explanatory comments describing the regression mechanism (`$OFS` collapse) and the AAA structure is clear (Arrange: fixture + mocks; Act: invoke `Save-CertThumbprintToEnv` / `& $script:ScriptPath`; Assert: line-count, containment, and no-duplicate-key checks). |
| Coverage requirements (>=85% line, >=75% branch, uniform across tiers) | PASS | See Section 5 (Test Coverage Detail). Repo-wide PowerShell command/line coverage post-change: 89.93%, used as both line and branch-proxy signal per established repo precedent (Pester 5 emits command-level coverage only). |
| No regression on changed lines | PASS | Both changed lines (`Publish.ps1:118`, `New-MsixDevCert.ps1:72`) independently confirmed covered (`mi="0" ci="1"`) in `artifacts/pester/powershell-coverage.xml`. See Section 5. |
| Coverage Exclusion Policy (no production file excluded) | PASS | Coverage run scope is all 30 production `.ps1`/`.psm1` files under `scripts/**`, no `ExcludedPath` entry in either the baseline or post-change run, per `FEATURE/evidence/baseline/poshqc-test.2026-07-07T15-36.md` and `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-07T15-49.md`. |
| Scenario completeness (positive/negative/edge/error) | PASS | New regression tests cover the positive multi-line-preservation path; pre-existing tests in both files already cover negative/edge cases (missing version, blank version, signing-gate failure) unaffected by this change. |
| AAA structure | PASS | Confirmed by direct reading of both new `It` blocks (diff reviewed in full). |
| External dependencies / temp files | PASS | No real filesystem `.env` is read or written by the new or modified tests — this reviewer independently confirmed `Read-EnvFileContent`/`Write-EnvFileContent` are mocked in both contexts and that no repo `.env` file was touched by an independent re-run (`git status --porcelain` showed no `.env` change after re-running the suite). |
| Test file location (mirrors `src`/production tree under `tests/`) | PASS | `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` mirror `scripts/Publish.ps1` and `scripts/New-MsixDevCert.ps1` respectively; no colocation. |
| Mocking rules (never mock the executable directly; mock signature parity) | PASS | Mocks target the `Read-EnvFileContent`/`Write-EnvFileContent` wrapper seam from `Publish.Env.psm1`, not an external executable. Mock signatures (`param([string]$Path)` / `param([string]$Path, [string[]]$Content)`) match production named parameters. |

## 2. General Code Change Policy Compliance

Reference: `.claude/rules/general-code-change.md`.

| Requirement | Status | Evidence |
|---|---|---|
| Simplicity first | PASS | The fix is the minimal one-line change per call site; no incidental refactor. |
| Reusability / no copy-paste of new logic | PASS | No new logic introduced; only a redundant wrapper removed. |
| Extensibility / composition over inheritance | N/A | No API surface changed. |
| Separation of concerns | PASS | Fix stays within the existing I/O seam (`Publish.Env.psm1`); callers pass the seam's return value through unchanged. |
| File size limit (<=500 lines) | PASS | `scripts/Publish.ps1` 249 lines, `scripts/New-MsixDevCert.ps1` 211 lines, `tests/scripts/Publish.Tests.ps1` 428 lines, `tests/scripts/New-MsixDevCert.Tests.ps1` 226 lines — all under the 500-line limit. |
| Fail fast / no silent catch-alls | N/A | No new error-handling code introduced. |
| Naming conventions | PASS | No new identifiers introduced by the production fix; new test variable/It-block names are descriptive (`$thumb`, `$written`, `$thumbprintLines`, `$versionLines`). |
| Public API / compatibility | PASS | `Save-CertThumbprintToEnv` and the `Publish.ps1` version-persistence path retain identical external signatures/behavior contracts; only the internal binding bug is fixed. |
| Dependencies | PASS | No new dependency added. |
| I/O boundary isolation | PASS | `Read-EnvFileContent`/`Write-EnvFileContent` remain the sole `.env` I/O seam; the fix does not introduce ad hoc I/O in caller scripts. |

## 3. Language-Specific Code Change Policy Compliance (PowerShell)

Reference: `.claude/rules/powershell.md`. PowerShell is the only language with changed files on this branch (confirmed via `git diff --name-only <merge-base>..HEAD`; no `.ts`/`.tsx`/`.py`/`.cs` files changed).

| Requirement | Status | Evidence |
|---|---|---|
| Toolchain order (format -> analyze -> test) | PASS | `FEATURE/evidence/qa-gates/final-poshqc-format.2026-07-07T15-45.md` (EXIT_CODE 0) -> `final-poshqc-analyze.2026-07-07T15-46.md` (EXIT_CODE 0) -> `final-poshqc-test.2026-07-07T15-49.md` (workaround EXIT_CODE 0, 367/367 tests passing), run in that order with no restart required. |
| PowerShell 7+ compatibility | PASS | No syntax introduced outside PS7+ compatible constructs; unary-comma return idiom and `[string[]]` casts are PS7-compatible. |
| ShouldProcess for state-changing actions | N/A (unchanged) | `Save-CertThumbprintToEnv`'s existing `ShouldProcess` gate is untouched by this fix. |
| Avoid global state | PASS | No new global/script-scoped mutable state introduced. |
| Change budget (direct mode <=2 production files; per-batch <=3 prod + 3 test) | PASS | Exactly 2 production files (`scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`) and 2 test files (`tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`) changed — within the direct-mode budget. |
| Design seams (wrapper function seam preferred) | PASS | The fix operates entirely within the pre-existing `Read-EnvFileContent`/`Write-EnvFileContent`/`Set-EnvFileValue` wrapper seam in `Publish.Env.psm1`; no new seam needed, none introduced. |
| Prohibited behaviors (broad refactors, generic runner frameworks, PSSA debt, weakened assertions, sleeps/retries) | PASS | Change is scoped to exactly the two call sites and their corresponding tests; no PSScriptAnalyzer suppressions added (confirmed 0 findings independently, Section 6); no assertion weakening; no sleep/retry hacks. |

## 4. Language-Specific Unit Test Policy Compliance (PowerShell)

Reference: `.claude/rules/powershell.md` "Testing Standards" / "Deterministic Test Requirements" / "Mocking Rules".

| Requirement | Status | Evidence |
|---|---|---|
| Pester v5.x | PASS | `Describe`/`Context`/`It` structure confirmed in both edited files; independently re-run under Pester 5.6.1. |
| Test file naming/location mirrors code structure | PASS | See Section 1. |
| One behavior per `It` | PASS | Each new `It` block asserts one regression scenario (multi-line preservation through one call site). |
| Mock sparingly; prefer real code paths | PASS | `Set-EnvFileValue` deliberately left unmocked in the `New-MsixDevCert.Tests.ps1` regression test so the real update mechanism from `Publish.Env.psm1` is exercised (per the test file's own comment and the plan's P1-T8 acceptance). |
| No external dependencies in unit tests | PASS | No network/filesystem/live-executable dependency in the new tests. |
| Deterministic test requirements (no network, no ambient PATH/profile/cwd assumptions) | PASS | Fixtures are literal in-memory strings; no ambient dependency introduced. |
| Mock signature parity | PASS | See Section 1. |

## 5. Test Coverage Detail

Coverage artifact (PowerShell): `artifacts/pester/powershell-coverage.xml` (JaCoCo-format XML), corroborated at `FEATURE/evidence/baseline/poshqc-test.2026-07-07T15-36.md` (baseline) and `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-07T15-49.md` + `FEATURE/evidence/qa-gates/coverage-comparison.2026-07-07T15-52.md` (post-change/comparison).

### 5.1 Coverage Artifact Presence by Changed Language

| Language | Changed files on branch | Coverage artifact | Verdict |
|---|---|---|---|
| PowerShell | Yes (2 production, 2 test) | `artifacts/pester/powershell-coverage.xml` present, mtime 2026-07-07 11:45 local (matches the 15:45 UTC/local-offset post-change run recorded in evidence) | **PASS** |
| TypeScript | No | `coverage/lcov.info` not evaluated | N/A — zero changed TypeScript files on this branch |
| Python | No | `artifacts/python/lcov.info` not evaluated | N/A — zero changed Python files on this branch |
| C# | No | `artifacts/csharp/coverage.xml` not evaluated | N/A — zero changed C# files on this branch |

### 5.2 PowerShell Coverage Comparison

| Metric | Baseline (P0-T8, pre-fix) | Post-change (P2-T3) | Threshold | Verdict |
|---|---|---|---|---|
| Repo-wide command/line coverage | 89.94% (2,017 analyzed commands / 30 files) | 89.93% (2,015 analyzed commands / 30 files) | >= 85% | **PASS** |
| Repo-wide branch-coverage proxy | 89.94% (command-coverage proxy; Pester 5 has no separate branch metric — established repo precedent, F11/F16) | 89.93% | >= 75% | **PASS** |
| Files measured / excluded | 30 measured, 0 excluded | 30 measured, 0 excluded | No production file excluded | **PASS** |
| `scripts/Publish.ps1` changed-line coverage (line 118) | n/a (line existed pre-change; reconstructed as covered) | `mi="0" ci="1"` — independently re-confirmed by this reviewer against `artifacts/pester/powershell-coverage.xml` | No regression on changed lines | **PASS** |
| `scripts/New-MsixDevCert.ps1` changed-line coverage (line 72) | n/a (line existed pre-change; reconstructed as covered) | `mi="0" ci="1"` — independently re-confirmed by this reviewer against `artifacts/pester/powershell-coverage.xml` | No regression on changed lines | **PASS** |
| New/changed-code coverage (P1-T7, P1-T8 regression tests) | n/a (new test code) | Both regression `It` blocks pass in isolation (targeted run, 54/54) and repo-wide (367/367) | New code (test) covered by its own execution | **PASS** |

Independent verification performed by this reviewer (not merely trusting the executor's reported numbers): extracted the `<sourcefile name="Publish.ps1">` and `<sourcefile name="New-MsixDevCert.ps1">` blocks from `artifacts/pester/powershell-coverage.xml` and confirmed `<line nr="118" mi="0" ci="1" .../>` and `<line nr="72" mi="0" ci="1" .../>` respectively. Also recomputed the repo-wide `<counter type="INSTRUCTION" missed="203" covered="1812" />` at the report root: 1812/2015 = 89.93%, matching the evidence's reported figure exactly.

The 0.01 percentage-point repo-wide decrease and the small per-file aggregate-ratio decreases (`Publish.ps1`: 97.50% -> 97.47%; `New-MsixDevCert.ps1`: 48.89% -> 47.73%) are mechanically explained by the removal of one already-covered line per file (denominator shrinks along with numerator); this is documented and reasoned correctly in `FEATURE/evidence/qa-gates/coverage-comparison.2026-07-07T15-52.md` and is not a coverage regression on any surviving or new line.

### 5.3 Coverage Tooling Accommodation (Documented Repo Precedent)

`mcp__drm-copilot__run_poshqc_test` fails in this repository on every invocation because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` entries that exist only in the `drm-copilot` source repository. This is a pre-existing, previously reproduced and accepted defect (F11 `#111`, F16 `#125`), not introduced by this feature. The executor's accepted workaround — importing the bundled `PoshQC.psd1` module directly with a scratchpad-only corrected `runsettings` covering all 30 production `scripts/**` files and no `ExcludedPath` — is consistent with prior repository precedent and is accepted here as well.

## 6. Test Execution Metrics

| Run | Source | Result |
|---|---|---|
| Baseline (pre-fix), repo-wide | `FEATURE/evidence/baseline/poshqc-test.2026-07-07T15-36.md` | Passed 365 / Failed 0 / Skipped 0 (30.32s) |
| Post-change, repo-wide | `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-07T15-49.md` | Passed 367 / Failed 0 / Skipped 0 (22.8s) — +2 tests (the new regression tests) |
| Targeted (2 edited test files + `Publish.Msix.Tests.ps1`), executor | `FEATURE/evidence/regression-testing/targeted-verification.2026-07-07T15-42.md` | Passed 54 / Failed 0 / Skipped 0 (3.72s) |
| **Independent reviewer re-run** (same 3-file targeted scope, `pwsh` 7.6.0 / Pester 5.6.1, plain `Invoke-Pester`, no coverage settings) | This audit, ad hoc | Passed 54 / Failed 0 / Skipped 0 (4.13s). Confirms the identical numeric outcome as the executor's targeted-verification evidence, independent of the MCP tool. |
| **Independent reviewer format check** (`Invoke-Formatter -ScriptDefinition`, idempotency check on all 4 edited files, PSScriptAnalyzer bundled formatter defaults) | This audit, ad hoc | CLEAN on all 4 files (`scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`) |
| **Independent reviewer lint check** (`Invoke-ScriptAnalyzer`, default rule set, PSScriptAnalyzer 1.24.0; repo-scoped settings file path referenced in `.claude/rules/powershell.md` does not exist in this repo, consistent with the F11/F16 tooling-gap precedent) | This audit, ad hoc | 0 findings on all 4 edited files |

Note on the reviewer's first targeted-run attempt: an initial re-run using a different file-array order (`New-MsixDevCert.Tests.ps1`, `Publish.Tests.ps1`, `Publish.Msix.Tests.ps1`) reproduced 25 failures due to a pre-existing, documented cross-file test-order coupling (`Publish.Tests.ps1`'s `Mock Invoke-VersionStamp` requires `Invoke-VersionStamp`, defined in `scripts/Publish.Msix.psm1`, to already be resolvable — satisfied only when `Publish.Msix.Tests.ps1` discovery runs first). This coupling is pre-existing in the test suite (unrelated to this feature's changes) and is explicitly documented in `FEATURE/evidence/regression-testing/targeted-verification.2026-07-07T15-42.md`. Re-running with the corrected file order (`Publish.Msix.Tests.ps1` first) reproduced the reported 54/54 passing result exactly. This is recorded as an Informational finding in the code review (Section 7 below / code-review artifact), not a Blocking finding, since it predates this branch's changes and the full repo-wide Phase 2 run (which discovers test files in natural/alphabetical order) is unaffected.

## 7. Code Quality Checks

See `code-review.2026-07-07T16-05.md` for the full findings table. Summary: 0 Blocking, 0 Major, 1 Informational (pre-existing test-order coupling, not introduced by this branch), 1 Informational (out-of-scope uncommitted working-tree `README.md`/agent-memory changes, not part of the branch diff).

## 8. Gaps and Exceptions

1. MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` were not available to this reviewer; the canonical major-heading structure was reproduced manually per `policy-audit-template-usage`. No structural auto-validation was run against this artifact.
2. `mcp__drm-copilot__run_poshqc_test`'s bundled coverage runsettings does not resolve in this repository (pre-existing, previously accepted defect F11/F16); the executor's scratchpad-corrected-runsettings workaround was used for both baseline and post-change coverage figures, and this reviewer accepts the same workaround as sufficient evidence, having independently cross-checked the raw `artifacts/pester/powershell-coverage.xml` XML against the reported percentages and per-line hit data.
3. Pester v5 emits command-level coverage only (no distinct branch-coverage metric for PowerShell); the command-coverage percentage is used as the branch-coverage proxy, consistent with established repository precedent (F11, F16) and accepted here.
4. `mcp__drm-copilot__collect_pr_context` was not invoked by this reviewer because the supplied `artifacts/pr_context.summary.txt` / `artifacts/pr_context.appendix.txt` pair already resolves to the current branch head (`34dfc7ef3dd48c56bfd78f6394311ae55f1ace75`) and merge-base (`5c7e4e154b8f6791bfff868e41bc02b3c28f8c10`); the artifacts were treated as fresh per `pr-context-artifacts`, and no refresh was performed.
5. This feature branch does not modify any path under `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**`; the `modified-workflow-needs-green-run` policy rule does not fire. No benchmark baseline files are touched; `.claude/rules/benchmark-baselines.md` and `.claude/rules/ci-workflows.md` are not applicable to this diff.
6. The working tree at review time contains uncommitted, unrelated modifications (`README.md` uninstall-troubleshooting addition, `.claude/agent-memory/orchestrator/MEMORY.md`, and a new untracked `.claude/agent-memory/orchestrator/feedback_no-direct-typed-engineer-delegation.md`). These are NOT part of the committed branch diff (`5c7e4e15..34dfc7e`) and are excluded from this audit's scope per the scope invariant (audit scope is the committed branch-vs-base diff, not working-tree state). Recorded here as an observation only, not a finding.

## Evidence Location Compliance

`git diff --name-only 5c7e4e154b8f6791bfff868e41bc02b3c28f8c10..34dfc7e` was scanned for any file path matching `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, or `artifacts/coverage/`. No such path is present in the branch diff. All evidence produced by this feature is correctly located under `docs/features/active/2026-07-07-env-array-wrap-corruption-135/evidence/{baseline,regression-testing,qa-gates}/`, matching the canonical scheme in `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. No `validate_evidence_locations.py` script exists in this repository (consistent with prior review precedent); the scan was performed manually via `git diff --name-only` filtering as the documented fallback. No violations found; no `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries required.

## 9. Summary of Changes

- `scripts/Publish.ps1` (1 line changed): removed redundant `@(...)` wrap around `Read-EnvFileContent` (AC-1).
- `scripts/New-MsixDevCert.ps1` (1 line changed): removed redundant `@(...)` wrap around `Read-EnvFileContent` (AC-2).
- `tests/scripts/Publish.Tests.ps1` (+36/-3 lines): mock parity fix (unary-comma return idiom, AC-4) + new multi-line regression test (AC-5).
- `tests/scripts/New-MsixDevCert.Tests.ps1` (+64/-1 lines): mock parity fix (unary-comma return idiom, AC-4) + new multi-line regression test (AC-5).
- `scripts/Publish.Env.psm1`: unchanged (confirmed via `git diff`, AC-3).
- `README.md`: unchanged by this branch's commits (confirmed via `git diff <merge-base>..HEAD -- README.md`, no output; AC-3). Note: an unrelated, uncommitted local `README.md` edit exists in the working tree — see Gaps and Exceptions item 6.
- Plus feature-tracking documents: `issue.md`, `plan.2026-07-07T11-17.md`, `docs/features/potential/promoted/2026-07-07-env-array-wrap-corruption.md`, and 8 evidence artifacts under `FEATURE/evidence/`.

## 10. Compliance Verdict

**PASS.** All applicable policy sections (General Unit Test, General Code Change, PowerShell-specific code-change and unit-test policy) are satisfied. Coverage verdicts are explicit PASS for the only language with changed files (PowerShell); all other languages are N/A (zero changed files, confirmed by `git diff --name-only`). No Blocking or Major findings. Two Informational notes recorded (pre-existing test-order coupling; out-of-scope uncommitted working-tree changes). No remediation is required; `remediation-inputs.<timestamp>.md` is not produced for this review.

## Appendix A: Test Inventory

| Test file | New/modified `It` blocks in this branch | Behavior covered |
|---|---|---|
| `tests/scripts/Publish.Tests.ps1` | Mock body change (BeforeEach, unary-comma parity) + 1 new `It`: "preserves a multi-line .env verbatim and updates only OPENCLAW_PACKAGE_VERSION in place (regression: redundant @() wrap)" | Multi-line `.env` preservation through the version-persistence call site; guards against reintroduction of the redundant `@()` wrap. |
| `tests/scripts/New-MsixDevCert.Tests.ps1` | Mock body change (unary-comma parity, existing `Save-CertThumbprintToEnv (AC-5)` context) + 1 new `Context`/`It`: "Save-CertThumbprintToEnv multi-line regression (AC-5)" / "preserves a multi-line .env verbatim and updates only OPENCLAW_CERT_THUMBPRINT in place (regression: redundant @() wrap)" | Multi-line `.env` preservation through the thumbprint-persistence call site, using the real (unmocked) `Set-EnvFileValue`; guards against reintroduction of the redundant `@()` wrap. |

Pre-existing `It` blocks in both files (unaffected by this change, continue to pass) are not re-enumerated here; see the full targeted-verification evidence (54/54 passing) and repo-wide post-change evidence (367/367 passing) for the complete inventory count.

## Appendix B: Toolchain Commands Reference

| Stage | Command | Source |
|---|---|---|
| Format (baseline) | `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) | `FEATURE/evidence/baseline/poshqc-format.2026-07-07T15-31.md` |
| Analyze (baseline) | `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) | `FEATURE/evidence/baseline/poshqc-analyze.2026-07-07T15-31.md` |
| Test+coverage (baseline) | `mcp__drm-copilot__run_poshqc_test` (fails on known defect) -> `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad-corrected-runsettings>` | `FEATURE/evidence/baseline/poshqc-test.2026-07-07T15-36.md` |
| Format (final) | `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) | `FEATURE/evidence/qa-gates/final-poshqc-format.2026-07-07T15-45.md` |
| Analyze (final) | `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) | `FEATURE/evidence/qa-gates/final-poshqc-analyze.2026-07-07T15-46.md` |
| Test+coverage (final) | `mcp__drm-copilot__run_poshqc_test` (fails on known defect) -> `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad-corrected-runsettings>` | `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-07T15-49.md` |
| Coverage comparison | analysis of baseline vs. final coverage XML | `FEATURE/evidence/qa-gates/coverage-comparison.2026-07-07T15-52.md` |
| Targeted verification | `mcp__drm-copilot__run_poshqc_test` (scoped) (fails on known defect) -> `Invoke-PoshQCTest` targeted workaround | `FEATURE/evidence/regression-testing/targeted-verification.2026-07-07T15-42.md` |
| Independent format re-check (reviewer) | `Invoke-Formatter -ScriptDefinition <content>` per file, idempotency compare | This audit, Section 6 |
| Independent lint re-check (reviewer) | `Invoke-ScriptAnalyzer -Path <file>` (default rules, PSScriptAnalyzer 1.24.0) | This audit, Section 6 |
| Independent test re-check (reviewer) | `Invoke-Pester -Configuration $cfg` (Pester 5.6.1, `Run.Path` = `Publish.Msix.Tests.ps1`, `Publish.Tests.ps1`, `New-MsixDevCert.Tests.ps1`) | This audit, Section 6 |
