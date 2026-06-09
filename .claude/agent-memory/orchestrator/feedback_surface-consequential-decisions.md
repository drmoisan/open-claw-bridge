---
name: surface-consequential-decisions
description: For irreversible or policy-shaping harness/scope decisions, present options and get operator confirmation rather than deciding unilaterally.
metadata:
  type: feedback
---

When an orchestration hits a consequential, hard-to-reverse decision — especially one that changes the agent harness's own structure, version-control policy, or the coverage/CI gate surface — stop and present the options with a recommendation, then let the operator choose. Proceed unilaterally only on low-risk, clearly-aligned defaults.

**Why:** During Issue #66 (PR #68), three such forks arose: (1) the harness was gitignored so most fixes were undeliverable — operator chose to track it; (2) tracking it exposed an under-scoped Python/TS removal — operator chose full removal; (3) newly-tracked hooks tripped the PowerShell coverage gate — operator chose a documented `.claude/hooks/**` coverage-scope exclusion over writing hook tests. Each time the operator engaged and decided; surfacing them was the right call and avoided baking irreversible choices into a large plan.

**How to apply:** Triage decisions into low-risk-default versus consequential. Batch the low-risk ones with stated defaults; for the consequential ones give a short options table with a recommendation and one-line rationale, then wait. This operator responds decisively to crisp option sets.
