# Policy Compliance Audit — Issue #139 (deploy-wrapper-entry-point)

- Reviewed: 2026-07-10T16-30
- Reviewer: feature-review agent
- Base branch: `main` (merge-base `c72782357d1cb10848d53d92f0d5cde091cc8c92`)
- Head: `feature/deploy-wrapper-entry-point-139` @ `f6c1ca9`
- Work mode: `full-feature` (AC sources: `spec.md` and `user-story.md`, per the persisted `- Work Mode: full-feature` marker in `issue.md`)
- Scope: full branch diff, merge-base..HEAD, 24 files changed (1,356 insertions / 3 deletions) — `git diff --stat c72782357d1cb10848d53d92f0d5cde091cc8c92..HEAD`

## Rejected Scope Narrowing

None. The delegating prompt explicitly instructed the opposite of narrowing ("Determine scope yourself per your scope invariant... Run the full toolchain and coverage verification for every language that has changed files in the branch diff"). No caller language attempting to narrow scope, mark any language out of scope, or skip a toolchain/coverage check was present. The full merge-base..HEAD diff was used as the audit scope.

## Policy Reading Order Applied

1. `CLAUDE.md` — attempted read; file does not exist at the repository root in this worktree. No standing-instructions file present; recorded as checked/absent, consistent with the branch's own baseline evidence (`evidence/baseline/phase0-instructions-read.2026-07-10T15-02.md`) and prior review precedent (#137).
2. `.claude/rules/general-code-change.md` — read, applied below.
3. `.claude/rules/general-unit-test.md` — read, applied below.
4. `.claude/rules/powershell.md` — read, applied (only language with changed files in this branch: `scripts/Deploy.ps1`, `scripts/Publish.ps1`, `tests/scripts/Deploy.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`).
5. `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/csharp.md`, `.claude/rules/self-explanatory-code-commenting.md` (path-scoped to `**/*.py`) — reviewed for applicability; none apply. No C#, Python, or TypeScript files changed; no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` diffs; no `artifacts/orchestration/orchestrator-state.json` changes.

## Language Inventory and Coverage Verdicts

`git diff --stat c72782357d1cb10848d53d92f0d5cde091cc8c92..HEAD` confirms the only source language with changed files is **PowerShell**: `scripts/Deploy.ps1` (new), `scripts/Publish.ps1` (modified), `tests/scripts/Deploy.Tests.ps1` (new), `tests/scripts/Publish.Tests.ps1` (modified). All other changed files are Markdown documentation/evidence artifacts under `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/`. TypeScript, Python, and C# have zero changed files on this branch — their coverage sections are correctly N/A per the "zero changed files" exception, not narrowed.

### PowerShell — Coverage Verdict: PASS

