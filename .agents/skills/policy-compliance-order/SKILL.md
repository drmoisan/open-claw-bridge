---
name: policy-compliance-order
description: 'Repository policy reading order and hard constraints. Use when an agent or skill must apply repository policy without duplicating the same preamble everywhere.'
---

# Policy Compliance Order

Shared policy-compliance instructions for repository workflows.

## Required Reading Order

1. `.github/copilot-instructions.md`
2. `.github/instructions/general-code-change.instructions.md`
3. `.github/instructions/general-unit-test.instructions.md`
4. Relevant language or domain policies based on files in scope

## Hard Constraints

- Do not modify policy documents under `.github/instructions/` unless explicitly asked.
- Do not create secrets or `.env` files unless explicitly requested.
- Prefer repository-defined commands when available.
- If information is missing, proceed with best-effort assumptions and document them.
