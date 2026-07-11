# Policy Compliance Audit — Issue #142 (installer-docker-images-not-bundled)

- Reviewed: 2026-07-10T20-01
- Reviewer: feature-review agent
- Base branch: `main` (merge-base `ca53297a558cd0fd8d3f13e8994d2637bef6740a`, committed 2026-07-10T18:18:44-04:00)
- Head: `bug/installer-docker-images-not-bundled-142` @ `79c6a3b21fbd9e59344b33e1b8b99b2295be790d`
- Work mode: `full-bug` (AC source: `spec.md` only, per the persisted `- Work Mode: full-bug` marker in `issue.md`)
- Scope: full branch diff, merge-base..HEAD, 40 files changed (2,163 insertions / 48 deletions) — `git diff --stat ca53297a558cd0fd8d3f13e8994d2637bef6740a..79c6a3b21fbd9e59344b33e1b8b99b2295be790d`

## Rejected Scope Narrowing

None. The delegating prompt explicitly instructed the opposite of narrowing ("Determine scope yourself per your scope invariant; do not narrow it"). No caller language attempting to narrow scope, mark any language out of scope, or skip a toolchain/coverage check was present. The full merge-base..HEAD diff was used as the audit scope.

Observation (noise, not narrowing): the PR-context summary's author-asserted autoclose list contains non-closing tokens (`#111`, `#125`, `#135`, `#137`, `#139`, `#24`, `#ISO-8601`, `#SHA-256`) parsed from precedent citations and spec prose. Only issue #142 is fixed and closable by this branch. This recurring parser noise was noted and ignored per established precedent; it does not affect scope.

## Policy Reading Order Applied

