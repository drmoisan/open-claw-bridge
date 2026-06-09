# AC-01 / AC-05 No-Regression Confirmation (over now-tracked tree)

Timestamp: 2026-06-08T11-30

## AC-01 marker scan

Command:
`rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
EXIT_CODE: 0 (matches found; all permitted exceptions below)

Per-hit disposition (live-policy hits; agent-memory provenance hits excluded as permitted):

| File:Line | Term | Disposition |
|---|---|---|
| AGENTS.md:601 | xUnit | PERMITTED — explicit prohibition |
| .github/instructions/csharp-unit-test.instructions.md:24 | xUnit | PERMITTED — explicit prohibition |
| .claude/rules/architecture-boundaries.md:19 | Office.js, dependency-cruiser | PERMITTED — qualified not-present |
| .claude/rules/csharp.md:35 | Directory.Build.props | PERMITTED — qualified not-present |
| .claude/rules/csharp.md:65 | xUnit | PERMITTED — explicit prohibition |
| .claude/agent-memory/* | various | PERMITTED — agent-memory provenance |

The generic illustrative test-runner list ("Jest, Vitest, Pytest, ... XUnit, RSpec") at
`.github/instructions/github-actions-ci-cd-best-practices.instructions.md` L322 and `AGENTS.md` is a
PERMITTED EXCEPTION, not a defect (generic CI guidance; does not match the lowercase-x "xUnit" marker).

No NEW genuine residual appeared after un-ignoring the harness. The only genuine residual that was
present at extension start (`.github/agents/csharp-typed-engineer.agent.md:173-175`) is corrected in
Phase 5. AC-01 still holds.

## AC-05 removed-worker scan

Command:
`rg -n "python-typed-engineer|typescript-engineer" .claude/agents/orchestrator.md .github/agents/orchestrator.agent.md`
EXIT_CODE: 1
Result: no matches. No removed-worker delegation remains in either orchestrator. AC-05 still holds.

Output Summary: AC-01 and AC-05 hold over the now-tracked tree; no new genuine residual was
introduced by bringing the previously-gitignored harness under version control. PASS.