Canonical artifact `artifacts/pester/powershell-coverage.xml` exists on disk (gitignored, JaCoCo format via the corrected-runsettings workaround for the known `run_poshqc_test` MCP defect — established on #111/#125/#135/#137). Independently re-parsed rather than trusting the branch's own prose:

- **Repo-wide (report-level `INSTRUCTION`/command-coverage counter, the established branch-coverage proxy in this repo since Pester's engine reports no separate branch metric):** `missed=207 covered=1850` → **89.94%**, 2,057 analyzed commands across 31 files. Matches the branch's own claim in `evidence/qa-gates/final-poshqc-test.2026-07-10T15-02.md` exactly.
- **Baseline (from `evidence/baseline/poshqc-test.2026-07-10T15-02.md`, pre-change, 30 files, `scripts/Deploy.ps1` not yet created):** 89.93%. Delta: **+0.01pp, no regression.**
- **New file `scripts/Deploy.ps1` (LINE counter, per-file, independently parsed):** `missed=4 covered=27` → **87.10%** line coverage. >= 85% floor: **PASS.** The 4 missed lines (99, 100, 117, 118) are the `$PSCmdlet.ShouldProcess(...)`/`return & ...` guard bodies inside the production `Invoke-PublishScript`/`Invoke-InstallScript` wrapper functions, which execute only when no test double is pre-registered; every `Deploy.Tests.ps1` test pre-registers a global override, matching the same accepted untested-guard-body pattern for `Test-IsElevatedAdmin` in `scripts/Install.ps1`.
- **Modified file `scripts/Publish.ps1` (LINE counter, per-file, independently parsed):** `missed=2 covered=77` → **97.47%** line coverage. >= 85% floor: **PASS.** Independently confirmed via per-line `<line nr="N" mi="M" ci="C">` entries that all three changed lines (221, 227, 245 — the `$null = ` assignments) show `mi=0 ci=1` (covered both before, per the P1-T7 evidence, and after). The two missed lines (160, 161) are the pre-existing, unrelated `Remove-Item` prior-bundle-cleanup branch (unchanged by this diff, confirmed via `git diff` hunk boundaries) — **no regression on changed lines.**
- **No production file excluded from measurement:** confirmed — 31 files measured (27 baseline + `scripts/Deploy.ps1`), `ExcludedPath` empty in the corrected runsettings.
- **Command-coverage branch-coverage proxy >= 75%:** PASS at all three levels (89.94% repo-wide, 87.10% `Deploy.ps1`, 97.47% `Publish.ps1`).

Independent verification method: `[xml]` parse of `artifacts/pester/powershell-coverage.xml`, cross-checking `//report/counter[@type='INSTRUCTION']` (repo-wide), `//class/counter[@type='LINE']` (per-file), and `//sourcefile/line[@nr]` (exact changed-line coverage) against the branch's `evidence/qa-gates/coverage-comparison.2026-07-10T15-02.md` prose. All numeric claims matched exactly; no discrepancy found.

### TypeScript — Coverage Verdict: N/A (zero changed files)
### Python — Coverage Verdict: N/A (zero changed files)
### C# — Coverage Verdict: N/A (zero changed files)

## Independent Toolchain Re-Verification (not solely trusting executor evidence)

MCP tools (`resolve_policy_audit_template_asset`, `validate_orchestration_artifacts`, `run_poshqc_*`) are not available in this review environment. Per established fallback (Review env fallbacks precedent), the underlying PoshQC assets were located directly on disk and invoked without the MCP wrapper:

| Gate | Command | Result |
|---|---|---|
| Format (idempotency) | `Invoke-Formatter -ScriptDefinition <content>` on `scripts/Deploy.ps1`, `scripts/Publish.ps1`, `tests/scripts/Deploy.Tests.ps1`, `tests/scripts/Publish.Tests.ps1` | **PASS** — all 4 files byte-identical before/after formatting; zero diffs. |
| Lint | `Invoke-ScriptAnalyzer -Path <file> -Settings pssa.settings.psd1` (bundled repo settings, one file at a time — batch `-Path` array is not supported by this PSScriptAnalyzer version) on the same 4 files | **PASS** — zero findings across all 4 files. |
| Type-check | N/A for PowerShell per `.claude/rules/powershell.md`. | N/A |
| Unit tests | `Invoke-Pester` (v5.6.1) over `tests/scripts/Publish.Msix.Tests.ps1`, `Publish.Env.Tests.ps1`, `Publish.Helpers.Tests.ps1`, `Publish.Helpers.CertThumbprint.Tests.ps1`, `Publish.Tests.ps1`, `Deploy.Tests.ps1` | **PASS** — 116/116 passed (107 Publish-family + 9 Deploy). |
| Unit tests (repo-wide) | `Invoke-Pester` over `tests/scripts` (all 26 `*.Tests.ps1` files) | **PASS** — 380/380 passed, 0 failed, 0 skipped. Reproduces the branch's own `evidence/qa-gates/final-poshqc-test.2026-07-10T15-02.md` claim exactly. |
| Architecture-boundary tests | N/A — no `.NET`/dependency-cruiser boundary in scope for this PowerShell-only change. | N/A |
| Contract/schema checks | N/A — no API contract or schema surface introduced. | N/A |
| Integration tests | N/A — the wrapper-function seam pattern isolates `Deploy.ps1`/`Publish.ps1` from live external process invocation in the unit-test suite; no separate integration-test tier exists for this tooling in the repo. | N/A |

All independently-run figures (116/116 targeted, 380/380 repo-wide, 0 analyzer findings, 0 format diffs, 89.94% repo-wide command coverage, 87.10%/97.47% per-file line coverage) match the branch's own recorded evidence exactly. No discrepancy was found between the executor's reported numbers and this reviewer's independent re-derivation.

## Evidence Location Compliance

- **Verdict: PASS.**
- `git diff --name-only c72782357d1cb10848d53d92f0d5cde091cc8c92..HEAD | grep -E '^artifacts/(baseline|baselines|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'` returns zero matches.
- All 15 evidence artifacts added by this branch live under the canonical path `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/evidence/{baseline,regression-testing,qa-gates,other,issue-updates}/`, consistent with the Evidence Location Invariant.
- `validate_evidence_locations.py` was not found anywhere in the repository tree (searched via `Glob`); fell back to the manual diff-scan method per established precedent. No violations found.
- The raw (non-evidence) coverage/test intermediates (`artifacts/pester/powershell-coverage.xml`, `powershell-coverage.koverage.xml`, `pester-junit.xml`) are gitignored, non-committed raw tool output — distinct from the evidence-artifact summaries under `<FEATURE>/evidence/`, consistent with the Evidence Location Invariant's scope (it governs evidence-summary artifacts, not raw gitignored tool output).
- No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries were needed; no caller instruction specified a non-canonical evidence path in this review's delegation prompt.

## Uniform Toolchain Gates (general-code-change.md, powershell.md)

| Gate | PowerShell | Evidence |
|---|---|---|
| Format | PASS (0 changes) | `evidence/qa-gates/final-poshqc-format.2026-07-10T15-02.md`; independently reproduced (`Invoke-Formatter` idempotency, this audit). |
| Lint/Analyze | PASS (0 findings on final recorded pass; one intermediate restart occurred and was resolved — see below) | `evidence/qa-gates/final-poshqc-analyze.2026-07-10T15-02.md`; independently reproduced (`Invoke-ScriptAnalyzer`, this audit). |
| Type-check | N/A (PowerShell) | — |
| Architecture boundaries | N/A (no `.NET` boundary changes touched by this branch) | — |
| Unit tests | PASS (380/380, 0 failed) | `evidence/qa-gates/final-poshqc-test.2026-07-10T15-02.md`; independently reproduced (`Invoke-Pester`, this audit). |
| Single clean pass, no restart pending | PASS — one restart occurred mid-Phase-2 loop (PSScriptAnalyzer flagged 3 issues in `Deploy.Tests.ps1`'s test-double stubs: 2x `PSUseOutputTypeCorrectly`, 1x `PSShouldProcess`); the fix (removed unneeded `SupportsShouldProcess` from stubs, added `[OutputType(...)]`) was followed by a full restart from format, and the recorded final artifacts are all from the subsequent clean re-run. | `evidence/qa-gates/phase2-poshqc-loop.2026-07-10T15-02.md` |

Restart-loop handling is compliant with `general-code-change.md`'s "restart from step 1 if any stage fails or auto-fixes files" requirement.

## Change Budget (powershell.md)

- Production PowerShell files changed: 2 (`scripts/Deploy.ps1` new, `scripts/Publish.ps1` modified). Within the direct-mode budget of "up to 2 production PowerShell files."
- Test files changed: 2 (`tests/scripts/Deploy.Tests.ps1` new, `tests/scripts/Publish.Tests.ps1` modified). Within the per-batch cap of "at most 3 production files and 3 test files."
- No third production module (e.g., a `Deploy.Helpers.psm1`) was introduced; the child-invocation seam is implemented as script-scope wrapper functions inside `Deploy.ps1` itself, matching the spec's explicit constraint.

## File Size Limit (general-code-change.md, 500-line cap)

- `scripts/Deploy.ps1`: 169 lines.
- `scripts/Publish.ps1`: 249 lines.
- `tests/scripts/Deploy.Tests.ps1`: 187 lines.
- `tests/scripts/Publish.Tests.ps1`: 480 lines.

All four files are under the 500-line cap; `Publish.Tests.ps1` is closest to the limit at 480 lines but remains compliant. No file exceeds the cap.

## Prohibited-Pattern Scan (powershell.md)

- `grep -nE "Invoke-Expression|password|secret|New-TemporaryFile|GetTempPath|\$env:TEMP" scripts/Deploy.ps1 tests/scripts/Deploy.Tests.ps1` — zero matches. No `Invoke-Expression`, no plaintext secrets, no temporary-file usage.
- `scripts/Deploy.ps1` forwards `-DockerEnvFilePath`/`-AnthropicEnvFilePath` as path strings only (confirmed via source read: no `Get-Content`, `Copy-Item`, or similar I/O against these parameters anywhere in the file) — consistent with the spec's explicit constraint that `Deploy.ps1` must not stage or read operator secret files.
- `Deploy.ps1` never calls `Set-Location`/`Push-Location` (confirmed via source read and the passing `'does not change the caller working directory (AC-2)'` test, independently reproduced in this audit's repo-wide Pester run).

## Coverage Exclusion Policy (general-unit-test.md)

- The corrected runsettings used for coverage measurement carry an empty `ExcludedPath`; all 31 production PowerShell files under `scripts/**` (including the new `scripts/Deploy.ps1`) are in the coverage denominator. No production file is excluded. **PASS.**

## Benchmark Baselines / CI Workflows / Orchestrator-State Rules

- Not applicable — no files under `scripts/benchmarks/**`, no `.github/workflows/**` diffs, and no `artifacts/orchestration/orchestrator-state.json` changes are present in this branch's diff (`git diff --name-only` confirms zero matches for all three path patterns).

## Quality Tiers (quality-tiers.md)

- `quality-tiers.yml` at repo root maps only `.csproj`/`.sln` projects (T1–T3 + test peers); PowerShell tooling under `scripts/**` is not a listed project and is not required to be (the file's own header scopes it to `OpenClaw.MailBridge.sln` projects). Per the tier examples in `.claude/rules/quality-tiers.md`, deployment/build tooling of this kind is exemplary T4 (Scaffolding).
- Uniform coverage thresholds (line >= 85%, branch >= 75%) apply regardless of tier and are met (see Coverage Verification above: 87.10%/97.47% per-file, 89.94% repo-wide, all above floor).
- T1/T2-specific gates (property-test density, mutation score) are not triggered: `Deploy.ps1` introduces no new pure function independent of I/O/process orchestration; its logic is parameter-forwarding and control flow around two wrapper functions, consistent with T3/T4-tier expectations, not a T1/T2 pure-function surface requiring property tests.

## Architecture Boundaries

- **Verdict: N/A / PASS.** No `.NET` project or dependency-cruiser boundary is touched. `scripts/Deploy.ps1` composes two existing, independent entry points (`Publish.ps1`, the staged `Install.ps1`) without introducing a new shared module; the spec's explicit constraint ("no third production module... implemented as wrapper functions defined at script scope inside Deploy.ps1 itself") is honored, confirmed via source read.

## Approved Exceptions / Documented Accommodations

1. **MCP tools unavailable.** `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. Review artifacts are structured to mirror the most recent validator-passing PowerShell-only artifact set (#137, `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/*.2026-07-10T15-10.md`) combined with known validator-quirk corrections (exact heading text, single-line per-language comparison format, `## Acceptance Criteria Check-off` lowercase spelling).
2. **`mcp__drm-copilot__run_poshqc_test` fails on every invocation in this repository** (pre-existing defect affecting F11 #111, F16 #125, #135, #137, reproduced again here): the bundled `pester.runsettings.psd1` hardcodes `drm-copilot`-repo-specific `CodeCoverage.Path` entries that do not exist in this repository. The branch's own corrected-runsettings workaround (repo-scoped `CodeCoverage.Path`) is the accepted substitute and was independently re-verified in this audit via a fresh `Invoke-Pester` run (without needing a corrected runsettings file, since this audit's re-run intentionally omitted `-CodeCoverage` and relied on the branch's already-committed coverage XML for numeric coverage verification instead).
3. **PR-context artifacts absent.** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` do not exist in this worktree despite the delegation prompt describing them as "refreshed." Per the `pr-context-artifacts` skill and established precedent (#119, #120, #124, #128), the caller's freshness claim was not trusted; this review instead used `git diff --stat`/`git diff` directly against the supplied merge-base SHA (`c72782357d1cb10848d53d92f0d5cde091cc8c92`) as the authoritative scope source, per the Scope Invariant's list of legitimate scope sources.

## Overall Policy Verdict

**PASS.** No blocking findings. Zero FAIL or PARTIAL results across coverage, toolchain, evidence location, change budget, file size, and prohibited-pattern checks. Three documented, precedent-consistent environment accommodations are recorded above; none affect the verdict.
