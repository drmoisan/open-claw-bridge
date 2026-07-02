---
name: worktree-stale-main-merge-base
description: In worktrees, local `main` can be stale; diff-scope tasks must use `git merge-base origin/main HEAD` or confinement checks show false extra files
metadata:
  type: feedback
---

Use `origin/main` (not local `main`) as the merge-base for diff-scope/confinement verification tasks in worktree checkouts.

**Why:** During issue #99 execution (2026-07-02), `git merge-base main HEAD` resolved against a stale local `main` ref that predated merged PRs #97/#98, so the production-confinement diff falsely listed five `src/OpenClaw.MailBridge/**` files from already-merged work. Against `origin/main` the diff was exactly the confined file set and `git log origin/main..HEAD` was empty.

**How to apply:** When a plan task says `git merge-base main HEAD`, first compare with `git merge-base origin/main HEAD`. If they differ, use origin/main as the authoritative mainline, record both results in the evidence artifact, and confirm the extra files belong to commits authored before execution began (`git log --format=%ci <sha>`).
