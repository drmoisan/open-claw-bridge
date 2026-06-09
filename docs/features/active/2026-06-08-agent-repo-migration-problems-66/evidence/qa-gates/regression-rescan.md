# QA Gate — No-Regression Re-Scan (Issue #66, P2-T3)

Timestamp: 2026-06-08T20-00

Commands:
1. Threshold scan: `rg -n "85%|75%|>= 85|>= 75" .claude/rules/general-unit-test.md .claude/rules/quality-tiers.md .claude/rules/powershell.md AGENTS.md`
2. Marker scan (AC-01/AC-12/AC-13): `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
3. Dangling-worker scan: `rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|rules/python\.md|rules/typescript\.md" .claude .github`

EXIT_CODE: scan 1 = 0; scan 2 = 0; scan 3 = 1 (no matches)

Output Summary:

Threshold scan (no value changed vs P0 baseline):
- `general-unit-test.md:23-24` — line >= 85%, branch >= 75% (unchanged).
- `general-unit-test.md:29` — new P1-T2 clause, states the exclusion "does not lower the product-code thresholds: line coverage >= 85% and branch coverage >= 75% remain unchanged."
- `quality-tiers.md:16` — new P1-T3 clause, states the exclusion "does not lower the uniform line >= 85% / branch >= 75% thresholds."
- `quality-tiers.md:33-34,51` — uniform thresholds (unchanged).
- `powershell.md:63-64` — line >= 85%, branch >= 75% (unchanged).
- `AGENTS.md:348` — repo-wide line >= 85%, branch >= 75% (unchanged).
- No `80%`/`90%` threshold reappears. No numeric threshold value was lowered or removed; the only additions are scope-exclusion clauses that explicitly preserve the existing thresholds.

Marker scan: identical hit set to the P0-T4 baseline. Every hit is a permitted exception (negation statement, correct repo-fact, or agent-memory provenance). No new residual marker introduced.

Dangling-worker scan: no matches (exit 1), identical to the P0-T4 baseline. No retained harness file references a removed Python/TypeScript worker or removed `rules/python.md`/`rules/typescript.md`.

Acceptance: thresholds unchanged from baseline; every marker/dangling-reference hit remains within the permitted-exception set; no regression vs P0-T4. Confirms no product-code coverage threshold was lowered.
