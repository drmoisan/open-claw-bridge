---
name: clean-worktree-before-ready
description: The git worktree must be clean (no uncommitted/untracked changes) before declaring a PR ready to merge.
metadata:
  type: feedback
---

The git worktree MUST always be clean — no modified, staged, or untracked files (`git status --porcelain` empty) — at the moment the orchestrator tells the operator a PR is ready to merge. Do not leave harness/memory/follow-up artifacts uncommitted "to keep the PR scoped"; commit them.

**Why:** During Issue #73 (PR #87) the orchestrator declared the PR merge-ready while leaving agent-memory edits and an issue-#88 promotion record uncommitted, reasoning that they were out of scope for the feature. The operator rejected this: "The worktree must always be clean when you tell me that a PR is ready to merge." A dirty worktree at the ready signal is a defect regardless of scope reasoning.

**How to apply:**
- Before any "ready to merge" statement, run `git status --porcelain` and ensure it is empty.
- This repo bundles agent-memory updates with feature work (e.g. commit `adcde87` added agent memory alongside issue-74 review docs), so committing memory/docs onto the feature branch is consistent with convention — prefer that over leaving them uncommitted.
- Batch all memory/docs writes into a single commit and push once, so the PR head does not churn (each push re-triggers CI; declaring ready requires required checks green on the FINAL head). Note `collect_pr_context` may itself create and push a commit of working-tree changes, which advances the head — re-confirm CI afterward.
- `artifacts/orchestration/orchestrator-state.json` is gitignored, so its edits do not affect worktree cleanliness. Related: [[surface-consequential-decisions]], [[pr-author-skill]].
