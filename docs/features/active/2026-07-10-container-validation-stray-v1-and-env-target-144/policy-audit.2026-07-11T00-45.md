# Policy Compliance Audit — Issue #144 (container-validation-stray-v1-and-env-target)

- Reviewed: 2026-07-11T00-45
- Reviewer: feature-review agent
- Base branch: `main` (merge-base `81debeb1d58dd7226e0eec1bc66aa154047e6a82`, committed 2026-07-10T21:17:12-04:00)
- Head: `bug/container-validation-stray-v1-and-env-target-144` @ `a79dee489b46e177f6b98d605f9ecd0c8e8f9f24` (committed 2026-07-10T23:43:18-04:00)
- Work mode: `minor-audit` (AC source: `issue.md` `## Acceptance Criteria`, AC1–AC7, per the persisted `- Work Mode: minor-audit` marker)
- Scope: full branch diff, merge-base..HEAD — `git diff --stat 81debeb1..a79dee48` — 28 files changed (979 insertions / 39 deletions)

## Rejected Scope Narrowing

None. The delegating prompt explicitly stated "Determine scope yourself; do not narrow it" and instructed the review to treat the "Notable context" (non-goals, AC7 rationale) as claims to verify, not as pre-decided scope. No caller language attempted to mark any language out of scope, skip a toolchain/coverage check, or restrict the diff to a subset of files. The full `81debeb1..a79dee48` diff was used as the audit scope, and all stated non-goals (no `src/OpenClaw.HostAdapter/**` change, no WebSocket/device-pairing handshake, tracked docker-compose/Dockerfiles/`Install.Helpers.psm1` untouched) were independently re-verified rather than assumed (see AC5/AC7 sections below).

## Policy Reading Order Applied

