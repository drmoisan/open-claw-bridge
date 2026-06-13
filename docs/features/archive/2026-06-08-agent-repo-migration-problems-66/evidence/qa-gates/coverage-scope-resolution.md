# QA Gate — Coverage-Scope Exclusion Resolution Summary (Issue #66, P2-T4)

Timestamp: 2026-06-08T20-00

Command: consolidation of prerequisite evidence artifacts (P0-T2, P0-T3, P2-T1, P2-T3); no new command executed.

EXIT_CODE: 0

Output Summary:

(a) Baseline state — `evidence/baseline/baseline-powershell-hook-coverage.md` (P0-T2): the 15 newly-tracked `.claude/hooks/*.ps1` files had 0% recorded line coverage; the Pester coverage artifact recorded only 5 of them (all `covered="0"`), and 10 of 15 changed hooks had no coverage entry at all. The pre-remediation gate scope — `evidence/baseline/baseline-coverage-scope.md` (P0-T3) — derived a changed-language set of `[PowerShell]` (Count = 1) solely from the `.claude/hooks/*.ps1` lines, which required a PowerShell coverage PASS/FAIL verdict and produced the blocking FAIL.

(b) Resolution — Option B: a documented `.claude/hooks/**` T4-scaffolding coverage-scope exclusion, not added coverage. No Pester hook tests were authored (Option A rejected). The machine gate now skips any changed-file path matching `.claude/hooks/` before mapping extensions to languages (P1-T1 edit in `Get-ChangedLanguageSet`).

(c) Machine gate re-derivation — `evidence/qa-gates/coverage-scope-rederivation.md` (P2-T1): after the change, `Get-ChangedLanguageSet` over the current `artifacts/pr_context.summary.txt` returns an empty set (Count = 0), `Contains('PowerShell') = False`. The gate no longer enumerates PowerShell from the hook diff. Existing behavior is preserved: a non-hook `scripts/*.ps1` still maps to PowerShell.

(d) Policy record: the exclusion is documented in the canonical policy files and the review skill:
- `.claude/rules/general-unit-test.md` (line 29, P1-T2) — coverage-scope clause.
- `.claude/rules/quality-tiers.md` (line 16, P1-T3) — T4 scaffolding classification.
- `.claude/skills/feature-review-workflow/SKILL.md` (line 117, P1-T4) — agent-judgment-layer coverage-scope text, cross-referencing both rule files and preserving the Scope-Invariant for in-scope application languages.
- Operator Option B authorizes the two `.claude/rules/*` edits as an explicit override of the policy-compliance-order no-edit baseline (recorded in `evidence/other/phase0-instructions-read.md`).

(e) No-regression — `evidence/qa-gates/regression-rescan.md` (P2-T3): no product-code coverage threshold was changed (line >= 85% / branch >= 75% remain at all locations); the AC-01/AC-12/AC-13 marker scan hit set and the dangling-worker scan (no matches) are identical to the P0-T4 baseline.

Post-change scope: contains no in-scope changed PowerShell application files. The exclusion is effective at the machine gate (empty derived set), not cosmetic.

Acceptance: the summary cites each prerequisite evidence artifact by path and confirms the exclusion is effective at the gate.
