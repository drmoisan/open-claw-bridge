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

### Enforced Handoff Contract

- The delegated preflight prompt MUST include the exact directive `DIRECTIVE: PREFLIGHT VALIDATION ONLY`.
- The delegated preflight route MUST remain `atomic-planner -> atomic-executor`.
- `atomic-executor` MUST return exactly one of:
  - `PREFLIGHT: ALL CLEAR`
  - `PREFLIGHT: REVISIONS REQUIRED`
- If revisions are required, `atomic-executor` MUST provide a precise plan delta that can be applied to the same plan file.
- Continue the validate -> revise -> validate loop until `PREFLIGHT: ALL CLEAR`.
- Do not treat a partial summary as a valid substitute for the exact preflight signal.
- If the required `atomic-executor` handoff cannot be started or completed, stop and report blocked state; do not self-approve the plan.
- Before reporting completion, the target plan MUST pass `python -m scripts.dev_tools.validate_orchestration_artifacts plan <plan-path>`.

## Write Scope

Allowed writes:
- create or update the plan file,
- normalize an existing plan template into executor-compatible structure.

Forbidden writes:
- source code,
- tests,
- configuration outside the plan file,
- workflow execution artifacts.

## Authoritative Plan Path Rule

When a caller provides an explicit target plan path:

- update that exact file in place,
- reuse that same file for all preflight revision iterations,
- do not create additional sibling `plan.*.md` files during the same planning cycle.

## Determinism Gates

Reject and revise any plan that contains:
- bucket tasks,
- vague acceptance criteria,
- placeholder tokens,
- mixed discovery and implementation in one task,
- multi-outcome tasks that should be split.
