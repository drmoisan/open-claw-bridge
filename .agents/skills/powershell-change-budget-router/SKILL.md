---
name: powershell-change-budget-router
description: 'Budget-first routing contract for PowerShell work. Use when a PowerShell request must be classified as small path or large path before planning or implementation.'
---

# PowerShell Change Budget Router

Canonical routing rules for deciding whether PowerShell work can stay on a direct path or must escalate to orchestration.

## Routing Rules

1. Estimate the number of production PowerShell files likely to change.
2. Route:
   - `1-2` production files plus corresponding tests -> small path
   - more than `2` production files -> large path

## Small-Path Requirements

Even for small path, the workflow must still:
- follow `feature-promotion-lifecycle`,
- use `repo-automation-adapter` for any host-specific repo automation,
- create a `minor-audit` plan through `atomic-planner`,
- require `atomic-executor` preflight,
- execute Phase 0 before branching into implementation,
- run reduced audit and QA at the end.

Direct implementation does not replace the orchestration lifecycle.

## Direct-Mode Rejection Rule

If direct implementation is requested for work estimated above the small-path budget:
- stop before implementation,
- route the work to the orchestrated PowerShell workflow.
