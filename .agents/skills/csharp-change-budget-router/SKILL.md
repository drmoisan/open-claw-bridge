---
name: csharp-change-budget-router
description: 'Budget-first routing contract for C# work. Use when a C# request must be classified as small path or large path before planning or implementation.'
---

# C# Change Budget Router

Canonical routing rules for deciding whether C# work can stay on a direct path or must escalate to orchestration.

## Routing Rules

1. Estimate the number of production C# files likely to change.
2. Route:
   - `1-3` production files plus corresponding tests -> small path
   - more than `3` production files or more than `3` test files -> large path

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
- route the work to the orchestrated C# workflow.
