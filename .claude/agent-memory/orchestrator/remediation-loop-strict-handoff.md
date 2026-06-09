---
name: remediation-loop-strict-handoff
description: Strict delegation chain the orchestrator must follow inside a remediation cycle — workers are invoked only by atomic-executor; never bypass atomic-planner/atomic-executor
metadata:
  type: feedback
---

Inside a remediation cycle, follow the strict delegation chain and never call a worker subagent directly.

**Rule:** While a remediation cycle is active, the only allowed delegates are exactly three subagents:
`atomic-planner` (authors and revises `remediation-plan.<entry-ts>.md`), `atomic-executor` (clears
preflight, then executes the plan task-by-task), and `feature-review` (produces the three reaudit
artifacts at cycle exit). Direct invocation of a typed-engineer worker (for example
`csharp-typed-engineer` or `powershell-typed-engineer`) is prohibited. Workers are invoked by
`atomic-executor` only, as a consequence of executing the approved plan. The orchestrator must not
bypass `atomic-planner` or `atomic-executor` by passing a free-form fix prompt to a worker.

The full chain is: orchestrator -> atomic-planner -> atomic-executor (preflight) -> atomic-planner
(revise loop) -> atomic-executor (execute) -> feature-review.

**Why:** A direct worker call skips the plan-of-record and the preflight gate, which is how scope
creep and unverified fixes enter a remediation cycle. The plan file is the single source of truth for
the cycle; the preflight handoff between `atomic-planner` and `atomic-executor` is a sub-state of the
cycle (`preflight.final_status in {clear, changes_requested, pending}`), and execution may begin only
after `final_status == clear`.

**How to apply:** When an audit produces FAIL or material PARTIAL findings, when required toolchain
checks fail, when acceptance criteria are unmet, or when a required CI check fails after the PR is
open, enter the cycle by authoring `remediation-inputs.<entry-ts>.md` and delegating to
`atomic-planner`. Do not route a `PREFLIGHT: REVISIONS REQUIRED` delta to execution and do not act on
the delta yourself — return it to `atomic-planner` to revise. Any new finding surfaced during
execution starts a new cycle with a fresh `remediation-inputs.<new-ts>.md`; it does not extend the
active plan. See `.claude/agents/orchestrator.md` (`## Remediation Loop Protocol`) and
`.claude/skills/remediation-handoff-atomic-planner/SKILL.md` for the canonical chain and the five
required per-cycle artifacts.