1. `CLAUDE.md` — checked; does not exist at the repository root in this worktree (consistent with prior review precedent #137/#139/#142; this repo state persists across reviews).
2. `.claude/rules/general-code-change.md` — read, applied below.
3. `.claude/rules/general-unit-test.md` — read, applied below (Determinism/Isolation principles are directly implicated by the Blocking finding).
4. `.claude/rules/powershell.md` — read, applied (only source language with changed files on this branch).
5. `.claude/rules/quality-tiers.md`, `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/tonality.md` — reviewed for applicability. No C#, Python, or TypeScript files changed (`git diff --name-status` confirms); no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` diffs; no `artifacts/orchestration/orchestrator-state.json` change.

## Language Inventory and Coverage Verdicts

`git diff --name-status 81debeb1..a79dee48` confirms the only source language with changed files is **PowerShell**: 2 production files (`scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, `scripts/Invoke-OpenClawContainerPathValidation.ps1`, both modified) plus the module manifest `OpenClawContainerValidation.psd1` (export-declaration data file), and 5 test files (2 new: `Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1`, `Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1`, `OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1`; 2 modified: `Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1`, `Invoke-OpenClawContainerPathValidation.Tests.ps1`). Remaining changed files are `README.md`, `docs/mailbridge-runbook.md` (Markdown documentation), and Markdown feature-folder evidence/plan/issue files. TypeScript, Python, and C# have zero changed files on this branch — their coverage sections are correctly N/A per the zero-changed-files exception.

### PowerShell — Coverage Verdict: PASS (numeric thresholds); Unit-test gate: **FAIL** (see Blocking finding)

Canonical artifact `artifacts/pester/powershell-coverage.xml` exists (JaCoCo/CoverageGutters format) from the executor's corrected-runsettings run scoped to exactly the two in-scope production files. Independently re-parsed with an `[xml]` scratch parser rather than trusting the branch's prose, and independently re-generated (repo-wide scope, matching the `#142`-precedent convention) to obtain a genuine repo-wide figure:

- PowerShell per-language comparison: Baseline: 91.73% -> Post-change: 92.41%, Change: +0.68%, New/changed-code coverage: 92.41% line / 91.73% command(branch-proxy) across the two changed production files, Disposition: PASS, Evidence: `artifacts/pester/powershell-coverage.xml` independently re-parsed; `evidence/qa-gates/final-poshqc-coverage.2026-07-10T20-30.md`.
- **Per-file (two changed production files, independently parsed `<class><counter>` blocks):**

| File | Line | Instruction (command proxy) | >= 85% line | >= 75% branch proxy |
|---|---|---|---|---|
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | 145/159 = **91.19%** | 204/223 = 91.48% | PASS | PASS |
| `scripts/powershell/modules/.../OpenClawContainerValidation.psm1` | 135/144 = **93.75%** | 173/188 = 92.02% | PASS | PASS |
| **Aggregate (both files)** | 280/303 = **92.41%** | 377/411 = **91.73%** | PASS | PASS |

  Every figure matches `evidence/qa-gates/final-poshqc-coverage.2026-07-10T20-30.md` exactly.
- **Baseline (from `evidence/baseline/poshqc-coverage.2026-07-10T20-30.md`, pre-change, same two files):** line 91.73% (255/278), command 91.08% (347/381). Delta: **+0.68pp line / +0.65pp command, no regression.** Baseline-freshness note: the baseline artifact is timestamped 20:30, nominally 47 minutes before the merge-base commit's own timestamp (21:17); however its recorded pass count (406) matches PR #142's own final recorded count exactly, confirming the baseline content already reflects the merge-base state (same quirk class as the #119 precedent; the timestamp label, not the content, is the anomaly).
- **Repo-wide, independently reproduced in this audit** (not merely re-parsed — the executor's own committed coverage run was scoped to only the two changed files, so no repo-wide figure existed to re-parse; a fresh repo-wide run was generated using the `#142`-precedent convention `CodeCoverage.Path = @('scripts/*.ps1','scripts/*.psm1')`, `ExcludedPath = @()`): report-level `LINE` 1315/1455 = **90.38%**; report-level `INSTRUCTION` (command/branch-proxy) 1663/1849 = **89.94%**, 1,849 analyzed commands across 24 files. Both well above the 80% mandatory-verification FAIL floor and the 85%/75% uniform gates.
- **Repo-wide convention limitation (Info, not a branch defect):** the `scripts/*.ps1`/`scripts/*.psm1` glob convention only matches files directly under `scripts/`; it does not recurse into `scripts/powershell/modules/**`, so it silently excludes `OpenClawContainerValidation.psm1` (one of this branch's two changed production files) and the pre-existing `OpenClawRbac` module from any "repo-wide" figure. This is a pre-existing tooling gap in the repo-wide convention (not introduced by this branch) and does not affect this branch's own per-file compliance, which is independently confirmed above via the exact-two-file corrected-runsettings run. Recommended follow-up: widen the convention to enumerate `scripts/**` recursively (a two-level `**` glob only reaches one extra directory level; true recursion needs `Get-ChildItem -Recurse` based path enumeration, not a glob pattern) — this is orthogonal to this branch's own compliance and does not block the verdict.
- **No production file excluded from measurement:** confirmed — the corrected runsettings used by the executor carries an empty `ExcludedPath`.

Independent verification method: `[xml]` parse of `artifacts/pester/powershell-coverage.xml`, cross-checking `//report/counter[@type='LINE'|'INSTRUCTION']` (both per-file-scoped and independently-regenerated repo-wide) against the executor's `evidence/qa-gates/final-poshqc-coverage.2026-07-10T20-30.md` prose. All numeric coverage claims matched exactly.

### TypeScript — Coverage Verdict: N/A (zero changed files)
### Python — Coverage Verdict: N/A (zero changed files)
### C# — Coverage Verdict: N/A (zero changed files)

## Independent Toolchain Re-Verification (not solely trusting executor evidence)

The MCP tools `resolve_policy_audit_template_asset`, `validate_orchestration_artifacts`, and `collect_pr_context` are not available in this review environment; the `run_poshqc_*` MCP tools were also not available as agent tool calls, so all checks below were re-run directly with locally installed `pwsh 7`, PSScriptAnalyzer 1.24.0, and Pester 5.6.1, using the repo's own PSScriptAnalyzer settings file (`settings/pssa.settings.psd1`, the same file PoshQC's `run_poshqc_format`/`run_poshqc_analyze` consume) and EOL-normalization (`-replace "\`r?\`n", "\`n"`) matching PoshQC's own formatter pre-processing step:

| Gate | Command | Result |
|---|---|---|
| Format (idempotency) | `Invoke-Formatter -ScriptDefinition <EOL-normalized content> -Settings <repo pssa.settings.psd1>` on all 8 changed PowerShell files (2 production + manifest + 5 test) | **PASS** — all 8 files byte-identical before/after formatting. (Note: an initial attempt without `-Settings` and without EOL normalization produced false non-idempotency on 2 files — a reviewer-tooling artifact of PSScriptAnalyzer's default formatting profile and its "mixed line endings" detector, not a real defect; resolved by using the repo's actual settings file and normalization step, matching how `PoshQC.Analyzer.psm1`'s `Invoke-PoshQCFormat` actually invokes the formatter.) |
| Lint | `Invoke-ScriptAnalyzer -Path <file> -Settings <repo pssa.settings.psd1> -Severity Error,Warning,Information` on the same 8 files | **PASS** — zero findings across all 8 files. |
| Type-check | N/A for PowerShell per `.claude/rules/powershell.md`. | N/A |
| Unit tests (repo-wide, coverage-instrumented invocation matching the executor's own tooling path) | `Invoke-PoshQCTest` (corrected runsettings, `Run.Path = tests/scripts`) | **PASS** — 416/416 passed, 0 failed, reproducing `evidence/qa-gates/final-poshqc-test.2026-07-10T20-30.md` exactly. |
| **Unit tests (repo-wide, standard `Invoke-Pester` invocation — no coverage, no MCP wrapper)** | `Invoke-Pester` (v5.6.1, `Run.Path = tests/scripts`, plain `New-PesterConfiguration`) | **FAIL** — 414 passed, **2 failed**, both in `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` (`CommandNotFoundException: Could not find Command Get-OpenClawOperatorEnvFilePath`). Reproduced standalone (file run alone) and in the full-suite run; not a flake. **See Blocking finding below.** |
| Coverage | Independent `[xml]` re-parse of `artifacts/pester/powershell-coverage.xml` plus an independently-generated repo-wide run | **PASS** (numeric thresholds) — see Coverage Verdict above. |
| Architecture-boundary tests | N/A — no .NET/dependency-cruiser boundary touched by this PowerShell-only change. | N/A |
| Contract/schema checks | N/A — no API/schema surface changed; the new `GatewayTokenInContainer`/`HostAdapterInContainer` result shapes reuse the existing `Get-OpenClawValidationResult` pscustomobject contract (source read). | N/A |
| Integration tests | Not run in this environment (requires Docker Desktop + a live `openclaw-core`/`openclaw-agent` stack). The issue's own manual retest step ("run the validation against a healthy install and confirm `OverallResult: Expected`") is a rollout verification, not a per-commit gate. Marked UNVERIFIED with this concrete reason; unit/mock-seam evidence covers the wiring deterministically. | UNVERIFIED (environment) |

**The single discrepancy found between the executor's reported evidence and this audit's independent re-derivation is the plain-`Invoke-Pester` unit-test failure above.** All coverage and format/analyze figures matched exactly.

### Blocking Finding: Two new tests fail under standard `Invoke-Pester`; pass only via the MCP wrapper's specific invocation path

- **What was independently verified:** `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` contains two `It` blocks (lines 27 and 55) that call `Mock Get-OpenClawOperatorEnvFilePath { ... }` **without** `-ModuleName`. Under a plain `Invoke-Pester` run (both the file alone and as part of the full `tests/scripts` suite), Pester's diagnostic trace shows: `Mock: Searching for command Get-OpenClawOperatorEnvFilePath in the script scope. Did not find command Get-OpenClawOperatorEnvFilePath in the script scope.`, and both tests fail with `CommandNotFoundException`.
- **Root cause, independently isolated:** the shared test fixture `Import-OpenClawContainerValidationModule` (defined in the pre-existing, unmodified `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1:31-39`) calls `Import-Module -Name $modulePath -Force -ErrorAction Stop` **without `-Global`**. Because this `Import-Module` call executes from inside a function that itself belongs to another module (`Fixtures.psm1`), the imported `OpenClawContainerValidation` module becomes a **nested module of Fixtures**, and its exported functions are not propagated to the caller's global/script scope (they are not re-exported by `Fixtures.psm1`'s own `Export-ModuleMember` list). The test file's own top-level `BeforeDiscovery` block does a *separate*, correctly-scoped `Import-Module -Force` first, but `BeforeAll`'s later call to the nested-import helper (needed by every sibling test file in this suite, for `Install-DefaultInvokeFakeDocker` etc.) re-imports with `-Force`, replacing the earlier global registration with the nested one — so by the time the `It` block's unscoped `Mock` runs, `Get-OpenClawOperatorEnvFilePath` is invisible to script-scope command resolution.
- **Why it passes under the executor's own evidence:** every "passing" run cited in this branch's committed evidence (`evidence/qa-gates/final-poshqc-test.2026-07-10T20-30.md`, `evidence/baseline/poshqc-coverage.2026-07-10T20-30.md`) was captured via the `Invoke-PoshQCTest` MCP-wrapper invocation path specifically, never a plain `Invoke-Pester` call. This audit confirmed empirically (scratch reproduction, described below) that the wrapper's specific closure-scoped default `-InvokePester` scriptblock — an implementation detail internal to that one tool — happens to avoid the visibility bug, while an equivalent `Invoke-Pester -Configuration $config` call made from a normal top-level script/session (matching how a CI job, a developer's terminal, or VS Code's Pester Test Adapter would invoke it) reproduces the failure every time. This is a `run_poshqc_test`-adjacent tool quirk masking a real, independent, standard-tool-reproducible test defect, distinct from the previously-documented "bundled drm-copilot `CodeCoverage.Path`" MCP defect (`#111`/`#125`/`#135`/`#137`/`#139`/`#142`).
- **Fix verified (scratch reproduction only; no repository files were modified by this review):** adding `-Global` to the `Import-Module` call at `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1:38` makes both tests pass under a completely plain `Invoke-Pester` run (2/2 passed, confirmed via a temporary scratch copy of the fixture + test file, both removed after verification; `git status --porcelain` confirmed clean afterward).
- **Policy basis:** `.claude/rules/powershell.md` — "Tests must produce identical results in Terminal and the VS Code Test Explorer... do not rely on ambient environment resolution." `.claude/rules/general-unit-test.md` — Determinism and Isolation core principles. AC4's explicit claim ("the full `tests/scripts` Pester suite passes ... in a single pass") is not substantiated under a standard invocation and is therefore not fully met as delivered.
- **Disposition: Blocking.** See `remediation-inputs.2026-07-11T00-45.md`.

## Evidence Location Compliance

- **Verdict: PASS.**
- `git diff --name-only 81debeb1..a79dee48 | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'` returns zero matches.
- All 15 evidence artifacts added by this branch live under the canonical path `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/evidence/{baseline,qa-gates,issue-updates,other}/`, consistent with the Evidence Location Invariant.
- `validate_evidence_locations.py` does not exist anywhere in this repository tree (established on prior reviews, `.claude/hooks/enforce-evidence-locations.ps1` is present but the Python validator script is not); fell back to the manual diff-scan method per precedent. No violations found.
- The raw coverage/test intermediates (`artifacts/pester/powershell-coverage*.xml`, `pester-junit*.xml`) are gitignored, non-committed raw tool output — outside the invariant's scope. This audit's own independently-generated repo-wide/single-file scratch coverage runs were written to this same gitignored `artifacts/pester/` location (consistent with the tool's fixed output convention, not a freely-chosen path) and are not committed evidence artifacts.
- No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries were needed; no caller instruction specified a non-canonical evidence path.

## Uniform Toolchain Gates (general-code-change.md, powershell.md)

| Gate | PowerShell | Evidence |
|---|---|---|
| Format | PASS (0 diffs, independently reproduced) | `evidence/qa-gates/final-poshqc-format.2026-07-10T20-30.md`; this audit's independent `Invoke-Formatter` idempotency check. |
| Lint/Analyze | PASS (0 findings, independently reproduced) | `evidence/qa-gates/final-poshqc-analyze.2026-07-10T20-30.md`; this audit's independent `Invoke-ScriptAnalyzer` run. |
| Type-check | N/A (PowerShell) | — |
| Architecture boundaries | N/A (no .NET boundary changes) | — |
| Unit tests | **FAIL under standard invocation** (416/416 only via the MCP-wrapper coverage path; 414/416 — 2 failures — under plain `Invoke-Pester`, both standalone and full-suite) | This audit's independent re-run; see Blocking finding above. |
| Single clean pass, no restart pending | Not satisfied — the toolchain loop's own "Unit tests" stage does not pass cleanly under a standard invocation of the tool the loop specifies (Pester). | — |

## Change Budget (powershell.md)

- Production PowerShell files changed: 2 (`OpenClawContainerValidation.psm1`, `Invoke-OpenClawContainerPathValidation.ps1`), within the direct-mode ceiling of 2. The module manifest `OpenClawContainerValidation.psd1` (an `FunctionsToExport` data-declaration edit, self-disclosed by the executor as a mechanically-necessary follow-on to the `.psm1` `Export-ModuleMember` change, not new executable logic) is treated consistently with prior precedent as not counting against the production-file ceiling.
- Test files changed: 5 (3 new, 2 modified) — within reasonable batch caps; no evidence of a batching violation.

## File Size Limit (general-code-change.md, 500-line cap)

- `scripts/Invoke-OpenClawContainerPathValidation.ps1`: 313 lines. `scripts/powershell/modules/.../OpenClawContainerValidation.psm1`: 452 lines. `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`: 366 lines. `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1`: 175 lines. `tests/scripts/Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1`: 109 lines. `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1`: 82 lines. `tests/scripts/powershell/modules/.../OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1`: 57 lines. All under the 500-line cap.

## Prohibited-Pattern Scan (powershell.md, general-unit-test.md)

- No `Invoke-Expression`, no plaintext secrets, no hard-coded credentials in any changed production file (source read of both).
- No temp-file usage in any of the 3 new test files (direct read: all fixtures are in-memory strings/lists).
- Both production functions added (`Get-OpenClawOperatorEnvFilePath`, `Resolve-OpenClawDefaultEnvFilePath`, `Test-OpenClawGatewayTokenInContainer`) use `[CmdletBinding()]`, `[OutputType(...)]`, and mandatory-parameter validation where applicable; `Test-OpenClawGatewayTokenInContainer` routes exclusively through the pre-existing `Invoke-OpenClawDockerCommand` seam (no new executable process invocation).
- Mocks in the (working) tests use `-ModuleName OpenClawContainerValidation` mock-signature parity consistent with the existing suite's convention, except the 2 tests identified in the Blocking finding.

## Coverage Exclusion Policy (general-unit-test.md)

- The corrected runsettings used for the branch's own per-file coverage measurement carries an empty `ExcludedPath`; both changed production files are in the coverage denominator. No production file is excluded. **PASS.**

## Benchmark Baselines / CI Workflows / Orchestrator-State Rules

- Not applicable — no files under `scripts/benchmarks/**`, no `.github/workflows/**` or `.github/actions/**` diffs, and no `artifacts/orchestration/orchestrator-state.json` changes (`git diff --name-only` confirms zero matches for all patterns). The `modified-workflow-needs-green-run` policy rule is not triggered.

## Quality Tiers (quality-tiers.md)

- `quality-tiers.yml` at repo root maps only `.csproj`/`.sln` projects; PowerShell tooling under `scripts/**` is not a listed project and is not required to be (precedent: #139/#142 audits). This validation tooling is exemplary T4 (Scaffolding).
- Uniform coverage thresholds (line >= 85%, branch proxy >= 75%) apply regardless of tier and are met at both per-file and (independently-reproduced) repo-wide levels.
- T1/T2-specific gates (property-test density, mutation score) are not triggered for T4 tooling.

## Approved Exceptions / Documented Accommodations

1. **MCP tools not available in this review environment.** `resolve_policy_audit_template_asset`, `validate_orchestration_artifacts`, `collect_pr_context`, and the `run_poshqc_*` tools were not available as agent tool calls. Review artifacts mirror the most recent validator-passing artifact set (#142, `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/*.2026-07-10T20-01.md`) combined with known validator-quirk corrections.
2. **`mcp__drm-copilot__run_poshqc_test`'s underlying bundled `pester.runsettings.psd1` hardcodes drm-copilot-specific `CodeCoverage.Path` entries** absent from this repository (pre-existing defect affecting #111/#125/#135/#137/#139/#142, reproduced again by the executor here). The branch's corrected-runsettings workaround is the accepted substitute for *coverage measurement*; however, this audit found that the workaround's *invocation path itself* (via `Invoke-PoshQCTest`, not the coverage settings per se) also masks the independent, standard-tool-reproducible test defect documented in the Blocking finding above — a new, distinct quirk from the previously-documented one.
3. **PR-context artifacts present and fresh.** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` were verified fresh (head SHA `a79dee48` matches the current branch head exactly). No regeneration was needed.

## 5. Test Coverage Detail

See the PowerShell Coverage Verdict section above for the complete per-file, repo-wide (independently regenerated), and baseline-comparison detail. Summary: per-file 92.41% line / 91.73% command (both changed production files, +0.68pp/+0.65pp vs baseline, no regression); independently-regenerated repo-wide 90.38% line / 89.94% command (24-file convention, with a disclosed, pre-existing, non-blocking recursion-depth limitation in that convention). Numeric coverage thresholds are PASS; the unit-test-pass gate that should accompany this coverage run is FAIL under standard invocation (see Blocking finding).

## 7. Code Quality Checks

See `code-review.2026-07-11T00-45.md` (same folder) for the full code-quality review: 1 Blocking, 0 Major, 1 Minor, 3 Info findings.

## Appendix A: Test Inventory

| Suite | Scope | Independent re-run result |
|---|---|---|
| `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` (modified) | HostAdapter in-container probe: `/status` (not `/v1/status`) in the shell command and `ExpectedCondition`; 200/500/non-zero-exit cases | PASS (part of both the 416/416 coverage-mode run and the 414/416 plain run — unaffected by the Blocking finding) |
| `tests/scripts/powershell/modules/.../OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1` (new, 5 Its) | `Get-OpenClawOperatorEnvFilePath` (real-call, no mock) present/absent-input cases; `Resolve-OpenClawDefaultEnvFilePath` (module-scoped `Test-Path` mock) present/absent/null-operator cases | PASS under plain `Invoke-Pester` (independently confirmed standalone) — not affected by the Blocking finding, since these tests call the pure helpers directly rather than mocking them. |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` (new, 2 Its) | End-to-end default `-EnvFilePath` resolution (operator-file-present / absent) | **FAIL under plain `Invoke-Pester`** (both tests) — see Blocking finding. PASS only via the MCP wrapper's coverage-mode invocation. |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1` (new, 3 Its) | `GatewayTokenInContainer` present/absent; `AgentDashboard.ExpectedCondition` no longer implies authentication | PASS (independently confirmed; uses `-ModuleName`-scoped mocks and an in-file fake-docker seam, not the vulnerable unscoped-Mock pattern) |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (modified) | Full-run aggregation count `14 -> 15`; `GatewayTokenInContainer.IsExpected` assertions added to 3 existing full-run Its | PASS |

## Appendix B: Command Reference

Commands executed by this review (all check-only; no mutation of source or policy files):

```
git diff --stat 81debeb1d58dd7226e0eec1bc66aa154047e6a82..a79dee489b46e177f6b98d605f9ecd0c8e8f9f24
git diff --name-status 81debeb1..a79dee48
git diff 81debeb1..a79dee48 -- scripts/... tests/scripts/... README.md docs/mailbridge-runbook.md
git diff --name-only 81debeb1..a79dee48 | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'   # zero matches
git show -s --format=%cI 81debeb1d58dd7226e0eec1bc66aa154047e6a82   # merge-base commit time
Invoke-Formatter -ScriptDefinition <EOL-normalized content> -Settings <repo pssa.settings.psd1>   # idempotency, all 8 changed PowerShell files
Invoke-ScriptAnalyzer -Path <file> -Settings <repo pssa.settings.psd1>                             # all 8 changed files
Invoke-Pester (Run.Path = tests/scripts), plain New-PesterConfiguration, no coverage               # repo-wide, 414/416 passed (2 failures)
Invoke-PoshQCTest -SettingsPath <corrected runsettings, scripts/*.ps1 + scripts/*.psm1, 24 files>   # independently-regenerated repo-wide coverage, 416/416 passed
[xml] parse of artifacts/pester/powershell-coverage.xml (both the branch's own 2-file run and this audit's regenerated 24-file run)
grep -n "MapGet" src/OpenClaw.HostAdapter/Program.cs   # confirms /status and all other HostAdapter routes are root-level, no /v1 prefix anywhere
```

## Overall Policy Verdict

**FAIL — 1 Blocking finding.** The unit-test toolchain stage does not pass in a single, standard `Invoke-Pester` invocation: two new tests in `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` fail deterministically outside the specific MCP-wrapper invocation path the executor's evidence exclusively relied on, due to a nested-module-import visibility defect in the shared test fixture. All other gates (format, lint, coverage numerics, evidence location, change budget, file size, prohibited patterns, non-goal invariants) are PASS. Remediation required; see `remediation-inputs.2026-07-11T00-45.md`.
