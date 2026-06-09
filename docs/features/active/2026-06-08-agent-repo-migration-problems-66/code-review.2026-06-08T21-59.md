# Code Review: Issue #66 — Agent Harness Migration Correction (Cycle-1 Remediation Re-Review)

**Review Date:** 2026-06-08
**Reviewer:** feature-review agent
**Feature Folder:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
**Feature Folder Selection Rule:** Suffix `-66` matches the issue number on branch `bug/agent-repo-migration-problems-66`; it is the only active folder with material scoping-doc changes in the branch diff.
**Base Branch:** `development` (merge-base `72d11879918bab20652abf2965eea42f17ab67d1`)
**Head Branch:** `bug/agent-repo-migration-problems-66` @ `613564ce90df9a21faf9038f7597252cfd52304f`
**Review Type:** Post-remediation re-review (cycle-1)

---

## Executive Summary

This is the cycle-1 remediation re-review of the Issue #66 agent-harness correction. The substantive harness change (residual-marker removal, tool-name reconciliation to MSTest/Moq/FluentAssertions, COM-confinement, path qualification, creation of `quality-tiers.yml`, `docs/ci.research.md`, and the orchestrator memory file, and the Python/TypeScript ecosystem removal with version-control inclusion) was reviewed in cycle-1 and remains intact. The cycle-1 review raised one blocking finding: PowerShell coverage FAIL on the 15 `.claude/hooks/*.ps1` files that entered the branch diff when `.gitignore` was edited to track `.claude/`.

The remediation under review (head `613564c`) resolves that finding through a documented coverage-scope exclusion of `.claude/hooks/**` as T4 agent-harness scaffolding. The code change is narrow: a single path-filter hunk in `Get-ChangedLanguageSet` within `.claude/hooks/validate-feature-review-coverage.ps1`, accompanied by documentation clauses in `.claude/rules/general-unit-test.md`, `.claude/rules/quality-tiers.md`, and `.claude/skills/feature-review-workflow/SKILL.md`. No coverage threshold value was changed.

**What changed:**
The remediation delta relative to the cycle-1 head (`3ed46ef`) is the `.claude/hooks/**` coverage-scope exclusion: `Get-ChangedLanguageSet` (`.claude/hooks/validate-feature-review-coverage.ps1` L129–135) normalizes each changed-file path and `continue`s past any path matching `(^|/)\.claude/hooks/` before mapping the file extension to a language. The two `.claude/rules/*` files and the review SKILL record the rationale (T4 scaffolding, consistent with excluding `tests/` and dev `scripts/`). The full branch diff (merge-base -> `613564c`) is 211 paths: 209 additions, 2 modifications (`.gitignore`, `AGENTS.md`); 192 Markdown, 15 PowerShell, 2 JSON, 1 YAML, 1 `.gitignore`.

**Top 3 risks:**
1. The exclusion is policy-based rather than test-based: the 15 hooks remain untested by an in-repo Pester suite. This is an accepted T4 trade-off (Option B), but the harness hooks carry runtime behavior that is exercised only by Claude Code at runtime, not by CI. Residual risk is bounded by the hooks being scaffolding, not product code.
2. The exclusion filter in `Get-ChangedLanguageSet` uses a path regex (`(^|/)\.claude/hooks/`); a future hook relocated outside `.claude/hooks/` would fall back into the application coverage surface. This is the intended behavior and was verified (a non-hook `scripts/*.ps1` still maps to PowerShell), but it depends on the hook directory convention remaining stable.
3. `AGENTS.md` and its source `.github/instructions/*` are hand-edited in parallel because the generator script is absent; drift between them is a documented follow-up risk, not introduced by this remediation.

