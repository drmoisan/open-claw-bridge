---
description: Route a request through budget-based orchestration and persist until the selected delivery path is complete
argument-hint: Provide objective, likely files, feature-or-bug hint, constraints, and whether small-path execution should stop after Phase 0 for manual bootstrap
---

Spawn `orchestrator` to coordinate the current request from intake through completion.

Inputs to provide or infer:
- request summary and expected outcome
- likely affected production and test files, when known
- initial classification hint: `feature` or `bug`, when known
- constraints, preserved APIs, or forbidden changes
- whether small-path execution should stop after Phase 0 for manual bootstrap

Required behavior:
- estimate change budget first and choose the correct small or large path
- maintain and resume from the canonical orchestration checkpoint
- use migrated Codex subagents when available
- route host-specific lifecycle automation through the shared adapter rules
- continue until planning, execution, validation, and review are complete for the selected path, unless the request explicitly requires a manual-bootstrap pause

On completion, report the selected route, branch, key variables, `plan-path` when applicable, checkpoint path, created or updated artifact paths, and final readiness summary.
