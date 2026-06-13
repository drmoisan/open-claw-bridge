# AC-12 — Full-tree Residual Marker Scan (post-change)

Timestamp: 2026-06-08T11-26

Command:
`rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
EXIT_CODE: 0 (matches found; all classified as permitted exceptions below)

Targeted confirmation:
`rg -n "msbuild TaskMaster|vstest\.console" .github/agents/csharp-typed-engineer.agent.md`
EXIT_CODE: 1 — no match. The Phase 5 fix removed the genuine residual. PASS.

Per-hit disposition table:

| File:Line | Matched term | Disposition |
|---|---|---|
| AGENTS.md:601 | xUnit | PERMITTED — explicit prohibition ("Do not introduce xUnit or NUnit") |
| .claude/rules/csharp.md:35 | Directory.Build.props | PERMITTED — qualified not-present ("not currently present in this repository") |
| .claude/rules/csharp.md:65 | xUnit | PERMITTED — explicit prohibition ("Do not introduce xUnit or NUnit") |
| .claude/rules/architecture-boundaries.md:19 | Office.js, dependency-cruiser | PERMITTED — qualified not-present ("There is no ... in this repository") |
| .github/instructions/csharp-unit-test.instructions.md:24 | xUnit | PERMITTED — explicit prohibition |
| .claude/agent-memory/task-researcher/project_state_2026_06.md:15 | multiple | PERMITTED — agent-memory provenance (historical audit note) |
| .claude/agent-memory/prd-feature/project_harness_canonical_policy.md:12,13,14,17 | multiple | PERMITTED — agent-memory provenance (canonical-policy memo, "not X" form) |

Permitted-exception note: the generic illustrative test-runner list ("Jest, Vitest, Pytest, ...
XUnit, RSpec") at `.github/instructions/github-actions-ci-cd-best-practices.instructions.md` L322
and `AGENTS.md` uses casing "XUnit" and is a permitted exception (generic CI guidance); it does not
appear in this scan because the marker pattern uses lowercase-x "xUnit".

Output Summary: No genuine (unqualified) residual marker remains. Every hit is an explicit
prohibition, a qualified not-present statement, or agent-memory provenance. The previously-genuine
`.github/agents/csharp-typed-engineer.agent.md:173-175` block is corrected. AC-12 PASS.
