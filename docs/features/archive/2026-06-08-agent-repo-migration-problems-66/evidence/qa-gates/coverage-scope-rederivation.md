# QA Gate — Coverage-Scope Re-Derivation After Exclusion (Issue #66, P2-T1)

Timestamp: 2026-06-08T20-00

Command: dot-source `.claude/hooks/validate-feature-review-coverage.ps1` (the script returns early when `$MyInvocation.InvocationName -eq '.'`, so dot-sourcing imports the functions without running the gate), then `Get-ChangedLanguageSet -Lines (Get-Content artifacts/pr_context.summary.txt)`, capturing the returned changed-language set.

EXIT_CODE: 0

Output Summary:

- Returned changed-language set: `(empty)`, Count = 0.
- `Set.Contains('PowerShell')` = False.
- The set does NOT contain `PowerShell`. The only language-mapping changed-file lines in the parsed artifact were `.claude/hooks/*.ps1`, which are now excluded from the changed-language set by the `.claude/hooks/**` filter added in P1-T1. No non-hook changed PowerShell application files remain, so no PowerShell language is enumerated.
- No other language is enumerated either: the remaining matched lines are `.md` files, which the gate's extension switch does not map.

Comparison to baseline (P0-T3): pre-remediation the derived set was `[PowerShell]` (Count = 1); post-remediation it is `[]` (Count = 0). The machine gate now evaluates with the hooks excluded, so it raises no PowerShell coverage requirement.

Acceptance: the returned set does not contain `PowerShell` and contains no in-scope changed PowerShell application files — confirmed.
