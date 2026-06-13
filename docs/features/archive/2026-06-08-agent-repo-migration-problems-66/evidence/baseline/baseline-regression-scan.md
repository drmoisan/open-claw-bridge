# Baseline — Regression / Dangling-Reference Scan (Issue #66, P0-T4)

Timestamp: 2026-06-08T20-00

Command 1 (AC-01/AC-12/AC-13 marker set):
`rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`

Command 2 (dangling worker references):
`rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|rules/python\.md|rules/typescript\.md" .claude .github`

EXIT_CODE: Command 1 = 0 (matches found, all permitted exceptions); Command 2 = 1 (no matches)

Output Summary:

Command 1 hit set — every hit is a permitted exception (negation/policy statement, historical agent-memory note, or correct repo-fact statement):

1. `AGENTS.md:601` — "Do not introduce xUnit or NUnit into existing test projects." (negation; permitted)
2. `.claude/agent-memory/task-researcher/project_state_2026_06.md:15` — historical issue-#66 migration catalogue listing residue markers as context (agent-memory provenance; permitted)
3. `.claude/rules/csharp.md:35` — note that `Directory.Build.props`/`Directory.Packages.props`/etc. are not present in this repo (correct repo-fact; permitted)
4. `.claude/rules/csharp.md:65` — "Do not introduce xUnit or NUnit." (negation; permitted)
5. `.claude/rules/architecture-boundaries.md:19` — "There is no TypeScript frontend, Office.js layer, or `dependency-cruiser` configuration in this repository." (correct repo-fact; permitted)
6. `.claude/agent-memory/prd-feature/project_harness_canonical_policy.md:12-14,17` — canonical-policy memory describing the corrected stack and the migration origin (agent-memory provenance; permitted)
7. `.github/instructions/csharp-unit-test.instructions.md:24` — "Do not introduce xUnit or NUnit into existing test projects." (negation; permitted)

Command 2 hit set: none (exit 1, no matches). No retained harness file references a removed Python/TypeScript worker, `python-qa-gate`, `invoke-python-engineer`, `rules/python.md`, or `rules/typescript.md`.

Conclusion: the no-regression baseline is that every marker hit is a permitted exception (negation, repo-fact, or agent-memory provenance) and there are zero dangling worker references. P2-T3 re-runs both scans and asserts the hit set is unchanged.
