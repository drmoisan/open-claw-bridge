---
name: policy-compliance-order
description: 'Repository policy compliance order and hard constraints. Use when an agent must read mandatory policy files, apply repo-wide constraints, or restate policy precedence without duplicating blocks across agents.'
---

# Policy Compliance Order

Shared policy-compliance instructions to avoid duplicating the same policy order across multiple agents.

## When to Use This Skill

Use this skill when:
- An agent must declare the repository’s mandatory policy reading order.
- You need to reiterate non-negotiable constraints (e.g., no policy edits, no secrets, no silent skips).
- Multiple agents share the same compliance preamble.

## Required Policy Reading Order (Baseline)

Claude Code auto-loads rules via path-scoped frontmatter in `.claude/rules/`. This ordering documents precedence when policies conflict:

1) `CLAUDE.md` (standing instructions, always loaded)
2) `.claude/rules/general-code-change.md` (cross-language code change policy)
3) `.claude/rules/general-unit-test.md` (cross-language unit test policy)
4) Language- or domain-specific rules based on files in scope:
   - Python: `.claude/rules/python.md`, `.claude/rules/python-suppressions.md`
   - PowerShell: `.claude/rules/powershell.md`
   - TypeScript: `.claude/rules/typescript.md`, `.claude/rules/typescript-suppressions.md`
   - C#: `.claude/rules/csharp.md`

## Hard Constraints (Baseline)

- Do NOT modify policy documents under `.claude/rules/` or `.github/instructions/`.
- Do NOT create secrets or `.env` files unless explicitly requested.
- Prefer repo-defined tasks/commands when running checks.
- If information is missing, proceed with best-effort assumptions and document them.

## Extensions (Agent-Specific)

Agents may append additional requirements that are unique to their role (e.g., policy audit templates or epic docs), but should not duplicate the baseline order above.

