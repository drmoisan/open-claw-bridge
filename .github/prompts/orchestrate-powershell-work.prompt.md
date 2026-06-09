---
agent: 'powershell-orchestrator'
description: 'Route a PowerShell request through budget-based orchestration: direct small-scope delivery or full feature/bug lifecycle with promotion, research, plan/execution, and review.'
---

# PowerShell Orchestration Prompt

Use this prompt to run an end-to-end PowerShell workflow through `powershell-orchestrator`.

## Objective

Coordinate the user request from intake to completion using the correct path based on rough change budget.

## Inputs

- **Request summary (required):** clear objective and expected outcome
- **Likely affected files (optional):** any known production/test files
- **Initial classification hint (optional):** `feature` or `bug`
- **Constraints (optional):** APIs/paths/behavior that must remain unchanged

## Required orchestration behavior

1. Estimate rough change budget first (production PowerShell files).
2. If budget is **1–2 production PowerShell files** (+ corresponding tests):
   - Delegate directly to `powershell-typed-engineer` for plan + implementation + QA closure.
3. If budget is **>2 production PowerShell files**:
   - Execute full large-path lifecycle:
     1) Scope and create potential entry (`feature` vs `bug`, short-name creation)
     2) Promote to GitHub issue and capture issue metadata
     3) Create branch and active feature folder
     4) Delegate research + spec/story completion
   5) Delegate plan creation to `powershell-atomic-planning` (which delegates to `atomic_planner`, then validates preflight via `atomic_executor` until `PREFLIGHT: ALL CLEAR`)
   6) After all-clear, delegate implementation + QA directly to `powershell_atomic_executor`
   7) Delegate post-implementation feature review

## Persistence and resume

- Maintain orchestration state in `artifacts/orchestration/powershell-orchestrator-state.json`.
- Resume from the next incomplete step if interrupted.
- Do not end early while required downstream steps remain.

## Output expectations

On completion, report:

- Selected route (`small` or `large`)
- Branch name (if created)
- Key captured variables (`promotion-type`, `short-name`, `issue-num`, `feature-folder` when applicable)
- Created/updated artifact paths
- Final readiness summary and any remaining action items
