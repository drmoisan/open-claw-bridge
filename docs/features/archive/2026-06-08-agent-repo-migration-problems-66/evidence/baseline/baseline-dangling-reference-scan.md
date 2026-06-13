# Baseline — Dangling-Reference Scan (Issue #66 Scope Extension, pre-change)

Timestamp: 2026-06-08T11-07

Command:
`rg -n --no-ignore "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md|orchestrate-python-work|javascript-typescript-jest" .claude .github`

EXIT_CODE: 0

Output Summary (full hit list, file:line):
- `.claude/settings.json:33` Agent(python-typed-engineer) — to remove (P3-T1)
- `.claude/settings.json:36` Agent(typescript-engineer) — to remove (P3-T1)
- `.claude/settings.json:46` Skill(python-change-budget-router *) — to remove (P3-T2)
- `.claude/settings.json:47` Skill(python-qa-gate *) — to remove (P3-T2)
- `.claude/settings.json:48` Skill(invoke-python-engineer *) — to remove (P3-T2)
- `.claude/settings.json:93` check-python-test-purity.ps1 hook command — to remove (P3-T3)
- `.claude/settings.json:97` enforce-python-batch-budget.ps1 hook command — to remove (P3-T3)
- `.claude/settings.json:133` SubagentStop matcher contains python-typed-engineer, typescript-engineer — to remove (P3-T4)
- `.claude/skills/policy-compliance-order/SKILL.md:25,27` rules/python.md, rules/typescript.md — to remove (P4-T1)
- `.claude/skills/python-change-budget-router/SKILL.md` (multiple) — directory to delete (P2-T1)
- `.claude/skills/python-qa-gate/SKILL.md` (multiple) — directory to delete (P2-T2)
- `.claude/skills/invoke-python-engineer/SKILL.md` (multiple) — directory to delete (P2-T3)
- `.claude/skills/remediation-handoff-atomic-planner/SKILL.md:103` worker enum — to edit (P4-T2)
- `.claude/skills/translate-copilot-to-claude/SKILL.md:157,159,160,201,202,203,204,291` examples — to edit (P4-T3)
- `.claude/hooks/check-python-test-purity.ps1:132` — file to delete (P2-T6)
- `.github/prompts/orchestrate-python-work.prompt.md:25` — file to delete (P2-T4)

Pre-change reference set is non-empty as expected (settings.json, three shared skills, the Python
skills/prompts/hooks slated for deletion). Baseline for AC-13.
