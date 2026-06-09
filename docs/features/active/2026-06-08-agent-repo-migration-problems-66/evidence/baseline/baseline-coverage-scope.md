# Baseline — Coverage-Gate Scope Derivation (Issue #66, P0-T3)

Timestamp: 2026-06-08T20-00

Command (derivation): dot-source `.claude/hooks/validate-feature-review-coverage.ps1` (returns early when dot-sourced), then `Get-ChangedLanguageSet -Lines (Get-Content artifacts/pr_context.summary.txt)`.
Command (raw match inspection): `rg -nP '^\s*-\s+(\S+)\s+\(\+\d+/-\d+\)\s*$' artifacts/pr_context.summary.txt`

EXIT_CODE: 0

Output Summary:

- The gate's `Get-ChangedLanguageSet` parses lines of the form `- <path> (+N/-N)` from `artifacts/pr_context.summary.txt`. Twenty such lines match in the artifact.
- The ten lines whose extension maps to a language are all `.ps1` files under `.claude/hooks/`:
  1. `.claude/hooks/validate-feature-review-coverage.ps1 (+459/-0)`
  2. `.claude/hooks/enforce-pr-author-skill.ps1 (+444/-0)`
  3. `.claude/hooks/validate-executor-output.ps1 (+296/-0)`
  4. `.claude/hooks/validate-planner-output.ps1 (+288/-0)`
  5. `.claude/hooks/enforce-checkpoint-monotonic.ps1 (+245/-0)`
  6. `.claude/hooks/enforce-powershell-batch-budget.ps1 (+238/-0)`
  7. `.claude/hooks/validate-orchestrator-output.ps1 (+223/-0)`
  8. `.claude/hooks/enforce-prd-feature-before-planner.ps1 (+216/-0)`
  9. `.claude/hooks/enforce-promotion-mcp-only.ps1 (+216/-0)`
  10. `.claude/hooks/validate-required-artifact-output.ps1 (+171/-0)`
- The remaining ten matched lines are `.md` files (`.github/agents/*.agent.md`, `.github/instructions/*.instructions.md`, `docs/features/.../*.md`); the gate's extension switch has no case for `.md`, so they contribute no language.
- The full branch contains 15 newly-tracked `.claude/hooks/*.ps1` files; the "Changed files overview" in the summary truncates each category to its ten largest entries, so only ten of the fifteen hook `.ps1` lines appear in the parsed artifact. All parsed `.ps1` lines are under `.claude/hooks/`.
- Derived changed-language set (current, pre-remediation): `PowerShell` (Count = 1). No other language is enumerated.

Conclusion: pre-remediation, the machine gate enumerates `PowerShell` as the sole changed language solely because of the `.claude/hooks/*.ps1` lines, and therefore requires a PowerShell coverage PASS/FAIL verdict. This is the gate behavior that Finding 1 reports and that the P1-T1 `.claude/hooks/**` exclusion resolves.
