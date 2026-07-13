---
name: pr-hook-epic-context-field
description: The PR-author hook's epic-base-branch check reads nested epic_context.integration_branch; a top-level integration_branch alone yields EPIC_BASE_BRANCH_MISMATCH.
metadata:
  type: project
---

For an epic child, the checkpoint must carry a nested `epic_context.integration_branch`, not just a top-level field.

`.claude/hooks/enforce-pr-author-skill.epic-base-branch.ps1` (`Test-EpicBaseBranchOverride`) reads `checkpoint.epic_context.integration_branch`. When `epic_mode == true` but `epic_context.integration_branch` is absent, it denies `gh pr create` with `EPIC_BASE_BRANCH_MISMATCH` even if a top-level `integration_branch` is present. It then requires the command to carry `--base <that exact branch>` (case-sensitive substring match).

**Why:** the child checkpoint I first wrote had only a top-level `integration_branch`; the hook ignores that key.

**How to apply:** in an epic child `artifacts/orchestration/orchestrator-state.json`, always add `"epic_context": { "integration_branch": "<branch>" }` (alongside `"epic_mode": true`) before delegating PR creation, and pass `--base <branch>` to `gh pr create`. Also note the PR-author receipt contract enforced by the same hook: canonical `artifacts/pr_body_<N>.md` (case-sensitive), sibling `artifacts/pr_body_<N>.receipt.json` with fields `number`, `sha256` (lowercase hex of body bytes), `created_at` (UTC, strictly newer than `artifacts/pr_context.summary.txt` last-write). `collect_pr_context` may not persist the summary to the worktree (lesson 4) — generate a faithful one from git so the file exists and its mtime precedes the receipt. Related: [[child-session-agent-availability-and-pr-fallback]], [[pr-author-skill]], [[epic-child-ci-and-merge]].
