---
name: merge-commit-policy
description: Every PR must be merged into main with a MERGE COMMIT — never squash or rebase merges. gh pr merge <n> --merge only.
metadata:
  type: feedback
---

All merges into `main` MUST use a **merge commit**. Use `gh pr merge <n> --merge`
(equivalently `git merge --no-ff`). Never `--squash` and never `--rebase`.

**Why:** Repo policy requires merge-commit history on `main` (the existing history
uses two-parent `Merge pull request #NN` commits). The operator flagged that squash
merges are prohibited: a squash produces a single-parent commit on `main` that
resembles a direct-to-`main` commit, which is not allowed, and it also leaves the
source branch looking "unmerged" (its commits are not ancestors of `main`).
Direct commits to `main` are likewise prohibited — all changes land via a PR.

**How to apply:**
- When merging any PR: `gh pr merge <number> --merge --delete-branch`. Do not pass
  `--squash` or `--rebase`.
- Never `git commit`/`git push` onto `main` directly; branch, PR, then merge-commit.
- After merging, the source branch is deleted (server-side and local). Related:
  [[clean-worktree-before-ready]], [[autonomous-finish-sequence]].
