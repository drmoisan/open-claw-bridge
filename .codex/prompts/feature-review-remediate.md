---
description: Run feature review and, if needed, remediation planning with executor preflight
---

$feature-review
Use pr_context as authoritative if present and valid. Only fall back to an explicit base branch if pr_context is missing, stale, or ambiguous.
If remediation is required, explicitly spawn atomic-planner to create or revise the remediation plan.
Before atomic-planner finalizes the plan, it must explicitly spawn atomic-executor in preflight-validation-only mode against that same file.
If atomic-executor returns PREFLIGHT: REVISIONS REQUIRED, atomic-planner must revise the same file and repeat until PREFLIGHT: ALL CLEAR.
Only then finalize the remediation plan and report all generated artifacts.