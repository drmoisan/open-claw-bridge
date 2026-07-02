---
name: autonomous-finish-sequence
description: After the exit gate passes, run the full finish sequence autonomously — commit+push ALL files, open the PR, remediate CI failures, commit final memories, ensure green CI. Never pause to ask.
metadata:
  type: feedback
---

Once the audit exit gate passes (blocking_count == 0), the orchestrator MUST
complete the entire delivery sequence autonomously, without asking the operator
for permission at any step:

1. Commit AND push every changed/created file (feature code, tests, docs,
   feature folder, evidence, audit artifacts).
2. Open the pull request (author the body via the `pr-author` skill — see
   [[pr-author-skill]]).
3. Monitor required CI checks; if any required check fails, enter and run the
   remediation loop until checks are green.
4. Commit any final memories / follow-up artifacts so the worktree is clean
   (see [[clean-worktree-before-ready]]).
5. Confirm green CI on the final PR head before reporting the PR ready.

**Why:** The operator stated (2026-06-16) that "Per your norms I have not
committed or opened a PR" is wrong — these steps ARE the norm and must happen
without their feedback. This OVERRIDES the generic base-tool guidance "commit or
push only when the user asks." For this operator and repo, finishing the
delivery is policy-defined work, not a decision to confirm. If a base/default
norm conflicts with this, this memory wins.

**How to apply:** Do not end a turn at "shall I commit / open a PR?" after a
passing audit. Just run steps 1-5. The only legitimate stops are a genuinely
novel/irreversible fork the policy does not cover (see
[[surface-consequential-decisions]]) or a hard external blocker (e.g. a
permission-denied path the operator must edit — see
[[env-files-permission-denied-to-tools]]). Related: [[clean-worktree-before-ready]].
