---
name: powershell-orchestration-state-machine
description: Checkpoint schema and resume protocol for long-running PowerShell orchestration workflows.
---

# PowerShell Orchestration State Machine

Canonical checkpoint and resume behavior for multi-step PowerShell orchestration workflows.

## Canonical Checkpoint Location

- `artifacts/orchestration/powershell-orchestrator-state.json`

## Required Fields

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

For short-path runs, also persist:
- `small_path_qc_summary`
- `small_path_audit_artifacts`
- `bootstrap_mode`
- `phase0_execution_summary`
- `resume_after_manual_bootstrap`

## Resume Rules

1. Read the checkpoint if it exists.
2. If incomplete, resume from `next_step`.
3. If missing or complete, start from intake.
4. If the user explicitly requests a restart, reset the checkpoint and start over.
