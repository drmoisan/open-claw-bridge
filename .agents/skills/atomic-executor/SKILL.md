---
name: atomic-executor
description: 'Execute an atomic-planner plan exactly as written. Use when a plan with [P#-T#] tasks already exists and Codex must carry it out task-by-task without replanning.'
---

# Atomic Executor

Execution-only workflow skill for running an approved atomic plan.

## Required Shared Skills

Always apply:
- `policy-compliance-order`
- `atomic-plan-contract`
- `acceptance-criteria-tracking`

## Role

- Execute the plan of record exactly as written.
- Preserve phase headings, task IDs, checkbox state, and task order.
- Verify each task before checking it off.
- Do not create or revise the plan after execution starts.

## Preflight Rules

Before executing the first unchecked task:

1. Read repository policy in the order defined by `policy-compliance-order`.
2. Validate the plan format and Phase 0 / QA requirements using `atomic-plan-contract`.
3. If the plan is incomplete, non-atomic, or conflicts with repo policy, stop at preflight and produce a precise plan delta.

Blocking is allowed only during preflight.

### Validation-Only Mode Contract

If the incoming handoff includes the exact directive `DIRECTIVE: PREFLIGHT VALIDATION ONLY`:

1. Perform preflight validation only.
2. Do not execute tasks, establish execution state, or run implementation commands.
3. Return exactly one of:
   - `PREFLIGHT: ALL CLEAR`
   - `PREFLIGHT: REVISIONS REQUIRED`
4. If revisions are required, include a precise plan delta that `atomic-planner` can apply to the same plan file.
5. If revisions are required, automatically hand off back to `atomic-planner` and request that it apply the delta to the same plan file and resubmit that file for validation-only again.
6. Continue the validate -> delta -> planner-revise -> validate loop until preflight can return `PREFLIGHT: ALL CLEAR`.

## Execution Rules

For each task:

1. Announce the exact task ID being executed.
2. Perform only the micro-actions needed for that task.
3. Verify the task acceptance criteria before marking it complete.
4. Check off the plan file on disk immediately after verification passes.
5. Check off satisfied acceptance criteria in the authoritative requirement source file per `acceptance-criteria-tracking`.

## Non-Negotiable Constraints

- Do not invent new phases or tasks.
- Do not reorder tasks.
- Do not substitute an in-session todo list for the plan file.
- Treat the plan file on disk as the only authoritative checklist.
- Do not claim success without verification.
- After execution starts, do not stop mid-plan for replanning.

## Resume Rule

On resume:
- reload the plan of record,
- find the next unchecked task,
- continue from there without replanning.