**PR readiness recommendation:** **Go** — the prior blocking finding is resolved by a documented, gate-enforced exclusion that did not lower any threshold; PoshQC format/analyze are clean on the edited hook; all 15 acceptance criteria re-verify PASS.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Info | `.claude/hooks/validate-feature-review-coverage.ps1` | L129–135 (`Get-ChangedLanguageSet`) | The remediation adds a path filter excluding `.claude/hooks/**` from changed-language derivation, with an explanatory comment citing the two rule files. Logic is correct and scoped to hooks only. | None required. | Resolves the cycle-1 PowerShell coverage blocker without altering thresholds; a non-hook `scripts/*.ps1` still maps to PowerShell, confirming the filter does not over-exclude. | `evidence/qa-gates/coverage-scope-rederivation.md` (derived set empty), live inspection of L129–135 |
| Info | `.claude/rules/general-unit-test.md` | L29 | New coverage-scope clause classifying `.claude/hooks/**` as T4 scaffolding excluded from the application coverage surface; explicitly states thresholds (85%/75%) are unchanged. | None required. | Records the exclusion in the canonical policy source so the gate behavior is policy-backed. | Live inspection; `evidence/qa-gates/regression-rescan.md` confirms no threshold value changed |
| Info | `.claude/rules/quality-tiers.md` | L16 | T4 examples extended to include `.claude/hooks/**` agent-harness tooling, cross-referencing the general-unit-test clause. | None required. | Keeps the T1–T4 classification and the exclusion consistent across both rule files. | Live inspection |
| Info | `.github/agents/csharp-typed-engineer.agent.md` | full file (tracked) | The cycle-1 OUT-OF-SCOPE finding (L173–175 `msbuild TaskMaster.sln` / `vstest.console.exe`) is resolved on the now-tracked file; AC-06 and the AC-01 marker scan return zero `msbuild TaskMaster` / `vstest.console` hits across `.github`. | None required. | Confirms the version-control inclusion (Option 1A) closed the previously-gitignored residual. | `rg` AC-06 scan returns 0 matches |
| Info | `.claude/hooks/*.ps1` (15 files) | repo-wide | Harness hooks are under the 500-line limit (largest: `validate-feature-review-coverage.ps1`, 466 lines) and pass PoshQC analyze with zero findings on the edited hook. | None required. | Confirms structure/lint hygiene on the only edited hook. | `evidence/qa-gates/poshqc-analyze.md`, `evidence/qa-gates/poshqc-format.md` |

No Blocker or Major findings.

---

## Implementation Audit

### PowerShell implementation audit

#### What changed well

- The remediation is the minimum change that resolves the blocker: a single `continue` path-filter in `Get-ChangedLanguageSet`, not a rewrite. It preserves all existing extension-to-language mapping behavior and adds an explanatory comment that cites the two authoritative rule files, so a future reader can trace the rationale.
- The filter normalizes backslashes to forward slashes (`$normalizedPath = $path -replace '\\', '/'`) before matching `(^|/)\.claude/hooks/`, so it behaves identically for Windows- and POSIX-style path separators in the PR-context summary.
- The exclusion is scoped to hooks only. The re-derivation evidence confirms a non-hook `scripts/*.ps1` path still maps to PowerShell, so the change does not silently drop application PowerShell from coverage enforcement.

#### API and safety notes

- `Get-ChangedLanguageSet` retains its `[OutputType([System.Collections.Hashtable])]` contract and `[string[]]$Lines` parameter; the edit adds only a local variable (`$normalizedPath`) and a guard `continue`. No parameter, output shape, or side-effect change.
- The script preserves its dot-source guard (`if ($MyInvocation.InvocationName -eq '.')` at L456), which allows the test/evidence step to import the function without executing the gate. This is the mechanism the re-derivation evidence relied on.

#### Error handling and logging

- The added hunk introduces no new failure path; it short-circuits a loop iteration. No error-handling or logging change is warranted, and none was made. Consistent with the existing function style.

### C# implementation audit (not applicable)

- Zero C# files changed in the branch diff. The C#-facing corrections (command strings, tool names) are documentation edits in `AGENTS.md` and `.github/instructions/*`, reviewed under the feature audit, not C# source.

---

## Test Quality Audit

