---
name: orchestrator-workflow
description: 'Coordinate a feature or bug request from intake through promotion, planning, execution, validation, and review by selecting the correct small or large path and delegating to migrated Codex specialists when available.'
---

# Orchestrator Workflow

Top-level delivery orchestration workflow for Codex.

## Required Shared Skills

Always apply:
- `policy-compliance-order`
- `feature-promotion-lifecycle`
- `repo-automation-adapter`
- `atomic-plan-contract`
- `acceptance-criteria-tracking`

Use as needed:
- `csharp-change-budget-router`
- `powershell-change-budget-router`
- `feature-review`
- `pr-base-branch-merge-base`
- `pr-context-artifacts`

## Role

- Coordinate the mission from intake through completion.
- Prefer migrated Codex subagents when they exist.
- Current preferred migrated subagents:
  - `atomic-planner`
  - `atomic-executor`
  - `feature-reviewer`
- If a specialist step has no migrated Codex subagent yet, perform that step directly while still following the governing shared skills.

## Checkpoint Contract

Canonical checkpoint path:
- `artifacts/orchestration/orchestrator-state.json`

Persist and reuse these fields exactly:
- `objective`
- `change_budget_estimate`
- `path_selected`
- `promotion-type`
- `short-name`
- `relativeFile`
- `long-name`
- `issue-num`
- `feature-folder`
- `work-mode`
- `plan-path`
- `completed_steps`
- `next_step`
- `last_updated`

For small-path runs, also persist:
- `bootstrap_mode`
- `phase0_execution_summary`
- `small_path_qc_summary`
- `small_path_audit_artifacts`
- `resume_after_manual_bootstrap`

## Resume Rules

1. Read the checkpoint first when it exists.
2. If the recorded mission is incomplete, resume from `next_step`.
3. Restart only when the user explicitly requests restart.
4. Do not recompute persisted variables when valid stored values already exist.

## Routing Rules

1. Estimate the likely touched production files and test files first.
2. Determine the dominant implementation language.
3. If the scope is primarily C#, use `csharp-change-budget-router`.
4. If the scope is primarily PowerShell, use `powershell-change-budget-router`.
5. If the scope is mixed-language, ambiguous, or unsupported by an existing change-budget router, fail closed to the large path.
6. Treat any request outside the applicable small-path budget as large path.

## Small Path

Use the small path only when the applicable language router clears it.

Required behavior:

1. Set `${work-mode}` to `minor-audit`.
2. Use `feature-promotion-lifecycle` as the source of truth for lifecycle variables, branch naming, and `${plan-path}` resolution.
3. Route all promotion, issue, and feature-folder automation through `repo-automation-adapter`.
4. Enforce minor-audit folder integrity:
   - `${feature-folder}/issue.md` must exist
   - `${feature-folder}/spec.md` must be absent
   - `${feature-folder}/user-story.md` must be absent
5. Spawn `atomic-planner` to create or revise the minimal plan at `${plan-path}`.
   - Include the directive `DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED`
   - Require the same `${plan-path}` to be updated in place
   - Do not continue until the planner reports `PREFLIGHT: ALL CLEAR`
6. Spawn `atomic-executor` to execute Phase 0 only.
7. If the request is manual bootstrap, persist the resume checkpoint and stop after Phase 0.
8. Otherwise continue with constrained implementation:
   - prefer a migrated language specialist when one exists
   - if no migrated specialist exists yet, execute the constrained implementation directly while staying within the approved plan and applicable repo policy
9. Validate the delivered work against `${feature-folder}/issue.md` and persist plan or acceptance-criteria checkoffs before review.
   - prefer `atomic-executor` for validation and checklist updates
10. Run reduced audit:
   - prefer `feature-reviewer`
   - otherwise execute the `feature-review` workflow directly in minor-audit mode
11. If review triggers remediation, create remediation inputs, delegate planning to `atomic-planner`, execute the remediation plan, and re-run reduced review until the gate is clean.

## Large Path

Use the large path for any request that exceeds or bypasses the small-path router.

Required behavior:

1. Set `${work-mode}` to:
   - `full-feature` for feature work
   - `full-bug` for bug work
2. Use `feature-promotion-lifecycle` as the source of truth for lifecycle variables, branch naming, and `${plan-path}` resolution.
3. Route all promotion, issue, and feature-folder automation through `repo-automation-adapter`.
4. Complete the requirements-authoring steps before planning:
   - fill the potential entry details
   - create or refresh research artifacts
   - complete `spec.md` and `user-story.md` when the selected work mode requires them
5. Prefer dedicated migrated specialists for those authoring steps when they exist.
6. When those specialists are not yet migrated, perform the authoring steps directly without changing template headings.
7. Spawn `atomic-planner` to finalize `${plan-path}` and require `PREFLIGHT: ALL CLEAR`.
8. Spawn `atomic-executor` to execute the approved plan.
9. Spawn `feature-reviewer` for post-implementation review when available.
10. If review triggers remediation, loop through remediation planning, remediation execution, and re-review until the gate is clean.

## Completion Gates

Do not claim mission completion until all of the following are true:

- the selected path completed end to end
- the checkpoint is updated with the final state
- `${feature-folder}` and `${plan-path}` are known when lifecycle setup was required
- required review artifacts exist on disk
- small path has Phase 0 evidence plus reduced audit artifacts
- large path has policy, code, and feature audit artifacts

## Hard Constraints

- Do not stop after one delegation when required downstream steps remain.
- Do not call `drmCopilotExtension.*` directly from this workflow.
- Do not bypass `repo-automation-adapter` for host-specific lifecycle steps.
- Do not create replacement audit artifacts yourself when `feature-reviewer` is available to own that workflow.
- Do not claim completion without reporting the checkpoint path and the created or updated artifact paths.
