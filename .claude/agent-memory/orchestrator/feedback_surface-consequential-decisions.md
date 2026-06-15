---
name: surface-consequential-decisions
description: For irreversible or policy-shaping harness/scope decisions, present options and get operator confirmation rather than deciding unilaterally.
metadata:
  type: feedback
---

When an orchestration hits a consequential, hard-to-reverse decision that is NOT already prescribed by the orchestration policy — especially a novel fork that changes the agent harness's own structure, version-control policy, or the coverage/CI gate surface — stop and present the options with a recommendation, then let the operator choose. Proceed unilaterally on low-risk, clearly-aligned defaults AND on every step the policy already defines.

**Do NOT pause for confirmation on policy-defined workflow steps.** Steps the orchestrator policy already prescribes — committing audit/evidence artifacts, opening the PR after the exit gate passes, post-PR CI monitoring, entering/running the remediation loop — execute autonomously. Pausing on these is a defect: the operator stated (Issue #73) these "should always happen without needing my feedback. It is described in the policy." If a policy-defined step keeps triggering a stoppage, that is an architecture gap — open an issue via the normal MCP potential→issue procedure rather than continuing to pause.

**Why:** Confirmation is for genuinely novel/irreversible forks the policy does not cover. During Issue #66 (PR #68), three such *uncovered* forks arose (harness gitignored→tracked; under-scoped Python/TS removal→full removal; hooks tripping the PS coverage gate→documented `.claude/hooks/**` exclusion) and surfacing them was correct. By contrast, opening the PR for Issue #73 after a clean exit gate was already in policy; pausing there wasted a round-trip.

**How to apply:** First ask "is this step prescribed by the orchestration policy?" If yes, just do it. Only if the decision is novel AND consequential, give a short options table with a recommendation and one-line rationale, then wait. This operator responds decisively to crisp option sets but does not want to be asked to authorize steps the policy already mandates. Related: [[harness-governance]].
