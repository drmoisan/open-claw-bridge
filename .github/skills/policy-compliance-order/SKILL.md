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

1) `.github/copilot-instructions.md`
2) `.github/instructions/general-code-change.instructions.md`
3) `.github/instructions/general-unit-test.instructions.md`
4) Language- or domain-specific policies based on files in scope:
   - Python: `.github/instructions/python-code-change.instructions.md`, `.github/instructions/python-unit-test.instructions.md`
   - PowerShell: `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`
   - GitHub Actions: `.github/instructions/github-actions.instructions.md` (for `.github/workflows/*`)
   - Any other `.github/instructions/*.instructions.md` relevant to touched paths

## Hard Constraints (Baseline)

- Do NOT modify policy documents under `.github/instructions/`.
- Do NOT create secrets or `.env` files unless explicitly requested.
- Prefer repo-defined tasks/commands when running checks.
- If information is missing, proceed with best-effort assumptions and document them.

## Extensions (Agent-Specific)

Agents may append additional requirements that are unique to their role (e.g., policy audit templates or epic docs), but should not duplicate the baseline order above.