This change adds no application code and no new application tests. The verification evidence reviewed is the remediation QA-gate set under the canonical evidence path.

### Reviewed test and QA artifacts

- `evidence/qa-gates/coverage-scope-rederivation.md` — verifies that, after the exclusion, `Get-ChangedLanguageSet` over the current `artifacts/pr_context.summary.txt` returns an empty changed-language set (Count = 0, `Contains('PowerShell') = False`), and that a non-hook `scripts/*.ps1` still maps to PowerShell. Confirms the exclusion is effective at the machine gate, not cosmetic.
- `evidence/qa-gates/poshqc-format.md` — PoshQC format on `.claude/hooks` reports `ok`; the edited hook required no reformatting beyond the intended exclusion hunk.
- `evidence/qa-gates/poshqc-analyze.md` — PoshQC analyze on `.claude/hooks` reports `ok` with zero findings on the edited hook; no new PSScriptAnalyzer debt.
- `evidence/qa-gates/regression-rescan.md` — confirms no product-code coverage threshold value changed (line >= 85% / branch >= 75% intact at all locations) and that the AC-01/AC-12/AC-13 marker and dangling-worker scans are identical to the P0 baseline.
- `evidence/qa-gates/coverage-scope-resolution.md` — consolidates the resolution (Option B: documented exclusion, not added tests) and cites each prerequisite artifact.

### Quality assessment prompts

- **Determinism:** The re-derivation reads a static PR-context summary and a dot-sourced function; no network, clock, or RNG dependency. Deterministic.
- **Isolation:** The exclusion hunk targets a single behavior (path filtering in language derivation); the evidence verifies that behavior in isolation.
- **Speed:** Not measured; the gate function is a single-pass string scan over the PR-context summary lines.
- **Diagnostics:** A future regression in the filter would surface as a non-empty derived language set in the re-derivation evidence, which clearly identifies the failing behavior.

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | ✅ PASS | The exclusion hunk and documentation clauses introduce no credentials or tokens; the harness change is policy/config text and a path filter. |
| No unsafe subprocess or command construction | ✅ PASS | The edited hook adds a `-replace`/`-match`/`continue`; no `Invoke-Expression`, no executable invocation, no dynamic command construction. |
| Input validation at boundaries | ✅ PASS | `Get-ChangedLanguageSet` already validates each line against a strict regex (`^\s*-\s+(\S+)\s+\(\+\d+/-\d+\)\s*$`) before processing; the new filter operates on the captured path only. |
| Error handling remains explicit | ✅ PASS | No error-handling change; the hunk short-circuits a loop iteration without suppressing any error. |
| Configuration / path handling is safe | ✅ PASS | Path normalization (`\` -> `/`) then anchored regex `(^|/)\.claude/hooks/`; no path traversal or wildcard-expansion risk. |

---

## Research Log

No external research was required. All findings are grounded in the branch diff, the live tree (`.claude/hooks/validate-feature-review-coverage.ps1`, `.claude/rules/general-unit-test.md`, `.claude/rules/quality-tiers.md`, `.claude/skills/feature-review-workflow/SKILL.md`), the PR-context summary, and the remediation QA-gate evidence under the canonical feature-folder evidence path.

---

## Verdict

The cycle-1 remediation is ready for normal PR flow. The single prior blocking finding — PowerShell coverage FAIL on the 15 `.claude/hooks/*.ps1` files — is resolved by a documented `.claude/hooks/**` T4-scaffolding coverage-scope exclusion that is enforced at the machine gate (`Get-ChangedLanguageSet` filter, verified by an empty re-derived language set), recorded in two canonical rule files and the review SKILL, and accompanied by clean PoshQC format and analyze on the edited hook. No coverage threshold value was lowered, and the filter is scoped to hooks only so application PowerShell remains under coverage enforcement.

This conclusion is consistent with the Findings Table (no Blocker or Major findings) and the PR readiness recommendation (Go). No remediation is required for this cycle.
