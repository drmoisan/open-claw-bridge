---
name: atomic-planner
description: 'Generate deterministic phased implementation plans with atomic [P#-T#] checkbox tasks. Use when Codex needs to turn a goal, issue, or remediation input into an executor-ready plan.'
---

# Atomic Planner

Planning-only workflow skill for generating executor-ready plans.

## Required Shared Skills

Always apply:
- `policy-compliance-order`
- `atomic-plan-contract`

## Role

- Produce a phased plan that an executor can run without replanning.
- Write or update plan files only when explicitly asked or when a calling workflow provides the authoritative target path.
- Do not implement the plan.

## Plan Requirements

Use `atomic-plan-contract` as the source of truth for:
- phase and task formatting,
- Phase 0 requirements,
- baseline evidence tasks,
- final QA requirements,
- preflight validation loop behavior.

Each task must be:
- atomic,
- binary in completion,
- independently verifiable,
- free of placeholder text.

## Mandatory Preflight Loop

Before a plan is finalized:

1. Hand the current plan to `atomic-executor` in preflight-validation-only mode.
2. If preflight returns `PREFLIGHT: REVISIONS REQUIRED`, revise the same file.
3. Repeat until preflight returns `PREFLIGHT: ALL CLEAR`.

Do not finalize a plan before preflight clears it.

## Write Scope

Allowed writes:
- create or update the plan file,
- normalize an existing plan template into executor-compatible structure.

Forbidden writes:
- source code,
- tests,
- configuration outside the plan file,
- workflow execution artifacts.

## Determinism Gates

Reject and revise any plan that contains:
- bucket tasks,
- vague acceptance criteria,
- placeholder tokens,
- mixed discovery and implementation in one task,
- multi-outcome tasks that should be split.
