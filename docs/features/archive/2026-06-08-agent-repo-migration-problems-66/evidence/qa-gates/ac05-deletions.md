# AC-05 — Deletions and Worker References (Issue #66)

Timestamp: 2026-06-08T09-49
Command: `Test-Path` for each removed file; `rg -n "python-typed-engineer|typescript-engineer" .claude/agents/orchestrator.md .github/agents/orchestrator.agent.md`
EXIT_CODE: 0

Output Summary: AC-05 PASS.

All eighteen removed TypeScript/Python harness files return Test-Path = False:
- `.claude/rules`: typescript.md, typescript-suppressions.md, python.md, python-suppressions.md (4).
- `.claude/agents`: typescript-engineer.md, python-typed-engineer.md (2).
- `.github/agents` REMOVE-list: expert-nextjs-developer, expert-react-frontend-engineer,
  pytest-unit-test-coding, python-atomic-executor, python-atomic-planning, python-execution-only-typed,
  python-orchestrator, python-typed-engineer, typescript-engineer, tdd-red, tdd-green, tdd-refactor (12).

(The plan/spec AC-05 text references "eight `.claude/` files and eleven `.github/agents/*`"; the
authoritative enumerated REMOVE set is the eighteen files above, all confirmed absent. The seven
REVIEW-classified `.github/agents/*` files — beast-mode set, hlbpa, mentor, commentary-remediation —
remain present and out of scope per spec.)

Worker references: `rg` for `python-typed-engineer|typescript-engineer` in
`.claude/agents/orchestrator.md` and `.github/agents/orchestrator.agent.md` returns no matches
(EXIT 1). No retained harness file references a removed worker.
