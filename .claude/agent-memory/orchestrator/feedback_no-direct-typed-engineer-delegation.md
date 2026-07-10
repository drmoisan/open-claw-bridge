---
name: no-direct-typed-engineer-delegation
description: Orchestrator must route ALL implementation through atomic-planner/atomic-executor/feature-review, never delegate directly to a typed engineer, even for a tiny direct-mode-budget fix.
metadata:
  type: feedback
---

The orchestrator MUST NOT delegate implementation directly to a typed engineer (`powershell-typed-engineer`, `python-typed-engineer`, `csharp-typed-engineer`, `typescript-engineer`). Even a 2-line, small-path fix goes through the full orchestrate lifecycle: promotion (potential entry -> issue -> feature folder via MCP) -> minimal-audit plan via `atomic-planner` -> preflight + execution via `atomic-executor` -> `feature-review` -> `pr-author`. Typed engineers are invoked BY `atomic-executor`, never by the orchestrator directly.

**Why:** User caught me delegating a confirmed 2-file PowerShell `.env` bug fix straight to `powershell-typed-engineer`, skipping promotion, planning, preflight, model-routing preflight, and checkpoint persistence. The `orchestrate/SKILL.md` Delegation Model lists only atomic-planner/atomic-executor/feature-review/task-researcher as the orchestrator's delegate set; the small route in `config/orchestration-routing.json` names exactly those three required_agents. The remediation protocol calls direct typed-engineer invocation "prohibited," and the same discipline holds on the normal path.

**How to apply:** The "direct-mode / 1-2 production file" budget in `.claude/rules/powershell.md` is a SCOPE CAP on the worker, NOT a mode that lets the orchestrator bypass its lifecycle. Do not conflate a task being framed as "direct-mode budget" or a typed-engineer's self-description with permission to skip planner/executor/review. Before the first delegation, also run the model-routing preflight (complexity_assessments[], model_routing_receipts[], model_routing_preflight) and start a FRESH checkpoint when the objective is new (do not carry a stale DONE checkpoint from a prior objective). Related: [[checkpoint-validator-contract]], [[surface-consequential-decisions]].
