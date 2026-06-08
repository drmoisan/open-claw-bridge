# Baseline Delete Inventory (Issue #66)

## Scope Extension cycle (Option 1A) — pre-deletion inventory (seven targets)

Timestamp: 2026-06-08T11-09
Command: `Test-Path` for each of the seven extension delete targets (three Python skill dirs, two Python/TS prompts, two Python hooks)
EXIT_CODE: 0

Output Summary: all seven targets present (True) before deletion:
- `.claude/skills/python-change-budget-router` = True
- `.claude/skills/python-qa-gate` = True
- `.claude/skills/invoke-python-engineer` = True
- `.github/prompts/orchestrate-python-work.prompt.md` = True
- `.github/prompts/javascript-typescript-jest.prompt.md` = True
- `.claude/hooks/check-python-test-purity.ps1` = True
- `.claude/hooks/enforce-python-batch-budget.ps1` = True

Baseline for AC-15.

---

## Original (prior plan) inventory — retained for provenance

Timestamp: 2026-06-08T09-20
Command: `Test-Path` for each of the eighteen delete-target files (four `.claude/rules`, two `.claude/agents`, twelve `.github/agents/*` REMOVE-list)
EXIT_CODE: 0

Output Summary: All eighteen target files present (True) before deletion:

`.claude/rules`:
- `.claude/rules/typescript.md` = True
- `.claude/rules/typescript-suppressions.md` = True
- `.claude/rules/python.md` = True
- `.claude/rules/python-suppressions.md` = True

`.claude/agents`:
- `.claude/agents/typescript-engineer.md` = True
- `.claude/agents/python-typed-engineer.md` = True

`.github/agents/*` REMOVE-list:
- `expert-nextjs-developer.agent.md` = True
- `expert-react-frontend-engineer.agent.md` = True
- `pytest-unit-test-coding.agent.md` = True
- `python-atomic-executor.agent.md` = True
- `python-atomic-planning.agent.md` = True
- `python-execution-only-typed.agent.md` = True
- `python-orchestrator.agent.md` = True
- `python-typed-engineer.agent.md` = True
- `typescript-engineer.agent.md` = True
- `tdd-red.agent.md` = True
- `tdd-green.agent.md` = True
- `tdd-refactor.agent.md` = True

Note: The plan's P0-T5 text says "seventeen" but enumerates eighteen delete targets (four rules + two agents + twelve github-agents = eighteen, matching P1-T1..T18). All eighteen confirmed True.