1. `CLAUDE.md` — checked; file does not exist at the repository root in this worktree. Recorded as checked/absent, consistent with prior review precedent (#137, #139); this repo state persists across reviews.
2. `.claude/rules/general-code-change.md` — read, applied below.
3. `.claude/rules/general-unit-test.md` — read, applied below.
4. `.claude/rules/powershell.md` — read, applied (only source language with changed files on this branch).
5. `.claude/rules/quality-tiers.md`, `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/tonality.md` — reviewed for applicability. No C#, Python, or TypeScript files changed; no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` diffs (`git diff --name-only` confirms zero matches); no `artifacts/orchestration/orchestrator-state.json` changes.

## Language Inventory and Coverage Verdicts

`git diff --name-status ca53297..79c6a3b` confirms the only source language with changed files is **PowerShell**: 5 production files (`scripts/Publish.Docker.psm1` new, `scripts/Install.Docker.psm1` new, `scripts/Publish.ps1` modified, `scripts/Publish.Helpers.psm1` modified, `scripts/Install.ps1` modified) and 7 test files (`tests/scripts/Publish.Docker.Tests.ps1` new, `tests/scripts/Install.Docker.Tests.ps1` new, `tests/scripts/Install.DockerStage.Tests.ps1` new, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Tests.ps1` modified). All other changed files are Markdown documentation/evidence artifacts under `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/` and `.claude/agent-memory/` (agent memory notes, not policy documents). TypeScript, Python, and C# have zero changed files on this branch — their coverage sections are correctly N/A per the "zero changed files" exception, not narrowed.

### PowerShell — Coverage Verdict: PASS

Canonical artifact `artifacts/pester/powershell-coverage.xml` exists on disk (JaCoCo format, generated 2026-07-10 19:48 via the corrected-runsettings workaround for the known `run_poshqc_test` MCP defect — established on #111/#125/#135/#137/#139) and includes both new modules. Independently re-parsed with an `[xml]` scratch parser rather than trusting the branch's own prose:

- PowerShell per-language comparison: Baseline: 89.34% -> Post-change: 89.91%, Change: +0.57%, New/changed-code coverage: 98.02% line `Publish.Docker.psm1` / 87.50% line `Install.Docker.psm1` / 97.56% line `Publish.ps1` / 96.70% line `Publish.Helpers.psm1` / 88.57% line `Install.ps1`, Disposition: PASS, Evidence: `artifacts/pester/powershell-coverage.xml` independently re-parsed; `evidence/qa-gates/coverage-comparison.2026-07-10T19-10.md`.
- **Repo-wide (report-level `INSTRUCTION`/command-coverage counter, the established branch-coverage proxy in this repo since Pester's engine reports no separate branch metric):** `covered=1658 missed=186` → **89.91%**, 1,844 analyzed commands across 24 files. Report-level `LINE` counter: 90.33%. Matches the branch's own claim in `evidence/qa-gates/final-poshqc-test.2026-07-10T19-10.md` exactly.
- **Baseline (from `evidence/baseline/poshqc-test.2026-07-10T19-10.md`, pre-change, 22 files, before the two new modules existed):** 89.34%. Delta: **+0.57pp, no regression.** Baseline freshness check: the baseline was captured after the merge-base commit (merge-base committed 18:18:44-04:00; baseline evidence timestamped 19-10), so the baseline reflects the merge-base state.
- **Per-file (LINE and INSTRUCTION counters, independently parsed; uniform gates line >= 85%, command-proxy >= 75%):**

| File | Line | Instruction (command proxy) | >= 85% line | >= 75% branch proxy |
|---|---|---|---|---|
| `scripts/Publish.Docker.psm1` (new) | 99/101 = **98.02%** | 119/121 = 98.35% | PASS | PASS |
| `scripts/Install.Docker.psm1` (new) | 14/16 = **87.50%** | 23/25 = 92.00% | PASS | PASS |
| `scripts/Publish.ps1` (modified) | 80/82 = **97.56%** | 97/99 = 97.98% | PASS | PASS |
| `scripts/Publish.Helpers.psm1` (modified) | 88/91 = **96.70%** | 105/108 = 97.22% | PASS | PASS |
| `scripts/Install.ps1` (modified) | 155/175 = **88.57%** | 179/209 = 85.65% | PASS | PASS |

  Every figure matches the executor's `evidence/qa-gates/coverage-comparison.2026-07-10T19-10.md` to the hundredth.
- **Changed-line coverage (per-line `<line nr="N" mi="M" ci="C">` entries, independently parsed for every added executable line in the three modified files):** all added executable lines are covered (`mi=0`) with one exception — `scripts/Install.ps1:113` (`mi=2 ci=0`), the `catch` throw arm for a failed `Import-Module` of the new docker module. This new line exactly replicates the pre-existing uncovered pattern: the two sibling import-failure catch arms for the helper and preflight modules (lines 99 and 106) are equally `mi=2 ci=0` at head. Per the established shifted-pattern precedent, a changed-region gap that exactly replicates a pre-existing accepted pattern in the same file, in a file whose whole-file figures pass all gates, is graded **Minor (non-gating)** — recorded as finding CR-1 in the code review. `scripts/Publish.ps1` added lines 104, 213, 214, 247 and `scripts/Publish.Helpers.psm1` added lines 156, 189 are all `mi=0`; **no regression on changed lines.**
- **Uncovered-by-design seam bodies:** the only uncovered lines in the two new modules are the `Invoke-DockerExe` seam bodies (`& docker @DockerArgs 2>&1` plus the result call) — the thinnest possible host-bound wiring; all result-shaping logic was extracted into the pure, unit-tested `ConvertTo-DockerExeResult`. The seam lines remain in the coverage denominator (visible cost), consistent with the Coverage Exclusion Policy's refactor-not-exclude requirement.
- **No production file excluded from measurement:** confirmed — 24 files measured (22 baseline + 2 new modules); the corrected runsettings uses `CodeCoverage.Path = @('scripts/*.ps1','scripts/*.psm1')` with empty `ExcludedPath`.

Independent verification method: `[xml]` parse of `artifacts/pester/powershell-coverage.xml`, cross-checking `//report/counter[@type='INSTRUCTION']` (repo-wide), `//class/counter[@type='LINE']` (per-file), and `//sourcefile/line[@nr]` (exact changed-line coverage, added-line numbers derived from `git diff` hunk parsing) against the branch's `evidence/qa-gates/coverage-comparison.2026-07-10T19-10.md` prose. All numeric claims matched exactly; the single uncovered changed line (Install.ps1:113) is disclosed above and graded Minor.

### TypeScript — Coverage Verdict: N/A (zero changed files)
### Python — Coverage Verdict: N/A (zero changed files)
### C# — Coverage Verdict: N/A (zero changed files)

## Independent Toolchain Re-Verification (not solely trusting executor evidence)

The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment, nor are the `run_poshqc_*` tools. Per established fallback (#135/#137/#139 precedent), the checks were re-run directly with locally installed `pwsh`/PSScriptAnalyzer/Pester:

| Gate | Command | Result |
|---|---|---|
| Format (idempotency) | `Invoke-Formatter -ScriptDefinition <content>` on all 12 changed PowerShell files (5 production + 7 test) | **PASS** — all 12 files byte-identical before/after formatting; zero diffs. |
| Lint | `Invoke-ScriptAnalyzer -Path <file>` (default rules, one file at a time per the known batched-array limitation) on the same 12 files | **PASS** — zero findings across all 12 files. |
| Type-check | N/A for PowerShell per `.claude/rules/powershell.md`. | N/A |
| Unit tests (repo-wide) | `Invoke-Pester` (v5.x) over `tests/scripts` (all `*.Tests.ps1`) | **PASS** — 406/406 passed, 0 failed, 0 skipped, in 26.02s. Reproduces the branch's `evidence/qa-gates/final-poshqc-test.2026-07-10T19-10.md` claim exactly (406 passed at head vs 380 at baseline; +26 = the new/relocated docker suites). |
| Coverage | Independent `[xml]` re-parse of the fresh `artifacts/pester/powershell-coverage.xml` (2026-07-10 19:48) | **PASS** — see Coverage Verdict above; all executor figures reproduced exactly. |
| Architecture-boundary tests | N/A — no .NET/dependency-cruiser boundary touched by this PowerShell-only change. | N/A |
| Contract/schema checks | N/A — `manifest.json` entry shape unchanged (`path`, `size`, `sha256`); `size` widens `[int]` -> `[long]`, and the reader `Test-ManifestIntegrity` already parses `[long]` (spec Root Cause Analysis; `scripts/Install.Helpers.psm1` unchanged in this diff). | N/A |
| Integration tests | Not run in this environment (requires Docker Desktop + full publish/install cycle). The spec's manual integration retest (`Publish.ps1` -> `Install.ps1` on a machine without `src/` in the bundle) is a rollout step, not a per-commit gate. Marked UNVERIFIED with this concrete reason; unit/stage-sequence evidence covers the wiring deterministically. | UNVERIFIED (environment) |

All independently-run figures (406/406 repo-wide tests, 0 analyzer findings, 0 format diffs, 89.91% repo-wide command coverage, all five per-file figures) match the branch's own recorded evidence exactly. No discrepancy was found between the executor's reported numbers and this reviewer's independent re-derivation.

## Evidence Location Compliance

- **Verdict: PASS.**
- `git diff --name-only ca53297..79c6a3b | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'` returns zero matches.
- All 19 evidence artifacts added by this branch live under the canonical path `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/evidence/{baseline,qa-gates,regression-testing,issue-updates,other}/`, consistent with the Evidence Location Invariant.
- `validate_evidence_locations.py` does not exist anywhere in this repository tree (established on prior reviews); fell back to the manual diff-scan method per precedent. No violations found.
- The raw coverage/test intermediates (`artifacts/pester/powershell-coverage.xml`, `powershell-coverage.koverage.xml`, `pester-junit.xml`) are gitignored, non-committed raw tool output — outside the invariant's scope (it governs committed evidence-summary artifacts).
- No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries were needed; no caller instruction specified a non-canonical evidence path.

## Uniform Toolchain Gates (general-code-change.md, powershell.md)

| Gate | PowerShell | Evidence |
|---|---|---|
| Format | PASS (0 changes on final pass) | `evidence/qa-gates/final-poshqc-format.2026-07-10T19-10.md`; independently reproduced (`Invoke-Formatter` idempotency on all 12 files, this audit). |
| Lint/Analyze | PASS (0 findings on final recorded pass; intermediate findings occurred and were resolved with loop restarts — see below) | `evidence/qa-gates/final-poshqc-analyze.2026-07-10T19-10.md`; independently reproduced (`Invoke-ScriptAnalyzer`, this audit). |
| Type-check | N/A (PowerShell) | — |
| Architecture boundaries | N/A (no .NET boundary changes) | — |
| Unit tests | PASS (406/406, 0 failed) | `evidence/qa-gates/final-poshqc-test.2026-07-10T19-10.md`; independently reproduced (`Invoke-Pester`, this audit). |
| Single clean pass, no restart pending | PASS — intermediate analyzer findings during the phase loops (`PSReviewUnusedParameter`, `PSUseDeclaredVarsMoreThanAssignments` in Phase 1 tests; `PSUseBOMForUnicodeEncodedFile` on a Phase 4 test em-dash) were fixed and followed by full restarts; the recorded final artifacts are from the subsequent clean re-run after the Phase 5 coverage refactor (extraction of `ConvertTo-DockerExeResult`). | `evidence/qa-gates/phase1..4-poshqc-loop.2026-07-10T19-10.md`, `final-*` artifacts |

Restart-loop handling is compliant with `general-code-change.md`'s "restart from step 1 if any stage fails or auto-fixes files" requirement.

## Change Budget (powershell.md)

- Production PowerShell files changed: 5 (2 new modules + 3 modified scripts). This exceeds the direct-mode budget of 2, and the spec explicitly routed execution through `powershell-orchestrator` ("Change budget: more than 2 production PowerShell files change, so execution routes through `powershell-orchestrator`... batched at <= 3 production files"). The plan's phase structure (publish-side batch: `Publish.Docker.psm1` + `Publish.ps1` + `Publish.Helpers.psm1`; install-side batch: `Install.Docker.psm1` + `Install.ps1`) respects the per-batch cap of 3 production files. **Compliant.**
- Test files changed: 7 (3 new + 4 modified), batched across phases per the plan. Compliant with per-batch caps as planned.

## File Size Limit (general-code-change.md, 500-line cap)

- `scripts/Publish.Docker.psm1`: 373 lines. `scripts/Install.Docker.psm1`: 103 lines. `scripts/Publish.Helpers.psm1`: 357 lines. `scripts/Install.ps1`: 455 lines. `scripts/Publish.ps1`: 257 lines.
- `tests/scripts/Publish.Docker.Tests.ps1`: 278. `tests/scripts/Install.DockerStage.Tests.ps1`: 202. `tests/scripts/Install.Docker.Tests.ps1`: 95.
- All changed files are under the 500-line cap. The spec's boundary constraint was honored: no new function was added to the already-over-cap `scripts/Install.Helpers.psm1` (527 lines, byte-identical in this diff), and all new logic lives in the two new sub-500-line modules.

## Prohibited-Pattern Scan (powershell.md, general-unit-test.md)

- No `Invoke-Expression`, no plaintext secrets, no hard-coded credentials in any changed file (source read of all 5 production files).
- No temp-file usage in any of the three new test files: the branch's own `evidence/qa-gates/seam-hermeticity-checks.2026-07-10T19-10.md` grep (`function global:docker|New-TemporaryFile|GetTempPath|$env:TEMP`) returned zero matches; independently confirmed by direct read of all three files in this audit. Tests mock only the `Invoke-DockerExe` seam (module-scoped `Mock -ModuleName`), never the `docker` executable, with exact mock signature parity (`param([string[]]$DockerArgs)`).
- Both new modules use `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, advanced functions with `CmdletBinding()`, mandatory-parameter validation (`ValidatePattern` on `-Version`, `ValidateSet` on `-Kind`), and `SupportsShouldProcess` on every state-changing function (`Build-OpenClawDockerImage`, `Save-OpenClawDockerImage`, `Write-BundleCompose`, `Invoke-PublishDockerStage`, `Invoke-DockerImageLoad`) — verified by source read and by the passing `-WhatIf` tests.
- The seam parameter is named `DockerArgs`, not `Args`, per the design-seam rule.

## Coverage Exclusion Policy (general-unit-test.md)

- The corrected runsettings used for coverage measurement carries an empty `ExcludedPath`; all 24 production PowerShell files under `scripts/**` (including both new modules) are in the coverage denominator. No production file is excluded. The untestable `& docker` seam lines were handled by the policy-required refactor route (logic extracted to pure `ConvertTo-DockerExeResult`; thin wiring left visible in the metric), not by exclusion. **PASS.**

## Benchmark Baselines / CI Workflows / Orchestrator-State Rules

- Not applicable — no files under `scripts/benchmarks/**`, no `.github/workflows/**` or `.github/actions/**` diffs, and no `artifacts/orchestration/orchestrator-state.json` changes (`git diff --name-only` confirms zero matches for all patterns). The `modified-workflow-needs-green-run` policy rule is not triggered.

## Quality Tiers (quality-tiers.md)

- `quality-tiers.yml` at repo root maps only `.csproj`/`.sln` projects; PowerShell tooling under `scripts/**` is not a listed project and is not required to be (precedent: #139 audit). Publish/install tooling of this kind is exemplary T4 (Scaffolding).
- Uniform coverage thresholds (line >= 85%, branch proxy >= 75%) apply regardless of tier and are met at repo-wide and per-file levels (see Coverage Verdict).
- T1/T2-specific gates (property-test density, mutation score) are not triggered for T4 tooling. The one new pure function (`Convert-ComposeToBundleCompose`) nonetheless has directed positive, negative, and real-file invariant tests.

## Approved Exceptions / Documented Accommodations

1. **MCP tools not available in this review environment.** The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. Review artifacts are structured to mirror the most recent validator-passing artifact set (#139, `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/*.2026-07-10T16-30.md`) combined with known validator-quirk corrections (single-line per-language comparison with explicit `Baseline:`/`Post-change:`/`Change:` tokens; `## Acceptance Criteria Check-off` lowercase spelling).
2. **`mcp__drm-copilot__run_poshqc_test` fails on every invocation in this repository** (pre-existing workspace defect affecting #111, #125, #135, #137, #139, reproduced again by the executor here): the bundled `pester.runsettings.psd1` hardcodes drm-copilot-specific `CodeCoverage.Path` entries absent from this repository. The branch's corrected-runsettings workaround (repo-scoped `CodeCoverage.Path`, empty `ExcludedPath`) is the accepted substitute; this audit independently re-verified via a plain repo-wide `Invoke-Pester` run (406/406) plus direct XML parsing of the committed-on-disk coverage report rather than regenerating coverage.
3. **PR-context artifacts present and fresh.** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` were verified fresh before use (head SHA `79c6a3b` matches the current branch head exactly; generated 2026-07-10 23:53 UTC). No regeneration was needed. The summary's "Changed files overview" categorized this PowerShell branch correctly (no docs-only misclassification), consistent with the prior PowerShell-only branches.

## 5. Test Coverage Detail

See the PowerShell Coverage Verdict section above for the complete per-file, repo-wide, and changed-line detail, the baseline comparison, and the independent re-parse methodology. Summary: repo-wide 89.91% command coverage (baseline 89.34%, +0.57pp), all five changed/new production files above the 85%/75% uniform gates, one Minor non-gating changed-line gap (`Install.ps1:113`, pattern-replicating), no production file excluded.

## 7. Code Quality Checks

See `code-review.2026-07-10T20-01.md` (same folder) for the full code-quality review: 0 Blocking, 0 Major, 1 Minor, 3 Info findings. Format, lint, and test gates independently re-verified clean in this audit (table above).

## Appendix A: Test Inventory

New and updated tests delivered by this branch (all independently re-run in the 406/406 repo-wide pass):

| Suite | Scope | Coverage |
|---|---|---|
| `tests/scripts/Publish.Docker.Tests.ps1` (new, 15 Its) | `Convert-ComposeToBundleCompose` positive/real-file drift-guard/2 negative drift throws; exact core/agent `docker build` and `docker save` argument vectors via seam mock; non-zero-exit throws (build, save); `Resolve-OpenClawAgentBaseImage` present/absent/blank; `Invoke-PublishDockerStage` ordering (`build,build,save` + compose write) and `-WhatIf` zero-invocation; `ConvertTo-DockerExeResult` shaping + null output |
| `tests/scripts/Install.Docker.Tests.ps1` (new, 6 Its) | `Invoke-DockerImageLoad` exact `load -i <path>` vector; missing-tar throw naming path + re-publish remediation; non-zero-exit throw; `-WhatIf` zero-invocation; `ConvertTo-DockerExeResult` shaping + null output |
| `tests/scripts/Install.DockerStage.Tests.ps1` (new, 7 Its) | Stage-sequence harness: tar path exactness; load after `Copy-BundleContents` and before `Invoke-ComposeUp`; load on `-Force` reinstall; relocated `-SkipDocker` trio plus new `-SkipDocker` skips-load assertion |
| `tests/scripts/Publish.Helpers.Tests.ps1` (updated) | dev-compose no-copy case; `[long]` size case with 3,000,000,000 (> `[int]::MaxValue`); 5-file staging order including `Install.Docker.psm1` |
| `tests/scripts/Publish.Tests.ps1` (updated) | Stage-order assertion includes `Invoke-PublishDockerStage` between `Copy-DockerArtifact` and `Invoke-LayoutAssembly` |
| `tests/scripts/Install.Tests.ps1` / `Install.Force.Tests.ps1` (updated) | `Invoke-DockerImageLoad` registered in stage-sequence mock sets and helper-order expectation |

Fail-before regression evidence (both independently reviewed): `evidence/regression-testing/ps-expect-fail-manifest-size.2026-07-10T19-10.md` (pre-fix `[int]` cast throws `OverflowException` on 3,000,000,000) and `evidence/regression-testing/ps-expect-fail-install-load.2026-07-10T19-10.md` (pre-wiring Install.ps1 fails all 3 image-load-stage Its).

## Appendix B: Command Reference

Commands executed by this review (all check-only; no mutation of source or policy files):

```
git diff --stat ca53297a558cd0fd8d3f13e8994d2637bef6740a..79c6a3b21fbd9e59344b33e1b8b99b2295be790d
git diff --name-status ca53297..79c6a3b
git diff ca53297..79c6a3b -- scripts/Install.ps1 scripts/Publish.ps1 scripts/Publish.Helpers.psm1
git diff ca53297..79c6a3b -- docker-compose.yml docker-compose.dev.yml deploy/docker scripts/Install.Helpers.psm1   # empty (non-goal invariants)
git diff --name-only ca53297..79c6a3b | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'   # zero matches
git diff --name-only ca53297..79c6a3b | grep -E '^(\.github/workflows/|scripts/benchmarks/|\.github/actions/)'   # zero matches
git show -s --format=%cI ca53297   # merge-base commit time (baseline freshness check)
Invoke-Formatter -ScriptDefinition <content>          # idempotency, all 12 changed PowerShell files
Invoke-ScriptAnalyzer -Path <file>                    # one file at a time, all 12 changed files
Invoke-Pester (Run.Path = tests/scripts)              # repo-wide, 406/406 passed
[xml] parse of artifacts/pester/powershell-coverage.xml   # repo-wide INSTRUCTION, per-class LINE, per-line nr/mi/ci
```

## Overall Policy Verdict

**PASS.** No blocking findings. Zero FAIL or PARTIAL results across coverage, toolchain, evidence location, change budget, file size, prohibited-pattern, and non-goal-invariant checks. One Minor non-gating coverage note (`Install.ps1:113`, pattern-replicating uncovered catch arm) and three documented, precedent-consistent environment accommodations are recorded above; none affect the verdict.
