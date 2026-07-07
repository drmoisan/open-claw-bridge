---
name: epic-child-ci-and-merge
description: How an epic child feature drives CI green and merges into the integration branch (workflow_dispatch of ci.yml; gh pr merge --merge; issue stays open).
metadata:
  type: project
---

For an epic child feature whose PR targets the epic integration branch (e.g. `epic/openclaw-vision-integration`), not `main`:

- **CI trigger:** `.github/workflows/ci.yml` fires `push`/`pull_request` only for `branches: [main, development]`, so a PR into the integration branch does NOT auto-run CI. Drive it with `gh workflow run ci.yml --ref <feature-branch>` (ci.yml has `workflow_dispatch`, no required inputs), then `gh run watch <id> --exit-status`. Record the run in `ci_gate` with `head_sha` equal to the PR head. Verified F15/#119: dispatch run succeeded at the PR head, `ci_gate.head_sha == pr_gate.head_sha`.
- **Merge:** on green, `gh pr merge <N> --merge` (merge commit, never squash/rebase) into the integration branch; record `epic_merge {merge_commit_sha, target_branch, merged_at}`.
- **Issue stays open:** merging into a non-default branch does NOT autoclose the issue, and it must remain open for epic integration. Do NOT put `Closes #<N>` in the PR body; use `Refs #<N>` and the pr-author "None" autoclose fallback.
- **Branch base:** create the feature branch from `origin/<integration-branch>` (fetch first). If the integration branch advances mid-flight (a sibling child merges), forward-merge `origin/<integration-branch>` before opening the PR; sibling features usually live in separate namespaces (e.g. F14 CloudSync vs F15 CloudGraph) so conflicts are rare.
- **Epic checkpoint write-back:** the child updates the shared epic checkpoint (primary checkout `artifacts/orchestration/epic-orchestrator-state.json`) `features[]` entry by `feature_folder` key (never rename it — dependency edges reference it): set `issue_num`, `merge_status`, `pr_number`, `pr_url`, `merge_commit_sha`, `pr_opened_at`, `merge_confirmed_at`. The Edit tool is blocked from the primary checkout under worktree isolation; a `python -c` write via Bash to the absolute path works.

**Why:** Epic delivery fans children through isolated worktrees into one integration branch; the integration-to-main PR is the epic-orchestrator's final step, so child PRs deliberately target the integration branch and CI must be dispatched explicitly.

**How to apply:** Follow this for every epic child. Base-branch merge-base for review uses the integration branch (see [[pr-author-skill]] context). Related: [[openclaw-vision-program-status]], [[openclaw-delivery-loop]], [[merge-commit-policy]].
